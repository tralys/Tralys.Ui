using System.Numerics;
using Bgfx.Net;
using Tralys.Ui.Rendering;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Bgfx;

/// <summary>Draws one <see cref="IRenderedView"/> a frame through bgfx: the view's draw list and the buffers holding its
/// geometry, drawn with what its <see cref="BgfxRenderDevice"/> shares. A renderer serves one view id, once a frame,
/// after the device's <see cref="BgfxRenderDevice.BeginFrame"/>, so each view takes a renderer of its own. The host
/// points the view id at its target, again after a bgfx reset, which points every view back at the back buffer, and
/// clears it, except through <see cref="RenderTo"/>, which does both.</summary>
public sealed unsafe class BgfxViewRenderer : IDisposable
{
    const ulong PremultipliedOver =
        (ulong)StateFlags.BlendOne | ((ulong)StateFlags.BlendInvSrcAlpha << 4);
    const ulong DrawState =
        (ulong)StateFlags.WriteRgb
        | (ulong)StateFlags.WriteA
        | PremultipliedOver
        | (PremultipliedOver << 8);

    readonly BgfxRenderDevice _device;
    DynamicVertexBufferHandle _vertexBuffer;
    DynamicIndexBufferHandle _indexBuffer;
    uint _vertexCapacity;
    uint _indexCapacity;
    long _uploadedGeneration = -1;
    long _submittedFrame = -1;
    int[] _layerTargets = new int[4];
    bool[] _fallenShows = new bool[4];
    Vector2[] _layerOrigins = new Vector2[4];
    BgfxRenderTarget? _drawnTarget;
    bool _missingPixels;
    int _unboundBatches;
    bool _disposed;

    /// <param name="device">What it draws with, shared with the other views' renderers.</param>
    public BgfxViewRenderer(BgfxRenderDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        ObjectDisposedException.ThrowIf(device.IsDisposed, device);
        _device = device;
        DrawList = new DrawList(device.Ramps);
    }

    /// <summary>The list the view renders into each frame, before <see cref="Submit"/>.</summary>
    public DrawList DrawList { get; }

    /// <summary>How many of the last <see cref="Submit"/>'s layers drew without their effect, for want of a layer view
    /// or of room in a target; their content drew straight into their parents. A host shows or logs it, since nothing
    /// else tells.</summary>
    public int DroppedLayers { get; private set; }

    /// <summary>How many layer views the last frame wanted, for a host that grows what it passes to
    /// <see cref="Submit"/>; a layer can also drop for want of room in a target, which more views do not help.</summary>
    public int LayerPasses { get; private set; }

    /// <summary>How many draw calls the last <see cref="Submit"/> made, its layers' passes included: what the view costs
    /// the GPU each frame, beside a host's own draws.</summary>
    public int DrawCalls { get; private set; }

    /// <summary>Bytes of geometry the last <see cref="Submit"/> sent the GPU: none when the list was resubmitted as it
    /// was, since the buffers still hold it.</summary>
    public int UploadedBytes { get; private set; }

    /// <summary>Brings <paramref name="view"/>'s frame up to date and submits it, as <see cref="Submit"/> does.</summary>
    [FramePath]
    public void Render(
        IRenderedView view,
        ushort viewId,
        ushort firstLayerView = 0,
        ushort layerViews = 0
    )
    {
        ArgumentNullException.ThrowIfNull(view);
        ObjectDisposedException.ThrowIf(_disposed, this);
        view.Render(DrawList);
        Submit(viewId, view.PixelWidth, view.PixelHeight, firstLayerView, layerViews);
    }

    /// <summary>Brings <paramref name="view"/>'s frame up to date and, when anything in it changed, draws it into
    /// <paramref name="target"/> over a transparent clear, as <see cref="Submit"/> draws.</summary>
    /// <remarks>While nothing changed, the target keeps its pixels and no view id is touched, so a host handing out ids
    /// moves on only when this returns true. Views run in id order: <paramref name="viewId"/> must sort before any view
    /// sampling the target, which otherwise shows the last frame drawn.</remarks>
    /// <param name="view">The view, sized to the target in pixels.</param>
    /// <param name="target">What it draws into.</param>
    /// <param name="viewId">The view id drawing into the target.</param>
    /// <param name="firstLayerView">The first of the views its layers render on.</param>
    /// <param name="layerViews">How many views its layers may take.</param>
    /// <param name="force">Whether to draw though the view did not change, because a texture it draws has new pixels,
    /// such as the target of a live scene.</param>
    /// <returns>Whether it drew.</returns>
    /// <exception cref="ArgumentException">The view's pixel size is not the target's.</exception>
    /// <exception cref="ObjectDisposedException">The target is disposed.</exception>
    [FramePath]
    public bool RenderTo(
        IRenderedView view,
        BgfxRenderTarget target,
        ushort viewId,
        ushort firstLayerView = 0,
        ushort layerViews = 0,
        bool force = false
    )
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_disposed, this);
        ObjectDisposedException.ThrowIf(!target.FrameBuffer.Valid, target);
        if (view.PixelWidth != target.Width || view.PixelHeight != target.Height)
            throw new ArgumentException("The view's pixel size is not the target's.", nameof(view));

        var rebuilt = view.Render(DrawList);
        if (!rebuilt && !force && !PixelsArrived() && ReferenceEquals(target, _drawnTarget))
            return false;

        Bx.SetViewFrameBuffer(viewId, target.FrameBuffer);
        Bx.SetViewClear(viewId, (ushort)ClearFlags.Color, 0, 1f, 0);
        Submit(viewId, target.Width, target.Height, firstLayerView, layerViews);
        _drawnTarget = target;
        return true;
    }

    /// <summary>Submits what was rendered into <see cref="DrawList"/> this frame to <paramref name="viewId"/>, at
    /// <paramref name="width"/> by <paramref name="height"/> pixels, rendering its layers on the
    /// <paramref name="layerViews"/> views from <paramref name="firstLayerView"/>, which must all run before
    /// <paramref name="viewId"/>.</summary>
    /// <remarks>It takes one view per pass, packing the layers of a pass into one target, so the views a frame takes
    /// grow with how deeply its layers nest. A layer whose pass is beyond them draws straight into its parent, without
    /// its effect, and counts in <see cref="DroppedLayers"/>. Clips other than axis-aligned rectangles are layers too,
    /// so one that is dropped clips only to its bounding box. The geometry stays in the renderer's own buffers, so a
    /// list resubmitted as it was uploads nothing.</remarks>
    /// <exception cref="InvalidOperationException">The renderer already submitted since the device's last
    /// <see cref="BgfxRenderDevice.BeginFrame"/>.</exception>
    [FramePath]
    public void Submit(
        ushort viewId,
        int width,
        int height,
        ushort firstLayerView,
        ushort layerViews
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ObjectDisposedException.ThrowIf(_device.IsDisposed, _device);
        if (_submittedFrame == _device.Frame)
            throw new InvalidOperationException(
                "A renderer submits once a frame; start each frame with the device's BeginFrame."
            );
        _submittedFrame = _device.Frame;

        var list = DrawList;
        var view = Matrix4x4.Identity;
        var projection = Matrix4x4.CreateOrthographicOffCenter(0f, width, height, 0f, 0f, -1f);
        Bx.SetViewRect(viewId, 0, 0, (ushort)width, (ushort)height, 0f, 1f);
        Bx.SetViewTransform(viewId, &view, &projection);
        Bx.SetViewMode(viewId, ViewMode.Sequential);
        Bx.Touch(viewId);
        _device.UploadRamps();
        _missingPixels = false;

        var layers = list.Layers;
        if (_layerTargets.Length < layers.Length)
        {
            var length = Math.Max(_layerTargets.Length * 2, layers.Length);
            Array.Resize(ref _layerTargets, length);
            Array.Resize(ref _fallenShows, length);
            Array.Resize(ref _layerOrigins, length);
        }
        var passes = 0;
        for (var i = 0; i < layers.Length; i++)
        {
            _layerTargets[i] = -1;
            _fallenShows[i] = false;
            passes = Math.Max(passes, layers[i].Pass + 1);
        }
        LayerPasses = passes;

        for (var pass = 0; pass < Math.Min(passes, (int)layerViews); pass++)
        {
            if (Pack(layers, pass, width) is not (>= 0 and var index))
                continue;

            var surface = _device.Target(index);
            var layerView = (ushort)(firstLayerView + pass);
            var layerProjection = Matrix4x4.CreateOrthographicOffCenter(
                0f,
                surface.Width,
                surface.Height,
                0f,
                0f,
                -1f
            );
            Bx.SetViewFrameBuffer(layerView, surface.FrameBuffer);
            Bx.SetViewRect(layerView, 0, 0, (ushort)surface.Width, (ushort)surface.Height, 0f, 1f);
            Bx.SetViewClear(layerView, (ushort)ClearFlags.Color, 0, 1f, 0);
            Bx.SetViewTransform(layerView, &view, &layerProjection);
            Bx.SetViewMode(layerView, ViewMode.Sequential);
            Bx.Touch(layerView);
        }

        var fallen = false;
        var dropped = 0;
        for (var i = 0; i < layers.Length; i++)
        {
            if (layers[i].Pass < 0 || _layerTargets[i] >= 0)
                continue;
            dropped++;
            fallen |= layers[i].Input < 0;
        }
        DroppedLayers = dropped;

        UploadedBytes = 0;
        DrawCalls = 0;
        if (list.Indices.IsEmpty)
            return;
        if (fallen)
            ResolveFallbacks(layers, list.Batches);
        Upload(list);

        var device = _device;
        foreach (var batch in list.Batches)
        {
            var target = (int)batch.Target;
            if (target >= 0 && _layerTargets[target] < 0)
            {
                if (!_fallenShows[target])
                    continue;
                while (target >= 0 && _layerTargets[target] < 0)
                    target = layers[target].Parent;
            }

            if (batch.Source >= 0 && _layerTargets[batch.Source] < 0)
                continue;

            var texture = device.White;
            var array = device.WhiteArray;
            var glyphs = device.White;
            var paint = device.White;
            var paintArray = device.WhiteArray;
            Bind(batch.Texture, ref texture, ref array);
            Bind(batch.ArrayTexture, ref texture, ref array);
            Bind(batch.GlyphTexture, ref glyphs, ref array);
            Bind(batch.PaintTexture, ref paint, ref paintArray);

            // What is set on the encoder stays for the next submit, so a skipped batch must have set nothing.
            var submitView = viewId;
            if (target >= 0)
            {
                submitView = (ushort)(firstLayerView + layers[target].Pass);
                var origin = _layerOrigins[target];
                var model = Matrix4x4.CreateTranslation(
                    origin.X - layers[target].X,
                    origin.Y - layers[target].Y,
                    0f
                );
                Bx.SetTransform(&model, 1);
            }

            var layerTexture = device.White;
            if (batch.Source >= 0)
            {
                var surface = device.Target(_layerTargets[batch.Source]);
                layerTexture = surface.Color;
                var scale = new Vector4(1f / surface.Width, 1f / surface.Height, 0f, 0f);
                var origin = _layerOrigins[batch.Source];
                var region = new Vector4(
                    origin.X,
                    origin.Y,
                    layers[batch.Source].Width,
                    layers[batch.Source].Height
                );
                Bx.SetUniform(device.LayerUniform, &scale, 1);
                Bx.SetUniform(device.LayerRegionUniform, &region, 1);
            }

            Bx.SetDynamicVertexBuffer(
                0,
                _vertexBuffer,
                (uint)batch.FirstVertex,
                (uint)batch.VertexCount
            );
            Bx.SetDynamicIndexBuffer(_indexBuffer, (uint)batch.FirstIndex, (uint)batch.IndexCount);
            Bx.SetTexture(0, device.TextureSampler, texture, uint.MaxValue);
            Bx.SetTexture(2, device.ArraySampler, array, uint.MaxValue);
            Bx.SetTexture(6, device.GlyphSampler, glyphs, uint.MaxValue);
            if (batch.Rare)
            {
                Bx.SetTexture(1, device.RampSampler, device.RampTexture, uint.MaxValue);
                Bx.SetTexture(3, device.LayerSampler, layerTexture, uint.MaxValue);
                Bx.SetTexture(4, device.PaintSampler, paint, uint.MaxValue);
                Bx.SetTexture(5, device.PaintArraySampler, paintArray, uint.MaxValue);
            }
            Bx.SetState(DrawState, 0);
            Bx.Submit(
                submitView,
                batch.Rare ? device.RareProgram : device.Program,
                0,
                byte.MaxValue
            );
            DrawCalls++;
        }
        if (_missingPixels)
            _unboundBatches = UnboundBatches(list.Batches);
    }

    [FramePath]
    void Upload(DrawList list)
    {
        if (list.Generation == _uploadedGeneration)
        {
            UploadPatches(list);
            return;
        }

        list.ClearPatchedRanges();
        var vertices = list.Vertices;
        var indices = list.Indices;
        if ((uint)vertices.Length > _vertexCapacity)
        {
            if (_vertexCapacity > 0)
                Bx.DestroyDynamicVertexBuffer(_vertexBuffer);
            _vertexCapacity = Grown(_vertexCapacity, (uint)vertices.Length);
            fixed (VertexLayout* layout = &_device.Layout)
                _vertexBuffer = Bx.CreateDynamicVertexBuffer(
                    _vertexCapacity,
                    layout,
                    (ushort)BufferFlags.None
                );
        }
        if ((uint)indices.Length > _indexCapacity)
        {
            if (_indexCapacity > 0)
                Bx.DestroyDynamicIndexBuffer(_indexBuffer);
            _indexCapacity = Grown(_indexCapacity, (uint)indices.Length);
            _indexBuffer = Bx.CreateDynamicIndexBuffer(_indexCapacity, (ushort)BufferFlags.None);
        }

        Bx.UpdateDynamicVertexBuffer(_vertexBuffer, 0, Bx.Copy(vertices));
        Bx.UpdateDynamicIndexBuffer(_indexBuffer, 0, Bx.Copy(indices));
        _uploadedGeneration = list.Generation;
        UploadedBytes = vertices.Length * sizeof(UiVertex) + indices.Length * sizeof(ushort);
    }

    // One update per range: a single one spanning scattered leaves would resend most of the frame.
    [FramePath]
    void UploadPatches(DrawList list)
    {
        var vertices = list.Vertices;
        foreach (var range in list.PatchedRanges)
        {
            Bx.UpdateDynamicVertexBuffer(
                _vertexBuffer,
                (uint)range.First,
                Bx.Copy(vertices.Slice(range.First, range.Count))
            );
            UploadedBytes += range.Count * sizeof(UiVertex);
        }
        list.ClearPatchedRanges();
    }

    static uint Grown(uint capacity, uint needed) =>
        Math.Max(needed, Math.Max(capacity * 2, 1024u));

    [FramePath]
    bool PixelsArrived() => _missingPixels && UnboundBatches(DrawList.Batches) < _unboundBatches;

    // Reading a handle asks a texture for its pixels, as drawing it does.
    [FramePath]
    static int UnboundBatches(ReadOnlySpan<DrawBatch> batches)
    {
        var count = 0;
        foreach (ref readonly var batch in batches)
        {
            if (
                batch.Texture is IBgfxTexture { Handle.Valid: false }
                || batch.ArrayTexture is IBgfxTexture { Handle.Valid: false }
                || batch.GlyphTexture is IBgfxTexture { Handle.Valid: false }
                || batch.PaintTexture is IBgfxTexture { Handle.Valid: false }
            )
                count++;
        }
        return count;
    }

    // A texture without pixels yet draws nothing, and what shares its batch still draws.
    [FramePath]
    void Bind(RenderTexture? texture, ref TextureHandle plain, ref TextureHandle array)
    {
        if (texture is null)
            return;
        if (texture is not IBgfxTexture bound)
            throw NotBindable(texture);
        var isArray = texture is RenderTextureArray;
        var handle = bound.Handle;
        if (!handle.Valid)
        {
            _missingPixels = true;
            handle = isArray ? _device.ClearArray : _device.Clear;
        }
        if (isArray)
            array = handle;
        else
            plain = handle;
    }

    static InvalidOperationException NotBindable(RenderTexture texture) =>
        new($"{texture.GetType().Name} is not an {nameof(IBgfxTexture)}.");

    // Each packed layer reads only its own box, a pixel apart, so a blur never reaches a neighbour.
    [FramePath]
    int Pack(ReadOnlySpan<DrawLayer> layers, int pass, int width)
    {
        const int Gap = 1;
        var maxTargetSize = _device.MaxTargetSize;
        var widest = 0;
        for (var i = 0; i < layers.Length; i++)
        {
            if (layers[i].Pass == pass)
                widest = Math.Max(widest, layers[i].Width + Gap);
        }
        if (widest == 0)
            return -1;

        var shelf = Math.Min(Math.Max(width, widest), maxTargetSize);
        var x = 0;
        var y = 0;
        var row = 0;
        var right = 0;
        var placed = false;
        for (var i = 0; i < layers.Length; i++)
        {
            var layer = layers[i];
            if (layer.Pass != pass)
                continue;

            var w = layer.Width + Gap;
            var h = layer.Height + Gap;
            if (x > 0 && x + w > shelf)
            {
                y += row;
                x = 0;
                row = 0;
            }
            if (w > maxTargetSize || y + h > maxTargetSize)
                continue;

            _layerOrigins[i] = new Vector2(x, y);
            _layerTargets[i] = int.MaxValue;
            placed = true;
            x += w;
            row = Math.Max(row, h);
            right = Math.Max(right, x);
        }

        var index = placed ? _device.AcquireTarget(right, y + row) : -1;
        for (var i = 0; i < layers.Length; i++)
        {
            if (_layerTargets[i] == int.MaxValue)
                _layerTargets[i] = index;
        }
        return index;
    }

    // A fallen layer shows only where its composite would have, so one culled or inside a hidden parent draws nothing.
    [FramePath]
    void ResolveFallbacks(ReadOnlySpan<DrawLayer> layers, ReadOnlySpan<DrawBatch> batches)
    {
        foreach (ref readonly var batch in batches)
        {
            var source = batch.Source;
            if (
                source >= 0
                && _layerTargets[source] < 0
                && layers[source] is { Pass: >= 0, Input: < 0 } layer
                && batch.Target == layer.Parent
            )
                _fallenShows[source] = true;
        }

        // A layer begins after its parent, so the parent's answer is final by the time it is read.
        for (var i = 0; i < layers.Length; i++)
        {
            var parent = layers[i].Parent;
            if (_fallenShows[i] && parent >= 0 && _layerTargets[parent] < 0)
                _fallenShows[i] = _fallenShows[parent];
        }
    }

    /// <summary>Destroys its buffers; the device and what it shares stay the caller's.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_indexCapacity > 0)
            Bx.DestroyDynamicIndexBuffer(_indexBuffer);
        if (_vertexCapacity > 0)
            Bx.DestroyDynamicVertexBuffer(_vertexBuffer);
    }
}

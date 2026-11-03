using Bgfx.Net;
using Tralys.Ui.Rendering;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Bgfx;

/// <summary>What every <see cref="BgfxViewRenderer"/> on one bgfx device shares: the Tralys.Ui programs, the gradient
/// ramps and their texture, the textures a texture without pixels draws as, and the targets layers render into. The
/// host makes one after bgfx starts, calls <see cref="BeginFrame"/> once a frame before any view renders, and disposes
/// it after its renderers and before bgfx shuts down.</summary>
public sealed unsafe class BgfxRenderDevice : IDisposable
{
    const long EvictAfter = 600;

    readonly GradientRampAtlas _ramps;
    readonly ushort _maxTargetSize;
    VertexLayout _layout;
    LayerTarget[] _targets = new LayerTarget[4];
    int _targetCount;
    bool _disposed;

    /// <param name="rampRows">How many gradients one frame of every view together can draw.</param>
    /// <exception cref="NotSupportedException">No shaders ship for the running renderer.</exception>
    public BgfxRenderDevice(int rampRows = GradientRampAtlas.DefaultRows)
    {
        _ramps = new GradientRampAtlas(rampRows);
        var rows = checked((ushort)rampRows);

        fixed (VertexLayout* layout = &_layout)
        {
            Bx.VertexLayoutBegin(layout, Bx.GetRendererType());
            Bx.VertexLayoutAdd(layout, Attrib.Position, 2, AttribType.Float, false, false);
            Bx.VertexLayoutAdd(layout, Attrib.TexCoord0, 4, AttribType.Float, false, false);
            Bx.VertexLayoutAdd(layout, Attrib.TexCoord1, 4, AttribType.Float, false, false);
            Bx.VertexLayoutAdd(layout, Attrib.TexCoord2, 4, AttribType.Float, false, false);
            Bx.VertexLayoutAdd(layout, Attrib.TexCoord3, 4, AttribType.Float, false, false);
            Bx.VertexLayoutAdd(layout, Attrib.TexCoord4, 4, AttribType.Float, false, false);
            Bx.VertexLayoutAdd(layout, Attrib.TexCoord5, 4, AttribType.Float, false, false);
            Bx.VertexLayoutAdd(layout, Attrib.TexCoord6, 4, AttribType.Float, false, false);
            Bx.VertexLayoutAdd(layout, Attrib.TexCoord7, 4, AttribType.Float, false, false);
            Bx.VertexLayoutAdd(layout, Attrib.Color2, 4, AttribType.Float, false, false);
            Bx.VertexLayoutAdd(layout, Attrib.Color0, 4, AttribType.Uint8, true, false);
            Bx.VertexLayoutAdd(layout, Attrib.Color1, 4, AttribType.Uint8, true, false);
            Bx.VertexLayoutAdd(layout, Attrib.Color3, 4, AttribType.Float, false, false);
            Bx.VertexLayoutAdd(layout, Attrib.Tangent, 3, AttribType.Float, false, false);
            Bx.VertexLayoutEnd(layout);
        }
        if (_layout.Stride != sizeof(UiVertex))
            throw new InvalidOperationException(
                $"The vertex layout is {_layout.Stride} bytes but UiVertex is {sizeof(UiVertex)}."
            );

        Program = CreateProgram("tralys");
        RareProgram = CreateProgram("tralys_rare");
        TextureSampler = Bx.CreateUniform("s_texture", UniformType.Sampler, 1);
        RampSampler = Bx.CreateUniform("s_ramp", UniformType.Sampler, 1);
        ArraySampler = Bx.CreateUniform("s_textureArray", UniformType.Sampler, 1);
        GlyphSampler = Bx.CreateUniform("s_glyph", UniformType.Sampler, 1);
        LayerSampler = Bx.CreateUniform("s_layer", UniformType.Sampler, 1);
        PaintSampler = Bx.CreateUniform("s_paint", UniformType.Sampler, 1);
        PaintArraySampler = Bx.CreateUniform("s_paintArray", UniformType.Sampler, 1);
        LayerUniform = Bx.CreateUniform("u_layer", UniformType.Vec4, 1);
        LayerRegionUniform = Bx.CreateUniform("u_layerRegion", UniformType.Vec4, 1);
        _maxTargetSize = (ushort)Math.Clamp(Bx.GetCaps()->limits.MaxTextureSize, 64u, 16384u);

        ReadOnlySpan<byte> white = [255, 255, 255, 255];
        White = Bx.CreateTexture2d(1, 1, false, 1, TextureFormat.RGBA8, 0, Bx.Copy(white), 0);
        ReadOnlySpan<byte> whiteLayers = [255, 255, 255, 255, 255, 255, 255, 255];
        WhiteArray = Bx.CreateTexture2d(
            1,
            1,
            false,
            BgfxTextureArray.MinLayers,
            TextureFormat.RGBA8,
            0,
            Bx.Copy(whiteLayers),
            0
        );
        ReadOnlySpan<byte> clear = [0, 0, 0, 0];
        Clear = Bx.CreateTexture2d(1, 1, false, 1, TextureFormat.RGBA8, 0, Bx.Copy(clear), 0);
        ReadOnlySpan<byte> clearLayers = [0, 0, 0, 0, 0, 0, 0, 0];
        ClearArray = Bx.CreateTexture2d(
            1,
            1,
            false,
            BgfxTextureArray.MinLayers,
            TextureFormat.RGBA8,
            0,
            Bx.Copy(clearLayers),
            0
        );
        RampTexture = Bx.CreateTexture2d(
            GradientRampAtlas.Width,
            rows,
            false,
            1,
            TextureFormat.RGBA8,
            (ulong)(SamplerFlags.UClamp | SamplerFlags.VClamp),
            null,
            0
        );
    }

    internal long Frame { get; private set; }

    internal bool IsDisposed => _disposed;

    internal GradientRampAtlas Ramps => _ramps;

    internal ProgramHandle Program { get; }

    internal ProgramHandle RareProgram { get; }

    internal UniformHandle TextureSampler { get; }

    internal UniformHandle RampSampler { get; }

    internal UniformHandle ArraySampler { get; }

    internal UniformHandle GlyphSampler { get; }

    internal UniformHandle LayerSampler { get; }

    internal UniformHandle PaintSampler { get; }

    internal UniformHandle PaintArraySampler { get; }

    internal UniformHandle LayerUniform { get; }

    internal UniformHandle LayerRegionUniform { get; }

    internal TextureHandle White { get; }

    internal TextureHandle WhiteArray { get; }

    internal TextureHandle Clear { get; }

    internal TextureHandle ClearArray { get; }

    internal TextureHandle RampTexture { get; }

    internal ushort MaxTargetSize => _maxTargetSize;

    internal ref VertexLayout Layout => ref _layout;

    /// <summary>Starts a frame: the gradient rows and layer targets the views drew with before it are free for the views
    /// that render after it, which a view whose list is resubmitted as it was takes back first.</summary>
    [FramePath]
    public void BeginFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Frame++;
        _ramps.BeginFrame();
        for (var i = _targetCount - 1; i >= 0; i--)
        {
            ref var target = ref _targets[i];
            target.Busy = false;
            if (Frame - target.LastUse < EvictAfter)
                continue;

            target.Surface.Dispose();
            target = _targets[--_targetCount];
            _targets[_targetCount] = default;
        }
    }

    static ProgramHandle CreateProgram(string name)
    {
        var profile = Bx.GetRendererType() switch
        {
            RendererType.Vulkan or RendererType.Noop => "spirv",
            RendererType.Direct3D11 => "s_5_0",
            RendererType.Metal => "metal",
            RendererType.WebGPU => "wgsl",
            var renderer => throw new NotSupportedException(
                $"Tralys.Ui ships no shaders for the {renderer} renderer."
            ),
        };
        var vertex = Bx.CreateShader(Bx.Copy<byte>(ShippedShader(profile, $"vs_{name}.bin")));
        var fragment = Bx.CreateShader(Bx.Copy<byte>(ShippedShader(profile, $"fs_{name}.bin")));
        return Bx.CreateProgram(vertex, fragment, true);
    }

    static byte[] ShippedShader(string profile, string name)
    {
        using var stream =
            typeof(BgfxRenderDevice).Assembly.GetManifestResourceStream($"{profile}/{name}")
            ?? throw new InvalidOperationException($"{profile}/{name} is not embedded.");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    [FramePath]
    internal void UploadRamps()
    {
        if (!_ramps.TryTakeDirtyRows(out var firstRow, out var rowCount))
            return;

        const int rowBytes = GradientRampAtlas.Width * 4;
        var texels = _ramps.Pixels.Slice(firstRow * rowBytes, rowCount * rowBytes);
        Bx.UpdateTexture2d(
            RampTexture,
            0,
            0,
            0,
            (ushort)firstRow,
            GradientRampAtlas.Width,
            (ushort)rowCount,
            Bx.Copy(texels),
            rowBytes
        );
    }

    internal BgfxRenderTarget Target(int index) => _targets[index].Surface;

    // Taken until the next frame: another renderer's layer views may run before this renderer's view samples it.
    [FramePath]
    internal int AcquireTarget(int width, int height)
    {
        if (width > _maxTargetSize || height > _maxTargetSize)
            return -1;

        var best = -1;
        var smallest = int.MaxValue;
        for (var i = 0; i < _targetCount; i++)
        {
            var surface = _targets[i].Surface;
            if (_targets[i].Busy || surface.Width < width || surface.Height < height)
                continue;
            var area = surface.Width * surface.Height;
            if (area < smallest)
            {
                best = i;
                smallest = area;
            }
        }

        if (best < 0)
        {
            const int Step = 64;
            var w = (ushort)Math.Min(_maxTargetSize, (width + Step - 1) / Step * Step);
            var h = (ushort)Math.Min(_maxTargetSize, (height + Step - 1) / Step * Step);
            if (BgfxRenderTarget.TryCreate(w, h) is not { } created)
                return -1;

            if (_targetCount == _targets.Length)
                Array.Resize(ref _targets, _targets.Length * 2);
            best = _targetCount++;
            _targets[best] = new LayerTarget { Surface = created };
        }

        ref var chosen = ref _targets[best];
        chosen.Busy = true;
        chosen.LastUse = Frame;
        return best;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        for (var i = 0; i < _targetCount; i++)
            _targets[i].Surface.Dispose();
        _targetCount = 0;
        Bx.DestroyTexture(RampTexture);
        Bx.DestroyTexture(ClearArray);
        Bx.DestroyTexture(Clear);
        Bx.DestroyTexture(WhiteArray);
        Bx.DestroyTexture(White);
        Bx.DestroyUniform(LayerRegionUniform);
        Bx.DestroyUniform(LayerUniform);
        Bx.DestroyUniform(PaintArraySampler);
        Bx.DestroyUniform(PaintSampler);
        Bx.DestroyUniform(LayerSampler);
        Bx.DestroyUniform(GlyphSampler);
        Bx.DestroyUniform(ArraySampler);
        Bx.DestroyUniform(RampSampler);
        Bx.DestroyUniform(TextureSampler);
        Bx.DestroyProgram(RareProgram);
        Bx.DestroyProgram(Program);
    }

    struct LayerTarget
    {
        public BgfxRenderTarget Surface;
        public bool Busy;
        public long LastUse;
    }
}

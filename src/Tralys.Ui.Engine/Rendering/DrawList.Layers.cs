using System.Numerics;

namespace Tralys.Ui.Rendering;

public sealed partial class DrawList
{
    /// <summary>Draws what follows, until the matching pop, into an offscreen layer composited at
    /// <paramref name="opacity"/>, so overlapping primitives fade as one. A layer of a single primitive folds the
    /// opacity into it instead.</summary>
    public void PushLayer(float opacity)
    {
        Trace?.Layer(opacity);
        opacity = Math.Clamp(opacity, 0f, 1f);
        Save();
        if (opacity >= 1f || !(_state.Opacity > 0f))
            return;
        if (!(opacity > 0f))
        {
            _state.Opacity = 0f;
            return;
        }

        BeginLayer(LayerKind.Opacity).Opacity = opacity;
    }

    /// <summary>Draws what follows, until the matching pop, into an offscreen layer composited through the alpha of
    /// <paramref name="mask"/> laid over <paramref name="bounds"/> in local space.</summary>
    public void PushOpacityMask(Paint mask, Rect bounds)
    {
        Trace?.Mask(mask, bounds);
        Save();
        if (!(_state.Opacity > 0f))
            return;
        if (!mask.IsVisible || (mask.Kind == PaintKind.Image && ImageArea(mask, bounds).IsEmpty))
        {
            _state.Opacity = 0f;
            return;
        }

        ref var layer = ref BeginLayer(LayerKind.Mask);
        layer.MaskPaint = mask;
        layer.MaskBounds = bounds;
    }

    /// <summary>Draws what follows, until the matching pop, into an offscreen layer composited over its own
    /// <paramref name="shadow"/>.</summary>
    public void PushDropShadow(DropShadow shadow)
    {
        Trace?.Shadow(shadow);
        Save();
        var alpha = Math.Clamp(shadow.Opacity, 0f, 1f) * _state.Opacity;
        if (shadow.Color.A == 0 || !(alpha > 0f) || _state.Clip.IsEmpty)
            return;

        var transform = _state.Transform;
        var scale = UniformScale(transform);
        var angle = shadow.Direction * (MathF.PI / 180f);
        var offset = Vector2.TransformNormal(
            new Vector2(MathF.Cos(angle), -MathF.Sin(angle)) * shadow.ShadowDepth,
            transform
        );
        var sigma = MathF.Max(0f, shadow.BlurRadius) / 3f * scale;
        var reach = MathF.Ceiling(sigma * 3f) + 1f;

        ref var layer = ref BeginLayer(LayerKind.Shadow);
        layer.ShadowOffset = offset;
        layer.ShadowSigma = sigma;
        layer.ShadowReach = reach;
        layer.ShadowColor = shadow.Color.ToPremultipliedRgba(alpha);
        var grow = reach + MathF.Max(MathF.Abs(offset.X), MathF.Abs(offset.Y));
        _state.Clip = _state.Clip.Inflate(grow, grow).Intersect(_view.Inflate(grow, grow));
    }

    ref LayerRecord BeginLayer(LayerKind kind)
    {
        var index = ReserveLayer();
        ref var layer = ref _layerRecords[index];
        layer = default;
        layer.Kind = kind;
        layer.Opacity = 1f;
        layer.FirstBatch = _batches.Count;
        _layers[index] = new DrawLayer(0, 0, 0, 0, -1, _state.Layer);

        _state.Layer = (short)index;
        _state.Opacity = 1f;
        return ref layer;
    }

    void EndLayer(int index, Rect workingClip)
    {
        var layer = _layerRecords[index];
        if (layer.Primitives == 0)
            return;
        if (layer.Kind == LayerKind.Opacity && layer.Primitives == 1 && TryFold(index, layer))
        {
            Raise(_state.Layer, layer.Pass);
            return;
        }

        var area = layer
            .Content.Inflate(layer.ShadowReach, layer.ShadowReach)
            .Intersect(workingClip);

        var left = (int)MathF.Floor(area.X);
        var top = (int)MathF.Floor(area.Y);
        var right = (int)MathF.Ceiling(area.Right);
        var bottom = (int)MathF.Ceiling(area.Bottom);
        if (area.IsEmpty || right <= left || bottom <= top)
            return;

        var bounds = Rect.FromEdges(left, top, right, bottom);
        _layers[index] = new DrawLayer(
            left,
            top,
            right - left,
            bottom - top,
            layer.Pass,
            _state.Layer
        );
        Matrix3x2.Invert(_state.Transform, out var toLocal);

        var pixels = new Rect(bounds.Size);
        var last =
            layer.Kind == LayerKind.Shadow ? EmitShadow(index, layer, bounds, toLocal) : layer.Pass;
        Raise(_state.Layer, last + 1);
        if (layer.Kind == LayerKind.PathClip)
        {
            LandPathClip(layer, bounds);
            return;
        }

        UiVertex vertex = default;
        var mask = ResolveFill(
            ref vertex,
            layer.MaskPaint.IsVisible ? layer.MaskPaint : Color.FromRgb(255, 255, 255),
            layer.MaskBounds,
            Matrix3x2.Identity
        );
        vertex.Params.X = UiPrimitive.Layer;
        if (layer.Kind == LayerKind.ShapeClip)
        {
            vertex.Params.X = UiPrimitive.ClipLayer;
            vertex.Bounds = ToVector(layer.MaskBounds);
            vertex.Shape = ToVector(layer.ClipRadius);
        }
        ref var record = ref _layerRecords[index];
        record.CompositeFill = vertex.Fill;
        record.CompositeVertex = _vertexCount;
        vertex.Fill = Scale(vertex.Fill, layer.Opacity);
        EmitComposite(
            bounds,
            pixels,
            Vector2.Zero,
            _state.Clip,
            ref vertex,
            _state.Layer,
            (short)index,
            toLocal,
            mask
        );
        record.CompositeVertices = _vertexCount - record.CompositeVertex;
    }

    internal int Layer => _state.Layer;

    internal bool PatchLayerOpacity(int layer, float opacity)
    {
        if ((uint)layer >= (uint)_layerCount || !(opacity > 0f) || opacity >= 1f)
            return false;
        ref var record = ref _layerRecords[layer];
        if (record.Kind != LayerKind.Opacity || record.Folded)
            return false;
        record.Opacity = opacity;
        if (record.CompositeVertices == 0)
            return true;
        var fill = Scale(record.CompositeFill, opacity);
        foreach (
            ref var vertex in _vertices.AsSpan(record.CompositeVertex, record.CompositeVertices)
        )
            vertex.Fill = fill;
        _patched.Add(new VertexRange(record.CompositeVertex, record.CompositeVertices));
        return true;
    }

    // A wide blur hides a coarser target, so it blurs there and is scaled back up as it lands.
    int EmitShadow(int index, in LayerRecord layer, Rect bounds, in Matrix3x2 toLocal)
    {
        var sigma = layer.ShadowSigma;
        var factor =
            ShadowDownsample > 0 ? ShadowDownsample
            : sigma >= QuarterResolutionSigma ? 4
            : sigma >= HalfResolutionSigma ? 2
            : 1;
        var coarse = Rect.FromEdges(
            MathF.Floor(bounds.X / factor),
            MathF.Floor(bounds.Y / factor),
            MathF.Ceiling(bounds.Right / factor),
            MathF.Ceiling(bounds.Bottom / factor)
        );
        var coarsePixels = new Rect(coarse.Size);
        var covered = new Rect(
            coarse.X * factor,
            coarse.Y * factor,
            coarse.Width * factor,
            coarse.Height * factor
        );

        var across = ReserveLayer();
        _layers[across] = new DrawLayer(
            (int)coarse.X,
            (int)coarse.Y,
            (int)coarse.Width,
            (int)coarse.Height,
            layer.Pass + 1,
            _state.Layer,
            index
        );
        UiVertex vertex = default;
        vertex.Fill = uint.MaxValue;
        vertex.Shape = new Vector4(1f, 0f, sigma, factor >= 4 ? factor * 0.25f : 0f);
        vertex.Params = new Vector4(UiPrimitive.Blur, UiPaint.Solid, 0f, 0f);
        EmitComposite(
            coarse,
            new Rect(covered.X - bounds.X, covered.Y - bounds.Y, covered.Width, covered.Height),
            Vector2.Zero,
            coarse,
            ref vertex,
            (short)across,
            (short)index,
            toLocal,
            null
        );

        var blurred = across;
        if (factor > 1)
        {
            blurred = ReserveLayer();
            _layers[blurred] = new DrawLayer(
                (int)coarse.X,
                (int)coarse.Y,
                (int)coarse.Width,
                (int)coarse.Height,
                layer.Pass + 2,
                _state.Layer,
                across
            );
            vertex.Shape = new Vector4(0f, 1f, sigma / factor, 0f);
            EmitComposite(
                coarse,
                coarsePixels,
                Vector2.Zero,
                coarse,
                ref vertex,
                (short)blurred,
                (short)across,
                toLocal,
                null
            );
        }

        vertex.Fill = layer.ShadowColor;
        vertex.Shape = factor > 1 ? default : new Vector4(0f, 1f, sigma, 0f);
        EmitComposite(
            covered,
            coarsePixels,
            layer.ShadowOffset,
            _state.Clip,
            ref vertex,
            _state.Layer,
            (short)blurred,
            toLocal,
            null
        );
        return _layers[blurred].Pass;
    }

    // A one-primitive opacity layer composites to the same pixels as the primitive drawn at that opacity.
    bool TryFold(int index, in LayerRecord layer)
    {
        for (var b = layer.FirstBatch; b < _batches.Count; b++)
        {
            ref readonly var batch = ref _batches[b];
            if (batch.Target != index)
                return false;
        }

        _layerRecords[index].Folded = true;
        var opacity = layer.Opacity * _state.Opacity;
        for (var b = layer.FirstBatch; b < _batches.Count; b++)
        {
            ref var batch = ref _batches[b];
            batch = batch with { Target = _state.Layer };
            foreach (ref var vertex in _vertices.AsSpan(batch.FirstVertex, batch.VertexCount))
            {
                vertex.Fill = Scale(vertex.Fill, opacity);
                vertex.Stroke = Scale(vertex.Stroke, opacity);
            }
        }
        Track(layer.Content);
        return true;
    }

    void Raise(short layer, int pass)
    {
        if (layer >= 0)
            _layerRecords[layer].Pass = Math.Max(_layerRecords[layer].Pass, pass);
    }

    int ReserveLayer()
    {
        if (_layerCount > short.MaxValue)
            throw new InvalidOperationException("A frame holds at most 32768 layers.");

        if (_layerCount == _layerRecords.Length)
        {
            Array.Resize(ref _layerRecords, _layerRecords.Length * 2);
            Array.Resize(ref _layers, _layerRecords.Length);
        }
        return _layerCount++;
    }

    // Destination is in the target's pixels, sourcePixels in the source layer's.
    void EmitComposite(
        Rect destination,
        Rect sourcePixels,
        Vector2 offset,
        Rect clip,
        ref UiVertex vertex,
        short target,
        short source,
        in Matrix3x2 toLocal,
        RenderTexture? paint
    )
    {
        var device = new Rect(
            destination.X + offset.X,
            destination.Y + offset.Y,
            destination.Width,
            destination.Height
        );
        if (!Overlaps(device, clip))
            return;

        vertex.Clip = Edges(clip);
        var baseVertex = OpenBatch(null, null, paint, 4, 6, target, source, true);

        var corners = _vertices.AsSpan(_vertexCount, 4);
        for (var i = 0; i < 4; i++)
        {
            var corner = new Vector2((i & 1) == 0 ? 0f : 1f, (i & 2) == 0 ? 0f : 1f);
            var position =
                new Vector2(device.X, device.Y) + corner * new Vector2(device.Width, device.Height);
            var pixel =
                new Vector2(sourcePixels.X, sourcePixels.Y)
                + corner * new Vector2(sourcePixels.Width, sourcePixels.Height);
            var local = Vector2.Transform(position - offset, toLocal);
            corners[i] = vertex;
            corners[i].Position = position;
            corners[i].Local = new Vector4(local.X, local.Y, pixel.X, pixel.Y);
        }

        WriteQuadIndices(baseVertex);
        if (target == _state.Layer)
            Track(device.Intersect(clip));
    }

    void Track(Rect device)
    {
        if (_state.Layer < 0 || device.IsEmpty)
            return;

        ref var layer = ref _layerRecords[_state.Layer];
        if (Replaying)
        {
            ReplayTrack(layer, device);
            return;
        }

        layer.Content = layer.Primitives == 0 ? device : layer.Content.Union(device);
        layer.Primitives++;
    }

    static uint Scale(uint rgba, float opacity)
    {
        if (opacity >= 1f)
            return rgba;
        var r = (rgba & 0xff) * opacity;
        var g = ((rgba >> 8) & 0xff) * opacity;
        var b = ((rgba >> 16) & 0xff) * opacity;
        var a = (rgba >> 24) * opacity;
        return (uint)MathF.Round(r)
            | ((uint)MathF.Round(g) << 8)
            | ((uint)MathF.Round(b) << 16)
            | ((uint)MathF.Round(a) << 24);
    }

    void PushShapeClip(Rect clip, CornerRadius radius)
    {
        Save();
        var device = clip.TransformedBy(_state.Transform);
        // A turned edge softens across more than a pixel, past the corners of its bounding box.
        if (!IsAxisAligned(_state.Transform))
            device = device.Inflate(1f, 1f);
        if (!NarrowClip(device))
            return;

        ref var layer = ref BeginLayer(LayerKind.ShapeClip);
        layer.MaskBounds = clip;
        layer.ClipRadius = radius;
    }

    bool NarrowClip(Rect device)
    {
        _state.Clip = _state.Clip.Intersect(device);
        return !_state.Clip.IsEmpty && _state.Opacity > 0f;
    }

    // The path may not outlive this call, so its mesh goes down now and learns the layer's box at the pop.
    void PushPathClip(ReadOnlySpan<PathVerb> verbs, ReadOnlySpan<Vector2> points, FillRule rule)
    {
        Save();
        _tessellator.Clear();
        _tessellator.AddPath(verbs, points, _state.Transform);
        _tessellator.Fill(rule);
        if (_tessellator.Overflowed || _tessellator.Indices.IsEmpty)
        {
            _state.Clip = default;
            return;
        }
        if (!NarrowClip(MeshBounds()))
            return;

        UiVertex vertex = default;
        vertex.Fill = Tint(_state.Opacity);
        vertex.Params = new Vector4(UiPrimitive.MeshClipLayer, UiPaint.Solid, 0f, 0f);
        var parent = _state.Layer;
        ref var layer = ref BeginLayer(LayerKind.PathClip);
        layer.ClipFirstVertex = _vertexCount;
        WriteMesh(ref vertex, Matrix3x2.Identity, parent, _state.Layer, null);
        layer.ClipVertexCount = _vertexCount - layer.ClipFirstVertex;
    }

    void LandPathClip(in LayerRecord layer, Rect bounds)
    {
        var landed = bounds.Intersect(_state.Clip);
        var clip = Edges(landed);
        foreach (ref var vertex in _vertices.AsSpan(layer.ClipFirstVertex, layer.ClipVertexCount))
        {
            vertex.Local.Z = vertex.Position.X - bounds.X;
            vertex.Local.W = vertex.Position.Y - bounds.Y;
            vertex.Clip = clip;
        }
        Track(landed);
    }

    enum LayerKind : byte
    {
        Opacity,
        Mask,
        Shadow,
        ShapeClip,
        PathClip,
    }

    struct LayerRecord
    {
        public LayerKind Kind;
        public float Opacity;
        public int FirstBatch;
        public int Pass;
        public Rect Content;
        public int Primitives;
        public Paint MaskPaint;
        public Rect MaskBounds;
        public CornerRadius ClipRadius;
        public int ClipFirstVertex;
        public int ClipVertexCount;
        public Vector2 ShadowOffset;
        public float ShadowSigma;
        public float ShadowReach;
        public uint ShadowColor;
        public bool Folded;
        public uint CompositeFill;
        public int CompositeVertex;
        public int CompositeVertices;
    }
}

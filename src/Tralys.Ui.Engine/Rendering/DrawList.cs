using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Tralys.Ui.Rendering;

/// <summary>One frame of a view, flattened into indexed triangles and the batches that draw them. Each frame rebuilds
/// it into the same storage.</summary>
[FramePath]
public sealed partial class DrawList
{
    const float HalfResolutionSigma = 4f;
    const float QuarterResolutionSigma = 8f;
    const int MaxMipLevel = 16;
    const float MipSharpening = 0.5f;
    const float BoundsSlack = 1e-3f;
    const int MaxBatchVertices = ushort.MaxValue + 1;
    const int RarePaints =
        (int)UiPaint.LinearGradient
        | (int)UiPaint.LinearGradient * 4
        | (int)UiPaint.FillImage
        | (int)UiPaint.StrokeImage;

    readonly GradientRampAtlas _ramps;
    readonly PathTessellator _tessellator = new();
    UiVertex[] _vertices = new UiVertex[256];
    ushort[] _indices = new ushort[384];
    ValueList<DrawBatch> _batches = new(8);
    GradientStops[] _rampStops = new GradientStops[4];
    int[] _rampRows = new int[4];
    int _rampCount;
    LayerRecord[] _layerRecords = new LayerRecord[4];
    DrawLayer[] _layers = new DrawLayer[4];
    ValueList<State> _stack = new(16);
    State _state;
    int _vertexCount;
    int _indexCount;
    int _quadCount;
    int _layerCount;
    Rect _view;
    int[] _meshSlots = [];
    int[] _meshChunk = [];
    ValueList<SampledPage> _glyphPages = new(4);
    ValueList<Binding> _bindings = new(8);
    readonly HashSet<RenderTexture> _bound = new(ReferenceEqualityComparer.Instance);

    /// <param name="ramps">The atlas its gradients take rows of, shared by every list of a frame; its owner calls
    /// <see cref="GradientRampAtlas.BeginFrame"/> once a frame, before any of them is built.</param>
    public DrawList(GradientRampAtlas ramps)
    {
        ArgumentNullException.ThrowIfNull(ramps);
        _ramps = ramps;
    }

    /// <summary>Every primitive's vertices in drawing order; a quad's four run top-left, top-right, bottom-left,
    /// bottom-right.</summary>
    [Experimental("TRL5001")]
    public ReadOnlySpan<UiVertex> Vertices => _vertices.AsSpan(0, _vertexCount);

    /// <summary>Three per triangle, each counted from its batch's <see cref="DrawBatch.FirstVertex"/>.</summary>
    [Experimental("TRL5001")]
    public ReadOnlySpan<ushort> Indices => _indices.AsSpan(0, _indexCount);

    [Experimental("TRL5001")]
    public ReadOnlySpan<DrawBatch> Batches => _batches.AsSpan();

    [Experimental("TRL5001")]
    public ReadOnlySpan<DrawLayer> Layers => _layers.AsSpan(0, _layerCount);

    /// <summary>How many quads this frame drew; meshes are not counted.</summary>
    public int QuadCount => _quadCount;

    /// <summary>Moves on with every <see cref="Begin"/>, and no two lists share one, so a renderer tells a rebuilt list
    /// from one resubmitted as it was and uploads its geometry only when it moved.</summary>
    public long Generation { get; private set; }

    static long s_generations;

    internal GradientRampAtlas Ramps => _ramps;

    /// <summary>False while a frame is built only to be compared, so the visuals keep the spans of the list they
    /// draw into.</summary>
    internal bool RecordsSpans { get; set; } = true;

    /// <summary>How many times coarser than the frame every drop shadow blurs, trading its blur's quality for its
    /// cost; zero picks by each shadow's blur radius.</summary>
    public int ShadowDownsample { get; set; }

    internal ITextureResolver? Textures { get; set; }

    internal Diagnostics.DrawTrace? Trace { get; set; }

    /// <summary>For a frame that resubmits this list without rebuilding it: marks the gradient rows its last build
    /// drew from as used this frame, so no other list takes them. Call it after the atlas's
    /// <see cref="GradientRampAtlas.BeginFrame"/> and before other lists are built.</summary>
    /// <returns>False when another list has already taken one of the rows; the list must then be rebuilt.</returns>
    public bool RetainRamps()
    {
        for (var i = 0; i < _rampCount; i++)
        {
            if (!_ramps.TryRetain(_rampStops[i], _rampRows[i]))
                return false;
        }
        return true;
    }

    internal bool IsStale
    {
        get
        {
            foreach (ref readonly var page in _glyphPages.AsSpan())
            {
                if (page.Texture.Generation != page.Generation)
                    return true;
            }
            foreach (ref readonly var binding in _bindings.AsSpan())
            {
                if (
                    !ReferenceEquals(binding.Texture.Bind(out var layer), binding.Bound)
                    || layer != binding.Layer
                )
                    return true;
            }
            return false;
        }
    }

    /// <summary>Starts a frame of a view <paramref name="width"/> by <paramref name="height"/> device pixels.</summary>
    public void Begin(float width, float height)
    {
        Generation = Interlocked.Increment(ref s_generations);
        Array.Clear(_layerRecords, 0, _layerCount);
        Array.Clear(_rampStops, 0, _rampCount);
        _rampCount = 0;
        _layerCount = 0;
        _view = new Rect(0f, 0f, width, height);
        _vertexCount = 0;
        _indexCount = 0;
        _quadCount = 0;
        _batches.Clear();
        _stack.Clear();
        _glyphPages.Clear();
        _bindings.Clear();
        _bound.Clear();
        ForgetSpans();
        _state = new State
        {
            Transform = Matrix3x2.Identity,
            Opacity = 1f,
            Clip = new Rect(0f, 0f, width, height),
            Layer = -1,
        };
    }

    public void PushTransform(Matrix3x2 transform)
    {
        Trace?.Placement(transform);
        PushTransformCore(transform);
    }

    void PushTransformCore(Matrix3x2 transform)
    {
        Save();
        _state.Transform = transform * _state.Transform;
    }

    internal float Opacity => _state.Opacity;

    public void PushOpacity(float opacity)
    {
        Trace?.Opacity(opacity);
        Save();
        _state.Opacity *= Math.Clamp(opacity, 0f, 1f);
    }

    /// <summary>Clips to <paramref name="clip"/> in local space; under a rotation or skew, softening its edges through
    /// an offscreen layer, composited as <see cref="PushClip(Rect, CornerRadius)"/> says.</summary>
    public void PushClip(Rect clip)
    {
        Trace?.Clip(clip, default);
        if (!IsAxisAligned(_state.Transform))
        {
            PushShapeClip(clip, default);
            return;
        }

        Save();
        _state.Clip = _state.Clip.Intersect(clip.TransformedBy(_state.Transform));
    }

    /// <summary>Clips to <paramref name="clip"/> with rounded corners, softening its edge through an offscreen layer. A
    /// layer that gets no target draws straight into its parent, clipped only to the shape's bounding box.</summary>
    public void PushClip(Rect clip, CornerRadius radius)
    {
        if (radius == default)
        {
            PushClip(clip);
            return;
        }

        Trace?.Clip(clip, radius);
        PushShapeClip(clip, radius);
    }

    /// <summary>Clips to the region <paramref name="path"/> encloses by its fill rule, softening its edge through an
    /// offscreen layer, composited as <see cref="PushClip(Rect, CornerRadius)"/> says.</summary>
    public void PushClip(PathBuilder path)
    {
        ArgumentNullException.ThrowIfNull(path);
        Trace?.Clip(path.Verbs, path.Points, path.FillRule);
        PushPathClip(path.Verbs, path.Points, path.FillRule);
    }

    public void Pop()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("Pop has no matching push.");

        var popped = _state;
        _state = _stack.Pop();
        if (popped.Layer != _state.Layer)
            EndLayer(popped.Layer, popped.Clip);
    }

    static bool IsAxisAligned(in Matrix3x2 transform) =>
        (transform.M12 == 0f && transform.M21 == 0f)
        || (transform.M11 == 0f && transform.M22 == 0f);

    /// <summary>Appends what <paramref name="buffer"/> recorded under the current state. Pushes it left open end with it.</summary>
    public void Draw(RenderCommandBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var depth = _stack.Count;
        foreach (ref readonly var command in buffer.Commands)
        {
            switch (command.Op)
            {
                case RenderOp.PushClip:
                    PushClip(command.Rect);
                    break;
                case RenderOp.PushRoundedClip:
                    PushClip(command.Rect, command.Radius);
                    break;
                case RenderOp.PushPathClip:
                    var path = buffer.Paths[command.Start];
                    Trace?.Clip(buffer.VerbsOf(path), buffer.PointsOf(path), path.FillRule);
                    PushPathClip(buffer.VerbsOf(path), buffer.PointsOf(path), path.FillRule);
                    break;
                case RenderOp.PushTransform:
                    Trace?.Transform(command.Transform);
                    PushTransformCore(command.Transform);
                    break;
                case RenderOp.PushOpacity:
                    PushOpacity(command.Opacity);
                    break;
                case RenderOp.Pop:
                    Trace?.Pop();
                    Pop();
                    break;
                default:
                    Trace?.Draw(command, buffer);
                    if (!_state.Clip.IsEmpty && _state.Opacity > 0f)
                        Emit(command, buffer);
                    break;
            }
        }

        while (_stack.Count > depth)
            Pop();
    }

    void Emit(in RenderCommand command, RenderCommandBuffer buffer)
    {
        switch (command.Op)
        {
            case RenderOp.Box:
                EmitBox(command);
                break;
            case RenderOp.Ellipse:
                EmitEllipse(command);
                break;
            case RenderOp.Arc:
                EmitArc(command);
                break;
            case RenderOp.Line:
                EmitLine(command);
                break;
            case RenderOp.Image:
                EmitImage(command.DrawnTexture, command.Rect, command.Source, command.Opacity);
                break;
            case RenderOp.NineSlice:
                EmitNineSlice(command);
                break;
            case RenderOp.Glyphs:
                EmitGlyphs(command, buffer.Glyphs.Slice(command.Start, command.Count));
                break;
            case RenderOp.BoxShadow:
                EmitBoxShadow(command, false);
                break;
            case RenderOp.InsetBoxShadow:
                EmitBoxShadow(command, true);
                break;
            case RenderOp.Path:
                EmitPath(command, buffer);
                break;
        }
    }

    static bool Overlaps(Rect device, Rect clip) =>
        device.Right > clip.X
        && device.Bottom > clip.Y
        && device.X < clip.Right
        && device.Y < clip.Bottom;

    // A union rebuilds its right and bottom edges from its left and top, which rounds them.
    static bool Holds(Rect bounds, Rect device) =>
        device.X >= bounds.X
        && device.Y >= bounds.Y
        && device.Right <= bounds.Right + BoundsSlack
        && device.Bottom <= bounds.Bottom + BoundsSlack;

    void EmitBox(in RenderCommand command)
    {
        if (TrySplitImages(command))
            return;

        var inner = command.Rect.Deflate(command.Border);
        UiVertex vertex = default;
        vertex.Bounds = ToVector(command.Rect);
        vertex.Shape = ToVector(command.Radius);
        vertex.Inner = ToVector(inner);
        vertex.InnerRadii = ToVector(command.Radius.Deflate(command.Border));
        var fillImage = ResolveFill(ref vertex, command.Fill, inner, Matrix3x2.Identity);
        var strokeImage = ResolveStroke(ref vertex, command.Stroke, command.Rect);
        vertex.Params.X = UiPrimitive.Box;
        EmitBoxQuad(command.Rect, _state.Transform, ref vertex, fillImage ?? strokeImage);
    }

    // Edges on device pixels under a uniform scale soften nothing, so the quad takes no margin, and a plain box no shape.
    void EmitBoxQuad(Rect local, in Matrix3x2 transform, ref UiVertex vertex, RenderTexture? paint)
    {
        if (!LandsOnPixels(local, transform))
        {
            EmitShape(local, transform, ref vertex, paint);
            return;
        }

        if (
            vertex.Inner == vertex.Bounds
            && vertex.Shape == default
            && ((int)vertex.Params.Y & RarePaints) == 0
        )
            vertex.Params.X = UiPrimitive.Rect;
        Emit(local, transform, ref vertex, null, default, paint);
    }

    static bool LandsOnPixels(Rect local, in Matrix3x2 transform)
    {
        if (
            !IsAxisAligned(transform)
            || MathF.Abs(
                MathF.Abs(transform.M11 + transform.M12) - MathF.Abs(transform.M21 + transform.M22)
            ) > 1e-5f
        )
            return false;

        var from = Vector2.Transform(new Vector2(local.X, local.Y), transform);
        var to = Vector2.Transform(new Vector2(local.Right, local.Bottom), transform);
        return OnPixel(from.X) && OnPixel(from.Y) && OnPixel(to.X) && OnPixel(to.Y);
    }

    static bool OnPixel(float position) => MathF.Abs(position - MathF.Round(position)) < 1e-3f;

    // A vertex holds one image paint's window, so a fill and a stroke that both paint images draw one at a time.
    bool TrySplitImages(in RenderCommand command)
    {
        if (command.Fill.Kind != PaintKind.Image || command.Stroke.Kind != PaintKind.Image)
            return false;

        var fill = command;
        fill.Stroke = default;
        var stroke = command;
        stroke.Fill = default;
        if (command.Op == RenderOp.Box)
        {
            EmitBox(fill);
            EmitBox(stroke);
        }
        else
        {
            EmitEllipse(fill);
            EmitEllipse(stroke);
        }
        return true;
    }

    void EmitEllipse(in RenderCommand command)
    {
        if (TrySplitImages(command))
            return;

        UiVertex vertex = default;
        vertex.Bounds = ToVector(command.Rect);
        vertex.Inner = new Vector4(command.StrokeWidth, 0f, 0f, 0f);
        var fillImage = ResolveFill(ref vertex, command.Fill, command.Rect, Matrix3x2.Identity);
        var strokeImage = ResolveStroke(ref vertex, command.Stroke, command.Rect);
        vertex.Params.X = UiPrimitive.Ellipse;
        EmitShape(command.Rect, _state.Transform, ref vertex, fillImage ?? strokeImage);
    }

    void EmitArc(in RenderCommand command)
    {
        var start = command.ArcStart;
        var sweep = command.ArcSweep;
        var rotation = MathF.PI * 0.5f - (start + sweep * 0.5f);
        var half = MathF.Min(sweep * 0.5f, MathF.PI);

        UiVertex vertex = default;
        vertex.Bounds = ToVector(command.Rect);
        vertex.Shape = new Vector4(
            command.Rect.Width * 0.5f,
            command.StrokeWidth,
            MathF.Cos(rotation),
            MathF.Sin(rotation)
        );
        vertex.Inner = new Vector4(
            MathF.Sin(half),
            MathF.Cos(half),
            sweep >= MathF.Tau ? 1f : 0f,
            0f
        );
        var image = ResolveFill(ref vertex, command.Fill, command.Rect, Matrix3x2.Identity);
        vertex.Params.X = UiPrimitive.Arc;
        EmitShape(command.Rect, _state.Transform, ref vertex, image);
    }

    void EmitLine(in RenderCommand command)
    {
        var from = command.LineFrom;
        var to = command.LineTo;
        var delta = to - from;
        if (!KeepsInLineFrame(command.Fill, delta))
        {
            StrokeLine(from, to, command.StrokeWidth, command.Fill);
            return;
        }

        var frame =
            Matrix3x2.CreateRotation(MathF.Atan2(delta.Y, delta.X))
            * Matrix3x2.CreateTranslation(from);
        Matrix3x2.Invert(frame, out var toFrame);
        var local = new Rect(0f, -command.StrokeWidth * 0.5f, delta.Length(), command.StrokeWidth);

        UiVertex vertex = default;
        vertex.Bounds = ToVector(local);
        vertex.Inner = vertex.Bounds;
        var image = ResolveFill(ref vertex, command.Fill, local, toFrame);
        vertex.Params.X = UiPrimitive.Box;
        EmitBoxQuad(local, frame * _state.Transform, ref vertex, image);
    }

    // A line draws in a frame turned along it, where a local-space ellipse or image no longer lies on the axes.
    static bool KeepsInLineFrame(in Paint paint, Vector2 delta) =>
        paint.Kind != PaintKind.Image
        && (
            paint.Kind != PaintKind.RadialGradient
            || paint.Space != GradientSpace.Absolute
            || paint.End.X == paint.End.Y
            || delta.Y == 0f
        );

    void StrokeLine(Vector2 from, Vector2 to, float thickness, in Paint paint)
    {
        var transform = _state.Transform;
        var scale = UniformScale(transform);
        ReadOnlySpan<PathVerb> verbs = [PathVerb.MoveTo, PathVerb.LineTo];
        ReadOnlySpan<Vector2> points = [from, to];
        _tessellator.Clear();
        _tessellator.AddPath(verbs, points, transform);
        _tessellator.Stroke(
            thickness * 0.5f * scale,
            PenLineJoin.Miter,
            PenLineCap.Flat,
            PenLineCap.Flat,
            1f,
            default,
            scale,
            0f,
            PenLineCap.Flat
        );
        EmitMesh(paint, LineBox(from, to, thickness));
    }

    static Rect LineBox(Vector2 from, Vector2 to, float thickness)
    {
        var delta = to - from;
        var length = delta.Length();
        var reach =
            length > 0f
                ? new Vector2(MathF.Abs(delta.Y), MathF.Abs(delta.X)) * (thickness * 0.5f / length)
                : default;
        var min = Vector2.Min(from, to) - reach;
        var max = Vector2.Max(from, to) + reach;
        return Rect.FromEdges(min.X, min.Y, max.X, max.Y);
    }

    void EmitGlyphs(in RenderCommand command, ReadOnlySpan<GlyphQuad> glyphs)
    {
        var atlas = command.DrawnTexture;
        float width = atlas.Width;
        float height = atlas.Height;
        if (!(width > 0f && height > 0f) || command.Generation != atlas.Generation)
            return;

        NoteGlyphPage(atlas, command.Generation);
        UiVertex vertex = default;
        vertex.Bounds = ToVector(command.Rect);
        var image = ResolveFill(ref vertex, command.Fill, command.Rect, Matrix3x2.Identity);
        vertex.Params.X = UiPrimitive.Glyph;
        vertex.Shape = new Vector4(width, height, 0f, 0f);
        foreach (ref readonly var glyph in glyphs)
        {
            Emit(
                glyph.Destination,
                _state.Transform,
                ref vertex,
                atlas,
                Uv(glyph.Source, width, height),
                image
            );
        }
    }

    RenderTexture Bind(RenderTexture texture, out int layer)
    {
        var bound = texture.Bind(out layer);
        if (_bound.Add(texture))
            _bindings.Add(new Binding(texture, bound, layer));
        return bound;
    }

    void NoteGlyphPage(RenderTexture page, int generation)
    {
        foreach (ref readonly var noted in _glyphPages.AsSpan())
        {
            if (ReferenceEquals(noted.Texture, page))
                return;
        }
        _glyphPages.Add(new SampledPage(page, generation));
    }

    void EmitBoxShadow(in RenderCommand command, bool inset)
    {
        var caster = command.Rect;
        var sigma = command.ShadowBlur * 0.5f;
        var grow = inset ? -command.ShadowSpread : command.ShadowSpread;
        var shadow = Grow(caster, grow);
        var offset = command.ShadowOffset;
        shadow = new Rect(shadow.X + offset.X, shadow.Y + offset.Y, shadow.Width, shadow.Height);
        if (!inset && shadow.IsEmpty)
            return;

        UiVertex vertex = default;
        vertex.Bounds = ToVector(caster);
        vertex.Shape = ToVector(command.Radius);
        vertex.Inner = ToVector(shadow);
        vertex.InnerRadii = ToVector(Grow(command.Radius, grow));
        var image = ResolveFill(ref vertex, command.Fill, caster, Matrix3x2.Identity);
        vertex.StrokeGradient.X = sigma;
        vertex.Params.X = inset ? UiPrimitive.InsetBoxShadow : UiPrimitive.BoxShadow;
        var reach = sigma * 3f;
        EmitShape(
            inset ? caster : shadow.Inflate(reach, reach),
            _state.Transform,
            ref vertex,
            image
        );
    }

    void EmitShape(Rect local, in Matrix3x2 transform, ref UiVertex vertex, RenderTexture? paint)
    {
        var pad = DevicePixel(transform);
        Emit(local.Inflate(pad.X, pad.Y), transform, ref vertex, null, default, paint);
    }

    static Vector2 DevicePixel(in Matrix3x2 transform) =>
        new(
            1f / MathF.Max(new Vector2(transform.M11, transform.M12).Length(), 1e-6f),
            1f / MathF.Max(new Vector2(transform.M21, transform.M22).Length(), 1e-6f)
        );

    void Emit(
        Rect quad,
        in Matrix3x2 transform,
        ref UiVertex vertex,
        RenderTexture? texture,
        Vector4 uv,
        RenderTexture? paint
    )
    {
        var topLeft = Vector2.Transform(new Vector2(quad.X, quad.Y), transform);
        var topRight = Vector2.Transform(new Vector2(quad.Right, quad.Y), transform);
        var bottomLeft = Vector2.Transform(new Vector2(quad.X, quad.Bottom), transform);
        var bottomRight = Vector2.Transform(new Vector2(quad.Right, quad.Bottom), transform);
        var min = Vector2.Min(Vector2.Min(topLeft, topRight), Vector2.Min(bottomLeft, bottomRight));
        var max = Vector2.Max(Vector2.Max(topLeft, topRight), Vector2.Max(bottomLeft, bottomRight));
        var device = Rect.FromEdges(min.X, min.Y, max.X, max.Y);
        var clip = _state.Clip;
        if (!Overlaps(device, clip))
            return;

        var kept = device.Intersect(clip);
        var contained = kept == device;
        if (!contained && IsAxisAligned(transform))
        {
            if (kept.IsEmpty)
                return;

            ClipQuad(clip, transform, ref quad, ref uv);
            topLeft = Vector2.Transform(new Vector2(quad.X, quad.Y), transform);
            topRight = Vector2.Transform(new Vector2(quad.Right, quad.Y), transform);
            bottomLeft = Vector2.Transform(new Vector2(quad.X, quad.Bottom), transform);
            bottomRight = Vector2.Transform(new Vector2(quad.Right, quad.Bottom), transform);
            contained = true;
        }

        var rare = !contained || NeedsRareProgram(vertex);
        if (!rare)
            vertex.Params.Z = SoftWidth(transform);

        vertex.Clip = Edges(clip);
        var isGlyph = vertex.Params.X == UiPrimitive.Glyph;
        var baseVertex = OpenBatch(
            isGlyph ? null : texture,
            isGlyph ? texture : null,
            paint,
            4,
            6,
            _state.Layer,
            -1,
            rare
        );
        Track(kept);
        var corners = _vertices.AsSpan(_vertexCount, 4);
        corners[0] = vertex;
        corners[0].Position = topLeft;
        corners[0].Local = new Vector4(quad.X, quad.Y, uv.X, uv.Y);
        corners[1] = vertex;
        corners[1].Position = topRight;
        corners[1].Local = new Vector4(quad.Right, quad.Y, uv.Z, uv.Y);
        corners[2] = vertex;
        corners[2].Position = bottomLeft;
        corners[2].Local = new Vector4(quad.X, quad.Bottom, uv.X, uv.W);
        corners[3] = vertex;
        corners[3].Position = bottomRight;
        corners[3].Local = new Vector4(quad.Right, quad.Bottom, uv.Z, uv.W);

        WriteQuadIndices(baseVertex);
        _quadCount++;
    }

    static bool NeedsRareProgram(in UiVertex vertex) =>
        vertex.Params.X
            is not (
                UiPrimitive.Box
                or UiPrimitive.Rect
                or UiPrimitive.Glyph
                or UiPrimitive.Image
                or UiPrimitive.ArrayImage
            )
        || ((int)vertex.Params.Y & RarePaints) != 0;

    // An affine transform makes the derivatives of local position constant, so the shader need not take them.
    static float SoftWidth(in Matrix3x2 transform)
    {
        var determinant = MathF.Max(MathF.Abs(transform.GetDeterminant()), 1e-12f);
        var perPixel = new Vector2(
            MathF.Abs(transform.M22) + MathF.Abs(transform.M21),
            MathF.Abs(transform.M12) + MathF.Abs(transform.M11)
        );
        return MathF.Max(perPixel.Length() / determinant * 0.70710678f, 0.0001f);
    }

    // In the quad's own space, so a side the clip leaves alone keeps its exact edge.
    static void ClipQuad(Rect clip, in Matrix3x2 transform, ref Rect quad, ref Vector4 uv)
    {
        Matrix3x2.Invert(transform, out var toLocal);
        var from = Vector2.Transform(new Vector2(clip.X, clip.Y), toLocal);
        var to = Vector2.Transform(new Vector2(clip.Right, clip.Bottom), toLocal);
        var min = Vector2.Max(Vector2.Min(from, to), new Vector2(quad.X, quad.Y));
        var max = Vector2.Min(Vector2.Max(from, to), new Vector2(quad.Right, quad.Bottom));
        var uvPerLocal = new Vector2((uv.Z - uv.X) / quad.Width, (uv.W - uv.Y) / quad.Height);
        uv = new Vector4(
            uv.X + (min.X - quad.X) * uvPerLocal.X,
            uv.Y + (min.Y - quad.Y) * uvPerLocal.Y,
            uv.Z + (max.X - quad.Right) * uvPerLocal.X,
            uv.W + (max.Y - quad.Bottom) * uvPerLocal.Y
        );
        quad = Rect.FromEdges(min.X, min.Y, max.X, max.Y);
    }

    void WriteQuadIndices(int baseVertex)
    {
        var indices = _indices.AsSpan(_indexCount, 6);
        indices[0] = (ushort)baseVertex;
        indices[1] = (ushort)(baseVertex + 1);
        indices[2] = (ushort)(baseVertex + 2);
        indices[3] = (ushort)(baseVertex + 2);
        indices[4] = (ushort)(baseVertex + 1);
        indices[5] = (ushort)(baseVertex + 3);
        _vertexCount += 4;
        _indexCount += 6;
    }

    int OpenBatch(
        RenderTexture? image,
        RenderTexture? glyphs,
        RenderTexture? paint,
        int vertices,
        int indices,
        short target,
        short source,
        bool rare
    )
    {
        ValueList.EnsureCapacity(ref _vertices, _vertexCount + vertices);
        ValueList.EnsureCapacity(ref _indices, _indexCount + indices);

        var request = new BatchRequest(
            image,
            glyphs,
            paint,
            vertices,
            indices,
            target,
            source,
            rare
        );
        if (Replaying)
            return ReplayBatch(request);

        var first = JoinOrAppend(request);
        NoteRequest(request, first);
        return first;
    }

    int JoinOrAppend(in BatchRequest request)
    {
        var array = request.Image as RenderTextureArray;
        var texture = array is null ? request.Image : null;
        if (_batches.Count > 0)
        {
            ref var open = ref _batches[_batches.Count - 1];
            if (
                SharesSampler(open.Texture, texture)
                && SharesSampler(open.ArrayTexture, array)
                && SharesSampler(open.GlyphTexture, request.Glyphs)
                && SharesSampler(open.PaintTexture, request.Paint)
                && open.Target == request.Target
                && open.Source == request.Source
                && open.Rare == request.Rare
                && open.VertexCount + request.Vertices <= MaxBatchVertices
            )
            {
                var first = open.VertexCount;
                open = open with
                {
                    Texture = open.Texture ?? texture,
                    ArrayTexture = open.ArrayTexture ?? array,
                    GlyphTexture = open.GlyphTexture ?? request.Glyphs,
                    PaintTexture = open.PaintTexture ?? request.Paint,
                    VertexCount = open.VertexCount + request.Vertices,
                    IndexCount = open.IndexCount + request.Indices,
                };
                return first;
            }
        }

        AppendBatch(
            new DrawBatch(
                texture,
                _vertexCount,
                request.Vertices,
                _indexCount,
                request.Indices,
                request.Target,
                request.Source
            )
            {
                ArrayTexture = array,
                GlyphTexture = request.Glyphs,
                PaintTexture = request.Paint,
                Rare = request.Rare,
            }
        );
        return 0;
    }

    static bool SharesSampler(RenderTexture? open, RenderTexture? next) =>
        next is null || open is null || ReferenceEquals(open, next);

    void AppendBatch(in DrawBatch batch) => _batches.Add(batch);

    void Save() => _stack.Add(_state);

    // Once it shrinks to nothing it stays at its centre, where an inset shadow's spread meets.
    static Rect Grow(Rect rect, float amount)
    {
        var width = MathF.Max(0f, rect.Width + amount * 2f);
        var height = MathF.Max(0f, rect.Height + amount * 2f);
        return new Rect(
            rect.X + (rect.Width - width) * 0.5f,
            rect.Y + (rect.Height - height) * 0.5f,
            width,
            height
        );
    }

    // CSS keeps a sharp corner sharp under spread.
    static CornerRadius Grow(CornerRadius radius, float amount) =>
        new(
            radius.TopLeft > 0f ? MathF.Max(0f, radius.TopLeft + amount) : 0f,
            radius.TopRight > 0f ? MathF.Max(0f, radius.TopRight + amount) : 0f,
            radius.BottomRight > 0f ? MathF.Max(0f, radius.BottomRight + amount) : 0f,
            radius.BottomLeft > 0f ? MathF.Max(0f, radius.BottomLeft + amount) : 0f
        );

    static float UniformScale(in Matrix3x2 transform) =>
        MathF.Sqrt(MathF.Abs(transform.GetDeterminant()));

    static uint Tint(float opacity) => Color.Pack(opacity, opacity, opacity, opacity);

    static Vector4 Edges(Rect rect) => new(rect.X, rect.Y, rect.Right, rect.Bottom);

    static Vector4 Uv(Rect texels, float width, float height) =>
        new(texels.X / width, texels.Y / height, texels.Right / width, texels.Bottom / height);

    static Vector4 ToVector(Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    static Vector4 ToVector(CornerRadius radius) =>
        new(radius.TopLeft, radius.TopRight, radius.BottomRight, radius.BottomLeft);

    struct State
    {
        public Matrix3x2 Transform;
        public float Opacity;
        public Rect Clip;
        public short Layer;
    }

    readonly record struct SampledPage(RenderTexture Texture, int Generation);

    readonly record struct Binding(RenderTexture Texture, RenderTexture Bound, int Layer);

    readonly record struct BatchRequest(
        RenderTexture? Image,
        RenderTexture? Glyphs,
        RenderTexture? Paint,
        int Vertices,
        int Indices,
        short Target,
        short Source,
        bool Rare
    )
    {
        public int First { get; init; }

        public bool Matches(in BatchRequest other) =>
            ReferenceEquals(Image, other.Image)
            && ReferenceEquals(Glyphs, other.Glyphs)
            && ReferenceEquals(Paint, other.Paint)
            && Vertices == other.Vertices
            && Indices == other.Indices
            && Target == other.Target
            && Source == other.Source
            && Rare == other.Rare;
    }
}

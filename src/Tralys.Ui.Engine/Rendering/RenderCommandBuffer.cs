using System.Numerics;

namespace Tralys.Ui.Rendering;

internal enum RenderOp : byte
{
    Box,
    Ellipse,
    Arc,
    Line,
    Image,
    NineSlice,
    Glyphs,
    BoxShadow,
    InsetBoxShadow,
    Path,
    PushClip,
    PushRoundedClip,
    PushPathClip,
    PushTransform,
    PushOpacity,
    Pop,
}

[FramePath]
internal struct RenderCommand
{
    public RenderOp Op;
    public Rect Rect;
    public CornerRadius Radius;
    public Thickness Border;
    public float StrokeWidth;
    public Vector4 Data;
    public Rect Source;
    public float Opacity;
    public Matrix3x2 Transform;
    public Paint Fill;
    public Paint Stroke;
    public RenderTexture? Texture;
    public int Generation;
    public int Start;
    public int Count;
    public DashRange Dash;

    public readonly RenderTexture DrawnTexture =>
        Texture
        ?? throw new InvalidOperationException(
            "An image, nine-slice or glyph command always carries its texture."
        );

    public readonly PenLineJoin LineJoin => (PenLineJoin)Data.X;

    public readonly PenLineCap StartCap => (PenLineCap)Data.Y;

    public readonly PenLineCap EndCap => (PenLineCap)Data.Z;

    public readonly float MiterLimit => Data.W;

    // A square cap's corner reaches √2 half-widths out, past a miter limit below that.
    public readonly float StrokeReach => StrokeWidth * 0.5f * MathF.Max(MiterLimit, MathF.Sqrt(2f));

    public readonly float ArcStart => Data.X;

    public readonly float ArcSweep => Data.Y;

    public readonly Vector2 LineFrom => new(Data.X, Data.Y);

    public readonly Vector2 LineTo => new(Data.Z, Data.W);

    public readonly float SliceScale => Data.X;

    public readonly bool SliceTiles => Data.Y > 0.5f;

    public readonly float SliceInset => Data.Z;

    public readonly Vector2 ShadowOffset => new(Data.X, Data.Y);

    public readonly float ShadowBlur => Data.Z;

    public readonly float ShadowSpread => Data.W;
}

// Lengths and offset are already times the thickness, in an even count; a zero count strokes solid.
internal readonly record struct DashRange(int Start, int Count, float Offset, PenLineCap Cap);

internal readonly record struct PathRecord(
    int VerbStart,
    int VerbCount,
    int PointStart,
    int PointCount,
    FillRule FillRule,
    Rect Bounds
);

/// <summary>What one visual drew, kept until the visual is invalidated and records again. Recording reuses its capacity.</summary>
[FramePath]
public sealed class RenderCommandBuffer
{
    RenderCommand[] _commands = [];
    int _commandCount;
    GlyphQuad[] _glyphs = [];
    PathVerb[] _verbs = [];
    Vector2[] _points = [];
    SharedList<PathRecord> _paths;
    float[] _dashes = [];
    int _glyphCount;
    int _verbCount;
    int _pointCount;
    int _dashCount;
    int _depth;

    public bool IsEmpty => _commandCount == 0;

    /// <summary>Whether a glyph run was recorded against an atlas generation that has since moved on.</summary>
    public bool IsStale
    {
        get
        {
            if (_glyphCount == 0)
                return false;

            foreach (ref readonly var command in Commands)
            {
                if (
                    command.Op == RenderOp.Glyphs
                    && command.Generation != command.DrawnTexture.Generation
                )
                    return true;
            }
            return false;
        }
    }

    internal ReadOnlySpan<RenderCommand> Commands => _commands.AsSpan(0, _commandCount);

    internal ReadOnlySpan<GlyphQuad> Glyphs => _glyphs.AsSpan(0, _glyphCount);

    internal ReadOnlySpan<PathRecord> Paths => _paths.AsSpan();

    internal ReadOnlySpan<PathVerb> VerbsOf(in PathRecord path) =>
        _verbs.AsSpan(path.VerbStart, path.VerbCount);

    internal ReadOnlySpan<Vector2> PointsOf(in PathRecord path) =>
        _points.AsSpan(path.PointStart, path.PointCount);

    internal ReadOnlySpan<float> DashesOf(in DashRange dash) =>
        _dashes.AsSpan(dash.Start, dash.Count);

    /// <summary>Discards what was recorded and records again.</summary>
    public DrawingContext Open()
    {
        Clear();
        return new DrawingContext(this);
    }

    public void Clear()
    {
        Array.Clear(_commands, 0, _commandCount);
        _commandCount = 0;
        _glyphCount = 0;
        _verbCount = 0;
        _pointCount = 0;
        _paths.Clear();
        _dashCount = 0;
        _depth = 0;
    }

    internal void Release()
    {
        Clear();
        SharedArrays<RenderCommand>.Release(ref _commands);
        SharedArrays<GlyphQuad>.Release(ref _glyphs);
        SharedArrays<PathVerb>.Release(ref _verbs);
        SharedArrays<Vector2>.Release(ref _points);
        SharedArrays<float>.Release(ref _dashes);
        _paths.Release();
    }

    internal ref RenderCommand Add(RenderOp op)
    {
        SharedArrays<RenderCommand>.Grow(ref _commands, _commandCount + 1, _commandCount);
        ref var command = ref _commands[_commandCount++];
        command = default;
        command.Op = op;
        return ref command;
    }

    internal ref RenderCommand AddPush(RenderOp op)
    {
        _depth++;
        return ref Add(op);
    }

    internal int AddGlyphs(ReadOnlySpan<GlyphQuad> glyphs)
    {
        SharedArrays<GlyphQuad>.Grow(ref _glyphs, _glyphCount + glyphs.Length, _glyphCount);
        var start = _glyphCount;
        glyphs.CopyTo(_glyphs.AsSpan(start));
        _glyphCount += glyphs.Length;
        return start;
    }

    internal int AddPath(PathBuilder path)
    {
        var verbs = path.Verbs;
        var points = path.Points;
        SharedArrays<PathVerb>.Grow(ref _verbs, _verbCount + verbs.Length, _verbCount);
        SharedArrays<Vector2>.Grow(ref _points, _pointCount + points.Length, _pointCount);

        verbs.CopyTo(_verbs.AsSpan(_verbCount));
        points.CopyTo(_points.AsSpan(_pointCount));
        _paths.Add(
            new PathRecord(
                _verbCount,
                verbs.Length,
                _pointCount,
                points.Length,
                path.FillRule,
                PathMath.Bounds(path)
            )
        );
        _verbCount += verbs.Length;
        _pointCount += points.Length;
        return _paths.Count - 1;
    }

    internal DashRange AddDashes(in Pen pen)
    {
        if (!pen.IsDashed)
            return default;

        var lengths = pen.DashArray.Lengths;
        SharedArrays<float>.Grow(ref _dashes, _dashCount + lengths.Length, _dashCount);

        var thickness = pen.Thickness;
        var written = _dashes.AsSpan(_dashCount, lengths.Length);
        for (var i = 0; i < lengths.Length; i++)
            written[i] = lengths[i] * thickness;

        var start = _dashCount;
        _dashCount += lengths.Length;
        return new DashRange(start, lengths.Length, pen.DashOffset * thickness, pen.DashCap);
    }

    internal void Pop()
    {
        if (_depth == 0)
            throw new InvalidOperationException("Pop has no matching push.");

        _depth--;
        Add(RenderOp.Pop);
    }
}

using System.Numerics;

namespace Tralys.Ui.Rendering;

/// <summary>Which points a path's figures fill, by the winding of its edges around them.</summary>
public enum FillRule : byte
{
    /// <summary>Points an odd number of edges wind around.</summary>
    EvenOdd,

    /// <summary>Points the edges wind around a nonzero number of times.</summary>
    Nonzero,
}

/// <summary>How a combined path's region follows from its two operands'.</summary>
public enum GeometryCombineMode : byte
{
    /// <summary>What either operand fills.</summary>
    Union,

    /// <summary>What both operands fill.</summary>
    Intersect,

    /// <summary>What exactly one operand fills.</summary>
    Xor,

    /// <summary>The first operand less the second.</summary>
    Exclude,
}

internal enum PathVerb : byte
{
    MoveTo,
    LineTo,
    QuadraticTo,
    CubicTo,
    Close,
}

/// <summary>A mutable path of figures made of lines and curves, in local space. Drawing copies it, so one builder can
/// be cleared and rebuilt for the next draw; clearing keeps its storage.</summary>
[FramePath]
public sealed class PathBuilder
{
    const float Kappa = 0.5522847498f;

    const float CombineFineness = 8f;

    ValueList<PathVerb> _verbs = new(16);
    ValueList<Vector2> _points = new(32);
    PathCombiner? _combiner;
    bool _figureOpen;
    Vector2 _start;
    Vector2 _current;

    public FillRule FillRule { get; set; }

    public bool IsEmpty => _verbs.Count == 0;

    internal ReadOnlySpan<PathVerb> Verbs => _verbs.AsSpan();

    internal ReadOnlySpan<Vector2> Points => _points.AsSpan();

    [FramePath]
    public void Clear()
    {
        _verbs.Clear();
        _points.Clear();
        _figureOpen = false;
        _start = default;
        _current = default;
    }

    /// <summary>Starts a figure at <paramref name="point"/>.</summary>
    [FramePath]
    public void MoveTo(Vector2 point)
    {
        AddVerb(PathVerb.MoveTo);
        AddPoint(point);
        _figureOpen = true;
        _start = point;
        _current = point;
    }

    [FramePath]
    public void LineTo(Vector2 point)
    {
        EnsureFigure();
        AddVerb(PathVerb.LineTo);
        AddPoint(point);
        _current = point;
    }

    public void QuadraticTo(Vector2 control, Vector2 end)
    {
        EnsureFigure();
        AddVerb(PathVerb.QuadraticTo);
        AddPoint(control);
        AddPoint(end);
        _current = end;
    }

    public void CubicTo(Vector2 control1, Vector2 control2, Vector2 end)
    {
        EnsureFigure();
        AddVerb(PathVerb.CubicTo);
        AddPoint(control1);
        AddPoint(control2);
        AddPoint(end);
        _current = end;
    }

    /// <summary>An elliptical arc to <paramref name="end"/>, as WPF's ArcSegment and SVG's arc draw it: the ellipse of
    /// <paramref name="radius"/> turned <paramref name="rotation"/> radians, choosing the larger or smaller of its two
    /// arcs and the one sweeping clockwise or counter-clockwise. Radii too small to reach are scaled up until they do.</summary>
    public void ArcTo(Vector2 end, Vector2 radius, float rotation, bool isLargeArc, bool clockwise)
    {
        EnsureFigure();
        var from = _current;
        var rx = MathF.Abs(radius.X);
        var ry = MathF.Abs(radius.Y);
        if (from == end)
            return;
        if (rx < 1e-6f || ry < 1e-6f)
        {
            LineTo(end);
            return;
        }

        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);
        var half = (from - end) * 0.5f;
        var p = new Vector2(cos * half.X + sin * half.Y, -sin * half.X + cos * half.Y);
        var lambda = p.X * p.X / (rx * rx) + p.Y * p.Y / (ry * ry);
        if (lambda > 1f)
        {
            var grow = MathF.Sqrt(lambda);
            rx *= grow;
            ry *= grow;
        }

        var numerator = rx * rx * ry * ry - rx * rx * p.Y * p.Y - ry * ry * p.X * p.X;
        var denominator = rx * rx * p.Y * p.Y + ry * ry * p.X * p.X;
        var root = MathF.Sqrt(MathF.Max(0f, numerator / denominator));
        if (isLargeArc == clockwise)
            root = -root;
        var centre = new Vector2(root * rx * p.Y / ry, -root * ry * p.X / rx);

        var u = new Vector2((p.X - centre.X) / rx, (p.Y - centre.Y) / ry);
        var v = new Vector2((-p.X - centre.X) / rx, (-p.Y - centre.Y) / ry);
        var start = MathF.Atan2(u.Y, u.X);
        var sweep = MathF.Atan2(u.X * v.Y - u.Y * v.X, Vector2.Dot(u, v));
        if (clockwise && sweep < 0f)
            sweep += MathF.Tau;
        else if (!clockwise && sweep > 0f)
            sweep -= MathF.Tau;

        // A chord so short its square underflows has no centre to sweep about.
        if (float.IsNaN(sweep))
        {
            LineTo(end);
            return;
        }

        var mid = (from + end) * 0.5f;
        var origin =
            new Vector2(cos * centre.X - sin * centre.Y, sin * centre.X + cos * centre.Y) + mid;
        var segments = Math.Max(
            1,
            (int)MathF.Ceiling(MathF.Abs(sweep) / (MathF.PI * 0.5f) - 1e-4f)
        );
        var step = sweep / segments;
        var handle = 4f / 3f * MathF.Tan(step * 0.25f);
        var angle = start;
        for (var i = 0; i < segments; i++)
        {
            var next = angle + step;
            var a = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var b = new Vector2(MathF.Cos(next), MathF.Sin(next));
            var frame = new Vector4(rx, ry, cos, sin);
            CubicTo(
                OnEllipse(a + new Vector2(-a.Y, a.X) * handle, frame, origin),
                OnEllipse(b - new Vector2(-b.Y, b.X) * handle, frame, origin),
                i == segments - 1 ? end : OnEllipse(b, frame, origin)
            );
            angle = next;
        }
    }

    static Vector2 OnEllipse(Vector2 unit, Vector4 frame, Vector2 origin)
    {
        var x = unit.X * frame.X;
        var y = unit.Y * frame.Y;
        return new Vector2(frame.Z * x - frame.W * y, frame.W * x + frame.Z * y) + origin;
    }

    /// <summary>Ends the figure with a line back to where it started.</summary>
    [FramePath]
    public void Close()
    {
        if (!_figureOpen)
            return;

        AddVerb(PathVerb.Close);
        _figureOpen = false;
        _current = _start;
    }

    [FramePath]
    public void AddRectangle(Rect rect)
    {
        MoveTo(new Vector2(rect.X, rect.Y));
        LineTo(new Vector2(rect.Right, rect.Y));
        LineTo(new Vector2(rect.Right, rect.Bottom));
        LineTo(new Vector2(rect.X, rect.Bottom));
        Close();
    }

    /// <summary>A rectangle whose corners are quarter ellipses of <paramref name="radius"/>, each limited to half the
    /// rectangle; the corners are square unless both radii are nonzero.</summary>
    public void AddRoundedRectangle(Rect rect, Vector2 radius)
    {
        var corner = PathMath.Corner(rect, radius.X, radius.Y);
        if (!(corner.X > 0f && corner.Y > 0f))
        {
            AddRectangle(rect);
            return;
        }

        var (left, top, right, bottom) = (rect.X, rect.Y, rect.Right, rect.Bottom);
        var (rx, ry) = (corner.X, corner.Y);
        var (kx, ky) = (rx * (1f - Kappa), ry * (1f - Kappa));
        MoveTo(new Vector2(left + rx, top));
        LineTo(new Vector2(right - rx, top));
        CubicTo(
            new Vector2(right - kx, top),
            new Vector2(right, top + ky),
            new Vector2(right, top + ry)
        );
        LineTo(new Vector2(right, bottom - ry));
        CubicTo(
            new Vector2(right, bottom - ky),
            new Vector2(right - kx, bottom),
            new Vector2(right - rx, bottom)
        );
        LineTo(new Vector2(left + rx, bottom));
        CubicTo(
            new Vector2(left + kx, bottom),
            new Vector2(left, bottom - ky),
            new Vector2(left, bottom - ry)
        );
        LineTo(new Vector2(left, top + ry));
        CubicTo(
            new Vector2(left, top + ky),
            new Vector2(left + kx, top),
            new Vector2(left + rx, top)
        );
        Close();
    }

    /// <summary>The ellipse filling <paramref name="bounds"/>, wound clockwise.</summary>
    public void AddEllipse(Rect bounds)
    {
        var rx = bounds.Width * 0.5f;
        var ry = bounds.Height * 0.5f;
        var cx = bounds.X + rx;
        var cy = bounds.Y + ry;
        var ox = rx * Kappa;
        var oy = ry * Kappa;

        MoveTo(new Vector2(cx + rx, cy));
        CubicTo(
            new Vector2(cx + rx, cy + oy),
            new Vector2(cx + ox, cy + ry),
            new Vector2(cx, cy + ry)
        );
        CubicTo(
            new Vector2(cx - ox, cy + ry),
            new Vector2(cx - rx, cy + oy),
            new Vector2(cx - rx, cy)
        );
        CubicTo(
            new Vector2(cx - rx, cy - oy),
            new Vector2(cx - ox, cy - ry),
            new Vector2(cx, cy - ry)
        );
        CubicTo(
            new Vector2(cx + ox, cy - ry),
            new Vector2(cx + rx, cy - oy),
            new Vector2(cx + rx, cy)
        );
        Close();
    }

    /// <summary>Replaces this path with the region <paramref name="mode"/> makes of it and <paramref name="other"/>,
    /// each filled by its own <see cref="FillRule"/>: closed straight-edged figures around that region, filled
    /// <see cref="FillRule.Nonzero"/>, their curves flattened to stay smooth at eight times their size. Combining
    /// keeps its working storage, so combining again allocates nothing once warm.</summary>
    [FramePath]
    public void Combine(PathBuilder other, GeometryCombineMode mode)
    {
        ArgumentNullException.ThrowIfNull(other);
        (_combiner ??= new PathCombiner()).Outline(this, other, mode, CombineFineness, this);
    }

    [FramePath]
    void EnsureFigure()
    {
        if (!_figureOpen)
            MoveTo(_current);
    }

    [FramePath]
    void AddVerb(PathVerb verb)
    {
        _verbs.Add(verb);
    }

    [FramePath]
    void AddPoint(Vector2 point)
    {
        _points.Add(point);
    }
}

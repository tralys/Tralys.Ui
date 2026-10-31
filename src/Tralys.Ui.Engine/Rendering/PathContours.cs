using System.Numerics;

namespace Tralys.Ui.Rendering;

[FramePath]
internal sealed class PathContours
{
    ValueList<Vector2> _points = new(64);
    ValueList<Contour> _contours = new(8);
    int _figure = -1;

    public int Count => _contours.Count;

    public int PointCount => _points.Count;

    public ReadOnlySpan<Vector2> Points => _points.AsSpan();

    public Contour this[int index] => _contours[index];

    public ReadOnlySpan<Vector2> PointsOf(in Contour contour) =>
        _points.AsSpan(contour.Start, contour.Count);

    public ReadOnlySpan<Vector2> FillPointsOf(in Contour contour) =>
        _points.AsSpan(contour.Start, contour.FillCount);

    public void Clear()
    {
        _points.Clear();
        _contours.Clear();
        _figure = -1;
    }

    /// <summary>Starts a closed figure at <paramref name="point"/>, ending the one before it.</summary>
    public void StartFigure(Vector2 point)
    {
        EndFigure();
        _figure = _points.Count;
        _points.Add(point);
    }

    public void LineTo(Vector2 point) => _points.Add(point);

    /// <summary>Closes the figure <see cref="StartFigure"/> began, if one is open.</summary>
    public void EndFigure()
    {
        if (_figure < 0)
            return;

        EndFigure(_figure, closed: true);
        _figure = -1;
    }

    public void Add(
        ReadOnlySpan<PathVerb> verbs,
        ReadOnlySpan<Vector2> points,
        in Matrix3x2 transform
    )
    {
        var sink = new Sink(this);
        PathMath.Flatten(verbs, points, transform, ref sink);
    }

    /// <summary>How many times the figures wind around <paramref name="point"/>, each closed back to its
    /// start.</summary>
    public int Winding(Vector2 point)
    {
        var winding = 0;
        foreach (ref readonly var contour in _contours.AsSpan())
        {
            if (contour.FillCount < 3)
                continue;

            var points = FillPointsOf(contour);
            var a = points[^1];
            foreach (var b in points)
            {
                winding += PathMath.Crossing(a, b, point);
                a = b;
            }
        }
        return winding;
    }

    void EndFigure(int start, bool closed)
    {
        var write = start + 1;
        for (var read = start + 1; read < _points.Count; read++)
        {
            if (Vector2.DistanceSquared(_points[read], _points[write - 1]) > 1e-8f)
                _points[write++] = _points[read];
        }
        if (
            closed
            && write - start > 1
            && Vector2.DistanceSquared(_points[write - 1], _points[start]) <= 1e-8f
        )
            write--;
        _points.Truncate(write);

        var count = write - start;
        var fillCount =
            !closed
            && count > 1
            && Vector2.DistanceSquared(_points[write - 1], _points[start]) <= 1e-8f
                ? count - 1
                : count;
        _contours.Add(new Contour(start, count, closed, fillCount));
    }

    struct Sink(PathContours contours) : PathMath.IEdgeSink
    {
        int _start;

        public void Begin() => _start = contours._points.Count;

        public readonly void Edge(Vector2 from, Vector2 to, bool closesForFill)
        {
            if (contours._points.Count == _start)
                contours._points.Add(from);
            if (!closesForFill)
                contours._points.Add(to);
        }

        public readonly void End(bool closed) => contours.EndFigure(_start, closed);
    }
}

internal readonly record struct Contour(int Start, int Count, bool Closed, int FillCount);

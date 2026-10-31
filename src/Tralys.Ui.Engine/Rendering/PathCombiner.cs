using System.Numerics;

namespace Tralys.Ui.Rendering;

[FramePath]
internal sealed class PathCombiner
{
    readonly PathContours _first = new();
    readonly PathContours _second = new();
    readonly PathCrossings _crossings = new();
    FillRule _firstRule;
    FillRule _secondRule;
    GeometryCombineMode _mode;
    Segment[] _boundary = new Segment[16];
    float[] _boundaryKeys = new float[16];
    int[] _boundaryOrder = new int[16];
    bool[] _boundaryUsed = new bool[16];
    ValueList<Vector2> _loopPoints = new(64);
    ValueList<Contour> _loops = new(8);
    int _boundaryCount;

    // Closed loops around the combination into target, which may be an operand, its region on each loop's left.
    public void Outline(
        PathBuilder first,
        PathBuilder second,
        GeometryCombineMode mode,
        float fineness,
        PathBuilder target
    )
    {
        _first.Clear();
        _second.Clear();
        _crossings.Clear();
        _loopPoints.Clear();
        _loops.Clear();
        _boundaryCount = 0;
        var scale = Matrix3x2.CreateScale(fineness);
        _first.Add(first.Verbs, first.Points, scale);
        _second.Add(second.Verbs, second.Points, scale);
        _firstRule = first.FillRule;
        _secondRule = second.FillRule;
        _mode = mode;
        Combine();
        target.Clear();
        target.FillRule = FillRule.Nonzero;
        WriteLoops(target, 1f / fineness);
    }

    void Combine()
    {
        _crossings.Add(_first);
        _crossings.Add(_second);
        _crossings.Find();
        var cover = new Combined(this);
        _crossings.Classify(ref cover);

        for (var e = 0; e < _crossings.EdgeCount; e++)
        {
            if (_crossings.Next(e) < 0)
                continue;

            for (var k = 0; k <= _crossings.Splits(e); k++)
            {
                var sign = _crossings.Sign(e, k);
                if (sign == 0)
                    continue;

                var piece = _crossings.Piece(e, k);
                var from = new Vector2((float)piece.Ax, (float)piece.Ay);
                var to = new Vector2((float)piece.Bx, (float)piece.By);
                if (Vector2.Distance(from, to) > PathMath.Snap)
                    AddBoundary(sign < 0 ? new Segment(from, to) : new Segment(to, from));
            }
        }
        ChainBoundary();
    }

    void WriteLoops(PathBuilder target, float scale)
    {
        foreach (ref readonly var loop in _loops.AsSpan())
        {
            var points = _loopPoints.AsSpan(loop.Start, loop.Count);
            target.MoveTo(points[0] * scale);
            for (var i = 1; i < points.Length; i++)
                target.LineTo(points[i] * scale);
            target.Close();
        }
    }

    void AddBoundary(Segment piece)
    {
        if (_boundaryCount == _boundary.Length)
            Array.Resize(ref _boundary, _boundary.Length * 2);
        _boundary[_boundaryCount++] = piece;
    }

    bool Inside(Vector2 point)
    {
        var first = PathMath.Covers(_firstRule, _first.Winding(point));
        var second = PathMath.Covers(_secondRule, _second.Winding(point));
        return _mode switch
        {
            GeometryCombineMode.Union => first || second,
            GeometryCombineMode.Intersect => first && second,
            GeometryCombineMode.Xor => first != second,
            _ => first && !second,
        };
    }

    // Overlapping operand edges leave the same piece twice; it bounds the region once.
    void ChainBoundary()
    {
        var count = _boundaryCount;
        if (_boundaryKeys.Length < count)
        {
            Array.Resize(ref _boundaryKeys, _boundary.Length);
            Array.Resize(ref _boundaryOrder, _boundary.Length);
            Array.Resize(ref _boundaryUsed, _boundary.Length);
        }
        for (var b = 0; b < count; b++)
        {
            _boundaryKeys[b] = _boundary[b].From.X;
            _boundaryOrder[b] = b;
            _boundaryUsed[b] = false;
        }
        _boundaryKeys.AsSpan(0, count).Sort(_boundaryOrder.AsSpan(0, count));

        for (var o = 0; o < count; o++)
        {
            var piece = _boundary[_boundaryOrder[o]];
            for (
                var other = o + 1;
                other < count && _boundaryKeys[other] <= _boundaryKeys[o] + PathMath.Snap;
                other++
            )
            {
                var twin = _boundaryOrder[other];
                if (Near(_boundary[twin].From, piece.From) && Near(_boundary[twin].To, piece.To))
                    _boundaryUsed[twin] = true;
            }
        }

        for (var o = 0; o < count; o++)
        {
            var current = _boundaryOrder[o];
            if (_boundaryUsed[current])
                continue;

            var start = _loopPoints.Count;
            _loopPoints.Add(_boundary[current].From);
            while (current >= 0)
            {
                _boundaryUsed[current] = true;
                var end = _boundary[current].To;
                if (Near(end, _loopPoints[start]))
                    break;
                _loopPoints.Add(end);
                current = NextPiece(end);
            }

            if (_loopPoints.Count - start < 3)
            {
                _loopPoints.Truncate(start);
                continue;
            }
            _loops.Add(
                new Contour(start, _loopPoints.Count - start, true, _loopPoints.Count - start)
            );
        }
    }

    int NextPiece(Vector2 from)
    {
        var low = 0;
        var high = _boundaryCount;
        while (low < high)
        {
            var middle = (low + high) >> 1;
            if (_boundaryKeys[middle] < from.X - PathMath.Snap)
                low = middle + 1;
            else
                high = middle;
        }
        for (var o = low; o < _boundaryCount && _boundaryKeys[o] <= from.X + PathMath.Snap; o++)
        {
            var piece = _boundaryOrder[o];
            if (!_boundaryUsed[piece] && Near(_boundary[piece].From, from))
                return piece;
        }
        return -1;
    }

    static bool Near(Vector2 a, Vector2 b) =>
        Vector2.DistanceSquared(a, b) <= PathMath.Snap * PathMath.Snap;

    readonly record struct Segment(Vector2 From, Vector2 To);

    readonly struct Combined(PathCombiner combiner) : PathCrossings.ICover
    {
        public bool Covers(Vector2 point) => combiner.Inside(point);
    }
}

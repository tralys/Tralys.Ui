using System.Numerics;

namespace Tralys.Ui.Rendering;

// Runs break at every crossing and every edge passing through a vertex, so one probe classifies a whole run.
[FramePath]
internal sealed class PathCrossings
{
    const double Snap = PathMath.Snap;
    const double Parallel = 1e-6;

    ValueList<Segment> _segments = new(64);
    ValueList<Loop> _loops = new(8);
    int[] _next = new int[64];
    bool[] _breaks = new bool[64];
    double[] _tops = new double[64];
    int[] _order = new int[64];
    double[] _splitKeys = new double[16];
    Point[] _splitPoints = new Point[16];
    int[] _splitStart = new int[65];
    sbyte[] _signs = new sbyte[64];
    int _splitCount;

    public interface ICover
    {
        bool Covers(Vector2 point);
    }

    /// <summary>What <paramref name="contours"/> fill by <paramref name="rule"/>.</summary>
    public readonly struct Fill(PathContours contours, FillRule rule) : ICover
    {
        public bool Covers(Vector2 point) => PathMath.Covers(rule, contours.Winding(point));
    }

    public void Clear()
    {
        _segments.Clear();
        _loops.Clear();
        _splitCount = 0;
    }

    /// <returns>The edge of the store's first point.</returns>
    public int Add(PathContours contours)
    {
        var first = _segments.Count;
        var count = first + contours.PointCount;
        ValueList.EnsureCapacity(ref _next, count);
        ValueList.EnsureCapacity(ref _breaks, count);
        var points = contours.Points;
        for (var p = 0; p < points.Length; p++)
        {
            _segments.Add(default);
            _next[first + p] = -1;
        }

        for (var c = 0; c < contours.Count; c++)
        {
            var contour = contours[c];
            if (contour.FillCount < 3)
                continue;

            var start = first + contour.Start;
            _loops.Add(new Loop(start, contour.FillCount));
            for (var i = 0; i < contour.FillCount; i++)
            {
                var from = points[contour.Start + i];
                var to = points[contour.Start + (i + 1) % contour.FillCount];
                _segments[start + i] = new Segment(from.X, from.Y, to.X, to.Y);
                _next[start + i] = start + (i + 1) % contour.FillCount;
            }
        }
        return first;
    }

    /// <summary>One more than the last edge of the stores added.</summary>
    public int EdgeCount => _segments.Count;

    /// <summary>The edge after <paramref name="edge"/> in its contour, or -1 when it is no edge.</summary>
    public int Next(int edge) => _next[edge];

    public Segment Edge(int edge) => _segments[edge];

    public int Splits(int edge) => _splitStart[edge + 1] - _splitStart[edge];

    /// <summary>How far along <paramref name="edge"/> piece <paramref name="k"/> starts, from 0 to 1;
    /// <see cref="Splits"/> plus one is the edge's end.</summary>
    public double SplitAt(int edge, int k) =>
        k <= 0 ? 0
        : k > Splits(edge) ? 1
        : _splitKeys[_splitStart[edge] + k - 1] - edge;

    /// <summary>Piece <paramref name="k"/> of <paramref name="edge"/>, between its splits.</summary>
    public Segment Piece(int edge, int k)
    {
        ref readonly var whole = ref _segments[edge];
        var from = k <= 0 ? new Point(whole.Ax, whole.Ay) : _splitPoints[_splitStart[edge] + k - 1];
        var to =
            k >= Splits(edge) ? new Point(whole.Bx, whole.By) : _splitPoints[_splitStart[edge] + k];
        return new Segment(from.X, from.Y, to.X, to.Y);
    }

    /// <summary>Which side of piece <paramref name="k"/> of <paramref name="edge"/> lies outside what
    /// <see cref="Classify"/> was given: -1 its right, 1 its left, 0 neither or both.</summary>
    public sbyte Sign(int edge, int k) => _signs[edge + _splitStart[edge] + k];

    public void Find()
    {
        var count = _segments.Count;
        ValueList.EnsureCapacity(ref _tops, count);
        ValueList.EnsureCapacity(ref _order, count);
        ValueList.EnsureCapacity(ref _splitStart, count + 1);
        _breaks.AsSpan(0, count).Clear();
        _splitCount = 0;

        var live = 0;
        for (var e = 0; e < count; e++)
        {
            if (_next[e] < 0)
                continue;
            _tops[live] = Math.Min(_segments[e].Ay, _segments[e].By);
            _order[live++] = e;
        }
        _tops.AsSpan(0, live).Sort(_order.AsSpan(0, live));

        for (var oi = 0; oi < live; oi++)
        {
            var i = _order[oi];
            ref readonly var a = ref _segments[i];
            var bottom = Math.Max(a.Ay, a.By) + Snap;
            var left = Math.Min(a.Ax, a.Bx) - Snap;
            var right = Math.Max(a.Ax, a.Bx) + Snap;
            for (var oj = oi + 1; oj < live && _tops[oj] <= bottom; oj++)
            {
                var j = _order[oj];
                ref readonly var b = ref _segments[j];
                if (Math.Max(b.Ax, b.Bx) >= left && Math.Min(b.Ax, b.Bx) <= right)
                    Intersect(i, j);
            }
        }
        _splitKeys.AsSpan(0, _splitCount).Sort(_splitPoints.AsSpan(0, _splitCount));

        var split = 0;
        for (var e = 0; e <= count; e++)
        {
            _splitStart[e] = split;
            while (split < _splitCount && (int)_splitKeys[split] == e)
                split++;
        }
    }

    // Edges meeting at their shared point cross nothing there; only one doubling back over the other splits.
    void Intersect(int i, int j)
    {
        var a = _segments[i];
        var b = _segments[j];
        var adjacent = _next[i] == j || _next[j] == i;
        var rx = a.Bx - a.Ax;
        var ry = a.By - a.Ay;
        var sx = b.Bx - b.Ax;
        var sy = b.By - b.Ay;
        var lengthA = Math.Sqrt(rx * rx + ry * ry);
        var lengthB = Math.Sqrt(sx * sx + sy * sy);
        if (!(lengthA > 0 && lengthB > 0))
            return;

        var cross = rx * sy - ry * sx;
        var ox = b.Ax - a.Ax;
        var oy = b.Ay - a.Ay;
        if (Math.Abs(cross) <= Parallel * lengthA * lengthB)
        {
            if (Math.Abs(ox * ry - oy * rx) > Snap * lengthA)
                return;
            AddSplit(i, b.Ax, b.Ay, !adjacent);
            AddSplit(i, b.Bx, b.By, !adjacent);
            AddSplit(j, a.Ax, a.Ay, !adjacent);
            AddSplit(j, a.Bx, a.By, !adjacent);
            return;
        }
        if (adjacent)
            return;

        var t = (ox * sy - oy * sx) / cross;
        var u = (ox * ry - oy * rx) / cross;
        var slackT = Snap / lengthA;
        var slackU = Snap / lengthB;
        if (t < -slackT || t > 1 + slackT || u < -slackU || u > 1 + slackU)
            return;

        var x = a.Ax + rx * t;
        var y = a.Ay + ry * t;
        if (Near(x, y, a.Ax, a.Ay))
            (x, y) = (a.Ax, a.Ay);
        else if (Near(x, y, a.Bx, a.By))
            (x, y) = (a.Bx, a.By);
        else if (Near(x, y, b.Ax, b.Ay))
            (x, y) = (b.Ax, b.Ay);
        else if (Near(x, y, b.Bx, b.By))
            (x, y) = (b.Bx, b.By);
        AddSplit(i, x, y, true);
        AddSplit(j, x, y, true);
    }

    void AddSplit(int edge, double x, double y, bool breaks)
    {
        ref readonly var segment = ref _segments[edge];
        if (Near(x, y, segment.Ax, segment.Ay))
        {
            _breaks[edge] |= breaks;
            return;
        }
        if (Near(x, y, segment.Bx, segment.By))
        {
            _breaks[_next[edge]] |= breaks;
            return;
        }

        var dx = segment.Bx - segment.Ax;
        var dy = segment.By - segment.Ay;
        var t = ((x - segment.Ax) * dx + (y - segment.Ay) * dy) / (dx * dx + dy * dy);
        if (!(t > 0 && t < 1))
            return;

        if (_splitCount == _splitKeys.Length)
        {
            Array.Resize(ref _splitKeys, _splitKeys.Length * 2);
            Array.Resize(ref _splitPoints, _splitKeys.Length);
        }
        _splitKeys[_splitCount] = edge + t;
        _splitPoints[_splitCount++] = new Point(x, y);
    }

    static bool Near(double x, double y, double px, double py) =>
        (x - px) * (x - px) + (y - py) * (y - py) <= Snap * Snap;

    /// <summary>Gives every piece its <see cref="Sign"/>, probing the longest piece of each run on both sides with
    /// <paramref name="cover"/>.</summary>
    public void Classify<TCover>(ref TCover cover)
        where TCover : ICover, allows ref struct
    {
        ValueList.EnsureCapacity(ref _signs, _segments.Count + _splitCount);
        foreach (ref readonly var loop in _loops.AsSpan())
        {
            var start = loop.Start;
            var end = start + loop.Count;
            var first = -1;
            var firstPiece = 0;
            for (var e = start; e < end && first < 0; e++)
            {
                if (_breaks[e])
                    first = e;
                else if (Splits(e) > 0)
                    (first, firstPiece) = (e, 1);
            }

            if (first < 0)
            {
                var longest = start;
                for (var e = start + 1; e < end; e++)
                {
                    if (_segments[e].LengthSquared > _segments[longest].LengthSquared)
                        longest = e;
                }
                var sign = Side(_segments[longest], ref cover);
                for (var e = start; e < end; e++)
                    _signs[e + _splitStart[e]] = sign;
                continue;
            }

            var pieces = loop.Count + _splitStart[end] - _splitStart[start];
            var runLength = 0;
            var runEdge = first;
            var runPiece = firstPiece;
            var widest = default(Segment);
            var edge = first;
            var k = firstPiece;
            for (var step = 0; step <= pieces; step++)
            {
                if (step == pieces || (runLength > 0 && (k > 0 || _breaks[edge])))
                {
                    MarkRun(start, end, runEdge, runPiece, runLength, Side(widest, ref cover));
                    if (step == pieces)
                        break;
                    (runLength, runEdge, runPiece, widest) = (0, edge, k, default);
                }

                var piece = Piece(edge, k);
                if (piece.LengthSquared > widest.LengthSquared)
                    widest = piece;
                runLength++;

                if (++k > Splits(edge))
                {
                    k = 0;
                    edge = edge + 1 < end ? edge + 1 : start;
                }
            }
        }
    }

    void MarkRun(int start, int end, int edge, int k, int length, sbyte sign)
    {
        for (var i = 0; i < length; i++)
        {
            _signs[edge + _splitStart[edge] + k] = sign;
            if (++k > Splits(edge))
            {
                k = 0;
                edge = edge + 1 < end ? edge + 1 : start;
            }
        }
    }

    static sbyte Side<TCover>(in Segment piece, ref TCover cover)
        where TCover : ICover, allows ref struct
    {
        var from = new Vector2((float)piece.Ax, (float)piece.Ay);
        var to = new Vector2((float)piece.Bx, (float)piece.By);
        var length = Vector2.Distance(from, to);
        if (!(length > 0f))
            return 0;
        PathMath.PointsBeside(from, to, length, out var leftPoint, out var rightPoint);
        var left = cover.Covers(leftPoint);
        return left == cover.Covers(rightPoint) ? (sbyte)0
            : left ? (sbyte)-1
            : (sbyte)1;
    }

    internal readonly record struct Segment(double Ax, double Ay, double Bx, double By)
    {
        public double LengthSquared => (Bx - Ax) * (Bx - Ax) + (By - Ay) * (By - Ay);
    }

    readonly record struct Point(double X, double Y);

    readonly record struct Loop(int Start, int Count);
}

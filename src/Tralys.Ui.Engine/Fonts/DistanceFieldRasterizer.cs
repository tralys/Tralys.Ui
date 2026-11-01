using System.Numerics;
using Tralys.Ui.Rendering;
using Edge = Tralys.Ui.Rendering.PathCrossings.Segment;

namespace Tralys.Ui.Fonts;

/// <summary>Builds the signed distance field of an outline traced in texels, x right and y up, or of that outline
/// stroked with round joins and caps.</summary>
/// <remarks>Each texel holds 128 plus <see cref="LevelsPerTexel"/> for every texel its centre lies inside the outline,
/// or minus as many outside, rounded and clamped to a byte, so the field reaches <see cref="Spread"/> texels either
/// side of the edge. Inside is where the outline winds non-zero, so overlapping contours fill as one; a stroke is
/// inside within half its width of that region's edge. The bitmap spans the outline's control box, grown by half the
/// stroke, out to whole texels and then <see cref="Spread"/> more on every side. Reused, it allocates nothing once its
/// buffers have grown.</remarks>
[FramePath]
internal sealed class DistanceFieldRasterizer
{
    /// <summary>How many texels the field reaches either side of the outline before it clamps.</summary>
    public const int Spread = 4;

    /// <summary>Levels of a byte per texel of distance; 128 is the edge.</summary>
    public const int LevelsPerTexel = 32;

    const double Flatness = 0.02;
    const int MaxSplits = 12;

    // Past this beyond the drawn edge a texel's level clamps whatever its nearest edge, so an edge reaches no further.
    const double ClampedReach = Spread + 1.0 / LevelsPerTexel;

    readonly PathContours _contours = new();
    readonly PathCrossings _crossings = new();
    ValueList<Edge> _boundary = new(64);
    Crossing[] _rowCrossings = [];
    int[] _rowStarts = [];
    int[] _rowEnds = [];
    double[] _nearest = [];
    byte[] _texels = [];
    double _x;
    double _y;
    double _minX = double.PositiveInfinity;
    double _minY = double.PositiveInfinity;
    double _maxX = double.NegativeInfinity;
    double _maxY = double.NegativeInfinity;

    /// <summary>Forgets the outline traced so far.</summary>
    public void Clear()
    {
        _contours.Clear();
        _minX = _minY = double.PositiveInfinity;
        _maxX = _maxY = double.NegativeInfinity;
    }

    /// <summary>Starts a contour at (<paramref name="x"/>, <paramref name="y"/>), closing the one before it.</summary>
    public void MoveTo(double x, double y)
    {
        _contours.StartFigure(Point(x, y));
        _x = x;
        _y = y;
        Include(x, y);
    }

    public void LineTo(double x, double y)
    {
        Include(x, y);
        AddPoint(x, y);
    }

    /// <summary>A quadratic Bézier from the current point through control (<paramref name="cx"/>,
    /// <paramref name="cy"/>).</summary>
    public void QuadraticTo(double cx, double cy, double x, double y)
    {
        Include(cx, cy);
        Include(x, y);
        var bendX = _x - 2 * cx + x;
        var bendY = _y - 2 * cy + y;
        var steps = Math.Max(
            2,
            (int)Math.Ceiling(Math.Sqrt(Math.Sqrt(bendX * bendX + bendY * bendY) / (4 * Flatness)))
        );
        var fromX = _x;
        var fromY = _y;
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            var u = 1 - t;
            AddPoint(
                u * u * fromX + 2 * u * t * cx + t * t * x,
                u * u * fromY + 2 * u * t * cy + t * t * y
            );
        }
    }

    /// <summary>A cubic Bézier from the current point through controls (<paramref name="c1x"/>,
    /// <paramref name="c1y"/>) and (<paramref name="c2x"/>, <paramref name="c2y"/>).</summary>
    public void CubicTo(double c1x, double c1y, double c2x, double c2y, double x, double y)
    {
        Include(c1x, c1y);
        Include(c2x, c2y);
        Include(x, y);
        Flatten(_x, _y, c1x, c1y, c2x, c2y, x, y, 0);
    }

    /// <summary>Closes the last contour and fills the field: of the outline, or with <paramref name="halfStroke"/>
    /// above zero, of the band that far either side of its edge.</summary>
    /// <returns>False when nothing was traced that encloses anything. The bitmap's texels stay valid until the next
    /// call.</returns>
    public bool TryRasterize(double halfStroke, out GlyphBitmap bitmap)
    {
        bitmap = default;
        _contours.EndFigure();
        if (!FindBoundary())
            return false;

        var stroke = Math.Max(halfStroke, 0);
        var left = (int)Math.Floor(_minX - stroke) - Spread;
        var bottom = (int)Math.Floor(_minY - stroke) - Spread;
        var top = (int)Math.Ceiling(_maxY + stroke) + Spread;
        var width = (int)Math.Ceiling(_maxX + stroke) + Spread - left;
        var height = top - bottom;
        var count = width * height;
        ValueList.EnsureCapacity(ref _nearest, count);
        ValueList.EnsureCapacity(ref _texels, count);

        var reach = ClampedReach + stroke;
        var nearest = _nearest.AsSpan(0, count);
        nearest.Fill(reach * reach);
        foreach (ref readonly var edge in _boundary.AsSpan())
            Near(edge, nearest, left, top, width, height, reach);

        var texels = _texels.AsSpan(0, count);
        if (stroke > 0)
        {
            for (var i = 0; i < count; i++)
                texels[i] = Level(stroke - Math.Sqrt(nearest[i]));
        }
        else
        {
            Cross(top, height);
            for (var row = 0; row < height; row++)
                FillRow(
                    nearest.Slice(row * width, width),
                    texels.Slice(row * width, width),
                    left,
                    _rowCrossings.AsSpan(_rowStarts[row], _rowEnds[row] - _rowStarts[row])
                );
        }

        bitmap = new GlyphBitmap(texels, width, height, width, left, top);
        return true;
    }

    static byte Level(double inside) =>
        (byte)Math.Clamp(Math.Round(128 + LevelsPerTexel * inside), 0, 255);

    // Overlapping contours fill as one, so only edges between filled and empty space are the outline.
    bool FindBoundary()
    {
        _crossings.Clear();
        _crossings.Add(_contours);
        _crossings.Find();
        var cover = new PathCrossings.Fill(_contours, FillRule.Nonzero);
        _crossings.Classify(ref cover);

        _boundary.Clear();
        var edges = false;
        for (var e = 0; e < _crossings.EdgeCount; e++)
        {
            if (_crossings.Next(e) < 0)
                continue;

            edges = true;
            for (var k = 0; k <= _crossings.Splits(e); k++)
            {
                if (_crossings.Sign(e, k) != 0)
                    _boundary.Add(_crossings.Piece(e, k));
            }
        }
        return edges;
    }

    static void Near(
        in Edge edge,
        Span<double> nearest,
        int left,
        int top,
        int width,
        int height,
        double reach
    )
    {
        var firstRow = Math.Max(0, (int)Math.Floor(top - 0.5 - Math.Max(edge.Ay, edge.By) - reach));
        var lastRow = Math.Min(
            height - 1,
            (int)Math.Ceiling(top - 0.5 - Math.Min(edge.Ay, edge.By) + reach)
        );

        var dx = edge.Bx - edge.Ax;
        var dy = edge.By - edge.Ay;
        var lengthSquared = dx * dx + dy * dy;
        if (!(lengthSquared > 0))
            return;

        var perY = dy == 0 ? 0 : 1 / dy;
        var startX = new Vector<double>(edge.Ax);
        var startY = new Vector<double>(edge.Ay);
        var stepX = new Vector<double>(dx);
        var stepY = new Vector<double>(dy);
        var lengths = new Vector<double>(lengthSquared);
        for (var row = firstRow; row <= lastRow; row++)
        {
            var py = top - row - 0.5;
            var from = Math.Min(edge.Ax, edge.Bx);
            var to = Math.Max(edge.Ax, edge.Bx);
            if (dy != 0)
            {
                var low = edge.Ax + Math.Clamp((py - reach - edge.Ay) * perY, 0, 1) * dx;
                var high = edge.Ax + Math.Clamp((py + reach - edge.Ay) * perY, 0, 1) * dx;
                from = Math.Min(low, high);
                to = Math.Max(low, high);
            }

            var firstColumn = Math.Max(0, (int)Math.Floor(from - reach - left - 0.5));
            var lastColumn = Math.Min(width - 1, (int)Math.Ceiling(to + reach - left - 0.5));
            var line = nearest.Slice(row * width, width);
            var across = (py - edge.Ay) * dy;
            var column = firstColumn;
            // The same operations lane for lane as the loop below, so no texel's level depends on which loop took it.
            if (Vector.IsHardwareAccelerated)
            {
                var acrossLanes = new Vector<double>(across);
                var rowY = new Vector<double>(py);
                for (
                    ;
                    column + Vector<double>.Count <= lastColumn + 1;
                    column += Vector<double>.Count
                )
                {
                    var px = new Vector<double>(left + column + 0.5) + Vector<double>.Indices;
                    var t = Vector.Min(
                        Vector.Max(
                            ((px - startX) * stepX + acrossLanes) / lengths,
                            Vector<double>.Zero
                        ),
                        Vector<double>.One
                    );
                    var ox = startX + t * stepX - px;
                    var oy = startY + t * stepY - rowY;
                    var cells = line.Slice(column, Vector<double>.Count);
                    Vector.Min(new Vector<double>(cells), ox * ox + oy * oy).CopyTo(cells);
                }
            }

            for (; column <= lastColumn; column++)
            {
                var px = left + column + 0.5;
                var t = Math.Clamp(((px - edge.Ax) * dx + across) / lengthSquared, 0, 1);
                var ox = edge.Ax + t * dx - px;
                var oy = edge.Ay + t * dy - py;
                ref var best = ref line[column];
                best = Math.Min(best, ox * ox + oy * oy);
            }
        }
    }

    void Cross(int top, int height)
    {
        ValueList.EnsureCapacity(ref _rowStarts, height + 1);
        ValueList.EnsureCapacity(ref _rowEnds, height);
        var starts = _rowStarts.AsSpan(0, height + 1);
        var ends = _rowEnds.AsSpan(0, height);
        starts.Clear();
        for (var e = 0; e < _crossings.EdgeCount; e++)
        {
            if (_crossings.Next(e) < 0)
                continue;

            var edge = _crossings.Edge(e);
            var (first, last) = RowsSpanned(edge, top, height);
            for (var row = first; row <= last; row++)
            {
                if (Crosses(edge, top - row - 0.5))
                    starts[row + 1]++;
            }
        }

        for (var row = 0; row < height; row++)
            starts[row + 1] += starts[row];
        starts[..height].CopyTo(ends);
        ValueList.EnsureCapacity(ref _rowCrossings, starts[height]);

        for (var e = 0; e < _crossings.EdgeCount; e++)
        {
            if (_crossings.Next(e) < 0)
                continue;

            var edge = _crossings.Edge(e);
            var (first, last) = RowsSpanned(edge, top, height);
            for (var row = first; row <= last; row++)
            {
                var py = top - row - 0.5;
                if (!Crosses(edge, py))
                    continue;

                _rowCrossings[ends[row]++] = new Crossing(
                    edge.Ax + (py - edge.Ay) * (edge.Bx - edge.Ax) / (edge.By - edge.Ay),
                    edge.By > edge.Ay ? 1 : -1
                );
            }
        }
    }

    static (int First, int Last) RowsSpanned(in Edge edge, int top, int height) =>
        (
            Math.Max(0, (int)Math.Floor(top - 0.5 - Math.Max(edge.Ay, edge.By))),
            Math.Min(height - 1, (int)Math.Ceiling(top - 0.5 - Math.Min(edge.Ay, edge.By)))
        );

    static bool Crosses(in Edge edge, double py) => edge.Ay <= py != edge.By <= py;

    static void FillRow(
        ReadOnlySpan<double> nearest,
        Span<byte> texels,
        int left,
        Span<Crossing> crossings
    )
    {
        var winding = 0;
        for (var i = 0; i < crossings.Length; i++)
        {
            winding += crossings[i].Direction;
            var crossing = crossings[i];
            var j = i - 1;
            for (; j >= 0 && crossings[j].X > crossing.X; j--)
                crossings[j + 1] = crossings[j];
            crossings[j + 1] = crossing;
        }

        var next = 0;
        for (var column = 0; column < texels.Length; column++)
        {
            var px = left + column + 0.5;
            for (; next < crossings.Length && crossings[next].X <= px; next++)
                winding -= crossings[next].Direction;

            var distance = Math.Sqrt(nearest[column]);
            texels[column] = Level(winding != 0 ? distance : -distance);
        }
    }

    void Flatten(
        double x0,
        double y0,
        double x1,
        double y1,
        double x2,
        double y2,
        double x3,
        double y3,
        int splits
    )
    {
        var ux = 3 * x1 - 2 * x0 - x3;
        var uy = 3 * y1 - 2 * y0 - y3;
        var vx = 3 * x2 - 2 * x3 - x0;
        var vy = 3 * y2 - 2 * y3 - y0;
        if (
            splits >= MaxSplits
            || Math.Max(ux * ux, vx * vx) + Math.Max(uy * uy, vy * vy) <= 16 * Flatness * Flatness
        )
        {
            AddPoint(x3, y3);
            return;
        }

        var ax = (x0 + x1) / 2;
        var ay = (y0 + y1) / 2;
        var bx = (x1 + x2) / 2;
        var by = (y1 + y2) / 2;
        var cx = (x2 + x3) / 2;
        var cy = (y2 + y3) / 2;
        var dx = (ax + bx) / 2;
        var dy = (ay + by) / 2;
        var ex = (bx + cx) / 2;
        var ey = (by + cy) / 2;
        var mx = (dx + ex) / 2;
        var my = (dy + ey) / 2;
        Flatten(x0, y0, ax, ay, dx, dy, mx, my, splits + 1);
        Flatten(mx, my, ex, ey, cx, cy, x3, y3, splits + 1);
    }

    void AddPoint(double x, double y)
    {
        _contours.LineTo(Point(x, y));
        _x = x;
        _y = y;
    }

    void Include(double x, double y)
    {
        _minX = Math.Min(_minX, x);
        _minY = Math.Min(_minY, y);
        _maxX = Math.Max(_maxX, x);
        _maxY = Math.Max(_maxY, y);
    }

    static Vector2 Point(double x, double y) => new((float)x, (float)y);

    readonly record struct Crossing(double X, int Direction);
}

/// <summary>A glyph's field, one byte per texel and <see cref="Stride"/> bytes per row, its top-left texel
/// <see cref="Left"/> right of the pen and <see cref="Top"/> above the baseline.</summary>
internal readonly ref struct GlyphBitmap(
    ReadOnlySpan<byte> texels,
    int width,
    int height,
    int stride,
    int left,
    int top
)
{
    public ReadOnlySpan<byte> Texels { get; } = texels;

    public int Width { get; } = width;

    public int Height { get; } = height;

    public int Stride { get; } = stride;

    public int Left { get; } = left;

    public int Top { get; } = top;
}

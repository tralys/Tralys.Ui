using System.Numerics;

namespace Tralys.Ui.Rendering;

// The stroker's joins and caps follow Prowl.Quill's PolylineMesher (MIT); see THIRD-PARTY-NOTICES.md.
[FramePath]
internal sealed class PathTessellator
{
    const float Fringe = 0.5f;
    public const int MaxVertices = 1 << 20;

    readonly PathContours _contours = new();
    readonly PathCrossings _crossings = new();
    Edge[] _edges = new Edge[64];
    float[] _edgeTops = new float[64];
    int[] _edgeOrder = new int[64];
    float[] _ys = new float[128];
    int[] _active = new int[32];
    float[] _activeTop = new float[32];
    float[] _activeBottom = new float[32];
    Vector2[] _offset = new Vector2[64];
    float[] _inset = new float[8];
    float[] _outset = new float[8];
    float[] _innerCoverage = new float[8];
    bool[] _fringed = new bool[8];
    ValueList<Vector2> _dash = new(16);
    int _edgeCount;
    int _dashSteps;

    Vector2[] _positions = new Vector2[256];
    float[] _coverage = new float[256];
    ValueList<int> _indices = new(384);
    int _vertexCount;

    public ReadOnlySpan<Vector2> Positions => _positions.AsSpan(0, _vertexCount);

    public ReadOnlySpan<float> Coverage => _coverage.AsSpan(0, _vertexCount);

    public ReadOnlySpan<int> Indices => _indices.AsSpan();

    public bool Overflowed { get; private set; }

    public void Clear()
    {
        _contours.Clear();
        ClearMesh();
    }

    public void ClearMesh()
    {
        _vertexCount = 0;
        _indices.Clear();
        Overflowed = false;
    }

    public void AddPath(
        ReadOnlySpan<PathVerb> verbs,
        ReadOnlySpan<Vector2> points,
        in Matrix3x2 transform
    ) => _contours.Add(verbs, points, transform);

    public void Fill(FillRule rule)
    {
        ClassifyContours(rule);
        BuildEdges();
        Sweep(rule);
        FringeContours();
    }

    // Dashes alternate dash and gap from a dash, in an even count, each scaled by dashScale; none strokes solid.
    public void Stroke(
        float halfWidth,
        PenLineJoin join,
        PenLineCap startCap,
        PenLineCap endCap,
        float miterLimit,
        ReadOnlySpan<float> dashes,
        float dashScale,
        float dashOffset,
        PenLineCap dashCap
    )
    {
        var alpha = 1f;
        if (halfWidth < Fringe)
        {
            alpha = halfWidth / Fringe;
            halfWidth = Fringe;
        }

        var style = new StrokeStyle(halfWidth, alpha, join, startCap, endCap, miterLimit, dashCap);
        var cursor = new DashCursor(dashes, dashScale, dashOffset * dashScale);
        _dashSteps = 0;
        for (var c = 0; c < _contours.Count; c++)
        {
            var contour = _contours[c];
            var points = _contours.PointsOf(contour);
            if (points.Length < 2)
                continue;
            if (cursor.Period > 0f)
                StrokeDashes(points, contour.Closed, style, cursor);
            else
                StrokeContour(points, contour.Closed, style, startCap, endCap);
            if (Overflowed)
                return;
        }
    }

    // Each dash is stroked as an open polyline; on a closed figure the dash running through its start joins the first.
    void StrokeDashes(
        ReadOnlySpan<Vector2> points,
        bool closed,
        in StrokeStyle style,
        DashCursor cursor
    )
    {
        var startsOn = cursor.On;
        var defersFirst = closed && startsOn;
        var firstEnd = -1;
        var firstDirection = Vector2.Zero;
        var dashStart = 0;
        var startCap = closed || !startsOn ? style.DashCap : style.StartCap;
        var direction = Vector2.Zero;
        _dash.Clear();
        if (cursor.On)
            AppendDash(points[0], dashStart);

        var segments = closed ? points.Length : points.Length - 1;
        for (var s = 0; s < segments; s++)
        {
            var a = points[s];
            var b = points[(s + 1) % points.Length];
            var length = Vector2.Distance(a, b);
            if (!(length > 0f))
                continue;

            direction = (b - a) / length;
            var t = 0f;
            // On a closed figure, a boundary at its start belongs to the wrap after the loop.
            var wraps = closed && s == segments - 1;
            while (cursor.Remaining <= length - t - (wraps ? PathMath.Snap : 0f))
            {
                if (++_dashSteps > DashCursor.MaxSteps)
                {
                    Overflowed = true;
                    return;
                }

                t += cursor.Remaining;
                AppendDash(a + direction * t, dashStart);
                if (cursor.On && defersFirst && firstEnd < 0)
                {
                    firstEnd = _dash.Count;
                    firstDirection = direction;
                    dashStart = _dash.Count;
                }
                else if (cursor.On)
                {
                    StrokeDash(dashStart, direction, startCap, style.DashCap, style);
                    _dash.Truncate(dashStart);
                }
                if (cursor.On)
                    startCap = style.DashCap;

                cursor.Step();
            }
            cursor.Advance(length - t);
            if (cursor.On)
                AppendDash(b, dashStart);
        }

        if (cursor.On && defersFirst && firstEnd < 0)
        {
            StrokeContour(points, true, style, style.DashCap, style.DashCap);
        }
        else if (cursor.On)
        {
            for (var i = 1; i < firstEnd; i++)
                AppendDash(_dash[i], dashStart);
            StrokeDash(
                dashStart,
                direction,
                startCap,
                closed ? style.DashCap : style.EndCap,
                style
            );
        }
        else if (firstEnd > 0)
        {
            _dash.Truncate(firstEnd);
            StrokeDash(0, firstDirection, style.DashCap, style.DashCap, style);
        }
    }

    void AppendDash(Vector2 point, int start)
    {
        if (_dash.Count > start && Vector2.DistanceSquared(point, _dash[_dash.Count - 1]) <= 1e-8f)
            return;
        _dash.Add(point);
    }

    // A dash of no length still shows its caps, along the direction it lies in.
    void StrokeDash(
        int start,
        Vector2 direction,
        PenLineCap startCap,
        PenLineCap endCap,
        in StrokeStyle style
    )
    {
        if (_dash.Count - start == 1)
        {
            if (startCap == PenLineCap.Flat && endCap == PenLineCap.Flat)
                return;
            _dash.Add(_dash[_dash.Count - 1] + direction * 1e-3f);
        }
        if (_dash.Count - start >= 2)
            StrokeContour(_dash.AsSpan(start, _dash.Count - start), false, style, startCap, endCap);
    }

    void ClassifyContours(FillRule rule)
    {
        _crossings.Clear();
        _crossings.Add(_contours);
        _crossings.Find();
        var cover = new PathCrossings.Fill(_contours, rule);
        _crossings.Classify(ref cover);

        var contours = _contours.Count;
        ValueList.EnsureCapacity(ref _inset, contours);
        ValueList.EnsureCapacity(ref _outset, contours);
        ValueList.EnsureCapacity(ref _innerCoverage, contours);
        ValueList.EnsureCapacity(ref _fringed, contours);
        ValueList.EnsureCapacity(ref _offset, _contours.PointCount * 2);
        for (var c = 0; c < contours; c++)
        {
            var contour = _contours[c];
            var crossed = false;
            var fringed = false;
            for (
                var p = contour.Start;
                contour.FillCount >= 3 && p < contour.Start + contour.FillCount;
                p++
            )
            {
                var splits = _crossings.Splits(p);
                crossed |= splits > 0;
                for (var k = 0; k <= splits; k++)
                    fringed |= _crossings.Sign(p, k) != 0;
            }
            _fringed[c] = fringed;
            Thin(contour, crossed, c);
            OffsetContour(rule, contour, c);
        }
    }

    // A contour thinner than its fringe is drawn as the stroke draws a thin line: a fringe's width, fainter.
    void Thin(in Contour contour, bool crossed, int c)
    {
        _inset[c] = Fringe;
        _outset[c] = Fringe;
        _innerCoverage[c] = 1f;
        var points = _contours.FillPointsOf(contour);
        if (crossed || points.Length < 3)
            return;

        var area = 0f;
        var perimeter = 0f;
        for (var i = 0; i < points.Length; i++)
        {
            var next = points[(i + 1) % points.Length];
            area += PathMath.Cross(points[i], next);
            perimeter += Vector2.Distance(points[i], next);
        }
        var thickness = MathF.Abs(area) / perimeter;
        if (!(thickness < 2f * Fringe))
            return;

        _inset[c] = thickness * 0.5f;
        _outset[c] = 2f * Fringe - thickness * 0.5f;
        _innerCoverage[c] = thickness / (2f * Fringe);
    }

    void OffsetContour(FillRule rule, in Contour contour, int c)
    {
        var points = _contours.FillPointsOf(contour);
        for (var i = 0; i < points.Length; i++)
        {
            var at = (contour.Start + i) * 2;
            var sign = points.Length < 3 ? 0 : _crossings.Sign(contour.Start + i, 0);
            if (sign == 0)
            {
                _offset[at] = points[i];
                _offset[at + 1] = points[i];
                continue;
            }

            var previous = points[(i + points.Length - 1) % points.Length];
            var next = points[(i + 1) % points.Length];
            var average =
                (PathMath.Normal(points[i] - previous) + PathMath.Normal(next - points[i]))
                * (0.5f * sign);
            var length = average.Length();
            Vector2 miter;
            if (length >= 1e-3f)
            {
                miter = average / length * MathF.Min(1f / length, 4f);
            }
            else
            {
                // A figure turning straight back has no bisector: its outside lies past the tip or behind it.
                var ahead = Vector2.Normalize(points[i] - previous);
                var inside = PathMath.Covers(rule, _contours.Winding(points[i] + ahead * 0.05f));
                miter = (inside ? -ahead : ahead) * 4f;
            }
            _offset[at] = points[i] - miter * _inset[c];
            _offset[at + 1] = points[i] + miter * _outset[c];
        }
    }

    void FringeContours()
    {
        for (var c = 0; c < _contours.Count; c++)
        {
            if (!_fringed[c])
                continue;

            var contour = _contours[c];
            var count = contour.FillCount;
            var coverage = _innerCoverage[c];
            var first = _vertexCount;
            for (var i = 0; i < count; i++)
            {
                AddVertex(_offset[(contour.Start + i) * 2], coverage);
                AddVertex(_offset[(contour.Start + i) * 2 + 1], 0f);
            }
            for (var i = 0; i < count; i++)
            {
                var p = contour.Start + i;
                var a = first + i * 2;
                var b = first + (i + 1) % count * 2;
                var splits = _crossings.Splits(p);
                for (var k = 0; k <= splits; k++)
                {
                    if (_crossings.Sign(p, k) == 0)
                        continue;
                    var from = (float)_crossings.SplitAt(p, k);
                    var to = (float)_crossings.SplitAt(p, k + 1);
                    if (!(to > from))
                        continue;
                    var near = k == 0 ? a : FringeAt(p, from, coverage);
                    var far = k == splits ? b : FringeAt(p, to, coverage);
                    AddQuad(near, near + 1, far, far + 1);
                }
            }
        }
    }

    int FringeAt(int edge, float t, float coverage)
    {
        var end = _crossings.Next(edge);
        var first = _vertexCount;
        AddVertex(Vector2.Lerp(_offset[edge * 2], _offset[end * 2], t), coverage);
        AddVertex(Vector2.Lerp(_offset[edge * 2 + 1], _offset[end * 2 + 1], t), 0f);
        return first;
    }

    void BuildEdges()
    {
        _edgeCount = 0;
        for (var c = 0; c < _contours.Count; c++)
        {
            var contour = _contours[c];
            if (contour.FillCount < 3)
                continue;

            var alpha = _innerCoverage[c];
            for (var i = 0; i < contour.FillCount; i++)
            {
                var j = (i + 1) % contour.FillCount;
                var a = _offset[(contour.Start + i) * 2];
                var b = _offset[(contour.Start + j) * 2];
                if (a.Y == b.Y)
                    continue;

                if (_edgeCount == _edges.Length)
                    Array.Resize(ref _edges, _edges.Length * 2);
                _edges[_edgeCount++] =
                    a.Y < b.Y ? new Edge(a, b, 1, alpha) : new Edge(b, a, -1, alpha);
            }
        }
    }

    void Sweep(FillRule rule)
    {
        if (_edgeCount == 0)
            return;

        if (_ys.Length < _edgeCount * 2)
            Array.Resize(ref _ys, _edges.Length * 2);
        if (_edgeTops.Length < _edgeCount)
        {
            Array.Resize(ref _edgeTops, _edges.Length);
            Array.Resize(ref _edgeOrder, _edges.Length);
        }

        for (var e = 0; e < _edgeCount; e++)
        {
            _ys[e * 2] = _edges[e].Top.Y;
            _ys[e * 2 + 1] = _edges[e].Bottom.Y;
            _edgeTops[e] = _edges[e].Top.Y;
            _edgeOrder[e] = e;
        }
        var ys = _ys.AsSpan(0, _edgeCount * 2);
        // NativeAOT's comparer-less span sort allocates a delegate per call.
        ys.Sort(static (a, b) => a.CompareTo(b));
        _edgeTops.AsSpan(0, _edgeCount).Sort(_edgeOrder.AsSpan(0, _edgeCount));

        var activeCount = 0;
        var nextEdge = 0;
        var previous = float.NaN;
        for (var y = 0; y < ys.Length - 1; y++)
        {
            var top = ys[y];
            if (top == previous)
                continue;
            previous = top;

            var write = 0;
            for (var read = 0; read < activeCount; read++)
            {
                if (_edges[_active[read]].Bottom.Y > top)
                    _active[write++] = _active[read];
            }
            activeCount = write;
            while (nextEdge < _edgeCount && _edges[_edgeOrder[nextEdge]].Top.Y <= top)
            {
                var edge = _edgeOrder[nextEdge++];
                if (_edges[edge].Bottom.Y <= top)
                    continue;
                if (activeCount == _active.Length)
                {
                    Array.Resize(ref _active, _active.Length * 2);
                    Array.Resize(ref _activeTop, _active.Length);
                    Array.Resize(ref _activeBottom, _active.Length);
                }
                _active[activeCount++] = edge;
            }

            var bottom = top;
            for (var k = y + 1; k < ys.Length; k++)
            {
                if (ys[k] > top)
                {
                    bottom = ys[k];
                    break;
                }
            }
            if (bottom <= top)
                break;

            SweepBand(rule, activeCount, top, bottom);
        }
    }

    // Each split passes one of at most n(n-1)/2 crossings; if rounding splits past that, the rest emits whole.
    void SweepBand(FillRule rule, int activeCount, float top, float bottom)
    {
        var splits = 64L + (long)activeCount * activeCount;
        while (!Overflowed)
        {
            for (var i = 0; i < activeCount; i++)
            {
                ref readonly var edge = ref _edges[_active[i]];
                _activeTop[i] = edge.XAt(top);
                _activeBottom[i] = edge.XAt(bottom);
            }

            for (var i = 1; i < activeCount; i++)
            {
                var edge = _active[i];
                var x0 = _activeTop[i];
                var x1 = _activeBottom[i];
                var j = i - 1;
                for (
                    ;
                    j >= 0
                        && (_activeTop[j] > x0 || (_activeTop[j] == x0 && _activeBottom[j] > x1));
                    j--
                )
                {
                    _active[j + 1] = _active[j];
                    _activeTop[j + 1] = _activeTop[j];
                    _activeBottom[j + 1] = _activeBottom[j];
                }
                _active[j + 1] = edge;
                _activeTop[j + 1] = x0;
                _activeBottom[j + 1] = x1;
            }

            var crossing = 1f;
            for (var i = 1; i < activeCount; i++)
            {
                if (_activeBottom[i - 1] <= _activeBottom[i])
                    continue;
                var gapTop = _activeTop[i] - _activeTop[i - 1];
                var closing = gapTop - (_activeBottom[i] - _activeBottom[i - 1]);
                if (closing > 0f)
                    crossing = MathF.Min(crossing, gapTop / closing);
            }

            // However thin, what is left must emit: the rasterizer snaps a sliver's gap onto a whole pixel row.
            var middle = top + (bottom - top) * MathF.Max(crossing, 1e-4f);
            if (crossing >= 1f || --splits < 0 || !(middle > top && bottom - middle > 1e-5f))
            {
                EmitSpans(rule, activeCount, top, bottom);
                return;
            }

            for (var i = 0; i < activeCount; i++)
                _activeBottom[i] = _edges[_active[i]].XAt(middle);
            EmitSpans(rule, activeCount, top, middle);
            top = middle;
        }
    }

    void EmitSpans(FillRule rule, int activeCount, float top, float bottom)
    {
        var winding = 0;
        var inside = false;
        var start = 0;
        for (var i = 0; i < activeCount; i++)
        {
            winding += _edges[_active[i]].Winding;
            var now = PathMath.Covers(rule, winding);
            if (now == inside)
                continue;

            if (now)
            {
                start = i;
            }
            else if (
                _activeTop[i] - _activeTop[start] > 1e-6f
                || _activeBottom[i] - _activeBottom[start] > 1e-6f
            )
            {
                var first = _vertexCount;
                var coverage = MathF.Min(_edges[_active[start]].Alpha, _edges[_active[i]].Alpha);
                AddVertex(new Vector2(_activeTop[start], top), coverage);
                AddVertex(new Vector2(_activeTop[i], top), coverage);
                AddVertex(new Vector2(_activeBottom[start], bottom), coverage);
                AddVertex(new Vector2(_activeBottom[i], bottom), coverage);
                AddQuad(first, first + 1, first + 2, first + 3);
            }
            inside = now;
        }
    }

    void StrokeContour(
        ReadOnlySpan<Vector2> points,
        bool closed,
        in StrokeStyle style,
        PenLineCap startCap,
        PenLineCap endCap
    )
    {
        var halfWidth = style.HalfWidth;
        var alpha = style.Alpha;
        var join = style.Join;
        var turnCap = join == PenLineJoin.Round ? PenLineCap.Round : PenLineCap.Flat;
        // Traced there and back, a closed figure on one line would cover its span twice.
        if (closed && OnOneLine(points, out var low, out var high))
        {
            StrokeContour([low, high], false, style, turnCap, turnCap);
            return;
        }

        var count = points.Length;
        var firstRib = -1;
        var lastRib = -1;

        if (!closed)
        {
            var direction = Vector2.Normalize(points[1] - points[0]);
            lastRib = Cap(points[0], direction, outward: -1f, halfWidth, alpha, startCap);
        }

        var from = closed ? 0 : 1;
        var to = closed ? count : count - 1;
        for (var i = from; i < to; i++)
        {
            var previous = points[(i + count - 1) % count];
            var point = points[i];
            var next = points[(i + 1) % count];
            var d0 = Vector2.Normalize(point - previous);
            var d1 = Vector2.Normalize(next - point);
            var n0 = PathMath.Normal(d0);
            var n1 = PathMath.Normal(d1);
            var turn = PathMath.Cross(d0, d1);

            int entry;
            int exit;
            if (MathF.Abs(turn) < 1e-4f && Vector2.Dot(d0, d1) > 0f)
            {
                entry = exit = AddRib(point, n1, n1, halfWidth, alpha);
            }
            else if (MathF.Abs(turn) < 1e-4f)
            {
                entry = Cap(point, d0, outward: 1f, halfWidth, alpha, turnCap);
                var start = turnCap == PenLineCap.Flat ? point + d1 * Fringe : point;
                exit = AddRib(start, n1, n1, halfWidth, alpha);
            }
            else
            {
                var average = (n0 + n1) * 0.5f;
                var length2 = average.LengthSquared();
                var bisector = average / length2;
                var reach = MathF.Min(
                    Vector2.Distance(point, previous),
                    Vector2.Distance(point, next)
                );
                var span = reach / (halfWidth + Fringe);
                var innerFits = length2 * (1f + span * span) >= 1f;
                var mitered =
                    join == PenLineJoin.Miter && 1f / MathF.Sqrt(length2) <= style.MiterLimit;
                var steps =
                    mitered ? (innerFits ? 0 : 2)
                    : join == PenLineJoin.Round ? ArcSteps(n0, n1, halfWidth)
                    : 1;

                var outerLeft = turn < 0f;
                var side = outerLeft ? 1f : -1f;
                var first = n0 * side;
                var last = n1 * side;
                // An inner corner past the shorter side's far end would fold the mesh over the stroke.
                var inner = innerFits ? bisector * -side : Vector2.Zero;
                var innerCore = point + inner * (halfWidth - Fringe);
                var innerFringe = point + inner * (halfWidth + Fringe);
                var before = first;
                var core = Vector2.Zero;
                var fringe = Vector2.Zero;
                var rib = -1;
                entry = -1;
                for (var s = 0; s <= steps; s++)
                {
                    if (mitered)
                    {
                        var outer =
                            steps == 0 || s == 1 ? bisector
                            : s == 0 ? n0
                            : n1;
                        core = point + outer * side * (halfWidth - Fringe);
                        fringe = point + outer * side * (halfWidth + Fringe);
                    }
                    else
                    {
                        var outer = Sweep(first, last, s, steps);
                        var after =
                            s == steps
                                ? last
                                : Vector2.Normalize(outer + Sweep(first, last, s + 1, steps));
                        var mitre = Mitre(before, after);
                        core = point + outer * halfWidth - mitre * Fringe;
                        fringe = point + outer * halfWidth + mitre * Fringe;
                        before = after;
                    }

                    var previousRib = rib;
                    rib = JoinRib(outerLeft, core, fringe, innerCore, innerFringe, alpha);
                    if (previousRib >= 0)
                    {
                        Connect(previousRib, rib);
                    }
                    else if (innerFits)
                    {
                        entry = rib;
                    }
                    else
                    {
                        var across = point - first * (halfWidth - Fringe);
                        var beyond = point - first * (halfWidth + Fringe);
                        entry = JoinRib(outerLeft, core, fringe, across, beyond, alpha);
                        Connect(entry, rib);
                    }
                }

                if (innerFits)
                {
                    exit = rib;
                }
                else
                {
                    var across = point - last * (halfWidth - Fringe);
                    var beyond = point - last * (halfWidth + Fringe);
                    exit = JoinRib(outerLeft, core, fringe, across, beyond, alpha);
                    Connect(rib, exit);
                }
            }

            Connect(lastRib, entry);
            if (firstRib < 0)
                firstRib = entry;
            lastRib = exit;
        }

        if (closed)
        {
            Connect(lastRib, firstRib);
            return;
        }

        var end = points[count - 1];
        var endDirection = Vector2.Normalize(end - points[count - 2]);
        var cap = Cap(end, endDirection, outward: 1f, halfWidth, alpha, endCap);
        Connect(lastRib, cap);
    }

    int Cap(
        Vector2 point,
        Vector2 direction,
        float outward,
        float halfWidth,
        float alpha,
        PenLineCap cap
    )
    {
        var normal = PathMath.Normal(direction);
        var past = direction * outward;
        switch (cap)
        {
            case PenLineCap.Round:
            case PenLineCap.Triangle:
                return Fan(point, normal, past, halfWidth, alpha, cap == PenLineCap.Round);
            default:
                var tip = cap == PenLineCap.Square ? point + past * halfWidth : point;
                var rib = AddRib(tip - past * Fringe, normal, normal, halfWidth, alpha);
                var edge = AddRib(tip + past * Fringe, normal, normal, halfWidth, 0f);
                // Connected in travel order, which decides the diagonal each quad's coverage interpolates across.
                if (outward < 0f)
                    Connect(edge, rib);
                else
                    Connect(rib, edge);
                return rib;
        }
    }

    // The rib it returns has `from` on its left.
    int Fan(Vector2 centre, Vector2 from, Vector2 apex, float halfWidth, float alpha, bool round)
    {
        var steps = round ? Math.Max(2, ArcSteps(from, apex, halfWidth) * 2) : 2;
        FanCorner(centre, from, apex, 0, steps, halfWidth, out var leftCore, out var leftFringe);
        FanCorner(
            centre,
            from,
            apex,
            steps,
            steps,
            halfWidth,
            out var rightCore,
            out var rightFringe
        );
        var rib = AddRib(leftFringe, leftCore, rightCore, rightFringe, alpha);
        var previousCore = rib + 1;
        var previousFringe = rib;
        for (var s = 1; s <= steps; s++)
        {
            var core = rib + 2;
            var fringe = rib + 3;
            if (s < steps)
            {
                FanCorner(centre, from, apex, s, steps, halfWidth, out var position, out var fade);
                core = _vertexCount;
                fringe = core + 1;
                AddVertex(position, alpha);
                AddVertex(fade, 0f);
            }
            if (s > 1)
                AddTriangle(rib + 1, previousCore, core);
            AddQuad(previousCore, previousFringe, core, fringe);
            previousCore = core;
            previousFringe = fringe;
        }
        return rib;
    }

    static void FanCorner(
        Vector2 centre,
        Vector2 from,
        Vector2 apex,
        int s,
        int steps,
        float halfWidth,
        out Vector2 core,
        out Vector2 fringe
    )
    {
        var outer = FanDirection(from, apex, s, steps);
        var before =
            s == 0 ? from : Vector2.Normalize(FanDirection(from, apex, s - 1, steps) + outer);
        var after =
            s == steps ? -from : Vector2.Normalize(outer + FanDirection(from, apex, s + 1, steps));
        var mitre = Mitre(before, after);
        core = centre + outer * halfWidth - mitre * Fringe;
        fringe = centre + outer * halfWidth + mitre * Fringe;
    }

    static Vector2 FanDirection(Vector2 from, Vector2 apex, int s, int steps)
    {
        if (s == 0)
            return from;
        if (s == steps)
            return -from;
        var t = s / (float)steps;
        return t <= 0.5f ? Rotate(from, apex, t * 2f) : Rotate(apex, -from, t * 2f - 1f);
    }

    static Vector2 Sweep(Vector2 from, Vector2 to, int s, int steps) =>
        s == 0 ? from
        : s == steps ? to
        : Rotate(from, to, s / (float)steps);

    // Not normalized: long enough that each side fades over a full fringe.
    static Vector2 Mitre(Vector2 a, Vector2 b) => (a + b) / (1f + Vector2.Dot(a, b));

    int JoinRib(
        bool outerLeft,
        Vector2 outerCore,
        Vector2 outerFringe,
        Vector2 innerCore,
        Vector2 innerFringe,
        float alpha
    ) =>
        outerLeft
            ? AddRib(outerFringe, outerCore, innerCore, innerFringe, alpha)
            : AddRib(innerFringe, innerCore, outerCore, outerFringe, alpha);

    // Four vertices across the stroke: outer fringe and core edge on the left (+left), then on the right (-right).
    int AddRib(Vector2 centre, Vector2 left, Vector2 right, float halfWidth, float alpha) =>
        AddRib(
            centre + left * (halfWidth + Fringe),
            centre + left * (halfWidth - Fringe),
            centre - right * (halfWidth - Fringe),
            centre - right * (halfWidth + Fringe),
            alpha
        );

    int AddRib(
        Vector2 leftFringe,
        Vector2 leftCore,
        Vector2 rightCore,
        Vector2 rightFringe,
        float alpha
    )
    {
        var first = _vertexCount;
        AddVertex(leftFringe, 0f);
        AddVertex(leftCore, alpha);
        AddVertex(rightCore, alpha);
        AddVertex(rightFringe, 0f);
        return first;
    }

    void Connect(int a, int b)
    {
        if (a < 0 || b < 0)
            return;
        AddQuad(a, a + 1, b, b + 1);
        AddQuad(a + 1, a + 2, b + 1, b + 2);
        AddQuad(a + 2, a + 3, b + 2, b + 3);
    }

    static int ArcSteps(Vector2 from, Vector2 to, float radius)
    {
        var angle = MathF.Acos(Math.Clamp(Vector2.Dot(from, to), -1f, 1f));
        var step =
            2f
            * MathF.Acos(
                MathF.Max(0f, 1f - PathMath.Tolerance / MathF.Max(radius, PathMath.Tolerance))
            );
        return Math.Clamp((int)MathF.Ceiling(angle / MathF.Max(step, 1e-3f)), 1, 32);
    }

    static Vector2 Rotate(Vector2 from, Vector2 to, float t)
    {
        var angle = MathF.Atan2(from.X * to.Y - from.Y * to.X, Vector2.Dot(from, to)) * t;
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        return new Vector2(from.X * cos - from.Y * sin, from.X * sin + from.Y * cos);
    }

    static bool OnOneLine(ReadOnlySpan<Vector2> points, out Vector2 low, out Vector2 high)
    {
        var origin = points[0];
        var farthest = origin;
        foreach (var point in points)
        {
            if (Vector2.DistanceSquared(point, origin) > Vector2.DistanceSquared(farthest, origin))
                farthest = point;
        }

        var direction = Vector2.Normalize(farthest - origin);
        var min = 0f;
        var max = 0f;
        foreach (var point in points)
        {
            var offset = point - origin;
            if (MathF.Abs(PathMath.Cross(direction, offset)) > PathMath.Snap)
            {
                low = high = default;
                return false;
            }
            var along = Vector2.Dot(direction, offset);
            min = MathF.Min(min, along);
            max = MathF.Max(max, along);
        }
        low = origin + direction * min;
        high = origin + direction * max;
        return true;
    }

    void AddVertex(Vector2 position, float coverage)
    {
        if (_vertexCount == MaxVertices)
        {
            Overflowed = true;
            return;
        }
        if (_vertexCount == _positions.Length)
        {
            Array.Resize(ref _positions, _positions.Length * 2);
            Array.Resize(ref _coverage, _positions.Length);
        }
        _positions[_vertexCount] = position;
        _coverage[_vertexCount++] = coverage;
    }

    void AddTriangle(int a, int b, int c)
    {
        if (Overflowed)
            return;
        _indices.Add(a);
        _indices.Add(b);
        _indices.Add(c);
    }

    void AddQuad(int topLeft, int topRight, int bottomLeft, int bottomRight)
    {
        AddTriangle(topLeft, topRight, bottomLeft);
        AddTriangle(bottomLeft, topRight, bottomRight);
    }

    readonly record struct StrokeStyle(
        float HalfWidth,
        float Alpha,
        PenLineJoin Join,
        PenLineCap StartCap,
        PenLineCap EndCap,
        float MiterLimit,
        PenLineCap DashCap
    );

    readonly struct Edge(Vector2 top, Vector2 bottom, sbyte winding, float alpha)
    {
        public readonly Vector2 Top = top;
        public readonly Vector2 Bottom = bottom;
        public readonly sbyte Winding = winding;
        public readonly float Alpha = alpha;
        readonly float _slope = (bottom.X - top.X) / (bottom.Y - top.Y);

        public float XAt(float y) => Top.X + (y - Top.Y) * _slope;
    }
}

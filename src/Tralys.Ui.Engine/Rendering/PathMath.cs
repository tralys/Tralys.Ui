using System.Numerics;

namespace Tralys.Ui.Rendering;

[FramePath]
internal static class PathMath
{
    public const float Tolerance = 0.25f;
    public const float Snap = 1e-3f;
    const int MaxSegments = 128;

    public static void Append(PathBuilder target, PathBuilder source) =>
        Append(target, source, Vector2.One, Vector2.Zero);

    public static void Append(PathBuilder target, PathBuilder source, Vector2 scale, Vector2 offset)
    {
        var points = source.Points;
        var point = 0;
        foreach (var verb in source.Verbs)
        {
            switch (verb)
            {
                case PathVerb.MoveTo:
                    target.MoveTo(points[point++] * scale + offset);
                    break;
                case PathVerb.LineTo:
                    target.LineTo(points[point++] * scale + offset);
                    break;
                case PathVerb.QuadraticTo:
                    target.QuadraticTo(
                        points[point] * scale + offset,
                        points[point + 1] * scale + offset
                    );
                    point += 2;
                    break;
                case PathVerb.CubicTo:
                    target.CubicTo(
                        points[point] * scale + offset,
                        points[point + 1] * scale + offset,
                        points[point + 2] * scale + offset
                    );
                    point += 3;
                    break;
                case PathVerb.Close:
                    target.Close();
                    break;
            }
        }
    }

    public static Vector2 Corner(Rect rect, float radiusX, float radiusY) =>
        new(
            MathF.Min(MathF.Abs(radiusX), MathF.Max(0f, rect.Width) * 0.5f),
            MathF.Min(MathF.Abs(radiusY), MathF.Max(0f, rect.Height) * 0.5f)
        );

    public static Rect Bounds(PathBuilder path)
    {
        var points = path.Points;
        if (points.IsEmpty)
            return default;

        var min = new Vector2(float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity);
        var point = 0;
        var current = Vector2.Zero;
        var start = Vector2.Zero;
        foreach (var verb in path.Verbs)
        {
            switch (verb)
            {
                case PathVerb.MoveTo:
                    current = start = points[point++];
                    Include(current, ref min, ref max);
                    break;
                case PathVerb.LineTo:
                    current = points[point++];
                    Include(current, ref min, ref max);
                    break;
                case PathVerb.QuadraticTo:
                {
                    var control = points[point];
                    var end = points[point + 1];
                    point += 2;
                    Include(end, ref min, ref max);
                    var denominator = current - control * 2f + end;
                    for (var axis = 0; axis < 2; axis++)
                    {
                        if (denominator[axis] == 0f)
                            continue;
                        var t = (current[axis] - control[axis]) / denominator[axis];
                        if (t > 0f && t < 1f)
                            Include(Quadratic(current, control, end, t), ref min, ref max);
                    }
                    current = end;
                    break;
                }
                case PathVerb.CubicTo:
                {
                    var c1 = points[point];
                    var c2 = points[point + 1];
                    var end = points[point + 2];
                    point += 3;
                    Include(end, ref min, ref max);
                    var a = c1 - current;
                    var b = c2 - c1;
                    var c = end - c2;
                    for (var axis = 0; axis < 2; axis++)
                    {
                        CubicExtrema(a[axis], b[axis], c[axis], out var t0, out var t1);
                        if (t0 > 0f && t0 < 1f)
                            Include(Cubic(current, c1, c2, end, t0), ref min, ref max);
                        if (t1 > 0f && t1 < 1f)
                            Include(Cubic(current, c1, c2, end, t1), ref min, ref max);
                    }
                    current = end;
                    break;
                }
                case PathVerb.Close:
                    current = start;
                    break;
            }
        }
        return Rect.FromEdges(min.X, min.Y, max.X, max.Y);
    }

    public static bool FillContains(PathBuilder path, Vector2 at) =>
        FillContains(path.Verbs, path.Points, path.FillRule, at);

    public static bool FillContains(
        ReadOnlySpan<PathVerb> verbs,
        ReadOnlySpan<Vector2> points,
        FillRule fillRule,
        Vector2 at
    )
    {
        var winding = new Winding(at);
        Flatten(verbs, points, Matrix3x2.Identity, ref winding);
        return Covers(fillRule, winding.Count);
    }

    [FramePath]
    public static bool Covers(FillRule rule, int winding) =>
        rule == FillRule.EvenOdd ? (winding & 1) != 0 : winding != 0;

    [FramePath]
    public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    [FramePath]
    public static Vector2 Normal(Vector2 direction)
    {
        var length = direction.Length();
        return length > 0f ? new Vector2(-direction.Y, direction.X) / length : Vector2.Zero;
    }

    [FramePath]
    public static void PointsBeside(
        Vector2 from,
        Vector2 to,
        float length,
        out Vector2 left,
        out Vector2 right
    )
    {
        var normal = Normal(to - from);
        var middle = (from + to) * 0.5f;
        var probe = MathF.Min(0.05f, length * 0.25f);
        left = middle + normal * probe;
        right = middle - normal * probe;
    }

    // Joins count as round; the dashes come in an even count.
    public static bool StrokeContains(
        ReadOnlySpan<PathVerb> verbs,
        ReadOnlySpan<Vector2> points,
        Vector2 at,
        float halfWidth,
        PenLineCap startCap,
        PenLineCap endCap,
        ReadOnlySpan<float> dashes = default,
        float dashScale = 1f,
        float dashOffset = 0f,
        PenLineCap dashCap = PenLineCap.Flat
    )
    {
        if (!(halfWidth > 0f))
            return false;

        var stroke = new StrokeHit(
            at,
            halfWidth,
            startCap,
            endCap,
            dashes,
            dashScale,
            dashOffset,
            dashCap
        );
        Flatten(verbs, points, Matrix3x2.Identity, ref stroke);
        return stroke.Hit;
    }

    public interface IEdgeSink
    {
        void Begin();

        void Edge(Vector2 from, Vector2 to, bool closesForFill);

        void End(bool closed);
    }

    /// <summary>What the edge from <paramref name="from"/> to <paramref name="to"/> adds to the winding around
    /// <paramref name="at"/>: 1 when it runs down past it on its right, -1 up, as y grows down; an edge counts from its
    /// top end to just short of its bottom.</summary>
    public static int Crossing(Vector2 from, Vector2 to, Vector2 at)
    {
        var side =
            (double)(to.X - from.X) * (at.Y - from.Y) - (double)(at.X - from.X) * (to.Y - from.Y);
        if (from.Y <= at.Y)
            return to.Y > at.Y && side > 0 ? 1 : 0;
        return to.Y <= at.Y && side < 0 ? -1 : 0;
    }

    struct Winding(Vector2 at) : IEdgeSink
    {
        public int Count;

        public readonly void Begin() { }

        public void Edge(Vector2 from, Vector2 to, bool closesForFill) =>
            Count += Crossing(from, to, at);

        public readonly void End(bool closed) { }
    }

    // Only a figure's end decides its first dash's start cap and its last dash's end, so those two wait for it.
    ref struct StrokeHit : IEdgeSink
    {
        readonly Vector2 _at;
        readonly float _halfWidth;
        readonly PenLineCap _startCap;
        readonly PenLineCap _endCap;
        readonly PenLineCap _dashCap;
        readonly DashCursor _figureStart;
        DashCursor _cursor;
        bool _startsOn;
        Join _next;
        bool _firstPending;
        Vector2 _firstReach;
        bool _endPending;
        Vector2 _endReach;
        int _steps;

        public bool Hit;

        public StrokeHit(
            Vector2 at,
            float halfWidth,
            PenLineCap startCap,
            PenLineCap endCap,
            ReadOnlySpan<float> dashes,
            float scale,
            float offset,
            PenLineCap dashCap
        )
        {
            _at = at;
            _halfWidth = halfWidth;
            _startCap = startCap;
            _endCap = endCap;
            _dashCap = dashCap;
            _figureStart = new DashCursor(dashes, scale, offset);
            Begin();
        }

        public void Begin()
        {
            _firstPending = false;
            _endPending = false;
            _next = Join.FigureStart;
            _cursor = _figureStart;
            _startsOn = _cursor.On;
        }

        public void Edge(Vector2 from, Vector2 to, bool closesForFill)
        {
            if (Hit || closesForFill)
                return;

            var length = Vector2.Distance(from, to);
            if (!(length > 0f && float.IsFinite(length)))
                return;

            var along = (to - from) / length;
            if (_endPending)
            {
                _endPending = false;
                if (Capped(PenLineCap.Round, _endReach))
                {
                    Hit = true;
                    return;
                }
            }

            var t = 0f;
            while (_cursor.Remaining <= length - t)
            {
                if (++_steps > DashCursor.MaxSteps)
                    return;

                var end = t + _cursor.Remaining;
                if (_cursor.On && Piece(from, along, t, end, _next, Join.DashEnd))
                {
                    Hit = true;
                    return;
                }

                _next = Join.DashEnd;
                _cursor.Step();
                t = end;
            }

            _cursor.Advance(length - t);
            if (_cursor.On && Piece(from, along, t, length, _next, Join.Pending))
                Hit = true;
            if (_cursor.On)
                _next = Join.Round;
        }

        public void End(bool closed)
        {
            if (Hit)
                return;

            var joined = closed && _startsOn && _cursor.On;
            if (_endPending)
            {
                _endPending = false;
                var cap =
                    joined ? PenLineCap.Round
                    : closed ? _dashCap
                    : _endCap;
                Hit = Capped(cap, _endReach);
            }
            if (!Hit && _firstPending)
            {
                _firstPending = false;
                var cap =
                    joined ? PenLineCap.Round
                    : closed ? _dashCap
                    : _startCap;
                Hit = Capped(cap, _firstReach);
            }
        }

        bool Piece(Vector2 from, Vector2 along, float start, float end, Join before, Join after)
        {
            var offset = _at - (from + along * start);
            var length = end - start;
            var t = Vector2.Dot(offset, along);
            var across = MathF.Abs(along.X * offset.Y - along.Y * offset.X);
            if (across > _halfWidth)
                return false;
            if (t >= 0f && t <= length)
                return true;

            if (t < 0f)
            {
                var reach = new Vector2(-t, across);
                if (before != Join.FigureStart)
                    return Capped(before == Join.Round ? PenLineCap.Round : _dashCap, reach);
                _firstPending = true;
                _firstReach = reach;
                return false;
            }

            var past = new Vector2(t - length, across);
            if (after != Join.Pending)
                return Capped(_dashCap, past);
            _endPending = true;
            _endReach = past;
            return false;
        }

        // Reach is how far past the dash's end the point lies, then how far across it.
        readonly bool Capped(PenLineCap cap, Vector2 reach) =>
            cap switch
            {
                PenLineCap.Square => reach.X <= _halfWidth,
                PenLineCap.Round => reach.LengthSquared() <= _halfWidth * _halfWidth,
                PenLineCap.Triangle => reach.X + reach.Y <= _halfWidth,
                _ => false,
            };

        enum Join : byte
        {
            FigureStart,
            DashEnd,
            Round,
            Pending,
        }
    }

    public static void Flatten<TSink>(
        ReadOnlySpan<PathVerb> verbs,
        ReadOnlySpan<Vector2> points,
        in Matrix3x2 transform,
        ref TSink sink
    )
        where TSink : IEdgeSink, allows ref struct
    {
        var point = 0;
        var current = Vector2.Zero;
        var start = Vector2.Zero;
        var open = false;
        foreach (var verb in verbs)
        {
            switch (verb)
            {
                case PathVerb.MoveTo:
                    if (open)
                    {
                        sink.Edge(current, start, closesForFill: true);
                        sink.End(closed: false);
                    }
                    current = start = Vector2.Transform(points[point++], transform);
                    open = true;
                    sink.Begin();
                    break;
                case PathVerb.LineTo:
                {
                    var end = Vector2.Transform(points[point++], transform);
                    sink.Edge(current, end, closesForFill: false);
                    current = end;
                    break;
                }
                case PathVerb.QuadraticTo:
                {
                    var control = Vector2.Transform(points[point], transform);
                    var end = Vector2.Transform(points[point + 1], transform);
                    point += 2;
                    var bend = (current - control * 2f + end).Length();
                    var segments = Segments(MathF.Sqrt(bend / (4f * Tolerance)));
                    var previous = current;
                    for (var i = 1; i <= segments; i++)
                    {
                        var next = Quadratic(current, control, end, i / (float)segments);
                        sink.Edge(previous, next, closesForFill: false);
                        previous = next;
                    }
                    current = end;
                    break;
                }
                case PathVerb.CubicTo:
                {
                    var c1 = Vector2.Transform(points[point], transform);
                    var c2 = Vector2.Transform(points[point + 1], transform);
                    var end = Vector2.Transform(points[point + 2], transform);
                    point += 3;
                    var bend = MathF.Max(
                        (current - c1 * 2f + c2).Length(),
                        (c1 - c2 * 2f + end).Length()
                    );
                    var segments = Segments(MathF.Sqrt(0.75f * bend / Tolerance));
                    var previous = current;
                    for (var i = 1; i <= segments; i++)
                    {
                        var next = Cubic(current, c1, c2, end, i / (float)segments);
                        sink.Edge(previous, next, closesForFill: false);
                        previous = next;
                    }
                    current = end;
                    break;
                }
                case PathVerb.Close:
                    sink.Edge(current, start, closesForFill: false);
                    sink.End(closed: true);
                    current = start;
                    open = false;
                    break;
            }
        }
        if (open)
        {
            sink.Edge(current, start, closesForFill: true);
            sink.End(closed: false);
        }
    }

    static int Segments(float estimate) => Math.Clamp((int)MathF.Ceiling(estimate), 1, MaxSegments);

    static Vector2 Quadratic(Vector2 from, Vector2 control, Vector2 to, float t)
    {
        var u = 1f - t;
        return from * (u * u) + control * (2f * u * t) + to * (t * t);
    }

    static Vector2 Cubic(Vector2 from, Vector2 c1, Vector2 c2, Vector2 to, float t)
    {
        var u = 1f - t;
        return from * (u * u * u)
            + c1 * (3f * u * u * t)
            + c2 * (3f * u * t * t)
            + to * (t * t * t);
    }

    static void CubicExtrema(float a, float b, float c, out float t0, out float t1)
    {
        var qa = a - 2f * b + c;
        var qb = 2f * (b - a);
        t0 = t1 = float.NaN;
        if (MathF.Abs(qa) < 1e-12f)
        {
            if (qb != 0f)
                t0 = -a / qb;
            return;
        }

        var discriminant = qb * qb - 4f * qa * a;
        if (discriminant < 0f)
            return;

        var root = MathF.Sqrt(discriminant);
        t0 = (-qb + root) / (2f * qa);
        t1 = (-qb - root) / (2f * qa);
    }

    static void Include(Vector2 point, ref Vector2 min, ref Vector2 max)
    {
        min = Vector2.Min(min, point);
        max = Vector2.Max(max, point);
    }
}

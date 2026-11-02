namespace Tralys.Ui;

/// <summary>A cubic Bézier from (0, 0) to (1, 1) through two control points, mapping linear progress (the curve's x)
/// to the progress it shows (its y). Immutable, so one can be shared.</summary>
public sealed class KeySpline : IEasingFunction
{
    const double Accuracy = .001;
    const double Fuzz = .000001;

    readonly bool _isLinear;
    readonly double _bx;
    readonly double _cx;
    readonly double _cxBx;
    readonly double _threeCx;
    readonly double _by;
    readonly double _cy;

    public KeySpline()
        : this(0f, 0f, 1f, 1f) { }

    public KeySpline(float x1, float y1, float x2, float y2)
    {
        if (!InUnitSquare(x1, y1) || !InUnitSquare(x2, y2))
            throw new ArgumentOutOfRangeException(
                nameof(x1),
                "A key spline's control points lie between 0 and 1 on both axes."
            );

        _isLinear = x1 == 0f && y1 == 0f && x2 == 1f && y2 == 1f;
        _bx = 3.0 * x1;
        _cx = 3.0 * x2;
        _cxBx = 2.0 * (_cx - _bx);
        _threeCx = 3.0 - _cx;
        _by = 3.0 * y1;
        _cy = 3.0 * y2;
    }

    /// <summary>The progress the spline shows at <paramref name="normalizedTime"/>, from 0 to 1.</summary>
    [FramePath]
    public double Ease(double normalizedTime)
    {
        if (_isLinear)
            return normalizedTime;

        var parameter = ParameterAt(normalizedTime);
        return BezierValue(_by, _cy, parameter);
    }

    [FramePath]
    static double BezierValue(double b, double c, double t)
    {
        var s = 1.0 - t;
        var t2 = t * t;
        return b * t * s * s + c * t2 * s + t2 * t;
    }

    [FramePath]
    double ParameterAt(double time)
    {
        if (time <= 0)
            return 0;
        if (time >= 1)
            return 1;

        var parameter = time;
        var bottom = 0.0;
        var top = 1.0;
        while (top - bottom > Fuzz)
        {
            var s = 1.0 - parameter;
            var t2 = parameter * parameter;
            var s2 = s * s;
            var x = _bx * parameter * s2 + _cx * t2 * s + t2 * parameter;
            var dx = _bx * s2 + _cxBx * s * parameter + _threeCx * t2;
            var absDx = Math.Abs(dx);

            if (x > time)
                top = parameter;
            else
                bottom = parameter;

            // The accuracy wanted is in y; dy/dt is at most 3, so dx alone bounds the error.
            if (Math.Abs(x - time) < Accuracy * absDx)
                break;

            if (absDx > Fuzz)
            {
                var next = parameter - (x - time) / dx;
                if (next >= top)
                    parameter = (parameter + top) / 2;
                else if (next <= bottom)
                    parameter = (parameter + bottom) / 2;
                else
                    parameter = next;
            }
            else
                parameter = (bottom + top) / 2;
        }
        return parameter;
    }

    static bool InUnitSquare(float x, float y) => x is >= 0f and <= 1f && y is >= 0f and <= 1f;
}

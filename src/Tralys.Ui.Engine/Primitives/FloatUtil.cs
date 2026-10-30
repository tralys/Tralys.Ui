namespace Tralys.Ui;

static class FloatUtil
{
    const float Epsilon = 1.1920929e-7f;

    public static bool AreClose(float value1, float value2)
    {
        if (value1 == value2)
            return true;

        var eps = (MathF.Abs(value1) + MathF.Abs(value2) + 10f) * Epsilon;
        var delta = value1 - value2;
        return -eps < delta && eps > delta;
    }

    public static bool AreClose(Size size1, Size size2) =>
        AreClose(size1.Width, size2.Width) && AreClose(size1.Height, size2.Height);

    public static bool AreClose(Rect rect1, Rect rect2) =>
        AreClose(rect1.X, rect2.X)
        && AreClose(rect1.Y, rect2.Y)
        && AreClose(rect1.Width, rect2.Width)
        && AreClose(rect1.Height, rect2.Height);

    public static bool LessThan(float value1, float value2) =>
        value1 < value2 && !AreClose(value1, value2);

    public static bool GreaterThan(float value1, float value2) =>
        value1 > value2 && !AreClose(value1, value2);

    public static bool LessThanOrClose(float value1, float value2) =>
        value1 < value2 || AreClose(value1, value2);

    public static bool GreaterThanOrClose(float value1, float value2) =>
        value1 > value2 || AreClose(value1, value2);

    public static bool IsZero(float value) => MathF.Abs(value) < 10f * Epsilon;

    public static bool IsFiniteNonNegative(float value) => value >= 0f && float.IsFinite(value);
}

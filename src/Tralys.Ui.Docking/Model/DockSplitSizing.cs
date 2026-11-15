namespace Tralys.Ui.Docking;

static class DockSplitSizing
{
    public static (float First, float Second) NormalizeProportions(float first, float second)
    {
        if (!IsPositiveFinite(first) || !IsPositiveFinite(second))
            return (0.5f, 0.5f);

        var total = first + second;
        if (!IsPositiveFinite(total))
            return (0.5f, 0.5f);

        return (first / total, second / total);
    }

    private static bool IsPositiveFinite(float value) => value > 0 && float.IsFinite(value);
}

namespace Tralys.Ui.Components;

[FramePath]
internal static class AnimatedParts
{
    /// <summary>How much of <paramref name="min"/> to <paramref name="max"/> <paramref name="value"/> has covered,
    /// from 0 to 1; 0 for a range that is empty or turned round.</summary>
    public static float Share(float value, float min, float max)
    {
        var span = max - min;
        if (!(span > 0f))
            return 0f;
        var share = (value - min) / span;
        return share > 0f ? MathF.Min(share, 1f) : 0f;
    }

    /// <summary>The band <paramref name="art"/>'s slice takes at its edges, in layout units.</summary>
    public static Thickness Inset(SkinPartImage art) =>
        new(
            art.Slice.Left * art.Scale,
            art.Slice.Top * art.Scale,
            art.Slice.Right * art.Scale,
            art.Slice.Bottom * art.Scale
        );
}

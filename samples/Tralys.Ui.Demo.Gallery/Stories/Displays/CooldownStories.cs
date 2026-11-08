using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Cooldown>("Displays")]
static class CooldownStories
{
    static readonly float[] Remaining = [3.4f, 0.6f, 0f];

    [FramePath]
    public static void Cast(Ui ui)
    {
        var t = ui.Theme;
        using (ui.Box(gap: 48, fontWeight: t.Weight(WeightToken.Interface)))
            for (var i = 0; i < Remaining.Length; i++)
                using (ui.Box(direction: Direction.Column, gap: 4, key: i))
                {
                    using (ui.Cooldown(duration: 8, remaining: Remaining[i], width: 48, height: 48))
                    using (
                        ui.Box(
                            grow: 1,
                            background: t.Color(ColorToken.BgAccent),
                            radius: t.Radius(RadiusToken.Md)
                        )
                    ) { }
                    ui.Text(
                        "Cast",
                        color: t.Color(ColorToken.FgMuted),
                        variant: TextVariant.Caption,
                        alignSelf: AlignSelf.Center
                    );
                }
    }
}

[Stories<Cooldown.Shade>("Displays")]
static class CooldownShadeStories
{
    static readonly float[] Progress = [1f, 0.75f, 0.4f, 0.1f];

    [FramePath]
    public static void Shares(Ui ui)
    {
        var accent = ui.Theme.Color(ColorToken.BgAccent);
        using (ui.Box(gap: 32))
            for (var i = 0; i < Progress.Length; i++)
                ui.CooldownShade(
                    progress: Progress[i],
                    color: accent,
                    width: 42,
                    height: 42,
                    key: i
                );
    }
}

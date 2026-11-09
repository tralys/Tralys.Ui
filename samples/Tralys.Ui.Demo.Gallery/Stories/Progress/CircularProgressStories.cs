using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<CircularProgress>("Progress")]
static class CircularProgressStories
{
    [FramePath]
    public static void Resources(Ui ui)
    {
        var t = ui.Theme;
        var strong = t.Color(ColorToken.FgStrong);
        var muted = t.Color(ColorToken.FgMuted);
        var weight = t.Weight(WeightToken.Interface);
        using (ui.Box(gap: 64))
        {
            using (
                ui.CircularProgress(
                    value: 73,
                    thickness: 6,
                    color: t.Color(ColorToken.BgDanger),
                    width: 72,
                    height: 72
                )
            )
            using (ui.Box(direction: Direction.Column, align: Align.Center))
            {
                ui.Text(
                    "73%",
                    color: strong,
                    fontWeight: weight,
                    variant: TextVariant.Heading,
                    wrap: true
                );
                ui.Text(
                    "CPU",
                    color: muted,
                    fontWeight: weight,
                    variant: TextVariant.Caption,
                    wrap: true
                );
            }
            using (ui.CircularProgress(value: 47, thickness: 6, width: 72, height: 72))
            using (ui.Box(direction: Direction.Column, align: Align.Center))
            {
                ui.Text(
                    "47%",
                    color: strong,
                    fontWeight: weight,
                    variant: TextVariant.Heading,
                    wrap: true
                );
                ui.Text(
                    "RAM",
                    color: muted,
                    fontWeight: weight,
                    variant: TextVariant.Caption,
                    wrap: true
                );
            }
        }
    }

    [FramePath]
    public static void Orbs(Ui ui)
    {
        var t = ui.Theme;
        using (ui.Box(gap: 32))
        {
            using (
                ui.CircularProgress(
                    value: 73,
                    variant: CircularProgressVariant.Orb,
                    color: t.Color(ColorToken.BgDanger),
                    width: 64,
                    height: 64
                )
            ) { }
            using (
                ui.CircularProgress(
                    value: 47,
                    variant: CircularProgressVariant.Orb,
                    color: t.Color(ColorToken.BgAccent),
                    width: 64,
                    height: 64
                )
            ) { }
            using (
                ui.CircularProgress(
                    value: 0,
                    variant: CircularProgressVariant.Orb,
                    color: t.Color(ColorToken.BgMuted),
                    width: 64,
                    height: 64
                )
            ) { }
        }
    }
}

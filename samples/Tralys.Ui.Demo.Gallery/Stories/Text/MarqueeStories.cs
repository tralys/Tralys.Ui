using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Marquee>("Text")]
static class MarqueeStories
{
    [FramePath]
    public static void ItemName(Ui ui)
    {
        var weight = ui.Theme.Weight(WeightToken.Interface);
        using (ui.Box(direction: Direction.Column, gap: 16, width: 220))
        {
            using (ui.Box())
            {
                ui.Icon(
                    glyph: Icons.CheckCircle,
                    alignSelf: AlignSelf.Center,
                    margin: new Thickness(0, 0, 6, 0)
                );
                using (ui.Marquee(grow: 1))
                    ui.Text(
                        "Ancestral Dragonbone Greatsword of the Ember Court +12",
                        fontWeight: weight
                    );
            }
            using (ui.Box())
            {
                ui.Icon(
                    glyph: Icons.CheckCircle,
                    alignSelf: AlignSelf.Center,
                    margin: new Thickness(0, 0, 6, 0)
                );
                using (ui.Marquee(grow: 1))
                    ui.Text("Iron Dagger", fontWeight: weight);
            }
        }
    }
}

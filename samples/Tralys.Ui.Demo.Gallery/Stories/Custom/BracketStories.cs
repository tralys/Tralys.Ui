using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Bracket>("Custom")]
static class BracketStories
{
    static readonly string[] Entrants = ["Aria", "Bram", "Cyra", "Dax", "Aria", "Dax", "Dax"];

    static readonly Slot<int> Entry = Slot<int>.Of(
        static (ui, i, _) =>
        {
            var t = ui.Theme;
            using (
                ui.Box(
                    align: Align.Center,
                    grow: 1,
                    padding: new Thickness(8, 0, 8, 0),
                    radius: t.Radius(RadiusToken.Md),
                    background: t.Color(ColorToken.BgRaised),
                    border: new(1, t.Color(ColorToken.Border))
                )
            )
                ui.Text(Entrants[i]);
        }
    );

    [FramePath]
    public static void ThreeRounds(Ui ui)
    {
        using (ui.Box(fontWeight: ui.Theme.Weight(WeightToken.Interface)))
            ui.Bracket(rounds: 3, card: new Size(96, 28), entry: Entry);
    }
}

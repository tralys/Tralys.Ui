using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Pill>("Custom")]
static class PillStories
{
    [FramePath]
    public static void States(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "A component is its render: its look from its props, its parts restyled by its states; the every-state toggle above holds each state"
            );
            using (ui.Box(gap: 8))
            {
                ui.Pill();
                ui.Pill(accent: true);
                ui.Pill(disabled: true);
            }
        }
    }
}

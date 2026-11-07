using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<HealthBar>("Custom")]
static class HealthBarStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                width: 240,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "A health meter drawn on a canvas: full, part full, low and pulsing, and upright"
            );
            ui.HealthBar(value: 100, height: 8);
            ui.HealthBar(value: 62, height: 8);
            ui.HealthBar(value: 18, height: 8);
            ui.HealthBar(
                value: 45,
                orientation: Orientation.Vertical,
                width: 8,
                height: 60,
                alignSelf: AlignSelf.Start
            );
        }
    }
}

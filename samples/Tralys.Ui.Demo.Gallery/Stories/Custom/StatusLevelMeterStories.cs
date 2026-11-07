using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<StatusLevelMeter>("Custom")]
static class StatusLevelMeterStories
{
    [FramePath]
    public static void Levels(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                width: 200,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "A level meter drawn on a canvas: two levels and a third part filled, then one banked behind a gate"
            );
            ui.StatusLevelMeter(level: 2, maxLevel: 5, percent: 40, height: 6);
            ui.StatusLevelMeter(level: 3, maxLevel: 5, percent: 100, height: 6);
        }
    }
}

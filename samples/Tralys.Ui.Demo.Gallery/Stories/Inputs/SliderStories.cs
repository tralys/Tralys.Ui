using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Slider>("Inputs")]
static class SliderStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var half = ui.State(50f);
        var quarter = ui.State(25f);
        var off = ui.State(75f);
        using (ui.Box(direction: Direction.Column, gap: 16, width: 320))
        {
            ui.Slider(value: half.Value, onValueChange: half.Setter, max: 100);
            ui.Slider(value: quarter.Value, onValueChange: quarter.Setter, max: 100);
            ui.Slider(value: off.Value, onValueChange: off.Setter, max: 100, disabled: true);
        }
    }

    [FramePath]
    public static void Range(Ui ui)
    {
        var value = ui.State(50f);
        ui.Slider(
            value: value.Value,
            onValueChange: value.Setter,
            max: 100,
            tickFrequency: 10,
            ticks: TickPlacement.BottomRight,
            snapToTicks: true,
            width: 320
        );
    }

    [FramePath]
    public static void Vertical(Ui ui)
    {
        var half = ui.State(50f);
        var ticked = ui.State(30f);
        var off = ui.State(75f);
        using (ui.Box(gap: 64, height: 200))
        {
            ui.Slider(
                value: half.Value,
                onValueChange: half.Setter,
                max: 100,
                orientation: Orientation.Vertical
            );
            ui.Slider(
                value: ticked.Value,
                onValueChange: ticked.Setter,
                max: 100,
                tickFrequency: 10,
                ticks: TickPlacement.Both,
                snapToTicks: true,
                orientation: Orientation.Vertical
            );
            ui.Slider(
                value: off.Value,
                onValueChange: off.Setter,
                max: 100,
                orientation: Orientation.Vertical,
                disabled: true
            );
        }
    }
}

using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<SliderField>("Inputs")]
static class SliderFieldStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        const float label = 83f;
        var volume = ui.State(50.0);
        var brightness = ui.State(80.0);
        using (ui.Box(direction: Direction.Column, gap: 32, width: 360))
        {
            ui.SliderField(
                label: "Volume",
                value: volume.Value,
                onValueChange: volume.Setter,
                labelWidth: label
            );
            ui.SliderField(
                label: "Brightness",
                value: brightness.Value,
                onValueChange: brightness.Setter,
                labelWidth: label
            );
        }
    }

    [FramePath]
    public static void Decimal(Ui ui)
    {
        const float label = 138f;
        var depth = ui.State(0.55);
        var cover = ui.State(0.2);
        using (ui.Box(direction: Direction.Column, gap: 32, width: 360))
        {
            ui.SliderField(
                label: "depthMapStrength",
                value: depth.Value,
                onValueChange: depth.Setter,
                max: 1,
                step: 0.01,
                mode: NumberMode.Decimal,
                labelWidth: label
            );
            ui.SliderField(
                label: "coverHeight",
                value: cover.Value,
                onValueChange: cover.Setter,
                max: 1,
                step: 0.01,
                mode: NumberMode.Decimal,
                labelWidth: label
            );
        }
    }

    [FramePath]
    public static void Format(Ui ui)
    {
        var latency = ui.State(125.5);
        using (ui.Box(direction: Direction.Column, width: 360))
            ui.SliderField(
                label: "Latency",
                value: latency.Value,
                onValueChange: latency.Setter,
                max: 500,
                mode: NumberMode.Decimal,
                format: "0.0 'ms'"
            );
    }

    [FramePath]
    public static void Disabled(Ui ui)
    {
        var volume = ui.State(50.0);
        using (ui.Box(direction: Direction.Column, width: 360))
            ui.SliderField(
                label: "Volume",
                value: volume.Value,
                onValueChange: volume.Setter,
                disabled: true
            );
    }
}

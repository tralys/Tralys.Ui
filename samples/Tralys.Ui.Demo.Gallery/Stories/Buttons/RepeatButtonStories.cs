using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<RepeatButton>("Buttons")]
static class RepeatButtonStories
{
    [FramePath]
    public static void Stepper(Ui ui)
    {
        var count = ui.State(0);
        var t = ui.Theme;
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8, padding: 16))
        {
            ui.Text(
                "Press and hold: one click as the press lands, then one every Interval after Delay",
                color: t.Color(ColorToken.FgMuted),
                fontWeight: t.Weight(WeightToken.Interface),
                wrap: true
            );
            using (ui.Box(gap: 8))
            {
                ui.RepeatButton(
                    iconLeft: Icons.Minus,
                    scheme: ColorScheme.Secondary,
                    width: 40,
                    onClick: Callback.Of(count, static c => c.Value--),
                    id: "minus"
                );
                using (
                    ui.Box(
                        width: 64,
                        align: Align.Center,
                        justify: Justify.Center,
                        background: t.Color(ColorToken.BgSunken),
                        border: new Border(1f, t.Color(ColorToken.Border))
                    )
                )
                    ui.Text(
                        ui.Format($"{count.Value}"),
                        fontSize: 16,
                        fontWeight: t.Weight(WeightToken.Interface),
                        id: "count"
                    );
                ui.RepeatButton(
                    iconLeft: Icons.Plus,
                    scheme: ColorScheme.Secondary,
                    width: 40,
                    onClick: Callback.Of(count, static c => c.Value++),
                    id: "plus"
                );
                ui.RepeatButton(
                    text: "+1 every 100 ms",
                    scheme: ColorScheme.Secondary,
                    delay: 0.25f,
                    interval: 0.1f,
                    padding: new Thickness(8f, 0f, 8f, 0f),
                    onClick: Callback.Of(count, static c => c.Value++),
                    id: "every"
                );
            }
        }
    }
}

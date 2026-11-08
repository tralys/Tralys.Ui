using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<ActionBar>("Displays")]
static class ActionBarStories
{
    [FramePath]
    public static void Files(Ui ui)
    {
        var t = ui.Theme;
        using (ui.ActionScope())
        {
            ui.Action(UiAction.Confirm, onRun: default, label: "Open");
            ui.Action(UiAction.Back, onRun: default, label: "Close");
            ui.Action(UiAction.NextTab, onRun: default, label: "Next folder");
            using (
                ui.Box(
                    direction: Direction.Column,
                    gap: 24,
                    width: 420,
                    fontWeight: t.Weight(WeightToken.Interface)
                )
            )
            {
                using (
                    ui.Box(
                        align: Align.Center,
                        justify: Justify.Center,
                        height: 120,
                        background: t.Color(ColorToken.BgSunken),
                        border: new Border(1f, t.Color(ColorToken.Border))
                    )
                )
                    ui.Text("Files", color: t.Color(ColorToken.FgMuted));
                ui.ActionBar();
            }
        }
    }
}

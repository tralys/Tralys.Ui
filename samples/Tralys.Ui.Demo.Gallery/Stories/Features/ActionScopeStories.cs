using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.ActionScope>("Features")]
static class ActionScopeStories
{
    [FramePath]
    public static void Layers(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Grid(
                rows: new(Track.Fr(1)),
                width: 420,
                height: 180,
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            using (ui.ActionScope())
            {
                ui.Action(UiAction.Back, onRun: default, label: "Leave town");
                using (
                    ui.Box(
                        column: 1,
                        row: 1,
                        minWidth: 0,
                        minHeight: 0,
                        background: t.Color(ColorToken.BgSunken)
                    )
                )
                    ui.Text("Town map", color: t.Color(ColorToken.FgMuted), margin: 8);
            }
            using (ui.ActionScope())
            {
                ui.Action(UiAction.Confirm, onRun: default, label: "Buy");
                ui.Action(UiAction.Back, onRun: default, label: "Close shop");
                using (
                    ui.Box(
                        direction: Direction.Column,
                        gap: 24,
                        column: 1,
                        row: 1,
                        minWidth: 0,
                        minHeight: 0,
                        margin: new Thickness(40, 30, 40, 30),
                        background: t.Color(ColorToken.BgRaised)
                    )
                )
                {
                    ui.Text(
                        "Shop, opened over the map, so only its actions show",
                        color: t.Color(ColorToken.FgMuted),
                        wrap: true,
                        margin: 8
                    );
                    ui.ActionBar(margin: 8);
                }
            }
        }
    }
}

using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<InsertLine>("Custom")]
static class InsertLineStories
{
    [FramePath]
    public static void BelowARow(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 4,
                width: 200,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            using (ui.InsertLine())
                ui.Text("Engineer");
            using (ui.InsertLine(below: true))
                ui.Text("Designer");
            using (ui.InsertLine())
                ui.Text("Writer");
            using (ui.InsertLine())
                ui.Text("Tester");
        }
    }

    [FramePath]
    public static void AboveARow(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption("The line over a row draws inside the box around the rows");
            using (
                ui.Box(
                    direction: Direction.Column,
                    gap: 4,
                    width: 240,
                    padding: 8,
                    background: t.Color(ColorToken.BgSunken)
                )
            )
            {
                using (ui.InsertLine())
                    ui.Text("Engineer");
                using (ui.InsertLine(below: false))
                    ui.Text("Designer");
                using (ui.InsertLine())
                    ui.Text("Writer");
            }
        }
    }

    [FramePath]
    public static void InAScrollArea(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption("In a scroll area the line scrolls with its row and clips to the viewport");
            using (
                ui.ScrollArea(
                    background: t.Color(ColorToken.BgSunken),
                    width: 240,
                    height: 90,
                    alignSelf: AlignSelf.Start
                )
            )
            using (ui.Box(direction: Direction.Column, gap: 4))
            {
                using (ui.InsertLine())
                    ui.Text("Engineer");
                using (ui.InsertLine(below: true))
                    ui.Text("Designer");
                using (ui.InsertLine())
                    ui.Text("Writer");
                using (ui.InsertLine())
                    ui.Text("Tester");
                using (ui.InsertLine())
                    ui.Text("Analyst");
            }
        }
    }
}

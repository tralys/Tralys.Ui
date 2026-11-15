using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Floating>("Layout")]
static class FloatingStories
{
    [FramePath]
    public static void Window(Ui ui)
    {
        var t = ui.Theme;
        var bounds = ui.State(new Rect(40f, 30f, 200f, 140f));
        var radius = t.Radius(RadiusToken.Md);
        using (
            ui.Box(
                width: 360,
                height: 240,
                background: t.Color(ColorToken.BgCanvas),
                border: new Border(1f, t.Color(ColorToken.Border)),
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        using (
            ui.Floating(
                bounds: bounds.Value,
                onBoundsChange: bounds.Setter,
                resizeEdges: ResizeEdges.All
            )
        )
        using (
            ui.Box(
                direction: Direction.Column,
                grow: 1,
                background: t.Color(ColorToken.Bg),
                border: new Border(1f, t.Color(ColorToken.Border)),
                radius: radius
            )
        )
        {
            using (ui.FloatingHandle())
            using (ui.Box(align: Align.Center, height: 24, padding: new Thickness(8f, 0f, 8f, 0f)))
                ui.Text("Drag me");
            using (ui.Box(grow: 1, align: Align.Center, justify: Justify.Center, padding: 8))
                ui.Text("Its edges resize it");
        }
    }
}

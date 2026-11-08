using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Separator>("Displays")]
static class SeparatorStories
{
    [FramePath]
    public static void Horizontal(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                width: 300,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Text("Above separator");
            ui.Separator();
            ui.Text("Below separator");
            ui.Separator();
            ui.Text("Another section");
        }
    }

    [FramePath]
    public static void Vertical(Ui ui)
    {
        using (ui.Box(fontWeight: ui.Theme.Weight(WeightToken.Interface)))
        {
            ui.Text("Left");
            ui.Separator(orientation: Orientation.Vertical);
            ui.Text("Center");
            ui.Separator(orientation: Orientation.Vertical);
            ui.Text("Right");
        }
    }
}

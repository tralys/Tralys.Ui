using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<CloseButton>("Buttons")]
static class CloseButtonStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var t = ui.Theme;
        using (ui.Box(padding: 24))
        using (
            ui.Box(
                width: 120,
                height: 100,
                padding: 12,
                border: new Border(1f, t.Color(ColorToken.Border)),
                radius: t.Radius(RadiusToken.Md)
            )
        )
            ui.CloseButton(position: Position.Absolute, top: 2, right: 2);
    }
}

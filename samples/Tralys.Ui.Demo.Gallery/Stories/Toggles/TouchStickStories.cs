using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<TouchStick>("Toggles")]
static class TouchStickStories
{
    [FramePath]
    public static void WithButton(Ui ui)
    {
        using (ui.Box(gap: 32))
        {
            ui.TouchStick();
            using (ui.TouchButton(width: 76, height: 76, alignSelf: AlignSelf.End))
                TouchButtonStories.Round(
                    ui,
                    Icons.Crosshair,
                    28,
                    ui.Theme.Color(ColorToken.FgAccent)
                );
        }
    }
}

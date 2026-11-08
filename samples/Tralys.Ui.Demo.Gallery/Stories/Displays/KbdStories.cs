using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Kbd>("Displays")]
static class KbdStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Box(align: Align.Center, gap: 4))
        {
            ui.Kbd(text: "Ctrl");
            ui.Kbd(text: "Shift");
            ui.Kbd(text: "F");
        }
    }
}

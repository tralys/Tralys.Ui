using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<ImageButton>("Buttons")]
static class ImageButtonStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Box(align: Align.Center, gap: 16, padding: 16))
        {
            ui.ImageButton(
                source: "button.dds",
                pressedSource: "button-pressed.dds",
                width: 64,
                height: 64
            );
            ui.ImageButton(source: "badge.png", width: 50, height: 50);
            ui.ImageButton(
                source: "badge.png",
                activeSource: "badge-active.png",
                active: true,
                width: 50,
                height: 50
            );
        }
    }
}

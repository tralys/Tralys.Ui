using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Tag>("Displays")]
static class TagStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Box(align: Align.Center, gap: 8))
        {
            ui.Tag(text: "Member");
            ui.Tag(text: "Owner", scheme: ColorScheme.Primary);
            ui.Tag(text: "Blocked", icon: Icons.Slash, scheme: ColorScheme.Danger);
            ui.Tag(
                text: "Verified",
                scheme: ColorScheme.Success,
                variant: ComponentVariant.Outlined
            );
        }
    }
}

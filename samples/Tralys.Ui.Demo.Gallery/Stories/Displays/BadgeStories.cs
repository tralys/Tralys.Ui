using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Badge>("Displays")]
static class BadgeStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Box(align: Align.Center, gap: 8))
        {
            ui.Badge(count: 0);
            ui.Badge(count: 3);
            ui.Badge(count: 42);
            ui.Badge(count: 999);
        }
    }

    [FramePath]
    public static void Schemes(Ui ui)
    {
        using (ui.Box(align: Align.Center, gap: 8))
        {
            ui.Badge(count: 3);
            ui.Badge(count: 12, scheme: ColorScheme.Secondary);
            ui.Badge(count: 5, scheme: ColorScheme.Danger);
            ui.Badge(count: 2, prefix: "@");
        }
    }
}

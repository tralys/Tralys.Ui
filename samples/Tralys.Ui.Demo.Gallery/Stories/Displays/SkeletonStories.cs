using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Skeleton>("Displays")]
static class SkeletonStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8, width: 300))
        {
            ui.Skeleton(width: 180);
            ui.Skeleton(width: 260);
            ui.Skeleton(width: 120);
        }
    }
}

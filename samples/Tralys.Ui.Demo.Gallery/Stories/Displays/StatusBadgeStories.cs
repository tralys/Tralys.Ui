using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<StatusBadge>("Displays")]
static class StatusBadgeStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Box(align: Align.Center, gap: 8))
        {
            ui.StatusBadge(text: "Pending");
            ui.StatusBadge(text: "In progress", state: StatusBadgeState.Active);
            ui.StatusBadge(text: "Complete", state: StatusBadgeState.Done);
            ui.StatusBadge(text: "Locked", state: StatusBadgeState.Locked);
        }
    }
}

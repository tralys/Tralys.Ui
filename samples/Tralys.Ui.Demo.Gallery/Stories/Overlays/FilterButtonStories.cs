using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<FilterButton>("Overlays")]
static class FilterButtonStories
{
    static readonly Slot Filters = Slot.Of(static ui =>
    {
        using (ui.Box(direction: Direction.Column, gap: 4))
        {
            ui.Checkbox(value: true, label: "Joined");
            ui.Checkbox(value: false, label: "Left");
            ui.Checkbox(value: true, label: "Promoted");
        }
    });

    [FramePath]
    public static void States(Ui ui)
    {
        using (ui.Box(gap: 16))
        {
            ui.FilterButton(content: Filters);
            ui.FilterButton(content: Filters, active: true, tooltip: "Filter actions");
            ui.FilterButton(content: Filters, disabled: true);
        }
    }

    [FramePath]
    public static void Open(Ui ui)
    {
        using (ui.Box(align: Align.Start, justify: Justify.End, width: 320, height: 200))
            ui.FilterButton(content: Filters, active: true, defaultOpen: true);
    }
}

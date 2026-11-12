using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Pagination>("Data")]
static class PaginationStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 32))
        {
            ui.Pagination(value: 1, total: 100, pageSize: 10);
            ui.Pagination(value: 3, total: 100, pageSize: 10);
            ui.Pagination(value: 10, total: 100, pageSize: 10);
        }
    }

    [FramePath]
    public static void NoCommand(Ui ui)
    {
        var page = ui.State(1);
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            HoldingListBoxStories.Caption(ui, "Turns its own page: nothing handles the change");
            ui.Pagination(
                value: page.Value,
                onValueChange: page.Setter,
                total: 45,
                pageSize: 10,
                width: 320
            );
        }
    }

    [FramePath]
    public static void Empty(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            HoldingListBoxStories.Caption(ui, "One page or none: the paginator hides");
            ui.Pagination(total: 0, pageSize: 10, width: 320);
        }
    }
}

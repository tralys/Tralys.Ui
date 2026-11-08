using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<EmptyState>("Displays")]
static class EmptyStateStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                width: 360,
                height: 200,
                background: ui.Theme.Color(ColorToken.BgSunken)
            )
        )
            ui.EmptyState(text: "No items match these filters. Clear one to see more.");
    }

    [FramePath]
    public static void Backdrop(Ui ui)
    {
        using (ui.Box(width: 360, height: 200))
        {
            using (
                ui.Grid(
                    columns: GridTracks.Repeat(4, Track.Fr(1)),
                    rows: GridTracks.Repeat(3, Track.Fr(1)),
                    align: Align.Center,
                    justifyItems: Align.Center,
                    grow: 1
                )
            )
                for (var i = 0; i < 12; i++)
                    ui.Skeleton(width: 64, height: 40, key: i);
            using (
                ui.Box(
                    align: Align.Center,
                    justify: Justify.Center,
                    position: Position.Absolute,
                    left: 0,
                    top: 0,
                    right: 0,
                    bottom: 0
                )
            )
                ui.EmptyState(text: "Nothing stored yet", backdrop: true);
        }
    }
}

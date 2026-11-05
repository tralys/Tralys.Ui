using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class GridAllocationTests
{
    [Test]
    public async Task A_grid_remounting_with_changing_tracks_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            var step = model.Count % 4;
            if (step == 3)
                return;
            using (
                ui.Grid(
                    columns: step == 0
                        ? new GridTracks(Track.Px(100), Track.Fr(1))
                        : new GridTracks(Track.Auto, Track.Auto, Track.Fr(2)),
                    firstColumn: step,
                    width: 400
                )
            )
            {
                ui.Text("a");
                ui.Text("b");
            }
        });

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Content_sized_grids_whose_spanning_cell_grows_every_frame_allocate_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            var tall = 100 + model.Count % 7;
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                using (
                    ui.Grid(
                        columns: new GridTracks(Track.Auto, Track.Fr(1)),
                        rows: new GridTracks(Track.Fr(1), Track.Auto),
                        width: 200
                    )
                )
                {
                    using (ui.Box(column: 1, row: 1, id: "star")) { }
                    using (ui.Box(width: 30, height: 40, column: 1, row: 2)) { }
                    using (ui.Box(height: tall, column: 2, row: 1, rowSpan: 2)) { }
                }
                using (
                    ui.Grid(
                        columns: new GridTracks(Track.Auto, Track.Auto),
                        rows: new GridTracks(Track.Auto, Track.Auto)
                    )
                )
                {
                    using (ui.Box(width: 30, height: 40, column: 1, row: 2)) { }
                    using (ui.Box(width: 30, height: tall, column: 2, row: 1, rowSpan: 2)) { }
                }
            }
        });

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(driver.Get("star").Bounds.Height).IsEqualTo(60f + model.Count % 7);
    }

    [Test]
    public async Task Grids_shrunk_by_their_row_or_offered_less_than_their_content_resizing_every_frame_allocate_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            var wide = 80 + model.Count % 7;
            using (ui.Box(direction: Direction.Column, width: wide, align: Align.Start))
            {
                using (ui.Box())
                using (ui.Grid(columns: new GridTracks(Track.Auto, Track.Fr(1))))
                {
                    ui.Text("one two three four", wrap: true);
                    using (ui.ScrollArea(height: 40))
                    using (ui.Box(height: 200)) { }
                }
                using (ui.Grid(columns: new GridTracks(Track.Auto), id: "bounded"))
                    ui.Text("one two three four five six", wrap: true);
            }
        });

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(driver.Get("bounded").Bounds.Width).IsEqualTo(80f + model.Count % 7);
    }
}

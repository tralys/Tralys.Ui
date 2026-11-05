using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class LayoutTests
{
    [Test]
    public async Task Growing_items_share_free_space_from_their_unclamped_bases()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 300, height: 20))
            {
                using (ui.Box(basis: 100, grow: 1, minWidth: 120, id: "a")) { }
                using (ui.Box(basis: 100, grow: 1, id: "b")) { }
            }
        });

        await Assert.That(driver.Get("a").ViewBounds.Width).IsEqualTo(150f);
        await Assert.That(driver.Get("b").ViewBounds.Width).IsEqualTo(150f);
    }

    [Test]
    public async Task Shrinking_stops_at_min_content_unless_the_item_clips()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 60, height: 40, align: Align.Start))
            {
                ui.Text("unbreakable", wrap: true, id: "text");
                using (ui.Box(width: 200, height: 10, overflow: Overflow.Clip, id: "clip")) { }
            }
        });
        using var natural = UiDriver.Render(ui =>
        {
            using (ui.Box())
                ui.Text("unbreakable", id: "text");
        });

        await Assert
            .That(driver.Get("text").ViewBounds.Width)
            .IsEqualTo(natural.Get("text").ViewBounds.Width);
        await Assert.That(driver.Get("clip").ViewBounds.Width).IsEqualTo(0f);
    }

    [Test]
    public async Task Text_that_grows_widens_the_auto_sized_row_around_it()
    {
        var model = new Model { Name = "a" };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (ui.Box(id: "row"))
            {
                ui.Text(model.Name, id: "label");
                using (ui.Box(width: 10, height: 10)) { }
            }
        });

        model.Name = "a label far longer than before";
        driver.Frame();

        await Assert
            .That(driver.Get("row").ViewBounds.Width)
            .IsEqualTo(driver.Get("label").ViewBounds.Width + 10f);
    }

    [Test]
    public async Task A_fixed_size_box_that_changes_size_resizes_its_auto_sized_parent()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (ui.Box(id: "row"))
            using (ui.Box(width: model.Count == 0 ? 10 : 50, height: 10)) { }
        });

        model.Count = 1;
        driver.Frame();

        await Assert.That(driver.Get("row").ViewBounds.Width).IsEqualTo(50f);
    }

    [Test]
    public async Task Text_that_keeps_its_size_costs_its_auto_sized_ancestors_no_measure()
    {
        var shallow = new Model { Name = "abc" };
        var deep = new Model { Name = "abc" };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (ui.Box(padding: 2))
                    ui.Text(shallow.Name, width: 100);
                using (ui.Box(padding: 2))
                using (ui.Box(padding: 2))
                using (ui.Box(padding: 2))
                using (ui.Box(padding: 2))
                using (ui.Box(padding: 2))
                    ui.Text(deep.Name, width: 100);
            }
        });

        shallow.Name = "xyz";
        driver.Frame();
        var near = driver.Stats.Layouts;
        deep.Name = "xyz";
        driver.Frame();

        await Assert.That(driver.Stats.Layouts - near).IsLessThan(2 * 4);
    }

    [Test]
    public async Task An_fr_row_holds_an_item_whose_min_height_is_zero_or_that_clips_to_its_share()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Grid(
                    columns: new GridTracks(Track.Fr(1), Track.Fr(1)),
                    rows: new GridTracks(Track.Fr(1)),
                    width: 200,
                    height: 100
                )
            )
            {
                using (ui.Box(minHeight: 0, id: "unfloored"))
                using (ui.Box(height: 500)) { }
                using (ui.Box(overflow: Overflow.Clip, id: "clipping"))
                using (ui.Box(height: 500)) { }
            }
        });

        await Assert.That(driver.Get("unfloored").ViewBounds.Height).IsEqualTo(100f);
        await Assert.That(driver.Get("clipping").ViewBounds.Height).IsEqualTo(100f);
    }

    [Test]
    public async Task Fixed_then_content_then_fr_tracks_size_in_that_order()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Grid(columns: new GridTracks(Track.Px(100), Track.Auto, Track.Fr(1)), width: 400)
            )
            {
                using (ui.Box(column: 1, height: 10, id: "fixed")) { }
                using (ui.Box(column: 2, width: 150, height: 10, id: "auto")) { }
                using (ui.Box(column: 3, height: 10, id: "fr")) { }
            }
        });

        await Assert.That(driver.Get("fixed").ViewBounds.Width).IsEqualTo(100f);
        await Assert.That(driver.Get("auto").ViewBounds.Width).IsEqualTo(150f);
        await Assert.That(driver.Get("fr").ViewBounds.Width).IsEqualTo(150f);
    }

    [Test]
    public async Task A_grid_items_percentage_resolves_against_its_cell_and_sizes_its_track_as_auto()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Grid(columns: new GridTracks(Track.Px(100), Track.Auto, Track.Fr(1)), width: 400)
            )
            {
                using (ui.Box(column: 1, width: Length.Full, height: 10, id: "fixed")) { }
                using (ui.Box(column: 2, width: Length.Full, id: "auto"))
                using (ui.Box(width: 50, height: 10)) { }
                using (ui.Box(column: 3, height: 10, id: "fr")) { }
            }
        });

        await Assert.That(driver.Get("fixed").ViewBounds.Width).IsEqualTo(100f);
        await Assert.That(driver.Get("auto").ViewBounds.Width).IsEqualTo(50f);
        await Assert.That(driver.Get("fr").ViewBounds.Width).IsEqualTo(250f);
    }

    [Test]
    public async Task A_stretched_grid_item_keeps_to_its_max_width_and_its_auto_row_fits_it_there()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (ui.Grid(columns: new GridTracks(Track.Px(200))))
            {
                using (ui.Box(maxWidth: 60, id: "capped"))
                    ui.Text("one two three four", wrap: true);
                using (ui.Box(width: 60, id: "sized"))
                    ui.Text("one two three four", wrap: true);
            }
        });

        var capped = driver.Get("capped").ViewBounds;
        await Assert.That(capped.Width).IsEqualTo(60f);
        await Assert.That(capped.Height).IsEqualTo(driver.Get("sized").ViewBounds.Height);
    }

    [Test]
    public async Task A_grid_its_row_shrinks_wraps_its_auto_columns_text_to_the_room()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 100, align: Align.Start))
            using (ui.Grid(columns: new GridTracks(Track.Auto), id: "grid"))
                ui.Text("one two three four five six", wrap: true, id: "text");
        });

        var grid = driver.Get("grid").ViewBounds;
        await Assert.That(grid.Width).IsEqualTo(100f);
        await Assert.That(grid).IsEqualTo(driver.Get("text").ViewBounds);
    }

    [Test]
    public async Task A_grid_offered_less_than_its_max_content_measures_as_tall_as_it_arranges()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, width: 100, align: Align.Start))
            using (ui.Grid(columns: new GridTracks(Track.Auto), id: "grid"))
                ui.Text("one two three four five six", wrap: true, id: "text");
        });

        var grid = driver.Get("grid").ViewBounds;
        await Assert.That(grid.Width).IsEqualTo(100f);
        await Assert.That(grid).IsEqualTo(driver.Get("text").ViewBounds);
    }

    [Test]
    public async Task A_scroll_area_in_a_definite_grids_auto_row_keeps_to_the_row_and_scrolls()
    {
        var scroll = new ScrollHandle();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, width: 200, height: 100))
            using (ui.Grid(grow: 1, minHeight: 0))
            using (ui.ScrollArea(scroll: scroll, id: "area"))
            using (ui.Box(height: 500)) { }
        });

        driver.Wheel(driver.Get("area"), new Vector2(0f, -1f));

        await Assert.That(driver.Get("area").ViewBounds.Height).IsEqualTo(100f);
        await Assert.That(scroll.Offset.Y).IsGreaterThan(0f);
    }

    [Test]
    public async Task Auto_placement_is_sparse_and_row_major()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Grid(
                    columns: GridTracks.Repeat(3, Track.Px(50)),
                    rows: new GridTracks(Track.Px(20))
                )
            )
            {
                using (ui.Box(columnSpan: 2, height: 10, id: "a")) { }
                using (ui.Box(height: 10, id: "b")) { }
                using (ui.Box(height: 10, id: "c")) { }
            }
        });

        await Assert.That(driver.Get("b").ViewBounds.X).IsEqualTo(100f);
        await Assert.That(driver.Get("c").ViewBounds.X).IsEqualTo(0f);
        await Assert.That(driver.Get("c").ViewBounds.Y).IsEqualTo(20f);
    }

    [Test]
    public async Task A_span_grows_its_content_tracks_evenly()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (
                ui.Grid(columns: new GridTracks(Track.Auto, Track.Auto), justifyItems: Align.Start)
            )
            {
                using (ui.Box(column: 1, row: 1, width: 50, height: 10, id: "left")) { }
                using (ui.Box(column: 2, row: 1, width: 50, height: 10, id: "right")) { }
                using (ui.Box(column: 1, row: 2, columnSpan: 2, width: 200, height: 10)) { }
            }
        });

        await Assert.That(driver.Get("right").ViewBounds.X).IsEqualTo(100f);
    }

    [Test]
    public async Task A_grid_with_no_tracks_overlays_its_children_in_one_cell_as_large_as_the_largest()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (ui.Grid(justifyItems: Align.Start, align: Align.Start, id: "grid"))
            {
                using (ui.Box(width: 40, height: 10, id: "wide")) { }
                using (ui.Box(width: 10, height: 30, id: "tall")) { }
            }
        });

        await Assert.That(driver.Get("grid").ViewBounds).IsEqualTo(new Rect(0, 0, 40, 30));
        await Assert.That(driver.Get("tall").ViewBounds).IsEqualTo(new Rect(0, 0, 10, 30));
    }

    [Test]
    public async Task Snapped_neighbours_share_their_edges()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 100, height: 10))
            {
                using (ui.Box(grow: 1, id: "a")) { }
                using (ui.Box(grow: 1, id: "b")) { }
                using (ui.Box(grow: 1, id: "c")) { }
            }
        });

        var a = driver.Get("a").ViewBounds;
        var b = driver.Get("b").ViewBounds;
        var c = driver.Get("c").ViewBounds;
        await Assert.That(b.X).IsEqualTo(a.Right);
        await Assert.That(c.X).IsEqualTo(b.Right);
        await Assert.That(c.Right).IsEqualTo(100f);
    }

    [Test]
    public async Task Centred_lines_a_pixel_too_tall_for_their_room_overflow_it_by_that_pixel_at_the_start()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (
                ui.Box(
                    width: 30,
                    height: 27,
                    padding: 4,
                    wrap: FlexWrap.Wrap,
                    alignContent: AlignContent.Center
                )
            )
            {
                using (ui.Box(width: 22, height: 10, id: "first")) { }
                using (ui.Box(width: 22, height: 10)) { }
            }
        });

        await Assert.That(driver.Get("first").ViewBounds.Y).IsEqualTo(3f);
    }

    [Test]
    public async Task Two_insets_stretch_an_absolute_child()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 200, height: 100, padding: 7))
            {
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 10,
                        right: 30,
                        top: 5,
                        height: 10,
                        id: "abs"
                    )
                ) { }
            }
        });

        await Assert.That(driver.Get("abs").ViewBounds).IsEqualTo(new Rect(10, 5, 160, 10));
    }

    [Test]
    public async Task A_root_child_and_an_absolute_child_stretched_between_insets_keep_to_their_limits()
    {
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(maxWidth: 120, maxHeight: 50, id: "panel"))
                {
                    using (
                        ui.Box(
                            position: Position.Absolute,
                            left: 10,
                            right: 10,
                            top: 0,
                            bottom: 0,
                            maxWidth: 40,
                            minHeight: 80,
                            id: "abs"
                        )
                    ) { }
                }
            },
            new Size(300, 200)
        );

        await Assert.That(driver.Get("panel").ViewBounds).IsEqualTo(new Rect(0, 0, 120, 50));
        await Assert.That(driver.Get("abs").ViewBounds).IsEqualTo(new Rect(10, 0, 40, 80));
    }

    [Test]
    public async Task A_box_aligns_by_its_texts_first_line_as_that_text_wraps_beside_its_siblings()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Baseline))
            {
                ui.Text("x", fontSize: 10, id: "label");
                using (ui.Box(width: 200))
                {
                    using (ui.Text(wrap: true, id: "inner"))
                    {
                        ui.Span("aa ", fontSize: 10);
                        ui.Span("BIG", fontSize: 40);
                    }
                    using (ui.Box(width: 150, height: 1, shrink: 0)) { }
                }
            }
        });

        await Assert
            .That(driver.Get("label").ViewBounds.Y)
            .IsEqualTo(driver.Get("inner").ViewBounds.Y)
            .Within(0.01f);
    }

    [Test]
    public async Task A_quarter_turn_swaps_the_measured_axes()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            {
                using (
                    ui.Box(width: 100, height: 20, quarterTurns: 1, background: Ink, id: "turned")
                ) { }
                using (ui.Box(width: 10, height: 10, id: "after")) { }
            }
        });

        await Assert.That(driver.Get("turned").ViewBounds).IsEqualTo(new Rect(0, 0, 20, 100));
        await Assert.That(driver.Get("after").ViewBounds.X).IsEqualTo(20f);
        await Assert
            .That(driver.View.HitTest(new System.Numerics.Vector2(15, 90))?.Id)
            .IsEqualTo("turned");
    }

    static readonly Color Ink = Color.FromRgb(200, 60, 40);

    [Test]
    public async Task A_box_insets_its_child_by_its_border_then_its_padding_and_the_child_by_its_margins()
    {
        var border = new Border(new Thickness(1, 2, 3, 4), Ink);
        var padding = new Thickness(5, 6, 7, 8);
        var margin = new Thickness(5, 6, 7, 8);
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (ui.Box(border: border, padding: padding, id: "auto"))
                using (ui.Box(width: 10, height: 10, margin: margin, id: "inner")) { }
                using (
                    ui.Box(
                        width: 200,
                        height: 100,
                        justify: Justify.End,
                        align: Align.End,
                        border: border,
                        padding: padding,
                        id: "fixed"
                    )
                )
                using (ui.Box(width: 50, height: 20, margin: margin, id: "end")) { }
            }
        });
        var auto = driver.Get("auto").ViewBounds;
        var inner = driver.Get("inner").ViewBounds;
        var fixedBox = driver.Get("fixed").ViewBounds;
        var end = driver.Get("end").ViewBounds;

        await Assert.That(auto.Size).IsEqualTo(new Size(38, 44));
        await Assert.That((inner.X - auto.X, inner.Y - auto.Y)).IsEqualTo((11f, 14f));
        await Assert.That((end.X - fixedBox.X, end.Y - fixedBox.Y)).IsEqualTo((133f, 60f));
    }

    [Test]
    public async Task Edges_snap_to_device_pixels_at_the_views_scale_and_a_half_rounds_up()
    {
        using var scaled = UiDriver.Render(
            ui =>
            {
                using (ui.Box(align: Align.Start))
                using (
                    ui.Box(width: 20, height: 20, margin: new Thickness(10.4f, 0, 0, 0), id: "box")
                ) { }
            },
            scale: 1.5f
        );
        using var half = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (ui.Box(width: 20, height: 20, margin: new Thickness(12.5f, 0, 0, 0), id: "box"))
            { }
        });

        await Assert.That(scaled.Get("box").ViewBounds.X).IsEqualTo(16f / 1.5f);
        await Assert.That(half.Get("box").ViewBounds.X).IsEqualTo(13f);
    }

    [Test]
    public async Task A_collapsed_child_takes_no_space_or_gap_and_a_hidden_one_keeps_its_space_and_draws_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (ui.Box(direction: Direction.Column, gap: 5, id: "column"))
            {
                using (ui.Box(width: 10, height: 10, background: Ink)) { }
                using (
                    ui.Box(
                        width: 10,
                        height: 20,
                        background: Ink,
                        collapsed: model.Count == 0,
                        hidden: model.Count == 1
                    )
                ) { }
                using (ui.Box(width: 10, height: 30, background: Ink, id: "last")) { }
            }
        });
        var collapsed = Placed(driver);

        model.Count = 1;
        driver.Frame();

        await Assert.That(collapsed).IsEqualTo((15f, 45f, 2));
        await Assert.That(Placed(driver)).IsEqualTo((40f, 70f, 2));
    }

    static (float LastY, float Height, int Boxes) Placed(UiDriver driver)
    {
        var boxes = 0;
        foreach (var line in driver.Draw().Lines)
        {
            if (line.Contains(": box ", StringComparison.Ordinal))
                boxes++;
        }
        return (driver.Get("last").ViewBounds.Y, driver.Get("column").ViewBounds.Height, boxes);
    }

    [Test]
    public async Task A_child_shown_again_after_collapsing_lays_out_in_its_slot_as_it_now_is()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (ui.Grid())
            {
                using (ui.Box(width: 10, height: model.Count >= 2 ? 60 : 20)) { }
                using (ui.Box(width: 10, collapsed: model.Count is 1 or 2, id: "overlay")) { }
            }
        });

        for (var step = 1; step <= 3; step++)
        {
            model.Count = step;
            driver.Frame();
        }

        await Assert.That(driver.Get("overlay").ViewBounds.Height).IsEqualTo(60f);
    }

    [Test]
    public async Task A_wrapping_row_gives_a_collapsed_child_no_slot_and_one_wider_than_a_line_a_line_alone()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (ui.Box(width: 100, wrap: FlexWrap.Wrap, align: Align.Start))
            {
                using (ui.Box(width: 150, height: 10, id: "wide")) { }
                using (ui.Box(width: 40, height: 30, id: "tall")) { }
                using (ui.Box(width: 40, height: 40, collapsed: true)) { }
                using (ui.Box(width: 40, height: 20, id: "beside")) { }
                using (ui.Box(width: 40, height: 10, id: "wrapped")) { }
            }
        });

        await Assert.That(At(driver, "wide")).IsEqualTo((0f, 0f));
        await Assert.That(At(driver, "tall")).IsEqualTo((0f, 10f));
        await Assert.That(At(driver, "beside")).IsEqualTo((40f, 10f));
        await Assert.That(At(driver, "wrapped")).IsEqualTo((0f, 40f));
    }

    [Test]
    public async Task An_item_on_a_later_line_keeps_its_lines_cross_size_however_many_lines_measuring_it_adds()
    {
        for (var lines = 1; lines <= 40; lines++)
        {
            using var driver = UiDriver.Render(ui =>
            {
                using (ui.Box(align: Align.Start))
                using (ui.Box(width: 100, wrap: FlexWrap.Wrap))
                {
                    using (ui.Box(width: 95, height: 10)) { }
                    using (ui.Box(wrap: FlexWrap.Wrap, maxWidth: 10, id: "stack"))
                    {
                        for (var i = 0; i < lines; i++)
                            using (ui.Box(width: 10, height: 10, key: i)) { }
                    }
                }
            });

            await Assert
                .That(driver.Get("stack").ViewBounds)
                .IsEqualTo(new Rect(0, 10, 10, lines * 10));
        }
    }

    static (float X, float Y) At(UiDriver driver, string id) =>
        (driver.Get(id).ViewBounds.X, driver.Get(id).ViewBounds.Y);

    [Test]
    public async Task A_content_sized_grid_shares_a_span_across_its_fr_tracks_by_weight()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (
                ui.Grid(
                    rows: new GridTracks(Track.Fr(1), Track.Fr(2), Track.Auto, Track.Px(20)),
                    columns: new GridTracks(Track.Px(50))
                )
            )
            {
                using (ui.Box(row: 1, column: 1, id: "first")) { }
                using (ui.Box(row: 2, column: 1, id: "second")) { }
                using (ui.Box(row: 1, column: 1, rowSpan: 4, height: 300)) { }
                using (ui.Box(row: 3, column: 1, height: 40)) { }
            }
        });

        await Assert.That(driver.Get("first").ViewBounds.Height).IsEqualTo(80f);
        await Assert.That(driver.Get("second").ViewBounds.Height).IsEqualTo(160f);
    }

    [Test]
    public async Task Fr_tracks_snap_their_edges_so_their_cells_meet_and_fill_the_grid()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Grid(columns: GridTracks.Repeat(3, Track.Fr(1)), width: 100))
            {
                using (ui.Box(height: 10, id: "a")) { }
                using (ui.Box(height: 10, id: "b")) { }
                using (ui.Box(height: 10, id: "c")) { }
            }
        });
        var a = driver.Get("a").ViewBounds;
        var b = driver.Get("b").ViewBounds;
        var c = driver.Get("c").ViewBounds;

        await Assert.That((a.X, a.Right, b.Right, c.Right)).IsEqualTo((0f, 33f, 67f, 100f));
        await Assert.That((b.X, c.X)).IsEqualTo((a.Right, b.Right));
    }

    [Test]
    public async Task Auto_placement_gives_a_collapsed_child_no_cell_and_the_gap_parts_columns_and_rows()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (ui.Grid(columns: GridTracks.Repeat(2, Track.Fr(1)), gap: 4, width: 100))
            {
                using (ui.Box(height: 10, id: "first")) { }
                using (ui.Box(height: 10, collapsed: true)) { }
                using (ui.Box(height: 10, id: "second")) { }
                using (ui.Box(height: 10, id: "third")) { }
            }
        });

        await Assert.That(driver.Get("first").ViewBounds).IsEqualTo(new Rect(0, 0, 48, 10));
        await Assert.That(driver.Get("second").ViewBounds).IsEqualTo(new Rect(52, 0, 48, 10));
        await Assert.That(driver.Get("third").ViewBounds).IsEqualTo(new Rect(0, 14, 48, 10));
    }
}

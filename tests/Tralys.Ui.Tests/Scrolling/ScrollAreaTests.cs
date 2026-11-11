using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class ScrollAreaTests
{
    static readonly string[] Ids = ["content0", "content1", "content2", "content3"];

    [Test]
    public async Task An_arrow_glides_a_line_unless_smooth_scrolling_is_off_and_the_thumb_moves_the_content_at_once()
    {
        var area = new AreaFixture();
        using var driver = area.Render();
        var down = Bars.Arrow(driver, forward: true);

        driver.Click(down, settle: false);
        driver.Frame();
        driver.Frame();
        var gliding = area.Handle!.Offset.Y;
        driver.Settle();
        var glided = area.Handle.Offset.Y;
        var held = driver.Hold(Bars.Thumb(driver));
        held.MoveBy(new Vector2(0f, 2f), settle: false);
        driver.Frame();
        var dragged = area.Handle.Offset.Y;
        held.Release();
        driver.View.Context.SmoothScrolling = false;
        driver.Click(down, settle: false);
        driver.Frame();

        var track = Bars.Track(driver).ViewBounds.Height;
        var room = track - Bars.Thumb(driver).ViewBounds.Height;
        await Assert.That(gliding).IsGreaterThan(0f).And.IsLessThan(16f);
        await Assert.That(glided).IsEqualTo(16f);
        await Assert.That(dragged).IsEqualTo(16f + 2f * 900f / room).Within(0.01f);
        await Assert.That(area.Handle.Offset.Y).IsEqualTo(dragged + 16f).Within(0.01f);
    }

    [Test]
    public async Task A_held_press_on_the_track_pages_at_once_then_every_100_ms_after_250_ms_until_the_thumb_reaches_the_pointer()
    {
        var area = new AreaFixture();
        using var driver = area.Render();
        driver.View.Context.SmoothScrolling = false;

        var held = driver.Hold(Bars.Page(driver, forward: true), settle: false);
        driver.Frame();
        var pressed = area.Handle!.Offset.Y;
        driver.Frame(0.2f);
        var waiting = area.Handle.Offset.Y;
        driver.Frame(0.1f);
        var repeated = area.Handle.Offset.Y;
        for (var i = 0; i < 180; i++)
            driver.Frame();
        var thumb = Bars.Thumb(driver).ViewBounds;
        held.Release(settle: false);
        driver.Frame(0.7f);

        await Assert.That((pressed, waiting, repeated)).IsEqualTo((100f, 100f, 200f));
        await Assert.That(area.Handle.Offset.Y).IsGreaterThan(200f).And.IsLessThan(900f);
        await Assert.That(thumb.Y).IsLessThanOrEqualTo(held.Position.Y);
        await Assert.That(thumb.Bottom).IsGreaterThan(held.Position.Y);
    }

    [Test]
    public async Task Idle_bars_rest_dim_and_show_while_hovered_focused_within_or_dragged_fading_back_after_a_pause()
    {
        var area = new AreaFixture();
        using var driver = area.Render();
        float Shown() => Bars.Root(driver.All<ScrollAreaBar>()[0]).Visual.Opacity ?? 1f;

        var resting = Shown();
        driver.Hover(driver.Get("area"));
        var hovered = Shown();
        driver.Unhover(settle: false);
        driver.Frame(1f);
        var pausing = Shown();
        driver.Frame(0.5f);
        driver.Settle();
        var faded = Shown();
        driver.Tab();
        driver.Tab();
        var focused = Shown();
        driver.Focus(driver.Get("outside"));
        var held = driver.Hold(Bars.Thumb(driver));
        held.MoveTo(driver.Get("outside"), settle: false);
        driver.Frame(3f);
        var dragged = Shown();
        held.Release(settle: false);
        driver.Frame(0.5f);
        var released = Shown();
        driver.Settle();
        area.ContentHeight = 2000f;
        driver.Rerender();
        area.Handle!.ScrollToEnd();
        driver.Settle();

        await Assert.That((resting, hovered, pausing, faded)).IsEqualTo((0.35f, 1f, 1f, 0.35f));
        await Assert.That((focused, dragged, released)).IsEqualTo((1f, 1f, 1f));
        await Assert.That(area.Handle.Offset.Y).IsGreaterThan(900f);
        await Assert.That(Shown()).IsEqualTo(0.35f);
    }

    [Test]
    public async Task An_auto_bar_shows_only_while_the_content_overflows_and_a_hidden_one_takes_no_space_yet_scrolls()
    {
        var area = new AreaFixture { ContentHeight = 50f, Padding = 12f };
        using var driver = area.Render();
        var fitting = (Bars.Shown(driver), area.Handle!.Viewport);
        area.ContentHeight = 300f;
        driver.Rerender();
        var overflowing = (Bars.Shown(driver), area.Handle.Viewport);
        var origin = driver.Get("area").ViewBounds;
        var bar = Bars.Root(driver.All<Scrollbar>()[0]).ViewBounds;
        var content = driver.Get("content").ViewBounds;
        area.Vertical = ScrollbarVisibility.Hidden;
        driver.Rerender();
        var hidden = (Bars.Shown(driver), area.Handle.Viewport);
        driver.Wheel(driver.Get("content"), new Vector2(0f, -1f));
        var wheeled = area.Handle.Offset.Y;
        area.ContentHeight = 50f;
        area.Vertical = ScrollbarVisibility.Visible;
        driver.Rerender();

        await Assert.That(fitting).IsEqualTo((0, new Size(176f, 76f)));
        await Assert.That(overflowing).IsEqualTo((1, new Size(160f, 76f)));
        await Assert.That(bar).IsEqualTo(new Rect(origin.X + 184f, origin.Y, 16f, 100f));
        await Assert.That((content.X - origin.X, content.Y - origin.Y)).IsEqualTo((12f, 12f));
        await Assert.That(hidden).IsEqualTo((0, new Size(176f, 76f)));
        await Assert.That(wheeled).IsEqualTo(48f);
        await Assert.That(Bars.Shown(driver)).IsEqualTo(1);
        await Assert.That(driver.All<ScrollbarThumb>().Count).IsEqualTo(0);
    }

    [Test]
    public async Task Its_border_holds_its_bars_and_its_viewport_inside()
    {
        var area = new AreaFixture { ContentHeight = 300f, Edge = 2f };
        using var driver = area.Render();

        var origin = driver.Get("area").ViewBounds;
        var bar = Bars.Root(driver.All<Scrollbar>()[0]).ViewBounds;
        var content = driver.Get("content").ViewBounds;

        await Assert.That(bar).IsEqualTo(new Rect(origin.X + 182f, origin.Y + 2f, 16f, 96f));
        await Assert.That((content.X - origin.X, content.Y - origin.Y)).IsEqualTo((2f, 2f));
        await Assert.That(area.Handle!.Viewport).IsEqualTo(new Size(180f, 96f));
    }

    [Test]
    public async Task An_axis_with_scrolling_off_has_no_bar_and_wraps_its_content_to_the_viewport()
    {
        var area = new AreaFixture
        {
            Horizontal = ScrollbarVisibility.Disabled,
            ContentHeight = 0f,
            Text =
                "A line long enough to wrap many times over inside a viewport only two hundred wide",
        };
        using var driver = area.Render();

        driver.Wheel(driver.Get("content"), new Vector2(1f, 0f));

        await Assert.That(Bars.Shown(driver)).IsEqualTo(0);
        await Assert.That(driver.GetText(area.Text).ViewBounds.Width).IsLessThanOrEqualTo(200f);
        await Assert.That(driver.GetText(area.Text).ViewBounds.Height).IsGreaterThan(40f);
        await Assert.That(area.Handle!.Offset.X).IsEqualTo(0f);
    }

    [Test]
    public async Task A_turn_over_the_areas_empty_part_still_scrolls_it()
    {
        var area = new AreaFixture { ContentWidth = 40f };
        using var driver = area.Render();

        driver.Wheel(driver.Get("area"), new Vector2(0f, -1f));

        await Assert.That(area.Handle!.Offset.Y).IsEqualTo(48f);
    }

    [Test]
    public async Task A_turn_over_its_bar_scrolls_it()
    {
        var area = new AreaFixture();
        using var driver = area.Render();

        driver.Wheel(Bars.Track(driver), new Vector2(0f, -1f));

        await Assert.That(area.Handle!.Offset.Y).IsEqualTo(48f);
    }

    [Test]
    public async Task Each_panning_mode_takes_only_the_axes_and_moves_it_allows()
    {
        var handles = new ScrollHandle[4];
        Panning[] modes =
        [
            Panning.HorizontalFirst,
            Panning.HorizontalFirst,
            Panning.VerticalOnly,
            Panning.HorizontalOnly,
        ];
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(gap: 8))
                    for (var i = 0; i < modes.Length; i++)
                    {
                        handles[i] = ui.ScrollHandle(key: i);
                        using (
                            ui.ScrollArea(
                                scroll: handles[i],
                                panning: modes[i],
                                width: 100,
                                height: 100,
                                key: i
                            )
                        )
                        using (ui.Box(width: 400, height: 400, id: Ids[i])) { }
                    }
            },
            new Size(600, 200)
        );

        driver.Drag(driver.Get("content0"), new Vector2(-10f, -40f), PointerKind.Touch);
        driver.Drag(driver.Get("content1"), new Vector2(-40f, -30f), PointerKind.Touch);
        driver.Drag(driver.Get("content2"), new Vector2(-20f, -40f), PointerKind.Touch);
        driver.Drag(driver.Get("content3"), new Vector2(-40f, -20f), PointerKind.Touch);

        await Assert
            .That(handles.Select(static h => h.Offset))
            .IsEquivalentTo([
                Vector2.Zero,
                new Vector2(40f, 30f),
                new Vector2(0f, 40f),
                new Vector2(40f, 0f),
            ]);
    }

    [Test]
    public async Task A_finger_lifted_slowly_or_after_holding_still_leaves_the_content_where_it_is_and_a_coast_stops_at_the_end()
    {
        var area = new AreaFixture { ContentHeight = 4000f };
        using var driver = area.Render();
        var content = driver.Get("content");

        var slow = driver.Hold(content, PointerKind.Touch, settle: false);
        slow.MoveBy(new Vector2(0f, -20f), steps: 20, seconds: 1f, settle: false);
        slow.Release();
        var crept = area.Handle!.Offset.Y;
        var still = driver.Hold(content, PointerKind.Touch, settle: false);
        still.MoveBy(new Vector2(0f, -100f), steps: 5, seconds: 0.08f, settle: false);
        driver.Frame(0.15f);
        still.Release();
        var held = area.Handle.Offset.Y;
        driver.Fling(content, new Vector2(0f, -40f), seconds: 0.1f);

        await Assert.That(crept).IsEqualTo(20f);
        await Assert.That(held).IsEqualTo(120f);
        await Assert
            .That(area.Handle.Offset.Y)
            .IsGreaterThan(160f + 40f)
            .And.IsLessThan(160f + 120f);
    }

    [Test]
    public async Task Of_nested_areas_each_pans_only_the_way_it_scrolls()
    {
        var outer = new ScrollState();
        using var driver = UiDriver.Render(
            ui =>
            {
                outer.Outer = ui.ScrollHandle();
                outer.Inner = ui.ScrollHandle();
                using (
                    ui.ScrollArea(
                        scroll: outer.Outer,
                        horizontal: ScrollbarVisibility.Disabled,
                        width: 280,
                        height: 160
                    )
                )
                using (ui.Box(direction: Direction.Column, gap: 16))
                {
                    using (
                        ui.ScrollArea(
                            scroll: outer.Inner,
                            vertical: ScrollbarVisibility.Disabled,
                            height: 48
                        )
                    )
                    using (ui.Box(gap: 16, id: "strip"))
                        for (var i = 0; i < 3; i++)
                            using (ui.Box(width: 120, height: 20, key: i)) { }
                    using (ui.Box(height: 400, shrink: 0)) { }
                }
            },
            new Size(400, 300)
        );

        driver.Fling(driver.Get("strip"), new Vector2(-40f, 4f));
        var across = (outer.Inner!.Offset, outer.Outer!.Offset);
        driver.Drag(driver.Get("strip"), new Vector2(4f, -40f), PointerKind.Touch);

        await Assert.That(across.Item1.X).IsGreaterThan(40f);
        await Assert.That(across.Item2).IsEqualTo(Vector2.Zero);
        await Assert.That(outer.Outer.Offset).IsEqualTo(new Vector2(0f, 40f));
        await Assert.That(outer.Inner.Offset).IsEqualTo(across.Item1);
    }

    [Test]
    public async Task A_drag_to_pan_area_rests_under_the_scroll_all_cursor_and_pans_only_past_the_threshold_without_coasting()
    {
        var area = new AreaFixture
        {
            DragToPan = true,
            ContentWidth = 400f,
            ContentHeight = 400f,
            Cursor = Cursors.Hand,
        };
        using var driver = area.Render();
        var content = driver.Get("content");

        driver.Hover(content);
        var resting = driver.Cursor;
        driver.Hover(Bars.Thumb(driver));
        var overBar = driver.Cursor;
        var mouse = driver.Hold(content);
        mouse.MoveBy(new Vector2(-3f, 0f));
        var jittered = area.Handle!.Offset;
        mouse.MoveBy(new Vector2(0f, -3f));
        var crossed = area.Handle.Offset;
        mouse.MoveBy(new Vector2(-17f, -17f));
        var panning = driver.Cursor;
        mouse.Release(settle: false);
        for (var i = 0; i < 30; i++)
            driver.Frame();
        var released = area.Handle.Offset;
        area.DragToPan = false;
        driver.Rerender();
        driver.Hover(content);

        await Assert
            .That((resting, overBar, panning))
            .IsEqualTo((Cursors.ScrollAll, Cursors.Arrow, Cursors.ScrollAll));
        await Assert.That(jittered).IsEqualTo(Vector2.Zero);
        await Assert.That(crossed).IsEqualTo(new Vector2(3f, 3f));
        await Assert.That(released).IsEqualTo(new Vector2(20f, 20f));
        await Assert.That(driver.Cursor).IsEqualTo(Cursors.Hand);
    }

    [Test]
    public async Task A_drag_to_pan_area_leaves_a_drag_from_its_bar_a_thumb_or_another_button_alone()
    {
        var area = new AreaFixture
        {
            DragToPan = true,
            ContentWidth = 400f,
            ContentHeight = 400f,
            WithThumb = true,
        };
        using var driver = area.Render();
        var content = driver.Get("content");

        var right = driver.Hold(content, button: MouseButton.Right);
        right.MoveBy(new Vector2(-30f, -30f));
        right.Release();
        var held = driver.Hold(driver.Get("thumb"));
        for (var i = 0; i < 8; i++)
            held.MoveBy(new Vector2(10f, 4f));
        held.Release();
        var bar = driver.Hold(Bars.Thumb(driver));
        bar.MoveBy(new Vector2(0f, 10f));
        bar.Release();

        await Assert.That(area.Dragged).IsEqualTo(new Vector2(80f, 32f));
        await Assert.That(area.Handle!.Offset.X).IsEqualTo(0f);
        await Assert.That(area.Handle.Offset.Y).IsGreaterThan(0f);
    }

    [Test]
    public async Task Of_nested_drag_to_pan_areas_the_inner_takes_the_drag_and_hovering_after_pans_nothing()
    {
        var outer = new ScrollState();
        using var driver = UiDriver.Render(
            ui =>
            {
                outer.Outer = ui.ScrollHandle();
                outer.Inner = ui.ScrollHandle();
                using (ui.ScrollArea(scroll: outer.Outer, dragToPan: true, width: 200, height: 200))
                using (ui.Box(width: 600, height: 600))
                using (ui.ScrollArea(scroll: outer.Inner, dragToPan: true, width: 100, height: 100))
                using (ui.Box(width: 300, height: 300, id: "inner")) { }
            },
            new Size(400, 400)
        );

        var mouse = driver.Hold(driver.Get("inner"));
        mouse.MoveBy(new Vector2(-10f, 0f));
        mouse.Release();
        driver.Hover(driver.Get("inner"));

        await Assert.That(outer.Inner!.Offset).IsEqualTo(new Vector2(10f, 0f));
        await Assert.That(outer.Outer!.Offset).IsEqualTo(Vector2.Zero);
    }

    [Test]
    public async Task Followers_hold_the_leads_horizontal_range_and_offset_and_scrolling_either_moves_both()
    {
        var follow = new FollowFixture();
        using var driver = follow.Render();

        driver.Drag(driver.Get("board"), new Vector2(-30f, 0f));
        var panned = (follow.Lead!.Offset.X, follow.Header!.Offset.X, follow.Footer!.Offset.X);
        follow.Header.ScrollTo(150f, null);
        driver.Settle();
        var asked = (follow.Lead.Offset.X, follow.Header.Offset.X);
        follow.Lead.ScrollTo(1000f, null);
        driver.Settle();

        await Assert.That(panned).IsEqualTo((30f, 30f, 30f));
        await Assert.That(asked).IsEqualTo((150f, 150f));
        await Assert.That(follow.Lead.Offset.X).IsEqualTo(500f);
        await Assert.That(follow.Header.Offset.X).IsEqualTo(500f);
    }

    [Test]
    public async Task Bringing_a_node_into_view_scrolls_the_least_that_shows_it_and_replaces_a_glide()
    {
        var area = new AreaFixture { ContentHeight = 2000f, WithMarks = true };
        using var driver = area.Render();
        var offsets = new List<float>();

        void Reveal(string id)
        {
            area.Mark(id).ScrollIntoView();
            driver.Settle();
            offsets.Add(area.Handle!.Offset.Y);
        }

        Reveal("shown");
        Reveal("below");
        Reveal("above");
        Reveal("tall");
        driver.Wheel(driver.Get("content"), new Vector2(0f, -1f), settle: false);
        driver.Frame();
        area.Mark("far").ScrollIntoView();
        driver.Settle();

        await Assert.That(offsets).IsEquivalentTo([0f, 140f, 100f, 400f]);
        await Assert.That(area.Handle!.Offset.Y).IsEqualTo(940f);
    }

    [Test]
    public async Task Code_scrolls_clamped_at_once_stopping_a_glide_or_a_coast_and_pages_by_the_viewport()
    {
        var area = new AreaFixture();
        using var driver = area.Render();
        var handle = area.Handle!;
        var offsets = new List<float>();

        void Then()
        {
            driver.Settle();
            offsets.Add(handle.Offset.Y);
        }

        handle.ScrollTo(null, -50f);
        Then();
        handle.ScrollTo(null, 5000f);
        Then();
        handle.PageBy(-1);
        driver.Frame();
        offsets.Add(handle.Offset.Y);
        driver.Wheel(driver.Get("content"), new Vector2(0f, 1f), settle: false);
        driver.Frame();
        handle.ScrollTo(null, 300f);
        Then();
        driver.Fling(driver.Get("content"), new Vector2(0f, -60f), settle: false);
        driver.Frame();
        handle.ScrollTo(null, 300f);
        Then();
        area.ContentHeight = 50f;
        driver.Rerender();
        offsets.Add(handle.Offset.Y);

        await Assert.That(offsets).IsEquivalentTo([0f, 900f, 800f, 300f, 300f, 0f]);
    }

    [Test]
    public async Task An_offset_asked_before_the_first_layout_is_kept_as_another_area_settles()
    {
        var pair = new ScrollState();
        using var driver = UiDriver.Render(ui =>
        {
            var first = ui.ScrollHandle();
            var second = ui.ScrollHandle();
            if (pair.Outer is null)
                first.ScrollTo(null, 300f);
            pair.Outer = first;
            pair.Inner = second;
            using (ui.Box(gap: 8))
            {
                using (
                    ui.ScrollArea(
                        scroll: first,
                        onScrollChange: Callback<ScrollChange>.Of(
                            second,
                            static (s, change) => s.ScrollTo(null, change.Offset.Y)
                        ),
                        width: 100,
                        height: 100
                    )
                )
                using (ui.Box(height: 1000)) { }
                using (ui.ScrollArea(scroll: second, width: 100, height: 100))
                using (ui.Box(height: 1000)) { }
            }
        });

        await Assert.That(pair.Outer!.Offset.Y).IsEqualTo(300f);
        await Assert.That(pair.Inner!.Offset.Y).IsEqualTo(300f);
    }

    [Test]
    public async Task It_reports_each_layout_that_changed_its_offset_extent_or_viewport_with_the_changes_and_no_other()
    {
        var area = new AreaFixture { Reports = [] };
        using var driver = area.Render();
        area.Reports.Clear();

        area.ContentHeight = 1200f;
        driver.Rerender();
        area.Width = 150f;
        driver.Rerender();
        driver.Rerender();

        await Assert.That(area.Reports.Count).IsEqualTo(2);
        await Assert.That(area.Reports[0].ExtentDelta).IsEqualTo(new Size(0f, 200f));
        await Assert.That(area.Reports[1].ViewportDelta).IsEqualTo(new Size(-50f, 0f));
    }
}

/// <summary>A 200 × 100 <see cref="ScrollArea"/> over content of a set size, beside a focusable box.</summary>
internal sealed class AreaFixture
{
    public ScrollHandle? Handle;
    public float ContentWidth;
    public float ContentHeight = 1000f;
    public float Width = 200f;
    public ScrollbarVisibility Horizontal;
    public ScrollbarVisibility Vertical;
    public bool DragToPan;
    public Thickness Padding;
    public float Edge;
    public Cursor? Cursor;
    public string? Text;
    public bool WithThumb;
    public bool WithMarks;
    public int Clicks;
    public Vector2 Dragged;
    public List<ScrollChange>? Reports;

    public UiDriver Render() =>
        UiDriver.Render(
            ui =>
            {
                Handle = ui.ScrollHandle();
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
                {
                    using (ui.Box(width: 40, height: 20, focusable: true, id: "outside")) { }
                    Area(ui);
                }
            },
            new Size(400, 300)
        );

    public readonly NodeRef?[] Marks = new NodeRef?[MarkIds.Length];

    static readonly string[] MarkIds = ["shown", "below", "above", "tall", "far"];
    static readonly float[] MarkTops = [40f, 200f, 100f, 400f, 1000f];
    static readonly float[] MarkHeights = [20f, 40f, 20f, 300f, 40f];

    public NodeRef Mark(string id) => Marks[Array.IndexOf(MarkIds, id)]!;

    [FramePath]
    void Area(Ui ui, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        using var fragment = ui.Fragment(file: file, line: line);
        using (
            ui.ScrollArea(
                scroll: Handle,
                horizontal: Horizontal,
                vertical: Vertical,
                padding: Padding,
                border: new Border(Edge, Color.FromRgb(1, 2, 3)),
                dragToPan: DragToPan,
                onScrollChange: Reports is null
                    ? default
                    : Callback<ScrollChange>.Of(Reports, static (r, change) => r.Add(change)),
                cursor: Cursor,
                width: Width,
                height: 100,
                id: "area"
            )
        )
        using (
            ui.Box(
                direction: Direction.Column,
                align: Align.Start,
                width: ContentWidth > 0f ? ContentWidth : Length.Auto,
                height: ContentHeight > 0f ? ContentHeight : Length.Auto,
                shrink: 0f,
                id: "content"
            )
        )
        {
            if (Text is { } text)
                ui.Text(text, wrap: true);
            using (
                ui.Box(
                    width: 60,
                    height: 30,
                    onClick: Callback.Of(this, static a => a.Clicks++),
                    id: "button"
                )
            ) { }
            if (WithThumb)
                using (
                    ui.Thumb(
                        onDragDelta: Callback<Vector2>.Of(this, static (a, by) => a.Dragged = by),
                        width: 30,
                        height: 30,
                        id: "thumb"
                    )
                ) { }
            if (WithMarks)
                for (var i = 0; i < MarkIds.Length; i++)
                {
                    Marks[i] = ui.Ref(key: i);
                    using (
                        ui.Box(
                            width: 10,
                            height: MarkHeights[i],
                            position: Position.Absolute,
                            top: MarkTops[i],
                            nodeRef: Marks[i],
                            id: MarkIds[i],
                            key: i
                        )
                    ) { }
                }
        }
    }
}

/// <summary>A board area with a header and a footer following its horizontal scroll.</summary>
internal sealed class FollowFixture
{
    public ScrollHandle? Lead;
    public ScrollHandle? Header;
    public ScrollHandle? Footer;

    public UiDriver Render() =>
        UiDriver.Render(
            ui =>
            {
                Lead = ui.ScrollHandle();
                Header = ui.ScrollHandle();
                Footer = ui.ScrollHandle();
                using (ui.Box(direction: Direction.Column, align: Align.Start))
                {
                    using (
                        ui.ScrollArea(
                            scroll: Header,
                            horizontal: ScrollbarVisibility.Hidden,
                            vertical: ScrollbarVisibility.Disabled,
                            followScroll: Lead,
                            width: 200,
                            height: 20
                        )
                    )
                    using (ui.Box(width: 600, height: 20)) { }
                    using (
                        ui.ScrollArea(
                            scroll: Lead,
                            horizontal: ScrollbarVisibility.Hidden,
                            vertical: ScrollbarVisibility.Disabled,
                            dragToPan: true,
                            width: 200,
                            height: 100
                        )
                    )
                    using (ui.Box(width: 700, height: 100, id: "board")) { }
                    using (
                        ui.ScrollArea(
                            scroll: Footer,
                            horizontal: ScrollbarVisibility.Hidden,
                            vertical: ScrollbarVisibility.Disabled,
                            followScroll: Lead,
                            width: 200,
                            height: 20
                        )
                    )
                    using (ui.Box(width: 800, height: 20)) { }
                }
            },
            new Size(400, 300)
        );
}

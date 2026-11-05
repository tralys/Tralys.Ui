using System.Numerics;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class ScrollTests
{
    static readonly Size View = new(200, 200);

    static UiDriver Nested(ScrollState state, Panning inner = default, PressMode rows = default) =>
        UiDriver.Render(
            ui =>
            {
                var outer = ui.ScrollHandle();
                var innerScroll = ui.ScrollHandle();
                state.Outer = outer;
                state.Inner = innerScroll;
                using (
                    ui.Box(
                        direction: Direction.Column,
                        overflow: Overflow.ScrollY,
                        scroll: outer,
                        height: 200,
                        id: "outer"
                    )
                )
                {
                    using (
                        ui.Box(
                            direction: Direction.Column,
                            overflow: Overflow.ScrollY,
                            scroll: innerScroll,
                            panning: inner,
                            height: 200,
                            shrink: 0,
                            id: "inner"
                        )
                    )
                    {
                        for (var i = 0; i < 6; i++)
                        {
                            using (
                                ui.Box(
                                    height: 50,
                                    onClick: Callback.Of(
                                        state.Clicks,
                                        Boxed.Of(i),
                                        static (c, b) => c.Add(b.Value)
                                    ),
                                    pressMode: rows,
                                    id: "item" + i,
                                    key: i
                                )
                            ) { }
                        }
                    }
                    using (ui.Box(height: 200, shrink: 0, id: "after")) { }
                }
            },
            View
        );

    [Test]
    public async Task A_wheel_turn_scrolls_the_innermost_container_that_can_still_move_that_way()
    {
        var state = new ScrollState();
        using var driver = Nested(state);

        driver.Wheel(driver.Get("item0"), new Vector2(0, -1));

        await Assert.That(state.Inner!.Offset).IsEqualTo(new Vector2(0, 48));
        await Assert.That(state.Outer!.Offset).IsEqualTo(Vector2.Zero);
        await Assert.That(driver.Get("item1").ViewBounds.Y).IsEqualTo(2f);

        driver.Click(new Vector2(100, 10));
        await Assert.That(state.Clicks).IsEquivalentTo([1]);

        driver.Wheel(driver.Get("item3"), new Vector2(0, -10));
        driver.Wheel(driver.Get("item5"), new Vector2(0, -1));

        await Assert.That(state.Inner.Offset).IsEqualTo(new Vector2(0, 100));
        await Assert.That(state.Outer.Offset).IsEqualTo(new Vector2(0, 48));

        driver.Wheel(driver.Get("item5"), new Vector2(0, 1));

        await Assert.That(state.Inner.Offset).IsEqualTo(new Vector2(0, 52));
    }

    [Test]
    public async Task A_wheel_turn_goes_to_the_innermost_container_that_can_move_along_its_larger_axis()
    {
        var state = new ScrollState();
        using var driver = UiDriver.Render(
            ui =>
            {
                var outer = ui.ScrollHandle();
                var inner = ui.ScrollHandle();
                state.Outer = outer;
                state.Inner = inner;
                using (ui.Box(overflow: Overflow.ScrollX, scroll: outer, width: 200, height: 200))
                using (
                    ui.Box(
                        direction: Direction.Column,
                        overflow: Overflow.ScrollY,
                        scroll: inner,
                        width: 300,
                        height: 200,
                        shrink: 0,
                        id: "inner"
                    )
                )
                using (ui.Box(height: 600, shrink: 0)) { }
            },
            View
        );

        driver.Wheel(driver.Get("inner"), new Vector2(2, -0.5f));
        var sideways = (state.Outer!.Offset, state.Inner!.Offset);
        driver.Wheel(driver.Get("inner"), new Vector2(0.25f, -2));

        await Assert.That(sideways).IsEqualTo((new Vector2(96, 0), Vector2.Zero));
        await Assert
            .That((state.Outer.Offset, state.Inner.Offset))
            .IsEqualTo((new Vector2(96, 0), new Vector2(0, 96)));
    }

    [Test]
    public async Task A_container_handed_no_handle_after_one_lets_it_go_and_scrolls_by_its_own()
    {
        var model = new Model { Remember = true };
        var state = new ScrollState();
        using var driver = UiDriver.Render(
            ui =>
            {
                var given = ui.ScrollHandle();
                state.Outer = given;
                using (
                    ui.Box(
                        direction: Direction.Column,
                        overflow: Overflow.ScrollY,
                        scroll: model.Remember ? given : null,
                        height: 100,
                        id: "box"
                    )
                )
                using (ui.Box(height: 400, shrink: 0)) { }
            },
            View
        );

        model.Remember = false;
        driver.Settle();
        state.Outer!.ScrollTo(null, 50);
        driver.Settle();

        await Assert.That(state.Outer.Offset.Y).IsEqualTo(0f);
        await Assert.That(driver.Get("box").Scroller).IsNotSameReferenceAs(state.Outer);
    }

    [Test]
    public async Task Wheel_steps_glide_and_a_step_mid_glide_adds_to_where_it_lands()
    {
        var state = new ScrollState();
        using var driver = Nested(state);
        var item = driver.Get("item0");

        driver.Wheel(item, new Vector2(0, -1), settle: false);
        driver.Frame();
        var midway = state.Inner!.Offset.Y;
        driver.Wheel(item, new Vector2(0, -1), settle: false);
        driver.Settle();

        await Assert.That(midway).IsGreaterThan(0f).And.IsLessThan(48f);
        await Assert.That(state.Inner.Offset.Y).IsEqualTo(96f);
    }

    [Test]
    public async Task A_request_applies_clamped_at_the_next_layout_and_reports_once()
    {
        var changes = new List<ScrollChange>();
        var state = new ScrollState();
        using var driver = UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                state.Outer = scroll;
                using (
                    ui.Box(
                        direction: Direction.Column,
                        overflow: Overflow.ScrollY,
                        scroll: scroll,
                        onScrollChange: Callback<ScrollChange>.Of(
                            changes,
                            static (c, change) => c.Add(change)
                        ),
                        height: 100
                    )
                )
                using (ui.Box(height: 300)) { }
            },
            View
        );
        changes.Clear();

        state.Outer!.ScrollTo(null, 1000);
        driver.Settle();

        await Assert.That(changes.Count).IsEqualTo(1);
        await Assert.That(changes[0].Offset).IsEqualTo(new Vector2(0, 200));
        await Assert.That(changes[0].OffsetDelta).IsEqualTo(new Vector2(0, 200));
        await Assert.That(changes[0].Extent).IsEqualTo(new Size(200, 300));
        await Assert.That(changes[0].Viewport).IsEqualTo(new Size(200, 100));
    }

    [Test]
    public async Task Scrolled_to_its_end_a_container_stays_there_as_its_content_grows()
    {
        var model = new Model { Count = 300 };
        var state = new ScrollState();
        using var driver = UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                state.Outer = scroll;
                using (ui.Box(overflow: Overflow.ScrollY, scroll: scroll, height: 100))
                    ui.CountBox(model: model, shrink: 0);
            },
            View
        );

        state.Outer!.ScrollToEnd();
        driver.Settle();
        model.Count = 500;
        driver.Settle();

        await Assert.That(state.Outer.Offset).IsEqualTo(new Vector2(0, 400));
    }

    [Test]
    public async Task A_touch_pan_follows_the_whole_travel_and_its_press_clicks_nothing()
    {
        var state = new ScrollState();
        using var driver = Nested(state);

        driver.Drag(driver.Get("item1"), new Vector2(0, -30), PointerKind.Touch);

        await Assert.That(state.Inner!.Offset).IsEqualTo(new Vector2(0, 30));
        await Assert.That(state.Clicks).IsEmpty();

        driver.Drag(driver.Get("item2"), new Vector2(0, -4), PointerKind.Touch);

        await Assert.That(state.Inner.Offset).IsEqualTo(new Vector2(0, 30));
        await Assert.That(state.Clicks).IsEquivalentTo([2]);
    }

    [Test]
    public async Task A_lift_ending_a_pan_clicks_no_row_that_clicks_on_any_release_over_it()
    {
        var state = new ScrollState();
        using var driver = Nested(state, rows: PressMode.ReleaseOver);

        driver.Drag(driver.Get("item1"), new Vector2(0, -30), PointerKind.Touch);

        await Assert.That(state.Inner!.Offset).IsEqualTo(new Vector2(0, 30));
        await Assert.That(state.Clicks).IsEmpty();

        driver.Drag(driver.Get("item2"), new Vector2(0, -4), PointerKind.Touch);

        await Assert.That(state.Clicks).IsEquivalentTo([2]);
    }

    [Test]
    public async Task A_pan_chains_to_the_container_around_it_at_its_edge()
    {
        var state = new ScrollState();
        using var driver = Nested(state);

        driver.Drag(driver.Get("item3"), new Vector2(0, -150), PointerKind.Touch);

        await Assert.That(state.Inner!.Offset).IsEqualTo(new Vector2(0, 100));
        await Assert.That(state.Outer!.Offset).IsEqualTo(new Vector2(0, 50));
    }

    [Test]
    public async Task A_container_whose_panning_refuses_the_move_leaves_it_to_the_one_around_it()
    {
        var state = new ScrollState();
        using var driver = Nested(state, inner: Panning.None);

        driver.Drag(driver.Get("item1"), new Vector2(0, -60), PointerKind.Touch);

        await Assert.That(state.Inner!.Offset).IsEqualTo(Vector2.Zero);
        await Assert.That(state.Outer!.Offset).IsEqualTo(new Vector2(0, 60));
    }

    [Test]
    public async Task A_pointer_a_node_captured_itself_is_never_panned()
    {
        var state = new ScrollState();
        using var driver = UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                state.Outer = scroll;
                using (
                    ui.Box(
                        direction: Direction.Column,
                        overflow: Overflow.ScrollY,
                        scroll: scroll,
                        height: 100
                    )
                )
                {
                    using (
                        ui.Box(
                            height: 300,
                            onPointerDown: Handler<PointerEvent>.Of(
                                static (ref PointerEvent e) => e.Capture()
                            ),
                            id: "grip"
                        )
                    ) { }
                }
            },
            View
        );

        driver.Drag(driver.Get("grip"), new Vector2(0, -60), PointerKind.Touch);

        await Assert.That(state.Outer!.Offset).IsEqualTo(Vector2.Zero);
    }

    [Test]
    public async Task A_mouse_drags_to_pan_a_drag_to_pan_container_but_not_from_a_pressable()
    {
        var clicks = new List<int>();
        var state = new ScrollState();
        using var driver = UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                state.Outer = scroll;
                using (
                    ui.Box(
                        direction: Direction.Column,
                        overflow: Overflow.ScrollY,
                        scroll: scroll,
                        dragToPan: true,
                        height: 100,
                        background: Color.FromRgb(1, 2, 3),
                        id: "pane"
                    )
                )
                {
                    using (
                        ui.Box(
                            height: 40,
                            shrink: 0,
                            onClick: Callback.Of(clicks, static c => c.Add(1)),
                            id: "button"
                        )
                    ) { }
                    using (ui.Box(height: 300, shrink: 0)) { }
                }
            },
            View
        );

        driver.Drag(driver.Get("button"), new Vector2(0, -30));

        await Assert.That(state.Outer!.Offset).IsEqualTo(Vector2.Zero);

        driver.Drag(driver.Get("pane"), new Vector2(0, -30));

        await Assert.That(state.Outer.Offset).IsEqualTo(new Vector2(0, 30));
    }

    [Test]
    public async Task Bringing_a_node_into_view_scrolls_every_scroll_container_around_it()
    {
        var state = new ScrollState();
        var target = new RefHolder();
        using var driver = UiDriver.Render(
            ui =>
            {
                var outer = ui.ScrollHandle();
                var inner = ui.ScrollHandle();
                target.Ref = ui.Ref();
                state.Outer = outer;
                state.Inner = inner;
                using (
                    ui.Box(
                        direction: Direction.Column,
                        overflow: Overflow.ScrollY,
                        scroll: outer,
                        height: 100
                    )
                )
                {
                    using (ui.Box(height: 150, shrink: 0)) { }
                    using (
                        ui.Box(
                            direction: Direction.Column,
                            overflow: Overflow.ScrollY,
                            scroll: inner,
                            height: 80,
                            shrink: 0
                        )
                    )
                    {
                        using (ui.Box(height: 200, shrink: 0)) { }
                        using (ui.Box(height: 30, shrink: 0, nodeRef: target.Ref, id: "target")) { }
                    }
                }
            },
            View
        );

        target.Ref!.ScrollIntoView();
        driver.Settle();

        await Assert.That(state.Inner!.Offset).IsEqualTo(new Vector2(0, 150));
        await Assert.That(state.Outer!.Offset).IsEqualTo(new Vector2(0, 130));
        await Assert.That(driver.Get("target").ViewBounds).IsEqualTo(new Rect(0, 70, 200, 30));
    }

    [Test]
    public async Task Bringing_a_scaled_node_into_view_scrolls_to_where_it_draws()
    {
        var state = new ScrollState();
        var target = new RefHolder();
        using var driver = UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                target.Ref = ui.Ref();
                state.Outer = scroll;
                using (
                    ui.Box(
                        direction: Direction.Column,
                        overflow: Overflow.ScrollY,
                        scroll: scroll,
                        height: 100
                    )
                )
                using (
                    ui.Box(
                        direction: Direction.Column,
                        height: 200,
                        shrink: 0,
                        transform: Transform.Scale(2),
                        transformOrigin: System.Numerics.Vector2.Zero
                    )
                )
                {
                    using (ui.Box(height: 60, shrink: 0)) { }
                    using (ui.Box(height: 30, shrink: 0, nodeRef: target.Ref)) { }
                }
            },
            View
        );

        target.Ref!.ScrollIntoView();
        driver.Settle();

        await Assert.That(state.Outer!.Offset.Y).IsEqualTo(80f).Within(0.5f);
    }

    [Test]
    public async Task A_follower_holds_its_leaders_horizontal_offset_and_moves_it()
    {
        var state = new ScrollState();
        using var driver = UiDriver.Render(
            ui =>
            {
                var lead = ui.ScrollHandle();
                state.Outer = lead;
                using (ui.Box(direction: Direction.Column))
                {
                    using (
                        ui.Box(
                            overflow: Overflow.ScrollX,
                            followScroll: lead,
                            width: 100,
                            height: 20,
                            id: "header"
                        )
                    )
                    using (ui.Box(width: 400, height: 20)) { }
                    using (ui.Box(overflow: Overflow.ScrollX, scroll: lead, width: 100, height: 50))
                    using (ui.Box(width: 400, height: 50)) { }
                }
            },
            View
        );

        state.Outer!.ScrollTo(120, null);
        driver.Settle();

        await Assert.That(driver.Get("header").Scroller!.Offset.X).IsEqualTo(120f);

        driver.Wheel(driver.Get("header"), new Vector2(1, 0));

        await Assert.That(state.Outer.Offset.X).IsEqualTo(168f);
    }

    [Test]
    public async Task Keyboard_focus_scrolls_into_view_and_pointer_focus_does_not()
    {
        var state = new ScrollState();
        using var driver = UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                state.Outer = scroll;
                using (
                    ui.Box(
                        direction: Direction.Column,
                        overflow: Overflow.ScrollY,
                        scroll: scroll,
                        height: 100
                    )
                )
                {
                    for (var i = 0; i < 10; i++)
                    {
                        using (ui.Box(height: 40, shrink: 0, focusable: true, id: "b" + i, key: i))
                        { }
                    }
                }
            },
            View
        );

        for (var i = 0; i < 4; i++)
            driver.Tab();

        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("b3"));
        await Assert.That(state.Outer!.Offset).IsEqualTo(new Vector2(0, 60));

        driver.Click(driver.Get("b1"));

        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("b1"));
        await Assert.That(state.Outer.Offset).IsEqualTo(new Vector2(0, 60));
    }

    [Test]
    public async Task A_popup_anchored_in_a_scroll_area_follows_it_and_leaves_when_scrolled_out()
    {
        var anchor = new RefHolder();
        var dismissals = new Counter();
        using var driver = UiDriver.Render(
            ui =>
            {
                anchor.Ref = ui.Ref();
                using (ui.Box(direction: Direction.Column))
                {
                    using (
                        ui.Box(
                            direction: Direction.Column,
                            overflow: Overflow.ScrollY,
                            height: 100,
                            id: "pane"
                        )
                    )
                    {
                        using (ui.Box(height: 40, shrink: 0)) { }
                        using (ui.Box(height: 20, shrink: 0, nodeRef: anchor.Ref)) { }
                        using (ui.Box(height: 300, shrink: 0)) { }
                    }
                    using (
                        ui.Positioner(
                            anchor: anchor.Ref,
                            placement: PlacementMode.BottomStart,
                            dismissOn: Dismiss.AnchorHidden,
                            onDismiss: Callback.Of(dismissals, static c => c.Value++),
                            id: "popup"
                        )
                    )
                    {
                        using (ui.Box(width: 30, height: 30)) { }
                    }
                }
            },
            View
        );
        var before = driver.Get("popup").ViewBounds.Y;

        driver.Wheel(driver.Get("pane"), new Vector2(0, -0.5f));

        await Assert.That(driver.Get("popup").ViewBounds.Y).IsEqualTo(before - 24f);
        await Assert.That(dismissals.Value).IsEqualTo(0);

        driver.Wheel(driver.Get("pane"), new Vector2(0, -2));

        await Assert.That(dismissals.Value).IsEqualTo(1);
    }

    [Test]
    public async Task A_half_pixel_node_scrolled_above_the_origin_moves_by_the_offset_and_keeps_its_size()
    {
        var state = new ScrollState();
        using var driver = UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                state.Outer = scroll;
                using (
                    ui.Box(
                        direction: Direction.Column,
                        overflow: Overflow.ScrollY,
                        scroll: scroll,
                        height: 100
                    )
                )
                {
                    using (ui.Box(height: 10.5f, shrink: 0)) { }
                    using (ui.Box(height: 20, shrink: 0, id: "row")) { }
                    using (ui.Box(height: 200, shrink: 0)) { }
                }
            },
            View
        );
        var resting = driver.Get("row").ViewBounds;

        state.Outer!.ScrollTo(null, 20);
        driver.Settle();

        await Assert
            .That(driver.Get("row").ViewBounds)
            .IsEqualTo(resting with { Y = resting.Y - 20 });
    }

    [Test]
    public async Task A_started_drag_keeps_its_finger_from_a_pan()
    {
        var moves = new Counter();
        var state = new ScrollState();
        using var driver = UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                state.Outer = scroll;
                using (
                    ui.Box(
                        direction: Direction.Column,
                        overflow: Overflow.ScrollY,
                        scroll: scroll,
                        height: 100
                    )
                )
                {
                    using (
                        ui.Box(
                            height: 300,
                            onDrag: Handler<DragEvent>.Of(
                                moves,
                                static (Counter m, ref DragEvent e) => m.Value++
                            ),
                            id: "grip"
                        )
                    ) { }
                }
            },
            View
        );

        driver.Drag(driver.Get("grip"), new Vector2(0, -60), PointerKind.Touch);

        await Assert.That(state.Outer!.Offset).IsEqualTo(Vector2.Zero);
        await Assert.That(moves.Value).IsGreaterThan(0);
    }
}

internal sealed class ScrollState
{
    public ScrollHandle? Outer;
    public ScrollHandle? Inner;
    public readonly List<int> Clicks = [];
}

internal sealed class RefHolder
{
    public NodeRef? Ref;
}

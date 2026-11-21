using System.Numerics;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class ScrollGestureTests
{
    static readonly Size View = new(200, 200);

    static UiDriver Pane(ScrollState state) =>
        UiDriver.Render(
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
                using (ui.Box(height: 600, shrink: 0)) { }
            },
            View
        );

    static UiDriver Feed(ScrollState state) =>
        UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                state.Outer = scroll;
                using (
                    ui.Box(
                        direction: Direction.Column,
                        overflow: Overflow.ScrollY,
                        scroll: scroll,
                        height: 200
                    )
                )
                {
                    for (var i = 0; i < 40; i++)
                    {
                        using (
                            ui.Box(
                                height: 50,
                                shrink: 0,
                                onClick: Callback.Of(
                                    state.Clicks,
                                    Boxed.Of(i),
                                    static (c, b) => c.Add(b.Value)
                                ),
                                id: "row" + i,
                                key: i
                            )
                        ) { }
                    }
                }
            },
            View
        );

    [Test]
    public async Task A_pan_ends_at_a_move_whose_buttons_lost_its_release()
    {
        var state = new ScrollState();
        using var driver = Pane(state);

        var mouse = driver.Hold(driver.Get("pane"));
        mouse.MoveBy(new Vector2(0, -30));
        mouse.LoseRelease();
        driver.Hover(driver.Get("pane"));

        var scroll = await Assert.That(state.Outer).IsNotNull();
        await Assert.That(scroll.Offset).IsEqualTo(new Vector2(0, 30));
    }

    [Test]
    public async Task A_pan_ends_when_the_window_loses_the_focus()
    {
        var state = new ScrollState();
        using var driver = Pane(state);

        var mouse = driver.Hold(driver.Get("pane"));
        mouse.MoveBy(new Vector2(0, -30));
        driver.WindowFocus(false);
        driver.WindowFocus(true);
        mouse.MoveBy(new Vector2(0, -30));

        var scroll = await Assert.That(state.Outer).IsNotNull();
        await Assert.That(scroll.Offset).IsEqualTo(new Vector2(0, 30));
    }

    [Test]
    public async Task A_fling_coasts_on_past_the_fingers_travel()
    {
        var state = new ScrollState();
        using var driver = Feed(state);

        driver.Fling(driver.Get("row2"), new Vector2(0, -60));

        var scroll = await Assert.That(state.Outer).IsNotNull();
        await Assert.That(scroll.Offset.Y).IsGreaterThan(60f);
    }

    [Test]
    public async Task A_flick_after_holding_still_coasts_on_from_its_own_speed()
    {
        var state = new ScrollState();
        using var driver = Feed(state);

        var finger = driver.Hold(driver.Get("row2"), PointerKind.Touch, settle: false);
        driver.Frame(2f);
        finger.MoveBy(new Vector2(0, -4), settle: false);
        finger.MoveBy(new Vector2(0, -56), seconds: 0.05f, settle: false);
        finger.Release();

        var scroll = await Assert.That(state.Outer).IsNotNull();
        await Assert.That(scroll.Offset.Y).IsGreaterThan(60f);
    }

    [Test]
    public async Task A_press_stops_a_coast_and_goes_no_further()
    {
        var state = new ScrollState();
        using var driver = Feed(state);
        var scroll = await Assert.That(state.Outer).IsNotNull();

        driver.Fling(driver.Get("row2"), new Vector2(0, -60), settle: false);
        driver.Frame();
        var coasting = scroll.Offset.Y;
        driver.Tap(driver.Get("row4"));

        await Assert.That(coasting).IsGreaterThan(60f);
        await Assert.That(scroll.Offset.Y).IsEqualTo(coasting);
        await Assert.That(state.Clicks).IsEmpty();
    }

    [Test]
    public async Task A_drag_near_a_scroll_edge_scrolls_until_it_moves_away()
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
                        height: 200
                    )
                )
                {
                    using (ui.Box(height: 40, shrink: 0, dragData: "payload", id: "source")) { }
                    using (ui.Box(height: 800, shrink: 0)) { }
                }
            },
            View
        );

        var scroll = await Assert.That(state.Outer).IsNotNull();
        var mouse = driver.Hold(driver.Get("source"));
        mouse.MoveBy(new Vector2(0, 176), settle: false);
        driver.Frame(0.1f);
        var scrolled = scroll.Offset.Y;
        mouse.MoveBy(new Vector2(0, -96), settle: false);
        driver.Frame(0.1f);
        driver.Frame(0.1f);

        await Assert.That(scrolled).IsGreaterThan(0f);
        await Assert.That(scroll.Offset.Y).IsEqualTo(scrolled);
    }

    [Test]
    public async Task A_wheel_turn_is_taken_while_a_layer_dismissed_by_outside_presses_is_open()
    {
        var open = new Switches { A = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (ui.Box(width: 100, height: 100, id: "scene")) { }
                if (open.A)
                {
                    using (
                        ui.Layer(
                            dismissOn: Dismiss.OutsidePress,
                            onDismiss: Callback.Of(open, static s => s.A = false),
                            justify: Justify.End,
                            align: Align.End
                        )
                    )
                    using (ui.Box(width: 40, height: 40, background: Color.FromRgb(4, 5, 6))) { }
                }
            }
        });

        var whileOpen = driver.Wheel(driver.Get("scene"), new Vector2(0, -1));
        open.A = false;
        driver.Settle();
        var closed = driver.Wheel(driver.Get("scene"), new Vector2(0, -1));

        await Assert.That(whileOpen).IsTrue();
        await Assert.That(closed).IsFalse();
    }

    [Test]
    public async Task A_wheel_turn_over_ui_that_nothing_scrolls_is_taken_and_over_the_roots_own_paint_alone_is_not()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, background: Color.FromRgb(1, 2, 3), id: "root"))
            using (ui.Box(width: 100, height: 100, background: Color.FromRgb(4, 5, 6), id: "panel"))
            { }
        });

        var overPanel = driver.Wheel(driver.Get("panel"), new Vector2(0, -1));
        var overRoot = driver.Wheel(driver.Get("root"), new Vector2(0, -1));

        await Assert.That((overPanel, overRoot)).IsEqualTo((true, false));
    }
}

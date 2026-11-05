using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class PointerGestureTests
{
    [Test]
    public async Task A_move_without_the_pressed_button_ends_the_press_without_a_click()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    width: 40,
                    height: 40,
                    onClick: Callback.Of(log, static l => l.Add("click")),
                    onPointerCancel: Handler<PointerEvent>.Of(
                        log,
                        static (Log l, ref PointerEvent e) => l.Add("cancel")
                    ),
                    id: "button"
                )
            ) { }
        });
        var view = driver.View;
        var at = new Vector2(20, 20);

        view.PointerMove(Pointer.Mouse, at, MouseButtons.None, false, default, default);
        view.PointerDown(Pointer.Mouse, at, MouseButton.Left, default, default);
        view.PointerMove(Pointer.Mouse, at, MouseButtons.None, false, default, default);
        view.PointerUp(Pointer.Mouse, at, MouseButton.Left, default, default);
        driver.Settle();

        await Assert.That(log.ToString()).IsEqualTo("cancel");
        await Assert
            .That(driver.Get("button").States & InputStates.Capturing)
            .IsEqualTo(InputStates.None);
    }

    [Test]
    public async Task A_held_touch_runs_its_long_press_else_its_context_menu_and_never_clicks()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start))
            {
                using (
                    ui.Box(
                        width: 40,
                        height: 40,
                        onClick: Callback.Of(log, static l => l.Add("click")),
                        onLongPress: Callback.Of(log, static l => l.Add("long")),
                        id: "long"
                    )
                ) { }
                using (
                    ui.Box(
                        width: 40,
                        height: 40,
                        onClick: Callback.Of(log, static l => l.Add("click")),
                        onContextMenu: Handler<PointerEvent>.Of(
                            log,
                            static (Log l, ref PointerEvent e) => l.Add("menu")
                        ),
                        id: "menu"
                    )
                ) { }
            }
        });

        driver.LongPress(driver.Get("long"));
        driver.LongPress(driver.Get("menu"));

        await Assert.That(log.ToString()).IsEqualTo("long menu");
    }

    [Test]
    public async Task A_right_release_asks_the_node_under_it_for_a_context_menu()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    align: Align.Start,
                    onContextMenu: Handler<PointerEvent>.Of(
                        log,
                        static (Log l, ref PointerEvent e) => l.Add("outer")
                    )
                )
            )
            {
                using (
                    ui.Box(
                        width: 40,
                        height: 40,
                        onContextMenu: Handler<PointerEvent>.Of(
                            log,
                            static (Log l, ref PointerEvent e) =>
                            {
                                l.Add("inner");
                                e.StopPropagation();
                            }
                        ),
                        id: "inner"
                    )
                ) { }
            }
        });

        driver.Click(driver.Get("inner"), MouseButton.Right);
        driver.Click(driver.Get("inner"));

        await Assert.That(log.ToString()).IsEqualTo("inner");
    }

    [Test]
    public async Task A_double_click_belongs_to_the_innermost_pressable_it_lands_on()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    direction: Direction.Row,
                    width: 200,
                    height: 40,
                    onDoubleClick: Callback.Of(log, static l => l.Add("row")),
                    id: "row"
                )
            )
            {
                using (
                    ui.Box(
                        width: 40,
                        height: 40,
                        onClick: Callback.Of(log, static l => l.Add("button")),
                        id: "button"
                    )
                ) { }
            }
        });

        driver.DoubleClick(driver.Get("row"));
        driver.DoubleClick(driver.Get("button"));

        await Assert.That(log.ToString()).IsEqualTo("row button button");
    }

    [Test]
    public async Task Two_quick_taps_close_together_make_a_double_tap()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    width: 40,
                    height: 40,
                    onDoubleClick: Callback.Of(log, static l => l.Add("double")),
                    id: "target"
                )
            ) { }
        });

        driver.Tap(driver.Get("target"));
        driver.Tap(driver.Get("target"));

        await Assert.That(log.ToString()).IsEqualTo("double");
    }

    [Test]
    public async Task A_drag_counts_from_the_press_and_takes_the_pointer_from_the_button_it_started_on()
    {
        var drags = new DragLog();
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    align: Align.Start,
                    width: 200,
                    height: 100,
                    onDrag: Handler<DragEvent>.Of(
                        drags,
                        static (DragLog d, ref DragEvent e) => d.Items.Add((e.Phase, e.Offset))
                    )
                )
            )
            {
                using (
                    ui.Box(
                        width: 40,
                        height: 40,
                        onClick: Callback.Of(log, static l => l.Add("click")),
                        onPointerCancel: Handler<PointerEvent>.Of(
                            log,
                            static (Log l, ref PointerEvent e) => l.Add("cancel")
                        ),
                        id: "button"
                    )
                ) { }
            }
        });

        driver.Drag(driver.Get("button"), new Vector2(40, 0));

        await Assert
            .That(drags.ToString())
            .IsEqualTo("Start:10 Move:10 Move:20 Move:30 Move:40 End:40");
        await Assert.That(log.ToString()).IsEqualTo("cancel");
    }

    [Test]
    public async Task A_lift_ending_a_drag_clicks_no_row_that_clicks_on_any_release_over_it()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (
                    ui.Box(
                        width: 40,
                        height: 40,
                        onDrag: Handler<DragEvent>.Of(log, static (Log l, ref DragEvent e) => { }),
                        id: "grip"
                    )
                ) { }
                using (
                    ui.Box(
                        width: 40,
                        height: 40,
                        onClick: Callback.Of(log, static l => l.Add("click")),
                        pressMode: PressMode.ReleaseOver
                    )
                ) { }
            }
        });

        driver.Drag(driver.Get("grip"), new Vector2(0, 40));

        await Assert.That(log.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task A_press_or_tap_node_acts_as_a_mouse_lands_and_as_a_finger_lifts_unmoved()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (
                ui.Box(
                    width: 40,
                    height: 40,
                    onClick: Callback.Of(log, static l => l.Add("click")),
                    pressMode: PressMode.PressOrTap,
                    id: "slot"
                )
            ) { }
        });
        var slot = driver.Get("slot");

        var mouse = driver.Hold(slot);
        var mouseLanded = log.Items.Count;
        mouse.Release();
        var mouseLifted = log.Items.Count;
        var finger = driver.Hold(slot, PointerKind.Touch);
        var fingerLanded = log.Items.Count;
        finger.Release();
        var tapped = log.Items.Count;
        var sliding = driver.Hold(slot, PointerKind.Touch);
        sliding.MoveBy(new Vector2(0, 60));
        sliding.Release();

        await Assert.That((mouseLanded, mouseLifted)).IsEqualTo((1, 1));
        await Assert.That((fingerLanded, tapped)).IsEqualTo((1, 2));
        await Assert.That(log.Items.Count).IsEqualTo(2);
    }

    [Test]
    [Arguments(DragLoss.Disabled)]
    [Arguments(DragLoss.Hidden)]
    [Arguments(DragLoss.Removed)]
    public async Task A_node_disabled_hidden_or_removed_mid_drag_gets_a_cancelled_drag_and_nothing_after(
        DragLoss loss
    )
    {
        var drags = new DragLog();
        var lost = new Switches();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    width: 200,
                    height: 200,
                    align: Align.Start,
                    disabled: lost.A && loss == DragLoss.Disabled,
                    hidden: lost.A && loss == DragLoss.Hidden
                )
            )
            {
                if (!lost.A || loss != DragLoss.Removed)
                    ui.Notice(
                        number: 1,
                        exit: new Transition(1f),
                        onDrag: Handler<DragEvent>.Of(
                            drags,
                            static (DragLog d, ref DragEvent e) => d.Items.Add((e.Phase, e.Offset))
                        )
                    );
            }
        });
        var held = driver.Hold(driver.GetText("notice 1"));
        held.MoveBy(new Vector2(10, 0));

        lost.A = true;
        driver.Frame();
        var atOnce = drags.ToString();
        held.MoveBy(new Vector2(10, 0));
        held.Release();

        await Assert.That(atOnce).IsEqualTo("Start:10 Move:10 Cancel:10");
        await Assert.That(drags.ToString()).IsEqualTo(atOnce);
    }

    [Test]
    public async Task A_touch_that_strayed_past_the_slop_never_holds_a_hold_moved_on_is_cancelled_and_one_drifting_within_it_holds_and_leaves_nothing_hovered_or_pressed()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    width: 200,
                    height: 200,
                    onLongPress: Callback.Of(log, static l => l.Add("long")),
                    id: "target"
                )
            ) { }
        });
        var target = driver.Get("target");

        var strayed = driver.Hold(target, PointerKind.Touch);
        strayed.MoveBy(new Vector2(10, 0));
        strayed.MoveBy(new Vector2(-10, 0));
        driver.Frame(2f);
        strayed.Release();
        var moved = driver.Hold(target, PointerKind.Touch);
        driver.Frame(1f);
        moved.MoveBy(new Vector2(60, 0));
        moved.Release();
        var still = driver.Hold(target, PointerKind.Touch);
        still.MoveBy(new Vector2(2, 0));
        driver.Frame(1f);
        still.Release();

        await Assert.That(log.ToString()).IsEqualTo("long");
        await Assert
            .That(driver.Get("target").States & (InputStates.Hovered | InputStates.Pressed))
            .IsEqualTo(InputStates.None);
    }

    [Test]
    public async Task A_touch_taps_only_lifting_within_its_slop_on_what_it_landed_on_and_a_reused_id_starts_afresh()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (
                ui.Box(
                    width: 40,
                    height: 40,
                    margin: new Thickness(100, 0, 0, 0),
                    onClick: Callback.Of(log, static l => l.Add("tap"))
                )
            ) { }
        });
        var view = driver.View;
        var finger = new Pointer(PointerKind.Touch, 1);
        var on = new Vector2(110, 20);
        var beside = new Vector2(40, 20);

        void Touch(Vector2 down, Vector2 up)
        {
            view.PointerDown(finger, down, MouseButton.Left, default, default);
            view.PointerMove(finger, up, MouseButtons.Left, true, default, default);
            view.PointerUp(finger, up, MouseButton.Left, default, default);
            driver.Settle();
        }

        Touch(beside, on);
        Touch(on, beside);
        Touch(beside, on);
        Touch(on, on + new Vector2(6, 0));

        await Assert.That(log.ToString()).IsEqualTo("tap");
    }

    [Test]
    public async Task A_drag_whose_pointer_an_ancestor_captures_is_cancelled_once_and_follows_no_more_moves()
    {
        var drags = new DragLog();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    width: 200,
                    height: 200,
                    align: Align.Start,
                    onPointerDown: Handler<PointerEvent>.Of(
                        static (ref PointerEvent e) =>
                        {
                            if (e.Button == MouseButton.Right)
                                e.Capture();
                        }
                    )
                )
            )
            using (
                ui.Box(
                    width: 40,
                    height: 40,
                    onDrag: Handler<DragEvent>.Of(
                        drags,
                        static (DragLog d, ref DragEvent e) => d.Items.Add((e.Phase, e.Offset))
                    ),
                    id: "grip"
                )
            ) { }
        });
        var held = driver.Hold(driver.Get("grip"));
        held.MoveBy(new Vector2(10, 0));

        driver.View.PointerDown(Pointer.Mouse, held.Position, MouseButton.Right, default, default);
        driver.View.PointerUp(Pointer.Mouse, held.Position, MouseButton.Right, default, default);
        driver.Settle();
        held.MoveBy(new Vector2(10, 0));
        held.Release();

        await Assert.That(drags.ToString()).IsEqualTo("Start:10 Move:10 Cancel:10");
    }

    [Test]
    public async Task A_tap_counts_as_a_second_only_within_half_a_second_and_24_units_of_the_last()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    width: 200,
                    height: 100,
                    onDoubleClick: Callback.Of(log, static l => l.Add("double")),
                    id: "target"
                )
            ) { }
        });
        void TapAt(float x) => driver.Hold(new Vector2(x, 50), PointerKind.Touch).Release();

        TapAt(20);
        TapAt(40);
        driver.Frame(1f);
        TapAt(20);
        driver.Frame(0.6f);
        TapAt(20);
        driver.Frame(1f);
        TapAt(20);
        TapAt(48);

        await Assert.That(log.ToString()).IsEqualTo("double");
    }

    const InputStates Held = InputStates.Pressed | InputStates.Capturing;

    [Test]
    public async Task A_held_press_follows_its_target_anywhere_showing_it_pressed_only_while_over_it_and_hovering_nothing_else()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 20))
            {
                using (ui.Box(padding: 5, id: "outer"))
                using (
                    ui.Box(
                        width: 40,
                        height: 40,
                        onClick: Callback.Of(log, static l => l.Add("click")),
                        onPointerMove: Handler<PointerEvent>.Of(
                            log,
                            static (Log l, ref PointerEvent e) => l.Add("move")
                        ),
                        id: "target"
                    )
                ) { }
                using (
                    ui.Box(width: 40, height: 40, background: Color.FromRgb(5, 5, 5), id: "other")
                ) { }
            }
        });
        var target = driver.Get("target");
        var outer = driver.Get("outer");
        var other = driver.Get("other");

        var held = driver.Hold(target);
        var down = (target.States & Held, outer.States & Held);
        log.Items.Clear();
        held.MoveTo(other);
        var away = (
            target.States & Held,
            outer.States & InputStates.Capturing,
            other.States & InputStates.Hovered,
            log.Items.Contains("move")
        );
        held.MoveTo(target);
        var back = target.States & Held;
        held.Release();

        await Assert.That(down).IsEqualTo((Held, Held));
        await Assert
            .That(away)
            .IsEqualTo((InputStates.Capturing, InputStates.Capturing, InputStates.None, true));
        await Assert.That(back).IsEqualTo(Held);
        await Assert.That((target.States | outer.States) & Held).IsEqualTo(InputStates.None);
        await Assert.That(log.Items.Count(static i => i == "click")).IsEqualTo(1);
    }

    [Test]
    public async Task A_still_pointer_hovers_what_layout_moved_under_it_telling_one_leave_and_one_enter()
    {
        var log = new Log();
        var swapped = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    direction: Direction.Column,
                    align: Align.Start,
                    onHoverChange: Callback<bool>.Of(
                        log,
                        static (l, on) => l.Add(on ? "column+" : "column-")
                    ),
                    id: "column"
                )
            )
            {
                if (swapped.Remember)
                    Hoverable(ui, log, "b", BHover);
                Hoverable(ui, log, "a", AHover);
                if (!swapped.Remember)
                    Hoverable(ui, log, "b", BHover);
            }
        });

        driver.Hover(driver.Get("a"));
        swapped.Remember = true;
        driver.Frame();

        await Assert.That(log.ToString()).IsEqualTo("column+ a+ a- b+");
        await Assert
            .That(driver.Get("b").States & InputStates.Hovered)
            .IsEqualTo(InputStates.Hovered);
        await Assert.That(driver.Get("a").States & InputStates.Hovered).IsEqualTo(InputStates.None);
    }

    [Test]
    public async Task A_hovered_card_removed_by_its_own_hover_hands_the_hover_to_whats_beneath_and_once_that_goes_nothing_stays_hovered()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, id: "root"))
            {
                if (model.Count == 0)
                {
                    using (
                        ui.Box(
                            width: 200,
                            height: 100,
                            align: Align.Start,
                            background: Color.FromRgb(5, 5, 5),
                            id: "page"
                        )
                    )
                    {
                        if (!model.Remember)
                        {
                            using (
                                ui.Box(
                                    width: 100,
                                    height: 50,
                                    align: Align.Start,
                                    onHoverChange: Callback<bool>.Of(
                                        model,
                                        static (m, on) => m.Remember |= on
                                    ),
                                    id: "card"
                                )
                            )
                            using (
                                ui.Box(
                                    width: 40,
                                    height: 20,
                                    background: Color.FromRgb(5, 5, 5),
                                    id: "label"
                                )
                            ) { }
                        }
                    }
                }
            }
        });
        var card = driver.Get("card");
        var label = driver.Get("label");
        var page = driver.Get("page");
        var root = driver.Get("root");

        driver.Hover(label);
        var cardGone = (
            card.States & InputStates.Hovered,
            label.States & InputStates.Hovered,
            page.States & InputStates.Hovered
        );
        model.Count = 1;
        driver.Settle();

        await Assert
            .That(cardGone)
            .IsEqualTo((InputStates.None, InputStates.None, InputStates.Hovered));
        await Assert
            .That((page.States | root.States) & InputStates.Hovered)
            .IsEqualTo(InputStates.None);
    }

    [Test]
    public async Task A_card_its_holder_lifts_while_hovered_stays_lifted_under_a_pointer_it_rose_off_until_the_pointer_leaves_the_view()
    {
        var lifted = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (
                ui.Box(
                    width: 100,
                    height: 60,
                    onHoverChange: Callback<bool>.Of(lifted, static (m, on) => m.Remember = on)
                )
            )
            using (
                ui.Box(
                    width: 100,
                    height: 60,
                    background: Color.FromRgb(5, 5, 5),
                    transform: lifted.Remember ? Transform.Translate(0, -6) : default,
                    id: "card"
                )
            ) { }
        });
        var card = driver.Get("card");

        driver.View.PointerMove(
            Pointer.Mouse,
            new Vector2(50, 58),
            MouseButtons.None,
            false,
            default,
            default
        );
        driver.Settle();
        for (var i = 0; i < 3; i++)
            driver.Frame();
        var resting = card.Visual.Transform.Matrix.Translation.Y;
        driver.Unhover();

        await Assert.That(resting).IsEqualTo(-6f);
        await Assert.That(card.Visual.Transform.IsIdentity).IsTrue();
    }

    static readonly string[] AHover = ["a+", "a-"];
    static readonly string[] BHover = ["b+", "b-"];

    [FramePath]
    static void Hoverable(
        Ui ui,
        Log log,
        string id,
        string[] names,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                width: 40,
                height: 40,
                background: Color.FromRgb(5, 5, 5),
                onHoverChange: Callback<bool>.Of(
                    log,
                    names,
                    static (l, names, on) => l.Add(names[on ? 0 : 1])
                ),
                id: id,
                key: id,
                file: file,
                line: line
            )
        ) { }
    }

    [Test]
    public async Task A_held_hovered_focused_node_leaving_the_view_lets_go_of_all_at_once_and_is_told_in_order()
    {
        var log = new Log();
        var gone = new Switches();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    width: 100,
                    height: 100,
                    focusable: true,
                    background: Color.FromRgb(5, 5, 5),
                    id: "parent"
                )
            )
            {
                if (!gone.A)
                {
                    using (
                        ui.Box(
                            width: 40,
                            height: 40,
                            focusable: true,
                            onClick: Callback.Of(log, static l => l.Add("click")),
                            onPointerCancel: Handler<PointerEvent>.Of(
                                log,
                                static (Log l, ref PointerEvent e) => l.Add("cancel")
                            ),
                            onHoverChange: Callback<bool>.Of(
                                log,
                                static (l, on) => l.Add(on ? "enter" : "exit")
                            ),
                            onFocusChange: Callback<FocusChange>.Of(
                                log,
                                static (l, f) => l.Add(f.Focused ? "focus" : "blur")
                            ),
                            id: "child"
                        )
                    ) { }
                }
            }
        });
        var held = driver.Hold(driver.Get("child"));
        log.Items.Clear();

        gone.A = true;
        driver.Frame();
        var told = log.ToString();
        held.Release();

        await Assert.That(told).IsEqualTo("cancel exit blur");
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("parent"));
        await Assert.That(log.ToString()).IsEqualTo(told);
    }
}

public enum DragLoss
{
    Disabled,
    Hidden,
    Removed,
}

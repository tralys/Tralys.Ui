using System.Numerics;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class InputAllocationTests
{
    [Test]
    public async Task Presses_a_window_previews_before_its_content_allocate_nothing()
    {
        var seen = new Counter();
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    padding: 10,
                    onPreviewPointerDown: Handler<PointerEvent>.Of(
                        seen,
                        static (c, ref _) => c.Value++
                    )
                )
            )
            using (
                ui.Box(
                    width: 20,
                    height: 20,
                    onClick: Callback.Of(clicks, static c => c.Value++),
                    id: "content"
                )
            ) { }
        });
        var content = driver.Get("content");

        var bytes = driver.WorstFrameBytes(
            content,
            static (d, n) =>
            {
                d.Click(n, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(seen.Value).IsEqualTo(clicks.Value);
    }

    [Test]
    public async Task Keys_navigating_running_shortcuts_actions_and_clicks_allocate_nothing()
    {
        var log = new Counter();
        var ran = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Screen(name: "screen", log: ran, active: true);
                using (
                    ui.Keyed(name: "keyed", log: ran, chord: Chord.Of(Key.S, ModifierKeys.Control))
                )
                {
                    using (
                        ui.Box(direction: Direction.Row, arrowNavigation: Navigation.Cycle, gap: 4)
                    )
                    {
                        for (var i = 0; i < 3; i++)
                            using (
                                ui.Box(
                                    width: 20,
                                    height: 20,
                                    onClick: Callback.Of(log, static c => c.Value++),
                                    key: i
                                )
                            ) { }
                    }
                }
            }
        });
        driver.Tab();
        driver.Tab();
        driver.Settle();

        var bytes = driver.WorstFrameBytes(
            ran,
            static (d, r) =>
            {
                r.Items.Clear();
                d.Tab(settle: false);
                d.Press(Key.Right, settle: false);
                d.Press(Key.Enter, settle: false);
                d.Press(Key.Space, settle: false);
                d.Press(Key.S, ModifierKeys.Control, settle: false);
                d.Press(Key.Escape, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Gamepad_buttons_repeats_and_sticks_switching_the_device_allocate_nothing()
    {
        var seen = new Seen<InputDevice>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, arrowNavigation: Navigation.Cycle, gap: 4))
            {
                ui.DeviceProbe(seen: seen);
                for (var i = 0; i < 3; i++)
                    using (
                        ui.Box(
                            width: 20,
                            height: 20,
                            onClick: Callback.Of(static () => { }),
                            onGamepad: Handler<GamepadEvent>.Of(static (ref GamepadEvent e) => { }),
                            key: i
                        )
                    ) { }
            }
        });
        driver.Tab();

        var bytes = driver.WorstFrameBytes(static d =>
        {
            d.Press(GamepadButton.South, settle: false);
            d.View.Gamepad(GamepadButton.DPadRight, true);
            d.Frame(0.5f);
            d.View.Gamepad(GamepadButton.DPadRight, false);
            d.View.Stick(GamepadStick.Left, new Vector2(0.5f, 0));
            d.Press(Key.A, settle: false);
            d.Frame();
        });

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Double_clicks_context_menus_holds_and_drags_allocate_nothing()
    {
        var log = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    align: Align.Start,
                    width: 200,
                    height: 200,
                    onDrag: Handler<DragEvent>.Of(
                        log,
                        static (Counter c, ref DragEvent e) => c.Value++
                    )
                )
            )
            {
                using (
                    ui.Box(
                        width: 40,
                        height: 40,
                        onDoubleClick: Callback.Of(log, static c => c.Value++),
                        onLongPress: Callback.Of(log, static c => c.Value++),
                        onContextMenu: Handler<PointerEvent>.Of(
                            log,
                            static (Counter c, ref PointerEvent e) => c.Value++
                        ),
                        id: "target"
                    )
                ) { }
            }
        });
        var target = driver.Get("target");

        var bytes = driver.WorstFrameBytes(
            target,
            static (d, t) =>
            {
                d.DoubleClick(t, settle: false);
                d.Click(t, MouseButton.Right, settle: false);
                d.LongPress(t, settle: false);
                d.Drag(t, new Vector2(30, 0), settle: false);
                d.Drag(t, new Vector2(30, 0), PointerKind.Touch, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_press_or_tap_node_pressed_by_a_mouse_and_tapped_by_a_finger_allocates_nothing()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (
                ui.Box(
                    width: 40,
                    height: 40,
                    onClick: Callback.Of(clicks, static c => c.Value++),
                    pressMode: PressMode.PressOrTap,
                    id: "target"
                )
            ) { }
        });
        var target = driver.Get("target");

        var bytes = driver.WorstFrameBytes(
            target,
            static (d, t) =>
            {
                d.Click(t, settle: false);
                d.Tap(t, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(clicks.Value).IsGreaterThan(0);
    }

    [Test]
    public async Task A_drag_and_drop_with_its_preview_onto_a_target_allocates_nothing()
    {
        var effects = new List<DragEffect>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 20))
            {
                using (
                    ui.Box(
                        width: 40,
                        height: 40,
                        dragData: "payload",
                        dragPreview: Slot.Of(static ui => ui.Text("ghost")),
                        onDragEnd: Callback<DragEffect>.Of(
                            effects,
                            static (e, effect) => e.Clear()
                        ),
                        id: "source"
                    )
                ) { }
                using (
                    ui.Box(
                        width: 120,
                        height: 120,
                        onDragOver: Handler<DropEvent>.Of(
                            static (ref DropEvent e) => e.Effect = DragEffect.Copy
                        ),
                        onDrop: Handler<DropEvent>.Of(static (ref DropEvent e) => { })
                    )
                )
                    ui.Text("slot");
            }
        });
        var source = driver.Get("source");

        var bytes = driver.WorstFrameBytes(
            source,
            static (d, s) =>
            {
                d.Drag(s, new Vector2(100, 0), settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task An_item_dragged_between_nested_drop_targets_every_frame_lights_only_the_one_under_it_and_allocates_nothing()
    {
        using var driver = UiDriver.Render(ui =>
        {
            var copies = Handler<DropEvent>.Of(
                static (ref DropEvent e) => e.Effect = DragEffect.Copy
            );
            var drops = Handler<DropEvent>.Of(static (ref DropEvent e) => { });
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 20))
            {
                using (ui.Box(width: 40, height: 40, dragData: "payload", id: "source")) { }
                using (
                    ui.Box(
                        width: 200,
                        height: 200,
                        align: Align.Start,
                        onDragOver: copies,
                        onDrop: drops,
                        id: "outer"
                    )
                )
                using (
                    ui.Box(width: 50, height: 50, onDragOver: copies, onDrop: drops, id: "inner")
                ) { }
            }
        });
        var outer = driver.Get("outer");
        var inner = driver.Get("inner");
        var press = driver.Hold(driver.Get("source"));
        press.MoveTo(inner);
        var own = new Vector2(outer.ViewBounds.Right - 25, outer.ViewBounds.Bottom - 25);
        var hops = (
            moves: new Moves(press, own - press.Position),
            inner,
            outer,
            wrong: new Counter()
        );

        var bytes = driver.WorstFrameBytes(
            hops,
            static (d, h) =>
            {
                var toOuter = h.moves.Tick++ % 2 == 0;
                h.moves.Press.MoveBy(toOuter ? h.moves.By : -h.moves.By, settle: false);
                d.Frame();
                if (Lit(h.inner) == toOuter || Lit(h.outer) != toOuter || !d.View.DragDropActive)
                    h.wrong.Value++;
            }
        );
        var dragged = (hops.moves.Tick > 0, hops.wrong.Value);
        press.Release();

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(dragged).IsEqualTo((true, 0));
        await Assert
            .That((Lit(inner), Lit(outer), driver.View.DragDropActive))
            .IsEqualTo((false, false, false));
    }

    static bool Lit(Node node) => (node.States & InputStates.DropTarget) != 0;

    [Test]
    public async Task Window_focus_auto_focus_and_letting_go_of_press_focus_allocate_nothing()
    {
        var gate = new Model();
        using var driver = UiDriver.Render(ui => ui.AutoFocusProbe(gate: gate));
        driver.View.Context.PressKeepsFocus = false;
        var button = driver.Get("button");

        var bytes = driver.WorstFrameBytes(
            (gate, button),
            static (d, s) =>
            {
                d.View.WindowFocus(false);
                d.View.WindowFocus(true);
                s.gate.Remember = !s.gate.Remember;
                d.Click(s.button, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_focused_captor_unmounted_mid_press_every_frame_is_told_it_lost_both_and_allocates_nothing()
    {
        var shown = new Model { Remember = true };
        var lost = new Lost();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (ui.Box(width: 100, height: 40, id: "stage"))
            {
                if (shown.Remember)
                    using (
                        ui.Box(
                            width: 100,
                            height: 40,
                            focusable: true,
                            onPointerDown: Handler<PointerEvent>.Of(
                                shown,
                                static (Model m, ref PointerEvent e) =>
                                {
                                    e.Capture();
                                    m.Remember = false;
                                }
                            ),
                            onPointerCancel: Handler<PointerEvent>.Of(
                                lost,
                                static (Lost l, ref PointerEvent e) => l.Capture++
                            ),
                            onFocusChange: Callback<FocusChange>.Of(
                                lost,
                                static (l, change) =>
                                {
                                    if (!change.Focused)
                                        l.Focus++;
                                }
                            )
                        )
                    ) { }
            }
        });
        var stage = driver.Get("stage");

        var bytes = driver.WorstFrameBytes(
            (shown, lost, stage),
            static (d, s) =>
            {
                d.LongPress(s.stage, settle: false);
                s.shown.Remember = true;
                d.Frame();
                s.lost.Frames++;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(lost.Capture).IsEqualTo(lost.Frames);
        await Assert.That(lost.Focus).IsEqualTo(lost.Frames);
    }

    [Test]
    public async Task Scope_actions_and_buttons_following_their_commands_allocate_nothing()
    {
        var open = new Switches();
        var command = new Command(static () => { }, () => open.A);
        var disabled = new Seen<bool>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Screen(name: "screen", log: new Log(), active: true, command: command);
                ui.ActionBarProbe();
                ui.CommandButton(onClick: command, disabled: disabled);
            }
        });

        var bytes = driver.WorstFrameBytes(
            open,
            static (d, open) =>
            {
                open.A = !open.A;
                var actions = d.View.Context.Actions;
                actions[UiAction.Confirm] =
                    actions[UiAction.Confirm].Key.Key == Key.Enter
                        ? new ActionBinding(Key.F, GamepadButton.South)
                        : new ActionBinding(Key.Enter, GamepadButton.South);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_strip_kept_off_system_gestures_moving_every_frame_tells_the_host_each_move_and_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui => ui.GestureStrip(model: model));
        var strip = new Strip(model);

        var bytes = driver.WorstFrameBytes(
            strip,
            static (d, m) =>
            {
                var sent = d.SystemGestureExclusionsSent;
                m.Model.Count = (m.Model.Count + 1) % 100;
                d.Frame();
                if (
                    d.SystemGestureExclusionsSent != sent + 1
                    || d.SystemGestureExclusions[0].X != m.Model.Count
                )
                    m.Wrong++;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(strip.Wrong).IsEqualTo(0);
    }

    sealed class Strip(Model model)
    {
        public readonly Model Model = model;
        public int Wrong;
    }

    sealed class Lost
    {
        public int Capture;
        public int Focus;
        public int Frames;
    }
}

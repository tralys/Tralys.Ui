using System.Numerics;
using Tralys.Ui.Testing;

namespace Tralys.Ui.DevTools.Tests;

public sealed class InspectorTests
{
    const ModifierKeys CtrlShift = ModifierKeys.Control | ModifierKeys.Shift;

    static UiDriver Card(Counter clicks, Counter ups) =>
        UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    id: "card",
                    width: 100,
                    height: 40,
                    background: Color.FromRgb(30, 30, 30),
                    onClick: Callback.Of(clicks, static c => c.Value++),
                    onPointerUp: Handler<PointerEvent>.Of(
                        ups,
                        static (Counter c, ref PointerEvent e) => c.Value++
                    )
                )
            )
            using (ui.Box(id: "inner", width: 20, height: 20)) { }
        });

    static Inspector Beside(UiDriver driver)
    {
        var inspector = new Inspector(driver.View, new NullHost());
        inspector.Resize(new Size(800f + Inspector.PanelWidth, 600f));
        driver.Settle();
        inspector.Update();
        return inspector;
    }

    static void Click(Inspector inspector, Vector2 at, MouseButton button = MouseButton.Left)
    {
        inspector.PointerDown(Pointer.Mouse, at, button, default, default);
        inspector.PointerUp(Pointer.Mouse, at, button, default, default);
    }

    [Test]
    public async Task A_press_while_picking_selects_the_deepest_node_there_and_never_reaches_the_target()
    {
        var clicks = new Counter();
        using var driver = Card(clicks, new Counter());
        using var inspector = Beside(driver);
        var at = Find.Corner(driver.Get("inner")) + new Vector2(5f, 5f);

        inspector.KeyDown(Key.C, CtrlShift, repeat: false);
        inspector.KeyUp(Key.C, CtrlShift);
        Click(inspector, at);
        driver.Settle();

        await Assert.That(inspector.Selected).IsSameReferenceAs(driver.Get("inner"));
        await Assert.That(inspector.IsPicking).IsFalse();
        await Assert.That(clicks.Value).IsEqualTo(0);
    }

    [Test]
    public async Task A_release_goes_where_its_own_press_went_though_a_picking_press_came_between()
    {
        var ups = new Counter();
        using var driver = Card(new Counter(), ups);
        using var inspector = Beside(driver);
        var at = Find.Corner(driver.Get("card")) + new Vector2(60f, 10f);

        inspector.PointerDown(Pointer.Mouse, at, MouseButton.Left, default, default);
        inspector.IsPicking = true;
        Click(inspector, at, MouseButton.Right);
        inspector.PointerUp(Pointer.Mouse, at, MouseButton.Left, default, default);
        driver.Settle();

        await Assert.That(ups.Value).IsEqualTo(1);
        await Assert.That(inspector.Selected).IsSameReferenceAs(driver.Get("card"));
    }

    [Test]
    public async Task A_press_in_the_panel_whose_release_was_dropped_leaves_the_next_release_to_the_target()
    {
        var clicks = new Counter();
        using var driver = Card(clicks, new Counter());
        using var inspector = Beside(driver);
        var card = Find.Corner(driver.Get("card")) + new Vector2(60f, 10f);

        inspector.PointerDown(
            Pointer.Mouse,
            new Vector2(820f, 300f),
            MouseButton.Left,
            default,
            default
        );
        Click(inspector, card);
        driver.Settle();

        await Assert.That(clicks.Value).IsEqualTo(1);
    }

    [Test]
    public async Task A_drag_out_onto_the_panel_stays_with_the_target_to_its_release()
    {
        var ups = new Counter();
        using var driver = Card(new Counter(), ups);
        using var inspector = Beside(driver);
        var card = Find.Centre(driver.Get("card"));
        var onPanel = new Vector2(800f + 10f, 10f);

        inspector.PointerDown(Pointer.Mouse, card, MouseButton.Left, default, default);
        inspector.PointerMove(Pointer.Mouse, onPanel, MouseButtons.Left, true, default, default);
        inspector.PointerUp(Pointer.Mouse, onPanel, MouseButton.Left, default, default);
        driver.Settle();

        await Assert.That(ups.Value).IsEqualTo(1);
        await Assert
            .That(driver.Get("card").States & InputStates.Pressed)
            .IsEqualTo(InputStates.None);
    }

    [Test]
    public async Task Moving_onto_the_panel_the_target_hears_the_pointer_leave()
    {
        var probe = new HoverProbe();
        using var driver = UiDriver.Render(ui => ui.Lamp(probe: probe));
        using var inspector = Beside(driver);

        inspector.PointerMove(
            Pointer.Mouse,
            Find.Centre(driver.GetText("lamp")),
            default,
            false,
            default,
            default
        );
        driver.Settle();
        var hovered = probe.Hovered;
        inspector.PointerMove(
            Pointer.Mouse,
            new Vector2(820f, 300f),
            default,
            false,
            default,
            default
        );
        driver.Settle();

        await Assert.That(hovered).IsTrue();
        await Assert.That(probe.Hovered).IsFalse();
    }

    [Test]
    public async Task Work_posted_to_the_target_waits_for_the_targets_update_not_the_inspectors()
    {
        var ran = new Counter();
        using var driver = UiDriver.Render(ui => ui.Text("app"));
        using var inspector = Beside(driver);

        driver.View.Context.Dispatcher.Post(() => ran.Value++);
        inspector.Update();
        var early = ran.Value;
        driver.Frame();

        await Assert.That(early).IsEqualTo(0);
        await Assert.That(ran.Value).IsEqualTo(1);
    }

    [Test]
    public async Task The_selection_box_goes_while_an_ancestor_is_collapsed_and_comes_back_once_shown()
    {
        var collapsed = false;
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(id: "outer", collapsed: collapsed))
            using (ui.Box(id: "inner", width: 20, height: 20)) { }
        });
        using var inspector = Beside(driver);
        inspector.Selected = driver.Get("inner");

        var shown = inspector.Model.SelectedBox;
        collapsed = true;
        driver.Rerender();
        inspector.Update();
        var hidden = inspector.Model.SelectedBox;
        collapsed = false;
        driver.Rerender();
        inspector.Update();

        await Assert.That(shown).IsNotNull();
        await Assert.That(hidden).IsNull();
        await Assert.That(inspector.Model.SelectedBox).IsEqualTo(shown);
    }

    [Test]
    public async Task A_selected_wrapping_box_shows_a_gap_between_neighbours_on_each_line_and_between_lines_as_its_items_change()
    {
        var items = 3;
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    id: "wrap",
                    width: 100,
                    height: 56,
                    wrap: FlexWrap.Wrap,
                    rowGap: 6,
                    columnGap: 10,
                    padding: 5
                )
            )
            {
                for (var i = 0; i < items; i++)
                    using (ui.Box(width: 40, height: 20, key: i)) { }
            }
        });
        using var inspector = Beside(driver);

        inspector.Selected = driver.Get("wrap");
        driver.Settle();
        inspector.Update();
        var three = inspector.Model.Gaps.ToArray();
        items = 4;
        driver.Rerender();
        inspector.Update();

        await Assert
            .That(three)
            .IsEquivalentTo(new[] { new Rect(45, 5, 10, 20), new Rect(5, 25, 90, 6) });
        await Assert
            .That(inspector.Model.Gaps.ToArray())
            .IsEquivalentTo(
                new[] { new Rect(45, 5, 10, 20), new Rect(5, 25, 90, 6), new Rect(45, 31, 10, 20) }
            );
    }

    [Test]
    public async Task A_selected_grid_shows_its_column_gaps_row_by_row_and_its_row_gaps_across_its_tracks()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Grid(
                    id: "grid",
                    columns: new GridTracks(Track.Px(30), Track.Px(50)),
                    height: 42,
                    gap: 8,
                    padding: 2
                )
            )
            {
                using (ui.Box(height: 10)) { }
                using (ui.Box(height: 20)) { }
                using (ui.Box(height: 10)) { }
                using (ui.Box(height: 10)) { }
            }
        });
        using var inspector = Beside(driver);

        inspector.Selected = driver.Get("grid");
        driver.Settle();
        inspector.Update();

        await Assert
            .That(inspector.Model.Gaps.ToArray())
            .IsEquivalentTo(
                new[] { new Rect(32, 2, 8, 20), new Rect(2, 22, 88, 8), new Rect(32, 30, 8, 10) }
            );
    }

    [Test]
    public async Task The_environment_shown_is_the_selected_components_root_as_resolved_with_what_is_held_on_it()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(theme: Themes.Light, disabled: true))
                ui.Meter(level: 1f);
        });
        using var inspector = Beside(driver);

        inspector.Selected = driver.Get<Meter>();
        inspector.Held = InputStates.Hovered;
        driver.Settle();
        inspector.Update();
        var shown = inspector.Model.SelectedEnvironment!.Value;

        await Assert.That(shown.Theme).IsSameReferenceAs(Themes.Light);
        await Assert.That(shown.Disabled).IsTrue();
        await Assert.That(shown.Held & InputStates.Hovered).IsEqualTo(InputStates.Hovered);
    }

    [Test]
    public async Task The_environment_tag_sits_below_the_selected_box_or_above_it_without_room_below_inside_the_target()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: Length.Full, height: Length.Full))
            {
                using (
                    ui.Box(
                        id: "high",
                        position: Position.Absolute,
                        left: 20,
                        top: 20,
                        width: 40,
                        height: 20
                    )
                ) { }
                using (
                    ui.Box(
                        id: "low",
                        position: Position.Absolute,
                        right: 0,
                        bottom: 0,
                        width: 40,
                        height: 20
                    )
                ) { }
            }
        });
        using var inspector = Beside(driver);
        var high = driver.Get("high").ViewBounds;
        var low = driver.Get("low").ViewBounds;

        inspector.Selected = driver.Get("high");
        inspector.Update();
        var below = Find.Id(inspector.View.Root, "environment")!.ViewBounds;
        inspector.Selected = driver.Get("low");
        inspector.Update();
        var above = Find.Id(inspector.View.Root, "environment")!.ViewBounds;

        await Assert.That(below.X).IsEqualTo(high.X);
        await Assert.That(below.Y).IsGreaterThanOrEqualTo(high.Bottom);
        await Assert.That(below.Y).IsLessThan(high.Bottom + 10f);
        await Assert.That(above.Bottom).IsLessThanOrEqualTo(low.Y);
        await Assert.That(above.Right).IsLessThanOrEqualTo(driver.View.Size.Width);
        await Assert.That(above.X).IsLessThan(low.X);
    }

    [Test]
    public async Task Keys_go_to_the_panel_while_it_holds_the_focus_and_a_release_where_its_press_went()
    {
        var downs = new Counter();
        var ups = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    id: "field",
                    width: 100,
                    height: 30,
                    focusable: true,
                    onKeyDown: Handler<KeyEvent>.Of(
                        downs,
                        static (Counter c, ref KeyEvent e) => c.Value++
                    ),
                    onKeyUp: Handler<KeyEvent>.Of(
                        ups,
                        static (Counter c, ref KeyEvent e) => c.Value++
                    )
                )
            ) { }
        });
        using var inspector = Beside(driver);
        Click(inspector, Find.Centre(driver.Get("field")));
        inspector.Update();
        var row = Find.Text(inspector.View.Root, "Box")!;

        inspector.KeyDown(Key.Space, default, repeat: false);
        Click(inspector, Find.Centre(row));
        inspector.KeyUp(Key.Space, default);
        var panelHeld = inspector.View.Focused is not null;
        inspector.KeyDown(Key.A, default, repeat: false);
        inspector.KeyUp(Key.A, default);
        var downsWhilePanel = downs.Value;
        Click(inspector, Find.Centre(driver.Get("field")));
        inspector.KeyDown(Key.A, default, repeat: false);

        await Assert.That(panelHeld).IsTrue();
        await Assert.That(ups.Value).IsEqualTo(1);
        await Assert.That(downsWhilePanel).IsEqualTo(1);
        await Assert.That(inspector.View.Focused).IsNull();
        await Assert.That(downs.Value).IsEqualTo(2);
    }

    [Test]
    public async Task Ctrl_shift_c_flips_picking_once_per_press_and_escape_ends_it_the_target_hearing_neither()
    {
        var downs = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    id: "field",
                    width: 100,
                    height: 30,
                    focusable: true,
                    autoFocus: true,
                    onKeyDown: Handler<KeyEvent>.Of(
                        downs,
                        static (Counter c, ref KeyEvent e) => c.Value++
                    )
                )
            ) { }
        });
        using var inspector = Beside(driver);

        inspector.KeyDown(Key.C, CtrlShift, repeat: false);
        inspector.KeyDown(Key.C, CtrlShift, repeat: true);
        inspector.KeyUp(Key.C, CtrlShift);
        var picking = inspector.IsPicking;
        inspector.KeyDown(Key.Escape, default, repeat: false);
        inspector.KeyUp(Key.Escape, default);

        await Assert.That(picking).IsTrue();
        await Assert.That(inspector.IsPicking).IsFalse();
        await Assert.That(downs.Value).IsEqualTo(0);
    }

    [Test]
    public async Task While_picking_the_target_hears_the_pointer_leave_and_a_hover_box_follows_what_a_press_would_pick()
    {
        var probe = new HoverProbe();
        using var driver = UiDriver.Render(ui => ui.Lamp(probe: probe));
        using var inspector = Beside(driver);
        var lamp = driver.GetText("lamp").ViewBounds;
        var at = new Vector2(lamp.X + 2f, lamp.Y + 2f);

        inspector.PointerMove(Pointer.Mouse, at, default, false, default, default);
        driver.Settle();
        var hoveredBefore = probe.Hovered;
        inspector.IsPicking = true;
        inspector.PointerMove(Pointer.Mouse, at + Vector2.One, default, false, default, default);
        driver.Settle();
        inspector.Update();
        var box = inspector.Model.HoverBox;
        inspector.IsPicking = false;

        await Assert.That(hoveredBefore).IsTrue();
        await Assert.That(probe.Hovered).IsFalse();
        await Assert.That(box).IsEqualTo(lamp);
        await Assert.That(inspector.Model.HoverBox).IsNull();
    }

    [Test]
    public async Task The_picker_takes_the_topmost_node_by_z_index_and_reaches_into_an_open_layer()
    {
        var layered = false;
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(id: "stack", width: 200, height: 100))
            {
                using (
                    ui.Box(
                        id: "raised",
                        position: Position.Absolute,
                        width: 80,
                        height: 80,
                        zIndex: 1
                    )
                ) { }
                using (ui.Box(id: "later", position: Position.Absolute, width: 80, height: 80)) { }
                if (layered)
                    using (ui.Layer(id: "layer"))
                    using (ui.Box(id: "in-layer", width: 40, height: 40)) { }
            }
        });
        using var inspector = Beside(driver);

        var raised = inspector.Pick(new Vector2(20f, 20f));
        layered = true;
        driver.Rerender();
        var inLayer = inspector.Pick(new Vector2(20f, 20f));

        await Assert.That(raised).IsSameReferenceAs(driver.Get("raised"));
        await Assert.That(inLayer).IsSameReferenceAs(driver.Get("in-layer"));
    }

    [Test]
    public async Task Held_states_land_on_the_component_a_selected_part_belongs_to_until_it_is_deselected()
    {
        var probe = new HoverProbe();
        using var driver = UiDriver.Render(ui => ui.Lamp(probe: probe));
        using var inspector = new Inspector(driver.View, new NullHost());

        inspector.Selected = driver.GetText("lamp");
        inspector.Held = InputStates.Hovered;
        driver.Settle();
        var held = probe.Hovered;
        inspector.Selected = null;
        driver.Settle();

        await Assert.That(held).IsTrue();
        await Assert.That(probe.Hovered).IsFalse();
    }

    [Test]
    public async Task Letting_go_of_a_held_state_keeps_what_another_holder_forced()
    {
        var probe = new HoverProbe();
        using var driver = UiDriver.Render(ui => ui.Lamp(probe: probe));
        driver.ForceStates(driver.Get<Lamp>(), InputStates.Hovered);
        using var inspector = new Inspector(driver.View, new NullHost());

        inspector.Selected = driver.GetText("lamp");
        inspector.Held = InputStates.Pressed;
        driver.Settle();
        var pressed = probe.Pressed;
        inspector.Held = InputStates.None;
        driver.Settle();

        await Assert.That(pressed).IsTrue();
        await Assert.That(probe.Pressed).IsFalse();
        await Assert.That(probe.Hovered).IsTrue();
    }

    [Test]
    public async Task What_repaints_flashes_until_its_flash_fades_or_repaints_are_turned_off()
    {
        var lit = false;
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    id: "lamp",
                    width: 40,
                    height: 20,
                    background: lit ? Color.FromRgb(250, 200, 0) : Color.FromRgb(40, 40, 40)
                )
            ) { }
        });
        using var inspector = Beside(driver);
        for (var i = 0; i < InspectorModel.FlashFrames; i++)
            inspector.Update();
        var lamp = driver.Get("lamp");

        lit = true;
        driver.Rerender();
        inspector.Update();
        var flashed = Flashes(inspector, lamp);
        for (var i = 0; i < InspectorModel.FlashFrames; i++)
            inspector.Update();
        var faded = Flashes(inspector, lamp);
        lit = false;
        driver.Rerender();
        inspector.Model.ShowsRepaints = false;
        inspector.Update();

        await Assert.That(flashed).IsEqualTo(1);
        await Assert.That(faded).IsEqualTo(0);
        await Assert.That(inspector.Model.Flashes.Length).IsEqualTo(0);
    }

    static int Flashes(Inspector inspector, Node node)
    {
        var count = 0;
        foreach (var flash in inspector.Model.Flashes)
        {
            if (flash.Node == node)
                count++;
        }
        return count;
    }

    [Test]
    public async Task The_speed_button_steps_the_targets_animations_down_to_paused_then_back_and_closing_restores_its_own()
    {
        using var driver = UiDriver.Render(ui => ui.Text("app"));
        driver.View.Context.AnimationSpeed = 0.75f;
        var speeds = new float[5];
        var panels = new float[5];
        using (var inspector = Beside(driver))
        {
            var model = inspector.Model;
            for (var i = 0; i < speeds.Length; i++)
            {
                model.CycleSpeed();
                speeds[i] = driver.View.Context.AnimationSpeed;
                panels[i] = inspector.View.Context.AnimationSpeed;
            }
            model.CycleSpeed();
        }

        await Assert.That(speeds).IsEquivalentTo(new[] { 0.5f, 0.25f, 0.1f, 0f, 0.75f });
        await Assert.That(panels).IsEquivalentTo(new[] { 1f, 1f, 1f, 1f, 1f });
        await Assert.That(driver.View.Context.AnimationSpeed).IsEqualTo(0.75f);
    }

    [Test]
    public async Task Frame_bytes_count_what_the_target_allocates_between_updates()
    {
        using var driver = UiDriver.Render(ui => ui.Text("app"));
        using var inspector = Beside(driver);
        inspector.Update();

        GC.KeepAlive(new byte[10_000]);
        inspector.Update();
        var busy = inspector.Model.FrameBytes;
        inspector.Update();

        await Assert.That(busy).IsGreaterThanOrEqualTo(10_000L);
        await Assert.That(inspector.Model.FrameBytes).IsEqualTo(0L);
        await Assert.That(inspector.Model.WorstBytes).IsEqualTo(busy);
    }
}

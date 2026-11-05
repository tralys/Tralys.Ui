using System.Numerics;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class InputTests
{
    [Test]
    public async Task A_preview_handler_sees_a_press_outermost_first_before_content_that_stops_it()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    padding: 10,
                    onPreviewPointerDown: Handler<PointerEvent>.Of(
                        log,
                        static (l, ref _) => l.Add("window")
                    ),
                    onPointerDown: Handler<PointerEvent>.Of(
                        log,
                        static (l, ref _) => l.Add("window bubble")
                    )
                )
            )
            using (
                ui.Box(
                    padding: 10,
                    onPreviewPointerDown: Handler<PointerEvent>.Of(
                        log,
                        static (l, ref _) => l.Add("panel")
                    )
                )
            )
            using (
                ui.Box(
                    width: 20,
                    height: 20,
                    onPointerDown: Handler<PointerEvent>.Of(
                        log,
                        static (l, ref e) =>
                        {
                            l.Add("content");
                            e.StopPropagation();
                        }
                    ),
                    id: "content"
                )
            ) { }
        });

        driver.Click(driver.Get("content"));

        await Assert.That(log.ToString()).IsEqualTo("window panel content");
    }

    [Test]
    public async Task A_reorder_with_unequal_row_heights_keeps_bounds_and_hits_right()
    {
        var rows = new ObservableList<int> { 1, 2, 3 };
        var clicked = new List<int>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                foreach (var row in rows)
                {
                    using (
                        ui.Box(
                            width: 50,
                            height: row * 10,
                            onClick: Callback.Of(
                                clicked,
                                Boxed.Of(row),
                                static (c, r) => c.Add(r.Value)
                            ),
                            id: "row" + row,
                            key: row
                        )
                    ) { }
                }
            }
        });

        rows.Move(2, 0);
        driver.Settle();

        await Assert.That(driver.Get("row3").ViewBounds).IsEqualTo(new Rect(0, 0, 50, 30));
        await Assert.That(driver.Get("row1").ViewBounds).IsEqualTo(new Rect(0, 30, 50, 10));
        await Assert.That(driver.Get("row2").ViewBounds).IsEqualTo(new Rect(0, 40, 50, 20));
        driver.Click(new Vector2(25, 25));
        driver.Click(new Vector2(25, 35));
        await Assert.That(clicked).IsEquivalentTo([3, 1]);
    }

    [Test]
    public async Task A_layout_only_box_passes_input_through()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 100, height: 100))
            {
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 0,
                        top: 0,
                        width: 100,
                        height: 100,
                        onClick: Callback.Of(clicks, static c => c.Value++),
                        id: "button"
                    )
                ) { }
                using (
                    ui.Box(position: Position.Absolute, left: 0, top: 0, width: 100, height: 100)
                ) { }
            }
        });

        driver.Click(driver.Get("button"));

        await Assert.That(clicks.Value).IsEqualTo(1);
    }

    static readonly Color Ink = Color.FromRgb(200, 60, 40);

    static string? HitAt(UiDriver driver, float x, float y) =>
        driver.View.HitTest(new Vector2(x, y))?.Id;

    [Test]
    public async Task A_node_kept_off_system_gestures_sends_its_device_pixel_rect_at_the_first_update_and_again_only_as_it_moves()
    {
        var model = new Model();
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box())
                {
                    using (
                        ui.Box(
                            position: Position.Absolute,
                            left: model.Count,
                            top: 0,
                            width: 40,
                            height: 300,
                            excludeSystemGestures: true
                        )
                    ) { }
                }
            },
            scale: 2
        );
        var first = (
            driver.SystemGestureExclusions.ToArray().Single(),
            driver.SystemGestureExclusionsSent
        );
        driver.Frame();
        var still = driver.SystemGestureExclusionsSent;

        model.Count = 10;
        driver.Settle();

        await Assert.That(first).IsEqualTo((new Rect(0, 0, 80, 600), 1));
        await Assert.That(still).IsEqualTo(1);
        await Assert
            .That(
                (
                    driver.SystemGestureExclusions.ToArray().Single(),
                    driver.SystemGestureExclusionsSent
                )
            )
            .IsEqualTo((new Rect(20, 0, 80, 600), 2));
    }

    [Test]
    public async Task A_hit_finds_the_raised_or_later_sibling_passing_over_hidden_ones_and_ones_taking_no_pointer()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 300, height: 100))
            {
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 0,
                        width: 50,
                        height: 50,
                        background: Ink,
                        id: "a"
                    )
                ) { }
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 20,
                        width: 50,
                        height: 50,
                        background: Ink,
                        id: "b"
                    )
                ) { }
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 30,
                        width: 50,
                        height: 50,
                        background: Ink,
                        pointerEvents: PointerEvents.None,
                        id: "none"
                    )
                ) { }
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 100,
                        width: 50,
                        height: 50,
                        background: Ink,
                        id: "under"
                    )
                ) { }
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 100,
                        width: 50,
                        height: 50,
                        background: Ink,
                        hidden: true,
                        id: "hidden"
                    )
                ) { }
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 200,
                        width: 50,
                        height: 50,
                        background: Ink,
                        zIndex: 1,
                        id: "raised"
                    )
                ) { }
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 210,
                        width: 50,
                        height: 50,
                        background: Ink,
                        id: "later"
                    )
                ) { }
            }
        });

        await Assert.That(HitAt(driver, 35, 25)).IsEqualTo("b");
        await Assert.That(HitAt(driver, 120, 25)).IsEqualTo("under");
        await Assert.That(HitAt(driver, 230, 25)).IsEqualTo("raised");
    }

    [Test]
    public async Task A_turned_box_is_hit_where_it_draws_and_not_where_it_lay()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (
                ui.Box(
                    width: 100,
                    height: 20,
                    margin: new Thickness(100, 100, 0, 0),
                    transform: Transform.Rotate(90),
                    background: Ink,
                    id: "turned"
                )
            ) { }
        });

        await Assert.That(HitAt(driver, 150, 140)).IsEqualTo("turned");
        await Assert.That(HitAt(driver, 110, 110)).IsNull();
    }

    [Test]
    public async Task A_handlers_position_maps_through_its_own_and_its_ancestors_transforms()
    {
        var down = new Spot();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (
                ui.Box(
                    width: 100,
                    height: 100,
                    margin: new Thickness(100, 100, 0, 0),
                    transform: Transform.Scale(2),
                    align: Align.Start
                )
            )
            using (
                ui.Box(
                    width: 50,
                    height: 50,
                    transform: Transform.Rotate(90),
                    onPointerDown: Handler<PointerEvent>.Of(
                        down,
                        static (s, ref e) => s.At = e.Position
                    )
                )
            ) { }
        });

        driver.Click(new Vector2(70, 70));

        await Assert.That(down.At.X).IsEqualTo(10f).Within(1e-3f);
        await Assert.That(down.At.Y).IsEqualTo(40f).Within(1e-3f);
    }

    [Test]
    public async Task A_skew_in_degrees_moves_x_by_y_and_a_handler_reads_its_position_back_through_it()
    {
        var down = new Spot();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (
                ui.Box(
                    width: 100,
                    height: 100,
                    margin: new Thickness(100, 100, 0, 0),
                    transform: Transform.Skew(45, 0),
                    transformOrigin: Vector2.Zero,
                    onPointerDown: Handler<PointerEvent>.Of(
                        down,
                        static (s, ref e) => s.At = e.Position
                    )
                )
            ) { }
        });

        driver.Click(new Vector2(230, 150));

        await Assert.That(down.At.X).IsEqualTo(80f).Within(1e-3f);
        await Assert.That(down.At.Y).IsEqualTo(50f).Within(1e-3f);
    }

    [Test]
    public async Task A_composed_transform_applies_its_first_part_then_the_next()
    {
        var down = new Spot();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (
                ui.Box(
                    width: 100,
                    height: 100,
                    margin: new Thickness(100, 100, 0, 0),
                    transform: Transform.Scale(2, 1).Then(Transform.Translate(10, 0)),
                    transformOrigin: Vector2.Zero,
                    onPointerDown: Handler<PointerEvent>.Of(
                        down,
                        static (s, ref e) => s.At = e.Position
                    )
                )
            ) { }
        });

        driver.Click(new Vector2(150, 150));

        await Assert.That(down.At.X).IsEqualTo(20f).Within(1e-3f);
    }

    [Test]
    public async Task A_rounded_box_and_a_pill_drawn_at_a_fractional_origin_hit_inside_their_outline_but_not_in_their_cut_corners()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 10))
            {
                using (ui.Box(width: 40, height: 20, radius: 10, background: Ink, id: "rounded"))
                { }
                ui.Canvas(state: 0, draw: Pill, width: 50, height: 44, id: "pill");
            }
        });

        await Assert.That(HitAt(driver, 1, 1)).IsNull();
        await Assert.That(HitAt(driver, 20, 1)).IsEqualTo("rounded");
        await Assert.That(HitAt(driver, 51, 1)).IsNull();
        await Assert.That(HitAt(driver, 72, 22)).IsEqualTo("pill");
    }

    static readonly CanvasDraw<int> Pill = static (dc, _, _) =>
        dc.DrawRoundedRectangle(Ink, new Rect(0.01f, 0, 44, 44), new CornerRadius(22));

    [Test]
    public async Task A_clip_keeps_its_children_from_being_hit_past_its_edge_or_in_its_rounded_corners()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 400, height: 200, background: Ink, id: "behind"))
            {
                using (
                    ui.Box(
                        position: Position.Absolute,
                        width: 100,
                        height: 100,
                        radius: 30,
                        overflow: Overflow.Clip
                    )
                )
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: -50,
                        top: -50,
                        width: 300,
                        height: 300,
                        background: Ink,
                        id: "clipped"
                    )
                ) { }
                using (ui.Box(position: Position.Absolute, left: 200, width: 100, height: 100))
                using (
                    ui.Box(
                        position: Position.Absolute,
                        width: 50,
                        height: 150,
                        background: Ink,
                        id: "hanging"
                    )
                ) { }
            }
        });

        await Assert.That(HitAt(driver, 50, 50)).IsEqualTo("clipped");
        await Assert.That(HitAt(driver, 100, 50)).IsEqualTo("behind");
        await Assert.That(HitAt(driver, 4, 4)).IsEqualTo("behind");
        await Assert.That(HitAt(driver, 225, 140)).IsEqualTo("hanging");
    }

    [Test]
    public async Task A_press_is_taken_over_painted_ui_and_left_to_the_scene_over_bare_layout_until_a_dismissable_layer_opens()
    {
        var open = new Switches();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start))
            {
                using (ui.Box(width: 100, height: 100))
                using (ui.Box(width: 50, height: 50)) { }
                using (ui.Box(width: 100, height: 100, background: Ink))
                using (ui.Box(width: 50, height: 50)) { }
                if (open.A)
                {
                    using (ui.Layer(dismissOn: Dismiss.OutsidePress))
                    using (ui.Box(width: 10, height: 10, background: Ink)) { }
                }
            }
        });

        var bare = Press(driver, new Vector2(25, 25));
        var painted = Press(driver, new Vector2(125, 25));
        open.A = true;
        driver.Settle();
        var dismissable = Press(driver, new Vector2(25, 25));

        await Assert.That((bare, painted, dismissable)).IsEqualTo((false, true, true));
    }

    [Test]
    public async Task A_press_on_the_mounted_roots_own_paint_is_left_to_the_scene_even_disabled_unless_the_root_presses_or_focuses()
    {
        var root = new Switches();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    align: Align.Start,
                    background: Ink,
                    focusable: root.B,
                    disabled: root.C,
                    onClick: root.A ? Callback.Of(static () => { }) : default
                )
            )
            using (ui.Box(width: 100, height: 100, background: Ink)) { }
        });

        var plain = Press(driver, new Vector2(300, 300));
        var painted = Press(driver, new Vector2(50, 50));
        root.A = true;
        driver.Settle();
        var pressed = Press(driver, new Vector2(300, 300));
        root.A = false;
        root.B = true;
        driver.Settle();
        var focused = Press(driver, new Vector2(300, 300));
        root.B = false;
        root.C = true;
        driver.Settle();
        var disabled = Press(driver, new Vector2(300, 300));

        await Assert
            .That((plain, painted, pressed, focused, disabled))
            .IsEqualTo((false, true, true, true, false));
    }

    [Test]
    public async Task A_press_on_a_disabled_node_inside_a_card_hits_it_and_goes_no_further()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (
                ui.Box(
                    width: 100,
                    height: 100,
                    align: Align.Start,
                    background: Ink,
                    onClick: Callback.Of(clicks, static c => c.Value++)
                )
            )
            using (
                ui.Box(
                    width: 40,
                    height: 40,
                    disabled: true,
                    onClick: Callback.Of(clicks, static c => c.Value++),
                    id: "disabled"
                )
            ) { }
        });

        var hit = HitAt(driver, 20, 20);
        var taken = Press(driver, new Vector2(20, 20));

        await Assert.That((hit, taken, clicks.Value)).IsEqualTo(("disabled", true, 0));
    }

    [Test]
    public async Task Asking_whether_a_press_would_be_taken_answers_what_the_press_then_reports()
    {
        var root = new Switches();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    align: Align.Start,
                    background: Ink,
                    focusable: root.B,
                    onClick: root.A ? Callback.Of(static () => { }) : default
                )
            )
            {
                using (ui.Box(width: 100, height: 100, background: Ink)) { }
                if (root.C)
                {
                    using (
                        ui.Layer(
                            dismissOn: Dismiss.OutsidePress,
                            onDismiss: Callback.Of(root, static r => r.C = false),
                            justify: Justify.End,
                            align: Align.End
                        )
                    )
                    using (ui.Box(width: 40, height: 40, background: Ink)) { }
                }
            }
        });
        var asked = new List<bool>();
        var pressed = new List<bool>();
        void Ask(bool pressing, bool focusable, bool open, Vector2 at)
        {
            (root.A, root.B, root.C) = (pressing, focusable, open);
            driver.Settle();
            asked.Add(driver.View.TakesPress(at));
            pressed.Add(Press(driver, at));
        }

        Ask(false, false, false, new Vector2(300, 300));
        Ask(false, false, false, new Vector2(50, 50));
        Ask(true, false, false, new Vector2(300, 300));
        Ask(false, true, false, new Vector2(300, 300));
        Ask(false, false, true, new Vector2(300, 300));

        await Assert.That(asked).IsEquivalentTo(pressed);
        await Assert.That(asked).IsEquivalentTo([false, true, true, true, true]);
    }

    static bool Press(UiDriver driver, Vector2 at)
    {
        var taken = driver.View.PointerDown(Pointer.Mouse, at, MouseButton.Left, default, default);
        driver.View.PointerUp(Pointer.Mouse, at, MouseButton.Left, default, default);
        driver.Settle();
        return taken;
    }
}

internal sealed class Boxed
{
    public required int Value { get; init; }

    public static Boxed Of(int value) => new() { Value = value };
}

internal sealed class Spot
{
    public Vector2 At;
}

using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class DragDropTests
{
    [Test]
    public async Task A_drop_lands_on_the_deepest_accepting_target_and_tells_the_source_its_effect()
    {
        var log = new Log();
        var ended = new List<DragEffect>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 20))
            {
                Source(ui, ended);
                using (
                    ui.Box(
                        width: 120,
                        height: 120,
                        align: Align.Start,
                        onDrop: Handler<DropEvent>.Of(
                            log,
                            static (Log l, ref DropEvent e) => l.Add("outer")
                        ),
                        id: "outer"
                    )
                )
                {
                    using (
                        ui.Box(
                            width: 60,
                            height: 60,
                            onDragOver: Handler<DropEvent>.Of(
                                log,
                                static (Log l, ref DropEvent e) => e.Effect = DragEffect.Copy
                            ),
                            onDrop: Handler<DropEvent>.Of(
                                log,
                                static (Log l, ref DropEvent e) => l.Add((string)e.Data!)
                            ),
                            id: "inner"
                        )
                    ) { }
                }
            }
        });

        driver.Drag(driver.Get("source"), Between(driver, "source", "inner"));

        await Assert.That(log.ToString()).IsEqualTo("payload");
        await Assert.That(ended).IsEquivalentTo([DragEffect.Copy]);
        await Assert
            .That(
                (driver.Get("inner").States | driver.Get("outer").States) & InputStates.DropTarget
            )
            .IsEqualTo(InputStates.None);
    }

    [Test]
    public async Task A_drag_and_its_drop_carry_the_button_so_a_source_can_refuse_all_but_the_left()
    {
        var drops = new List<MouseButton>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 20))
            {
                using (ui.Box(width: 40, height: 40, dragData: "any", id: "any")) { }
                using (
                    ui.Box(
                        width: 40,
                        height: 40,
                        dragData: "left",
                        onDrag: Handler<DragEvent>.Of(
                            static (ref DragEvent e) =>
                            {
                                if (e.Phase == DragPhase.Start && e.Button != MouseButton.Left)
                                    e.PreventDefault();
                            }
                        ),
                        id: "left"
                    )
                ) { }
                using (
                    ui.Box(
                        width: 60,
                        height: 60,
                        onDragOver: Handler<DropEvent>.Of(
                            static (ref DropEvent e) => e.Effect = DragEffect.Move
                        ),
                        onDrop: Handler<DropEvent>.Of(
                            drops,
                            static (List<MouseButton> d, ref DropEvent e) => d.Add(e.Button)
                        ),
                        id: "target"
                    )
                ) { }
            }
        });

        foreach (var source in (string[])["any", "left"])
        {
            var held = driver.Hold(driver.Get(source), button: MouseButton.Right);
            held.MoveBy(Between(driver, source, "target"), steps: 4);
            held.Release();
        }

        await Assert.That(drops).IsEquivalentTo([MouseButton.Right]);
    }

    [Test]
    public async Task A_drag_tells_only_its_target_each_time_it_enters_and_leaves_drops_as_its_button_lifts_and_keeps_the_release_from_it()
    {
        var log = new Log();
        var ended = new List<DragEffect>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 20))
            {
                Source(ui, ended);
                using (
                    ui.Box(
                        width: 60,
                        height: 60,
                        background: Color.FromRgb(9, 9, 9),
                        onPointerUp: Handler<PointerEvent>.Of(
                            log,
                            static (Log l, ref PointerEvent e) => l.Add("plain up")
                        ),
                        id: "plain"
                    )
                ) { }
                using (
                    ui.Box(
                        width: 60,
                        height: 60,
                        onDragOver: Handler<DropEvent>.Of(
                            log,
                            static (Log l, ref DropEvent e) =>
                            {
                                l.Add("over");
                                e.Effect = DragEffect.Move;
                            }
                        ),
                        onDragLeave: Callback.Of(log, static l => l.Add("leave")),
                        onDrop: Handler<DropEvent>.Of(
                            log,
                            static (Log l, ref DropEvent e) => l.Add((string)e.Data!)
                        ),
                        onPointerUp: Handler<PointerEvent>.Of(
                            log,
                            static (Log l, ref PointerEvent e) => l.Add("up")
                        ),
                        id: "target"
                    )
                ) { }
            }
        });
        var plain = driver.Get("plain");
        var target = driver.Get("target");

        var left = driver.Hold(driver.Get("source"));
        left.MoveTo(plain);
        left.MoveTo(target);
        left.MoveTo(plain);
        left.MoveTo(target);
        left.Release();
        var byLeft = log.ToString();
        log.Items.Clear();
        var right = driver.Hold(driver.Get("source"), button: MouseButton.Right);
        right.MoveTo(target);
        right.Release();

        await Assert.That(byLeft).IsEqualTo("over leave over payload");
        await Assert.That(log.ToString()).IsEqualTo("over payload");
        await Assert.That(ended).IsEquivalentTo([DragEffect.Move, DragEffect.Move]);
        await Assert.That(driver.View.DragDropActive).IsFalse();
    }

    [Test]
    public async Task A_press_leaving_a_draggable_button_short_of_the_threshold_keeps_it_captured_hovering_nothing_beside_it_until_it_crosses()
    {
        var entered = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start))
            {
                using (
                    ui.Box(
                        width: 40,
                        height: 40,
                        onClick: Callback.Of(entered, static _ => { }),
                        dragData: "payload",
                        id: "item"
                    )
                ) { }
                using (
                    ui.Box(
                        width: 40,
                        height: 40,
                        background: Color.FromRgb(9, 9, 9),
                        onHoverChange: Callback<bool>.Of(
                            entered,
                            static (c, on) =>
                            {
                                if (on)
                                    c.Value++;
                            }
                        ),
                        id: "neighbour"
                    )
                ) { }
            }
        });
        var item = driver.Get("item");
        var neighbour = driver.Get("neighbour");

        var held = driver.Hold(new Vector2(38, 20));
        held.MoveBy(new Vector2(3, 0));
        var shortOfIt = (
            driver.View.DragDropActive,
            (item.States & InputStates.Capturing) != 0,
            (neighbour.States & InputStates.Hovered) != 0,
            entered.Value
        );
        held.MoveBy(new Vector2(9, 0));
        var crossed = driver.View.DragDropActive;
        held.Release();

        await Assert.That(shortOfIt).IsEqualTo((false, true, false, 0));
        await Assert.That(crossed).IsTrue();
    }

    [Test]
    public async Task A_second_finger_on_a_draggable_button_leaves_the_first_its_drag()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    width: 40,
                    height: 40,
                    onClick: Callback.Of(new Counter(), static c => c.Value++),
                    dragData: "payload",
                    id: "item"
                )
            ) { }
        });

        var first = driver.Hold(driver.Get("item"), PointerKind.Touch);
        var second = driver.Hold(driver.Get("item"), PointerKind.Touch);
        first.MoveBy(new Vector2(20, 0));
        var dragging = driver.View.DragDropActive;
        first.Release();
        second.Release();

        await Assert.That(dragging).IsTrue();
    }

    [Test]
    public async Task A_lit_target_raises_its_parent_over_its_siblings_until_the_drop_and_leaves_its_parents_own_z_index_as_it_was()
    {
        var ended = new List<DragEffect>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 400, height: 200))
            {
                Cell(ui, left: 0, zIndex: 0, "target");
                Cover(ui, left: 50, zIndex: 0, "cover");
                Cell(ui, left: 200, zIndex: 3, "lifted-target");
                Cover(ui, left: 250, zIndex: 2, "lifted-cover");
                using (ui.Box(position: Position.Absolute, left: 0, top: 150))
                    Source(ui, ended);
            }
        });
        var view = driver.View;
        var overlap = new Vector2(75, 50);
        var liftedOverlap = new Vector2(275, 50);

        var held = driver.Hold(driver.Get("source"));
        held.MoveBy(new Vector2(25, 50) - held.Position);
        var overTheFirst = (view.HitTest(overlap)?.Id, view.HitTest(liftedOverlap)?.Id);
        held.MoveBy(new Vector2(225, 50) - held.Position);
        var overTheLifted = view.HitTest(overlap)?.Id;
        held.Release();

        await Assert.That(overTheFirst).IsEqualTo(("target", "lifted-target"));
        await Assert.That(overTheLifted).IsEqualTo("cover");
        await Assert.That(ended.Count).IsEqualTo(1);
        await Assert
            .That((view.HitTest(overlap)?.Id, view.HitTest(liftedOverlap)?.Id))
            .IsEqualTo(("cover", "lifted-target"));
    }

    [Test]
    public async Task A_target_leaving_the_view_mid_drag_hears_it_leave_once_after_it_has_gone_and_the_drag_goes_on()
    {
        var shown = new Switches { A = true };
        var heard = new Heard();
        var ended = new List<DragEffect>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 20))
            {
                Source(ui, ended);
                if (shown.A)
                {
                    using (
                        ui.Box(
                            width: 60,
                            height: 60,
                            onDragLeave: Callback.Of(
                                heard,
                                static h =>
                                    h.Log.Add(h.Target!.IsMounted ? "leave in view" : "leave")
                            ),
                            onDrop: Handler<DropEvent>.Of(
                                heard,
                                static (Heard h, ref DropEvent e) => h.Log.Add("drop")
                            ),
                            id: "target"
                        )
                    ) { }
                }
                using (ui.Box(width: 200, height: 60)) { }
                using (
                    ui.Box(
                        width: 60,
                        height: 60,
                        onDrop: Handler<DropEvent>.Of(
                            heard,
                            static (Heard h, ref DropEvent e) => h.Log.Add("second")
                        ),
                        id: "second"
                    )
                ) { }
            }
        });
        heard.Target = driver.Get("target");

        var held = driver.Hold(driver.Get("source"));
        held.MoveTo(heard.Target);
        shown.A = false;
        driver.Settle();
        var dragging = driver.View.DragDropActive;
        held.MoveTo(driver.Get("second"));
        held.Release();

        await Assert.That(dragging).IsTrue();
        await Assert.That(heard.Log.ToString()).IsEqualTo("leave second");
    }

    [Test]
    public async Task A_leave_handler_cancelling_the_drag_each_time_it_runs_hears_one_leave_and_the_source_one_end()
    {
        var heard = new Heard();
        var ended = new List<DragEffect>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 20))
            {
                Source(ui, ended);
                using (
                    ui.Box(
                        width: 60,
                        height: 60,
                        onDragLeave: Callback.Of(
                            heard,
                            static h =>
                            {
                                h.Log.Add("leave");
                                h.View!.CancelDrag();
                            }
                        ),
                        onDrop: Handler<DropEvent>.Of(
                            heard,
                            static (Heard h, ref DropEvent e) => h.Log.Add("drop")
                        ),
                        id: "target"
                    )
                ) { }
            }
        });
        heard.View = driver.View;

        var held = driver.Hold(driver.Get("source"));
        held.MoveTo(driver.Get("target"));
        held.MoveTo(driver.Get("source"));
        var dragging = driver.View.DragDropActive;
        held.Release();

        await Assert.That(dragging).IsFalse();
        await Assert.That(heard.Log.ToString()).IsEqualTo("leave");
        await Assert.That(ended).IsEquivalentTo([DragEffect.None]);
    }

    [Test]
    public async Task A_refusing_target_hears_the_drag_leave_and_the_source_gets_no_effect()
    {
        var log = new Log();
        var ended = new List<DragEffect>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 20))
            {
                Source(ui, ended);
                using (
                    ui.Box(
                        width: 60,
                        height: 60,
                        onDragOver: Handler<DropEvent>.Of(
                            log,
                            static (Log l, ref DropEvent e) => e.Effect = DragEffect.None
                        ),
                        onDragLeave: Callback.Of(log, static l => l.Add("leave")),
                        onDrop: Handler<DropEvent>.Of(
                            log,
                            static (Log l, ref DropEvent e) => l.Add("drop")
                        ),
                        id: "target"
                    )
                ) { }
            }
        });

        driver.Drag(driver.Get("source"), Between(driver, "source", "target"));

        await Assert.That(log.ToString()).IsEqualTo("leave");
        await Assert.That(ended).IsEquivalentTo([DragEffect.None]);
    }

    [Test]
    public async Task Escape_cancels_a_drag_so_its_release_drops_nothing()
    {
        var log = new Log();
        var ended = new List<DragEffect>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 20))
            {
                Source(ui, ended);
                using (
                    ui.Box(
                        width: 60,
                        height: 60,
                        onDragLeave: Callback.Of(log, static l => l.Add("leave")),
                        onDrop: Handler<DropEvent>.Of(
                            log,
                            static (Log l, ref DropEvent e) => l.Add("drop")
                        ),
                        id: "target"
                    )
                ) { }
            }
        });
        var view = driver.View;
        var from = Centre(driver.Get("source"));
        var to = Centre(driver.Get("target"));

        view.PointerDown(Pointer.Mouse, from, MouseButton.Left, default, default);
        view.PointerMove(Pointer.Mouse, to, MouseButtons.Left, true, default, default);
        driver.Press(Key.Escape);
        view.PointerUp(Pointer.Mouse, to, MouseButton.Left, default, default);
        driver.Settle();

        await Assert.That(log.ToString()).IsEqualTo("leave");
        await Assert.That(ended).IsEquivalentTo([DragEffect.None]);
    }

    [Test]
    public async Task A_press_that_only_closed_a_layer_drags_nothing_while_a_button_pressed_after_it_drags_until_the_last_lifts()
    {
        var open = new Switches { A = true };
        var log = new Log();
        var ended = new List<DragEffect>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 20))
            {
                Source(ui, ended);
                using (
                    ui.Box(
                        width: 60,
                        height: 60,
                        onDrop: Handler<DropEvent>.Of(
                            log,
                            static (Log l, ref DropEvent e) => l.Add("drop")
                        ),
                        id: "target"
                    )
                ) { }
                if (open.A)
                {
                    using (
                        ui.Positioner(
                            anchor: Anchor.Point(new Vector2(300, 300)),
                            placement: PlacementMode.Absolute,
                            dismissOn: Dismiss.OutsidePress,
                            onDismiss: Callback.Of(open, static s => s.A = false)
                        )
                    )
                    {
                        using (ui.Box(width: 20, height: 20)) { }
                    }
                }
            }
        });

        var closing = driver.Hold(driver.Get("source"));
        closing.MoveTo(driver.Get("target"));
        closing.Release();
        var afterTheClosingPress = (log.ToString(), ended.Count, open.A);
        open.A = true;
        driver.Settle();
        var left = driver.Hold(driver.Get("target"));
        var right = driver.Hold(driver.Get("source"), button: MouseButton.Right);
        right.MoveTo(driver.Get("target"));
        right.Release();
        var beforeTheLastLift = log.ToString();
        left.Release();

        await Assert.That(afterTheClosingPress).IsEqualTo(("", 0, false));
        await Assert.That(open.A).IsFalse();
        await Assert.That(beforeTheLastLift).IsEmpty();
        await Assert.That(log.ToString()).IsEqualTo("drop");
        await Assert.That(ended.Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_source_that_left_the_view_mid_drag_still_hears_the_result()
    {
        var ended = new List<DragEffect>();
        var rows = new ObservableList<int> { 1 };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 20))
            {
                using (ui.Box(width: 40, height: 40))
                {
                    foreach (var row in rows)
                        Source(ui, ended, key: row);
                }
                using (
                    ui.Box(
                        width: 60,
                        height: 60,
                        onDrop: Handler<DropEvent>.Of(
                            rows,
                            static (ObservableList<int> r, ref DropEvent e) =>
                                e.Effect = DragEffect.Move
                        ),
                        id: "target"
                    )
                ) { }
            }
        });
        var view = driver.View;
        var from = Centre(driver.Get("source"));
        var to = Centre(driver.Get("target"));

        view.PointerDown(Pointer.Mouse, from, MouseButton.Left, default, default);
        view.PointerMove(Pointer.Mouse, to, MouseButtons.Left, true, default, default);
        rows.Clear();
        driver.Settle();
        view.PointerUp(Pointer.Mouse, to, MouseButton.Left, default, default);
        driver.Settle();

        await Assert.That(ended).IsEquivalentTo([DragEffect.Move]);
    }

    [FramePath]
    static void Source(
        Ui ui,
        List<DragEffect> ended,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                width: 40,
                height: 40,
                dragData: "payload",
                dragEffects: DragEffect.Copy | DragEffect.Move,
                onDragEnd: Callback<DragEffect>.Of(ended, static (e, effect) => e.Add(effect)),
                id: "source",
                key: key,
                file: file,
                line: line
            )
        ) { }
    }

    [FramePath]
    static void Cell(
        Ui ui,
        float left,
        int zIndex,
        string target,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                position: Position.Absolute,
                left: left,
                top: 0,
                width: 100,
                height: 100,
                zIndex: zIndex,
                file: file,
                line: line
            )
        )
        using (
            ui.Box(
                width: 100,
                height: 100,
                background: Color.FromRgb(9, 9, 9),
                onDrop: Handler<DropEvent>.Of(target, static (string _, ref DropEvent e) => { }),
                id: target
            )
        ) { }
    }

    [FramePath]
    static void Cover(
        Ui ui,
        float left,
        int zIndex,
        string id,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                position: Position.Absolute,
                left: left,
                top: 0,
                width: 100,
                height: 100,
                zIndex: zIndex,
                background: Color.FromRgb(60, 60, 60),
                id: id,
                file: file,
                line: line
            )
        ) { }
    }

    sealed class Heard
    {
        public readonly Log Log = new();
        public Node? Target;
        public UiView? View;
    }

    static Vector2 Centre(Node node) =>
        new(
            node.ViewBounds.X + node.ViewBounds.Width / 2,
            node.ViewBounds.Y + node.ViewBounds.Height / 2
        );

    static Vector2 Between(UiDriver driver, string from, string to) =>
        Centre(driver.Get(to)) - Centre(driver.Get(from));
}

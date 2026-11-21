using System.Numerics;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class InputWiringTests
{
    [Test]
    public async Task Escape_cancels_a_drag_before_it_dismisses_a_layer()
    {
        var open = new Switches { A = true };
        var ended = new List<DragEffect>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 20))
            {
                using (
                    ui.Box(
                        width: 40,
                        height: 40,
                        dragData: "payload",
                        onDragEnd: Callback<DragEffect>.Of(
                            ended,
                            static (e, effect) => e.Add(effect)
                        ),
                        id: "source"
                    )
                ) { }
                using (
                    ui.Box(
                        width: 60,
                        height: 60,
                        onDrop: Handler<DropEvent>.Of(static (ref DropEvent e) => { }),
                        id: "target"
                    )
                ) { }
                if (open.A)
                {
                    using (
                        ui.Layer(
                            hit: LayerHit.PassThrough,
                            dismissOn: Dismiss.Escape,
                            onDismiss: Callback.Of(open, static s => s.A = false)
                        )
                    ) { }
                }
            }
        });
        var view = driver.View;
        var to = Centre(driver.Get("target"));

        view.PointerDown(
            Pointer.Mouse,
            Centre(driver.Get("source")),
            MouseButton.Left,
            default,
            default
        );
        view.PointerMove(Pointer.Mouse, to, MouseButtons.Left, true, default, default);
        driver.Press(Key.Escape);
        var openAfterCancel = open.A;
        view.PointerUp(Pointer.Mouse, to, MouseButton.Left, default, default);
        driver.Press(Key.Escape);

        await Assert.That(ended).IsEquivalentTo([DragEffect.None]);
        await Assert.That(openAfterCancel).IsTrue();
        await Assert.That(open.A).IsFalse();
    }

    [Test]
    public async Task East_runs_back_before_dismissing_while_escape_dismisses_first()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box())
            {
                ui.Screen(name: "screen", log: log, active: true);
                using (
                    ui.Layer(
                        dismissOn: Dismiss.Escape,
                        onDismiss: Callback.Of(log, static l => l.Add("dismiss"))
                    )
                ) { }
            }
        });

        driver.Press(GamepadButton.East);
        driver.Press(Key.Escape);

        await Assert.That(log.ToString()).IsEqualTo("back dismiss");
    }

    [Test]
    public async Task A_modal_takes_focus_to_its_first_tab_stop_keeps_tab_inside_and_silences_view_shortcuts()
    {
        var log = new Log();
        var open = new Switches { A = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start))
            {
                using (
                    ui.Keyed(
                        name: "view",
                        log: log,
                        chord: Chord.Of(Key.K, ModifierKeys.Control),
                        scope: ShortcutScope.View
                    )
                )
                {
                    using (ui.Box(width: 20, height: 20, focusable: true, id: "outside")) { }
                }
                using (
                    ui.Dialog(
                        open: open.A,
                        onOpenChange: Callback<bool>.Of(open, static (s, v) => s.A = v)
                    )
                )
                {
                    using (ui.Box(width: 20, height: 20, focusable: true, id: "x")) { }
                    using (ui.Box(width: 20, height: 20, focusable: true, tabIndex: 1, id: "y")) { }
                }
            }
        });
        var seen = new List<string?> { driver.Focused?.Id };

        driver.Tab();
        seen.Add(driver.Focused?.Id);
        driver.Tab();
        seen.Add(driver.Focused?.Id);
        driver.Press(Key.K, ModifierKeys.Control);

        await Assert.That(string.Join(" ", seen)).IsEqualTo("y x y");
        await Assert.That(log.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task Tab_passes_over_an_exiting_component()
    {
        var shown = new Switches { A = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 4))
            {
                if (shown.A)
                    ui.Notice(number: 1, exit: new Transition(1f));
                using (ui.Box(width: 20, height: 20, focusable: true, id: "after")) { }
            }
        });

        shown.A = false;
        driver.Frame();
        driver.Tab(settle: false);

        await Assert.That(driver.Focused?.Id).IsEqualTo("after");
    }

    [Test]
    public async Task A_field_takes_its_place_in_tab_order_by_its_tab_index()
    {
        var text = new ObservableText("text");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 4))
            {
                using (ui.Box(width: 20, height: 20, focusable: true, id: "box")) { }
                ui.EditableText(text: text, tabIndex: 1, width: 80, id: "field");
            }
        });

        driver.Tab();

        await Assert.That(driver.Focused?.Id).IsEqualTo("field");
    }

    [Test]
    public async Task A_window_losing_focus_stops_text_input_and_getting_it_back_keeps_the_selection()
    {
        var name = new ObservableText("hello");
        using var driver = UiDriver.Render(ui =>
            ui.EditableText(text: name, width: 200, id: "field")
        );
        driver.Click(driver.Get("field"));

        driver.View.WindowFocus(false);
        driver.Settle();
        var away = driver.TextInput;
        driver.View.WindowFocus(true);
        driver.Settle();
        driver.Type("X");

        await Assert.That(away).IsNull();
        await Assert.That(driver.TextInput).IsNotNull();
        await Assert.That(name.Length).IsEqualTo(6);
    }

    [Test]
    public async Task Clearing_the_focus_from_the_host_leaves_the_field_stopping_its_text_input_and_committing()
    {
        var name = new ObservableText("hello");
        var commits = new Log();
        using var driver = UiDriver.Render(ui =>
            ui.EditableText(
                text: name,
                onCommit: Callback.Of(commits, name, static (l, t) => l.Add(t.ToString())),
                width: 200,
                id: "field"
            )
        );
        driver.Click(driver.Get("field"));
        driver.Type("X");

        driver.View.ClearFocus();
        driver.Settle();

        await Assert.That(driver.Focused).IsNull();
        await Assert.That(driver.TextInput).IsNull();
        await Assert.That(commits.Items).IsEquivalentTo([name.ToString()]);
    }

    [Test]
    public async Task An_edit_command_follows_the_focused_fields_selection_and_runs_on_it()
    {
        var command = new Seen<Command?>();
        var can = new Seen<bool>();
        var copied = new ObservableText("copied");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.EditableText(text: copied, width: 200, id: "field");
                ui.CopyProbe(command: command, can: can);
            }
        });
        driver.Click(driver.Get("field"));
        var before = can.Value;

        driver.Press(Key.A, ModifierKeys.Control);
        var after = can.Value;
        var copy = await Assert.That(command.Value).IsNotNull();
        copy.Run();

        await Assert.That(before).IsFalse();
        await Assert.That(after).IsTrue();
        await Assert.That(driver.Clipboard).IsEqualTo("copied");
    }

    [Test]
    public async Task An_edit_command_aims_at_a_field_focused_inside_a_layer_and_goes_no_further()
    {
        var command = new Seen<Command?>();
        var can = new Seen<bool>();
        var open = new Switches();
        var behind = new ObservableText("behind");
        var inside = new ObservableText("inside");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.EditableText(text: behind, width: 200, id: "behind");
                ui.CopyProbe(command: command, can: can);
                if (open.A)
                {
                    using (ui.Layer())
                        ui.EditableText(text: inside, width: 200, id: "inside");
                }
            }
        });
        driver.Click(driver.Get("behind"));
        driver.Press(Key.A, ModifierKeys.Control);
        open.A = true;
        driver.Rerender();
        driver.Click(driver.Get("inside"));
        var before = can.Value;
        var copyNothing = await Assert.That(command.Value).IsNotNull();
        copyNothing.Run();
        var copied = driver.Clipboard;

        driver.Press(Key.A, ModifierKeys.Control);
        var copyInside = await Assert.That(command.Value).IsNotNull();
        copyInside.Run();

        await Assert.That(before).IsFalse();
        await Assert.That(copied).IsNull();
        await Assert.That(driver.Clipboard).IsEqualTo("inside");
    }

    [Test]
    public async Task A_selection_drag_ends_on_a_move_without_its_button()
    {
        var selections = new List<TextSelection>();
        var hello = new ObservableText("hello world");
        using var driver = UiDriver.Render(ui =>
            ui.EditableText(
                text: hello,
                onSelectionChange: Callback<TextSelection>.Of(
                    selections,
                    static (l, s) => l.Add(s)
                ),
                width: 200,
                id: "field"
            )
        );
        var view = driver.View;
        var bounds = driver.Get("field").ViewBounds;
        Vector2 At(float x) => new(bounds.X + x, bounds.Y + bounds.Height / 2);

        view.PointerDown(Pointer.Mouse, At(2), MouseButton.Left, default, default);
        view.PointerDown(Pointer.Mouse, At(2), MouseButton.Right, default, default);
        view.PointerMove(
            Pointer.Mouse,
            At(30),
            MouseButtons.Left | MouseButtons.Right,
            true,
            default,
            default
        );
        var dragged = selections[^1];
        view.PointerMove(Pointer.Mouse, At(150), MouseButtons.Right, true, default, default);
        view.PointerUp(Pointer.Mouse, At(150), MouseButton.Right, default, default);
        driver.Settle();

        await Assert.That(dragged.Caret).IsGreaterThan(0);
        await Assert.That(selections[^1]).IsEqualTo(dragged);
    }

    [Test]
    public async Task A_drag_shows_its_preview_centred_on_the_pointer_until_it_drops()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            {
                using (
                    ui.Box(
                        width: 40,
                        height: 40,
                        dragData: "payload",
                        dragPreview: Slot.Of(static ui => ui.Text("ghost")),
                        id: "source"
                    )
                ) { }
            }
        });
        var view = driver.View;
        var to = new Vector2(300, 200);

        view.PointerDown(
            Pointer.Mouse,
            Centre(driver.Get("source")),
            MouseButton.Left,
            default,
            default
        );
        view.PointerMove(Pointer.Mouse, to, MouseButtons.Left, true, default, default);
        driver.Settle();
        var ghost = Centre(driver.GetText("ghost"));
        view.PointerUp(Pointer.Mouse, to, MouseButton.Left, default, default);
        driver.Settle();

        await Assert.That(Vector2.Distance(ghost, to)).IsLessThan(1f);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    static Vector2 Centre(Node node) =>
        new(
            node.ViewBounds.X + node.ViewBounds.Width / 2,
            node.ViewBounds.Y + node.ViewBounds.Height / 2
        );
}

public readonly record struct CopyProbeProps
{
    public required Seen<Command?> Command { get; init; }

    public required Seen<bool> Can { get; init; }
}

/// <summary>Records the view's Copy command and whether it can run now.</summary>
public sealed class CopyProbe : Component<CopyProbeProps>
{
    protected override void Render(Ui ui, in CopyProbeProps p)
    {
        var copy = ui.Command(EditCommand.Copy);
        p.Command.Value = copy;
        p.Can.Value = ui.CanRun(copy);
    }
}

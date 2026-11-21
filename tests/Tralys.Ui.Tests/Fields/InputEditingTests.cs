using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;
using static Tralys.Ui.Tests.Fields;

namespace Tralys.Ui.Tests;

public sealed class InputEditingTests
{
    const ModifierKeys Ctrl = ModifierKeys.Control;
    const ModifierKeys Shift = ModifierKeys.Shift;

    static UiDriver Bound(
        Entry entry,
        bool multiline = false,
        bool wrap = false,
        float width = 200
    ) =>
        UiDriver.Render(ui =>
            ui.Input(text: entry.Text, multiline: multiline, wrap: wrap, width: width)
        );

    [Test]
    public async Task It_reports_its_caret_as_it_moves()
    {
        var entry = new Entry();
        var carets = new List<int>();
        using var driver = UiDriver.Render(ui =>
            ui.Input(
                text: entry.Text,
                onSelectionChange: Callback<TextSelection>.Of(
                    carets,
                    static (c, selection) => c.Add(selection.Caret)
                ),
                width: 200
            )
        );
        driver.Focus(driver.TextOf());

        driver.Type("ab");
        driver.Press(Key.Left);

        await Assert.That(carets[^1]).IsEqualTo(1);
    }

    [Test]
    public async Task An_edit_keeps_the_caret_and_history_and_a_set_from_the_app_replaces_the_text()
    {
        var entry = new Entry();
        using var driver = Bound(entry);
        driver.Focus(driver.TextOf());

        driver.Type("a");
        driver.Type("b");
        var typed = driver.Selection();
        entry.Value = "cd";
        driver.Settle();
        var replaced = driver.Shown();
        driver.Press(Key.Z, Ctrl);
        var undone = entry.Value;
        driver.Press(Key.Z, Ctrl);

        await Assert.That(entry.Reports).IsEquivalentTo(["a", "ab", "ab", ""]);
        await Assert.That((typed, replaced, undone)).IsEqualTo(((2, 2), "cd", "ab"));
        await Assert.That(entry.Value).IsEqualTo("");
    }

    [Test]
    public async Task The_limits_bind_typing_by_characters_or_utf8_bytes_but_never_a_value_the_app_sets()
    {
        var entry = new Entry();
        var chars = new ObservableText("abcdef");
        var bytes = new ObservableText("abcdef");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Input(text: chars, maxLength: 3, width: 200, id: "chars");
                ui.Input(text: bytes, maxBytes: 3, width: 200, id: "bytes");
                ui.Input(text: entry.Text, maxBytes: 4, width: 200, id: "typed");
            }
        });
        driver.Focus(driver.TextOf("typed"));

        driver.Type("Øx");
        driver.Type("é");
        driver.Type("y");
        driver.Type("w");

        await Assert
            .That((driver.Shown("chars"), driver.Shown("bytes")))
            .IsEqualTo(("abcdef", "abcdef"));
        await Assert.That(entry.Reports).IsEquivalentTo(["Øx", "Øxy"]);
    }

    [Test]
    public async Task Enter_types_a_line_break_in_a_multiline_input_but_only_submits_where_it_has_a_handler()
    {
        var notes = new Entry("ab");
        var ready = new Entry("Ready");
        var log = new List<string>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.ActionScope())
            {
                ui.Action(UiAction.Confirm, Callback.Of(log, static l => l.Add("confirm")));
                using (ui.Box(direction: Direction.Column, align: Align.Start))
                {
                    ui.Input(text: notes.Text, multiline: true, width: 200, id: "notes");
                    ui.Input(
                        text: ready.Text,
                        multiline: true,
                        onSubmit: Into(log, ready.Text),
                        width: 200,
                        id: "ready"
                    );
                }
            }
        });

        driver.CaretAt(1, "notes");
        driver.Press(Key.Enter);
        driver.Click(driver.TextOf("ready"));
        driver.Press(Key.Enter);

        await Assert.That(notes.Value).IsEqualTo("a\nb");
        await Assert.That(ready.Value).IsEqualTo("Ready");
        await Assert.That(log).IsEquivalentTo(["Ready"]);
    }

    [Test]
    public async Task Backspace_and_delete_erase_the_selection_else_a_whole_cluster_and_with_ctrl_a_word()
    {
        var entry = new Entry("ab😀😀 cd ef");
        using var driver = Bound(entry, width: 300);

        driver.CaretAt(4);
        driver.Press(Key.BackSpace);
        var back = entry.Value;
        driver.CaretAt(2);
        driver.Press(Key.Delete);
        var forward = entry.Value;
        driver.CaretAt(0);
        driver.Press(Key.Delete, Ctrl);
        var word = entry.Value;
        driver.CaretAt(1);
        driver.Press(Key.Right, Shift);
        driver.Press(Key.Right, Shift);
        driver.Press(Key.Right, Shift);
        driver.Press(Key.BackSpace);
        var selection = entry.Value;
        entry.Value = "go  north";
        driver.Settle();
        driver.Press(Key.End);
        driver.Press(Key.BackSpace, Ctrl);

        await Assert
            .That((back, forward, word, selection))
            .IsEqualTo(("ab😀 cd ef", "ab cd ef", "cd ef", "cf"));
        await Assert.That(entry.Value).IsEqualTo("go  ");
    }

    [Test]
    public async Task A_read_only_input_takes_no_typing_paste_cut_erase_enter_undo_or_redo_yet_selects_and_copies()
    {
        var entry = new Entry("abc");
        var readOnly = false;
        var seen = new List<Key>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(onKeyDown: Handler<KeyEvent>.Of(seen, static (s, ref e) => s.Add(e.Key))))
                ui.Input(text: entry.Text, multiline: true, readOnly: readOnly, width: 200);
        });
        driver.CaretAt(3);
        driver.Type("d");
        driver.Press(Key.Left);
        driver.Press(Key.Right);
        driver.Type("x");
        driver.Press(Key.Z, Ctrl);
        readOnly = true;
        driver.Rerender();
        entry.Reports.Clear();
        seen.Clear();
        driver.Clipboard = "XY";

        driver.Type("x");
        driver.Press(Key.Enter);
        driver.Press(Key.V, Ctrl);
        driver.Press(Key.Delete);
        driver.Press(Key.BackSpace, Ctrl);
        driver.Press(Key.Z, Ctrl);
        driver.Press(Key.Y, Ctrl);
        driver.Press(Key.A, Ctrl);
        driver.Press(Key.X, Ctrl);
        var cut = driver.Clipboard;
        driver.Press(Key.C, Ctrl);

        await Assert.That(entry.Reports).IsEmpty();
        await Assert.That(entry.Value).IsEqualTo("abcd");
        await Assert.That(seen).Contains(Key.Enter);
        await Assert.That((cut, driver.Clipboard)).IsEqualTo(("XY", "abcd"));
    }

    [Test]
    public async Task Shift_arrows_home_and_end_select_from_the_anchor_and_a_bare_arrow_collapses_to_its_edge()
    {
        var text = new ObservableText("hello");
        using var driver = UiDriver.Render(ui => ui.Input(text: text, width: 200));

        driver.CaretAt(0);
        driver.Press(Key.Right, Shift);
        driver.Press(Key.Right, Shift);
        var grown = driver.Selection();
        driver.CaretAt(1);
        driver.Press(Key.Right, Shift);
        driver.Press(Key.Right, Shift);
        driver.Press(Key.Right, Shift);
        driver.Press(Key.Left);
        var left = driver.Selection();
        driver.Press(Key.Right, Shift);
        driver.Press(Key.Right, Shift);
        driver.Press(Key.Right, Shift);
        driver.Press(Key.Right);
        var right = driver.Selection();
        driver.CaretAt(2);
        driver.Press(Key.End, Shift);
        var end = driver.Selection();
        driver.Press(Key.Home, Shift);
        var home = driver.Selection();
        driver.Press(Key.End, Ctrl | Shift);

        await Assert.That((grown, left, right)).IsEqualTo(((0, 2), (1, 1), (4, 4)));
        await Assert.That((end, home, driver.Selection())).IsEqualTo(((2, 5), (2, 0), (2, 5)));
    }

    [Test]
    public async Task Ctrl_arrows_step_a_word_and_the_space_after_it_over_tabs_and_fullwidth_letters()
    {
        var text = new ObservableText("go north now");
        using var driver = UiDriver.Render(ui => ui.Input(text: text, width: 300));

        driver.CaretAt(0);
        driver.Press(Key.Right, Ctrl);
        var over = driver.Selection();
        driver.Press(Key.Right, Ctrl | Shift);
        var selected = driver.Selection();
        driver.Press(Key.Left, Ctrl);
        var back = driver.Selection();
        text.Set("go \t north");
        driver.Rerender();
        driver.Press(Key.End);
        driver.Press(Key.Left, Ctrl);
        var tabbed = driver.Selection();
        driver.Press(Key.Left, Ctrl);
        var start = driver.Selection();
        text.Set("ＡＢＣ def");
        driver.Rerender();
        driver.Press(Key.Home);
        driver.Press(Key.Right, Ctrl);

        await Assert.That((over, selected, back)).IsEqualTo(((3, 3), (3, 9), (3, 3)));
        await Assert.That((tabbed, start)).IsEqualTo(((5, 5), (0, 0)));
        await Assert.That(driver.Selection()).IsEqualTo((4, 4));
    }

    [Test]
    public async Task Up_and_down_move_a_line_keeping_the_column_and_past_the_ends_reach_them_in_a_multiline_or_wrapping_input()
    {
        var lines = new ObservableText("abcd\nx\nabcd");
        var wrapped = new ObservableText("aaaa bbbb cccc");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Input(text: lines, multiline: true, width: 200, id: "lines");
                ui.Input(text: wrapped, wrap: true, width: 72, id: "wrapped");
            }
        });

        driver.CaretAt(3, "lines");
        driver.Press(Key.Down);
        var shortLine = driver.Selection("lines");
        driver.Press(Key.Down);
        var third = driver.Selection("lines");
        driver.Press(Key.Up);
        driver.Press(Key.Up);
        var back = driver.Selection("lines");
        driver.Press(Key.Up);
        var top = driver.Selection("lines");
        driver.Press(Key.End, Ctrl);
        driver.Press(Key.Left);
        driver.Press(Key.Down);
        var bottom = driver.Selection("lines");
        driver.CaretAt(1, "wrapped");
        var taken = driver.KeyDown(Key.Down);
        driver.KeyUp(Key.Down);

        await Assert.That((shortLine, third, back)).IsEqualTo(((6, 6), (10, 10), (3, 3)));
        await Assert.That((top, bottom)).IsEqualTo(((0, 0), (11, 11)));
        await Assert.That(taken).IsTrue();
        await Assert.That(driver.Selection("wrapped").Caret).IsGreaterThan(4);
    }

    [Test]
    public async Task Home_and_end_reach_the_edges_of_the_line_the_caret_was_last_put_on_and_ctrl_the_texts_ends()
    {
        const string text = "the quick brown fox jumps over the lazy dog";
        var line = new ObservableText(text);
        var words = new ObservableText("aaaa bbbb cccc");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Input(text: line, wrap: true, width: 84, id: "long");
                ui.Input(text: words, wrap: true, width: 72, id: "words");
            }
        });
        var jumps = text.IndexOf("jumps", StringComparison.Ordinal) + 2;

        driver.CaretAt(jumps, "long");
        driver.Press(Key.Home);
        var home = driver.Selection("long").Caret;
        driver.Press(Key.End);
        var end = driver.Selection("long").Caret;
        driver.Press(Key.Home, Ctrl);
        var first = driver.Selection("long").Caret;
        driver.Press(Key.End, Ctrl);
        var last = driver.Selection("long").Caret;
        var typing = driver.TextInput;
        driver.CaretAt(0, "words");
        driver.Press(Key.End);
        driver.Press(Key.End, Ctrl);
        for (var i = 0; i < 4; i++)
            driver.Press(Key.Left);
        driver.Press(Key.Home);

        await Assert.That(home).IsGreaterThan(0).And.IsLessThanOrEqualTo(jumps - 2);
        await Assert.That(end).IsGreaterThanOrEqualTo(jumps + 3).And.IsLessThan(text.Length);
        await Assert.That((first, last)).IsEqualTo((0, text.Length));
        await Assert.That(typing).IsNotNull();
        await Assert.That(driver.Selection("words")).IsEqualTo((10, 10));
    }

    [Test]
    public async Task A_caret_after_a_line_break_stands_before_a_mark_that_follows_it()
    {
        var text = new ObservableText("a\ńb");
        using var driver = UiDriver.Render(ui => ui.Input(text: text, multiline: true, width: 200));

        driver.CaretAt(2);

        await Assert.That(driver.Selection()).IsEqualTo((2, 2));
    }

    [Test]
    public async Task Keys_an_input_cannot_use_go_on_to_what_holds_it_leaving_the_caret()
    {
        var entry = new Entry();
        var seen = new List<Key>();
        var shortcuts = new List<string>();
        using var driver = UiDriver.Render(ui =>
        {
            ui.Shortcut(Chord.Of(Key.Z, Ctrl), Callback.Of(shortcuts, static s => s.Add("undo")));
            ui.Shortcut(
                Chord.Of(Key.Z, Ctrl | Shift),
                Callback.Of(shortcuts, static s => s.Add("redo"))
            );
            using (ui.Box(onKeyDown: Handler<KeyEvent>.Of(seen, static (s, ref e) => s.Add(e.Key))))
                ui.Input(text: entry.Text, width: 200);
        });
        driver.CaretAt(0);

        driver.Press(Key.Z, Ctrl);
        driver.Press(Key.Z, Ctrl | Shift);
        driver.Type("hello");
        driver.CaretAt(1);
        driver.Press(Key.Enter);
        driver.Press(Key.Down);
        driver.Press(Key.Right, ModifierKeys.Gui);
        driver.Press(Key.Left, ModifierKeys.Alt);
        driver.Press(Key.Escape);
        var caret = driver.Selection();
        driver.Press(Key.End);
        driver.Type("!");
        driver.Press(Key.Z, Ctrl);

        await Assert
            .That(seen)
            .IsEquivalentTo([Key.Z, Key.Z, Key.Enter, Key.Down, Key.Right, Key.Left, Key.Escape]);
        await Assert.That(shortcuts).IsEquivalentTo(["undo", "redo"]);
        await Assert.That(caret).IsEqualTo((1, 1));
        await Assert.That(entry.Value).IsEqualTo("hello");
    }

    [Test]
    public async Task Tab_in_a_multiline_input_moves_the_focus_on_and_types_no_tab()
    {
        var entry = new Entry("notes");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Input(text: entry.Text, multiline: true, width: 200);
                using (ui.Box(focusable: true, width: 20, height: 20, id: "next")) { }
            }
        });
        driver.CaretAt(5);

        driver.Tab();

        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("next"));
        await Assert.That(entry.Reports).IsEmpty();
    }

    [Test]
    public async Task Ctrl_insert_shift_delete_and_shift_insert_copy_cut_and_paste()
    {
        var entry = new Entry("hello");
        using var driver = Bound(entry);
        driver.CaretAt(1);
        for (var i = 0; i < 3; i++)
            driver.Press(Key.Right, Shift);

        driver.Press(Key.Insert, Ctrl);
        var copied = (driver.Clipboard, entry.Value);
        driver.Press(Key.Delete, Shift);
        var cut = (driver.Clipboard, entry.Value);
        driver.Clipboard = "XY";
        driver.Press(Key.Insert, Shift);

        await Assert.That((copied, cut)).IsEqualTo((("ell", "hello"), ("ell", "ho")));
        await Assert.That(entry.Value).IsEqualTo("hXYo");
    }

    [Test]
    public async Task A_paste_keeps_what_fits_over_the_selection_and_drops_control_and_format_characters()
    {
        var limited = new Entry("abcdef");
        var line = new Entry();
        var lines = new Entry();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Input(text: limited.Text, maxLength: 8, width: 200, id: "limited");
                ui.Input(text: line.Text, width: 200, id: "line");
                ui.Input(text: lines.Text, multiline: true, width: 200, id: "lines");
            }
        });

        driver.CaretAt(2, "limited");
        driver.Press(Key.Right, Shift);
        driver.Press(Key.Right, Shift);
        driver.Clipboard = "W😀😀";
        driver.Press(Key.V, Ctrl);
        driver.Clipboard = "a\r\nb\tc\u0007‮­‍‌";
        driver.Focus(driver.TextOf("line"));
        driver.Press(Key.V, Ctrl);
        driver.Focus(driver.TextOf("lines"));
        driver.Press(Key.V, Ctrl);

        await Assert.That(limited.Value).IsEqualTo("abW😀ef");
        await Assert.That(line.Value).IsEqualTo("ab\tc‍‌");
        await Assert.That(lines.Value).IsEqualTo("a\r\nb\tc‍‌");
    }

    [Test]
    public async Task Edit_commands_run_on_the_focused_input_and_can_run_only_where_their_key_would_do_something()
    {
        var entry = new Entry("hello");
        var probe = new EditProbe();
        var readOnly = false;
        var masked = false;
        using var driver = UiDriver.Render(ui =>
        {
            ui.Input(text: entry.Text, readOnly: readOnly, masked: masked, width: 200);
            ui.EditCommands(probe: probe);
        });
        driver.CaretAt(0);
        var fresh = probe.Can;
        driver.Type("d");
        probe.Run(driver, EditCommand.Undo);
        var undone = (entry.Value, probe.Can);
        probe.Run(driver, EditCommand.Redo);
        var redone = entry.Value;
        driver.CaretAt(1);
        probe.Run(driver, EditCommand.Delete);
        var deleted = entry.Value;
        for (var i = 0; i < 3; i++)
            driver.Press(Key.Right, Shift);
        var selected = probe.Can;
        probe.Run(driver, EditCommand.Copy);
        var copied = driver.Clipboard;
        probe.Run(driver, EditCommand.Cut);
        var cut = entry.Value;
        driver.Clipboard = "XY";
        probe.Run(driver, EditCommand.Paste);
        var pasted = entry.Value;
        probe.Run(driver, EditCommand.SelectAll);
        var all = driver.Selection();
        masked = true;
        driver.Rerender();
        var hidden = probe.Can;
        masked = false;
        readOnly = true;
        driver.Rerender();

        await Assert.That(fresh).IsEqualTo("paste select-all delete");
        await Assert.That(undone).IsEqualTo(("hello", "paste redo select-all delete"));
        await Assert.That((redone, deleted)).IsEqualTo(("dhello", "dello"));
        await Assert.That(selected).IsEqualTo("cut copy paste undo select-all delete");
        await Assert.That((copied, cut, pasted)).IsEqualTo(("ell", "do", "dXYo"));
        await Assert.That(all).IsEqualTo((0, 4));
        await Assert.That(hidden).IsEqualTo("paste select-all delete");
        await Assert.That(probe.Can).IsEqualTo("copy select-all");
    }

    [Test]
    public async Task A_run_of_typing_backspaces_or_deletes_undoes_as_one_step_and_a_caret_move_starts_another()
    {
        var entry = new Entry();
        using var driver = Bound(entry);
        driver.CaretAt(0);

        driver.Type("a");
        driver.Type("b");
        driver.Type("c");
        driver.Press(Key.Z, Ctrl);
        var typed = entry.Value;
        entry.Value = "abcd";
        driver.Settle();
        driver.Press(Key.End);
        driver.Press(Key.BackSpace);
        driver.Press(Key.BackSpace);
        driver.Press(Key.Z, Ctrl);
        var erased = entry.Value;
        driver.Press(Key.Home);
        driver.Press(Key.Delete);
        driver.Press(Key.Delete);
        driver.Press(Key.Z, Ctrl);
        var deleted = entry.Value;
        entry.Value = "";
        driver.Settle();
        driver.Type("a");
        driver.Type("b");
        driver.Press(Key.Left);
        driver.Press(Key.Right);
        driver.Type("c");
        driver.Press(Key.Z, Ctrl);

        await Assert.That((typed, erased, deleted)).IsEqualTo(("", "abcd", "abcd"));
        await Assert.That(entry.Value).IsEqualTo("ab");
    }

    [Test]
    public async Task Undo_gives_back_the_selection_an_edit_replaced_and_ctrl_y_or_ctrl_shift_z_redoes_it()
    {
        var entry = new Entry("hello");
        using var driver = Bound(entry);
        driver.CaretAt(1);
        for (var i = 0; i < 3; i++)
            driver.Press(Key.Right, Shift);

        driver.Type("x");
        driver.Press(Key.Z, Ctrl);
        var restored = (entry.Value, driver.Selection());
        driver.Press(Key.Y, Ctrl);
        var redone = entry.Value;
        driver.Press(Key.Z, Ctrl);
        driver.Press(Key.Z, Ctrl | Shift);
        var again = entry.Value;
        entry.Value = "";
        driver.Settle();
        driver.Type("first");
        driver.Press(Key.Left);
        driver.Type(" second");
        driver.Press(Key.Z, Ctrl);
        var first = entry.Value;
        driver.Press(Key.Y, Ctrl);
        var second = entry.Value;
        driver.Press(Key.Z, Ctrl);

        await Assert.That(restored).IsEqualTo(("hello", (1, 4)));
        await Assert.That((redone, again)).IsEqualTo(("hxo", "hxo"));
        await Assert.That((first, second)).IsEqualTo(("first", "firs secondt"));
        await Assert.That(entry.Value).IsEqualTo("first");
    }

    [Test]
    public async Task Enter_moves_to_the_next_field_that_can_take_the_focus_and_in_the_last_commits_before_it_submits()
    {
        var name = new Entry();
        var password = new Entry();
        var other = new Entry();
        var blank = new ObservableText();
        var log = new List<(string, string)>();
        using var driver = UiDriver.Render(ui =>
        {
            var next = ui.Ref();
            var off = ui.Ref();
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Input(
                    text: name.Text,
                    next: next,
                    onSubmit: Into(log, "name", name.Text),
                    width: 200,
                    id: "name"
                );
                ui.PasswordInput(
                    text: password.Text,
                    nodeRef: next,
                    onCommit: Into(log, "commit", password.Text),
                    onSubmit: Into(log, "submit", password.Text),
                    width: 200
                );
                ui.Input(
                    text: other.Text,
                    next: off,
                    onSubmit: Into(log, "other", other.Text),
                    width: 200,
                    id: "other"
                );
                ui.Input(text: blank, disabled: true, nodeRef: off, width: 200);
            }
        });
        driver.Focus(driver.TextOf("name"));

        driver.Press(Key.Enter);
        var moved = driver.Focused;
        driver.Type("hunter2");
        driver.Press(Key.Enter);
        driver.Focus(driver.TextOf("other"));
        driver.Type("x");
        driver.Press(Key.Enter);

        await Assert
            .That(moved)
            .IsSameReferenceAs(Nodes.Find(driver.Get<PasswordInput>(), NodeKind.EditableText));
        await Assert
            .That(log)
            .IsEquivalentTo([("commit", "hunter2"), ("submit", "hunter2"), ("other", "x")]);
        await Assert.That(driver.Focused).IsSameReferenceAs(driver.TextOf("other"));
    }

    [Test]
    public async Task Escape_with_a_handler_commits_then_runs_it_and_takes_the_key()
    {
        var entry = new Entry();
        var log = new List<(string, string)>();
        var seen = new List<Key>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(onKeyDown: Handler<KeyEvent>.Of(seen, static (s, ref e) => s.Add(e.Key))))
                ui.Input(
                    text: entry.Text,
                    onCommit: Into(log, "commit", entry.Text),
                    onEscape: Into(log, "escape", entry.Text),
                    width: 200
                );
        });
        driver.Focus(driver.TextOf());

        driver.Type("ab");
        var taken = driver.KeyDown(Key.Escape);
        driver.KeyUp(Key.Escape);

        await Assert.That(taken).IsTrue();
        await Assert.That(seen).IsEmpty();
        await Assert.That(log).IsEquivalentTo([("commit", "ab"), ("escape", "ab")]);
    }
}

/// <summary>Reads the view's edit commands as a row of buttons beside a field would, and runs them as their clicks
/// would.</summary>
internal sealed class EditProbe
{
    static readonly EditCommand[] All = Enum.GetValues<EditCommand>();
    static readonly string[] Names =
    [
        "cut",
        "copy",
        "paste",
        "undo",
        "redo",
        "select-all",
        "delete",
    ];

    readonly Command?[] _commands = new Command?[All.Length];
    readonly bool[] _can = new bool[All.Length];

    /// <summary>The commands that can run, by name, in <see cref="EditCommand"/> order.</summary>
    public string Can => string.Join(" ", Names.Where((_, i) => _can[i]));

    [FramePath]
    internal void Read(Ui ui)
    {
        for (var i = 0; i < All.Length; i++)
        {
            var command = ui.Command(All[i]);
            _commands[i] = command;
            _can[i] = ui.CanRun(command);
        }
    }

    public void Run(UiDriver driver, EditCommand command)
    {
        if (_commands[(int)command] is not { } run)
            throw new InvalidOperationException("No render has read the edit commands yet.");
        run.Run();
        driver.Settle();
    }
}

internal readonly record struct EditCommandsProps
{
    public required EditProbe Probe { get; init; }
}

/// <summary>Reads the view's edit commands into its probe as it renders.</summary>
internal sealed class EditCommands : Component<EditCommandsProps>
{
    protected override void Render(Ui ui, in EditCommandsProps p) => p.Probe.Read(ui);
}

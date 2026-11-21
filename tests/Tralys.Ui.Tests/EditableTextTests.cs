using System.Numerics;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class EditableTextTests
{
    static Callback Into(List<string> log, ObservableText text) =>
        Callback.Of(log, text, static (l, t) => l.Add(t.ToString()));

    static Callback<TextSelection> Into(List<TextSelection> log) =>
        Callback<TextSelection>.Of(log, static (l, s) => l.Add(s));

    [Test]
    public async Task Typing_writes_each_edit_into_the_text_once_and_keeps_the_undo_history()
    {
        var text = new ObservableText();
        var changes = new List<string>();
        text.Changed += () => changes.Add(text.ToString());
        using var driver = UiDriver.Render(ui =>
            ui.EditableText(text: text, id: "field", width: 200)
        );
        driver.Focus(driver.Get("field"));

        driver.Type("a");
        driver.Type("b");
        driver.Press(Key.Z, ModifierKeys.Control);

        await Assert.That(changes).IsEquivalentTo(["a", "ab", ""]);
        await Assert.That(text.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task On_edit_runs_once_after_each_edit_has_reached_the_text_and_never_for_a_change_from_outside()
    {
        var text = new ObservableText();
        var edits = new List<string>();
        using var driver = UiDriver.Render(ui =>
            ui.EditableText(
                text: text,
                onEdit: Callback.Of(text, edits, static (t, e) => e.Add(t.ToString())),
                id: "field",
                width: 200
            )
        );
        var field = driver.Get("field");
        driver.Focus(field);

        driver.Type("ab");
        driver.Type("c");
        driver.Press(Key.BackSpace);
        driver.Press(Key.Z, ModifierKeys.Control);
        text.Set("xyz");
        driver.Settle();

        await Assert.That(edits).IsEquivalentTo(["ab", "abc", "ab", "abc"]);
        await Assert.That(field.Text.ToString()).IsEqualTo("xyz");
    }

    [Test]
    public async Task An_edit_the_owner_sets_back_as_it_changes_is_replaced_by_the_text()
    {
        var text = new ObservableText();
        var reports = new List<string>();
        text.Changed += () =>
        {
            if (text.IsEmpty)
                return;
            reports.Add(text.ToString());
            text.Clear();
        };
        using var driver = UiDriver.Render(ui =>
            ui.EditableText(text: text, id: "field", width: 200)
        );
        var field = driver.Get("field");
        driver.Focus(field);

        driver.Type("ab");

        await Assert.That(reports).IsEquivalentTo(["ab"]);
        await Assert.That(field.Text.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task A_change_from_outside_is_one_undo_step_and_keeps_the_caret_where_the_text_reaches()
    {
        var text = new ObservableText("hello");
        var selections = new List<TextSelection>();
        using var driver = UiDriver.Render(ui =>
            ui.EditableText(
                text: text,
                onSelectionChange: Into(selections),
                id: "field",
                width: 200
            )
        );
        var field = driver.Get("field");
        driver.Focus(field);
        driver.Press(Key.End);

        text.Set("hi");
        driver.Settle();

        await Assert.That(field.Text.ToString()).IsEqualTo("hi");
        await Assert.That(selections[^1]).IsEqualTo(new TextSelection(2, 2));
        driver.Press(Key.Z, ModifierKeys.Control);
        await Assert.That(text.ToString()).IsEqualTo("hello");
    }

    [Test]
    public async Task A_run_of_changes_from_outside_undoes_as_one_step_and_one_ending_where_it_began_leaves_none()
    {
        var dragged = new ObservableText("10");
        var returned = new ObservableText("10");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.EditableText(text: dragged, id: "dragged", width: 200);
                ui.EditableText(text: returned, id: "returned", width: 200);
            }
        });

        driver.Focus(driver.Get("dragged"));
        driver.Press(Key.End);
        foreach (var value in (string[])["20", "30", "40"])
        {
            dragged.Set(value);
            driver.Settle();
        }
        driver.Press(Key.Z, ModifierKeys.Control);

        driver.Focus(driver.Get("returned"));
        driver.Press(Key.End);
        driver.Type("5");
        foreach (var value in (string[])["70", "105"])
        {
            returned.Set(value);
            driver.Settle();
        }
        driver.Press(Key.Z, ModifierKeys.Control);

        await Assert.That(dragged.ToString()).IsEqualTo("10");
        await Assert.That(returned.ToString()).IsEqualTo("10");
    }

    [Test]
    public async Task Code_focus_selects_all_and_a_click_puts_the_caret_where_it_lands()
    {
        var selections = new List<TextSelection>();
        var text = new ObservableText("hello world");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.EditableText(
                    text: text,
                    onSelectionChange: Into(selections),
                    id: "field",
                    width: 200
                );
                using (ui.Box(focusable: true, width: 20, height: 20, id: "other")) { }
            }
        });
        var field = driver.Get("field");

        driver.Focus(field);
        await Assert.That(selections[^1]).IsEqualTo(new TextSelection(0, 11));

        driver.Click(driver.Get("other"));
        driver.Click(field, new Vector2(1f, field.ViewBounds.Height / 2f));
        await Assert.That(selections[^1]).IsEqualTo(new TextSelection(0, 0));
    }

    [Test]
    public async Task Keys_the_field_cannot_use_now_go_on_to_its_ancestors()
    {
        var seen = new List<Key>();
        var text = new ObservableText("hello");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(onKeyDown: Handler<KeyEvent>.Of(seen, static (s, ref e) => s.Add(e.Key))))
                ui.EditableText(text: text, id: "field", width: 200);
        });
        driver.Focus(driver.Get("field"));

        driver.Press(Key.Z, ModifierKeys.Control);
        driver.Press(Key.Escape);
        driver.Press(Key.Up);
        driver.Press(Key.Tab);
        driver.Press(Key.Left, ModifierKeys.Alt);
        driver.Press(Key.Left);
        driver.Press(Key.BackSpace);

        await Assert.That(seen).IsEquivalentTo([Key.Z, Key.Escape, Key.Up, Key.Tab, Key.Left]);
    }

    [Test]
    public async Task A_key_that_types_stays_with_an_editable_field_before_any_shortcut_and_a_read_only_one_passes_it_on()
    {
        var ran = new List<string>();
        var readOnly = false;
        var text = new ObservableText();
        using var driver = UiDriver.Render(ui =>
        {
            ui.Shortcut(Chord.Of(Key.Q), Callback.Of(ran, static r => r.Add("shortcut")));
            ui.EditableText(text: text, readOnly: readOnly, id: "field", width: 200);
        });
        driver.Focus(driver.Get("field"));

        driver.KeyDown(Key.Q);
        driver.Type("q");
        driver.KeyUp(Key.Q);
        var typed = driver.Get("field").Text.ToString();
        var ranWhileEditable = ran.Count;
        readOnly = true;
        driver.Rerender();
        driver.Press(Key.Q);

        await Assert.That(typed).IsEqualTo("q");
        await Assert.That(ranWhileEditable).IsEqualTo(0);
        await Assert.That(ran).IsEquivalentTo(["shortcut"]);
    }

    [Test]
    public async Task Enter_moves_to_the_next_field_else_commits_then_submits()
    {
        var first = new ObservableText();
        var second = new ObservableText();
        var log = new List<(string Kind, string Text)>();
        using var driver = UiDriver.Render(ui =>
        {
            var next = ui.Ref();
            using (ui.Box(direction: Direction.Column))
            {
                ui.EditableText(text: first, next: next, id: "first", width: 200);
                ui.EditableText(
                    text: second,
                    onCommit: Callback.Of(
                        log,
                        second,
                        static (l, t) => l.Add(("commit", t.ToString()))
                    ),
                    onSubmit: Callback.Of(
                        log,
                        second,
                        static (l, t) => l.Add(("submit", t.ToString()))
                    ),
                    nodeRef: next,
                    id: "second",
                    width: 200
                );
            }
        });
        driver.Focus(driver.Get("first"));

        driver.Press(Key.Enter);
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("second"));

        driver.Type("x");
        driver.Press(Key.Enter);
        await Assert.That(log).IsEquivalentTo([("commit", "x"), ("submit", "x")]);
    }

    [Test]
    public async Task The_clipboard_is_the_hosts_and_a_password_copies_nothing()
    {
        var text = new ObservableText("hello");
        var secret = new ObservableText("secret");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
            {
                ui.EditableText(text: text, id: "field", width: 200);
                ui.EditableText(
                    text: secret,
                    kind: TextInputKind.Password,
                    id: "password",
                    width: 200
                );
            }
        });
        driver.Focus(driver.Get("field"));

        driver.Press(Key.X, ModifierKeys.Control);
        await Assert.That(driver.Clipboard).IsEqualTo("hello");
        await Assert.That(text.ToString()).IsEqualTo("");

        driver.Clipboard = "XY";
        driver.Press(Key.V, ModifierKeys.Control);
        await Assert.That(text.ToString()).IsEqualTo("XY");

        driver.Focus(driver.Get("password"));
        driver.Press(Key.C, ModifierKeys.Control);
        await Assert.That(driver.Clipboard).IsEqualTo("XY");
    }

    [Test]
    public async Task A_read_only_field_selects_and_copies_but_takes_no_edit()
    {
        var text = new ObservableText("abcd");
        var reports = new List<string>();
        text.Changed += () => reports.Add(text.ToString());
        var seen = new List<Key>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(onKeyDown: Handler<KeyEvent>.Of(seen, static (s, ref e) => s.Add(e.Key))))
                ui.EditableText(text: text, readOnly: true, id: "field", width: 200);
        });
        driver.Focus(driver.Get("field"));

        driver.Type("x");
        driver.Press(Key.BackSpace);
        driver.Press(Key.C, ModifierKeys.Control);

        await Assert.That(reports).IsEmpty();
        await Assert.That(seen).IsEquivalentTo([Key.BackSpace]);
        await Assert.That(driver.Clipboard).IsEqualTo("abcd");
    }

    [Test]
    public async Task The_host_types_into_the_focused_field_until_it_blurs()
    {
        var text = new ObservableText();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.EditableText(text: text, kind: TextInputKind.Name, id: "field", width: 100);
                using (ui.Box(focusable: true, width: 20, height: 20, id: "other")) { }
            }
        });
        var field = driver.Get("field");

        driver.Focus(field);
        var input = await Assert.That(driver.TextInput).IsNotNull();
        await Assert.That(input.Kind).IsEqualTo(TextInputKind.Name);
        await Assert.That(input.ByContact).IsFalse();
        await Assert
            .That(field.ViewBounds.Contains(new Vector2(input.Area.X, input.Area.Y)))
            .IsTrue();

        driver.Click(driver.Get("other"));
        await Assert.That(driver.TextInput).IsNull();
    }

    [Test]
    public async Task The_hosts_text_input_follows_the_caret_as_it_types_without_starting_again_and_a_touch_on_a_field_asks_for_it_by_contact()
    {
        var text = new ObservableText();
        var other = new ObservableText();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 10))
            {
                ui.EditableText(text: text, id: "field", width: 200);
                ui.EditableText(text: other, id: "other", width: 200);
            }
        });
        driver.Focus(driver.Get("field"));
        var focused = await Assert.That(driver.TextInput).IsNotNull();

        driver.Type("abc");
        var typed = await Assert.That(driver.TextInput).IsNotNull();
        driver.Tap(driver.Get("field"));
        var tapped = await Assert.That(driver.TextInput).IsNotNull();
        driver.Tap(driver.Get("other"));

        await Assert.That(typed.Area.X).IsGreaterThan(focused.Area.X);
        await Assert.That((focused.ByContact, typed.Starts)).IsEqualTo((false, 1));
        await Assert.That((tapped.ByContact, tapped.Starts)).IsEqualTo((true, 2));
        var switched = await Assert.That(driver.TextInput).IsNotNull();
        await Assert.That(switched.ByContact).IsTrue();
    }

    [Test]
    public async Task The_caret_blinks_on_frame_time_and_is_patched_rather_than_rebuilt()
    {
        var text = new ObservableText();
        using var driver = UiDriver.Render(ui =>
            ui.EditableText(text: text, id: "field", width: 100)
        );
        var field = driver.Get("field");
        driver.Focus(field);
        var editor = await Assert.That(field.Payload as EditorPayload).IsNotNull();

        driver.Frame(0.6f);
        await Assert.That(editor.CaretOn).IsFalse();
        await Assert.That(driver.Stats.DrawListRebuilt).IsFalse();
        await Assert.That(driver.Stats.Paints).IsEqualTo(0);

        driver.Frame(0.5f);
        await Assert.That(editor.CaretOn).IsTrue();
        await Assert.That(driver.Stats.DrawListRebuilt).IsFalse();
    }

    [Test]
    public async Task Blur_commits_an_edit_once_and_so_does_leaving_the_view()
    {
        var text = new ObservableText();
        var commits = new List<string>();
        var shown = true;
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                if (shown)
                    ui.EditableText(
                        text: text,
                        onCommit: Into(commits, text),
                        id: "field",
                        width: 100
                    );
                using (ui.Box(focusable: true, width: 20, height: 20, id: "other")) { }
            }
        });
        var field = driver.Get("field");
        driver.Focus(field);
        driver.Type("ab");

        driver.Click(driver.Get("other"));
        driver.Focus(field);
        driver.Click(driver.Get("other"));
        await Assert.That(commits).IsEquivalentTo(["ab"]);

        driver.Focus(field);
        driver.Type("c");
        shown = false;
        driver.Rerender();
        await Assert.That(commits).IsEquivalentTo(["ab", "c"]);
    }

    [Test]
    public async Task Autofocus_takes_the_focus_selecting_all_but_not_under_touch_density()
    {
        var selections = new List<TextSelection>();
        var prefilled = new ObservableText("prefilled");
        using var driver = UiDriver.Render(ui =>
            ui.EditableText(
                text: prefilled,
                autoFocus: true,
                onSelectionChange: Into(selections),
                id: "field",
                width: 200
            )
        );
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("field"));
        await Assert.That(selections[^1]).IsEqualTo(new TextSelection(0, 9));

        using var touch = UiDriver.Render(
            ui => ui.EditableText(text: prefilled, autoFocus: true, id: "field", width: 200),
            density: Theming.Density.Touch
        );
        await Assert.That(touch.Focused).IsNull();
    }

    [Test]
    public async Task A_double_click_selects_a_word_and_a_drag_extends_past_the_field()
    {
        var selections = new List<TextSelection>();
        var text = new ObservableText("hello");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(padding: 100))
                ui.EditableText(text: text, onSelectionChange: Into(selections), id: "field");
        });
        var field = driver.Get("field");

        driver.DoubleClick(field);
        await Assert.That(selections[^1]).IsEqualTo(new TextSelection(0, 5));

        driver.Drag(field, new Vector2(-80f, 0f));
        await Assert.That(selections[^1].Caret).IsEqualTo(0);
        await Assert.That(selections[^1].Anchor).IsGreaterThan(0);
    }

    [Test]
    public async Task A_caret_past_the_fields_width_scrolls_into_view()
    {
        var text = new ObservableText("a line of text far longer than the field");
        using var driver = UiDriver.Render(ui =>
            ui.EditableText(text: text, id: "field", width: 60)
        );
        var field = driver.Get("field");
        driver.Focus(field);

        driver.Press(Key.End);

        var input = await Assert.That(driver.TextInput).IsNotNull();
        var area = input.Area;
        await Assert.That(area.Right).IsLessThanOrEqualTo(field.ViewBounds.Right);
        await Assert.That(area.X).IsGreaterThan(field.ViewBounds.X);
    }

    [Test]
    public async Task The_field_wants_its_texts_width_and_one_caret_width()
    {
        var text = new ObservableText("hello");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Text("hello", id: "text");
                ui.EditableText(text: text, id: "field");
            }
        });

        await Assert
            .That(driver.Get("field").Bounds.Width)
            .IsEqualTo(driver.Get("text").Bounds.Width + 1f);
    }

    [Test]
    public async Task A_masked_field_maps_a_click_through_its_dots()
    {
        var selections = new List<TextSelection>();
        var text = new ObservableText("a\U0001F600b");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
                ui.EditableText(
                    text: text,
                    masked: true,
                    onSelectionChange: Into(selections),
                    id: "field"
                );
        });
        var field = driver.Get("field");

        driver.Click(field, new Vector2(field.ViewBounds.Width - 1f, field.ViewBounds.Height / 2f));

        await Assert.That(selections[^1]).IsEqualTo(new TextSelection(4, 4));
    }
}

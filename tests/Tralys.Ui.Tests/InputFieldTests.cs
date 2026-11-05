using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class InputFieldTests
{
    [Test]
    public async Task An_input_autofocuses_takes_its_edits_into_its_text_and_submits_on_enter()
    {
        var submits = new List<string>();
        using var driver = UiDriver.Render(ui =>
        {
            var text = ui.Remember(static () => new ObservableText());
            ui.Input(text: text, autoFocus: true, onSubmit: Fields.Into(submits, text));
        });

        driver.Type("ab");
        driver.Press(Key.Enter);

        await Assert
            .That(driver.Focused)
            .IsSameReferenceAs(Nodes.Find(driver.Get<Input>(), NodeKind.EditableText));
        await Assert.That(submits).IsEquivalentTo(["ab"]);
    }

    [Test]
    public async Task A_press_on_an_inputs_padding_puts_the_caret_at_the_nearest_end_of_its_text()
    {
        using var driver = UiDriver.Render(ui =>
        {
            var text = ui.Remember(static () => new ObservableText("hello"));
            ui.Input(text: text, width: 200);
        });
        var input = driver.Get<Input>();
        var root = Nodes.Find(input, NodeKind.Box).ViewBounds;

        driver.Click(input, new Vector2(3, 3));
        driver.Type("<");
        driver.Click(input, new Vector2(root.Width - 3, root.Height - 3));
        driver.Type(">");

        await Assert
            .That(Nodes.Find(input, NodeKind.EditableText).Text.ToString())
            .IsEqualTo("<hello>");
    }

    [Test]
    public async Task A_press_above_or_below_a_multiline_inputs_text_lands_on_its_first_or_last_line()
    {
        var lines = new ObservableText("abcdefgh\nijklmnop");
        using var driver = UiDriver.Render(ui =>
            ui.Input(text: lines, multiline: true, width: 200)
        );
        var input = driver.Get<Input>();
        var root = Nodes.Find(input, NodeKind.Box).ViewBounds;
        var text = Nodes.Find(input, NodeKind.EditableText).ViewBounds;
        var x = text.X - root.X + 30;

        driver.Click(input, new Vector2(x, text.Y - root.Y + 4));
        var first = driver.TextInput!.Value.Area;
        driver.Click(input, new Vector2(x, 3));
        var above = driver.TextInput!.Value.Area;
        driver.Click(input, new Vector2(x, text.Bottom - root.Y - 4));
        var last = driver.TextInput!.Value.Area;
        driver.Click(input, new Vector2(x, root.Height - 3));
        var below = driver.TextInput!.Value.Area;

        await Assert.That(above).IsEqualTo(first);
        await Assert.That(below).IsEqualTo(last);
        await Assert.That(last.Y).IsGreaterThan(first.Y);
    }

    [Test]
    public async Task An_invalid_input_keeps_its_danger_border_while_focused()
    {
        var blank = new ObservableText();
        using var driver = UiDriver.Render(ui => ui.Input(text: blank, invalid: true, width: 200));
        var input = driver.Get<Input>();

        driver.Focus(Nodes.Find(input, NodeKind.EditableText));

        await Assert
            .That(Nodes.Find(input, NodeKind.Box).Visual.BorderPaint)
            .IsEqualTo((Paint)Themes.Dark.Color(ColorToken.BorderDanger));
    }
}

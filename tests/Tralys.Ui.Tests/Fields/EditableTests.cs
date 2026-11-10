using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class EditableTests
{
    static Paint Dark(ColorToken token) => Themes.Dark.Color(token);

    static UiDriver Render(
        Entry entry,
        bool disabled = false,
        Density density = default,
        bool? editing = null
    ) =>
        UiDriver.Render(
            ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8, padding: 8))
                {
                    ui.Editable(
                        value: entry.Value,
                        onValueChange: entry.Set,
                        editing: editing,
                        disabled: disabled,
                        width: 200,
                        id: "editable"
                    );
                    using (ui.Box(focusable: true, width: 40, height: 20, id: "other")) { }
                }
            },
            density: density
        );

    static bool Editing(UiDriver driver) => driver.All<Input>().Count == 1;

    [Test]
    public async Task A_press_from_elsewhere_released_over_it_or_a_right_click_leaves_it_shut()
    {
        var entry = new Entry("Old");
        using var driver = Render(entry);
        var editable = driver.Get("editable");

        var held = driver.Hold(driver.Get("other"));
        held.MoveTo(editable);
        held.Release();
        var dragged = Editing(driver);
        driver.Click(editable, MouseButton.Right);

        await Assert.That((dragged, Editing(driver))).IsEqualTo((false, false));
    }

    [Test]
    public async Task Enter_or_f2_opens_it_selecting_all_enter_commits_escape_drops_the_edit_and_the_focus_leaving_commits()
    {
        var entry = new Entry("Old");
        using var driver = Render(entry);
        var editable = driver.Get("editable");
        driver.Focus(editable);

        driver.Press(Key.Enter);
        var opened = (Editing(driver), driver.Focused == driver.TextOf(), driver.Selection());
        driver.Press(Key.Enter);
        var unchanged = (Editing(driver), entry.Reports.Count);
        driver.Press(Key.Enter);
        driver.Type("New");
        driver.Press(Key.Enter);
        var committed = (Editing(driver), driver.Focused);
        driver.Press(Key.F2);
        driver.Type("Gone");
        driver.Press(Key.Escape);
        var dropped = (Editing(driver), driver.Focused);
        driver.Press(Key.Enter);
        driver.Type("Kept");
        driver.Click(driver.Get("other"));

        await Assert.That(opened).IsEqualTo((true, true, (0, 3)));
        await Assert.That(unchanged).IsEqualTo((false, 0));
        await Assert.That(committed).IsEqualTo((false, editable));
        await Assert.That(dropped).IsEqualTo((false, editable));
        await Assert.That(entry.Reports).IsEquivalentTo(["New", "Kept"]);
        await Assert.That(Editing(driver)).IsFalse();
    }

    [Test]
    public async Task Under_touch_a_tap_opens_it_with_the_focus_in_its_input_and_a_tap_away_commits()
    {
        var entry = new Entry("Old");
        using var driver = Render(entry, density: Density.Touch);

        driver.Tap(driver.Get("editable"));
        var focused = driver.Focused == driver.TextOf();
        driver.Type("New");
        driver.Tap(driver.Get("other"));

        await Assert.That(focused).IsTrue();
        await Assert.That(entry.Reports).IsEquivalentTo(["New"]);
        await Assert.That(Editing(driver)).IsFalse();
    }

    [Test]
    public async Task A_disabled_editable_opens_on_no_click_tap_or_key_and_shows_no_i_beam()
    {
        var entry = new Entry("Old");
        using var driver = Render(entry, disabled: true);
        var editable = driver.Get("editable");

        driver.Click(editable);
        driver.Tap(editable);
        driver.Focus(editable);
        driver.Press(Key.F2);
        driver.Hover(editable);

        await Assert.That(Editing(driver)).IsFalse();
        await Assert.That(driver.Cursor).IsNotSameReferenceAs(Cursors.IBeam);
    }

    [Test]
    public async Task One_mounted_editing_starts_its_input_from_its_value()
    {
        var entry = new Entry("Old");
        using var driver = Render(entry, editing: true);

        var shown = driver.Shown();
        driver.Press(Key.Enter);

        await Assert.That(shown).IsEqualTo("Old");
        await Assert.That(driver.Shown()).IsEqualTo("Old");
        await Assert.That(entry.Reports).IsEmpty();
    }

    [Test]
    public async Task It_shows_what_its_source_kept_of_an_edit()
    {
        var hex = new Entry("#000000");
        using var driver = UiDriver.Render(ui =>
            ui.Editable(
                value: hex.Value,
                onValueChange: Callback<string>.Of(
                    hex,
                    static (h, typed) => h.Value = typed.ToUpperInvariant()
                ),
                id: "editable"
            )
        );
        driver.Focus(driver.Get("editable"));

        driver.Press(Key.F2);
        driver.Type("#3399ff");
        driver.Press(Key.Enter);

        await Assert.That(driver.GetText("#3399FF")).IsNotNull();
    }

    [Test]
    public async Task Its_input_takes_its_limit_and_danger_outline_in_the_box_its_text_showed_in()
    {
        var entry = new Entry("Old");
        using var driver = UiDriver.Render(ui =>
            ui.Editable(
                value: entry.Value,
                onValueChange: entry.Set,
                maxLength: 3,
                invalid: true,
                width: 200,
                id: "editable"
            )
        );
        var editable = driver.Get("editable");
        var shut = editable.Bounds;

        driver.Click(editable);
        driver.Type("abcd");

        await Assert.That(driver.Shown()).IsEqualTo("abc");
        await Assert
            .That(driver.RootOf().Visual.BorderPaint)
            .IsEqualTo(Dark(ColorToken.BorderDanger));
        await Assert.That(driver.RootOf().ViewBounds).IsEqualTo(editable.ViewBounds);
        await Assert.That(editable.Bounds).IsEqualTo(shut);
    }

    [Test]
    public async Task It_lights_while_hovered_outlines_while_focused_and_ends_a_long_text_in_an_ellipsis_or_shows_its_placeholder()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Editable(
                    value: "A name far too long for the room it has",
                    width: 120,
                    id: "long"
                );
                ui.Editable(placeholder: "Add a note", width: 120, id: "empty");
            }
        });
        var display = Display(driver.Get("long"));
        var text = Text(display);
        var rest = (display.Visual.Background, text.Inherit.Color);

        driver.Hover(driver.Get("long"));
        var lit = (display.Visual.Background, text.Inherit.Color);
        driver.Unhover();
        driver.Focus(driver.Get("long"));

        await Assert.That(rest).IsEqualTo((default(Paint), Dark(ColorToken.FgMuted)));
        await Assert.That(lit).IsEqualTo((Dark(ColorToken.StateHover), Dark(ColorToken.FgStrong)));
        await Assert.That(display.Visual.BorderPaint).IsEqualTo(Dark(ColorToken.BorderFocus));
        await Assert.That(text.Bounds.Width).IsLessThan(120f);
        await Assert
            .That(driver.GetText("Add a note").Inherit.Color)
            .IsEqualTo(Dark(ColorToken.FgPlaceholder));
    }

    static Node Display(Node root)
    {
        foreach (var child in root.Children)
            return child;
        throw new InvalidOperationException("No display.");
    }

    static Node Text(Node display) => Nodes.Find(display, NodeKind.Text);
}

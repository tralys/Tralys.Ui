using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;
using static Tralys.Ui.Tests.Rows;

namespace Tralys.Ui.Tests;

public sealed class MultiSelectTests
{
    static UiDriver Render(
        ListPick pick,
        IItemsSource<string>? items = null,
        Func<string, bool>? disabled = null,
        bool dialog = false,
        Slot<string> item = default
    )
    {
        items ??= pick.Items;
        pick.Items = items;
        return UiDriver.Render(
            ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8, padding: 8))
                {
                    if (dialog)
                        using (
                            ui.Layer(
                                modal: true,
                                dismissOn: Dismiss.Escape,
                                onDismiss: Callback.Of(pick, static p => p.Reports.Add("dialog")),
                                direction: Direction.Column,
                                align: Align.Start,
                                padding: 40
                            )
                        )
                            Field(ui, pick, items, disabled, item);
                    else
                        Field(ui, pick, items, disabled, item);
                    using (ui.Box(width: 40, height: 20, focusable: true, id: "after")) { }
                }
            },
            new Size(400, 500)
        );
    }

    [FramePath]
    static void Field(
        Ui ui,
        ListPick pick,
        IItemsSource<string> items,
        Func<string, bool>? disabled,
        Slot<string> item,
        UiKey key = default,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    ) =>
        ui.MultiSelect(
            items: items,
            itemKey: Words.ByText,
            itemText: Words.Text,
            item: item,
            itemDisabled: disabled,
            value: pick.Selection,
            onValueChange: pick.OnSelectionChange,
            open: pick.Open,
            onOpenChange: pick.OnOpenChange,
            placeholder: "Any",
            width: 200,
            key: key,
            file: file,
            line: line
        );

    static Node FieldOf(UiDriver driver) => driver.Get<MultiSelect<string>>();

    static Node Root(UiDriver driver)
    {
        foreach (var child in FieldOf(driver).Children)
            return child;
        throw new InvalidOperationException("No field.");
    }

    static string Summary(UiDriver driver)
    {
        var texts = new List<string>();
        Collect(Root(driver), texts);
        return string.Join(" ", texts);
    }

    static void Collect(Node node, List<string> texts)
    {
        foreach (var child in node.Children)
        {
            if (child.Kind == NodeKind.Text)
                texts.Add(child.Text.ToString());
            Collect(child, texts);
        }
    }

    static Node Of(UiDriver driver, string text)
    {
        for (Node? n = InDropdown(driver.View.Layers[0], text); n is not null; n = n.Parent)
        {
            if (n.RowFocusable)
                return n;
        }
        throw new InvalidOperationException($"No row '{text}'.");
    }

    static Node? InDropdown(Node node, string text)
    {
        foreach (var child in node.Children)
        {
            if (child.SpareRow)
                continue;
            if (child.Kind == NodeKind.Text && child.Text.SequenceEqual(text))
                return child;
            if (InDropdown(child, text) is { } found)
                return found;
        }
        return null;
    }

    static bool IsSelected(UiDriver driver, string text) =>
        (Of(driver, text).States & InputStates.Selected) != 0;

    static bool Lit(UiDriver driver, string text) =>
        Of(driver, text).Visual.Background == Dark(ColorToken.StateHover);

    [Test]
    public async Task A_press_on_the_field_opens_it_on_its_first_enabled_row()
    {
        var pick = new ListPick();
        using var driver = Render(pick, disabled: static text => text == "a");

        driver.Click(FieldOf(driver));

        await Assert.That(pick.Reports).IsEquivalentTo(["open"]);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
        await Assert.That(driver.Focused).IsSameReferenceAs(Of(driver, "b"));
        await Assert.That(Lit(driver, "b")).IsTrue();
    }

    [Test]
    public async Task A_press_on_a_row_ticks_or_unticks_it_leaving_it_open_and_the_field_shows_the_first_ticked_and_how_many_more()
    {
        var items = Words.Of("Read", "Write", "Review", "Deploy", "Admin");
        var pick = new ListPick { Items = items };
        pick.Selection.Apply(
            new SelectionChange(SelectionAction.Toggle, "Write", "Write"),
            items,
            Words.ByText
        );
        pick.Selection.Apply(
            new SelectionChange(SelectionAction.Toggle, "Deploy", "Deploy"),
            items,
            Words.ByText
        );
        using var driver = Render(pick, items);
        var closed = Summary(driver);

        driver.Click(FieldOf(driver));
        driver.Click(Of(driver, "Read"));
        var added = (Summary(driver), IsSelected(driver, "Read"), driver.View.Layers.Length);
        driver.Click(Of(driver, "Read"));

        await Assert.That(closed).IsEqualTo("Write +1");
        await Assert.That(added).IsEqualTo(("Read +2", true, 1));
        await Assert.That(Summary(driver)).IsEqualTo("Write +1");
        await Assert.That(pick.Selected()).IsEqualTo("Deploy Write");
    }

    [Test]
    public async Task Nothing_ticked_shows_the_placeholder_and_a_tick_from_outside_or_a_new_item_reaches_the_field()
    {
        var words = Words.List("Read");
        var pick = new ListPick();
        using var driver = Render(pick, ItemsSource.From(words));
        var empty = Summary(driver);

        pick.Selection.Apply(
            new SelectionChange(SelectionAction.Toggle, "Write", "Write"),
            pick.Items,
            Words.ByText
        );
        driver.Settle();
        var missing = Summary(driver);
        words.Add("Write");
        driver.Settle();

        await Assert.That(empty).IsEqualTo("Any");
        await Assert.That(missing).IsEqualTo("Any");
        await Assert.That(Summary(driver)).IsEqualTo("Write");
    }

    [Test]
    public async Task An_item_inserted_above_a_ticked_row_leaves_that_row_ticked()
    {
        var words = Words.List("Read", "Write");
        var pick = new ListPick();
        using var driver = Render(pick, ItemsSource.From(words));
        pick.Selection.Apply(
            new SelectionChange(SelectionAction.Toggle, "Write", "Write"),
            pick.Items,
            Words.ByText
        );
        driver.Click(FieldOf(driver));

        words.Insert(0, "Review");
        driver.Settle();

        await Assert.That(IsSelected(driver, "Review")).IsFalse();
        await Assert.That(IsSelected(driver, "Read")).IsFalse();
        await Assert.That(IsSelected(driver, "Write")).IsTrue();
    }

    [Test]
    public async Task A_press_outside_closes_it_and_ticks_nothing()
    {
        var pick = new ListPick();
        using var driver = Render(pick);
        driver.Click(FieldOf(driver));

        driver.Click(new Vector2(380, 480));

        await Assert.That(pick.Reports).IsEquivalentTo(["open", "close"]);
        await Assert.That(pick.Toggled).IsEmpty();
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    [Test]
    public async Task A_moving_pointer_lights_and_focuses_its_row_down_moves_the_light_and_a_row_wheeled_under_a_still_pointer_stays_dark()
    {
        var pick = new ListPick();
        using var driver = Render(pick, ItemsSource.From(Words.Numbered(40)));
        driver.Click(FieldOf(driver));

        driver.Hover(Of(driver, "3"));
        var hovered = (driver.Focused == Of(driver, "3"), Lit(driver, "3"));
        driver.Press(Key.Down);
        var moved = (Lit(driver, "3"), Lit(driver, "4"));
        var point = Of(driver, "3").ViewBounds.Center();
        driver.Wheel(Of(driver, "3"), new Vector2(0, -1));
        var under = RowAt(driver, point);

        await Assert.That(hovered).IsEqualTo((true, true));
        await Assert.That(moved).IsEqualTo((false, true));
        await Assert.That(under).IsNotSameReferenceAs(Of(driver, "3"));
        await Assert.That(under.Visual.Background).IsNotEqualTo(Dark(ColorToken.StateHover));
        await Assert.That(driver.Focused).IsSameReferenceAs(Of(driver, "4"));
    }

    [Test]
    public async Task A_row_lit_before_it_closed_is_dark_when_it_opens_again()
    {
        var pick = new ListPick();
        using var driver = Render(pick);
        driver.Click(FieldOf(driver));
        driver.Hover(Of(driver, "c"));

        driver.Press(Key.Escape);
        driver.Unhover();
        driver.Focus(Root(driver));
        driver.Press(Key.Down);

        await Assert.That(Lit(driver, "a")).IsTrue();
        await Assert.That(Lit(driver, "c")).IsFalse();
    }

    [Test]
    public async Task Down_space_or_enter_on_the_closed_field_opens_it()
    {
        var keys = new List<string>();
        foreach (var key in (Key[])[Key.Down, Key.Space, Key.Enter])
        {
            var pick = new ListPick();
            using var driver = Render(pick);
            driver.Focus(FieldOf(driver));
            driver.Press(key);
            keys.Add(string.Join(" ", pick.Reports));
        }

        await Assert.That(keys).IsEquivalentTo(["open", "open", "open"]);
    }

    [Test]
    public async Task The_keys_step_the_focus_over_the_enabled_rows_stopping_at_the_ends()
    {
        var pick = new ListPick();
        using var driver = Render(
            pick,
            Words.Of("a", "b", "c", "d", "e"),
            disabled: static text => text == "c"
        );
        driver.Click(FieldOf(driver));
        var steps = new List<string?>();

        foreach (
            var key in (Key[])
                [
                    Key.Up,
                    Key.Down,
                    Key.Down,
                    Key.Down,
                    Key.Down,
                    Key.Up,
                    Key.Up,
                    Key.Home,
                    Key.End,
                    Key.PageUp,
                    Key.PageDown,
                ]
        )
        {
            driver.Press(key);
            steps.Add(driver.Focused is { } focused ? FirstText(focused) : null);
        }

        await Assert.That(string.Join(" ", steps)).IsEqualTo("a b d e e d b a e a e");
    }

    [Test]
    public async Task Space_or_enter_on_the_focused_row_ticks_it_and_the_tick_shows_only_while_the_value_holds_it()
    {
        var pick = new ListPick();
        using var driver = Render(pick);
        driver.Click(FieldOf(driver));
        driver.Press(Key.Down);

        driver.Press(Key.Space);
        var ticked = (
            pick.Selected(),
            IsSelected(driver, "b"),
            Tick(driver, "b"),
            Tick(driver, "a")
        );
        driver.Press(Key.Enter);

        await Assert.That(ticked).IsEqualTo(("b", true, true, false));
        await Assert.That(pick.Selected()).IsEqualTo("");
        await Assert.That(Tick(driver, "b")).IsFalse();
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
    }

    [Test]
    public async Task With_no_items_down_is_taken_and_it_stays_open()
    {
        var pick = new ListPick();
        using var driver = Render(pick, Words.Of());
        driver.Click(FieldOf(driver));

        var taken = driver.KeyDown(Key.Down);
        driver.KeyUp(Key.Down);

        await Assert.That(taken).IsTrue();
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
    }

    [Test]
    public async Task Escape_closes_it_handing_the_focus_back_to_the_field_and_inside_a_dialog_closes_only_the_dropdown()
    {
        var pick = new ListPick();
        using var driver = Render(pick, dialog: true);
        driver.Click(FieldOf(driver));
        driver.Press(Key.Down);

        driver.Press(Key.Escape);

        await Assert.That(pick.Reports).IsEquivalentTo(["open", "close"]);
        await Assert.That(driver.Focused).IsSameReferenceAs(Root(driver));
    }

    [Test]
    public async Task Tab_and_shift_tab_keep_the_focus_inside()
    {
        var pick = new ListPick();
        using var driver = Render(pick);
        driver.Click(FieldOf(driver));

        driver.Tab();
        driver.Tab(shift: true);

        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
        await Assert.That(pick.Reports).IsEquivalentTo(["open"]);
        await Assert.That(driver.Focused).IsSameReferenceAs(Of(driver, "a"));
    }

    [Test]
    public async Task The_window_deactivating_leaves_it_open()
    {
        var pick = new ListPick();
        using var driver = Render(pick);
        driver.Click(FieldOf(driver));

        driver.WindowFocus(false);
        driver.WindowFocus(true);

        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
        await Assert.That(pick.Reports).IsEquivalentTo(["open"]);
    }

    [Test]
    public async Task Its_closed_field_wears_an_inputs_fill_and_border_enabled_or_not()
    {
        var off = false;
        var pick = new ListPick();
        var blank = new ObservableText();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.MultiSelect(
                    items: pick.Items,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    disabled: off,
                    width: 200
                );
                ui.Input(text: blank, disabled: off, width: 200, id: "input");
            }
        });
        var field = Root(driver);
        var enabled = (
            field.Visual.Background == driver.Get("input").Visual.Background,
            field.Visual.BorderPaint == driver.Get("input").Visual.BorderPaint
        );
        off = true;
        driver.Rerender();

        await Assert.That(enabled).IsEqualTo((true, true));
        await Assert.That(field.Visual.Background).IsEqualTo(driver.Get("input").Visual.Background);
        await Assert
            .That(field.Visual.BorderPaint)
            .IsEqualTo(driver.Get("input").Visual.BorderPaint);
    }

    [Test]
    public async Task An_item_renderer_shows_the_first_ticked_item_in_the_closed_field()
    {
        var pick = new ListPick();
        pick.Selection.Apply(
            new SelectionChange(SelectionAction.Toggle, "c", "c"),
            pick.Items,
            Words.ByText
        );
        using var driver = Render(
            pick,
            item: Slot<string>.Of(static (ui, text, _) => ui.Text(text.ToUpperInvariant()))
        );

        await Assert.That(Summary(driver)).IsEqualTo("C");
    }

    [Test]
    public async Task It_stands_its_rungs_height_at_every_size_in_every_theme_and_density()
    {
        var heights = new List<string>();
        foreach (var theme in (Theme[])[Themes.Dark, Themes.Light])
        foreach (var density in (Density[])[Density.Pointer, Density.Touch])
        foreach (var size in Enum.GetValues<ComponentSize>())
        {
            var pick = new ListPick();
            pick.Selection.Apply(
                new SelectionChange(SelectionAction.Toggle, "a", "a"),
                pick.Items,
                Words.ByText
            );
            using var driver = UiDriver.Render(
                ui =>
                {
                    using (ui.Box(direction: Direction.Column, align: Align.Start))
                        ui.MultiSelect(
                            items: pick.Items,
                            itemKey: Words.ByText,
                            itemText: Words.Text,
                            value: pick.Selection,
                            sublabel: "Class",
                            size: size,
                            width: 200
                        );
                },
                theme: theme,
                density: density
            );
            var expected = theme.Dimensions(density).Size(size).Height;
            var height = Root(driver).ViewBounds.Height;
            if (MathF.Abs(height - expected) > 0.01f)
                heights.Add($"{size}/{density}: {height} not {expected}");
        }

        await Assert.That(heights).IsEmpty();
    }

    [Test]
    public async Task Its_dropdown_is_as_wide_as_its_field_and_no_taller_than_the_overlay_list_height()
    {
        var pick = new ListPick();
        using var driver = Render(pick, ItemsSource.From(Words.Numbered(60)));

        driver.Click(FieldOf(driver));
        var layer = driver.View.Layers[0];

        await Assert.That(layer.ViewBounds.Width).IsEqualTo(Root(driver).ViewBounds.Width);
        await Assert.That(layer.ViewBounds.Height).IsEqualTo(400f);
    }

    static Node RowAt(UiDriver driver, Vector2 point)
    {
        for (var n = driver.View.HitTest(point); n is not null; n = n.Parent)
        {
            if (n.RowFocusable)
                return n;
        }
        throw new InvalidOperationException("No row there.");
    }

    static string? FirstText(Node node)
    {
        foreach (var child in node.Children)
        {
            if (child.Kind == NodeKind.Text)
                return child.Text.ToString();
            if (FirstText(child) is { } text)
                return text;
        }
        return null;
    }

    static bool Tick(UiDriver driver, string text)
    {
        foreach (var child in Of(driver, text).Children)
            return !child.Placement.Hidden;
        return false;
    }
}

internal static class RectCenter
{
    public static Vector2 Center(this Rect rect) =>
        new(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
}

using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;
using TUnit.Assertions.Enums;
using static Tralys.Ui.Tests.Rows;

namespace Tralys.Ui.Tests;

public sealed class ListBoxTests
{
    static UiDriver Single(
        ListPick pick,
        IItemsSource<string> items,
        float height = 0,
        Func<string, bool>? disabled = null,
        bool activates = false,
        SelectionMode? mode = null
    ) =>
        UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: items,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    itemDisabled: disabled,
                    selectionMode: mode,
                    value: pick.Value,
                    onValueChange: pick.OnValueChange,
                    onItemActivate: activates ? pick.OnActivate : default,
                    width: 200,
                    height: height > 0 ? height : Length.Auto
                ),
            new Size(400, 300)
        );

    static UiDriver Multiple(ListPick pick) =>
        UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: pick.Items,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    selectionMode: SelectionMode.Multiple,
                    selection: pick.Selection,
                    onSelectionChange: pick.OnSelectionChange,
                    width: 200
                ),
            new Size(400, 300)
        );

    [Test]
    public async Task Control_presses_toggle_rows_in_and_out_and_a_selected_row_fills_in_the_accent()
    {
        var pick = new ListPick();
        using var driver = Multiple(pick);

        foreach (var text in (string[])["a", "c", "a", "d"])
            driver.Click(Of(driver, text), modifiers: ModifierKeys.Control);

        await Assert.That(pick.Selected()).IsEqualTo("c d");
        await Assert.That(IsSelected(driver, "a")).IsFalse();
        await Assert.That(Of(driver, "a").Visual.Background.IsVisible).IsFalse();
        await Assert
            .That(Of(driver, "d").Visual.Background)
            .IsEqualTo(Dark(ComponentColor.RowBgSelected));
        await Assert.That(Of(driver, "d").Inherit.Color).IsEqualTo(Dark(ColorToken.FgAccent));
    }

    [Test]
    public async Task A_list_that_doesnt_fill_its_selection_marks_the_selected_row_by_its_accent_text_alone()
    {
        var pick = new ListPick();
        using var driver = UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: pick.Items,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    value: "b",
                    fillsSelection: false,
                    width: 200
                ),
            new Size(400, 300)
        );

        await Assert
            .That((Of(driver, "b").Visual.Background.IsVisible, Of(driver, "b").Inherit.Color))
            .IsEqualTo((false, Dark(ColorToken.FgAccent)));
    }

    [Test]
    public async Task Multiple_selection_arrows_move_without_selecting_and_space_toggles_the_item_reached_wherever_it_went()
    {
        var words = Words.List("a", "b", "c", "d");
        var pick = new ListPick { Items = ItemsSource.From(words) };
        using var driver = Multiple(pick);

        driver.Click(Of(driver, "a"));
        driver.Press(Key.Down);
        driver.Press(Key.Down);
        var arrowed = pick.Toggled.Count;
        driver.Press(Key.Space);
        var added = pick.Selected();
        words.Insert(0, "z");
        driver.Settle();
        driver.Press(Key.Space);

        await Assert.That(arrowed).IsEqualTo(1);
        await Assert.That(added).IsEqualTo("a c");
        await Assert.That(pick.Selected()).IsEqualTo("a");
    }

    [Test]
    public async Task A_press_whose_report_takes_its_item_out_leaves_the_focus_on_the_list()
    {
        var words = Words.List("a", "b", "c");
        var pick = new ListPick();
        using var driver = UiDriver.Render(ui =>
            ui.ListBox(
                items: ItemsSource.From(words),
                itemKey: Words.ByText,
                itemText: Words.Text,
                value: pick.Value,
                onValueChange: Callback<UiKey>.Of(
                    words,
                    static (w, key) => w.Remove(key.ToString())
                ),
                width: 200
            )
        );
        driver.Focus(Of(driver, "a"));

        driver.Click(Of(driver, "b"));

        await Assert.That(Shown(driver)).IsEquivalentTo(["a", "c"]);
        await Assert.That(driver.Focused).IsSameReferenceAs(List(driver));
    }

    [Test]
    public async Task Down_reports_and_scrolls_to_each_next_row_to_the_end_and_home_goes_back_to_the_first()
    {
        var pick = new ListPick { Value = "0" };
        using var driver = Single(pick, ItemsSource.From(Words.Numbered(20)), height: 100);
        driver.Focus(Of(driver, "0"));

        for (var i = 0; i < 19; i++)
            driver.Press(Key.Down);
        var last = Of(driver, "19").ViewBounds;
        var list = List(driver).ViewBounds;
        driver.Press(Key.Home);

        await Assert
            .That(string.Join(" ", pick.Reports))
            .IsEqualTo("1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 0");
        await Assert.That(last.Bottom).IsEqualTo(list.Bottom);
        await Assert.That(IsSelected(driver, "0")).IsTrue();
        await Assert.That(driver.Focused).IsSameReferenceAs(Of(driver, "0"));
    }

    [Test]
    public async Task Up_and_down_at_its_ends_keep_the_focus_in_the_list()
    {
        var pick = new ListPick { Value = "a" };
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start))
                {
                    using (ui.Box(width: 20, height: 20, focusable: true)) { }
                    ui.ListBox(
                        items: Words.Of("a", "b"),
                        itemKey: Words.ByText,
                        itemText: Words.Text,
                        value: pick.Value,
                        onValueChange: pick.OnValueChange,
                        realizeAll: true,
                        width: 200
                    );
                    using (ui.Box(width: 20, height: 20, focusable: true)) { }
                }
            },
            new Size(400, 300)
        );

        driver.Focus(Of(driver, "b"));
        driver.Press(Key.Down);
        var atEnd = driver.Focused;
        driver.Focus(Of(driver, "a"));
        driver.Press(Key.Up);

        await Assert.That(atEnd).IsSameReferenceAs(Of(driver, "b"));
        await Assert.That(driver.Focused).IsSameReferenceAs(Of(driver, "a"));
    }

    [Test]
    public async Task Left_and_right_in_a_list_of_rows_leave_the_selection_and_the_focus()
    {
        var pick = new ListPick { Value = "b" };
        using var driver = Single(pick, Words.Of("a", "b", "c"));
        driver.Focus(Of(driver, "b"));

        driver.Press(Key.Right);
        driver.Press(Key.Left);

        await Assert.That(pick.Reports).IsEmpty();
        await Assert.That(driver.Focused).IsSameReferenceAs(Of(driver, "b"));
    }

    [Test]
    public async Task Enter_activates_the_focused_item_only_where_there_is_one_and_a_handler_and_otherwise_goes_on()
    {
        var pick = new ListPick();
        using var driver = Single(pick, Words.Of("a", "b", "c"), activates: true);
        var bare = new ListPick();
        using var unhandled = Single(bare, Words.Of("a", "b"));

        driver.Focus(List(driver));
        var onNothing = driver.KeyDown(Key.Enter);
        driver.KeyUp(Key.Enter);
        driver.Click(Of(driver, "b"));
        var onB = driver.KeyDown(Key.Enter);
        driver.KeyUp(Key.Enter);
        unhandled.Click(Of(unhandled, "a"));
        var withoutHandler = unhandled.KeyDown(Key.Enter);

        await Assert.That(onNothing).IsFalse();
        await Assert.That(onB).IsTrue();
        await Assert.That(pick.Activated).IsEquivalentTo(["b"]);
        await Assert.That(withoutHandler).IsFalse();
    }

    [Test]
    public async Task A_value_whose_item_leaves_shows_nothing_selected_and_a_selected_item_moved_stays_selected()
    {
        var words = Words.List("a", "b", "c");
        var pick = new ListPick { Value = "c" };
        using var driver = Single(pick, ItemsSource.From(words));

        words.Remove("c");
        driver.Settle();
        var leftOver = Shown(driver).Count(text => IsSelected(driver, text));
        pick.Value = "b";
        driver.Settle();
        words.Move(0, 1);
        driver.Settle();

        await Assert.That(leftOver).IsEqualTo(0);
        await Assert.That(IsSelected(driver, "b")).IsTrue();
        await Assert.That(IsSelected(driver, "a")).IsFalse();
        await Assert.That(pick.Reports).IsEmpty();
    }

    [Test]
    public async Task A_new_items_list_shows_from_the_top_and_a_reset_of_the_same_keeps_the_offset()
    {
        var shelf = new Shelf(200);
        IItemsSource<string> items = shelf;
        var scroll = new ScrollHolder();
        using var driver = UiDriver.Render(
            ui =>
            {
                scroll.Handle = ui.ScrollHandle();
                ui.ListBox(
                    items: items,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    scroll: scroll.Handle,
                    width: 200,
                    height: 100
                );
            },
            new Size(400, 300)
        );

        var handle = await Assert.That(scroll.Handle).IsNotNull();
        handle.ScrollTo(null, 300f);
        driver.Settle();
        shelf.Reverse();
        driver.Settle();
        var kept = handle.Offset.Y;
        items = new Shelf(200);
        driver.Rerender();

        await Assert.That(kept).IsEqualTo(300f);
        await Assert.That(handle.Offset.Y).IsEqualTo(0f);
    }

    [Test]
    public async Task Its_vertical_bar_stands_beside_the_overflowing_rows_and_hidden_they_scroll_with_none()
    {
        IItemsSource<string> items = new Shelf(200);
        using var shown = UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: items,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    width: 200,
                    height: 100
                ),
            new Size(400, 300)
        );
        using var hidden = UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: items,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    verticalScrollbar: ScrollbarVisibility.Hidden,
                    width: 200,
                    height: 100
                ),
            new Size(400, 300)
        );
        hidden.View.Context.SmoothScrolling = false;
        var bar = Bars.Root(shown.All<Scrollbar>().Single()).ViewBounds;
        var list = List(shown).ViewBounds;
        var row = Of(shown, "0").ViewBounds;

        hidden.Wheel(Of(hidden, "0"), new Vector2(0f, -1f));

        await Assert
            .That((bar.Y, bar.Right, bar.Bottom))
            .IsEqualTo((list.Y, list.Right, list.Bottom));
        await Assert.That(row.Right).IsEqualTo(bar.X);
        await Assert.That(hidden.All<Scrollbar>()).IsEmpty();
        var scroller = await Assert.That(List(hidden).Scroller).IsNotNull();
        await Assert.That(scroller.Offset.Y).IsGreaterThan(0f);
    }

    [Test]
    public async Task A_row_lights_on_the_frame_the_pointer_arrives_under_a_hand_cursor_until_it_leaves()
    {
        var pick = new ListPick();
        using var driver = Single(pick, Words.Of("a", "b", "c"));

        driver.Hover(Of(driver, "b"), settle: false);
        driver.Frame();
        var lit = (Of(driver, "b").Visual.Background, driver.Cursor);
        driver.Unhover();

        await Assert.That(lit).IsEqualTo((Dark(ColorToken.StateHover), Cursors.Hand));
        await Assert.That(Of(driver, "b").Visual.Background.IsVisible).IsFalse();
        await Assert.That(driver.Cursor).IsNotEqualTo(Cursors.Hand);
    }

    [Test]
    public async Task A_row_held_down_shows_pressed_until_released()
    {
        var pick = new ListPick();
        using var driver = Single(pick, Words.Of("a", "b"), mode: SelectionMode.None);

        var press = driver.Hold(Of(driver, "b"));
        var held = Of(driver, "b").Visual.Background;
        press.Release();

        await Assert.That(held).IsEqualTo(Dark(ColorToken.StatePressed));
        await Assert.That(Of(driver, "b").Visual.Background).IsEqualTo(Dark(ColorToken.StateHover));
    }

    [Test]
    public async Task A_disabled_row_greys_out_under_no_hand_and_the_arrows_pass_it_and_a_disabled_list_greys_every_row()
    {
        var pick = new ListPick { Value = "a" };
        var off = false;
        using var driver = UiDriver.Render(ui =>
            ui.ListBox(
                items: pick.Items,
                itemKey: Words.ByText,
                itemText: Words.Text,
                itemDisabled: static text => text == "b",
                value: pick.Value,
                onValueChange: pick.OnValueChange,
                disabled: off,
                width: 200
            )
        );
        driver.Focus(Of(driver, "a"));

        driver.Hover(Of(driver, "b"));
        var cursor = driver.Cursor;
        driver.Press(Key.Down);
        var grey = (Of(driver, "b").Inherit.Color, Of(driver, "a").Inherit.Color.IsVisible);
        driver.Press(Key.Up);
        off = true;
        driver.Rerender();

        await Assert.That(cursor).IsNotEqualTo(Cursors.Hand);
        await Assert.That(pick.Reports).IsEquivalentTo(["c", "a"], CollectionOrdering.Matching);
        await Assert.That(grey).IsEqualTo((Dark(ColorToken.FgDisabled), false));
        await Assert.That(Of(driver, "a").Inherit.Color).IsEqualTo(Dark(ColorToken.FgDisabled));
        await Assert.That(Of(driver, "c").Inherit.Color).IsEqualTo(Dark(ColorToken.FgDisabled));
    }

    [Test]
    public async Task With_no_items_the_empty_text_shows_until_an_item_arrives()
    {
        var words = Words.List();
        using var driver = UiDriver.Render(ui =>
            ui.ListBox(
                items: ItemsSource.From(words),
                itemKey: Words.ByText,
                itemText: Words.Text,
                emptyText: "No members yet",
                width: 200,
                height: 120
            )
        );
        var empty = driver.GetText("No members yet").ViewBounds;

        words.Add("a");
        driver.Settle();

        await Assert.That(empty.Y + empty.Height / 2f).IsEqualTo(60f).Within(1f);
        await Assert
            .That(() => driver.GetText("No members yet"))
            .Throws<InvalidOperationException>();
        await Assert.That(Shown(driver)).IsEquivalentTo(["a"]);
    }

    [Test]
    public async Task Rows_stand_the_themes_height_for_their_size_in_every_theme_and_density()
    {
        var heights = new List<string>();
        foreach (var theme in (Theme[])[Themes.Dark, Themes.Light])
        foreach (var density in (Density[])[Density.Pointer, Density.Touch])
        foreach (var size in (ComponentSize?[])[null, .. Enum.GetValues<ComponentSize>()])
        {
            using var driver = UiDriver.Render(
                ui =>
                    ui.ListBox(
                        items: Words.Of("a"),
                        itemKey: Words.ByText,
                        itemText: Words.Text,
                        size: size,
                        width: 200
                    ),
                theme: theme,
                density: density
            );
            var expected = theme.Dimensions(density).Size(size ?? ComponentSize.Md).Height;
            var height = Of(driver, "a").ViewBounds.Height;
            if (MathF.Abs(height - expected) > 0.01f)
                heights.Add($"{size}/{density}: {height} not {expected}");
        }

        await Assert.That(heights).IsEmpty();
    }

    [Test]
    public async Task A_row_height_never_goes_under_the_themes()
    {
        var rung = Themes.Dark.Dimensions(Density.Pointer).Size(ComponentSize.Md).Height;
        var asked = rung / 2f;
        using var driver = UiDriver.Render(ui =>
            ui.ListBox(
                items: Words.Of("a", "b"),
                itemKey: Words.ByText,
                itemText: Words.Text,
                rowHeight: asked,
                width: 200
            )
        );
        var half = Of(driver, "a").ViewBounds.Height;
        asked = rung * 2f;
        driver.Rerender();

        await Assert.That(half).IsEqualTo(rung);
        await Assert.That(Of(driver, "a").ViewBounds.Height).IsEqualTo(rung * 2f);
    }

    [Test]
    public async Task A_finger_swiping_over_a_row_or_a_node_selects_nothing_while_a_tap_selects_what_it_lands_on()
    {
        var pick = new ListPick();
        using var driver = Single(pick, ItemsSource.From(Words.Numbered(40)), height: 100);
        var leaves = new Leaf[40];
        for (var i = 0; i < leaves.Length; i++)
            leaves[i] = new Leaf(i + 1, "n" + i);
        var grove = new Grove(leaves);
        using var tree = grove.Render(height: 100);
        var top = Of(driver, "0").ViewBounds.Y;

        driver.Drag(Of(driver, "1"), new Vector2(0, -30), PointerKind.Touch);
        var scrolled = Of(driver, "0").ViewBounds.Y;
        var finger = driver.Hold(Of(driver, "2"), PointerKind.Touch);
        finger.MoveBy(new Vector2(0, -30));
        finger.MoveBy(new Vector2(0, 30));
        finger.Release();
        driver.Drag(Of(driver, "2"), new Vector2(40, 0), PointerKind.Touch);
        var swiped = pick.Reports.Count;
        driver.Tap(Of(driver, "3"));
        tree.Drag(tree.Get("n1"), new Vector2(0, -30), PointerKind.Touch);
        var swipedNodes = grove.Picks.Count;
        tree.Tap(tree.Get("n2"));

        await Assert.That(scrolled).IsLessThan(top);
        await Assert.That((swiped, swipedNodes)).IsEqualTo((0, 0));
        await Assert.That(pick.Reports).IsEquivalentTo(["3"]);
        await Assert.That(grove.Picks).IsEquivalentTo(["n2"]);
    }
}

internal sealed class ScrollHolder
{
    public ScrollHandle? Handle;
}

using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;
using static Tralys.Ui.Tests.Rows;

namespace Tralys.Ui.Tests;

public sealed class ListBoxGridTests
{
    static readonly Slot<string> Card = Slot<string>.Of(static (ui, text, _) => ui.Text(text));

    static readonly Slot<string> CardWithButton = Slot<string>.Of(
        static (ui, text, _) =>
        {
            ui.Text(text);
            using (
                ui.Box(
                    width: 20,
                    height: 20,
                    onClick: Callback.Of(static () => { }),
                    id: text == "a" ? "button" : null
                )
            ) { }
        }
    );

    static UiDriver Grid(
        ListPick pick,
        IItemsSource<string> items,
        float width = 316,
        float height = 200,
        Func<string, bool>? disabled = null,
        Slot<string> card = default
    ) =>
        UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: items,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    card: card.IsEmpty ? Card : card,
                    itemDisabled: disabled,
                    layout: ListLayout.Grid,
                    itemWidth: 50,
                    itemHeight: 50,
                    value: pick.Value,
                    onValueChange: pick.OnValueChange,
                    width: width,
                    height: height
                ),
            new Size(600, 400)
        );

    [Test]
    public async Task The_arrows_step_across_and_down_a_row_end_and_home_reach_the_ends_and_a_disabled_card_is_passed_over()
    {
        var pick = new ListPick();
        using var driver = Grid(
            pick,
            ItemsSource.From(Words.Numbered(20)),
            disabled: static text => text == "8"
        );
        driver.Focus(List(driver));

        foreach (
            var key in (Key[])
                [Key.Down, Key.Right, Key.Down, Key.Left, Key.Up, Key.Up, Key.End, Key.Home]
        )
            driver.Press(key);
        pick.Value = "7";
        driver.Settle();
        driver.Focus(Of(driver, "7"));
        driver.Press(Key.Right);

        await Assert.That(string.Join(" ", pick.Reports)).IsEqualTo("0 1 6 5 0 19 0 9");
    }

    [Test]
    public async Task A_press_on_a_card_selects_and_focuses_it_but_one_on_a_button_in_it_or_a_right_press_reports_nothing()
    {
        var pick = new ListPick();
        using var driver = Grid(pick, Words.Of("a", "b", "c"), card: CardWithButton);

        driver.Click(driver.Get("button"));
        driver.Click(Of(driver, "b"), MouseButton.Right);
        var before = pick.Reports.Count;
        driver.Click(Of(driver, "c"), new Vector2(40, 5));

        await Assert.That(before).IsEqualTo(0);
        await Assert.That(pick.Reports).IsEquivalentTo(["c"]);
        await Assert.That(driver.Focused).IsSameReferenceAs(Of(driver, "c"));
    }

    [Test]
    public async Task A_card_whose_content_drags_starts_the_drag_with_its_item_as_the_same_press_selects_it()
    {
        var pick = new ListPick();
        var dropped = new List<string>();
        var items = Words.Of("first", "second");
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(gap: 20))
                {
                    ui.ListBox(
                        items: items,
                        itemKey: Words.ByText,
                        itemText: Words.Text,
                        card: Slot<string>.Of(
                            static (ui, text, _) =>
                            {
                                using (
                                    ui.Box(grow: 1, dragData: text, dragEffects: DragEffect.Move)
                                )
                                    ui.Text(text);
                            }
                        ),
                        layout: ListLayout.Grid,
                        itemWidth: 50,
                        itemHeight: 50,
                        value: pick.Value,
                        onValueChange: pick.OnValueChange,
                        width: 200,
                        height: 100
                    );
                    using (
                        ui.Box(
                            width: 60,
                            height: 60,
                            onDrop: Handler<DropEvent>.Of(
                                dropped,
                                static (d, ref e) => d.Add((string)e.Data!)
                            ),
                            onDragOver: Handler<DropEvent>.Of(
                                static (ref DropEvent e) => e.Effect = DragEffect.Move
                            ),
                            id: "target"
                        )
                    ) { }
                }
            },
            new Size(400, 300)
        );
        var from = Of(driver, "first").ViewBounds;
        var to = driver.Get("target").ViewBounds;

        driver.Drag(
            Of(driver, "first"),
            new Vector2(to.X + to.Width / 2 - (from.X + from.Width / 2), 0)
        );

        await Assert.That(pick.Reports).IsEquivalentTo(["first"]);
        await Assert.That(dropped).IsEquivalentTo(["first"]);
    }

    [Test]
    public async Task A_grid_fits_columns_of_its_item_width_widens_them_to_fill_and_grows_their_height_only_to_keep_the_aspect()
    {
        float itemWidth = 60;
        var fill = false;
        var aspect = false;
        var twenty = ItemsSource.From(Words.Numbered(20));
        using var driver = UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: twenty,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    card: Card,
                    layout: ListLayout.Grid,
                    itemWidth: itemWidth,
                    itemHeight: 60,
                    fillWidth: fill,
                    keepAspect: aspect,
                    width: 440,
                    height: 300
                ),
            new Size(600, 400)
        );
        var plain = (Of(driver, "0").ViewBounds, Of(driver, "6").ViewBounds.X);
        fill = true;
        driver.Rerender();
        var filled = Of(driver, "0").ViewBounds;
        var row = Of(driver, "5").ViewBounds.Right - Of(driver, "0").ViewBounds.X;
        aspect = true;
        driver.Rerender();
        var kept = Of(driver, "0").ViewBounds;
        itemWidth = 120;
        fill = false;
        driver.Rerender();

        await Assert.That(plain.Item1.Width).IsEqualTo(62f);
        await Assert.That(plain.Item2).IsEqualTo(4f);
        await Assert.That(filled.Width).IsEqualTo(65f);
        await Assert.That(filled.Height).IsEqualTo(62f);
        await Assert.That(row).IsLessThanOrEqualTo(440f);
        await Assert.That(row).IsGreaterThanOrEqualTo(440f - 6f - 8f);
        await Assert.That(kept.Height).IsEqualTo(65f);
        await Assert.That(Of(driver, "0").ViewBounds.Width).IsEqualTo(122f);
    }

    [Test]
    public async Task A_filled_grid_keeping_the_aspect_settles_at_every_height_as_its_bar_comes_and_goes()
    {
        var height = new Model { Count = 400 };
        var twenty = ItemsSource.From(Words.Numbered(20));
        using var driver = UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: twenty,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    card: Card,
                    layout: ListLayout.Grid,
                    itemWidth: 60,
                    itemHeight: 60,
                    fillWidth: true,
                    keepAspect: true,
                    width: 400,
                    height: height.Count
                ),
            new Size(600, 600)
        );

        var unsettled = new List<int>();
        var widths = new HashSet<float>();
        for (; height.Count >= 200; height.Count--)
        {
            driver.Rerender();
            for (var i = 0; i < 3; i++)
                driver.Frame();
            var width = Of(driver, "0").ViewBounds.Width;
            for (var i = 0; i < 3; i++)
                driver.Frame();
            if (Of(driver, "0").ViewBounds.Width != width)
                unsettled.Add(height.Count);
            widths.Add(width);
        }

        await Assert.That(unsettled).IsEmpty();
        await Assert.That(widths.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_selected_card_wears_the_selected_edge_and_a_hovered_one_lights()
    {
        var pick = new ListPick { Value = "b" };
        using var driver = Grid(pick, Words.Of("a", "b", "c"));

        driver.Hover(Of(driver, "a"));

        await Assert
            .That(CardOf(driver, "b").Visual.BorderPaint)
            .IsEqualTo(Dark(ColorToken.BorderSelected));
        await Assert
            .That(CardOf(driver, "a").Visual.BorderPaint)
            .IsEqualTo(Dark(ColorToken.BorderHover));
        await Assert
            .That(CardOf(driver, "c").Visual.BorderPaint)
            .IsEqualTo(Dark(ColorToken.BorderMuted));
        await Assert.That(driver.Cursor).IsEqualTo(Cursors.Hand);
    }

    [Test]
    public async Task Fewer_columns_keep_the_first_shown_item_in_the_first_shown_row()
    {
        var columns = 6;
        var scroll = new ScrollHolder();
        var many = ItemsSource.From(Words.Numbered(300));
        using var driver = UiDriver.Render(
            ui =>
            {
                scroll.Handle = ui.ScrollHandle();
                ui.ListBox(
                    items: many,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    card: Card,
                    layout: ListLayout.Grid,
                    itemWidth: 50,
                    itemHeight: 50,
                    columns: columns,
                    scroll: scroll.Handle,
                    width: 360,
                    height: 200
                );
            },
            new Size(600, 400)
        );
        driver.Wheel(List(driver), new Vector2(0, -30));
        var first = FirstShown(driver);
        columns = 4;
        driver.Rerender();
        var now = FirstShown(driver);

        await Assert.That(first).IsGreaterThan(0);
        await Assert.That(now).IsLessThanOrEqualTo(first);
        await Assert.That(first).IsLessThan(now + 4);
    }

    [Test]
    public async Task The_extent_covers_every_shown_row_and_a_filter_shrinks_it_packing_the_matches_and_clamping_the_offset()
    {
        var shelf = new Shelf(400);
        var range = new List<ItemRange>();
        var scroll = new ScrollHolder();
        using var driver = UiDriver.Render(
            ui =>
            {
                scroll.Handle = ui.ScrollHandle();
                ui.ListBox(
                    items: shelf,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    card: Card,
                    layout: ListLayout.Grid,
                    itemWidth: 50,
                    itemHeight: 50,
                    onVisibleRangeChange: Callback<ItemRange>.Of(
                        range,
                        static (r, shown) => r.Add(shown)
                    ),
                    emptyText: "Nothing in this category",
                    scroll: scroll.Handle,
                    width: 316,
                    height: 200
                );
            },
            new Size(600, 400)
        );
        var extent = scroll.Handle!.Extent.Height;
        scroll.Handle.ScrollToEnd();
        driver.Settle();
        shelf.Filter(static text =>
            int.Parse(text, System.Globalization.CultureInfo.InvariantCulture) % 7 == 0
        );
        driver.Settle();
        var filtered = (
            scroll.Handle.Extent.Height,
            scroll.Handle.Offset.Y,
            Of(driver, "392").ViewBounds.X - Of(driver, "385").ViewBounds.X
        );
        shelf.Filter(static _ => false);
        driver.Settle();
        var empty = (driver.GetText("Nothing in this category") is not null, range[^1]);
        shelf.Filter(null);
        driver.Settle();

        await Assert.That(extent).IsEqualTo(80f * 60f);
        await Assert.That(filtered).IsEqualTo((12f * 60f, 12f * 60f - 200f, 60f));
        await Assert.That(empty).IsEqualTo((true, new ItemRange(0, -1)));
        await Assert
            .That(() => driver.GetText("Nothing in this category"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task The_shown_range_follows_the_wheel_over_the_cards_in_view()
    {
        var range = new List<ItemRange>();
        var many = ItemsSource.From(Words.Numbered(400));
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(direction: Direction.Column))
                    ui.ListBox(
                        items: many,
                        itemKey: Words.ByText,
                        itemText: Words.Text,
                        card: Card,
                        layout: ListLayout.Grid,
                        itemWidth: 50,
                        itemHeight: 50,
                        onVisibleRangeChange: Callback<ItemRange>.Of(
                            range,
                            static (r, shown) => r.Add(shown)
                        ),
                        width: 316,
                        height: 200
                    );
            },
            new Size(600, 400)
        );
        var first = range[^1];
        driver.Wheel(List(driver), new Vector2(0, -3));
        var wheeled = range[^1];
        var shown = Shown(driver);

        await Assert.That(first).IsEqualTo(new ItemRange(0, 19));
        await Assert.That(wheeled.First).IsGreaterThan(0);
        await Assert
            .That(shown)
            .Contains(wheeled.First.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Assert
            .That(shown)
            .Contains(wheeled.Last.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Assert.That(shown.Count).IsLessThan(60);
    }

    [Test]
    public async Task Switching_between_rows_and_cards_shows_each_item_once_and_keeps_the_selection()
    {
        var pick = new ListPick { Value = "b" };
        var abc = Words.Of("a", "b", "c");
        var layout = ListLayout.Grid;
        using var driver = UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: abc,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    card: Card,
                    layout: layout,
                    itemWidth: 50,
                    itemHeight: 50,
                    value: pick.Value,
                    onValueChange: pick.OnValueChange,
                    width: 300,
                    height: 200
                ),
            new Size(600, 400)
        );
        var cards = Shown(driver);
        layout = ListLayout.Rows;
        driver.Rerender();
        var rows = (
            string.Join(" ", Shown(driver)),
            driver.All<ListBox.Row>().Count,
            IsSelected(driver, "b")
        );
        layout = ListLayout.Grid;
        driver.Rerender();

        await Assert.That(cards).IsEquivalentTo(["a", "b", "c"]);
        await Assert.That(rows).IsEqualTo(("a b c", 3, true));
        await Assert.That(Shown(driver)).IsEquivalentTo(["a", "b", "c"]);
        await Assert.That(driver.All<ListBox.Row>()).IsEmpty();
        await Assert.That(IsSelected(driver, "b")).IsTrue();
        await Assert.That(pick.Reports).IsEmpty();
    }

    static Node CardOf(UiDriver driver, string text) => driver.GetText(text).Parent!;

    static int FirstShown(UiDriver driver)
    {
        var list = List(driver).ViewBounds;
        var first = int.MaxValue;
        foreach (var text in Shown(driver))
        {
            if (Of(driver, text).ViewBounds.Y >= list.Y - 1f)
                first = Math.Min(
                    first,
                    int.Parse(text, System.Globalization.CultureInfo.InvariantCulture)
                );
        }
        return first;
    }
}

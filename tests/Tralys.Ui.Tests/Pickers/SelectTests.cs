using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Tests;
using Tralys.Ui.Theming;
using TUnit.Assertions.Enums;

namespace Tralys.Ui.Components.Tests;

public sealed class SelectTests
{
    static readonly string[] Greek =
    [
        "Alpha",
        "Beta",
        "Gamma",
        "Delta",
        "Epsilon",
        "Zeta",
        "Eta",
        "Theta",
    ];

    static UiDriver Render(
        Picked picked,
        IItemsSource<string>? items = null,
        Func<string, bool>? disabled = null,
        bool searchable = false
    ) =>
        UiDriver
            .Render(ui =>
            {
                using (ui.Box(align: Align.Start, gap: 8))
                {
                    ui.Select(
                        items: items ?? Fruits.Items,
                        itemKey: Fruits.ByName,
                        itemText: Fruits.Named,
                        itemDisabled: disabled,
                        searchable: searchable,
                        value: picked.Value,
                        onValueChange: picked.OnValueChange,
                        onOpenChange: picked.OnOpenChange,
                        width: 200
                    );
                    using (ui.Box(width: 40, height: 20, focusable: true, id: "after")) { }
                }
            })
            .WithIcons();

    static Node Box(UiDriver driver) => driver.RootOf<Select<string>>();

    static Node Item(UiDriver driver, string text) =>
        driver.All<Select.Item>().Single(n => Shows(n.Props<SelectItemProps>(), text));

    static bool Shows(SelectItemProps item, string text) =>
        item.Text.IsNull ? item.Value == (UiKey)text : item.Text.ToString() == text;

    static Node Row(UiDriver driver, string text) => Menus.RootOf(Item(driver, text));

    static string[] Rows(UiDriver driver) =>
        [.. driver.All<Select.Item>().Select(n => n.Props<SelectItemProps>().Text.ToString())];

    static string? FocusedRow(UiDriver driver) =>
        driver
            .All<Select.Item>()
            .Where(n => ReferenceEquals(Menus.RootOf(n), driver.Focused))
            .Select(n => n.Props<SelectItemProps>().Text.ToString())
            .SingleOrDefault();

    static Paint Fill(UiDriver driver, string text) =>
        Menus.RootOf(Menus.RootOf(Row(driver, text))).Visual.Background;

    static Node Search(UiDriver driver) =>
        Nodes.Find(driver.RootOf<Overlay.Frame>(), NodeKind.EditableText);

    static List<string> Texts(Node within, List<string>? found = null)
    {
        found ??= [];
        foreach (var child in within.Children)
        {
            if (child.Component == typeof(Icon))
                continue;
            if (child.Kind == NodeKind.Text)
                found.Add(child.Text.ToString());
            Texts(child, found);
        }
        return found;
    }

    static bool HasIcon(Node within)
    {
        foreach (var child in within.Children)
        {
            if (child.Component == typeof(Icon) || HasIcon(child))
                return true;
        }
        return false;
    }

    static IItemsSource<string> Towns(int count)
    {
        string[] stems =
        [
            "Ashford",
            "Brightwater",
            "Coldharbour",
            "Dunmere",
            "Eastwick",
            "Fairhaven",
        ];
        var towns = new string[count];
        for (var i = 0; i < count; i++)
            towns[i] = $"{stems[i % stems.Length]} {i / stems.Length + 1}";
        return ItemsSource.From(towns);
    }

    [Test]
    public async Task A_dropdown_in_columns_lays_its_items_across_and_the_arrows_move_across_and_down()
    {
        using var driver = UiDriver
            .Render(ui =>
                ui.Select(
                    items: Fruits.Items,
                    itemKey: Fruits.ByName,
                    itemText: Fruits.Named,
                    value: "Apple",
                    columns: 3,
                    popupMinWidth: 300,
                    width: 200
                )
            )
            .WithIcons();
        driver.Click(Box(driver));
        var apple = Row(driver, "Apple").ViewBounds;
        var banana = Row(driver, "Banana").ViewBounds;

        driver.Press(Key.Right);
        var across = FocusedRow(driver);
        driver.Press(Key.Down);

        await Assert.That((banana.Y, banana.X > apple.X)).IsEqualTo((apple.Y, true));
        await Assert.That((across, FocusedRow(driver))).IsEqualTo(("Banana", "Mango"));
    }

    [Test]
    public async Task A_closed_box_clips_a_face_wider_than_it_and_keeps_its_chevron_inside()
    {
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(align: Align.Start))
                    ui.Select(
                        items: Fruits.Items,
                        itemKey: Fruits.ByName,
                        itemText: Fruits.Named,
                        valueContent: Slot<string>.Of(
                            static (ui, _, _) =>
                            {
                                using (ui.Box(width: 300, height: 10)) { }
                            }
                        ),
                        value: "Apple",
                        width: 120,
                        id: "box"
                    );
            })
            .WithIcons();
        var box = driver.Get("box").ViewBounds;
        var chevron = driver.All<Icon>().Single().Children[0].ViewBounds;

        await Assert.That(chevron.Right).IsLessThanOrEqualTo(box.Right);
    }

    [Test]
    public async Task A_click_on_the_box_opens_it_on_the_values_row_a_right_click_on_a_row_picks_nothing_and_a_click_picks_it_giving_the_box_the_focus_back()
    {
        var picked = new Picked { Value = "Cherry" };
        using var driver = Render(picked);

        driver.Click(Box(driver));
        var opened = FocusedRow(driver);
        driver.Click(Row(driver, "Grape"), MouseButton.Right);
        var afterRight = (picked.Values.Count, driver.View.Layers.Length);
        driver.Click(Row(driver, "Grape"));

        await Assert.That(opened).IsEqualTo("Cherry");
        await Assert.That(afterRight).IsEqualTo((0, 1));
        await Assert.That(picked.Values).IsEquivalentTo(["Grape"], CollectionOrdering.Matching);
        await Assert.That(picked.Opens).IsEquivalentTo([true, false], CollectionOrdering.Matching);
        await Assert.That(driver.Focused).IsSameReferenceAs(Box(driver));
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    [Test]
    public async Task A_rows_content_reads_whether_its_row_is_the_selected_one()
    {
        var picked = new Picked { Value = "Cherry" };
        using var driver = UiDriver
            .Render(ui =>
                ui.Select(
                    items: Fruits.Items,
                    itemKey: Fruits.ByName,
                    item: Slot<string>.Of(
                        static (ui, name, _) =>
                            ui.SelectItem(
                                value: name,
                                content: Slot.Of(name, static (ui, n) => ui.RowProbe(name: n))
                            )
                    ),
                    value: picked.Value,
                    onValueChange: picked.OnValueChange,
                    width: 200
                )
            )
            .WithIcons();

        driver.Click(Box(driver));
        driver.Press(Key.Down);

        await Assert.That(driver.Focused).IsSameReferenceAs(Row(driver, "Grape"));
        await Assert
            .That((RowProbe.Selected("Cherry"), RowProbe.Selected("Grape")))
            .IsEqualTo((true, false));
    }

    [Test]
    public async Task The_arrows_on_the_closed_box_select_the_item_before_or_after_stopping_at_the_first_and_home_and_end_reach_the_ends()
    {
        var picked = new Picked();
        using var driver = Render(picked);
        driver.Focus(Box(driver));

        driver.Press(Key.Up);
        driver.Press(Key.PageDown);
        driver.Press(Key.PageUp);
        var idle = picked.Values.Count;
        driver.Press(Key.Down);
        driver.Press(Key.Down);
        driver.Press(Key.Up);
        driver.Press(Key.Up);
        driver.Press(Key.End);
        driver.Press(Key.Home);

        await Assert.That(idle).IsEqualTo(0);
        await Assert
            .That(picked.Values)
            .IsEquivalentTo(
                ["Apple", "Banana", "Apple", "Orange", "Apple"],
                CollectionOrdering.Matching
            );
        await Assert.That(picked.Opens).IsEmpty();
        await Assert.That(driver.Focused).IsSameReferenceAs(Box(driver));
    }

    [Test]
    public async Task Typing_on_the_closed_box_steps_through_the_items_a_letter_starts_a_prefix_narrows_a_pause_starts_afresh_and_a_disabled_item_is_passed_over()
    {
        var picked = new Picked();
        using var driver = Render(
            picked,
            ItemsSource.From((string[])["Analyst", "Baker", "Barber", "Biologist", "Carver"]),
            disabled: static name => name == "Carver"
        );
        driver.Focus(Box(driver));

        foreach (var key in (Key[])[Key.B, Key.B, Key.B, Key.B])
            driver.Press(key);
        driver.Frame(1.1f);
        driver.Press(Key.B);
        driver.Press(Key.I);
        driver.Frame(1.1f);
        driver.Press(Key.A);
        driver.Frame(1.1f);
        driver.Press(Key.C);

        await Assert
            .That(picked.Values)
            .IsEquivalentTo(
                ["Baker", "Barber", "Biologist", "Baker", "Barber", "Biologist", "Analyst"],
                CollectionOrdering.Matching
            );
        await Assert.That(picked.Opens).IsEmpty();
    }

    [Test]
    public async Task Typing_in_an_open_dropdown_lights_the_next_row_starting_with_it_picking_nothing_and_reopening_forgets_what_was_typed()
    {
        var picked = new Picked { Value = "Ashford 1" };
        using var driver = Render(picked, Towns(10_000));
        driver.Focus(Box(driver));

        driver.Press(Key.F4);
        var opened = FocusedRow(driver);
        driver.Press(Key.C);
        var first = FocusedRow(driver);
        driver.Press(Key.C);
        var again = FocusedRow(driver);
        driver.Press(Key.Escape);
        driver.Press(Key.F4);
        driver.Press(Key.A);

        await Assert
            .That((opened, first, again))
            .IsEqualTo(("Ashford 1", "Coldharbour 1", "Coldharbour 2"));
        await Assert.That(FocusedRow(driver)).IsEqualTo("Ashford 2");
        await Assert.That(picked.Values).IsEmpty();
        await Assert
            .That(picked.Opens)
            .IsEquivalentTo([true, false, true], CollectionOrdering.Matching);
    }

    [Test]
    public async Task F4_or_alt_down_on_an_open_dropdown_takes_the_focused_row_and_closes_alt_up_opens_it_and_escape_closes_it_as_it_was()
    {
        var picked = new Picked { Value = "Apple" };
        using var driver = Render(picked);
        driver.Focus(Box(driver));

        driver.Press(Key.Up, ModifierKeys.Alt);
        driver.Press(Key.Down);
        driver.Press(Key.F4);
        driver.Press(Key.F4);
        driver.Press(Key.Down);
        driver.Press(Key.Down, ModifierKeys.Alt);
        driver.Press(Key.F4);
        driver.Press(Key.Down);
        driver.Press(Key.Escape);

        await Assert
            .That(picked.Values)
            .IsEquivalentTo(["Banana", "Cherry"], CollectionOrdering.Matching);
        await Assert
            .That(picked.Opens)
            .IsEquivalentTo([true, false, true, false, true, false], CollectionOrdering.Matching);
        await Assert.That(driver.Focused).IsSameReferenceAs(Box(driver));
    }

    [Test]
    public async Task Opening_focuses_the_first_enabled_row_the_arrows_pass_over_a_disabled_one_and_with_none_enabled_the_box_keeps_the_focus()
    {
        var items = ItemsSource.From((string[])["Alpha", "Bravo", "Charlie", "Delta"]);
        var picked = new Picked();
        using var driver = Render(
            picked,
            items,
            disabled: static name => name is "Alpha" or "Charlie"
        );
        var none = new Picked();
        using var shut = Render(none, items, disabled: static _ => true);

        driver.Click(Box(driver));
        var opened = FocusedRow(driver);
        driver.Press(Key.Down);
        shut.Click(Box(shut));

        await Assert.That((opened, FocusedRow(driver))).IsEqualTo(("Bravo", "Delta"));
        await Assert.That(shut.View.Layers.Length).IsEqualTo(1);
        await Assert.That(shut.Focused).IsSameReferenceAs(Box(shut));
    }

    [Test]
    public async Task Its_dropdown_is_as_wide_as_the_box_or_its_minimum_at_most_400_tall_and_opens_on_a_far_value_building_only_the_rows_in_view()
    {
        var towns = Components.Tests.Towns.Make(10_000);
        var wide = new Switches();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(align: Align.Start))
                    ui.Select(
                        items: towns,
                        itemKey: Components.Tests.Towns.ByName,
                        itemText: Components.Tests.Towns.Named,
                        value: "Town 9000",
                        popupMinWidth: wide.A ? 300 : 0,
                        width: 200
                    );
            })
            .WithIcons();
        driver.Focus(Box(driver));

        driver.Press(Key.F4);
        var field = Box(driver).ViewBounds;
        var narrow = driver.RootOf<Overlay.Frame>().ViewBounds;
        var rows = Rows(driver);
        var focused = FocusedRow(driver);
        wide.A = true;
        driver.Rerender();

        await Assert
            .That((narrow.X, narrow.Y, narrow.Width, narrow.Height))
            .IsEqualTo((field.X, field.Bottom, 200f, 400f));
        await Assert.That(focused).IsEqualTo("Town 9000");
        await Assert.That(rows.Length).IsBetween(1, 99);
        await Assert.That(driver.RootOf<Overlay.Frame>().ViewBounds.Width).IsEqualTo(300f);
    }

    [Test]
    public async Task End_then_home_in_one_frame_focuses_the_first_row_and_end_as_an_item_is_inserted_focuses_the_last_item()
    {
        var items = new ObservableList<string>();
        for (var i = 1; i <= 200; i++)
            items.Add($"Item {i}");
        var picked = new Picked();
        using var driver = Render(picked, ItemsSource.From(items));
        driver.Focus(Box(driver));
        driver.Press(Key.F4);

        driver.Press(Key.End, settle: false);
        driver.Press(Key.Home, settle: false);
        driver.Settle();
        var home = FocusedRow(driver);
        driver.Press(Key.End, settle: false);
        items.Insert(0, "Item 0");
        driver.Settle();

        await Assert.That(home).IsEqualTo("Item 1");
        await Assert.That(FocusedRow(driver)).IsEqualTo("Item 200");
    }

    [Test]
    public async Task A_hovered_row_takes_the_focus_and_the_light_an_arrow_moves_both_off_it_and_rows_scrolling_under_a_resting_pointer_never_take_them()
    {
        var picked = new Picked();
        using var driver = Render(picked, Components.Tests.Towns.Make(200, "Item"));
        driver.Focus(Box(driver));
        driver.Press(Key.F4);
        var hover = Themes.Dark.Color(ColorToken.StateHover);

        driver.Hover(Row(driver, "Item 4"));
        var hovered = (FocusedRow(driver), Fill(driver, "Item 4"));
        driver.Press(Key.Down);
        var moved = (Fill(driver, "Item 4"), Fill(driver, "Item 5"));
        driver.Press(Key.End);
        driver.Frame();
        var ended = FocusedRow(driver);
        driver.Press(Key.Enter);

        await Assert.That(hovered).IsEqualTo(("Item 4", hover));
        await Assert.That(moved).IsEqualTo((default(Paint), hover));
        await Assert.That(ended).IsEqualTo("Item 200");
        await Assert.That(picked.Values).IsEquivalentTo(["Item 200"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Tab_and_shift_tab_step_through_the_rows_in_item_order_even_scrolled_to_the_end()
    {
        var picked = new Picked();
        using var driver = Render(picked, Components.Tests.Towns.Make(10_000));
        driver.Focus(Box(driver));
        driver.Press(Key.F4);

        driver.Press(Key.End);
        driver.Press(Key.Tab, ModifierKeys.Shift);
        var back = FocusedRow(driver);
        driver.Press(Key.Tab, ModifierKeys.Shift);
        var twice = FocusedRow(driver);
        driver.Press(Key.Tab);

        await Assert.That((back, twice)).IsEqualTo(("Town 9999", "Town 9998"));
        await Assert.That(FocusedRow(driver)).IsEqualTo("Town 9999");
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
    }

    [Test]
    public async Task A_searchable_dropdown_opens_in_its_search_box_shows_its_matches_in_item_order_says_no_results_for_a_miss_and_enter_picks_the_only_match()
    {
        var picked = new Picked();
        using var driver = Render(picked, ItemsSource.From(Greek), searchable: true);
        driver.Focus(Box(driver));

        driver.Press(Key.F4);
        var searching = ReferenceEquals(driver.Focused, Search(driver));
        driver.Type("zz");
        var missed = (driver.All<EmptyState>().Count, Rows(driver).Length);
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("eta");
        var matched = Rows(driver);
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("the");
        driver.Press(Key.Enter);
        var picks = picked.Values.ToArray();
        driver.Click(Box(driver));

        await Assert.That(searching).IsTrue();
        await Assert.That(missed).IsEqualTo((1, 0));
        await Assert
            .That(matched)
            .IsEquivalentTo(["Beta", "Zeta", "Eta", "Theta"], CollectionOrdering.Matching);
        await Assert.That(picks).IsEquivalentTo(["Theta"], CollectionOrdering.Matching);
        await Assert.That(Rows(driver).Length).IsEqualTo(8);
        await Assert.That(Search(driver).Text.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task A_dropdown_that_realizes_all_holds_a_row_for_each_match_in_item_order_as_the_search_changes()
    {
        var names = new string[300];
        for (var i = 0; i < names.Length; i++)
            names[i] = $"Item {i + 1}";
        var picked = new Picked();
        using var driver = UiDriver
            .Render(ui =>
                ui.Select(
                    items: ItemsSource.From(names),
                    itemKey: Fruits.ByName,
                    itemText: Fruits.Named,
                    searchable: true,
                    realizeAll: true,
                    onValueChange: picked.OnValueChange,
                    width: 200
                )
            )
            .WithIcons();
        driver.Focus(Box(driver));
        driver.Press(Key.F4);

        driver.Type("1");
        var ones = Rows(driver);
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("29");

        await Assert
            .That(ones)
            .IsEquivalentTo(names.Where(n => n.Contains('1')), CollectionOrdering.Matching);
        await Assert
            .That(Rows(driver))
            .IsEquivalentTo(names.Where(n => n.Contains("29")), CollectionOrdering.Matching);
    }

    [Test]
    public async Task In_the_search_down_reaches_the_first_match_up_stays_put_and_enter_closes_on_the_value_among_the_matches_else_picks_nothing()
    {
        var picked = new Picked { Value = "Gamma" };
        using var driver = Render(picked, ItemsSource.From(Greek), searchable: true);
        driver.Focus(Box(driver));
        driver.Press(Key.F4);

        driver.Press(Key.Up);
        driver.Press(Key.PageUp);
        var stayed = ReferenceEquals(driver.Focused, Search(driver));
        driver.Press(Key.Down);
        var down = FocusedRow(driver);
        driver.Press(Key.Tab, ModifierKeys.Shift);
        var back = ReferenceEquals(driver.Focused, Search(driver));
        driver.Type("a");
        driver.Press(Key.Enter);
        var closed = driver.View.Layers.Length;
        driver.Press(Key.F4);
        driver.Type("eta");
        driver.Press(Key.Enter);

        await Assert.That((stayed, down, back)).IsEqualTo((true, "Alpha", true));
        await Assert.That(closed).IsEqualTo(0);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
        await Assert.That(picked.Values).IsEmpty();
        await Assert
            .That(picked.Opens)
            .IsEquivalentTo([true, false, true], CollectionOrdering.Matching);
    }

    [Test]
    public async Task The_closed_box_shows_its_value_through_its_value_content_else_its_row_hides_its_chevron_without_an_arrow_and_a_rows_button_runs_alone()
    {
        var forgets = new Forgets();
        var hidden = new Switches();
        var row = Slot<Account>.Of(
            static (ui, account, _) =>
                ui.SelectItem(
                    value: account.Name,
                    content: Slot.Of(
                        account,
                        static (ui, a) =>
                        {
                            ui.Text(a.Name);
                            ui.IconButton(icon: IconGlyph.Of(IconRole.X), onClick: a.Forget);
                        }
                    )
                )
        );
        var picked = new Picked();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
                {
                    ui.Select(
                        items: forgets.Items,
                        itemKey: Account.ByName,
                        item: row,
                        valueContent: Slot<Account>.Of(
                            static (ui, a, _) =>
                            {
                                ui.Text("Signed in as");
                                ui.Text(a.Name);
                            }
                        ),
                        value: "Brook",
                        hasArrow: !hidden.A,
                        width: 200
                    );
                    ui.Select(
                        items: forgets.Items,
                        itemKey: Account.ByName,
                        item: row,
                        value: "Brook",
                        onValueChange: picked.OnValueChange,
                        width: 200
                    );
                }
            })
            .WithIcons();
        var boxes = driver.All<Select<Account>>();
        var shown = (Texts(boxes[0]), Texts(boxes[1]));
        var arrow = HasIcon(boxes[0]);
        hidden.A = true;
        driver.Rerender();
        var hiddenArrow = HasIcon(driver.All<Select<Account>>()[0]);
        driver.Focus(Menus.RootOf(driver.All<Select<Account>>()[1]));
        driver.Press(Key.F4);

        driver.Click(ButtonIn(driver, "Cleo"));

        await Assert
            .That(shown.Item1)
            .IsEquivalentTo(["Signed in as", "Brook"], CollectionOrdering.Matching);
        await Assert.That(shown.Item2).IsEquivalentTo(["Brook"], CollectionOrdering.Matching);
        await Assert.That((arrow, hiddenArrow)).IsEqualTo((true, false));
        await Assert
            .That(forgets.Names)
            .IsEquivalentTo(["Ada", "Brook", "Dara"], CollectionOrdering.Matching);
        await Assert.That(picked.Values).IsEmpty();
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
    }

    [Test]
    public async Task Over_items_with_no_text_typing_selects_nothing_and_a_search_matches_none()
    {
        var forgets = new Forgets();
        var picked = new Picked();
        using var driver = UiDriver
            .Render(ui =>
                ui.Select(
                    items: forgets.Items,
                    itemKey: Account.ByName,
                    item: Slot<Account>.Of(
                        static (ui, a, _) => ui.SelectItem(value: a.Name, text: a.Name)
                    ),
                    searchable: true,
                    onValueChange: picked.OnValueChange,
                    width: 200
                )
            )
            .WithIcons();
        driver.Focus(driver.RootOf<Select<Account>>());

        driver.Press(Key.A);
        driver.Press(Key.F4);
        driver.Type("a");

        await Assert.That(picked.Values).IsEmpty();
        await Assert.That(driver.All<EmptyState>().Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_press_outside_closes_it_keeping_its_value()
    {
        var picked = new Picked { Value = "Banana" };
        using var driver = Render(picked);

        driver.Click(Box(driver));
        driver.Press(Key.Down);
        driver.Click(driver.Get("after"));

        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
        await Assert.That(picked.Values).IsEmpty();
        await Assert.That(picked.Opens).IsEquivalentTo([true, false], CollectionOrdering.Matching);
    }

    static Node ButtonIn(UiDriver driver, string row)
    {
        var item = Item(driver, row);
        foreach (var button in driver.All<IconButton>())
        {
            for (var at = button.Parent; at is not null; at = at.Parent)
            {
                if (ReferenceEquals(at, item))
                    return button;
            }
        }
        throw new InvalidOperationException($"No button in {row}.");
    }
}

public readonly record struct RowProbeProps
{
    public required string Name { get; init; }
}

/// <summary>Shows its name and records whether the row holding it showed selected.</summary>
public sealed class RowProbe : Component<RowProbeProps>
{
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> Seen = new();

    public static bool Selected(string name) => Seen.TryGetValue(name, out var on) && on;

    protected override void Render(Ui ui, in RowProbeProps p)
    {
        Seen[p.Name] = ui.Use(ListBox.RowContext).Selected;
        ui.Text(p.Name);
    }
}

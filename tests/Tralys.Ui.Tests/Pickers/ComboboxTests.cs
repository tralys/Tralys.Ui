using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Tests;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components.Tests;

public sealed class ComboboxTests
{
    static UiDriver Render(Picked picked, IItemsSource<string>? items = null) =>
        UiDriver
            .Render(ui =>
            {
                using (ui.Box(align: Align.Start, gap: 8))
                {
                    ui.Combobox(
                        items: items ?? Fruits.Items,
                        itemKey: Fruits.ByName,
                        itemText: Fruits.Named,
                        value: picked.Value,
                        onValueChange: picked.OnValueChange,
                        onOpenChange: picked.OnOpenChange,
                        width: 200
                    );
                    using (ui.Box(width: 40, height: 20, focusable: true, id: "after")) { }
                }
            })
            .WithIcons();

    static Node Text(UiDriver driver) =>
        Nodes.Find(driver.Get<Combobox<string>>(), NodeKind.EditableText);

    static Node Chevron(UiDriver driver) => Nodes.Glyph(driver.Get<Combobox<string>>());

    static string[] Rows(UiDriver driver) =>
        [.. driver.All<Combobox.Item>().Select(n => n.Props<ComboboxItemProps>().Text.ToString())];

    static Node Row(UiDriver driver, string text) =>
        driver
            .All<Combobox.Item>()
            .Single(n => n.Props<ComboboxItemProps>().Text.ToString() == text);

    static Paint Fill(Node item) =>
        Menus.RootOf(Menus.RootOf(Menus.RootOf(Menus.RootOf(item)))).Visual.Background;

    [Test]
    public async Task Typing_filters_the_rows_and_enter_picks_the_lit_one_then_the_popup_fades_out()
    {
        var picked = new Picked();
        using var driver = Render(picked);
        var text = Text(driver);
        driver.Focus(text);

        driver.Type("an");
        var rows = Rows(driver);
        driver.Press(Key.Down);
        driver.Press(Key.Enter, settle: false);
        driver.Frame();
        var fading = driver.View.Layers.Length;
        driver.Settle();

        await Assert.That(rows).IsEquivalentTo(["Banana", "Mango", "Orange"]);
        await Assert.That(picked.Values).IsEquivalentTo(["Mango"]);
        await Assert.That(picked.Opens).IsEquivalentTo([true, false]);
        await Assert.That(driver.Focused).IsSameReferenceAs(text);
        await Assert.That(text.Text.ToString()).IsEqualTo("Mango");
        await Assert.That(fading).IsEqualTo(1);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Arrows_open_on_the_selection_and_move_without_wrapping_and_escape_closes_without_picking()
    {
        var picked = new Picked { Value = "Cherry" };
        using var driver = Render(picked);
        driver.Focus(Text(driver));

        driver.Press(Key.Down, ModifierKeys.Control);
        driver.Press(Key.End, ModifierKeys.Alt);
        driver.Press(Key.PageDown);
        var pagedShut = picked.Opens.Count;
        driver.Press(Key.Down);
        driver.Press(Key.Up);
        driver.Press(Key.Up);
        driver.Press(Key.Up);
        driver.Press(Key.Enter);
        driver.Press(Key.Down, ModifierKeys.Alt);
        driver.Press(Key.Down);
        driver.Press(Key.Up, ModifierKeys.Alt);
        var afterAltUp = picked.Values.Count;
        driver.Press(Key.Down);
        driver.Press(Key.Down);
        driver.Press(Key.Escape);
        var afterEscape = picked.Values.Count;
        driver.Press(Key.Down);
        driver.Press(Key.Down);
        driver.Press(Key.Enter);

        await Assert.That(pagedShut).IsEqualTo(0);
        await Assert.That((afterAltUp, afterEscape)).IsEqualTo((1, 1));
        await Assert.That(picked.Values).IsEquivalentTo(["Apple", "Banana"]);
        await Assert
            .That(picked.Opens)
            .IsEquivalentTo([true, false, true, false, true, false, true, false]);
    }

    [Test]
    public async Task Its_chevron_opens_it_a_right_click_on_a_row_picks_nothing_and_a_click_picks_it_leaving_the_focus_in_the_field()
    {
        var picked = new Picked();
        using var driver = Render(picked);
        var text = Text(driver);

        driver.Click(Chevron(driver));
        driver.Click(Row(driver, "Grape"), MouseButton.Right);
        var afterRight = (picked.Values.Count, driver.View.Layers.Length);
        driver.Click(Row(driver, "Grape"));

        await Assert.That(afterRight).IsEqualTo((0, 1));
        await Assert.That(picked.Values).IsEquivalentTo(["Grape"]);
        await Assert.That(picked.Opens).IsEquivalentTo([true, false]);
        await Assert.That(driver.Focused).IsSameReferenceAs(text);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    [Test]
    public async Task A_hovered_row_takes_the_light_in_the_row_hover_and_an_arrow_moves_it_off_the_row_under_the_pointer()
    {
        var picked = new Picked();
        using var driver = Render(picked);
        driver.Focus(Text(driver));
        driver.Press(Key.Down);
        var hover = Themes.Dark.Color(ColorToken.StateHover);

        driver.Hover(Row(driver, "Banana"));
        var hovered = (Fill(Row(driver, "Banana")), Fill(Row(driver, "Apple")));
        driver.Press(Key.Down);
        var moved = (Fill(Row(driver, "Banana")), Fill(Row(driver, "Cherry")));
        driver.Press(Key.Enter);

        await Assert.That(hovered).IsEqualTo((hover, default(Paint)));
        await Assert.That(moved).IsEqualTo((default(Paint), hover));
        await Assert.That(picked.Values).IsEquivalentTo(["Cherry"]);
    }

    [Test]
    public async Task Page_keys_move_the_light_seven_rows_and_rows_scrolling_under_a_resting_pointer_never_take_it()
    {
        var picked = new Picked();
        using var driver = Render(picked, Towns.Make(200, "Item"));
        driver.Focus(Text(driver));
        driver.Press(Key.Down);
        driver.Hover(Row(driver, "Item 4"));

        driver.Press(Key.PageDown);
        driver.Press(Key.PageDown);
        driver.Press(Key.PageUp);
        driver.Press(Key.PageDown);
        driver.Press(Key.Enter);
        driver.Press(Key.Down);
        driver.Press(Key.PageUp);
        driver.Press(Key.PageUp);
        driver.Press(Key.PageUp);
        driver.Press(Key.Enter);

        await Assert.That(picked.Values).IsEquivalentTo(["Item 18", "Item 1"]);
    }

    [Test]
    public async Task Escape_after_typing_puts_the_selection_back_and_reopening_shows_every_row()
    {
        var picked = new Picked { Value = "Cherry" };
        using var driver = Render(picked);
        var text = Text(driver);
        driver.Focus(text);

        driver.Type("an");
        var typed = Rows(driver).Length;
        driver.Press(Key.Escape);
        var shown = text.Text.ToString();
        driver.Press(Key.Down);

        await Assert.That(typed).IsEqualTo(3);
        await Assert.That(shown).IsEqualTo("Cherry");
        await Assert.That(Rows(driver).Length).IsEqualTo(6);
        await Assert.That(picked.Values).IsEmpty();
    }

    [Test]
    public async Task A_press_outside_the_focus_leaving_and_a_window_coming_back_after_the_focus_left_close_it_keeping_its_value()
    {
        var picked = new Picked { Value = "Banana" };
        using var driver = Render(picked);
        driver.Focus(Text(driver));

        driver.Press(Key.Down);
        driver.Click(driver.Get("after"));
        var pressed = driver.View.Layers.Length;
        driver.Focus(Text(driver));
        driver.Press(Key.Down);
        driver.Focus(driver.Get("after"));
        var left = driver.View.Layers.Length;
        driver.Focus(Text(driver));
        driver.Press(Key.Down);
        driver.WindowFocus(false);
        driver.WindowFocus(true);
        var back = driver.View.Layers.Length;
        driver.WindowFocus(false);
        driver.Focus(driver.Get("after"));
        var inactive = driver.View.Layers.Length;
        driver.WindowFocus(true);

        await Assert.That((pressed, left, back, inactive)).IsEqualTo((0, 0, 1, 1));
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
        await Assert.That(picked.Values).IsEmpty();
        await Assert.That(picked.Opens).IsEquivalentTo([true, false, true, false, true, false]);
    }

    [Test]
    public async Task Gamepad_east_closes_it_before_the_scopes_back_runs()
    {
        var picked = new Picked();
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Screen(name: "screen", log: log, active: true);
                ui.Combobox(
                    items: Fruits.Items,
                    itemKey: Fruits.ByName,
                    itemText: Fruits.Named,
                    onOpenChange: picked.OnOpenChange,
                    width: 200
                );
            }
        });
        driver.Focus(Text(driver));
        driver.Press(Key.Down);

        driver.Press(GamepadButton.East);

        await Assert.That(picked.Opens).IsEquivalentTo([true, false]);
        await Assert.That(log.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task Its_popup_is_as_wide_as_the_field_or_its_minimum_eight_rows_tall_and_opens_on_a_far_selection_building_only_the_rows_in_view()
    {
        var height = Themes.Dark.Dimensions(Density.Pointer).Size(ComponentSize.Md).Height;
        var towns = Towns.Make(10_000);
        var wide = new Switches();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.Combobox(
                    items: towns,
                    itemKey: Towns.ByName,
                    itemText: Towns.Named,
                    value: "Town 9000",
                    popupMinWidth: wide.A ? 300 : 0,
                    width: 200
                );
        });
        driver.Focus(Text(driver));

        driver.Press(Key.Down);
        var field = driver.RootOf<Combobox<string>>().ViewBounds;
        var narrow = driver.RootOf<Overlay.Frame>().ViewBounds;
        var list = Nodes.Find(driver.RootOf<Overlay.Frame>(), NodeKind.VirtualList).ViewBounds;
        var rows = Rows(driver);
        wide.A = true;
        driver.Rerender();

        await Assert
            .That((narrow.X, narrow.Y, narrow.Width))
            .IsEqualTo((field.X, field.Bottom, 200f));
        await Assert.That(list.Height).IsEqualTo(8 * height);
        await Assert.That(rows).Contains("Town 9000");
        await Assert.That(rows.Length).IsBetween(1, 99);
        await Assert.That(Fill(Row(driver, "Town 9000"))).IsNotEqualTo(default(Paint));
        await Assert.That(driver.RootOf<Overlay.Frame>().ViewBounds.Width).IsEqualTo(300f);
    }

    [Test]
    public async Task At_twice_the_scale_its_rows_open_just_below_its_corner_and_a_click_picks_one()
    {
        var picked = new Picked();
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(align: Align.Start, padding: 20))
                    ui.Combobox(
                        items: Fruits.Items,
                        itemKey: Fruits.ByName,
                        itemText: Fruits.Named,
                        onValueChange: picked.OnValueChange,
                        width: 200
                    );
            },
            scale: 2f
        );
        driver.Focus(Text(driver));
        driver.Press(Key.Down);

        var field = driver.RootOf<Combobox<string>>().ViewBounds;
        var first = Menus.RootOf(Row(driver, "Apple")).ViewBounds;
        driver.Click(Row(driver, "Cherry"));

        await Assert.That(first.X - field.X).IsBetween(0f, 16f);
        await Assert.That(first.Y - field.Bottom).IsBetween(0f, 16f);
        await Assert.That(picked.Values).IsEquivalentTo(["Cherry"]);
    }

    [Test]
    public async Task A_button_in_a_rows_content_runs_alone_picking_nothing_and_leaving_it_open()
    {
        var forgets = new Forgets();
        var picked = new Picked();
        using var driver = UiDriver.Render(ui =>
            ui.Combobox(
                items: forgets.Items,
                itemKey: Account.ByName,
                itemText: Account.Named,
                item: Slot<Account>.Of(
                    static (ui, account, _) =>
                        ui.ComboboxItem(
                            value: account.Name,
                            content: Slot.Of(
                                account,
                                static (ui, a) =>
                                {
                                    ui.Text(a.Name, grow: 1f);
                                    ui.IconButton(
                                        icon: IconGlyph.Of(IconRole.X),
                                        onClick: a.Forget
                                    );
                                }
                            )
                        )
                ),
                onValueChange: picked.OnValueChange,
                defaultOpen: true,
                width: 200
            )
        );

        driver.Click(Last<IconButton>(driver, "Brook"));

        await Assert.That(forgets.Names).IsEquivalentTo(["Ada", "Cleo", "Dara"]);
        await Assert.That(picked.Values).IsEmpty();
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
    }

    [Test]
    public async Task The_lit_row_keeps_its_item_as_the_items_change_around_it_and_enter_picks_that_item()
    {
        var letters = new Tralys.Ui.Mvvm.ObservableList<string>();
        foreach (
            var letter in (string[])
                ["Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta"]
        )
            letters.Add(letter);
        var picked = new Picked();
        using var driver = Render(picked, ItemsSource.From(letters));
        driver.Focus(Text(driver));

        driver.Type("eta");
        driver.Press(Key.Down);
        letters.Insert(0, "Omega");
        letters.Insert(0, "Beta Two");
        letters.Remove("Alpha");
        letters.Remove("Theta");
        letters.Insert(0, "Theta");
        driver.Settle();
        var rows = Rows(driver);
        driver.Press(Key.Down);
        driver.Press(Key.Up);
        driver.Press(Key.Enter);

        await Assert.That(rows).IsEquivalentTo(["Theta", "Beta Two", "Beta", "Zeta", "Eta"]);
        await Assert.That(picked.Values).IsEquivalentTo(["Zeta"]);
    }

    [Test]
    public async Task A_search_says_no_results_again_whenever_the_items_change_under_it()
    {
        var roles = new Tralys.Ui.Mvvm.ObservableList<string>();
        roles.Add("Engineer");
        var picked = new Picked();
        using var driver = Render(picked, ItemsSource.From(roles));
        driver.Focus(Text(driver));

        driver.Type("des");
        var none = driver.All<EmptyState>().Count;
        roles.Add("Designer");
        driver.Settle();
        var one = (driver.All<EmptyState>().Count, Rows(driver));
        roles[1] = "Writer";
        driver.Settle();

        await Assert.That(none).IsEqualTo(1);
        await Assert.That(one.Item1).IsEqualTo(0);
        await Assert.That(one.Item2).IsEquivalentTo(["Designer"]);
        await Assert.That(driver.All<EmptyState>().Count).IsEqualTo(1);
    }

    static Node Last<T>(UiDriver driver, string row)
    {
        Node? found = null;
        foreach (var item in driver.All<Combobox.Item>())
        {
            if (item.Props<ComboboxItemProps>().Value != (UiKey)row)
                continue;
            foreach (var button in driver.All<T>())
            {
                for (var at = button.Parent; at is not null; at = at.Parent)
                {
                    if (ReferenceEquals(at, item))
                        found = button;
                }
            }
        }
        return found ?? throw new InvalidOperationException($"No {typeof(T).Name} in {row}.");
    }
}

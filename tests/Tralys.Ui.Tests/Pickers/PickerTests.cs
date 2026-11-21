using Tralys.Ui.Testing;
using Tralys.Ui.Tests;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components.Tests;

public sealed class PickerFieldTests
{
    static readonly Slot Body = Slot.Of(static ui =>
    {
        using (ui.Box(width: 40, height: 20)) { }
    });

    [Test]
    public async Task A_click_opens_it_once_space_toggles_it_and_its_popup_is_as_wide_as_the_field_or_its_minimum()
    {
        var reports = new Reports();
        var wide = new Switches();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.PickerField(
                    text: "Short",
                    content: Body,
                    onOpenChange: reports.OnOpenChange,
                    popupMinWidth: wide.A ? 300 : 0,
                    width: 100
                );
        });

        driver.Click(driver.RootOf<PickerField>());
        var clicked = (driver.View.Layers.Length, driver.RootOf<Overlay.Frame>().ViewBounds.Width);
        driver.Press(Key.Space);
        var spaced = driver.View.Layers.Length;
        driver.Press(Key.Space);
        wide.A = true;
        driver.Rerender();

        await Assert.That(clicked).IsEqualTo((1, 100f));
        await Assert.That(spaced).IsEqualTo(0);
        await Assert.That(reports.Opens).IsEquivalentTo([true, false, true]);
        await Assert.That(driver.RootOf<Overlay.Frame>().ViewBounds.Width).IsEqualTo(300f);
    }

    [Test]
    public async Task A_field_widened_while_open_places_its_bottom_end_popup_by_its_new_width_in_the_same_update()
    {
        var wide = new Switches();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, padding: new Thickness(200f, 0f, 0f, 0f)))
                ui.PickerField(
                    text: "Short",
                    content: Body,
                    placement: PlacementMode.BottomEnd,
                    defaultOpen: true,
                    width: wide.A ? 160 : 100
                );
        });

        wide.A = true;
        driver.Rerender();
        var frame = driver.RootOf<Overlay.Frame>().ViewBounds;

        await Assert.That((frame.Width, frame.Right)).IsEqualTo((160f, 360f));
    }
}

public sealed class CollectionPickerTests
{
    static readonly IItemsSource<string> Garments = Make();

    static IItemsSource<string> Make()
    {
        string[] cuts = ["Silk", "Linen", "Leather", "Velvet", "Wool", "Cotton"];
        string[] kinds = ["Robe", "Tunic", "Cloak", "Vest", "Sash"];
        var names = new string[cuts.Length * kinds.Length];
        for (var i = 0; i < names.Length; i++)
            names[i] = $"{cuts[i % cuts.Length]} {kinds[i / cuts.Length]}";
        return ItemsSource.From(names);
    }

    static UiDriver Render(Picked picked, IItemsSource<string>? items = null, float width = 240) =>
        UiDriver
            .Render(ui =>
            {
                using (ui.Box(align: Align.Start))
                    ui.CollectionPicker(
                        items: items ?? Garments,
                        itemKey: Towns.ByName,
                        itemText: Towns.Named,
                        value: picked.Value,
                        onValueChange: picked.OnValueChange,
                        onOpenChange: picked.OnOpenChange,
                        width: width
                    );
            })
            .WithIcons();

    static string[] Shown(UiDriver driver) =>
        [
            .. driver
                .All<CollectionPickerEntry<string>>()
                .Select(n => n.Props<CollectionPickerEntryProps<string>>().Item),
        ];

    static Node Entry(UiDriver driver, string name) =>
        Menus.RootOf(
            driver
                .All<CollectionPickerEntry<string>>()
                .Single(n => n.Props<CollectionPickerEntryProps<string>>().Item == name)
        );

    static bool SearchFocused(UiDriver driver)
    {
        for (var at = driver.Focused; at is not null; at = at.Parent)
        {
            if (at.Component == typeof(SearchInput))
                return true;
        }
        return false;
    }

    [Test]
    public async Task Opening_focuses_its_search_which_keeps_the_items_holding_it_and_enter_with_one_left_picks_it_and_closes()
    {
        var picked = new Picked();
        using var driver = Render(picked);

        driver.Click(driver.RootOf<CollectionPicker<string>>());
        var focused = SearchFocused(driver);
        driver.Type("VELvet");
        var velvet = Shown(driver);
        driver.Press(Key.Enter);
        var many = (picked.Values.Count, driver.View.Layers.Length);
        driver.Type(" robe");
        driver.Press(Key.Enter);
        driver.Click(driver.RootOf<CollectionPicker<string>>());
        var reopened = Shown(driver).Length;
        driver.Type("zz");

        await Assert.That(focused).IsTrue();
        await Assert
            .That(velvet)
            .IsEquivalentTo([
                "Velvet Robe",
                "Velvet Tunic",
                "Velvet Cloak",
                "Velvet Vest",
                "Velvet Sash",
            ]);
        await Assert.That(many).IsEqualTo((0, 1));
        await Assert.That(picked.Values).IsEquivalentTo(["Velvet Robe"]);
        await Assert.That(picked.Opens).IsEquivalentTo([true, false, true]);
        await Assert.That(reopened).IsGreaterThan(5);
        await Assert.That(driver.GetText("No results")).IsNotNull();
    }

    [Test]
    public async Task The_clear_button_in_its_search_empties_it_and_shows_every_item_again()
    {
        var picked = new Picked();
        using var driver = Render(picked);
        driver.Click(driver.RootOf<CollectionPicker<string>>());
        var every = Shown(driver);

        driver.Type("VELvet");
        var searched = Shown(driver).Length;
        driver.Click(driver.All<InputClear>()[0]);

        await Assert.That(searched).IsEqualTo(5);
        var search = await Assert.That(driver.Focused).IsNotNull();
        await Assert.That(search.Text.ToString()).IsEqualTo("");
        await Assert.That(Shown(driver)).IsEquivalentTo(every);
    }

    [Test]
    public async Task A_click_on_an_item_picks_it_and_closes_while_a_pressable_inside_another_runs_alone()
    {
        var picked = new Picked { Value = "Cape" };
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.CollectionPicker(
                    items: ItemsSource.From((string[])["Cape", "Hood", "Boots"]),
                    itemKey: Towns.ByName,
                    itemText: Towns.Named,
                    item: Slot<string>.Of(
                        clicks,
                        static (ui, c, name, _) =>
                        {
                            ui.Text(name, grow: 1f);
                            ui.Button(text: "Try", onClick: Callback.Of(c, static c => c.Value++));
                        }
                    ),
                    value: picked.Value,
                    onValueChange: picked.OnValueChange,
                    onOpenChange: picked.OnOpenChange,
                    defaultLayout: ListLayout.Rows,
                    defaultOpen: true,
                    width: 240
                );
        });

        driver.Click(Button(driver, "Hood"));
        var tried = (clicks.Value, picked.Values.Count, driver.View.Layers.Length);
        driver.Click(Entry(driver, "Boots"));

        await Assert.That(tried).IsEqualTo((1, 0, 1));
        await Assert.That(picked.Values).IsEquivalentTo(["Boots"]);
        await Assert.That(picked.Opens).IsEquivalentTo([false]);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    static Node Button(UiDriver driver, string row)
    {
        var entry = Entry(driver, row);
        foreach (var button in driver.All<Button>())
        {
            for (var at = button.Parent; at is not null; at = at.Parent)
            {
                if (ReferenceEquals(at, entry))
                    return button;
            }
        }
        throw new InvalidOperationException($"No button in {row}.");
    }

    [Test]
    public async Task Down_from_the_search_moves_into_the_items_whose_arrows_pick_each_one_and_enter_closes()
    {
        var picked = new Picked { Value = "Linen Tunic" };
        using var driver = Render(picked);
        driver.Click(driver.RootOf<CollectionPicker<string>>());

        driver.Press(Key.Down);
        var left = SearchFocused(driver);
        driver.Press(Key.Down);
        driver.Press(Key.Right);
        driver.Press(Key.Left);
        driver.Press(Key.Enter);

        await Assert.That(left).IsFalse();
        await Assert.That(picked.Values).IsEquivalentTo(["Silk Robe", "Linen Robe", "Silk Robe"]);
        await Assert.That(picked.Opens).IsEquivalentTo([true, false]);
    }

    [Test]
    public async Task Its_view_button_switches_grid_and_rows_showing_and_naming_what_it_switches_to()
    {
        var picked = new Picked();
        using var driver = Render(picked);
        driver.Click(driver.RootOf<CollectionPicker<string>>());
        static (ListLayout, string) Seen(UiDriver d) =>
            (
                d.Get<ListBox<string>>().Props<ListBoxProps<string>>().Layout,
                d.Get<Tooltip>().Props<TooltipProps>().Text.ToString()
            );

        var grid = Seen(driver);
        var gridIcon = driver.Get<Toggle>().Props<ToggleProps>().Value;
        driver.Click(driver.Get<Toggle>());
        var rows = Seen(driver);
        driver.Click(driver.Get<Toggle>());

        await Assert.That(grid).IsEqualTo((ListLayout.Grid, "List view"));
        await Assert.That(gridIcon).IsTrue();
        await Assert.That(rows).IsEqualTo((ListLayout.Rows, "Grid view"));
        await Assert.That(Seen(driver).Item1).IsEqualTo(ListLayout.Grid);
    }

    [Test]
    public async Task Its_popup_is_as_wide_as_the_field_or_360_at_its_edge_with_80_by_96_tiles_and_never_shows_a_null_item()
    {
        var picked = new Picked();
        IItemsSource<string?> holed = ItemsSource.From((string?[])["Cape", null, "Hood"]);
        using var narrow = UiDriver
            .Render(ui =>
            {
                using (ui.Box(align: Align.Start))
                    ui.CollectionPicker(
                        items: holed,
                        itemKey: static name => name,
                        itemText: static name => name,
                        value: picked.Value,
                        onValueChange: picked.OnValueChange,
                        onOpenChange: picked.OnOpenChange,
                        width: 200
                    );
            })
            .WithIcons();
        using var wide = Render(picked, width: 390);

        narrow.Click(narrow.RootOf<CollectionPicker<string?>>());
        wide.Click(wide.RootOf<CollectionPicker<string>>());
        var field = narrow.RootOf<CollectionPicker<string?>>().ViewBounds;
        var frame = narrow.RootOf<Overlay.Frame>().ViewBounds;
        var list = narrow.Get<ListBox<string?>>().Props<ListBoxProps<string?>>();
        narrow.Click(new System.Numerics.Vector2(frame.X + 20f, frame.Bottom - 1f));

        await Assert.That((frame.X, frame.Width)).IsEqualTo((field.X, 360f));
        await Assert.That(wide.RootOf<Overlay.Frame>().ViewBounds.Width).IsEqualTo(390f);
        await Assert.That((list.ItemWidth, list.ItemHeight)).IsEqualTo((80f, 96f));
        await Assert.That(Shown(narrow)).IsEquivalentTo(["Cape", "Hood"]);
        await Assert.That(narrow.View.Layers.Length).IsEqualTo(1);
    }
}

public sealed class PaginationTests
{
    [Test]
    public async Task It_reports_the_page_a_step_asks_for_and_shows_the_page_it_is_given_within_its_pages()
    {
        var page = new Paged { Value = 1 };
        using var driver = UiDriver
            .Render(ui =>
                ui.Pagination(
                    value: page.Value,
                    onValueChange: page.OnValueChange,
                    total: page.Total,
                    pageSize: 10,
                    width: 320
                )
            )
            .WithIcons();
        static bool[] Enabled(UiDriver d) =>
            [.. d.All<IconButton>().Select(b => (b.States & InputStates.Disabled) == 0)];

        driver.Click(driver.All<IconButton>()[2]);
        var stepped = (page.Reports.Count, driver.GetText("Page 1 of 5") is not null);
        page.Value = 5;
        driver.Rerender();
        var last = Enabled(driver);
        driver.Click(driver.All<IconButton>()[0]);
        page.Value = 7;
        driver.Rerender();
        var past = driver.GetText("Page 5 of 5") is not null;
        page.Value = 0;
        driver.Rerender();
        var before = (driver.GetText("Page 1 of 5") is not null, Enabled(driver));
        page.Value = 5;
        page.Total = 30;
        driver.Rerender();
        driver.Click(driver.All<IconButton>()[1]);

        await Assert.That(stepped).IsEqualTo((1, true));
        await Assert.That(last).IsEquivalentTo([true, true, false, false]);
        await Assert.That(past).IsTrue();
        await Assert.That(before.Item1).IsTrue();
        await Assert.That(before.Item2).IsEquivalentTo([false, false, true, true]);
        await Assert.That(page.Reports).IsEquivalentTo([2, 1, 2]);
    }

    [Test]
    public async Task One_page_or_none_renders_nothing()
    {
        var page = new Paged { Total = 30 };
        var size = new Counter();
        using var driver = UiDriver.Render(ui =>
            ui.Pagination(value: 1, total: page.Total, pageSize: size.Value)
        );

        var unpaged = Kids.Of(driver.Get<Pagination>()).Length;
        size.Value = 10;
        driver.Rerender();
        var paged = Kids.Of(driver.Get<Pagination>()).Length;
        page.Total = 10;
        driver.Rerender();

        await Assert.That((unpaged, paged)).IsEqualTo((0, 1));
        await Assert.That(Kids.Of(driver.Get<Pagination>()).Length).IsEqualTo(0);
    }

    sealed class Paged
    {
        public readonly List<int> Reports = [];
        public int Value;
        public int Total = 45;

        public Callback<int> OnValueChange =>
            Callback<int>.Of(this, static (p, page) => p.Reports.Add(page));
    }
}

public sealed class FilterButtonTests
{
    static readonly Slot Filters = Slot.Of(static ui =>
    {
        using (ui.Box(width: 120, height: 40)) { }
    });

    [Test]
    public async Task A_click_toggles_its_filters_below_its_end_edge_and_active_shows_a_dot_over_its_corner()
    {
        var reports = new Reports();
        var active = new Switches { A = true };
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(padding: 200, align: Align.Start))
                    ui.FilterButton(
                        content: Filters,
                        active: active.A,
                        onOpenChange: reports.OnOpenChange
                    );
            })
            .WithIcons();
        var button = OverlayProbes.BoxOf(driver.Get<IconButton>()).ViewBounds;
        var look = driver.Get<IconButton>().Props<IconButtonProps>();
        var dotNode = Kids.Of(Menus.RootOf(driver.Get<Popover>()))[^1];
        var dot = (dotNode.ViewBounds, dotNode.Visual.Background);

        driver.Click(driver.Get<IconButton>());
        var frame = Menus
            .RootOf(
                driver
                    .All<Overlay.Frame>()
                    .Single(f => f.Props<OverlayFrameProps>().Inset == OverlayInset.Block)
            )
            .ViewBounds;
        driver.Click(driver.Get<IconButton>());
        active.A = false;
        driver.Rerender();

        await Assert.That(reports.Opens).IsEquivalentTo([true, false]);
        await Assert.That((frame.Right, frame.Y)).IsEqualTo((button.Right, button.Bottom));
        await Assert
            .That((look.Variant, look.Scheme, look.Size))
            .IsEqualTo((ComponentVariant.Outlined, ColorScheme.Secondary, ComponentSize.Sm));
        await Assert
            .That(driver.Get<Tooltip>().Props<TooltipProps>().Text.ToString())
            .IsEqualTo("Filter");
        await Assert
            .That((dot.ViewBounds.Width, dot.ViewBounds.Right, dot.ViewBounds.Y))
            .IsEqualTo((6f, button.Right - 2f, button.Y + 2f));
        await Assert.That(dot.Background).IsEqualTo(Themes.Dark.Color(ColorToken.BgAccent));
        await Assert.That(Kids.Of(Menus.RootOf(driver.Get<Popover>())).Length).IsEqualTo(1);
    }
}

internal static class Kids
{
    public static Node[] Of(Node node)
    {
        var kids = new List<Node>();
        foreach (var child in node.Children)
            kids.Add(child);
        return [.. kids];
    }
}

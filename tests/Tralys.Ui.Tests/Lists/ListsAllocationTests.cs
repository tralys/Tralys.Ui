using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed partial class ListsAllocationTests
{
    static readonly string[] Names = ["alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta"];

    static readonly KeyOf<int> ById = static id => id;

    static readonly TextOf<int> Named = static id => Names[id % Names.Length];

    static readonly Slot<int> Card = Slot<int>.Of(
        static (ui, id, _) => ui.Text(Names[id % Names.Length])
    );

    static IItemsSource<int> Ids(int count)
    {
        var ids = new int[count];
        for (var i = 0; i < count; i++)
            ids[i] = i;
        return ItemsSource.From(ids);
    }

    [Test]
    public async Task Lighting_a_different_row_every_frame_allocates_nothing()
    {
        var items = Ids(600);
        using var driver = UiDriver.Render(
            ui => ui.ListBox(items: items, itemKey: ById, itemText: Named, width: 200, height: 500),
            new Size(400, 600)
        );
        var walk = new Walk
        {
            Rows =
            [
                .. driver
                    .All<ListBox.Row>()
                    .Select(Root)
                    .Where(static r => r.ViewBounds.Bottom <= 500f),
            ],
        };

        var bytes = driver.WorstFrameBytes(
            walk,
            static (d, w) =>
            {
                d.Hover(w.Rows[w.Tick++ % w.Rows.Length], settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(walk.Rows.Length).IsGreaterThan(10);
    }

    [Test]
    public async Task Mounting_an_overflowing_list_box_with_its_bar_again_every_other_frame_allocates_nothing()
    {
        var items = Ids(600);
        var model = new Model();
        using var driver = UiDriver.Render(
            ui =>
            {
                if ((model.Count & 1) == 0)
                    ui.ListBox(
                        items: items,
                        itemKey: ById,
                        itemText: Named,
                        width: 200,
                        height: 300
                    );
            },
            new Size(400, 400)
        );
        var bars = driver.All<Scrollbar>().Count;

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(bars).IsEqualTo(1);
    }

    [Test]
    public async Task Moving_the_selection_down_every_frame_and_home_at_the_end_reporting_value_typed_ids_allocates_nothing()
    {
        var items = Ids(200);
        var pick = new IdPick();
        using var driver = UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: items,
                    itemKey: ById,
                    itemText: Named,
                    value: pick.Value,
                    onValueChange: pick.Change,
                    width: 200,
                    height: 100
                ),
            new Size(400, 300)
        );
        driver.Focus(Nodes.Find(driver.Root, NodeKind.VirtualList));
        driver.Press(Key.Down);

        var bytes = driver.WorstFrameBytes(
            pick,
            static (d, p) =>
            {
                d.Press(p.Value == 199 ? Key.Home : Key.Down, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(pick.Reports).IsGreaterThan(200);
    }

    [Test]
    public async Task Wheeling_a_thousand_card_grid_a_notch_every_frame_allocates_nothing()
    {
        var items = Ids(1000);
        using var driver = UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: items,
                    itemKey: ById,
                    itemText: Named,
                    card: Card,
                    layout: ListLayout.Grid,
                    itemWidth: 60,
                    itemHeight: 60,
                    fillWidth: true,
                    width: 400,
                    height: 300
                ),
            new Size(600, 400)
        );
        var walk = new Walk { List = Nodes.Find(driver.Root, NodeKind.VirtualList) };

        var bytes = driver.WorstFrameBytes(
            walk,
            static (d, w) =>
            {
                d.Wheel(w.List!, new Vector2(0, w.Tick++ % 40 < 30 ? -1 : 1), settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Moving_a_grids_selection_by_the_arrows_every_frame_allocates_nothing()
    {
        var items = Ids(1000);
        var pick = new IdPick();
        using var driver = UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: items,
                    itemKey: ById,
                    itemText: Named,
                    card: Card,
                    layout: ListLayout.Grid,
                    itemWidth: 60,
                    itemHeight: 60,
                    value: pick.Value,
                    onValueChange: pick.Change,
                    width: 400,
                    height: 300
                ),
            new Size(600, 400)
        );
        driver.Focus(Nodes.Find(driver.Root, NodeKind.VirtualList));
        Key[] keys = [Key.Right, Key.Down, Key.Down, Key.Down, Key.Left];
        var walk = new Walk { Keys = keys };

        var bytes = driver.WorstFrameBytes(
            walk,
            static (d, w) =>
            {
                d.Press(w.Keys![w.Tick++ % w.Keys.Length], settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(pick.Reports).IsGreaterThan(50);
    }

    [Test]
    public async Task Refiltering_a_card_grid_every_other_frame_as_it_wheels_allocates_nothing()
    {
        var shelf = new Shelf(400);
        Func<string, bool>?[] filters =
        [
            null,
            static text => text.Length > 0 && (text[^1] - '0') % 2 == 0,
            static text => text.Length > 0 && (text[^1] - '0') % 3 == 0,
            static text => text.Length > 0 && (text[^1] - '0') == 7,
        ];
        using var driver = UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: shelf,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    card: Slot<string>.Of(static (ui, text, _) => ui.Text(text)),
                    layout: ListLayout.Grid,
                    itemWidth: 60,
                    itemHeight: 60,
                    width: 400,
                    height: 300
                ),
            new Size(600, 400)
        );
        var walk = new Refilter(shelf, filters, Nodes.Find(driver.Root, NodeKind.VirtualList));

        var bytes = driver.WorstFrameBytes(
            walk,
            static (d, w) =>
            {
                if (w.Tick % 2 == 0)
                    w.Shelf.Filter(w.Filters[w.Tick / 2 % w.Filters.Length]);
                d.Wheel(w.List, new Vector2(0, w.Tick++ % 12 < 6 ? -1 : 1), settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Refiltering_a_grouped_lists_items_every_other_frame_allocates_nothing()
    {
        var shelves = new Shelf[4];
        var groups = new Crew[shelves.Length];
        for (var g = 0; g < groups.Length; g++)
        {
            shelves[g] = new Shelf(12);
            groups[g] = new Crew($"g{g}", shelves[g]);
        }
        Func<string, bool>?[] filters =
        [
            null,
            static text => (text[^1] - '0') % 2 == 0,
            static text => (text[^1] - '0') % 3 == 0,
        ];
        var source = ItemsSource.From(groups);
        using var driver = UiDriver.Render(
            ui =>
                ui.GroupedList(
                    groups: source,
                    groupKey: Crew.ByName,
                    groupItems: Crew.Members,
                    groupText: Crew.Named,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    realizeAll: true,
                    width: 200,
                    height: 300
                ),
            new Size(400, 400)
        );

        var bytes = driver.WorstFrameBytes(
            new Refilters(shelves, filters),
            static (d, w) =>
            {
                if (w.Tick % 2 == 0)
                    foreach (var shelf in w.Shelves)
                        shelf.Filter(w.Filters[w.Tick / 2 % w.Filters.Length]);
                w.Tick++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Wheeling_a_grouped_list_and_folding_its_focused_group_every_thirty_frames_allocates_nothing()
    {
        var groups = new Crew[20];
        for (var g = 0; g < groups.Length; g++)
            groups[g] = new Crew($"g{g}", $"g{g}a", $"g{g}b", $"g{g}c");
        var source = ItemsSource.From(groups);
        using var driver = UiDriver.Render(
            ui =>
                ui.GroupedList(
                    groups: source,
                    groupKey: Crew.ByName,
                    groupItems: Crew.Members,
                    groupText: Crew.Named,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    realizeAll: true,
                    width: 200,
                    height: 300
                ),
            new Size(400, 400)
        );
        var walk = new Walk
        {
            List = Nodes.Find(driver.Root, NodeKind.VirtualList),
            Rows = [Rows.Of(driver, "g0")],
        };
        driver.Focus(walk.Rows[0]);

        var bytes = driver.WorstFrameBytes(
            walk,
            static (d, w) =>
            {
                var step = w.Tick % 30;
                if (step == 29)
                    d.Press(Key.Space, settle: false);
                else
                    d.Wheel(w.List!, new Vector2(0, step < 15 ? -1 : 1), settle: false);
                w.Tick++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Ticking_rows_of_an_open_multi_select_by_the_arrows_and_space_every_frame_allocates_nothing()
    {
        var pick = new ListPick { Items = Words.Of("a", "b", "c", "d", "e", "f", "g") };
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start))
                    ui.MultiSelect(
                        items: pick.Items,
                        itemKey: Words.ByText,
                        itemText: Words.Text,
                        value: pick.Selection,
                        onValueChange: pick.OnSelectionChange,
                        defaultOpen: true,
                        width: 200
                    );
            },
            new Size(400, 500)
        );
        pick.Toggled.Capacity = 4096;

        var bytes = driver.WorstFrameBytes(
            pick,
            static (d, p) =>
            {
                var step = p.Toggled.Count % 10;
                d.Press(step < 5 ? Key.Down : Key.Up, settle: false);
                d.Press(Key.Space, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(pick.Toggled.Count).IsGreaterThan(50);
    }

    static Node Root(Node component)
    {
        foreach (var child in component.Children)
            return child;
        throw new InvalidOperationException("No root.");
    }

    sealed class Walk
    {
        public int Tick;
        public Node[] Rows = [];
        public Node? List;
        public Key[]? Keys;
    }

    sealed class Refilters(Shelf[] shelves, Func<string, bool>?[] filters)
    {
        public readonly Shelf[] Shelves = shelves;
        public readonly Func<string, bool>?[] Filters = filters;
        public int Tick;
    }

    sealed class Refilter(Shelf shelf, Func<string, bool>?[] filters, Node list)
    {
        public readonly Shelf Shelf = shelf;
        public readonly Func<string, bool>?[] Filters = filters;
        public readonly Node List = list;
        public int Tick;
    }

    sealed partial class IdPick : ObservableObject
    {
        public int Reports;

        [ObservableProperty]
        public partial UiKey Value { get; set; }

        public Callback<UiKey> Change =>
            Callback<UiKey>.Of(
                this,
                static (p, key) =>
                {
                    p.Reports++;
                    p.Value = key;
                }
            );
    }
}

using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;
using static Tralys.Ui.Tests.Rows;

namespace Tralys.Ui.Tests;

public sealed class GroupedListTests
{
    static UiDriver Render(
        ListPick pick,
        IItemsSource<Crew> groups,
        float height = 0,
        ISelection? collapsed = null,
        Callback<SelectionChange> onCollapsed = default,
        bool realizeAll = false,
        Slot<string> item = default
    ) =>
        UiDriver.Render(
            ui =>
                ui.GroupedList(
                    groups: groups,
                    groupKey: Crew.ByName,
                    groupItems: Crew.Members,
                    groupText: Crew.Named,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    item: item,
                    value: pick.Value,
                    onValueChange: pick.OnValueChange,
                    collapsedGroups: collapsed,
                    onCollapsedGroupsChange: onCollapsed,
                    realizeAll: realizeAll,
                    width: 200,
                    height: height > 0 ? height : Length.Auto
                ),
            new Size(400, 400)
        );

    static IItemsSource<Crew> Two() =>
        ItemsSource.From(new[] { new Crew("one", "a1", "a2"), new Crew("two", "b1", "b2") });

    [Test]
    public async Task A_press_on_a_header_folds_and_unfolds_its_group_turning_its_chevron_and_selects_nothing()
    {
        var pick = new ListPick();
        using var driver = Render(pick, Two());

        driver.Click(Of(driver, "one"));
        var folded = (string.Join(" ", Shown(driver)), Chevron(driver, "one"));
        driver.Click(Of(driver, "one"));

        await Assert.That(folded).IsEqualTo(("one two b1 b2", 0f));
        await Assert.That(string.Join(" ", Shown(driver))).IsEqualTo("one a1 a2 two b1 b2");
        await Assert.That(Chevron(driver, "one")).IsEqualTo(90f);
        await Assert.That(pick.Reports).IsEmpty();
        await Assert.That(IsSelected(driver, "one")).IsFalse();
    }

    [Test]
    public async Task An_item_in_two_groups_shows_selected_in_the_row_a_press_or_an_arrow_reached()
    {
        var pick = new ListPick();
        using var driver = Render(
            pick,
            ItemsSource.From(
                new[]
                {
                    new Crew("first", "Shared", "Only first"),
                    new Crew("second", "Shared", "Only second"),
                }
            )
        );

        driver.Click(SecondShared(driver));
        var clicked = (Selected(driver), pick.Value.ToString());
        driver.Click(Of(driver, "Only second"));
        driver.Press(Key.Up);

        await Assert.That(clicked).IsEqualTo(("4", "Shared"));
        await Assert.That(Selected(driver)).IsEqualTo("4");
        await Assert.That(pick.Value.ToString()).IsEqualTo("Shared");
        await Assert.That(pick.Reports).IsEquivalentTo(["Shared", "Only second", "Shared"]);
    }

    [Test]
    public async Task Left_folds_a_focused_header_right_unfolds_it_and_the_arrows_never_select_a_header()
    {
        var pick = new ListPick();
        using var driver = Render(pick, Two());
        driver.Focus(Of(driver, "one"));

        driver.Press(Key.Left);
        var folded = string.Join(" ", Shown(driver));
        driver.Press(Key.Left);
        var again = string.Join(" ", Shown(driver));
        driver.Press(Key.Right);
        driver.Press(Key.Down);
        var down = pick.Value.ToString();
        driver.Press(Key.Up);

        await Assert.That(folded).IsEqualTo("one two b1 b2");
        await Assert.That(again).IsEqualTo(folded);
        await Assert.That(down).IsEqualTo("a1");
        await Assert.That(pick.Reports).IsEquivalentTo(["a1"]);
        await Assert.That(IsSelected(driver, "a1")).IsTrue();
        await Assert.That(driver.Focused).IsSameReferenceAs(Of(driver, "one"));
    }

    [Test]
    public async Task Space_or_enter_on_a_header_the_arrows_reached_folds_it_and_keeps_the_selected_row()
    {
        var pick = new ListPick();
        using var driver = Render(pick, Two());

        driver.Click(Of(driver, "a2"));
        driver.Press(Key.Down);
        driver.Press(Key.Space);
        var spaced = string.Join(" ", Shown(driver));
        driver.Press(Key.Enter);

        await Assert.That(spaced).IsEqualTo("one a1 a2 two");
        await Assert.That(string.Join(" ", Shown(driver))).IsEqualTo("one a1 a2 two b1 b2");
        await Assert.That(pick.Reports).IsEquivalentTo(["a2"]);
        await Assert.That(IsSelected(driver, "a2")).IsTrue();
    }

    [Test]
    public async Task A_row_a_closed_collapsible_still_holds_never_renders_an_entry_it_released()
    {
        var model = new Model { Name = "before" };
        var members = Words.List("a1", "a2", "a3");
        var groups = ItemsSource.From(new[] { new Crew("one", members) });
        var open = true;
        var shown = new List<string>();
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Collapsible(header: "Section", open: open))
                    ui.GroupedList(
                        groups: groups,
                        groupKey: Crew.ByName,
                        groupItems: Crew.Members,
                        groupText: Crew.Named,
                        itemKey: Words.ByText,
                        item: Slot<string>.Of(
                            Tuple.Create(model, shown),
                            static (ui, seen, name, index) =>
                            {
                                _ = seen.Item1.Name;
                                seen.Item2.Add(name);
                                ui.Text(name);
                            }
                        ),
                        width: 200,
                        height: 200
                    );
            },
            new Size(400, 400)
        );
        open = false;
        driver.Rerender();
        for (var i = 0; i < 30; i++)
            driver.Frame(0.1f);

        members.Remove("a1");
        driver.Frame();
        driver.Frame();
        driver.Frame();
        members.Remove("a2");
        driver.Frame();
        shown.Clear();
        model.Name = "after";
        driver.Frame();

        await Assert.That(shown).DoesNotContain(static name => name is null);
        await Assert.That(shown).DoesNotContain("a1");
    }

    [Test]
    public async Task Home_in_a_field_inside_a_row_moves_its_caret_not_the_list()
    {
        var pick = new ListPick();
        var text = new ObservableText("abcd");
        using var driver = Render(
            pick,
            Two(),
            item: Slot<string>.Of(
                text,
                static (ui, t, name, _) =>
                {
                    if (name == "a2")
                        ui.EditableText(text: t, width: 80);
                    else
                        ui.Text(name);
                }
            )
        );
        var field = Nodes.Find(driver.Root, NodeKind.EditableText);
        driver.Focus(field);

        driver.Press(Key.End);
        driver.Press(Key.Home);
        driver.Type("x");

        await Assert.That(driver.Focused).IsSameReferenceAs(field);
        await Assert.That(text.ToString()).IsEqualTo("xabcd");
        await Assert.That(pick.Reports).IsEmpty();
    }

    [Test]
    public async Task A_folded_groups_selected_item_stays_the_value_and_shows_selected_once_unfolded()
    {
        var pick = new ListPick { Value = "b1" };
        using var driver = Render(pick, Two());

        driver.Click(Of(driver, "two"));
        driver.Click(Of(driver, "two"));

        await Assert.That(IsSelected(driver, "b1")).IsTrue();
        await Assert.That(pick.Reports).IsEmpty();
    }

    [Test]
    public async Task A_value_set_from_outside_scrolls_its_row_into_view()
    {
        var pick = new ListPick();
        var groups = new Crew[10];
        for (var g = 0; g < groups.Length; g++)
            groups[g] = new Crew($"g{g}", $"g{g}a", $"g{g}b", $"g{g}c");
        using var driver = Render(pick, ItemsSource.From(groups), height: 100);

        pick.Value = "g8b";
        driver.Settle();
        var row = Of(driver, "g8b").ViewBounds;
        var list = List(driver).ViewBounds;

        await Assert.That(IsSelected(driver, "g8b")).IsTrue();
        await Assert.That(row.Y).IsGreaterThanOrEqualTo(list.Y);
        await Assert.That(row.Bottom).IsLessThanOrEqualTo(list.Bottom);
    }

    [Test]
    public async Task A_value_set_while_its_group_is_folded_scrolls_its_row_into_view_once_unfolded()
    {
        var pick = new ListPick();
        var groups = new Crew[10];
        for (var g = 0; g < groups.Length; g++)
            groups[g] = new Crew($"g{g}", $"g{g}a", $"g{g}b", $"g{g}c");
        var source = ItemsSource.From(groups);
        var folds = new SelectionSet();
        var fold = new SelectionChange(SelectionAction.Toggle, "g8", "g8");
        folds.Apply(fold, source, Crew.ByName);
        using var driver = Render(pick, source, height: 100, collapsed: folds);

        pick.Value = "g8b";
        driver.Settle();
        folds.Apply(fold, source, Crew.ByName);
        driver.Settle();
        var row = Of(driver, "g8b").ViewBounds;
        var list = List(driver).ViewBounds;

        await Assert.That(row.Y).IsGreaterThanOrEqualTo(list.Y);
        await Assert.That(row.Bottom).IsLessThanOrEqualTo(list.Bottom);
    }

    [Test]
    public async Task Given_its_folds_it_shows_them_reports_a_fold_and_shows_it_once_followed_and_a_fold_follows_its_groups_key()
    {
        var pick = new ListPick();
        var folds = new SelectionSet();
        IItemsSource<Crew> groups = Two();
        var reported = new List<SelectionChange>();
        folds.Apply(new SelectionChange(SelectionAction.Toggle, "two", "two"), groups, Crew.ByName);
        using var driver = UiDriver.Render(
            ui =>
                ui.GroupedList(
                    groups: groups,
                    groupKey: Crew.ByName,
                    groupItems: Crew.Members,
                    groupText: Crew.Named,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    collapsedGroups: folds,
                    onCollapsedGroupsChange: Callback<SelectionChange>.Of(
                        reported,
                        static (r, change) => r.Add(change)
                    ),
                    width: 200
                ),
            new Size(400, 400)
        );
        var given = string.Join(" ", Shown(driver));

        driver.Click(Of(driver, "one"));
        var unfollowed = string.Join(" ", Shown(driver));
        folds.Apply(reported[^1], groups, Crew.ByName);
        driver.Settle();
        var followed = string.Join(" ", Shown(driver));
        groups = ItemsSource.From(new[] { new Crew("one", "a1"), new Crew("two", "c1") });
        driver.Rerender();

        await Assert.That(given).IsEqualTo("one a1 a2 two");
        await Assert.That(unfollowed).IsEqualTo(given);
        await Assert.That(reported.Count).IsEqualTo(1);
        await Assert.That(reported[0].Key).IsEqualTo((UiKey)"one");
        await Assert.That(followed).IsEqualTo("one two");
        await Assert.That(string.Join(" ", Shown(driver))).IsEqualTo("one two");
    }

    [Test]
    public async Task A_header_pressed_in_a_list_nested_in_a_row_folds_only_the_inner_list()
    {
        var pick = new ListPick();
        var inner = Two();
        using var driver = Render(
            pick,
            ItemsSource.From(new[] { new Crew("outer", "host") }),
            item: Slot<string>.Of(
                inner,
                static (ui, groups, _, _) =>
                    ui.GroupedList(
                        groups: groups,
                        groupKey: Crew.ByName,
                        groupItems: Crew.Members,
                        groupText: Crew.Named,
                        itemKey: Words.ByText,
                        itemText: Words.Text,
                        realizeAll: true,
                        width: 150
                    )
            )
        );

        driver.Click(Of(driver, "one"));

        await Assert.That(Of(driver, "outer")).IsNotNull();
        await Assert.That(() => driver.GetText("a1")).Throws<InvalidOperationException>();
        await Assert.That(driver.GetText("b1")).IsNotNull();
    }

    [Test]
    public async Task Without_virtualization_every_row_is_built_from_the_first_layout()
    {
        var pick = new ListPick();
        var groups = new Crew[20];
        for (var g = 0; g < groups.Length; g++)
            groups[g] = new Crew($"g{g}", $"g{g}a", $"g{g}b");
        using var driver = Render(pick, ItemsSource.From(groups), height: 300, realizeAll: true);

        await Assert.That(Shown(driver).Count).IsEqualTo(60);
    }

    [Test]
    public async Task A_group_refilled_keeps_the_offset_and_rows_built_before_show_the_new_rows()
    {
        var pick = new ListPick();
        var rows = Words.List();
        for (var i = 0; i < 30; i++)
            rows.Add($"r{i}");
        var groups = ItemsSource.From(new[] { new Crew("g", rows) });
        using var driver = UiDriver.Render(
            ui =>
                ui.GroupedList(
                    groups: groups,
                    groupKey: Crew.ByName,
                    groupItems: Crew.Members,
                    groupText: Crew.Named,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    width: 200,
                    height: 200
                ),
            new Size(400, 400)
        );
        driver.Wheel(List(driver), new Vector2(0, -3));
        var offset = OffsetOf(driver);

        Refill(rows, "s");
        driver.Settle();
        var kept = OffsetOf(driver);
        var refilled = Shown(driver);
        rows.Clear();
        driver.Settle();
        var built = Descendants(List(driver));
        Refill(rows, "t");
        driver.Settle();
        await Assert.That(offset).IsGreaterThan(0f);
        await Assert.That(kept).IsEqualTo(offset);
        await Assert.That(refilled.All(static text => text[0] is 's' or 'g')).IsTrue();
        await Assert.That(RowNodes(driver).IsSubsetOf(built)).IsTrue();
        await Assert.That(Shown(driver).All(static text => text[0] is 't' or 'g')).IsTrue();
    }

    static void Refill(ObservableList<string> rows, string prefix)
    {
        rows.Clear();
        for (var i = 0; i < 30; i++)
            rows.Add(prefix + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Test]
    public async Task It_listens_to_its_groups_and_each_groups_items_while_it_shows_them_and_to_none_once_gone()
    {
        var pick = new ListPick();
        var a = new Heard();
        var b = new Heard();
        var groups = Words.List("A", "B");
        Func<string, IItemsSource<string>?> itemsOf = name => name == "A" ? a : b;
        var shown = true;
        using var driver = UiDriver.Render(ui =>
        {
            if (shown)
                ui.GroupedList(
                    groups: ItemsSource.From(groups),
                    groupKey: Words.ByText,
                    groupItems: itemsOf,
                    groupText: Words.Text,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    width: 200
                );
        });
        var both = (a.Listeners, b.Listeners);

        groups.Remove("B");
        driver.Settle();
        var pruned = (a.Listeners, b.Listeners);
        shown = false;
        driver.Rerender();

        await Assert.That(both).IsEqualTo((1, 1));
        await Assert.That(pruned).IsEqualTo((1, 0));
        await Assert.That((a.Listeners, b.Listeners)).IsEqualTo((0, 0));
    }

    [Test]
    public async Task A_row_that_renders_again_in_the_update_its_item_leaves_renders_that_item()
    {
        var leaving = new Model { Name = "leaving", Count = 1 };
        var members = new ObservableList<Model>(3)
        {
            leaving,
            new Model { Name = "staying", Count = 2 },
        };
        var items = ItemsSource.From(members);
        Func<string, IItemsSource<Model>?> itemsOf = _ => items;
        var rendered = new List<string>();
        using var driver = UiDriver.Render(ui =>
            ui.GroupedList(
                groups: Words.Of("team"),
                groupKey: Words.ByText,
                groupItems: itemsOf,
                groupText: Words.Text,
                itemKey: static model => model.Count,
                item: Slot<Model>.Of(
                    rendered,
                    static (ui, rendered, model, _) =>
                    {
                        rendered.Add(model.Name);
                        ui.Text(model.Name);
                    }
                ),
                width: 200
            )
        );
        rendered.Clear();

        leaving.Name = "left";
        members.Remove(leaving);
        members.Add(new Model { Name = "joining", Count = 3 });
        driver.Settle();

        await Assert.That(rendered).Contains("left");
        await Assert.That(string.Join(" ", Shown(driver))).IsEqualTo("team staying joining");
    }

    static string Selected(UiDriver driver)
    {
        var shown = Shown(driver);
        var selected = new List<string>();
        var nodes = AllRows(driver);
        for (var i = 0; i < nodes.Count; i++)
        {
            if ((nodes[i].States & InputStates.Selected) != 0)
                selected.Add(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return string.Join(" ", selected);
    }

    static HashSet<Node> Descendants(Node node)
    {
        var all = new HashSet<Node>();
        foreach (var child in node.Children)
        {
            all.Add(child);
            all.UnionWith(Descendants(child));
        }
        return all;
    }

    static List<Node> AllRows(UiDriver driver)
    {
        var rows = new List<Node>();
        foreach (var wrapper in List(driver).Children)
        {
            if (wrapper.SpareRow)
                continue;
            for (var n = wrapper; n is not null; n = Child(n))
            {
                if (n.RowFocusable)
                {
                    rows.Add(n);
                    break;
                }
            }
        }
        return rows;
    }

    static Node? Child(Node node)
    {
        foreach (var child in node.Children)
            return child;
        return null;
    }

    static Node SecondShared(UiDriver driver) => AllRows(driver)[4];

    static float Chevron(UiDriver driver, string header)
    {
        foreach (var child in Of(driver, header).Children)
        {
            if (child.Visual.Transform.Matrix.M12 is var sin && child.Kind == NodeKind.Box)
                return MathF.Round(MathF.Asin(Math.Clamp(sin, -1f, 1f)) * 180f / MathF.PI);
        }
        throw new InvalidOperationException("No chevron.");
    }

    static float OffsetOf(UiDriver driver) => List(driver).Scroller!.Offset.Y;

    static HashSet<Node> RowNodes(UiDriver driver) => [.. AllRows(driver)];
}

/// <summary>A group of a grouped list test, keyed and headed by its name.</summary>
internal sealed class Crew(string name, IItemsSource<string> members)
{
    public Crew(string name, params string[] members)
        : this(name, Words.Of(members)) { }

    public Crew(string name, ObservableList<string> members)
        : this(name, ItemsSource.From(members)) { }

    public string Name { get; } = name;

    public IItemsSource<string> People { get; } = members;

    public static readonly KeyOf<Crew> ByName = static crew => crew.Name;

    public static readonly TextOf<Crew> Named = static crew => crew.Name;

    public static readonly Func<Crew, IItemsSource<string>?> Members = static crew => crew.People;
}

/// <summary>A group's items that count who listens to them.</summary>
internal sealed class Heard : IItemsSource<string>
{
    readonly string[] _items = ["x", "y"];
    readonly List<IItemsSourceListener> _listeners = [];

    public int Listeners => _listeners.Count;

    public int Count => _items.Length;

    public object? this[int index] => _items[index];

    public string ItemAt(int index) => _items[index];

    public int IndexOf(object? item) => Array.IndexOf(_items, item);

    public void AddListener(IItemsSourceListener listener) => _listeners.Add(listener);

    public void RemoveListener(IItemsSourceListener listener) => _listeners.Remove(listener);
}

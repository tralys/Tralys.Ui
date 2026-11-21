using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

internal sealed record Member(string Name, string Role, int Level);

internal static class Team
{
    public static readonly KeyOf<Member> ByName = static m => m.Name;

    public static readonly TextOf<Member> Name = static m => m.Name;

    public static readonly TextOf<Member> Role = static m => m.Role;

    public static readonly Comparison<Member> Levels = static (a, b) => a.Level.CompareTo(b.Level);

    public static readonly Slot<Member> Level = Slot<Member>.Of(
        static (ui, m, _) => ui.Text(ui.Format($"{m.Level}"))
    );

    public static Member[] Five() =>
        [
            new("Alice", "Engineer", 120),
            new("Bram", "Designer", 98),
            new("Cyra", "Writer", 104),
            new("Doran", "Analyst", 87),
            new("Elin", "Manager", 112),
        ];

    public static Member[] Many(int count)
    {
        string[] roles = ["Engineer", "Designer", "Writer", "Analyst", "Manager"];
        var members = new Member[count];
        for (var i = 0; i < members.Length; i++)
            members[i] = new Member(
                $"Member {i + 1:D5}",
                roles[i % roles.Length],
                i * 37 % 150 + 1
            );
        return members;
    }

    public static TableColumn<Member>[] Columns(
        float? name = 160,
        float? role = 120,
        float? level = 80
    ) =>
        [
            new()
            {
                Header = "Name",
                SortKey = "Name",
                Width = name,
                Text = Name,
            },
            new()
            {
                Header = "Role",
                SortKey = "Role",
                Width = role,
                Text = Role,
            },
            new()
            {
                Header = "Level",
                SortKey = "Level",
                Width = level,
                Cell = Level,
                Compare = Levels,
            },
        ];
}

/// <summary>What a table reported, fed back as its props the way an app's state would be.</summary>
internal sealed partial class TableState : ObservableObject
{
    public readonly List<UiKey> Values = [];
    public readonly List<TableSort> Sorts = [];

    /// <summary>Whether each report joins its list; off, reports only count, allocating nothing.</summary>
    public bool Records = true;

    public int Reports;
    public readonly Callback<UiKey> OnValueChange;
    public readonly Callback<TableSort> OnSortChange;

    public TableState()
    {
        OnValueChange = Callback<UiKey>.Of(
            this,
            static (s, v) =>
            {
                s.Reports++;
                if (s.Records)
                    s.Values.Add(v);
                s.Value = v;
            }
        );
        OnSortChange = Callback<TableSort>.Of(
            this,
            static (s, v) =>
            {
                s.Reports++;
                if (s.Records)
                    s.Sorts.Add(v);
                s.Sort = v;
            }
        );
    }

    [ObservableProperty]
    public partial UiKey Value { get; set; }

    [ObservableProperty]
    public partial TableSort Sort { get; set; }
}

internal static class TableNodes
{
    public static Paint Dark(ColorToken token) => Themes.Dark.Color(token);

    public static Node Root(Node component)
    {
        foreach (var child in component.Children)
            return child;
        throw new InvalidOperationException("The component rendered nothing.");
    }

    public static Node Header(UiDriver driver, string text) =>
        driver
            .All<TableHeaderCell>()
            .Single(n => n.Props<TableHeaderCellProps>().Header.ToString() == text);

    public static string[] Headers(UiDriver driver) =>
        [
            .. driver
                .All<TableHeaderCell>()
                .OrderBy(n => Root(n).ViewBounds.X)
                .Select(n => n.Props<TableHeaderCellProps>().Header.ToString()),
        ];

    public static Node Gripper(UiDriver driver, string header)
    {
        var index = Header(driver, header).Props<TableHeaderCellProps>().Index;
        return driver.All<TableGripper>().Single(n => n.Props<TableGripperProps>().Index == index);
    }

    public static Node Row<TItem>(UiDriver driver, Func<TItem, bool> which) =>
        driver.All<Table.Row<TItem>>().Single(n => which(n.Props<TableRowProps<TItem>>().Item));

    public static Node Row(UiDriver driver, string name) =>
        Row<Member>(driver, m => m.Name == name);

    /// <summary>The names of the rows shown, top to bottom.</summary>
    public static string[] Shown(UiDriver driver) =>
        [
            .. driver
                .All<Table.Row<Member>>()
                .Where(n => !Root(n).Place.Collapsed)
                .OrderBy(n => Root(n).ViewBounds.Y)
                .Select(n => n.Props<TableRowProps<Member>>().Item.Name),
        ];

    /// <summary>The line marking where a dragged header would drop, or null while none shows.</summary>
    public static Node? DropLine(UiDriver driver)
    {
        var row = Root(driver.Get<Table.HeaderRow<Member>>());
        foreach (var child in row.Children)
        {
            if (child.Kind == NodeKind.Box && child.Place.Position == Position.Absolute)
                return child;
        }
        return null;
    }

    /// <summary>The box scrolling a table's columns sideways.</summary>
    public static ScrollHandle Sideways(UiDriver driver)
    {
        if (Root(Root(driver.Get<Table<Member>>())).Scroller is not { } sideways)
            throw new InvalidOperationException("The table scrolls no columns sideways.");
        return sideways;
    }

    /// <summary>The text of <paramref name="row"/>'s cell showing <paramref name="text"/>.</summary>
    public static Node Cell(Node row, string text) =>
        Walk(row).First(n => n.Kind == NodeKind.Text && n.Text.SequenceEqual(text));

    public static IEnumerable<Node> Walk(Node node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var below in Walk(child))
                yield return below;
        }
    }
}

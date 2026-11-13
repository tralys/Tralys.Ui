using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

/// <summary>A row of a test's tree.</summary>
public sealed class Leaf(int id, string name, params Leaf[] children) : TreeRow
{
    public static readonly KeyOf<Leaf> Key = static leaf => leaf.Id;

    public static readonly TextOf<Leaf> Named = static leaf => leaf.Name;

    public static readonly Slot<Leaf> Locking = Slot<Leaf>.Of(
        static (ui, leaf, _) => ui.TreeItem(text: leaf.Name, disabled: leaf.Locked, id: leaf.Name)
    );

    public int Id { get; } = id;

    public string Name { get; } = name;

    public List<Leaf> Children { get; } = [.. children];

    public bool Locked { get; init; }

    public int Score { get; set; }

    public Leaf Open()
    {
        IsExpanded = true;
        return this;
    }
}

/// <summary>A test's tree: its top rows, the rows a tree shows, the selection and what the tree reported.</summary>
public sealed partial class Grove : ObservableObject, ITreeChildren<Leaf>
{
    public readonly List<Leaf> Roots;
    public readonly TreeRows<Leaf> Rows;
    public readonly List<string> Picks = [];
    public readonly ValueLog<(bool Expanded, string Name)> Expansions = new(static change =>
        (change.Expanded ? "+" : "-") + change.Name
    );
    public bool Follows = true;
    public bool Applies = true;
    public bool Logs = true;

    public Grove(params Leaf[] roots)
    {
        Roots = [.. roots];
        Rows = new TreeRows<Leaf>(this);
        Rows.Refresh();
    }

    [ObservableProperty]
    public partial UiKey Value { get; set; }

    public Callback<UiKey> OnValueChange =>
        Callback<UiKey>.Of(
            this,
            static (g, key) =>
            {
                if (g.Logs)
                    g.Picks.Add(g.NameOf(key));
                if (g.Follows)
                    g.Value = key;
            }
        );

    public Callback<TreeExpandedChange<Leaf>> OnExpandedChange =>
        Callback<TreeExpandedChange<Leaf>>.Of(this, static (g, change) => g.Expanded(change));

    void Expanded(TreeExpandedChange<Leaf> change)
    {
        if (Logs)
            Expansions.Add((change.Expanded, change.Row.Name));
        if (Applies)
            change.Row.IsExpanded = change.Expanded;
    }

    public int ChildCount(Leaf? parent) => (parent?.Children ?? Roots).Count;

    public Leaf ChildAt(Leaf? parent, int index) => (parent?.Children ?? Roots)[index];

    public Leaf Find(string name) => Find(Roots, name) ?? throw new KeyNotFoundException(name);

    public UiKey KeyOf(string name) => Find(name).Id;

    public string NameOf(UiKey key)
    {
        if (!key.IsKeyed)
            return "none";
        key.TryGetLong(out var id);
        return FindId(Roots, id)?.Name ?? "?";
    }

    static Leaf? Find(List<Leaf> rows, string name)
    {
        foreach (var row in rows)
        {
            if (row.Name == name)
                return row;
            if (Find(row.Children, name) is { } found)
                return found;
        }
        return null;
    }

    static Leaf? FindId(List<Leaf> rows, long id)
    {
        foreach (var row in rows)
        {
            if (row.Id == id)
                return row;
            if (FindId(row.Children, id) is { } found)
                return found;
        }
        return null;
    }

    /// <summary>Staff (Engineers (Alice, Dave), Designers (Bob)), Contractors (Erin, Frank), with Staff and Engineers
    /// open.</summary>
    public static Grove Staff() =>
        new(
            new Leaf(
                1,
                "Staff",
                new Leaf(2, "Engineers", new Leaf(3, "Alice"), new Leaf(4, "Dave")).Open(),
                new Leaf(5, "Designers", new Leaf(6, "Bob"))
            ).Open(),
            new Leaf(7, "Contractors", new Leaf(8, "Erin"), new Leaf(9, "Frank"))
        );

    public UiDriver Render(
        Slot<Leaf> item = default,
        SelectionMode? selectionMode = null,
        float width = 240,
        float height = 300
    ) =>
        UiDriver
            .Render(ui =>
                ui.Tree(
                    items: Rows,
                    itemKey: Leaf.Key,
                    itemText: Leaf.Named,
                    item: item.IsEmpty ? Named : item,
                    value: Value,
                    onValueChange: OnValueChange,
                    selectionMode: selectionMode,
                    onExpandedChange: OnExpandedChange,
                    width: width,
                    height: height,
                    id: "tree"
                )
            )
            .WithIcons();

    static readonly Slot<Leaf> Named = Slot<Leaf>.Of(
        static (ui, leaf, _) => ui.TreeItem(text: leaf.Name, id: leaf.Name)
    );
}

internal static class Trees
{
    public static Node Row(UiDriver driver, string name) => driver.Get(name);

    public static bool Selected(UiDriver driver, string name) =>
        (driver.Get(name).States & InputStates.Selected) != 0;

    public static string? Focused(UiDriver driver) => driver.Focused?.Id;

    /// <summary>The names of the rows the tree shows, in order.</summary>
    public static string Shown(Grove grove)
    {
        var names = new List<string>();
        foreach (var row in grove.Rows)
            names.Add(row.Name);
        return string.Join(" ", names);
    }
}

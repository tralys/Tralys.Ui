using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

/// <summary>A row of a story's tree: a name, the rows within it, whether it is locked, and what it counts.</summary>
sealed class Branch(int id, string name, Branch[] children) : TreeRow
{
    public static readonly KeyOf<Branch> Key = static b => b.Id;

    public static readonly TextOf<Branch> Named = static b => b.Name;

    public static readonly Callback<TreeExpandedChange<Branch>> Expand = Callback<
        TreeExpandedChange<Branch>
    >.Of(static change => change.Row.IsExpanded = change.Expanded);

    public int Id { get; } = id;

    public string Name { get; } = name;

    public Branch[] Children { get; } = children;

    public bool Locked { get; init; }

    public int Badge { get; init; }
}

/// <summary>A story's tree of branches as the rows a tree shows, laid out once.</summary>
sealed class Forest : ITreeChildren<Branch>
{
    readonly Branch[] _roots;

    Forest(Branch[] roots, int selected)
    {
        _roots = roots;
        Selected = selected < 0 ? default(UiKey) : selected;
        Rows = new TreeRows<Branch>(this);
        Rows.Refresh();
    }

    public TreeRows<Branch> Rows { get; }

    public UiKey Selected { get; }

    public int ChildCount(Branch? parent) => (parent?.Children ?? _roots).Length;

    public Branch ChildAt(Branch? parent, int index) => (parent?.Children ?? _roots)[index];

    public static Forest Staff()
    {
        var b = new Builder();
        return new(
            [
                b.Open(
                    "Staff",
                    b.Open("Engineers", b.Leaf("Alice (Lv 42)"), b.Leaf("Dave (Lv 31)")),
                    b.Shut("Designers", b.Leaf("Bob (Lv 38)"))
                ),
                b.Shut("Contractors", b.Leaf("Erin (Lv 1)"), b.Leaf("Frank (Lv 99)")),
            ],
            -1
        );
    }

    public static Forest Lisbon()
    {
        var b = new Builder();
        var bakery = b.Leaf("Bakery");
        return new(
            [b.Open("Lisbon", b.Open("Baixa", b.Open("Shops", bakery, b.Leaf("Pharmacy"))))],
            bakery.Id
        );
    }

    public static Forest Locks()
    {
        var b = new Builder();
        return new(
            [
                b.Open("Unlocked", b.Leaf("Reports"), b.Locked("Payroll (locked)")),
                b.Locked("Locked branch", b.Leaf("Hidden")),
            ],
            -1
        );
    }

    public static Forest LongLabels()
    {
        var b = new Builder();
        var child = b.Leaf("A child whose name also runs past the edge");
        return new([b.Open("A folder whose name runs much wider than the tree", child)], child.Id);
    }

    public static Forest Atlas()
    {
        var b = new Builder();
        return new(
            [
                b.Open("Portugal", b.Leaf("Porto"), b.Leaf("Faro")),
                b.Open(
                    "Japan",
                    b.Open("Tokyo", b.Leaf("Shibuya"), b.Leaf("Ginza")),
                    b.Leaf("Kyoto")
                ),
                b.Shut("Kenya", b.Leaf("Nairobi")),
            ],
            -1
        );
    }

    /// <summary>Ten regions of ten towns of ten districts, 1,110 rows; with <paramref name="opened"/>, the first region
    /// and its first town open.</summary>
    public static Forest Thousand(bool opened)
    {
        var b = new Builder();
        var regions = b.Nest(3, "Region");
        if (opened)
        {
            regions[0].IsExpanded = true;
            regions[0].Children[0].IsExpanded = true;
        }
        return new(regions, -1);
    }

    public static Forest States()
    {
        var b = new Builder();
        var selected = b.Leaf("Selected leaf");
        return new(
            [
                b.Open(
                    "Open, with a selected leaf",
                    selected,
                    b.Locked("Disabled leaf"),
                    b.Shut("Shut, with leaves", b.Leaf("Hidden until opened"))
                ),
                new Branch(b.Next(), "An element header", []) { Badge = 2 },
            ],
            selected.Id
        );
    }

    public static Forest Levels()
    {
        var b = new Builder();
        return new([b.Open("First level", b.Open("Second level", b.Leaf("Third level")))], -1);
    }

    sealed class Builder
    {
        int _next;

        public int Next() => _next++;

        public Branch Leaf(string name) => new(_next++, name, []);

        public Branch Locked(string name, params Branch[] children) =>
            new(_next++, name, children) { Locked = true };

        public Branch Shut(string name, params Branch[] children) => new(_next++, name, children);

        public Branch Open(string name, params Branch[] children) =>
            new(_next++, name, children) { IsExpanded = true };

        public Branch[] Nest(int depth, string kind)
        {
            var branches = new Branch[10];
            for (var i = 0; i < branches.Length; i++)
                branches[i] = new(
                    _next++,
                    $"{kind} {i + 1}",
                    depth > 1 ? Nest(depth - 1, depth == 3 ? "Town" : "District") : []
                );
            return branches;
        }
    }
}

using System.Runtime.CompilerServices;
using Tralys.Ui.Controls;
using TUnit.Assertions.Enums;

namespace Tralys.Ui.Engine.Tests;

public sealed class TreeRowsTests
{
    [Test]
    public async Task AFilter_ShowsEachMatchBeneathEveryRowAboveIt_AndNoOtherRow()
    {
        var forest = Forest.Fleet();
        var rows = forest.Rows();
        rows.Filter = new RowFilter<Node>(static node => node.Name == "Ace");

        await Assert
            .That(Names(rows))
            .IsEquivalentTo(["Fleet", "Wing", "Ace"], CollectionOrdering.Matching);
        await Assert.That(Depths(rows)).IsEquivalentTo([0, 1, 2], CollectionOrdering.Matching);
        await Assert.That(rows[1].HasChildren && !rows[2].HasChildren).IsTrue();
    }

    [Test]
    public async Task ACollapsedRow_HidesEveryRowBeneathIt_WhichShowAsTheyWereOnceItExpands()
    {
        var forest = Forest.Fleet();
        var rows = forest.Rows();
        var all = Names(rows);

        forest.Find("Fleet").IsExpanded = false;
        var collapsed = Names(rows);
        forest.Find("Fleet").IsExpanded = true;

        await Assert
            .That(all)
            .IsEquivalentTo(
                ["Fleet", "Wing", "Ace", "Rookie", "Tanker", "Depot", "Crate"],
                CollectionOrdering.Matching
            );
        await Assert
            .That(collapsed)
            .IsEquivalentTo(["Fleet", "Depot", "Crate"], CollectionOrdering.Matching);
        await Assert.That(Names(rows)).IsEquivalentTo(all, CollectionOrdering.Matching);
    }

    [Test]
    public async Task AComparer_OrdersEachRowsChildren_BeneathTheirOwnRow()
    {
        var forest = Forest.Fleet();
        var rows = forest.Rows();

        rows.Comparer = Comparer<Node>.Create(
            static (a, b) => string.CompareOrdinal(b.Name, a.Name)
        );

        await Assert
            .That(Names(rows))
            .IsEquivalentTo(
                ["Fleet", "Wing", "Rookie", "Ace", "Tanker", "Depot", "Crate"],
                CollectionOrdering.Matching
            );
    }

    [Test]
    public async Task AFiltersRefresh_LaysOutATreeListenedTo_AndOneUnheardOnceItIsListenedTo()
    {
        var rows = Forest.Fleet().Rows();
        var shown = "Ace";
        var filter = new RowFilter<Node>(node => node.Name == shown);
        rows.Filter = filter;

        shown = "Crate";
        filter.Refresh();
        rows.AddListener(new Deaf());
        var caughtUp = Names(rows);
        shown = "Tanker";
        filter.Refresh();

        await Assert.That(caughtUp).IsEquivalentTo(["Depot", "Crate"], CollectionOrdering.Matching);
        await Assert
            .That(Names(rows))
            .IsEquivalentTo(["Fleet", "Tanker"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task ATreeNoLongerListenedTo_IsNotKeptAliveByAFilterThatOutlivesIt()
    {
        var filter = new RowFilter<Node>(static _ => true);
        var tree = FilteredThenLeft(filter);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Assert.That(tree.IsAlive).IsFalse();
        GC.KeepAlive(filter);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference FilteredThenLeft(RowFilter filter)
    {
        var rows = Forest.Fleet().Rows();
        rows.Filter = filter;
        var listener = new Deaf();
        rows.AddListener(listener);
        rows.RemoveListener(listener);
        return new WeakReference(rows);
    }

    static List<string> Names(TreeRows<Node> rows) => [.. rows.Select(static row => row.Name)];

    static List<int> Depths(TreeRows<Node> rows) => [.. rows.Select(static row => row.Depth)];

    sealed class Deaf : IItemsSourceListener
    {
        public void OnItemsChanged(IItemsSource source, in ItemsChange change) { }
    }
}

/// <summary>A node of a test tree, holding its children.</summary>
sealed class Node(string name, params Node[] children) : TreeRow
{
    public string Name { get; } = name;

    public Node[] Children { get; } = children;

    public int Score { get; set; }

    public override string ToString() => Name;
}

/// <summary>A test tree, every row expanded.</summary>
sealed class Forest : ITreeChildren<Node>
{
    readonly Node[] _roots;

    public Forest(params Node[] roots)
    {
        _roots = roots;
        foreach (var root in roots)
            Expand(root);
    }

    public static Forest Fleet() =>
        new(
            new Node(
                "Fleet",
                new Node("Wing", new Node("Ace"), new Node("Rookie")),
                new Node("Tanker")
            ),
            new Node("Depot", new Node("Crate"))
        );

    public TreeRows<Node> Rows()
    {
        var rows = new TreeRows<Node>(this);
        rows.Refresh();
        return rows;
    }

    public Node Find(string name) =>
        Find(_roots, name) ?? throw new InvalidOperationException($"No node is named {name}.");

    public int ChildCount(Node? parent) => (parent?.Children ?? _roots).Length;

    public Node ChildAt(Node? parent, int index) => (parent?.Children ?? _roots)[index];

    static void Expand(Node node)
    {
        node.IsExpanded = true;
        foreach (var child in node.Children)
            Expand(child);
    }

    static Node? Find(Node[] nodes, string name)
    {
        foreach (var node in nodes)
        {
            if (node.Name == name)
                return node;
            if (Find(node.Children, name) is { } found)
                return found;
        }
        return null;
    }
}

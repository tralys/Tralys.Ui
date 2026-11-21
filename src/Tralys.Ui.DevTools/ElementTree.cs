using System.Diagnostics.CodeAnalysis;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui.DevTools;

internal sealed class InspectorRow : TreeRow
{
    const int NodeProperty = 3;

    Node _node;
    internal readonly InspectorModel Model;
    internal readonly Callback<bool> Hover;
    internal bool Hovered;
    internal int Generation;
    internal int Key;
    internal int ChildrenStamp = -1;
    internal readonly List<InspectorRow> Children = [];
    internal char[] Shown = new char[16];
    internal int ShownLength;

    public InspectorRow(InspectorModel model, Node node, int key)
    {
        Model = model;
        Hover = Callback<bool>.Of(this, static (row, hovered) => row.HoverChanged(hovered));
        Bind(node, key);
    }

    public Node Node
    {
        get
        {
            Observation.Read(this, NodeProperty);
            return _node;
        }
    }

    internal Node Peek => _node;

    [MemberNotNull(nameof(_node))]
    internal void Bind(Node node, int key)
    {
        _node = node;
        Generation = node.Generation;
        Key = key;
        ChildrenStamp = -1;
        Children.Clear();
        ShownLength = -1;
        NotifyObservers(NodeProperty);
    }

    internal void Touch() => NotifyObservers(NodeProperty);

    internal void Show(ReadOnlySpan<char> text)
    {
        if (Shown.Length < text.Length)
            Shown = new char[Math.Max(text.Length, Shown.Length * 2)];
        text.CopyTo(Shown);
        ShownLength = text.Length;
    }

    internal void HoverChanged(bool hovered)
    {
        Hovered = hovered;
        Model.HoverRow(_node, hovered);
    }
}

internal sealed class ElementTree : ITreeChildren<InspectorRow>
{
    readonly InspectorModel _model;
    readonly Dictionary<Node, InspectorRow> _rows = [];
    readonly Stack<InspectorRow> _spare = [];
    readonly List<InspectorRow> _top = [];
    readonly List<Node> _dead = [];
    readonly List<Node> _chain = [];
    int _topStamp = -1;
    int _stamp;
    int _nextKey;

    public ElementTree(InspectorModel model)
    {
        _model = model;
        Rows = new TreeRows<InspectorRow>(this);
    }

    public TreeRows<InspectorRow> Rows { get; }

    public int ChildCount(InspectorRow? parent) => ChildrenOf(parent).Count;

    public InspectorRow ChildAt(InspectorRow? parent, int index) => ChildrenOf(parent)[index];

    internal static bool Collapses(Node node) =>
        node.Kind is NodeKind.Fragment or NodeKind.ActionScope
        || node.Component == typeof(RootComponent);

    public void Rebuild()
    {
        _stamp++;
        using (Rows.DeferRefresh())
        {
            foreach (var (node, row) in _rows)
            {
                if (!node.IsMounted || node.Generation != row.Generation)
                    _dead.Add(node);
            }
            foreach (var node in _dead)
            {
                var row = _rows[node];
                _rows.Remove(node);
                row.IsExpanded = false;
                _spare.Push(row);
            }
            _dead.Clear();
        }
        Rows.Refresh();
    }

    public InspectorRow? RowOf(Node node)
    {
        if (Collapses(node) || !InspectorModel.IsWithin(node, _model.Root) || node == _model.Root)
            return null;
        return RowFor(node);
    }

    public int Reveal(Node node)
    {
        if (RowOf(node) is not { } target)
            return -1;
        var root = _model.Root;
        for (var n = node.Parent; n is not null && n != root; n = n.Parent)
        {
            if (!Collapses(n))
                _chain.Add(n);
        }
        using (Rows.DeferRefresh())
        {
            for (var i = _chain.Count - 1; i >= 0; i--)
                RowFor(_chain[i]).IsExpanded = true;
        }
        _chain.Clear();
        Rows.Refresh();
        return Rows.IndexOf(target);
    }

    public static void Recheck(InspectorRow row)
    {
        var node = row.Peek;
        if (node.Kind != NodeKind.Text)
            return;
        var text = node.Text;
        if (row.ShownLength != text.Length || !text.SequenceEqual(row.Shown.AsSpan(0, text.Length)))
            row.Touch();
    }

    List<InspectorRow> ChildrenOf(InspectorRow? parent)
    {
        if (parent is null)
        {
            if (_topStamp != _stamp)
            {
                _topStamp = _stamp;
                _top.Clear();
                Collect(_model.Root, _top);
            }
            return _top;
        }
        if (parent.ChildrenStamp != _stamp)
        {
            parent.ChildrenStamp = _stamp;
            parent.Children.Clear();
            if (parent.Peek.IsMounted && parent.Peek.Generation == parent.Generation)
                Collect(parent.Peek, parent.Children);
        }
        return parent.Children;
    }

    void Collect(Node node, List<InspectorRow> into)
    {
        foreach (var child in node.Children)
        {
            if (Collapses(child))
                Collect(child, into);
            else
                into.Add(RowFor(child));
        }
    }

    InspectorRow RowFor(Node node)
    {
        if (_rows.TryGetValue(node, out var row))
        {
            if (row.Generation == node.Generation)
                return row;
            row.IsExpanded = false;
            row.Bind(node, ++_nextKey);
            return row;
        }
        if (_spare.TryPop(out row))
            row.Bind(node, ++_nextKey);
        else
            row = new InspectorRow(_model, node, ++_nextKey);
        _rows[node] = row;
        return row;
    }

    public void Invalidate() => _stamp++;
}

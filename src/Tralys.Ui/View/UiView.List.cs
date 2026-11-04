namespace Tralys.Ui;

public sealed partial class UiView
{
    Node? _rowBarrier;

    Size ListMeasured(Node node, Avail width, Avail height)
    {
        var barrier = _rowBarrier;
        _rowBarrier = node;
        try
        {
            return ((ListPayload)node.Payload!).Measure(width, height);
        }
        finally
        {
            _rowBarrier = barrier;
        }
    }

    void ListArranged(Node node, Size size)
    {
        var barrier = _rowBarrier;
        _rowBarrier = node;
        bool remeasure;
        try
        {
            remeasure = ((ListPayload)node.Payload!).Arrange(size);
        }
        finally
        {
            _rowBarrier = barrier;
        }
        if (remeasure)
            InvalidateMeasure(node);
    }

    internal Node MountRow(
        Node list,
        ComponentHost host,
        ComponentHost? caller,
        string file,
        int line,
        UiKey key
    )
    {
        var node = RentNode(NodeKind.Component);
        node.File = file;
        node.Line = line;
        node.Key = key;
        list.InsertAfter(list._last, node);
        Mounted(node);
        node.Host = host;
        host.PassesPlacement = true;
        host.Mounted(this, node, caller);
        host.Dirty = true;
        return node;
    }

    internal void RenderRow(Node list, Node row)
    {
        Builder.Render(row.Host!, fromCaller: true);
        ResolveEnv(row, list.Env, force: false);
    }

    internal void RetireRow(Node row)
    {
        Departed(row);
        Retire(row);
        row.SpareRow = true;
        row.Place = new Placement { Collapsed = true };
        _compositeDirty = true;
    }

    static void Retire(Node node)
    {
        if (node.Host is { } host)
        {
            var caller = host.Caller;
            host.Unmounted();
            host.Caller = caller;
        }
        if (node.Hooks is { } hooks)
        {
            ref var items = ref hooks.Items;
            for (var i = items.Count - 1; i >= 0; i--)
            {
                if (items[i].SurvivesRecycle)
                    continue;
                items[i].Discard();
                items.RemoveAt(i);
            }
        }
        node.RowFocusable = false;
        node.SetRowSelected(false);
        for (var child = node._first; child is not null; child = child._next)
            Retire(child);
    }

    internal void ReuseRow(Node row, UiKey key, ComponentHost? caller)
    {
        row.SpareRow = false;
        row.Place = default;
        row.Key = key;
        Reuse(row, caller);
        row.Host!.Caller = caller;
        _compositeDirty = true;
    }

    void Reuse(Node node, ComponentHost? caller)
    {
        if (node.Host is { } host)
        {
            host.Mounted(this, node, host.Caller ?? caller);
            host.Dirty = true;
        }
        if (node.Hooks is { } hooks)
        {
            for (var i = 0; i < hooks.Items.Count; i++)
                hooks.Items[i].Recycled();
        }
        for (var child = node._first; child is not null; child = child._next)
            Reuse(child, caller);
    }

    internal void Report<T>(Callback<T> callback, T value) => Invoke(callback, value);

    internal void HandFocus(Node node, bool visible)
    {
        if (CanFocus(node))
            MoveFocus(node, FocusCause.Code, visible);
    }

    internal bool HoldsFocus(Node row) =>
        _focused is { } focused && row.IsAncestorOrSelfOf(focused);
}

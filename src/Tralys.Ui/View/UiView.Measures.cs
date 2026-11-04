namespace Tralys.Ui;

public sealed partial class UiView
{
    ValueList<PendingMeasure> _pendingMeasures;
    int _measuring;

    // Outside layout a change keeps its ancestors' measures until layout finds the sizes they read have moved.
    bool Deferring => _measuring == 0 && _rowBarrier is null;

    // A node's size feeds its parent's measure even when the node is a layout root.
    internal void InvalidateSize(Node node)
    {
        node.ClearCache();
        var parent = node.Kind is NodeKind.Layer or NodeKind.Root ? null : Measured(node._parent);
        if (!Deferring)
        {
            node.Dirty |= Dirty.Layout;
            InvalidateMeasure(parent ?? node);
            return;
        }
        MarkLayout(node);
        if (parent is not null)
            Hold(parent);
    }

    void ContentChanged(Node node)
    {
        if (Measured(node) is not { } changed)
            return;
        MarkLayout(changed);
        Hold(changed);
    }

    // A span's text is read by its paragraph, not measured on its own.
    static Node? Measured(Node? node)
    {
        for (var n = node; n is not null; n = n._parent)
        {
            if (!n.IsTransparent && n.Kind != NodeKind.Span)
                return n;
        }
        return null;
    }

    void MarkLayout(Node node)
    {
        node.Dirty |= Dirty.Layout;
        if (node.Kind == NodeKind.Layer)
            return;
        for (var n = node._parent; n is not null; n = n._parent)
        {
            if (n.IsTransparent)
                continue;
            if ((n.Dirty & Dirty.Layout) != 0)
                return;
            n.Dirty |= Dirty.Layout;
            if (n.Kind == NodeKind.Layer)
                return;
        }
    }

    void Hold(Node node)
    {
        if (node.MeasurePending || !node.MeasuresHeld)
            return;
        node.MeasurePending = true;
        _pendingMeasures.Add(new PendingMeasure(node, node.Generation, node.TakeCache()));
    }

    // Deepest first, so a node is measured again only after every change below it has settled.
    void SettleMeasures()
    {
        if (_pendingMeasures.Count == 0)
            return;
        _measuring++;
        try
        {
            while (_pendingMeasures.Count > 0)
            {
                var deepest = 0;
                for (var i = 0; i < _pendingMeasures.Count; i++)
                    deepest = Math.Max(deepest, _pendingMeasures[i].Node.Depth);
                for (var i = _pendingMeasures.Count - 1; i >= 0; i--)
                {
                    if (_pendingMeasures[i].Node.Depth != deepest)
                        continue;
                    var pending = _pendingMeasures[i];
                    _pendingMeasures.RemoveAtSwap(i);
                    var node = pending.Node;
                    node.MeasurePending = false;
                    if (
                        !node.IsMounted
                        || node.Generation != pending.Generation
                        || node.Kind is NodeKind.Layer or NodeKind.Root
                        || !Moved(node, pending.Cache)
                    )
                        continue;
                    if (Measured(node._parent) is { } parent)
                        Hold(parent);
                }
            }
        }
        finally
        {
            _measuring--;
        }
    }

    bool Moved(Node node, in Node.MeasureCache cache)
    {
        if (cache.Evicted || cache.Baselined)
            return true;
        var basis = node.PctBasis;
        var moved = false;
        for (var i = 0; i < cache.Count && !moved; i++)
        {
            var entry = cache.Entries[i];
            node.PctBasis = entry.Basis;
            moved = CachedContent(node, entry.Width, entry.Height) != entry.Size;
        }
        node.PctBasis = basis;
        return moved;
    }

    record struct PendingMeasure(Node Node, int Generation, Node.MeasureCache Cache);
}

using System.Numerics;

namespace Tralys.Ui;

public sealed partial class UiView
{
    Size CustomLayout(Node node, Avail width, Avail height, bool perform)
    {
        var payload = node.PayloadOf<LayoutPayload>();
        ref readonly var style = ref node.Layout;
        var pad = style.Padding;
        var border = style.Border;
        var inset = new Thickness(
            border.Left + pad.Left,
            border.Top + pad.Top,
            border.Right + pad.Right,
            border.Bottom + pad.Bottom
        );
        var contentW = width.IsExact
            ? MathF.Max(0f, width.Value - inset.Left - inset.Right)
            : float.NaN;
        var contentH = height.IsExact
            ? MathF.Max(0f, height.Value - inset.Top - inset.Bottom)
            : float.NaN;

        var start = _layoutChildren.Count;
        CollectFlow(node, ref _layoutChildren);
        var count = _layoutChildren.Count - start;
        for (var i = start; i < start + count; i++)
            _layoutChildren[i].PctBasis = new Vector2(contentW, contentH);
        var cx = new LayoutContext(this, node, payload, start, count, inset, perform);

        Size result;
        if (perform)
        {
            result = new Size(width.Value, height.Value);
            payload.Arrange(ref cx, result);
            PlaceAbsoluteChildren(node, pad);
        }
        else
        {
            result = payload.Measure(ref cx, AvailableSpace.Of(width), AvailableSpace.Of(height));
            if (!float.IsFinite(result.Width) || !float.IsFinite(result.Height))
                throw Faults.CustomLayout(payload.AlgorithmType, "measured a non-finite size");
        }
        _layoutChildren.Truncate(start);
        return result;
    }

    void PlaceAbsoluteChildren(Node node, Thickness pad)
    {
        var border = node.Layout.Border;
        var frame = node.QuarterTurned
            ? new Size(node.Frame.Height, node.Frame.Width)
            : node.Frame.Size;
        var paddingBox = Rect.FromEdges(
            border.Left,
            border.Top,
            frame.Width - border.Right,
            frame.Height - border.Bottom
        );
        var start = _layoutChildren.Count;
        CollectChildren(node, ref _layoutChildren);
        for (var c = start; c < _layoutChildren.Count; c++)
        {
            var child = _layoutChildren[c];
            if (child.Place.Position != Position.Absolute)
                continue;
            LayOutHidden(child, false);
            PlaceAbsolute(node, child, paddingBox, pad);
        }
        _layoutChildren.Truncate(start);
    }

    internal void LayOutHidden(Node child, bool hidden)
    {
        if (child.LayoutHidden == hidden)
            return;
        child.LayoutHidden = hidden;
        _compositeDirty = true;
        VisibilityFlipped(child);
        if (!hidden)
            ShownAgain(child);
    }

    static void CollectFlow(Node parent, ref ValueList<Node> into)
    {
        for (var child = parent._first; child is not null; child = child._next)
        {
            if (child.IsTransparent)
                CollectFlow(child, ref into);
            else if (
                !child.Place.Collapsed
                && child.Kind != NodeKind.Layer
                && child.Place.Position != Position.Absolute
            )
                into.Add(child);
        }
    }

    internal Node LayoutChild(int index) => _layoutChildren[index];

    internal void PlaceCustom(Node parent, Node child, Rect borderBox, Transform transform)
    {
        LayOutHidden(child, false);
        if (child.Placed != transform)
        {
            child.Placed = transform;
            _compositeDirty = true;
        }
        Arrange(
            child,
            new Rect(
                borderBox.X,
                borderBox.Y,
                MathF.Max(0f, borderBox.Width),
                MathF.Max(0f, borderBox.Height)
            ),
            ContentToView(parent),
            ContentSnappedToView(parent)
        );
    }
}

using Tralys.Ui.Rendering;

namespace Tralys.Ui;

public sealed partial class UiView
{
    const int MultipliedFade = -1;
    const int NoFadeLayer = -2;

    ValueList<Node> _faded;

    internal void OpacityChanged(Node node, float was, float now)
    {
        if (!(was > 0f && was < 1f && now > 0f && now < 1f))
        {
            _compositeDirty = true;
            return;
        }
        if (node.FadeQueued || !node.IsMounted)
            return;
        node.FadeQueued = true;
        _faded.Add(node);
    }

    // Group opacity over a subtree that draws at most one primitive multiplies into it: the same pixels, no layer.
    void PushFade(Node node, float opacity, DrawList list)
    {
        var count = 0;
        Node? owner = null;
        if (opacity > 0f && Single(node, ref count, ref owner))
        {
            if (list.RecordsSpans)
            {
                node.FadeLayer = MultipliedFade;
                node.FadeOwner = owner;
                node.FadeBase = list.Opacity;
                node.FadeList = list.Generation;
            }
            list.PushOpacity(opacity);
            return;
        }
        var outer = list.Layer;
        list.PushLayer(MathF.Max(0f, opacity));
        if (list.RecordsSpans)
        {
            node.FadeLayer = list.Layer != outer ? list.Layer : NoFadeLayer;
            node.FadeOwner = null;
            node.FadeList = list.Generation;
        }
    }

    bool Single(Node node, ref int count, ref Node? owner)
    {
        if (
            node.Layout.Overflow != Overflow.Visible
            || node.Visual.Mask.IsVisible
            || node.Kind == NodeKind.EditableText
        )
            return false;
        foreach (ref readonly var command in CommandsOf(node, FrameSize(node)).Commands)
        {
            switch (command.Op)
            {
                case RenderOp.PushTransform or RenderOp.PushOpacity or RenderOp.Pop:
                    continue;
                case RenderOp.Box
                or RenderOp.Ellipse
                or RenderOp.Arc
                or RenderOp.Line
                    when command.Fill.Kind != PaintKind.Image
                        || command.Stroke.Kind != PaintKind.Image:
                case RenderOp.Image or RenderOp.BoxShadow or RenderOp.InsetBoxShadow:
                    count++;
                    break;
                case RenderOp.Glyphs:
                    count += command.Count;
                    break;
                default:
                    return false;
            }
            if (count > 1)
                return false;
            owner = node;
        }
        return SingleBelow(node, ref count, ref owner);
    }

    bool SingleBelow(Node parent, ref int count, ref Node? owner)
    {
        for (var child = parent._first; child is not null; child = child._next)
        {
            if (
                child.Place.Collapsed
                || child.Place.Hidden
                || child.LayoutHidden
                || child.Kind is NodeKind.Layer or NodeKind.Span
            )
                continue;
            if (child.FaultHidden)
                return false;
            if (
                child.IsTransparent
                    ? !SingleBelow(child, ref count, ref owner)
                    : !Single(child, ref count, ref owner)
            )
                return false;
        }
        return true;
    }

    bool PatchFades(DrawList list)
    {
        for (var i = 0; i < _faded.Count; i++)
        {
            var node = _faded[i];
            if (!node.IsMounted || !Drawn(node))
                continue;
            if (node.FadeList != list.Generation)
                return false;
            var patched = node.FadeLayer switch
            {
                MultipliedFade => Multiply(TopFade(node, list.Generation), list),
                NoFadeLayer => false,
                _ => list.PatchLayerOpacity(node.FadeLayer, node.Visual.Opacity ?? 1f),
            };
            if (!patched)
                return false;
        }
        ClearFades();
        return true;
    }

    void ClearFades()
    {
        for (var i = 0; i < _faded.Count; i++)
            _faded[i].FadeQueued = false;
        _faded.Clear();
    }

    // The outermost multiplied fade over the same primitive; a fade above it that moved would leave its base stale.
    static Node TopFade(Node node, long generation)
    {
        var top = node;
        for (var n = node._parent; n is not null && n.Kind != NodeKind.Layer; n = n._parent)
        {
            if (n.FadeList != generation || !((n.Visual.Opacity ?? 1f) < 1f))
                continue;
            if (n.FadeLayer != MultipliedFade)
                break;
            top = n;
        }
        return top;
    }

    static bool Multiply(Node top, DrawList list)
    {
        if (top.FadeOwner is not { } owner)
            return true;
        if (
            !owner.IsMounted
            || owner.Span < 0
            || owner.SpanList != list.Generation
            || owner.Commands is not { } commands
        )
            return false;
        var under = top.FadeBase * Math.Clamp(top.Visual.Opacity ?? 1f, 0f, 1f);
        if (!list.BeginPatchAtOpacity(owner.Span, OpacityAt(owner, top, under)))
            return false;
        list.Draw(commands);
        return list.EndPatch();
    }

    // Multiplied top-down, as the pushes of a rebuild are, so the patch lands on the same floats.
    static float OpacityAt(Node node, Node top, float underTop)
    {
        if (node == top || node._parent is not { } parent)
            return underTop;
        var above = OpacityAt(parent, top, underTop);
        var opacity = node.Visual.Opacity ?? 1f;
        return opacity < 1f ? above * Math.Clamp(opacity, 0f, 1f) : above;
    }
}

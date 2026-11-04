using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Rendering;

namespace Tralys.Ui;

/// <summary>Layers over the root, and positioners that place one against an anchor.</summary>
[FramePath]
public static class Overlays
{
    /// <summary>A layer over the root, filling the view inside its safe insets, drawn above the root and hit before it.
    /// Inside a box that <c>containsLayers</c> it fills the part of that box in view instead, is clipped to it, takes
    /// only the presses that land there, and takes keys and Escape only while the focus is inside the box.
    /// It belongs to the node it is written in, its owner, for context, keys, bubbling and environment, and lives as
    /// long as this call. It dismisses only as <c>dismissOn</c> says, reporting through <c>onDismiss</c>; a press
    /// counts as inside when it lands in <c>content</c> (the root by default), in a layer opened from inside it, or in
    /// <c>dismissExempt</c>. With <c>restoreFocus</c>, closing with focus inside gives focus back to what had it when
    /// it opened; with <c>autoFocus</c>, its content's first stop takes focus once it shows. A <c>modal</c> root hits
    /// over its whole bounds, keeps Tab inside and takes focus off what is outside.</summary>
    public static Scope Layer(
        this Ui ui,
        LayerHit hit = default,
        Callback onDismiss = default,
        Dismiss dismissOn = default,
        bool restoreFocus = false,
        bool autoFocus = false,
        LayerTier tier = default,
        NodeRef? dismissExempt = null,
        NodeRef? content = null,
        Direction direction = default,
        FlexWrap wrap = default,
        Align align = default,
        Justify justify = default,
        AlignContent alignContent = default,
        float gap = 0,
        float? rowGap = null,
        float? columnGap = null,
        Thickness padding = default,
        Overflow overflow = default,
        Paint background = default,
        Border border = default,
        CornerRadius radius = default,
        BoxShadow shadow = default,
        Paint mask = default,
        bool modal = false,
        string? id = null,
        NodeRef? nodeRef = null,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        Open(
            ui,
            new LayerProps
            {
                Hit = hit,
                OnDismiss = onDismiss,
                DismissOn = dismissOn,
                RestoreFocus = restoreFocus,
                AutoFocus = autoFocus,
                Tier = tier,
                DismissExempt = dismissExempt,
                Content = content,
            },
            new LayoutStyle
            {
                Direction = direction,
                Wrap = wrap,
                Align = align,
                Justify = justify,
                AlignContent = alignContent,
                RowGap = rowGap ?? gap,
                ColumnGap = columnGap ?? gap,
                Padding = padding,
                Border = border.Width,
                Overflow = overflow,
            },
            new VisualStyle
            {
                Background = background,
                BorderPaint = border.Paint,
                Radius = radius,
                Shadow = shadow,
                Mask = mask,
            },
            modal,
            id,
            nodeRef,
            key,
            file,
            line
        );

    /// <summary>A layer whose root is placed against <c>anchor</c> (by default the node it is written in): on the side
    /// and along it as <c>placement</c> says (<see cref="PlacementMode.BottomStart"/> by default), <c>sideOffset</c>
    /// away on whichever side it lands, then moved by <c>offset</c>, never mirrored. It flips to the other side only
    /// when it fits there wholly, shrinks to the larger side's room when it fits neither, slides back into the view,
    /// or the box that contains it as for <see cref="Layer"/>, inside <c>collisionPadding</c> along it, and follows its
    /// anchor through layout, scrolling and transforms.
    /// <c>matchAnchorWidth</c> lays its root out at the anchor's width. Dismissal and focus are as for
    /// <see cref="Layer"/>.</summary>
    public static Scope Positioner(
        this Ui ui,
        Anchor anchor = default,
        PlacementMode? placement = null,
        float sideOffset = 0,
        Vector2 offset = default,
        bool matchAnchorWidth = false,
        Thickness collisionPadding = default,
        LayerHit hit = default,
        Callback onDismiss = default,
        Dismiss dismissOn = default,
        bool restoreFocus = false,
        bool autoFocus = false,
        LayerTier tier = default,
        NodeRef? dismissExempt = null,
        Direction direction = default,
        FlexWrap wrap = default,
        Align align = default,
        Justify justify = default,
        AlignContent alignContent = default,
        float gap = 0,
        float? rowGap = null,
        float? columnGap = null,
        Thickness padding = default,
        Overflow overflow = default,
        Paint background = default,
        Border border = default,
        CornerRadius radius = default,
        BoxShadow shadow = default,
        Paint mask = default,
        Length width = default,
        Length height = default,
        Length minWidth = default,
        Length minHeight = default,
        Length maxWidth = default,
        Length maxHeight = default,
        string? id = null,
        NodeRef? nodeRef = null,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        Open(
            ui,
            new LayerProps
            {
                Hit = hit,
                OnDismiss = onDismiss,
                DismissOn = dismissOn,
                RestoreFocus = restoreFocus,
                AutoFocus = autoFocus,
                Tier = tier,
                DismissExempt = dismissExempt,
                Positioned = true,
                Anchor = anchor,
                Placement = placement,
                SideOffset = sideOffset,
                Offset = offset,
                MatchAnchorWidth = matchAnchorWidth,
                CollisionPadding = collisionPadding,
            },
            new LayoutStyle
            {
                Direction = direction,
                Wrap = wrap,
                Align = align,
                Justify = justify,
                AlignContent = alignContent,
                RowGap = rowGap ?? gap,
                ColumnGap = columnGap ?? gap,
                Padding = padding,
                Border = border.Width,
                Overflow = overflow,
                Width = width,
                Height = height,
                MinWidth = minWidth,
                MinHeight = minHeight,
                MaxWidth = maxWidth,
                MaxHeight = maxHeight,
            },
            new VisualStyle
            {
                Background = background,
                BorderPaint = border.Paint,
                Radius = radius,
                Shadow = shadow,
                Mask = mask,
            },
            false,
            id,
            nodeRef,
            key,
            file,
            line
        );

    static Scope Open(
        Ui ui,
        scoped in LayerProps props,
        scoped in LayoutStyle layout,
        scoped in VisualStyle visual,
        bool modal,
        string? id,
        NodeRef? nodeRef,
        UiKey key,
        string file,
        int line
    )
    {
        var b = ui.Builder;
        if (b.Discarding)
            return default;
        var depth = b.FrameDepth;
        var hooks = b.HookDepth;
        var node = b.Claim(NodeKind.Layer, null, key, file, line, out var mounted);
        var view = b.View;
        if (mounted)
            view.LayerOpened(node);
        var changed = ((LayerPayload)node.Payload!).Write(view, props);
        changed |= node.WriteLayout(layout);
        changed |= node.WriteVisual(visual);
        changed |= node.WriteInteraction(new InteractionProps { Modal = modal });
        changed |= node.WritePlacement(Builder.EffectivePlacement(node, new Placement { Id = id }));
        changed |= node.WriteRef(nodeRef);
        if (changed)
            view.Stats.NodeWrites++;
        b.Push(node);
        return new Scope(b, depth, hooks);
    }
}

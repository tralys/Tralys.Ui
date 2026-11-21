using System.Numerics;
using Tralys.Ui.Rendering;

namespace Tralys.Ui;

public sealed partial class UiView
{
    ValueList<Node> _layers;
    Vector2 _pointerAt;

    /// <summary>The open layers, in the order they opened.</summary>
    public ReadOnlySpan<Node> Layers => _layers.AsSpan();

    bool DismissableLayerOver(Vector2 at)
    {
        for (var i = 0; i < _layers.Count; i++)
        {
            var layer = _layers[i];
            var payload = PayloadOf(layer);
            if (
                (payload.Props.DismissOn & Dismiss.OutsidePress) != 0
                && Drawn(layer)
                && !ExitingLayer(layer)
                && !(payload.Bounded && !payload.Area.Contains(at))
            )
                return true;
        }
        return false;
    }

    static LayerPayload PayloadOf(Node layer) => layer.PayloadOf<LayerPayload>();

    internal void LayerOpened(Node node)
    {
        var payload = Pool<LayerPayload>.TryRent() ?? new LayerPayload();
        node.Payload = payload;
        payload.Scope.Init(this, node, payload);
        payload.PointerAt = _pointerAt;
        if (_focused is { } focused)
        {
            payload.Scope.Opened(focused);
            payload.FocusWithinOwner =
                node._parent is { } owner && owner.IsAncestorOrSelfOf(focused);
        }
        _layers.Add(node);
        _compositeDirty = true;
    }

    void LayerClosed(Node node)
    {
        var index = _layers.IndexOf(node);
        if (index < 0)
            return;
        _layers.RemoveAt(index);
        var scope = PayloadOf(node).Scope;
        if (scope.Active)
        {
            scope.Active = false;
            _activeModals.RemoveAt(_activeModals.IndexOf(scope));
        }
        _compositeDirty = true;
    }

    Rect LayerArea() => new Rect(_size).Deflate(_safeInsets);

    static Node? BoundaryOf(Node node)
    {
        for (var n = node._parent; n is not null; n = n._parent)
        {
            if (n.Interaction?.Props.ContainsLayers == true)
                return n;
        }
        return null;
    }

    bool Reaches(Node node) => BoundaryOf(node) is not { } boundary || FocusInside(boundary);

    Rect AreaOf(Node layer, LayerPayload payload, Rect view)
    {
        var boundary = BoundaryOf(layer);
        payload.Bounded = boundary is not null;
        payload.Boundary = boundary;
        if (boundary is null)
            return view;
        if (
            boundary.View != this
            || !Drawn(boundary)
            || !ViewRects(boundary, new Rect(FrameSize(boundary)), out _, out var visible)
        )
            return default;
        return visible.Intersect(view);
    }

    void Contain(LayerPayload payload, Rect area)
    {
        if (payload.Area == area)
            return;
        payload.Area = area;
        _compositeDirty = true;
    }

    void LayoutLayers()
    {
        if (_layers.Count == 0)
            return;
        SettleMeasures();
        _measuring++;
        try
        {
            var view = LayerArea();
            for (var i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (!Drawn(layer))
                    continue;
                var payload = PayloadOf(layer);
                var area = AreaOf(layer, payload, view);
                Contain(payload, area);
                if (payload.Props.Positioned)
                    PlaceLayer(layer, payload, area, beforePaint: false);
                else
                    Fill(layer, area);
            }
        }
        finally
        {
            _measuring--;
        }
    }

    void Fill(Node layer, Rect area)
    {
        layer.PctBasis = new Vector2(area.Width, area.Height);
        Arrange(layer, area, Matrix3x2.Identity, Matrix3x2.Identity);
    }

    // Between layout and paint only a scroll offset or a transform moves an anchor or a boundary.
    void RepositionLayers()
    {
        if (_layers.Count == 0)
            return;
        SettleMeasures();
        _measuring++;
        try
        {
            var view = LayerArea();
            for (var i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (!Drawn(layer))
                    continue;
                var payload = PayloadOf(layer);
                if (payload.Props.Positioned)
                {
                    if (payload.Placed)
                    {
                        var area = AreaOf(layer, payload, view);
                        Contain(payload, area);
                        PlaceLayer(layer, payload, area, beforePaint: true);
                    }
                }
                else if (payload.Bounded)
                {
                    var area = AreaOf(layer, payload, view);
                    if (area == payload.Area)
                        continue;
                    Contain(payload, area);
                    Fill(layer, area);
                }
            }
        }
        finally
        {
            _measuring--;
        }
    }

    void PlaceLayer(Node layer, LayerPayload payload, Rect area, bool beforePaint)
    {
        var valid = AnchorRect(layer, payload.Props.Anchor, payload.PointerAt, out var anchor);
        if (valid)
        {
            payload.AnchorRect = anchor;
            payload.HasAnchor = true;
        }
        if (valid != payload.AnchorValid || !payload.Placed)
        {
            payload.AnchorValid = valid;
            if (
                !valid
                && (payload.Props.DismissOn & Dismiss.AnchorHidden) != 0
                && !ExitingLayer(layer)
            )
            {
                if (beforePaint)
                    PostAnchorLost(payload);
                else
                    DismissLayer(payload);
            }
        }
        if (
            beforePaint
            && (!valid || anchor == payload.PlacedAgainst)
            && area == payload.PlacedWithin
        )
            return;

        var target = payload.HasAnchor ? payload.AnchorRect : new Rect(area.X, area.Y, 0f, 0f);
        var rect = Place(layer, payload, target, area.Deflate(payload.Props.CollisionPadding));
        payload.PlacedAgainst = target;
        payload.PlacedWithin = area;
        payload.Placed = true;
        Arrange(layer, rect, Matrix3x2.Identity, Matrix3x2.Identity);
    }

    Rect Place(Node layer, LayerPayload payload, Rect anchor, Rect bounds)
    {
        ref readonly var props = ref payload.Props;
        var mode = props.Placement ?? PlacementMode.BottomStart;
        var (edge, gravity, axis) = PopupPositioner.Rules(mode);
        var target =
            mode is PlacementMode.Pointer or PlacementMode.Absolute ? anchor
            : axis == PopupEdge.Horizontal ? anchor.Inflate(props.SideOffset, 0f)
            : anchor.Inflate(0f, props.SideOffset);

        layer.PctBasis = new Vector2(bounds.Width, bounds.Height);
        var width = props.MatchAnchorWidth
            ? Avail.Exact(AnchorWidth(layer, anchor.Width))
            : Avail.AtMost(bounds.Width);
        var height = Avail.AtMost(bounds.Height);
        var size = Measure(layer, width, height);
        if (axis != PopupEdge.None)
        {
            var vertical = axis == PopupEdge.Vertical;
            var length = vertical ? size.Height : size.Width;
            var room = Room(target, edge, gravity, vertical, bounds);
            if (length > room + Epsilon)
            {
                var otherEdge = Flip(edge, axis);
                var otherGravity = Flip(gravity, axis);
                var other = Room(target, otherEdge, otherGravity, vertical, bounds);
                if (length <= other + Epsilon || other > room)
                {
                    edge = otherEdge;
                    gravity = otherGravity;
                }
                var fit = MathF.Max(room, other);
                if (length > fit + Epsilon && fit > 0f)
                    size = vertical
                        ? Measure(layer, width, Avail.AtMost(fit))
                        : Measure(layer, Avail.AtMost(fit), height);
            }
        }
        return PopupPositioner.Place(size, target, edge, gravity, axis, props.Offset, bounds);
    }

    static float AnchorWidth(Node layer, float width)
    {
        var (min, max) = Limits(layer, horizontal: true);
        return Clamp(width, Definite(min, 0f), max);
    }

    static PopupEdge Flip(PopupEdge edge, PopupEdge axis) =>
        (edge & axis) != 0 ? edge ^ axis : edge;

    static float Room(Rect target, PopupEdge edge, PopupEdge gravity, bool vertical, Rect bounds)
    {
        if (vertical)
        {
            var y =
                (edge & PopupEdge.Top) != 0 ? target.Y
                : (edge & PopupEdge.Bottom) != 0 ? target.Bottom
                : target.Y + target.Height / 2f;
            return (gravity & PopupEdge.Top) != 0 ? y - bounds.Y : bounds.Bottom - y;
        }
        var x =
            (edge & PopupEdge.Left) != 0 ? target.X
            : (edge & PopupEdge.Right) != 0 ? target.Right
            : target.X + target.Width / 2f;
        return (gravity & PopupEdge.Left) != 0 ? x - bounds.X : bounds.Right - x;
    }

    bool AnchorRect(Node layer, in Anchor anchor, Vector2 pointerAt, out Rect rect)
    {
        Node? node;
        switch (anchor.Kind)
        {
            case AnchorKind.View:
                rect = anchor.Area;
                return true;
            case AnchorKind.Pointer:
                rect = new Rect(pointerAt.X, pointerAt.Y, 0f, 0f);
                return true;
            case AnchorKind.Owner:
                node = layer.LayoutParent;
                break;
            default:
                node = anchor.Node?.Node;
                break;
        }

        rect = default;
        if (node is null || node.View != this || !Drawn(node))
            return false;
        var local = anchor.Kind == AnchorKind.Within ? anchor.Area : new Rect(FrameSize(node));
        if (!ViewRects(node, local, out var full, out _))
            return false;
        rect = full;
        return true;
    }

    static bool ViewRects(Node node, Rect local, out Rect full, out Rect visible)
    {
        full = local;
        visible = local;
        for (var n = node; ; )
        {
            var toParent = LocalTransform(n);
            full = full.TransformedBy(toParent);
            visible = visible.TransformedBy(toParent);
            if (n.Kind is NodeKind.Layer or NodeKind.Root || n.LayoutParent is not { } parent)
                break;
            if (parent.ScrollApplied != default)
            {
                var scrolled = Matrix3x2.CreateTranslation(-parent.ScrollApplied);
                full = full.TransformedBy(scrolled);
                visible = visible.TransformedBy(scrolled);
            }
            if (
                parent.Layout.Overflow != Overflow.Visible
                && !Clip(ref visible, PaddingBox(parent))
            )
                return false;
            n = parent;
        }
        return true;
    }

    static Rect PaddingBox(Node node)
    {
        var size = FrameSize(node);
        var border = node.Layout.Border;
        return Rect.FromEdges(
            border.Left,
            border.Top,
            size.Width - border.Right,
            size.Height - border.Bottom
        );
    }

    // A point or a line anchor stays visible on the clip's edge; an area needs some of itself inside.
    static bool Clip(ref Rect visible, Rect clip)
    {
        var left = MathF.Max(visible.X, clip.X);
        var top = MathF.Max(visible.Y, clip.Y);
        var right = MathF.Min(visible.Right, clip.Right);
        var bottom = MathF.Min(visible.Bottom, clip.Bottom);
        var outside = visible.IsEmpty
            ? right < left || bottom < top
            : right <= left || bottom <= top;
        if (outside)
            return false;
        visible = Rect.FromEdges(left, top, right, bottom);
        return true;
    }

    void PostAnchorLost(LayerPayload payload)
    {
        if (payload.AnchorLostPosted)
            return;
        payload.AnchorLostPosted = true;
        Context.Dispatcher.Post(payload, static p => p.Scope.View?.AnchorLost(p));
    }

    void AnchorLost(LayerPayload payload)
    {
        if (!payload.AnchorLostPosted)
            return;
        payload.AnchorLostPosted = false;
        if (
            payload.Scope.IsLive
            && !payload.AnchorValid
            && Drawn(payload.Scope.Node)
            && !ExitingLayer(payload.Scope.Node)
        )
            DismissLayer(payload);
    }

    // A layer a boundary holds draws with that box, in view space, so what is stacked above the box covers it too.
    void DrawLayers(DrawList list, Node? boundary)
    {
        for (var tier = 0; tier <= (int)LayerTier.Tooltip; tier++)
        {
            for (var i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                var payload = PayloadOf(layer);
                if (
                    (int)payload.Props.Tier != tier
                    || payload.Boundary != boundary
                    || !Drawn(layer)
                )
                    continue;
                if (payload.Bounded)
                    list.PushClip(payload.Area, default);
                DrawNode(layer, list);
                if (payload.Bounded)
                    list.Pop();
            }
        }
    }

    bool HoldsLayers(Node node)
    {
        if (node.Interaction?.Props.ContainsLayers != true)
            return false;
        for (var i = 0; i < _layers.Count; i++)
        {
            if (PayloadOf(_layers[i]).Boundary == node)
                return true;
        }
        return false;
    }

    void DrawHeldLayers(Node boundary, DrawList list)
    {
        if (!Matrix3x2.Invert(ToView(boundary), out var fromView))
            return;
        list.PushTransform(fromView);
        DrawLayers(list, boundary);
        list.Pop();
    }

    Node? HitLayers(Vector2 point, bool pick = false, Node? boundary = null)
    {
        for (var tier = (int)LayerTier.Tooltip; tier >= 0; tier--)
        {
            for (var i = _layers.Count - 1; i >= 0; i--)
            {
                var layer = _layers[i];
                var payload = PayloadOf(layer);
                ref readonly var props = ref payload.Props;
                if (
                    (int)props.Tier != tier
                    || payload.Boundary != boundary
                    || (props.Hit == LayerHit.PassThrough && !pick)
                    || !Drawn(layer)
                    || ExitingLayer(layer)
                    || (payload.Bounded && !payload.Area.Contains(point))
                )
                    continue;
                if (Hit(layer, point, pick) is { } hit)
                    return hit;
            }
        }
        return null;
    }

    bool DismissOnPress(PointerState state, MouseButton button, Node? hit)
    {
        var bit = 1 << (int)button;
        state.DismissedButtons &= ~bit;
        var dismissed = false;
        for (var i = _layers.Count - 1; i >= 0; i--)
        {
            var layer = _layers[i];
            var payload = PayloadOf(layer);
            if (!PressDismisses(layer, payload, state.Position, hit))
                continue;
            DismissLayer(payload);
            dismissed = true;
        }
        if (dismissed)
            state.DismissedButtons |= bit;
        return dismissed;
    }

    bool PressDismissesAny(Vector2 at, Node? hit)
    {
        for (var i = _layers.Count - 1; i >= 0; i--)
        {
            if (PressDismisses(_layers[i], PayloadOf(_layers[i]), at, hit))
                return true;
        }
        return false;
    }

    bool PressDismisses(Node layer, LayerPayload payload, Vector2 at, Node? hit) =>
        (payload.Props.DismissOn & Dismiss.OutsidePress) != 0
        && Drawn(layer)
        && !ExitingLayer(layer)
        && !(payload.Bounded && !payload.Area.Contains(at))
        && !Inside(layer, payload, hit);

    static bool ReleaseDismissed(PointerState state, MouseButton button)
    {
        var bit = 1 << (int)button;
        if ((state.DismissedButtons & bit) == 0)
            return false;
        state.DismissedButtons &= ~bit;
        return true;
    }

    static bool Inside(Node layer, LayerPayload payload, Node? hit)
    {
        if (hit is null)
            return false;
        if (payload.Props.DismissExempt?.Node is { } exempt && exempt.IsAncestorOrSelfOf(hit))
            return true;
        var content = payload.Props.Content?.Node ?? layer;
        var nested = false;
        for (var n = hit; n is not null; n = n._parent)
        {
            if (n == content)
                return true;
            if (n == layer)
                return nested;
            if (n.Kind == NodeKind.Layer)
                nested = true;
        }
        return false;
    }

    internal bool DismissTopmostLayer()
    {
        for (var tier = (int)LayerTier.Tooltip; tier >= 0; tier--)
        {
            for (var i = _layers.Count - 1; i >= 0; i--)
            {
                var layer = _layers[i];
                var payload = PayloadOf(layer);
                if (
                    (int)payload.Props.Tier != tier
                    || (payload.Props.DismissOn & Dismiss.Escape) == 0
                    || !Drawn(layer)
                    || ExitingLayer(layer)
                    || !Reaches(layer)
                )
                    continue;
                DismissLayer(payload);
                return true;
            }
        }
        return false;
    }

    Node? KeyParent(Node node, Key key) =>
        key == Key.Escape
        && node.Kind == NodeKind.Layer
        && (PayloadOf(node).Props.DismissOn & Dismiss.Escape) != 0
        && Drawn(node)
            ? null
            : node._parent;

    void DismissLayer(LayerPayload payload)
    {
        var onDismiss = payload.Props.OnDismiss;
        if (onDismiss.IsEmpty)
            return;
        try
        {
            onDismiss.Invoke();
        }
        catch (Exception fault) when (Context.OnFault is not null)
        {
            Context.OnFault(fault);
        }
    }
}

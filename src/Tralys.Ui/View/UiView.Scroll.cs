using System.Numerics;

namespace Tralys.Ui;

public sealed partial class UiView
{
    const float NotchLength = 48f;
    const float GlideSeconds = 0.15f;
    const float CoastSlowing = 1000f;
    const float EdgeZone = 32f;
    const float EdgeSpeed = 600f;

    ValueList<ScrollHandle> _scrollMotions;
    ValueList<ScrollHandle> _scrollNotices;
    ValueList<Reveal> _reveals;
    ValueList<Node> _followers;

    TimeSpan _scrollAdvanced;

    internal bool ScrollSettled =>
        _scrollMotions.Count == 0 && _reveals.Count == 0 && EdgeScroller(out _) is null;

    internal void InvalidateArrange(Node node)
    {
        for (var n = node; n is not null; n = n._parent)
        {
            if (n.IsTransparent)
                continue;
            if ((n.Dirty & Dirty.Layout) != 0 && n != node)
                return;
            n.Dirty |= Dirty.Layout;
        }
    }

    internal static Matrix3x2 ContentToView(Node node) =>
        node.ScrollApplied == default
            ? node.FrameToView
            : Matrix3x2.CreateTranslation(-node.ScrollApplied) * node.FrameToView;

    internal static Matrix3x2 ContentSnappedToView(Node node) =>
        node.ScrollApplied == default
            ? node.SnappedToView
            : Matrix3x2.CreateTranslation(-node.ScrollApplied) * node.SnappedToView;

    internal void BindScroller(Node node, ScrollHandle? handle)
    {
        if (node.Scroller is not null)
            ReleaseScroller(node);
        if (handle is null)
        {
            handle = Pool<ScrollHandle>.TryRent() ?? new ScrollHandle();
            handle.Implicit = true;
        }
        else if (handle.Node is { } other && other != node)
            ReleaseScroller(other);
        handle.Node = node;
        node.Scroller = handle;
        InvalidateArrange(node);
    }

    internal void DropScroller(Node node)
    {
        ReleaseScroller(node);
        if (node.ScrollApplied != default)
        {
            node.ScrollApplied = default;
            InvalidateArrange(node);
        }
    }

    void ReleaseScroller(Node node)
    {
        if (node.Scroller is not { } handle)
            return;
        node.Scroller = null;
        handle.Node = null;
        handle.StopMotion();
        if (handle.Implicit)
        {
            handle.Reset();
            Pool<ScrollHandle>.Return(handle);
        }
    }

    internal void UnbindScroller(ScrollHandle handle)
    {
        if (handle.Node is not { } node || node.Scroller != handle)
            return;
        node.Scroller = null;
        handle.Node = null;
        if (node.ScrollApplied != default)
        {
            node.ScrollApplied = default;
            InvalidateArrange(node);
        }
    }

    void DetachScroll(Node node)
    {
        if (node.Scroller is not null)
            ReleaseScroller(node);
        if (node.ScrollProps.FollowScroll is not null)
        {
            var at = _followers.IndexOf(node);
            if (at >= 0)
                _followers.RemoveAtSwap(at);
        }
    }

    internal void Followed(Node node, bool following)
    {
        var at = _followers.IndexOf(node);
        if (following && at < 0)
            _followers.Add(node);
        else if (!following && at >= 0)
            _followers.RemoveAtSwap(at);
    }

    static Size ViewportOf(Node node)
    {
        var frame = node.QuarterTurned
            ? new Size(node.Frame.Height, node.Frame.Width)
            : node.Frame.Size;
        var border = node.Layout.Border;
        return new Size(
            MathF.Max(0f, frame.Width - border.Left - border.Right),
            MathF.Max(0f, frame.Height - border.Top - border.Bottom)
        );
    }

    static Vector2 MaxOffset(Node node, Size viewport, Size extent)
    {
        var max = new Vector2(
            node.ScrollsX ? MathF.Max(0f, extent.Width - viewport.Width) : 0f,
            node.ScrollsY ? MathF.Max(0f, extent.Height - viewport.Height) : 0f
        );
        if (
            node.ScrollsX
            && node.ScrollProps.FollowScroll is { Node: { ScrollsX: true } } lead
            && lead != node.Scroller
        )
            max.X = MathF.Max(0f, lead.ExtentSize.Width - lead.ViewportSize.Width);
        return max;
    }

    static Vector2 MaxOffset(Node node, ScrollHandle handle) =>
        MaxOffset(node, handle.ViewportSize, handle.ExtentSize);

    static Vector2 Clamp(Vector2 offset, Vector2 max) =>
        new(
            float.IsFinite(offset.X) ? Math.Clamp(offset.X, 0f, max.X) : 0f,
            float.IsFinite(offset.Y) ? Math.Clamp(offset.Y, 0f, max.Y) : 0f
        );

    Vector2 SnapOffset(Vector2 offset) => new(SnapEdge(offset.X), SnapEdge(offset.Y));

    void ScrollerArranged(Node node)
    {
        var handle = node.Scroller!;
        var border = node.Layout.Border;
        var content = node.ContentExtent;
        var extent = new Size(
            MathF.Max(0f, content.Right - border.Left),
            MathF.Max(0f, content.Bottom - border.Top)
        );
        var viewport = ViewportOf(node);
        handle.ViewportSize = viewport;
        handle.ExtentSize = extent;
        var max = MaxOffset(node, viewport, extent);
        ResolveRequests(node, handle, max);
        if (handle.ToEnd)
            handle.Current = EndOf(node, handle.Current, max);
        SettleOffset(node, handle, max);
    }

    static Vector2 EndOf(Node node, Vector2 offset, Vector2 max) =>
        node.ScrollsY ? new Vector2(offset.X, max.Y) : new Vector2(max.X, offset.Y);

    internal void ResolveRequests(Node node, ScrollHandle handle, Vector2 max)
    {
        if (!handle.HasRequest)
            return;
        var gliding = handle.Motion == ScrollMotion.Glide;
        var from = handle.RequestAnimate && gliding ? handle.GlideTo : handle.Current;
        var target = Clamp(
            new Vector2(
                (handle.RequestX ?? from.X) + handle.RequestBy.X,
                (handle.RequestY ?? from.Y) + handle.RequestBy.Y
            ),
            max
        );
        var animate = handle.RequestAnimate && Context.SmoothScrolling;
        var acrossX = handle.RequestX is not null || handle.RequestBy.X != 0f;
        handle.HasRequest = false;
        handle.RequestX = handle.RequestY = null;
        handle.RequestBy = default;
        handle.RequestAnimate = false;
        if (animate)
            GlideTo(handle, target);
        else
        {
            handle.StopMotion();
            handle.Current = target;
        }
        if (
            acrossX
            && node.ScrollProps.FollowScroll is { Node: { } leadNode } lead
            && lead != handle
        )
        {
            lead.StopMotion();
            lead.Current = new Vector2(target.X, lead.Current.Y);
            InvalidateArrange(leadNode);
        }
    }

    internal void SettleOffset(Node node, ScrollHandle handle, Vector2 max)
    {
        if (node.ScrollProps.FollowScroll is { } lead && lead != handle)
            handle.Current = new Vector2(lead.Current.X, handle.Current.Y);
        handle.Current = Clamp(handle.Current, max);
        if (handle.Motion == ScrollMotion.Glide)
            handle.GlideTo = Clamp(handle.GlideTo, max);
        var applied = SnapOffset(handle.Current);
        if (applied != node.ScrollApplied)
        {
            var movedX = applied.X != node.ScrollApplied.X;
            node.ScrollApplied = applied;
            if (node.Kind != NodeKind.VirtualList)
                ShiftChildren(node);
            _compositeDirty = true;
            if (movedX)
                LeadMoved(handle);
        }
        QueueScrollNotice(handle);
    }

    void LeadMoved(ScrollHandle lead)
    {
        for (var i = 0; i < _followers.Count; i++)
        {
            var follower = _followers[i];
            if (follower.ScrollProps.FollowScroll == lead && follower.Scroller != lead)
                InvalidateArrange(follower);
        }
    }

    void ShiftChildren(Node node)
    {
        var start = _layoutChildren.Count;
        CollectChildren(node, ref _layoutChildren);
        var toView = ContentToView(node);
        var snapped = ContentSnappedToView(node);
        for (var i = start; i < _layoutChildren.Count; i++)
            Shift(_layoutChildren[i], toView, snapped);
        _layoutChildren.Truncate(start);
    }

    void Shift(Node node, in Matrix3x2 parentToView, in Matrix3x2 parentSnapped)
    {
        if (!node.Arranged || node.ArrangedParent == parentToView)
            return;
        var frame = node.Frame;
        var bounds = node._bounds;
        var view = Snap(frame.TransformedBy(parentToView));
        node._viewBounds = view;
        node.ArrangedParent = parentToView;
        node.FrameToView =
            Turn(node, frame.Size) * Matrix3x2.CreateTranslation(frame.X, frame.Y) * parentToView;
        node.SnappedToView =
            Turn(node, bounds.Size)
            * Matrix3x2.CreateTranslation(bounds.X, bounds.Y)
            * parentSnapped;
        node.Ref?.LaidOut(view);
        var start = _layoutChildren.Count;
        CollectChildren(node, ref _layoutChildren);
        var toView = ContentToView(node);
        var snapped = ContentSnappedToView(node);
        for (var i = start; i < _layoutChildren.Count; i++)
            Shift(_layoutChildren[i], toView, snapped);
        _layoutChildren.Truncate(start);
    }

    void QueueScrollNotice(ScrollHandle handle)
    {
        if (
            handle.NoticeQueued
            || (
                handle.Current == handle.ReportedOffset
                && handle.ExtentSize == handle.ReportedExtent
                && handle.ViewportSize == handle.ReportedViewport
            )
        )
            return;
        handle.NoticeQueued = true;
        _scrollNotices.Add(handle);
    }

    void DeliverScrollNotices()
    {
        for (var i = 0; i < _scrollNotices.Count; i++)
        {
            var handle = _scrollNotices[i];
            handle.NoticeQueued = false;
            var change = new ScrollChange(
                handle.Current,
                handle.Current - handle.ReportedOffset,
                handle.ExtentSize,
                new Size(
                    handle.ExtentSize.Width - handle.ReportedExtent.Width,
                    handle.ExtentSize.Height - handle.ReportedExtent.Height
                ),
                handle.ViewportSize,
                new Size(
                    handle.ViewportSize.Width - handle.ReportedViewport.Width,
                    handle.ViewportSize.Height - handle.ReportedViewport.Height
                )
            );
            handle.ReportedOffset = handle.Current;
            handle.ReportedExtent = handle.ExtentSize;
            handle.ReportedViewport = handle.ViewportSize;
            handle.Notify();
            if (handle.Node is { IsMounted: true } node)
                Invoke(node.ScrollProps.OnScrollChange, change);
        }
        _scrollNotices.Clear();
    }

    internal void GlideFor(ScrollHandle handle, Vector2 target) => GlideTo(handle, target);

    void GlideTo(ScrollHandle handle, Vector2 target)
    {
        if (target == handle.Current)
        {
            handle.StopMotion();
            return;
        }
        handle.Motion = ScrollMotion.Glide;
        handle.GlideFrom = handle.Current;
        handle.GlideTo = target;
        handle.GlideStart = Context.Now;
        AddMotion(handle);
    }

    internal void Coast(ScrollHandle handle, Vector2 velocity)
    {
        handle.Motion = ScrollMotion.Coast;
        handle.CoastVelocity = velocity;
        handle.CoastTime = Context.Now;
        AddMotion(handle);
    }

    void AddMotion(ScrollHandle handle)
    {
        if (handle.Moving)
            return;
        handle.Moving = true;
        _scrollMotions.Add(handle);
    }

    internal void AdvanceScroll(TimeSpan now)
    {
        var seconds = (float)(now - _scrollAdvanced).TotalSeconds;
        _scrollAdvanced = now;
        if (seconds > 0f && EdgeScroller(out var velocity) is { Scroller: { } edged } edging)
        {
            edged.StopMotion();
            edged.ToEnd = false;
            edged.Current = Clamp(edged.Current + velocity * seconds, MaxOffset(edging, edged));
            Scrolled(edging, edged);
        }
        for (var i = _scrollMotions.Count - 1; i >= 0; i--)
        {
            var handle = _scrollMotions[i];
            if (handle.Motion == ScrollMotion.None || handle.Node is not { } node)
            {
                handle.Moving = false;
                handle.StopMotion();
                _scrollMotions.RemoveAtSwap(i);
                continue;
            }
            if (handle.Motion == ScrollMotion.Glide)
                StepGlide(handle, now);
            else
                StepCoast(node, handle, now);
            InvalidateArrange(node);
        }
    }

    Node? EdgeScroller(out Vector2 velocity)
    {
        velocity = default;
        if (!DragDropActive)
            return null;
        var at = DragDropPosition;
        for (var node = HitAt(at); node is not null; node = node._parent)
        {
            if (node.Scroller is { } handle && (node.ScrollsX || node.ScrollsY))
            {
                var max = MaxOffset(node, handle);
                var view = node.ViewBounds;
                var border = node.Layout.Border;
                velocity = new Vector2(
                    node.ScrollsX
                        ? EdgeSpeedAlong(
                            at.X,
                            view.X + border.Left,
                            view.Right - border.Right,
                            handle.Current.X,
                            max.X
                        )
                        : 0f,
                    node.ScrollsY
                        ? EdgeSpeedAlong(
                            at.Y,
                            view.Y + border.Top,
                            view.Bottom - border.Bottom,
                            handle.Current.Y,
                            max.Y
                        )
                        : 0f
                );
                if (velocity != default)
                    return node;
            }
            if (node.Kind == NodeKind.Layer)
                break;
        }
        return null;
    }

    static float EdgeSpeedAlong(float at, float start, float end, float offset, float max)
    {
        var zone = MathF.Min(EdgeZone, (end - start) / 4f);
        if (!(zone > 0f))
            return 0f;
        if (at < start + zone && offset > 0f)
            return -EdgeSpeed * Math.Clamp(1f - (at - start) / zone, 0f, 1f);
        if (at > end - zone && offset < max)
            return EdgeSpeed * Math.Clamp(1f - (end - at) / zone, 0f, 1f);
        return 0f;
    }

    static void StepGlide(ScrollHandle handle, TimeSpan now)
    {
        var t = (float)(now - handle.GlideStart).TotalSeconds / GlideSeconds;
        if (!(t < 1f))
        {
            handle.Current = handle.GlideTo;
            handle.StopMotion();
            return;
        }
        var left = 1f - MathF.Max(0f, t);
        var eased = 1f - left * left * left;
        handle.Current = handle.GlideFrom + (handle.GlideTo - handle.GlideFrom) * eased;
    }

    static void StepCoast(Node node, ScrollHandle handle, TimeSpan now)
    {
        var seconds = (float)(now - handle.CoastTime).TotalSeconds;
        handle.CoastTime = now;
        if (!(seconds > 0f))
            return;
        var max = MaxOffset(node, handle);
        var velocity = handle.CoastVelocity;
        var moved = handle.Current + velocity * seconds;
        var clamped = Clamp(moved, max);
        if (clamped.X != moved.X)
            velocity.X = 0f;
        if (clamped.Y != moved.Y)
            velocity.Y = 0f;
        handle.Current = clamped;
        var speed = velocity.Length();
        var slowed = speed - CoastSlowing * seconds;
        if (!(slowed > 0f))
        {
            handle.StopMotion();
            return;
        }
        handle.CoastVelocity = velocity * (slowed / speed);
    }

    /// <summary>A wheel turn at <paramref name="at"/>, in view pixels: <paramref name="notches"/> positive Y away from
    /// the user, which scrolls toward the top, and positive X to the right.</summary>
    /// <returns>Whether the UI took it.</returns>
    public bool Wheel(Vector2 at, Vector2 notches, ModifierKeys m)
    {
        BeforeInput();
        UseDevice(InputDevice.KeyboardMouse);
        var position = at / _scale;
        var target = Live(FindState(Pointer.Mouse)?.Captor) ?? HitAt(position);
        if (target is null)
            return DismissableLayerOver(position);
        if ((target._states & InputStates.Disabled) != 0)
            return DismissableLayerOver(position) || !IsViewRoot(target);

        var e = new WheelEvent(notches, m, position);
        for (var node = target; node is not null && !e.Stopped; node = node._parent)
        {
            if (node.Interaction?.Props.OnWheel is { IsEmpty: false } handler)
            {
                e.Position = LocalPosition(node, position);
                try
                {
                    handler.Invoke(ref e);
                }
                catch (Exception fault) when (Context.OnFault is not null)
                {
                    Context.OnFault(fault);
                }
            }
            if (node.Kind == NodeKind.Layer)
                break;
        }

        var taken =
            e.Stopped || e.Prevented || DismissableLayerOver(position) || !IsViewRoot(target);
        if (!e.Prevented && WheelScroll(target, notches))
            taken = true;
        return taken;
    }

    internal bool WheelScroll(Node target, Vector2 notches)
    {
        var delta = new Vector2(notches.X * NotchLength, -notches.Y * NotchLength);
        var x = MathF.Abs(delta.X);
        var y = MathF.Abs(delta.Y);
        return x > y && WheelScroll(target, delta, leadX: true, leadY: false)
            || y > x && WheelScroll(target, delta, leadX: false, leadY: true)
            || WheelScroll(target, delta, leadX: true, leadY: true);
    }

    bool WheelScroll(Node target, Vector2 delta, bool leadX, bool leadY)
    {
        for (var node = target; node is not null; node = node._parent)
        {
            if (node.Scroller is { } handle && (node.ScrollsX || node.ScrollsY))
            {
                var max = MaxOffset(node, handle);
                var from = handle.Motion == ScrollMotion.Glide ? handle.GlideTo : handle.Current;
                var alongX = CanMove(node.ScrollsX, delta.X, from.X, max.X);
                var alongY = CanMove(node.ScrollsY, delta.Y, from.Y, max.Y);
                if (leadX && alongX || leadY && alongY)
                {
                    var to = Clamp(
                        from + new Vector2(alongX ? delta.X : 0f, alongY ? delta.Y : 0f),
                        max
                    );
                    handle.ToEnd = false;
                    if (Context.SmoothScrolling)
                        GlideTo(handle, to);
                    else
                    {
                        handle.StopMotion();
                        handle.Current = to;
                    }
                    Scrolled(node, handle);
                    return true;
                }
            }
            if (node.Kind == NodeKind.Layer)
                break;
        }
        return false;
    }

    static bool CanMove(bool scrolls, float delta, float from, float max) =>
        scrolls && delta != 0f && (delta > 0f ? from < max : from > 0f);

    void Scrolled(Node node, ScrollHandle handle)
    {
        if (node.ScrollProps.FollowScroll is { Node: { } leadNode } lead && lead != handle)
        {
            var target = handle.Motion == ScrollMotion.Glide ? handle.GlideTo.X : handle.Current.X;
            lead.StopMotion();
            lead.Current = new Vector2(
                Math.Clamp(target, 0f, MaxOffset(leadNode, lead).X),
                lead.Current.Y
            );
            InvalidateArrange(leadNode);
        }
        InvalidateArrange(node);
    }

    internal void RequestReveal(Node node, ScrollAlign align, bool animate) =>
        _reveals.Add(new Reveal(node, node.Generation, align, animate));

    void SettleScroll()
    {
        for (var i = 0; i < _reveals.Count; i++)
        {
            var reveal = _reveals[i];
            if (reveal.Node.Generation == reveal.Generation && reveal.Node.IsMounted)
                RevealNow(reveal.Node, reveal.Align, reveal.Animate);
        }
        _reveals.Clear();
        for (var round = 0; round < 4 && LayoutPending(); round++)
        {
            Layout();
            LayoutLayers();
        }
    }

    bool LayoutPending()
    {
        if ((_root.Dirty & Dirty.Layout) != 0)
            return true;
        for (var i = 0; i < _layers.Count; i++)
        {
            if ((_layers[i].Dirty & Dirty.Layout) != 0)
                return true;
        }
        return false;
    }

    void RevealNow(Node target, ScrollAlign align, bool animate)
    {
        for (var container = target._parent; container is not null; container = container._parent)
        {
            if (container.Kind == NodeKind.Layer)
                break;
            if (
                container.Scroller is not { } handle
                || !(container.ScrollsX || container.ScrollsY)
                || !container.Arranged
            )
                continue;
            var rect = DrawnIn(target, container);
            var border = container.Layout.Border;
            var viewport = handle.ViewportSize;
            var delta = new Vector2(
                container.ScrollsX
                    ? Reach(rect.X, rect.Right, border.Left, border.Left + viewport.Width, align)
                    : 0f,
                container.ScrollsY
                    ? Reach(rect.Y, rect.Bottom, border.Top, border.Top + viewport.Height, align)
                    : 0f
            );
            if (delta == default)
                continue;
            var to = Clamp(SnapOffset(handle.Current) + delta, MaxOffset(container, handle));
            handle.ToEnd = false;
            if (animate && Context.SmoothScrolling)
                GlideTo(handle, to);
            else
            {
                handle.StopMotion();
                handle.Current = to;
            }
            Scrolled(container, handle);
        }
    }

    internal static float Reach(
        float start,
        float end,
        float viewStart,
        float viewEnd,
        ScrollAlign align
    ) =>
        align switch
        {
            ScrollAlign.Start => start - viewStart,
            ScrollAlign.End => end - viewEnd,
            ScrollAlign.Center => (start + end - viewStart - viewEnd) / 2f,
            _ => start < viewStart || end - start > viewEnd - viewStart ? start - viewStart
            : end > viewEnd ? end - viewEnd
            : 0f,
        };

    Rect DrawnIn(Node target, Node container)
    {
        var matrix = Matrix3x2.Identity;
        for (var node = target; node is not null && node != container; node = node._parent)
        {
            if (node.IsTransparent)
                continue;
            matrix *= LocalTransform(node);
            if (node.LayoutParent is { Scroller: { } parentScroll })
                matrix *= Matrix3x2.CreateTranslation(-SnapOffset(parentScroll.Current));
        }
        return new Rect(FrameSize(target)).TransformedBy(matrix);
    }

    readonly record struct Reveal(Node Node, int Generation, ScrollAlign Align, bool Animate);
}

using System.Numerics;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

/// <summary>Where a target lands in the viewport when scrolled to. <see cref="Nearest"/> scrolls as little as
/// possible: not at all when the target is wholly shown.</summary>
public enum ScrollAlign : byte
{
    Nearest,
    Start,
    Center,
    End,
}

/// <summary>Which ways a touch pans a scroll container.</summary>
public enum Panning : byte
{
    /// <summary>Claims a move by its dominant axis, then pans both ways.</summary>
    Both,

    /// <summary>Never pans by touch.</summary>
    None,

    /// <summary>Claims a mostly horizontal move and pans only horizontally.</summary>
    HorizontalOnly,

    /// <summary>Claims a mostly vertical move and pans only vertically.</summary>
    VerticalOnly,

    /// <summary>Claims a mostly horizontal move, then pans both ways.</summary>
    HorizontalFirst,

    /// <summary>Claims a mostly vertical move, then pans both ways.</summary>
    VerticalFirst,
}

/// <summary>What one layout changed in a scroll container: the new offset, extent and viewport, each with the change
/// from the last report.</summary>
public readonly record struct ScrollChange(
    Vector2 Offset,
    Vector2 OffsetDelta,
    Size Extent,
    Size ExtentDelta,
    Size Viewport,
    Size ViewportDelta
);

/// <summary>A scroll container's position, and requests to move it. Each container has one, the one it was given as its
/// <c>scroll</c> or its own. Reads are tracked and render again in the next update.</summary>
[FramePath]
public sealed class ScrollHandle : IObservable
{
    ObserverSet _observers;
    internal Node? Node;
    internal bool Implicit;
    internal Vector2 Current;
    internal Size ViewportSize;
    internal Size ExtentSize;
    internal Vector2 ReportedOffset;
    internal Size ReportedExtent;
    internal Size ReportedViewport;
    internal bool NoticeQueued;
    internal bool HasRequest;
    internal float? RequestX;
    internal float? RequestY;
    internal Vector2 RequestBy;
    internal bool RequestAnimate;
    internal bool ToEnd;
    internal int ToIndex = -1;
    internal ScrollAlign ToIndexAlign;
    internal bool ToIndexAnimate;
    internal bool ToIndexShown;
    internal ScrollMotion Motion;
    internal bool Moving;
    internal Vector2 GlideFrom;
    internal Vector2 GlideTo;
    internal TimeSpan GlideStart;
    internal Vector2 CoastVelocity;
    internal TimeSpan CoastTime;

    ref ObserverSet IObservable.Observers => ref _observers;

    /// <summary>How far the content is scrolled from its start. Tracked.</summary>
    public Vector2 Offset
    {
        get
        {
            Observation.Read(this, 0);
            return Current;
        }
    }

    /// <summary>The size of the area the content shows through, as of the last layout. Tracked.</summary>
    public Size Viewport
    {
        get
        {
            Observation.Read(this, 0);
            return ViewportSize;
        }
    }

    /// <summary>The size of the content, padding included, as of the last layout. Tracked.</summary>
    public Size Extent
    {
        get
        {
            Observation.Read(this, 0);
            return ExtentSize;
        }
    }

    /// <summary>Scrolls to <paramref name="x"/> and <paramref name="y"/> at the next layout, clamped there; a null axis
    /// stays where it is. With <paramref name="animate"/> it glides there while the context's <c>SmoothScrolling</c>
    /// is on; otherwise it moves at once, stopping any glide or coast.</summary>
    public void ScrollTo(float? x, float? y, bool animate = false)
    {
        if (x is { } toX)
        {
            RequestX = toX;
            RequestBy.X = 0f;
        }
        if (y is { } toY)
        {
            RequestY = toY;
            RequestBy.Y = 0f;
        }
        Request(animate);
    }

    /// <summary>Scrolls by <paramref name="delta"/> at the next layout, clamped there.</summary>
    public void ScrollBy(Vector2 delta, bool animate = false)
    {
        RequestBy += delta;
        Request(animate);
    }

    /// <summary>Scrolls to the end, as the next layout settles it, and stays at the end while what comes before it
    /// changes size, until scrolled elsewhere.</summary>
    public void ScrollToEnd()
    {
        ClearRequests();
        ToEnd = true;
        StopMotion();
        Invalidate();
    }

    /// <summary>Scrolls by <paramref name="pages"/> viewports along the scrolling axis, at once.</summary>
    public void PageBy(int pages)
    {
        var view = ViewportSize;
        RequestBy += ScrollsVertically()
            ? new Vector2(0f, pages * view.Height)
            : new Vector2(pages * view.Width, 0f);
        Request(animate: false);
    }

    /// <summary>Scrolls a <c>VirtualList</c> to show its item at <paramref name="index"/>, realizing it, at the next
    /// layout. An index out of range is ignored.</summary>
    public void ScrollToIndex(int index, ScrollAlign align = default, bool animate = false)
    {
        if (index < 0)
            return;
        ClearRequests();
        ToEnd = false;
        ToIndex = index;
        ToIndexAlign = align;
        ToIndexAnimate = animate;
        if (!animate)
            StopMotion();
        Invalidate();
    }

    /// <summary>Scrolls every scroll container around <paramref name="target"/>'s node, innermost first, as little as
    /// <paramref name="align"/> allows to show it, at the next layout.</summary>
    public void ScrollIntoView(NodeRef target, ScrollAlign align = default, bool animate = false)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Node is { View: { } view } node)
            view.RequestReveal(node, align, animate);
    }

    internal bool ScrollsVertically() =>
        Node is not { } node || node.Layout.Overflow is not Overflow.ScrollX;

    void Request(bool animate)
    {
        ToEnd = false;
        ToIndex = -1;
        HasRequest = true;
        RequestAnimate = animate;
        if (!animate)
            StopMotion();
        Invalidate();
    }

    internal void ClearRequests()
    {
        HasRequest = false;
        RequestX = RequestY = null;
        RequestBy = default;
        RequestAnimate = false;
        ToIndex = -1;
        ToIndexShown = false;
    }

    internal void Invalidate()
    {
        if (Node is { View: { } view } node)
            view.InvalidateArrange(node);
    }

    internal void StopMotion()
    {
        if (Motion == ScrollMotion.None)
            return;
        Motion = ScrollMotion.None;
        CoastVelocity = default;
    }

    internal void Notify() => _observers.Notify(this, 0);

    internal void Release()
    {
        Node?.View?.UnbindScroller(this);
        _observers.Clear();
        Reset();
        Pool<ScrollHandle>.Return(this);
    }

    internal void Reset()
    {
        Node = null;
        Implicit = false;
        Current = default;
        ViewportSize = ExtentSize = default;
        ReportedOffset = default;
        ReportedExtent = ReportedViewport = default;
        NoticeQueued = false;
        ClearRequests();
        ToEnd = false;
        ToIndexAlign = default;
        ToIndexAnimate = false;
        Motion = ScrollMotion.None;
        Moving = false;
        GlideFrom = GlideTo = CoastVelocity = default;
        GlideStart = CoastTime = default;
    }
}

internal enum ScrollMotion : byte
{
    None,
    Glide,
    Coast,
}

/// <summary>A wheel turn as it bubbles from the node under the pointer, or its captor.</summary>
[FramePath]
public ref struct WheelEvent
{
    internal bool Stopped;
    internal bool Prevented;

    internal WheelEvent(Vector2 notches, ModifierKeys modifiers, Vector2 position)
    {
        Notches = notches;
        Modifiers = modifiers;
        Position = position;
    }

    /// <summary>How far the wheel turned: positive Y away from the user, positive X to the right.</summary>
    public Vector2 Notches { get; }

    public ModifierKeys Modifiers { get; }

    /// <summary>Where the pointer is in the current handler node's border-box space.</summary>
    public Vector2 Position { get; internal set; }

    /// <summary>Marks the turn taken and stops it bubbling.</summary>
    public void StopPropagation() => Stopped = true;

    /// <summary>Marks the turn taken and keeps it from scrolling anything.</summary>
    public void PreventDefault() => Prevented = true;
}

[FramePath]
internal record struct ScrollProps
{
    public Panning Panning;
    public bool DragToPan;
    public ScrollHandle? FollowScroll;
    public Callback<ScrollChange> OnScrollChange;
}

[FramePath]
internal sealed class ScrollHandleHook : Hook
{
    public ScrollHandle? Handle;

    public static ScrollHandle Rent() => Pool<ScrollHandle>.TryRent() ?? new ScrollHandle();

    public override void Discard()
    {
        Handle?.Release();
        Handle = null;
        Pool<ScrollHandleHook>.Return(this);
    }
}

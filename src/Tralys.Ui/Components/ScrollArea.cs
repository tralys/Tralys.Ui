using System.Numerics;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.Components;

/// <summary>When a <see cref="ScrollArea"/> shows its bar along an axis.</summary>
public enum ScrollbarVisibility : byte
{
    /// <summary>While the content overflows that way.</summary>
    Auto,

    /// <summary>Always, with no thumb while the content fits.</summary>
    Visible,

    /// <summary>Never, though the axis still scrolls by the wheel, a finger and code.</summary>
    Hidden,

    /// <summary>The axis doesn't scroll: the content is laid out to the viewport's size that way.</summary>
    Disabled,
}

public readonly record struct ScrollAreaProps
{
    /// <summary>The handle its offset is read and requested through; its own when null.</summary>
    public ScrollHandle? Scroll { get; init; }

    public ScrollbarVisibility Horizontal { get; init; }

    public ScrollbarVisibility Vertical { get; init; }

    /// <summary>Space between the area's edges, or its bars, and the viewport the content shows through.</summary>
    public Thickness Padding { get; init; }

    /// <summary>The fill under the bars and the viewport, inside <see cref="Border"/>.</summary>
    public Paint Background { get; init; }

    /// <summary>The edge around the area; its bars and viewport sit inside it.</summary>
    public Border Border { get; init; }

    public Panning Panning { get; init; }

    /// <summary>Whether a left mouse drag on the content pans it both ways, under the scroll-all cursor.</summary>
    public bool DragToPan { get; init; }

    /// <summary>A handle whose horizontal offset and range this area holds to, as a pinned header row follows the
    /// table under it.</summary>
    public ScrollHandle? FollowScroll { get; init; }

    public Callback<ScrollChange> OnScrollChange { get; init; }

    /// <summary>The cursor over the content while <see cref="DragToPan"/> is off.</summary>
    public Cursor? Cursor { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    public Length MaxWidth { get; init; }

    public Length MaxHeight { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>Shows its children through a viewport set in by <see cref="ScrollAreaProps.Padding"/>, scrolled by the wheel, a
/// finger, its bars and its handle, with a vertical bar down its right edge and a horizontal bar along its bottom, each
/// shown as its <see cref="ScrollbarVisibility"/> says. The bars rest at 0.35 opacity while nothing reaches for them,
/// show fully while the pointer is over the area, the focus is within it or something inside holds a pointer, and fade
/// back over 0.25 s 1.2 s after the last of those ends. A bar's arrows glide a line of 16, its track a page, both adding
/// to a glide under way, and its thumb moves the content at once.</summary>
[Children]
public sealed class ScrollArea : Component<ScrollAreaProps>
{
    static readonly GridTracks Cells = new(Track.Fr(1f), Track.Auto);

    protected override void Render(Ui ui, in ScrollAreaProps p)
    {
        var handle = p.Scroll ?? ui.ScrollHandle();
        var wheel = ui.Remember(handle, static h => new Wheel(h));
        var shown = ui.IsHovered || ui.IsFocusWithin || ui.IsCapturing;
        var acrossX = p.Horizontal != ScrollbarVisibility.Disabled;
        var acrossY = p.Vertical != ScrollbarVisibility.Disabled;
        wheel.X = acrossX;
        wheel.Y = acrossY;
        var overflow =
            acrossX && acrossY ? Overflow.Scroll
            : acrossX ? Overflow.ScrollX
            : acrossY ? Overflow.ScrollY
            : Overflow.Clip;
        using (
            ui.Grid(
                columns: Cells,
                rows: Cells,
                width: p.Width,
                height: p.Height,
                maxWidth: p.MaxWidth,
                maxHeight: p.MaxHeight,
                background: p.Background,
                border: p.Border,
                cursor: p.DragToPan ? Cursors.ScrollAll : p.Cursor
            )
        )
        {
            using (
                ui.Box(
                    direction: Direction.Column,
                    overflow: overflow,
                    scroll: handle,
                    panning: p.Panning,
                    dragToPan: p.DragToPan,
                    followScroll: p.FollowScroll,
                    onScrollChange: p.OnScrollChange,
                    onWheel: wheel.Turned,
                    margin: p.Padding,
                    column: 1,
                    row: 1
                )
            )
                ui.Children();
            ui.ScrollAreaBar(
                handle: handle,
                vertical: true,
                visibility: p.Vertical,
                shown: shown,
                column: 2,
                row: 1
            );
            ui.ScrollAreaBar(
                handle: handle,
                vertical: false,
                visibility: p.Horizontal,
                shown: shown,
                column: 1,
                row: 2
            );
        }
    }

    [FramePath]
    sealed class Wheel
    {
        internal readonly Handler<WheelEvent> Turned;
        internal readonly ScrollHandle Handle;
        internal bool X;
        internal bool Y;

        public Wheel(ScrollHandle handle)
        {
            Handle = handle;
            Turned = Handler<WheelEvent>.Of(
                this,
                static (w, ref e) =>
                {
                    if (w.Takes(e.Notches))
                        e.StopPropagation();
                }
            );
        }

        bool Takes(Vector2 notches)
        {
            var offset = Handle.Offset;
            var extent = Handle.Extent;
            var viewport = Handle.Viewport;
            return (
                    X
                    && notches.X != 0f
                    && (notches.X > 0f ? offset.X < extent.Width - viewport.Width : offset.X > 0f)
                )
                || (
                    Y
                    && notches.Y != 0f
                    && (notches.Y < 0f ? offset.Y < extent.Height - viewport.Height : offset.Y > 0f)
                );
        }
    }
}

public readonly record struct ScrollAreaBarProps
{
    /// <summary>The scroller the bar shows and moves.</summary>
    public ScrollHandle? Handle { get; init; }

    public bool Vertical { get; init; }

    public ScrollbarVisibility Visibility { get; init; }

    /// <summary>Shows the bar fully, as while the pointer is over its scroller, the focus is within it or something
    /// inside holds a pointer; otherwise it fades back to rest.</summary>
    public bool Shown { get; init; }
}

/// <summary>The bar a <see cref="ScrollArea"/> shows along one axis, for a scroller of the app's own, such as a bare
/// <c>ui.VirtualList</c>, to show the same: a <see cref="Scrollbar"/> over <see cref="ScrollAreaBarProps.Handle"/>,
/// resting at 0.35 opacity, fully shown while <see cref="ScrollAreaBarProps.Shown"/>, and fading back over 0.25 s 1.2 s
/// after. Its arrows glide a line of 16, its track a page, and its thumb moves the content at once; a wheel turn over it
/// scrolls as one over its scroller does.</summary>
public sealed class ScrollAreaBar : Component<ScrollAreaBarProps>
{
    const float Idle = 0.35f;
    const float LineStep = 16f;

    /// <summary>The room the bar takes beside its viewport, its gap included, which a scroller showing it keeps its
    /// content clear of.</summary>
    public const float Space = Scrollbar.Gap + Scrollbar.Breadth;

    static readonly Transition Fade = new(0.25f, new KeySpline(0f, 0f, 1f, 1f), 1.2f);

    /// <summary>Whether a bar of <paramref name="visibility"/> shows over <paramref name="handle"/> as it stands: always
    /// while visible, and while the content overflows that way while auto.</summary>
    public static bool Shows(ScrollHandle handle, ScrollbarVisibility visibility, bool vertical) =>
        visibility == ScrollbarVisibility.Visible
        || (
            visibility == ScrollbarVisibility.Auto
            && (
                vertical
                    ? handle.Extent.Height > handle.Viewport.Height
                    : handle.Extent.Width > handle.Viewport.Width
            )
        );

    protected override void Render(Ui ui, in ScrollAreaBarProps p)
    {
        if (
            p.Handle is not { } handle
            || p.Visibility is ScrollbarVisibility.Hidden or ScrollbarVisibility.Disabled
        )
            return;
        var extent = handle.Extent;
        var viewport = handle.Viewport;
        var length = p.Vertical ? extent.Height : extent.Width;
        var shows = p.Vertical ? viewport.Height : viewport.Width;
        var offset = handle.Offset;
        var bar = ui.Remember(static () => AreaBar.Rent());
        bar.Handle = handle;
        bar.Vertical = p.Vertical;
        var opacity = ui.Animate(p.Shown ? 1f : Idle, p.Shown ? Transition.Instant : Fade);
        if (!Shows(handle, p.Visibility, p.Vertical))
            return;
        using (
            ui.Box(
                direction: p.Vertical ? Direction.Row : Direction.Column,
                opacity: opacity,
                onWheel: bar.Turned
            )
        )
            ui.Scrollbar(
                value: p.Vertical ? offset.Y : offset.X,
                onValueChange: bar.Moved,
                max: MathF.Max(0f, length - shows),
                viewportSize: shows,
                lineStep: LineStep,
                pageStep: shows,
                orientation: p.Vertical ? Orientation.Vertical : Orientation.Horizontal
            );
    }

    [FramePath]
    sealed class AreaBar : IDisposable
    {
        internal readonly Callback<ScrollbarChange> Moved;
        internal readonly Handler<WheelEvent> Turned;
        internal ScrollHandle? Handle;
        internal bool Vertical;

        public AreaBar()
        {
            Moved = Callback<ScrollbarChange>.Of(this, static (b, change) => b.Move(change));
            Turned = Handler<WheelEvent>.Of(this, static (b, ref e) => b.Wheel(ref e));
        }

        internal static AreaBar Rent() => Pool<AreaBar>.TryRent() ?? new AreaBar();

        public void Dispose()
        {
            Handle = null;
            Pool<AreaBar>.Return(this);
        }

        void Move(ScrollbarChange change)
        {
            if (Handle is not { } handle)
                return;
            var page = Vertical ? handle.Viewport.Height : handle.Viewport.Width;
            var by = change.Step switch
            {
                ScrollbarStep.LineDecrease => -LineStep,
                ScrollbarStep.LineIncrease => LineStep,
                ScrollbarStep.PageDecrease => -page,
                ScrollbarStep.PageIncrease => page,
                _ => float.NaN,
            };
            if (float.IsNaN(by))
            {
                if (Vertical)
                    handle.ScrollTo(null, change.Value);
                else
                    handle.ScrollTo(change.Value, null);
                return;
            }
            handle.ScrollBy(Vertical ? new Vector2(0f, by) : new Vector2(by, 0f), animate: true);
        }

        void Wheel(ref WheelEvent e)
        {
            if (
                Handle?.Node is { View: { } view } scroller
                && view.WheelScroll(scroller, e.Notches)
            )
            {
                e.StopPropagation();
                e.PreventDefault();
            }
        }
    }
}

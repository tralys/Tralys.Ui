using System.Numerics;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>What moved a <see cref="Scrollbar"/>.</summary>
public enum ScrollbarStep : byte
{
    /// <summary>An arrow's step toward the minimum.</summary>
    LineDecrease,

    /// <summary>An arrow's step toward the maximum.</summary>
    LineIncrease,

    /// <summary>A press on the track before the thumb.</summary>
    PageDecrease,

    /// <summary>A press on the track past the thumb.</summary>
    PageIncrease,

    /// <summary>The thumb dragged.</summary>
    Thumb,
}

/// <summary>A move a <see cref="Scrollbar"/> reports: the value it moves to, within its range, and what moved it.</summary>
public readonly record struct ScrollbarChange(float Value, ScrollbarStep Step);

public readonly record struct ScrollbarProps
{
    public float Value { get; init; }

    public Callback<ScrollbarChange> OnValueChange { get; init; }

    public float Min { get; init; }

    /// <summary>The largest value; one below <see cref="Min"/> counts as it.</summary>
    public float Max { get; init; }

    /// <summary>How much of the range shows at once, in the range's units, which sizes the thumb.</summary>
    public float ViewportSize { get; init; }

    /// <summary>How far an arrow steps: 16 by default.</summary>
    public float? LineStep { get; init; }

    /// <summary>How far a press on the track steps: <see cref="ViewportSize"/> by default.</summary>
    public float? PageStep { get; init; }

    /// <summary>Which way it runs; <see cref="Orientation.Vertical"/> by default.</summary>
    public Orientation? Orientation { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A slim bar showing where <see cref="ScrollbarProps.Value"/> sits in its range, and how much of it
/// <see cref="ScrollbarProps.ViewportSize"/> covers, set 8 off what it scrolls: a track between two arrows, its
/// minimum at the top or the left. It shows the value clamped into the range and reports each move, with what moved
/// it, without changing its own: an arrow steps a line as it is pressed, then every 50 ms after 250 ms while held over
/// it; a press on the track pages toward the pointer, then every 100 ms after 250 ms until the thumb reaches the
/// pointer; and the thumb dragged moves the value by the track's scale, as far as the pointer goes. The thumb is the
/// viewport's share of the track, no shorter than the theme's extra-small control height while the track is longer
/// than that, and with nothing to scroll the track is empty. Pressing it leaves the focus where it was.</summary>
public sealed class Scrollbar : Component<ScrollbarProps>
{
    internal const float Breadth = 8f;
    internal const float Gap = 8f;
    internal const float ArrowLength = 12f;

    static readonly Thickness BeforeVertical = new(Gap, 0f, 0f, 0f);
    static readonly Thickness BeforeHorizontal = new(0f, Gap, 0f, 0f);

    protected override void Render(Ui ui, in ScrollbarProps p)
    {
        var t = ui.Theme;
        var vertical = (p.Orientation ?? Orientation.Vertical) == Orientation.Vertical;
        var min = float.IsFinite(p.Min) ? p.Min : 0f;
        var max = float.IsFinite(p.Max) ? MathF.Max(min, p.Max) : min;
        var value = float.IsFinite(p.Value) ? Math.Clamp(p.Value, min, max) : min;
        var viewport = float.IsFinite(p.ViewportSize) ? MathF.Max(0f, p.ViewportSize) : 0f;
        var range = max - min;
        var bar = ui.Remember(
            (
                Track: ui.Ref(),
                Grip: ui.Ref(),
                BackArrow: ui.Ref(),
                ForwardArrow: ui.Ref(),
                BackPage: ui.Ref(),
                ForwardPage: ui.Ref()
            ),
            static parts =>
                ScrollbarModel.Rent(
                    parts.Track,
                    parts.Grip,
                    parts.BackArrow,
                    parts.ForwardArrow,
                    parts.BackPage,
                    parts.ForwardPage
                )
        );
        bar.Host = ui.Self(p);
        bar.Shown = value;
        bar.Min = min;
        bar.Max = max;
        bar.Vertical = vertical;
        ui.Effect(bar, 0, static (b, _, cx) => b.View = cx.View);
        var shortest = t.Metrics(ComponentSize.Xs).Height;
        var length = vertical ? bar.Track.Size.Height : bar.Track.Size.Width;
        var direction = vertical ? Direction.Column : Direction.Row;
        using (
            ui.Box(
                direction: direction,
                width: p.Width,
                height: p.Height,
                padding: vertical ? BeforeVertical : BeforeHorizontal,
                cursor: Cursors.Arrow
            )
        )
        {
            ui.ScrollbarArrow(
                side: vertical ? ArrowSide.Up : ArrowSide.Left,
                onStep: bar.LineBack,
                onPress: bar.LineBackDown,
                onRelease: bar.Release,
                nodeRef: bar.BackArrow
            );
            using (
                ui.Box(
                    direction: direction,
                    grow: 1f,
                    width: vertical ? Breadth : Length.Auto,
                    height: vertical ? Length.Auto : Breadth,
                    nodeRef: bar.Track
                )
            )
            {
                if (range > 0f)
                {
                    var share = (value - min) / range;
                    var grip = viewport / (range + viewport);
                    var rest = 1f - grip;
                    using (
                        ui.Box(
                            grow: rest * share,
                            onClick: bar.PageBack,
                            pressMode: PressMode.Press,
                            focusable: false,
                            onPointerDown: bar.PageBackDown,
                            onPointerUp: bar.Release,
                            onPointerCancel: bar.Release,
                            nodeRef: bar.BackPage
                        )
                    ) { }
                    ui.ScrollbarThumb(
                        drag: bar.Drag,
                        vertical: vertical,
                        minLength: length == 0f || length > shortest ? shortest : 0f,
                        nodeRef: bar.Grip,
                        grow: grip,
                        basis: 0f
                    );
                    using (
                        ui.Box(
                            grow: rest * (1f - share),
                            onClick: bar.PageForward,
                            pressMode: PressMode.Press,
                            focusable: false,
                            onPointerDown: bar.PageForwardDown,
                            onPointerUp: bar.Release,
                            onPointerCancel: bar.Release,
                            nodeRef: bar.ForwardPage
                        )
                    ) { }
                }
            }
            ui.ScrollbarArrow(
                side: vertical ? ArrowSide.Down : ArrowSide.Right,
                onStep: bar.LineForward,
                onPress: bar.LineForwardDown,
                onRelease: bar.Release,
                nodeRef: bar.ForwardArrow
            );
        }
    }
}

internal enum ArrowSide : byte
{
    Up,
    Down,
    Left,
    Right,
}

internal readonly record struct ScrollbarArrowProps
{
    public ArrowSide Side { get; init; }

    public Callback OnStep { get; init; }

    public Handler<PointerEvent> OnPress { get; init; }

    public Handler<PointerEvent> OnRelease { get; init; }

    public NodeRef? NodeRef { get; init; }
}

internal sealed class ScrollbarArrow : Component<ScrollbarArrowProps>
{
    protected override void Render(Ui ui, in ScrollbarArrowProps p)
    {
        var t = ui.Theme;
        var color =
            ui.IsDisabled ? t.Color(ColorToken.IconDisabled)
            : ui.IsHovered || ui.IsPressed ? t.Color(ColorToken.IconHover)
            : t.Color(ColorToken.Icon);
        var across = p.Side is ArrowSide.Up or ArrowSide.Down;
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                width: across ? Scrollbar.Breadth : Scrollbar.ArrowLength,
                height: across ? Scrollbar.ArrowLength : Scrollbar.Breadth,
                onClick: p.OnStep,
                pressMode: PressMode.Press,
                focusable: false,
                onPointerDown: p.OnPress,
                onPointerUp: p.OnRelease,
                onPointerCancel: p.OnRelease,
                nodeRef: p.NodeRef
            )
        )
            ui.Canvas(
                state: new ArrowLook(p.Side, ui.Animate(color)),
                draw: ArrowLook.Draw,
                width: across ? 8f : 4f,
                height: across ? 4f : 8f,
                shrink: 0f
            );
    }

    readonly record struct ArrowLook(ArrowSide Side, Color Color)
    {
        public static readonly CanvasDraw<ArrowLook> Draw = static (dc, s, info) =>
        {
            var w = info.Size.Width;
            var h = info.Size.Height;
            var path = info.Path();
            switch (s.Side)
            {
                case ArrowSide.Up:
                    path.MoveTo(new Vector2(0f, h));
                    path.LineTo(new Vector2(w / 2f, 0f));
                    path.LineTo(new Vector2(w, h));
                    break;
                case ArrowSide.Down:
                    path.MoveTo(new Vector2(0f, 0f));
                    path.LineTo(new Vector2(w / 2f, h));
                    path.LineTo(new Vector2(w, 0f));
                    break;
                case ArrowSide.Left:
                    path.MoveTo(new Vector2(w, 0f));
                    path.LineTo(new Vector2(0f, h / 2f));
                    path.LineTo(new Vector2(w, h));
                    break;
                default:
                    path.MoveTo(new Vector2(0f, 0f));
                    path.LineTo(new Vector2(w, h / 2f));
                    path.LineTo(new Vector2(0f, h));
                    break;
            }
            path.Close();
            dc.DrawPath(path, s.Color, default);
        };
    }
}

internal readonly record struct ScrollbarThumbProps
{
    public ThumbDrag? Drag { get; init; }

    public bool Vertical { get; init; }

    public float MinLength { get; init; }

    public NodeRef? NodeRef { get; init; }
}

internal sealed class ScrollbarThumb : Component<ScrollbarThumbProps>
{
    protected override void Render(Ui ui, in ScrollbarThumbProps p)
    {
        var t = ui.Theme;
        var color =
            ui.IsDisabled ? t.Color(ColorToken.IconDisabled)
            : ui.IsHovered || ui.IsCapturing ? t.Color(ColorToken.IconHover)
            : t.Color(ColorToken.Icon);
        var drag = p.Drag;
        using (
            ui.Box(
                minWidth: p.Vertical ? Length.Auto : p.MinLength,
                minHeight: p.Vertical ? p.MinLength : Length.Auto,
                radius: t.Radius(RadiusToken.Md),
                background: ui.Animate(color),
                onPointerDown: drag?.Down ?? default,
                onPointerMove: drag?.Move ?? default,
                onPointerUp: drag?.Up ?? default,
                onPointerCancel: drag?.Cancel ?? default,
                nodeRef: p.NodeRef
            )
        ) { }
    }
}

[FramePath]
internal sealed class ScrollbarModel : IDisposable
{
    const float LineStep = 16f;
    const float Delay = 0.25f;
    const float LineInterval = 0.05f;
    const float PageInterval = 0.1f;

    internal readonly Callback LineBack;
    internal readonly Callback LineForward;
    internal readonly Callback PageBack;
    internal readonly Callback PageForward;
    internal readonly Handler<PointerEvent> LineBackDown;
    internal readonly Handler<PointerEvent> LineForwardDown;
    internal readonly Handler<PointerEvent> PageBackDown;
    internal readonly Handler<PointerEvent> PageForwardDown;
    internal readonly Handler<PointerEvent> Release;
    internal readonly ThumbDrag Drag = new();
    internal Host<ScrollbarProps> Host;
    internal NodeRef Track;
    internal NodeRef Grip;
    internal NodeRef BackArrow;
    internal NodeRef ForwardArrow;
    internal NodeRef BackPage;
    internal NodeRef ForwardPage;
    internal UiView? View;
    internal float Shown;
    internal float Min;
    internal float Max;
    internal bool Vertical;
    ScrollbarStep _held;
    bool _holding;
    bool _ticking;
    TimeSpan _next;

    ScrollbarModel(
        NodeRef track,
        NodeRef grip,
        NodeRef backArrow,
        NodeRef forwardArrow,
        NodeRef backPage,
        NodeRef forwardPage
    )
    {
        Track = track;
        Grip = grip;
        BackArrow = backArrow;
        ForwardArrow = forwardArrow;
        BackPage = backPage;
        ForwardPage = forwardPage;
        LineBack = Callback.Of(this, static b => b.Step(ScrollbarStep.LineDecrease));
        LineForward = Callback.Of(this, static b => b.Step(ScrollbarStep.LineIncrease));
        PageBack = Callback.Of(this, static b => b.Step(ScrollbarStep.PageDecrease));
        PageForward = Callback.Of(this, static b => b.Step(ScrollbarStep.PageIncrease));
        LineBackDown = Handler<PointerEvent>.Of(
            this,
            static (b, ref e) => b.Hold(ScrollbarStep.LineDecrease, ref e)
        );
        LineForwardDown = Handler<PointerEvent>.Of(
            this,
            static (b, ref e) => b.Hold(ScrollbarStep.LineIncrease, ref e)
        );
        PageBackDown = Handler<PointerEvent>.Of(
            this,
            static (b, ref e) => b.Hold(ScrollbarStep.PageDecrease, ref e)
        );
        PageForwardDown = Handler<PointerEvent>.Of(
            this,
            static (b, ref e) => b.Hold(ScrollbarStep.PageIncrease, ref e)
        );
        Release = Handler<PointerEvent>.Of(this, static (b, ref e) => b._holding = false);
        Drag.Moved = Callback<Vector2>.Of(this, static (b, moved) => b.Dragged(moved));
    }

    internal static ScrollbarModel Rent(
        NodeRef track,
        NodeRef grip,
        NodeRef backArrow,
        NodeRef forwardArrow,
        NodeRef backPage,
        NodeRef forwardPage
    )
    {
        if (Pool<ScrollbarModel>.TryRent() is not { } model)
            return new ScrollbarModel(track, grip, backArrow, forwardArrow, backPage, forwardPage);
        model.Track = track;
        model.Grip = grip;
        model.BackArrow = backArrow;
        model.ForwardArrow = forwardArrow;
        model.BackPage = backPage;
        model.ForwardPage = forwardPage;
        return model;
    }

    // A tick still posted keeps its flag, so it serves whichever bar rents the model next.
    public void Dispose()
    {
        Host = default;
        View = null;
        _holding = false;
        Drag.Reset();
        Pool<ScrollbarModel>.Return(this);
    }

    void Step(ScrollbarStep step)
    {
        ref readonly var p = ref Host.Props;
        var line = p.LineStep ?? LineStep;
        var page = p.PageStep ?? MathF.Max(0f, p.ViewportSize);
        var to = step switch
        {
            ScrollbarStep.LineDecrease => Shown - line,
            ScrollbarStep.LineIncrease => Shown + line,
            ScrollbarStep.PageDecrease => Shown - page,
            _ => Shown + page,
        };
        Report(to, step);
    }

    void Dragged(Vector2 moved)
    {
        var track = Track.Size;
        var grip = Grip.Size;
        var room = Vertical ? track.Height - grip.Height : track.Width - grip.Width;
        if (!(room > 0f))
            return;
        var along = Vertical ? moved.Y : moved.X;
        Report(Shown + along * (Max - Min) / room, ScrollbarStep.Thumb);
    }

    void Report(float to, ScrollbarStep step)
    {
        var value = float.IsFinite(to) ? Math.Clamp(to, Min, Max) : Min;
        if (step == ScrollbarStep.Thumb && value == Shown)
            return;
        Host.Props.OnValueChange.Invoke(new ScrollbarChange(value, step));
    }

    void Hold(ScrollbarStep step, ref PointerEvent e)
    {
        if (e.Pointer.Kind == PointerKind.Mouse && e.Button != MouseButton.Left)
            return;
        e.Capture();
        _held = step;
        _holding = true;
        if (View is not { } view)
            return;
        _next = view.Context.Now + TimeSpan.FromSeconds(Delay);
        Tick(view);
    }

    void OnTick()
    {
        _ticking = false;
        if (!_holding || !Host.IsMounted || View is not { } view)
        {
            _holding = false;
            return;
        }
        var part = _held switch
        {
            ScrollbarStep.LineDecrease => BackArrow.Node,
            ScrollbarStep.LineIncrease => ForwardArrow.Node,
            ScrollbarStep.PageDecrease => BackPage.Node,
            _ => ForwardPage.Node,
        };
        if (part is null || (part.States & InputStates.Disabled) != 0)
        {
            _holding = false;
            return;
        }
        var now = view.Context.Now;
        if ((part.States & InputStates.Pressed) != 0 && now >= _next)
        {
            var interval = _held is ScrollbarStep.LineDecrease or ScrollbarStep.LineIncrease
                ? LineInterval
                : PageInterval;
            _next = now + TimeSpan.FromSeconds(interval);
            Step(_held);
        }
        Tick(view);
    }

    void Tick(UiView view)
    {
        if (_ticking || !_holding)
            return;
        _ticking = true;
        view.Context.Dispatcher.Post(this, static b => b.OnTick());
    }
}

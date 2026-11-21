using System.Numerics;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>Where a <see cref="Slider"/> shows its ticks.</summary>
public enum TickPlacement : byte
{
    None,

    /// <summary>Above a horizontal slider, or left of a vertical one.</summary>
    TopLeft,

    /// <summary>Below a horizontal slider, or right of a vertical one.</summary>
    BottomRight,

    Both,
}

public readonly record struct SliderProps
{
    public float Value { get; init; }

    public Callback<float> OnValueChange { get; init; }

    public float Min { get; init; }

    /// <summary>The largest value: 10 by default; one below <see cref="Min"/> counts as it.</summary>
    public float? Max { get; init; }

    /// <summary>How far an arrow key moves the value: 0.1 by default.</summary>
    public float? Step { get; init; }

    /// <summary>How far Page Up, Page Down and a press on the track move the value: 1 by default.</summary>
    public float? PageStep { get; init; }

    /// <summary>A press on the track moves the thumb to the pointer and drags it from there, in place of stepping a
    /// page toward it.</summary>
    public bool MoveToPress { get; init; }

    /// <summary>The distance between ticks, in the range's units, counted from <see cref="Min"/>: 1 by default.</summary>
    public float? TickFrequency { get; init; }

    public TickPlacement Ticks { get; init; }

    /// <summary>Lands a drag on the nearest tick, and a step on the next tick its way; a value given stays as
    /// given.</summary>
    public bool SnapToTicks { get; init; }

    /// <summary>Runs the value from the right, or from the top when vertical, which also turns the arrow and page keys
    /// round.</summary>
    public bool Inverted { get; init; }

    public Orientation Orientation { get; init; }

    /// <summary>The length of a horizontal slider.</summary>
    public Length Width { get; init; }

    /// <summary>The length of a vertical slider.</summary>
    public Length Height { get; init; }

    public bool Disabled { get; init; }

    public Callback OnDragStart { get; init; }

    /// <summary>The thumb's drag ending: true when it was cancelled rather than released.</summary>
    public Callback<bool> OnDragEnd { get; init; }
}

/// <summary>Picks <see cref="SliderProps.Value"/> along a track, 20 across with the track 8 in from either end, the
/// value growing rightwards, or upwards when vertical. It shows the value clamped into its range and reports each move
/// without changing its own: the thumb dragged moves the value by the track's scale, a press on the track either side
/// of the thumb steps by <see cref="SliderProps.PageStep"/> toward the press, then every 100 ms after 250 ms until the
/// thumb reaches the pointer, or with <see cref="SliderProps.MoveToPress"/> moves the thumb to the press and drags it
/// from there, and with the focus the arrows step by <see cref="SliderProps.Step"/>, Page Up and Page
/// Down by <see cref="SliderProps.PageStep"/>, and Home and End go to the ends. The track fills in
/// <see cref="ColorToken.BgSelected"/> from the minimum to the thumb, and the thumb takes
/// <see cref="ColorToken.IconHover"/> while the slider is hovered and <see cref="ColorToken.FgAccent"/> while dragged.
/// A skin's <see cref="SkinPart.SliderTrack"/> and <see cref="SkinPart.SliderThumb"/> art replace the track and the
/// thumb, and its <see cref="SkinPart.SliderThumbHover"/> art shows over the thumb while the slider is hovered.</summary>
public sealed class Slider : Component<SliderProps>
{
    internal const float Breadth = 20f;
    internal const float Inset = 8f;

    static readonly Thickness AlongX = new(Inset, 0f, Inset, 0f);
    static readonly Thickness AlongY = new(0f, Inset, 0f, Inset);

    protected override void Render(Ui ui, in SliderProps p)
    {
        var t = ui.Theme;
        var vertical = p.Orientation == Orientation.Vertical;
        var min = float.IsFinite(p.Min) ? p.Min : 0f;
        var max =
            p.Max is { } top && float.IsFinite(top) ? MathF.Max(min, top) : MathF.Max(min, 10f);
        var value = float.IsFinite(p.Value) ? Math.Clamp(p.Value, min, max) : min;
        var share = max > min ? (value - min) / (max - min) : 0f;
        var model = ui.Remember(
            (Root: ui.Ref(), Track: ui.Ref()),
            static refs => new SliderModel(refs.Root, refs.Track)
        );
        model.Host = ui.Self(p);
        model.Shown = value;
        model.Min = min;
        model.Max = max;
        model.Vertical = vertical;
        model.Inverted = p.Inverted;
        ui.Effect(model, 0, static (m, _, cx) => m.View = cx.View);

        var disabled = ui.IsDisabled;
        var fill = t.Color(disabled ? ColorToken.BorderDisabled : ColorToken.BgSelected);
        var line = t.Color(ColorToken.Border);
        var fromStart = vertical != p.Inverted ? 1f - share : share;
        var filled = Length.Percent(share * 100f);
        var trackArt = t.Skin(SkinPart.SliderTrack);
        var ticks = new TickLook(
            min,
            max,
            p.TickFrequency ?? 1f,
            vertical,
            p.Inverted,
            false,
            line
        );
        using (
            ui.Box(
                width: vertical ? Breadth : p.Width,
                height: vertical ? p.Height : Breadth,
                padding: vertical ? AlongY : AlongX,
                onKeyDown: model.Keys,
                focusable: true,
                nodeRef: model.Root
            )
        )
        {
            using (
                ui.Box(
                    grow: 1f,
                    onPointerDown: model.TrackDown,
                    onPointerMove: model.TrackMove,
                    onPointerUp: model.TrackUp,
                    onPointerCancel: model.TrackCancel,
                    nodeRef: model.Track
                )
            )
            {
                if (trackArt is { } art)
                    using (
                        ui.Box(
                            position: Position.Absolute,
                            left: vertical ? 8f : 0f,
                            top: vertical ? 0f : 8f,
                            right: vertical ? Length.Auto : 0f,
                            bottom: vertical ? 0f : Length.Auto,
                            width: vertical ? 4f : Length.Auto,
                            height: vertical ? Length.Auto : 4f
                        )
                    )
                        ui.Art(art: art);
                else
                    using (
                        ui.Box(
                            position: Position.Absolute,
                            left: vertical ? 9f : 0f,
                            top: vertical ? 0f : 9f,
                            right: vertical ? Length.Auto : 0f,
                            bottom: vertical ? 0f : Length.Auto,
                            width: vertical ? 2f : Length.Auto,
                            height: vertical ? Length.Auto : 2f,
                            background: line
                        )
                    ) { }
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: vertical ? 9f
                            : p.Inverted ? Length.Auto
                            : 0f,
                        right: vertical || !p.Inverted ? Length.Auto : 0f,
                        top: !vertical ? 9f
                            : p.Inverted ? 0f
                            : Length.Auto,
                        bottom: vertical && !p.Inverted ? 0f : Length.Auto,
                        width: vertical ? 2f : filled,
                        height: vertical ? filled : 2f,
                        background: ui.Animate(fill)
                    )
                ) { }
                if (p.Ticks is TickPlacement.TopLeft or TickPlacement.Both)
                    ui.Canvas(
                        state: ticks,
                        draw: TickLook.Draw,
                        position: Position.Absolute,
                        left: 0f,
                        top: 0f,
                        right: vertical ? Length.Auto : 0f,
                        bottom: vertical ? 0f : Length.Auto,
                        width: vertical ? 4f : Length.Auto,
                        height: vertical ? Length.Auto : 4f
                    );
                if (p.Ticks is TickPlacement.BottomRight or TickPlacement.Both)
                    ui.Canvas(
                        state: ticks with
                        {
                            After = true,
                        },
                        draw: TickLook.Draw,
                        position: Position.Absolute,
                        left: vertical ? Length.Auto : 0f,
                        top: vertical ? 0f : Length.Auto,
                        right: 0f,
                        bottom: 0f,
                        width: vertical ? 4f : Length.Auto,
                        height: vertical ? Length.Auto : 4f
                    );
                var thumb = t.Skin(SkinPart.SliderThumb) is null ? 16f : 14f;
                var along = Length.Percent(fromStart * 100f);
                var across = (Breadth - thumb) / 2f;
                ui.SliderThumb(
                    drag: model.Drag,
                    size: thumb,
                    hovered: ui.IsHovered,
                    dragged: p.MoveToPress && ui.IsCapturing,
                    position: Position.Absolute,
                    left: vertical ? across : along,
                    top: vertical ? along : across,
                    margin: vertical
                        ? new Thickness(0f, -thumb / 2f, 0f, 0f)
                        : new Thickness(-thumb / 2f, 0f, 0f, 0f)
                );
            }
            if (ui.IsFocusVisible)
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 0f,
                        top: 0f,
                        right: 0f,
                        bottom: 0f,
                        border: new Border(1f, t.Color(ColorToken.BorderFocus)),
                        radius: t.Radius(RadiusToken.Md),
                        pointerEvents: PointerEvents.None
                    )
                ) { }
        }
    }

    readonly record struct TickLook(
        float Min,
        float Max,
        float Frequency,
        bool Vertical,
        bool Inverted,
        bool After,
        Color Color
    )
    {
        public static readonly CanvasDraw<TickLook> Draw = static (dc, s, info) =>
        {
            var width = info.Size.Width;
            var height = info.Size.Height;
            var range = s.Max - s.Min;
            var length = s.Vertical ? height : width;
            if (!(length > 0f) || !(range > 0f))
                return;
            var depth = s.Vertical ? width : height;
            var edge = s.After ? 0f : depth;
            var reach = s.After ? depth : -depth;
            var grows = s.Vertical == s.Inverted;
            Tick(dc, s, Along(length, 0f, grows), edge, reach);
            Tick(dc, s, Along(length, length, grows), edge, reach);
            var interval = s.Frequency;
            if (!(interval > 0f))
                return;
            interval = MathF.Max(interval, range / length);
            var perUnit = length / range;
            for (var at = interval; at < range; at += interval)
                Tick(dc, s, Along(length, at * perUnit, grows), edge, reach * 0.75f);
        };

        static float Along(float length, float distance, bool grows) =>
            grows ? distance : length - distance;

        static void Tick(DrawingContext dc, in TickLook s, float at, float edge, float reach)
        {
            var from = s.Vertical ? new Vector2(edge, at) : new Vector2(at, edge);
            var to = s.Vertical ? new Vector2(edge + reach, at) : new Vector2(at, edge + reach);
            dc.DrawLine(from, to, 1f, s.Color);
        }
    }
}

internal readonly record struct SliderThumbProps
{
    public ThumbDrag? Drag { get; init; }

    public float Size { get; init; }

    public bool Hovered { get; init; }

    public bool Dragged { get; init; }
}

/// <summary>A <see cref="Slider"/>'s thumb: a disc its caller centres on its point of the track.</summary>
internal sealed class SliderThumb : Component<SliderThumbProps>
{
    protected override void Render(Ui ui, in SliderThumbProps p)
    {
        var t = ui.Theme;
        var disabled = ui.IsDisabled;
        var art = t.Skin(SkinPart.SliderThumb);
        var color =
            disabled ? t.Color(ColorToken.BorderDisabled)
            : ui.IsCapturing || p.Dragged ? t.Color(ColorToken.FgAccent)
            : p.Hovered ? t.Color(ColorToken.IconHover)
            : t.Color(ColorToken.BgSelected);
        var drag = p.Drag;
        using (
            ui.Box(
                width: p.Size,
                height: p.Size,
                radius: p.Size / 2f,
                background: art is null ? ui.Animate(color) : default(Paint),
                cursor: disabled ? null : Cursors.Hand,
                onPointerDown: drag?.Down ?? default,
                onPointerMove: drag?.Move ?? default,
                onPointerUp: drag?.Up ?? default,
                onPointerCancel: drag?.Cancel ?? default
            )
        )
        {
            if (art is { } shown)
            {
                ui.Art(art: shown, opacity: disabled ? 0.5f : 1f);
                if (t.Skin(SkinPart.SliderThumbHover) is { } lit)
                    ui.Art(art: lit, opacity: p.Hovered ? (disabled ? 0.5f : 1f) : 0f);
            }
        }
    }
}

[FramePath]
internal sealed class SliderModel
{
    const float Delay = 0.25f;
    const float Interval = 0.1f;
    const float Reach = 8f;

    internal readonly ThumbDrag Drag = new();
    internal readonly Handler<KeyEvent> Keys;
    internal readonly Handler<PointerEvent> TrackDown;
    internal readonly Handler<PointerEvent> TrackMove;
    internal readonly Handler<PointerEvent> TrackUp;
    internal readonly Handler<PointerEvent> TrackCancel;
    internal Host<SliderProps> Host;
    internal readonly NodeRef Root;
    internal readonly NodeRef Track;
    internal UiView? View;
    internal float Shown;
    internal float Min;
    internal float Max;
    internal bool Vertical;
    internal bool Inverted;
    Pointer _pointer;
    Vector2 _at;
    bool _holding;
    bool _sliding;
    bool _increase;
    bool _ticking;
    TimeSpan _next;

    public SliderModel(NodeRef root, NodeRef track)
    {
        Root = root;
        Track = track;
        Keys = Handler<KeyEvent>.Of(this, static (m, ref e) => m.KeyDown(ref e));
        TrackDown = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Pressed(ref e));
        TrackMove = Handler<PointerEvent>.Of(
            this,
            static (m, ref e) =>
            {
                if (e.Pointer != m._pointer)
                    return;
                if (m._sliding)
                    m.SlideTo(e.ViewPosition);
                else if (m._holding)
                    m._at = e.ViewPosition;
            }
        );
        TrackUp = Handler<PointerEvent>.Of(
            this,
            static (m, ref e) => m.Released(ref e, cancelled: false)
        );
        TrackCancel = Handler<PointerEvent>.Of(
            this,
            static (m, ref e) => m.Released(ref e, cancelled: true)
        );
        Drag.Started = Callback.Of(
            this,
            static m =>
            {
                m.Root.Focus();
                m.Host.Props.OnDragStart.Invoke();
            }
        );
        Drag.Moved = Callback<Vector2>.Of(this, static (m, moved) => m.Dragged(moved));
        Drag.Ended = Callback<bool>.Of(
            this,
            static (m, cancelled) => m.Host.Props.OnDragEnd.Invoke(cancelled)
        );
    }

    float Length
    {
        get
        {
            var size = Track.Node?.ViewBounds.Size ?? default;
            return Vertical ? size.Height : size.Width;
        }
    }

    void KeyDown(ref KeyEvent e)
    {
        if (e.Modifiers != ModifierKeys.None)
            return;
        ref readonly var p = ref Host.Props;
        var small = Positive(p.Step ?? 0.1f);
        var large = Positive(p.PageStep ?? 1f);
        var turn = Inverted ? -1f : 1f;
        switch (e.Key)
        {
            case Key.Up:
            case Key.Right:
                StepToTick(turn * small);
                break;
            case Key.Down:
            case Key.Left:
                StepToTick(-turn * small);
                break;
            case Key.PageUp:
                StepToTick(turn * large);
                break;
            case Key.PageDown:
                StepToTick(-turn * large);
                break;
            case Key.Home:
                Report(Min);
                break;
            case Key.End:
                Report(Max);
                break;
            default:
                return;
        }
        e.PreventDefault();
        e.StopPropagation();
    }

    static float Positive(float step) => float.IsFinite(step) ? MathF.Max(0f, step) : 0f;

    void Dragged(Vector2 moved)
    {
        var length = Length;
        if (!(length > 0f))
            return;
        var along = Vertical ? -moved.Y : moved.X;
        if (Inverted)
            along = -along;
        var to = Shown + along * (Max - Min) / length;
        if (float.IsFinite(to))
            Report(Math.Clamp(Snap(to), Min, Max));
    }

    float Snap(float value)
    {
        ref readonly var p = ref Host.Props;
        if (!p.SnapToTicks)
            return value;
        var previous = Min;
        var next = Max;
        var frequency = p.TickFrequency ?? 1f;
        if (frequency > 0f)
        {
            previous = Min + MathF.Round((value - Min) / frequency) * frequency;
            next = MathF.Min(Max, previous + frequency);
        }
        return FloatUtil.GreaterThanOrClose(value, (previous + next) * 0.5f) ? next : previous;
    }

    void StepToTick(float direction)
    {
        if (direction == 0f)
            return;
        var value = Shown;
        var next = Snap(Math.Clamp(value + direction, Min, Max));
        var up = direction > 0f;
        if (
            Host.Props.SnapToTicks
            && (
                up
                    ? FloatUtil.LessThanOrClose(next, value)
                    : FloatUtil.GreaterThanOrClose(next, value)
            )
        )
            next = TickPast(value, up);
        Report(Math.Clamp(next, Min, Max));
    }

    float TickPast(float value, bool up)
    {
        var frequency = Host.Props.TickFrequency ?? 1f;
        if (!(frequency > 0f))
            return up ? Max : Min;
        var ticks = (value - Min) / frequency;
        if (FloatUtil.AreClose(ticks, MathF.Round(ticks)))
            ticks = MathF.Round(ticks);
        return Min + (up ? MathF.Floor(ticks) + 1f : MathF.Ceiling(ticks) - 1f) * frequency;
    }

    void Report(float value)
    {
        if (value != Shown)
            Host.Props.OnValueChange.Invoke(value);
    }

    void Pressed(ref PointerEvent e)
    {
        if (e.Pointer.Kind == PointerKind.Mouse && e.Button != MouseButton.Left)
            return;
        e.Capture();
        _pointer = e.Pointer;
        if (Host.Props.MoveToPress)
        {
            _sliding = true;
            Host.Props.OnDragStart.Invoke();
            SlideTo(e.ViewPosition);
            return;
        }
        _at = e.ViewPosition;
        _increase = ShareAt(_at) > ShownShare;
        _holding = true;
        Page();
        if (View is not { } view)
            return;
        _next = view.Context.Now + TimeSpan.FromSeconds(Delay);
        Tick(view);
    }

    void SlideTo(Vector2 point)
    {
        var to = Min + Math.Clamp(ShareAt(point), 0f, 1f) * (Max - Min);
        Report(Math.Clamp(Snap(to), Min, Max));
    }

    void Released(ref PointerEvent e, bool cancelled)
    {
        if (e.Pointer != _pointer)
            return;
        _holding = false;
        if (!_sliding)
            return;
        if (!cancelled && e.Pointer.Kind == PointerKind.Mouse && e.Button != MouseButton.Left)
            return;
        _sliding = false;
        Host.Props.OnDragEnd.Invoke(cancelled);
    }

    float ShownShare => Max > Min ? (Shown - Min) / (Max - Min) : 0f;

    float ShareAt(Vector2 point)
    {
        var bounds = Track.Node?.ViewBounds ?? default;
        var length = Vertical ? bounds.Height : bounds.Width;
        if (!(length > 0f))
            return 0f;
        var fromStart = Vertical ? (point.Y - bounds.Y) / length : (point.X - bounds.X) / length;
        return Vertical != Inverted ? 1f - fromStart : fromStart;
    }

    bool PastThumb()
    {
        var bounds = Track.Node?.ViewBounds ?? default;
        if (!bounds.Contains(_at))
            return false;
        var beyond = (ShareAt(_at) - ShownShare) * Length;
        return _increase ? beyond > Reach : beyond < -Reach;
    }

    void Page()
    {
        var large = Positive(Host.Props.PageStep ?? 1f);
        StepToTick(_increase ? large : -large);
    }

    void OnTick()
    {
        _ticking = false;
        if (!_holding || !Host.IsMounted || View is not { } view || Root.Node is not { } root)
        {
            _holding = false;
            return;
        }
        if ((root.States & InputStates.Disabled) != 0)
        {
            _holding = false;
            return;
        }
        var now = view.Context.Now;
        if (now >= _next)
        {
            _next = now + TimeSpan.FromSeconds(Interval);
            if (PastThumb())
                Page();
        }
        Tick(view);
    }

    void Tick(UiView view)
    {
        if (_ticking || !_holding)
            return;
        _ticking = true;
        view.Context.Dispatcher.Post(this, static m => m.OnTick());
    }
}

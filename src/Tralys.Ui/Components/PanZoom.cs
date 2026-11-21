using System.Numerics;

namespace Tralys.Ui.Components;

public readonly record struct PanZoomProps
{
    /// <summary>How many times their own size the children show at, kept within <see cref="MinZoom"/> and
    /// <see cref="MaxZoom"/>; uncontrolled while null.</summary>
    public float? Zoom { get; init; }

    /// <summary>The zoom an uncontrolled view starts at: 1 by default.</summary>
    public float? DefaultZoom { get; init; }

    public Callback<float> OnZoomChange { get; init; }

    /// <summary>The point in the view, from its top-left, that a change of <see cref="Zoom"/> holds still: the middle
    /// while null. The wheel and fingers zoom about where they are.</summary>
    public Vector2? ZoomAbout { get; init; }

    /// <summary>The smallest zoom: 0.5 by default.</summary>
    public float? MinZoom { get; init; }

    /// <summary>The largest zoom: 2 by default; one below <see cref="MinZoom"/> counts as it.</summary>
    public float? MaxZoom { get; init; }

    /// <summary>How far past each edge of the zoomed children the view may pan.</summary>
    public Thickness BoundaryMargin { get; init; }

    /// <summary>The handle its pan is read and requested through, as a scroll area's: its offset runs from the outer
    /// edge of <see cref="BoundaryMargin"/> around the zoomed children; its own when null.</summary>
    public ScrollHandle? Scroll { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>Shows its children at their own size, zoomed by <see cref="PanZoomProps.Zoom"/> and panned, clipped to its
/// box, with no chrome of its own. Children smaller than the view sit centred; larger ones pan until their edges are
/// <see cref="PanZoomProps.BoundaryMargin"/> inside the view's. A mouse drag or a finger pans it as they pan a scroll
/// container: past the drag threshold, the innermost container that can move the pointer's way claims it, so a scroll
/// area inside or around takes only a move this view can't make, and the pan takes the pointer from what it pressed, a
/// button too, which then clicks nothing. At this view's edge a finger's pan carries on into the next container out,
/// and a finger lifted while moving coasts on. The wheel zooms by 1.1 a notch about the pointer, two fingers zoom about their midpoint and pan
/// with it, the second taking the first from its pan, and, with the focus inside, Ctrl+Plus and Ctrl+Minus, or the
/// gamepad's shoulders, zoom by 1.25 about the middle, while its left stick pans. Keyboard focus moving inside pans it
/// into view. A scroll area inside takes the wheel it can scroll first; a thumb or drag inside keeps its pointer, and a
/// second finger then pans alone. Code pans it through <see cref="PanZoomProps.Scroll"/> and zooms it through
/// <see cref="PanZoomProps.Zoom"/>, about <see cref="PanZoomProps.ZoomAbout"/> or the middle. It reports each zoom and
/// changes its own only while uncontrolled.</summary>
[Children]
public sealed class PanZoom : Component<PanZoomProps>
{
    const float DefaultMin = 0.5f;
    const float DefaultMax = 2f;

    protected override void Render(Ui ui, in PanZoomProps p)
    {
        var own = ui.State(Valid(p.DefaultZoom) ?? 1f);
        var ownScroll = ui.ScrollHandle();
        var model = ui.Remember(
            (Own: own, Scroll: ownScroll, Root: ui.Ref(), Content: ui.Ref()),
            static hooks => new PanZoomModel(hooks.Own, hooks.Scroll, hooks.Root, hooks.Content)
        );
        model.Host = ui.Self(p);
        model.Handle = p.Scroll ?? ownScroll;
        ui.Effect(model, 0, static (m, _, cx) => m.View = cx.View);
        var (min, max) = Range(p);
        var zoom = Math.Clamp(Valid(p.Zoom) ?? own.Value, min, max);
        model.Shown(zoom, p.BoundaryMargin, p.ZoomAbout);
        var size = model.Content.Size;
        var known = size.Width > 0f || size.Height > 0f;
        using (
            ui.Box(
                direction: Direction.Column,
                width: p.Width,
                height: p.Height,
                padding: p.BoundaryMargin,
                overflow: Overflow.Scroll,
                scroll: model.Handle,
                dragToPan: true,
                onPointerDown: model.Down,
                onPointerMove: model.Move,
                onPointerUp: model.Up,
                onPointerCancel: model.Cancel,
                onWheel: model.Wheel,
                onKeyDown: model.Key,
                onGamepad: model.Gamepad,
                nodeRef: model.Root
            )
        )
        {
            ui.PanFromPressables();
            using (ui.Box(align: Align.Center, justify: Justify.Center, grow: 1f))
            using (
                ui.Box(
                    align: Align.Start,
                    width: known ? size.Width * zoom : Length.Auto,
                    height: known ? size.Height * zoom : Length.Auto,
                    shrink: 0f
                )
            )
            using (
                ui.Box(
                    direction: Direction.Column,
                    shrink: 0f,
                    transform: Transform.Scale(zoom),
                    transformOrigin: Vector2.Zero,
                    nodeRef: model.Content
                )
            )
                ui.Children();
        }
    }

    internal static (float Min, float Max) Range(in PanZoomProps p)
    {
        var min = Valid(p.MinZoom) ?? DefaultMin;
        return (min, MathF.Max(min, Valid(p.MaxZoom) ?? DefaultMax));
    }

    static float? Valid(float? zoom) => zoom is { } z && z > 0f && float.IsFinite(z) ? z : null;
}

[FramePath]
internal sealed class PanZoomModel
{
    const float WheelStep = 1.1f;
    const float CommandStep = 1.25f;
    const float StickSpeed = 600f;
    const float StickDeadZone = 0.25f;
    const float CoastFrom = 40f;
    const float CoastSlowing = 1000f;
    static readonly TimeSpan StillAfter = TimeSpan.FromSeconds(0.1);

    internal readonly Handler<PointerEvent> Down;
    internal readonly Handler<PointerEvent> Move;
    internal readonly Handler<PointerEvent> Up;
    internal readonly Handler<PointerEvent> Cancel;
    internal readonly Handler<WheelEvent> Wheel;
    internal readonly Handler<KeyEvent> Key;
    internal readonly Handler<GamepadEvent> Gamepad;
    internal Host<PanZoomProps> Host;
    internal readonly State<float> Own;
    internal ScrollHandle Handle;
    internal readonly NodeRef Root;
    internal readonly NodeRef Content;
    internal UiView? View;

    float _zoom;
    float _asked;
    Thickness _margin;
    bool _anchored;
    Vector2 _anchorContent;
    Vector2 _anchorAt;

    Pointer _pointer;
    bool _armed;
    bool _panning;
    Vector2 _start;
    Vector2 _at;
    Vector2 _origin;
    Vector2 _sampled;
    TimeSpan _sampledAt;
    Vector2 _velocity;

    bool _pinching;
    Pointer _second;
    Vector2 _secondAt;
    float _pinchDistance;
    float _pinchZoom;
    Vector2 _pinchContent;

    bool _taking;
    bool _handing;
    bool _coasting;
    Vector2 _coast;
    TimeSpan _coastTime;
    Vector2 _stick;
    TimeSpan _stickTime;
    bool _ticking;

    public PanZoomModel(State<float> own, ScrollHandle handle, NodeRef root, NodeRef content)
    {
        Own = own;
        Handle = handle;
        Root = root;
        Content = content;
        Down = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Pressed(ref e));
        Move = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Moved(ref e));
        Up = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Released(ref e));
        Cancel = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Lost(e.Pointer));
        Wheel = Handler<WheelEvent>.Of(this, static (m, ref e) => m.Turned(ref e));
        Key = Handler<KeyEvent>.Of(this, static (m, ref e) => m.Keyed(ref e));
        Gamepad = Handler<GamepadEvent>.Of(this, static (m, ref e) => m.Padded(ref e));
    }

    internal void Shown(float zoom, Thickness margin, Vector2? about)
    {
        var was = _zoom;
        var anchored = _anchored;
        var before = _margin;
        _margin = margin;
        _zoom = _asked = zoom;
        _anchored = false;
        if (was == 0f)
        {
            if (!Handle.HasRequest)
                Handle.ScrollTo(margin.Left, margin.Top);
            return;
        }
        if (before != margin)
            Handle.ScrollBy(new Vector2(margin.Left - before.Left, margin.Top - before.Top));
        if (was == zoom)
            return;
        var at = anchored ? _anchorAt : about ?? Middle;
        Place((anchored ? _anchorContent : ContentAt(at, was)) * zoom - at, zoom);
    }

    Vector2 Middle
    {
        get
        {
            var viewport = Handle.Viewport;
            return new Vector2(viewport.Width / 2f, viewport.Height / 2f);
        }
    }

    Vector2 ChildStart(float zoom)
    {
        var viewport = Handle.Viewport;
        var size = Content.Size;
        return new Vector2(
            _margin.Left
                + MathF.Max(
                    0f,
                    (viewport.Width - _margin.Left - _margin.Right - size.Width * zoom) / 2f
                ),
            _margin.Top
                + MathF.Max(
                    0f,
                    (viewport.Height - _margin.Top - _margin.Bottom - size.Height * zoom) / 2f
                )
        );
    }

    Vector2 ContentAt(Vector2 at, float zoom) => (at + Handle.Offset - ChildStart(zoom)) / zoom;

    void Place(Vector2 offset, float zoom)
    {
        var to = offset + ChildStart(zoom);
        Handle.ScrollTo(to.X, to.Y);
    }

    void ZoomTo(float zoom, Vector2 at, Vector2 content)
    {
        ref readonly var p = ref Host.Props;
        var (min, max) = PanZoom.Range(p);
        var to = Math.Clamp(zoom, min, max);
        if (to == _asked)
        {
            if (to == _zoom)
                Place(content * _zoom - at, _zoom);
            return;
        }
        _asked = to;
        _anchored = true;
        _anchorAt = at;
        _anchorContent = content;
        if (p.Zoom is null)
            Own.Value = to;
        p.OnZoomChange.Invoke(to);
    }

    void ZoomBy(float factor) => ZoomTo(_asked * factor, Middle, ContentAt(Middle, _zoom));

    void Pressed(ref PointerEvent e)
    {
        var mouse = e.Pointer.Kind == PointerKind.Mouse;
        if (mouse && e.Button != MouseButton.Left)
            return;
        _coasting = false;
        _taking = false;
        if (mouse)
            return;
        if (_armed && e.Pointer != _pointer)
        {
            if (!_pinching && Joins())
                BeginPinch(ref e);
            else if (!_panning)
                Arm(ref e);
            return;
        }
        Arm(ref e);
    }

    bool Joins()
    {
        if (_panning)
            return true;
        if (Root.Node is not { } root || View is not { } view)
            return false;
        _handing = true;
        var taken = view.TakePointer(_pointer, root, out var at);
        _handing = false;
        if (taken)
            _at = at;
        return taken;
    }

    void Arm(ref PointerEvent e)
    {
        _pointer = e.Pointer;
        _armed = true;
        _panning = false;
        _pinching = false;
        _start = _at = e.ViewPosition;
        _sampled = e.ViewPosition;
        _sampledAt = e.Time;
        _velocity = default;
    }

    void Take(ref PointerEvent e)
    {
        _taking = true;
        e.Capture();
    }

    void BeginPinch(ref PointerEvent e)
    {
        Take(ref e);
        e.StopPropagation();
        _second = e.Pointer;
        _secondAt = e.ViewPosition;
        _pinching = true;
        _pinchDistance = MathF.Max(Vector2.Distance(_at, _secondAt), 1f);
        _pinchZoom = _zoom;
        _pinchContent = ContentAt(Local((_at + _secondAt) / 2f), _zoom);
    }

    void Moved(ref PointerEvent e)
    {
        if (!_armed || e.Phase != PointerPhase.Move)
            return;
        _taking = false;
        if (_pinching && e.Pointer == _second)
        {
            _secondAt = e.ViewPosition;
            e.StopPropagation();
            Pinch();
            return;
        }
        if (e.Pointer != _pointer)
            return;
        _at = e.ViewPosition;
        if (_pinching)
        {
            Take(ref e);
            e.StopPropagation();
            Pinch();
            return;
        }
        if (!_panning)
            return;
        e.StopPropagation();
        Sample(e.ViewPosition, e.Time);
        var to = _origin - (_at - _start);
        Handle.ScrollTo(to.X, to.Y);
    }

    void Sample(Vector2 at, TimeSpan time)
    {
        var seconds = (float)(time - _sampledAt).TotalSeconds;
        if (!(seconds > 0f))
            return;
        _velocity = (at - _sampled) / seconds;
        _sampled = at;
        _sampledAt = time;
    }

    void Pinch()
    {
        var zoom = _pinchZoom * Vector2.Distance(_at, _secondAt) / _pinchDistance;
        ZoomTo(zoom, Local((_at + _secondAt) / 2f), _pinchContent);
    }

    void Released(ref PointerEvent e)
    {
        _taking = false;
        if (!_armed)
            return;
        if (_pinching && (e.Pointer == _pointer || e.Pointer == _second))
        {
            e.StopPropagation();
            if (e.Pointer == _pointer)
            {
                _pointer = _second;
                _at = _secondAt;
            }
            _pinching = false;
            _panning = true;
            _start = _at;
            _origin = Handle.Offset;
            _sampled = _at;
            _sampledAt = e.Time;
            _velocity = default;
            return;
        }
        if (e.Pointer != _pointer)
            return;
        var panned = _panning;
        _armed = _panning = false;
        if (!panned)
            return;
        e.PreventDefault();
        if (
            e.Time - _sampledAt <= StillAfter
            && _velocity.LengthSquared() >= CoastFrom * CoastFrom
            && View is { } view
        )
        {
            _coast = -_velocity;
            _coastTime = view.Context.Now;
            _coasting = true;
            Tick(view);
        }
    }

    void Lost(Pointer pointer)
    {
        if (_taking)
        {
            _taking = false;
            return;
        }
        if (_handing || (Root.Node is { } root && View?.Pans(pointer, root) == true))
            return;
        if (_pinching && pointer == _second)
        {
            _pinching = false;
            return;
        }
        if (_armed && pointer == _pointer)
            _armed = _panning = _pinching = false;
    }

    void Turned(ref WheelEvent e)
    {
        e.PreventDefault();
        var at = e.Position;
        ZoomTo(_asked * MathF.Pow(WheelStep, e.Notches.Y), at, ContentAt(at, _zoom));
    }

    void Keyed(ref KeyEvent e)
    {
        if (e.Modifiers != ModifierKeys.Control)
            return;
        if (e.Key is Tralys.Ui.Key.OemPlus or Tralys.Ui.Key.Add)
            ZoomBy(CommandStep);
        else if (e.Key is Tralys.Ui.Key.OemMinus or Tralys.Ui.Key.Subtract)
            ZoomBy(1f / CommandStep);
        else
            return;
        e.PreventDefault();
    }

    void Padded(ref GamepadEvent e)
    {
        if (e.Button is GamepadButton.LeftShoulder or GamepadButton.RightShoulder)
        {
            if (e.IsDown)
                ZoomBy(e.Button == GamepadButton.RightShoulder ? CommandStep : 1f / CommandStep);
            e.PreventDefault();
            return;
        }
        if (e.Stick != GamepadStick.Left)
            return;
        e.PreventDefault();
        var was = _stick;
        _stick = e.Value.LengthSquared() > StickDeadZone * StickDeadZone ? e.Value : default;
        if (was == default && _stick != default && View is { } view)
        {
            _stickTime = view.Context.Now;
            Tick(view);
        }
    }

    Vector2 Local(Vector2 viewPoint) =>
        Root.Node is { } root
            ? viewPoint - new Vector2(root.ViewBounds.X, root.ViewBounds.Y)
            : viewPoint;

    void OnTick()
    {
        _ticking = false;
        if (!Host.IsMounted || View is not { } view)
        {
            _coasting = false;
            _stick = default;
            return;
        }
        var now = view.Context.Now;
        if (_coasting)
            CoastStep(now);
        if (_stick != default)
            StickStep(now);
        Tick(view);
    }

    void CoastStep(TimeSpan now)
    {
        var seconds = (float)(now - _coastTime).TotalSeconds;
        _coastTime = now;
        if (!(seconds > 0f))
            return;
        var max = MaxOffset();
        var from = Handle.Offset;
        var moved = from + _coast * seconds;
        var to = Vector2.Clamp(moved, Vector2.Zero, max);
        if (to.X != moved.X)
            _coast.X = 0f;
        if (to.Y != moved.Y)
            _coast.Y = 0f;
        Handle.ScrollTo(to.X, to.Y);
        var speed = _coast.Length();
        var slowed = speed - CoastSlowing * seconds;
        if (!(slowed > 0f))
            _coasting = false;
        else
            _coast *= slowed / speed;
    }

    void StickStep(TimeSpan now)
    {
        if (
            Root.Node is not { } root
            || (root.States & (InputStates.Focused | InputStates.FocusWithin)) == 0
        )
        {
            _stick = default;
            return;
        }
        var seconds = (float)(now - _stickTime).TotalSeconds;
        _stickTime = now;
        if (seconds > 0f)
            Handle.ScrollBy(_stick * StickSpeed * seconds);
    }

    Vector2 MaxOffset()
    {
        var extent = Handle.Extent;
        var viewport = Handle.Viewport;
        return new Vector2(
            MathF.Max(0f, extent.Width - viewport.Width),
            MathF.Max(0f, extent.Height - viewport.Height)
        );
    }

    void Tick(UiView view)
    {
        if (_ticking || (!_coasting && _stick == default))
            return;
        _ticking = true;
        view.Context.Dispatcher.Post(this, static m => m.OnTick());
    }
}

using System.Numerics;

namespace Tralys.Ui.Components;

public enum TouchAimPhase : byte
{
    Move,
    Release,
}

/// <summary>Where a pointer dragging off a <see cref="TouchButton"/> points.</summary>
/// <param name="Phase">A move, or the pointer lifting or being cancelled.</param>
/// <param name="At">Where the pointer is, in the view's pixels.</param>
/// <param name="IsOverButton">Whether the pointer is back over the button, which calls the aim off when it releases
/// there.</param>
public readonly record struct TouchAim(TouchAimPhase Phase, Vector2 At, bool IsOverButton);

public readonly record struct TouchButtonProps
{
    public Callback OnClick { get; init; }

    /// <summary>Makes a pointer dragged off the button past <see cref="GestureSlop.Tap"/> aim instead of clicking:
    /// this takes a <see cref="TouchAim"/> as the pointer moves, and once as it lifts or is cancelled.</summary>
    public Callback<TouchAim> OnAim { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A button for a touch screen, whose content fills it: the first pointer to press it holds it until it
/// lifts, and clicks as it lifts over the button, however far it wandered meanwhile, while other fingers press other
/// buttons and a second finger on this one does nothing. A press leaves the focus where it is, a scroll area around it
/// never pans its pointer, and the system's edge swipes are kept off it. Each click runs
/// <see cref="TouchButtonProps.OnClick"/>; with <see cref="TouchButtonProps.OnAim"/> set, a pointer dragged off it
/// aims instead.</summary>
[Children]
public sealed class TouchButton : Component<TouchButtonProps>
{
    protected override void Render(Ui ui, in TouchButtonProps p)
    {
        ui.CanRun(p.OnClick);
        var model = ui.Remember(static () => new Presser());
        model.Host = ui.Self(p);
        ui.Effect(model, 0, static (m, _, cx) => m.View = cx.View);
        ui.Effect(model, ui.IsCapturing, static (m, capturing, _) => m.Captured(capturing));
        using (
            ui.Grid(
                width: p.Width,
                height: p.Height,
                excludeSystemGestures: true,
                onPointerDown: model.Down,
                onPointerMove: model.Move,
                onPointerUp: model.Up,
                onPointerCancel: model.Cancel
            )
        )
            ui.Children();
    }

    [FramePath]
    sealed class Presser
    {
        internal readonly Handler<PointerEvent> Down;
        internal readonly Handler<PointerEvent> Move;
        internal readonly Handler<PointerEvent> Up;
        internal readonly Handler<PointerEvent> Cancel;
        internal Host<TouchButtonProps> Host;
        internal UiView? View;
        Pointer _pointer;
        Vector2 _downAt;
        Vector2 _downInView;
        bool _held;
        bool _aiming;

        public Presser()
        {
            Down = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Pressed(ref e));
            Move = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Moved(ref e));
            Up = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Lifted(ref e));
            Cancel = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Cancelled(ref e));
        }

        void Pressed(ref PointerEvent e)
        {
            e.PreventDefault();
            if (_held || (e.Pointer.Kind == PointerKind.Mouse && e.Button != MouseButton.Left))
                return;
            e.Capture();
            _held = true;
            _aiming = false;
            _pointer = e.Pointer;
            _downAt = e.Position;
            _downInView = e.ViewPosition;
        }

        void Moved(ref PointerEvent e)
        {
            if (!_held || e.Pointer != _pointer)
                return;
            e.StopPropagation();
            if (!_aiming)
            {
                if (
                    Host.Props.OnAim.IsEmpty
                    || Vector2.DistanceSquared(e.Position, _downAt)
                        <= GestureSlop.Tap * GestureSlop.Tap
                )
                    return;
                _aiming = true;
            }
            Aim(TouchAimPhase.Move, e.ViewPosition, Over(e.Position, e.Size));
        }

        void Lifted(ref PointerEvent e)
        {
            if (
                !_held
                || e.Pointer != _pointer
                || (e.Pointer.Kind == PointerKind.Mouse && e.Button != MouseButton.Left)
            )
                return;
            e.StopPropagation();
            _held = false;
            if (_aiming)
            {
                _aiming = false;
                Aim(TouchAimPhase.Release, e.ViewPosition, Over(e.Position, e.Size));
                return;
            }
            if (Over(e.Position, e.Size))
                Host.Props.OnClick.Invoke();
        }

        void Cancelled(ref PointerEvent e)
        {
            if (e.Pointer == _pointer)
                Drop();
        }

        internal void Captured(bool capturing)
        {
            if (!capturing)
                Drop();
        }

        void Drop()
        {
            if (!_held)
                return;
            _held = false;
            if (!_aiming)
                return;
            _aiming = false;
            Aim(TouchAimPhase.Release, _downInView, over: true);
        }

        void Aim(TouchAimPhase phase, Vector2 inView, bool over)
        {
            ref readonly var p = ref Host.Props;
            p.OnAim.Invoke(new TouchAim(phase, inView * (View?.Scale ?? 1f), over));
        }

        static bool Over(Vector2 at, Size size) =>
            at.X >= 0f && at.Y >= 0f && at.X < size.Width && at.Y < size.Height;
    }
}

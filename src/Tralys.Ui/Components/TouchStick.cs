using System.Numerics;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>A straight step across the screen, which a game maps to its own axes.</summary>
public enum StickStep : byte
{
    None,
    Up,
    Right,
    Down,
    Left,
}

public readonly record struct TouchStickProps
{
    /// <summary>Reports each step the stick's pointer comes to hold: <see cref="StickStep.None"/> inside the dead
    /// zone, a quarter of the radius, and as the pointer lifts or is cancelled.</summary>
    public Callback<StickStep> OnStepChange { get; init; }

    /// <summary>120 by default.</summary>
    public Length Width { get; init; }

    /// <summary>120 by default.</summary>
    public Length Height { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A 4-way stick for a touch screen, steered by the pointer that presses it, which it keeps until that
/// pointer lifts, so other fingers press buttons meanwhile and a scroll area around it never pans it. Its knob follows
/// the pointer within its radius, half its smaller side, and the pointer's offset from the centre picks the step along
/// the axis it leans further on, across winning a tie. A press on it leaves the focus where it is, and the system's
/// edge swipes are kept off it.</summary>
public sealed class TouchStick : Component<TouchStickProps>
{
    const float DefaultSize = 120f;
    const float Knob = 52f;
    const float Chevron = 16f;
    const float Rim = 1f;
    const float ChevronInset = 8f - Rim;
    const float Round = 999f;

    protected override void Render(Ui ui, in TouchStickProps p)
    {
        var t = ui.Theme;
        var model = ui.Remember(static () => new Steering());
        model.Host = ui.Self(p);
        model.Knob = ui.State(Vector2.Zero);
        ui.Effect(model, ui.IsCapturing, static (m, capturing, _) => m.Captured(capturing));
        var knob = model.Knob.Value;
        var muted = t.Color(ColorToken.FgMuted);
        using (
            ui.Grid(
                width: p.Width.IsAuto ? DefaultSize : p.Width,
                height: p.Height.IsAuto ? DefaultSize : p.Height,
                background: t.Color(ColorToken.BgTranslucent),
                border: new Border(Rim, t.Color(ColorToken.Border)),
                radius: Round,
                excludeSystemGestures: true,
                onPointerDown: model.Down,
                onPointerMove: model.Move,
                onPointerUp: model.Up,
                onPointerCancel: model.Cancel
            )
        )
        {
            ui.Icon(
                glyph: IconGlyph.Of(IconRole.ChevronUp),
                size: Chevron,
                color: muted,
                alignSelf: AlignSelf.Start,
                justifySelf: AlignSelf.Center,
                margin: new Thickness(0f, ChevronInset, 0f, 0f)
            );
            ui.Icon(
                glyph: IconGlyph.Of(IconRole.ChevronRight),
                size: Chevron,
                color: muted,
                alignSelf: AlignSelf.Center,
                justifySelf: AlignSelf.End,
                margin: new Thickness(0f, 0f, ChevronInset, 0f)
            );
            ui.Icon(
                glyph: IconGlyph.Of(IconRole.ChevronDown),
                size: Chevron,
                color: muted,
                alignSelf: AlignSelf.End,
                justifySelf: AlignSelf.Center,
                margin: new Thickness(0f, 0f, 0f, ChevronInset)
            );
            ui.Icon(
                glyph: IconGlyph.Of(IconRole.ChevronLeft),
                size: Chevron,
                color: muted,
                alignSelf: AlignSelf.Center,
                justifySelf: AlignSelf.Start,
                margin: new Thickness(ChevronInset, 0f, 0f, 0f)
            );
            using (
                ui.Box(
                    background: t.Color(ColorToken.BgMuted),
                    border: new Border(1f, t.Color(ColorToken.BorderHover)),
                    radius: Round,
                    alignSelf: AlignSelf.Center,
                    justifySelf: AlignSelf.Center,
                    width: Knob,
                    height: Knob,
                    transform: knob == Vector2.Zero ? default : Transform.Translate(knob.X, knob.Y)
                )
            ) { }
        }
    }

    internal static StickStep StepOf(Vector2 offset, float deadZone)
    {
        if (offset.LengthSquared() < deadZone * deadZone)
            return StickStep.None;
        return MathF.Abs(offset.X) >= MathF.Abs(offset.Y)
            ? offset.X > 0f
                ? StickStep.Right
                : StickStep.Left
            : offset.Y > 0f
                ? StickStep.Down
                : StickStep.Up;
    }

    [FramePath]
    sealed class Steering
    {
        const float DeadZoneShare = 0.25f;

        internal readonly Handler<PointerEvent> Down;
        internal readonly Handler<PointerEvent> Move;
        internal readonly Handler<PointerEvent> Up;
        internal readonly Handler<PointerEvent> Cancel;
        internal Host<TouchStickProps> Host;
        internal State<Vector2> Knob = null!;
        Pointer _pointer;
        bool _held;
        StickStep _step;

        public Steering()
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
            _pointer = e.Pointer;
            Follow(e.Position, e.Size);
        }

        void Moved(ref PointerEvent e)
        {
            if (!_held || e.Pointer != _pointer)
                return;
            e.StopPropagation();
            Follow(e.Position, e.Size);
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
            Centre();
        }

        void Cancelled(ref PointerEvent e)
        {
            if (_held && e.Pointer == _pointer)
                Centre();
        }

        void Follow(Vector2 at, Size size)
        {
            var radius = MathF.Min(size.Width, size.Height) / 2f;
            var offset = at - new Vector2(size.Width / 2f, size.Height / 2f);
            var length = offset.Length();
            Knob.Value = length > radius && length > 0f ? offset * (radius / length) : offset;
            Report(StepOf(offset, radius * DeadZoneShare));
        }

        internal void Captured(bool capturing)
        {
            if (!capturing && _held)
                Centre();
        }

        void Centre()
        {
            _held = false;
            Knob.Value = Vector2.Zero;
            Report(StickStep.None);
        }

        void Report(StickStep step)
        {
            if (step == _step)
                return;
            _step = step;
            Host.Props.OnStepChange.Invoke(step);
        }
    }
}

using System.Numerics;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.Components;

public readonly record struct ThumbProps
{
    public Callback OnDragStart { get; init; }

    /// <summary>Each move of the dragging pointer: how far it is from where it pressed the thumb, in the thumb's
    /// space as last laid out, so a thumb laid out again under the pointer reports only what is left to move.</summary>
    public Callback<Vector2> OnDragDelta { get; init; }

    /// <summary>The drag's end: true when it was cancelled rather than released.</summary>
    public Callback<bool> OnDragEnd { get; init; }

    public Paint Background { get; init; }

    public Border Border { get; init; }

    public CornerRadius Radius { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A box a pointer drags: a left press, a finger or a pen captures the pointer and starts the drag, leaving
/// the focus where it was, each move reports how far the pointer is from where it pressed, and the release ends it, or
/// a cancel, a lost release or another node taking the pointer ends it cancelled. It moves nothing itself: its owner
/// places it from what it reports. A second pointer on it meanwhile does nothing, and no pan or drag around it takes
/// its pointer. It hits anywhere in its box, and draws its <see cref="ThumbProps.Background"/> and
/// <see cref="ThumbProps.Border"/> under its children.</summary>
[Children]
public sealed class Thumb : Component<ThumbProps>
{
    protected override void Render(Ui ui, in ThumbProps p)
    {
        var drag = ui.Remember(static () => new ThumbDrag());
        drag.Started = p.OnDragStart;
        drag.Moved = p.OnDragDelta;
        drag.Ended = p.OnDragEnd;
        using (
            ui.Box(
                direction: Direction.Column,
                width: p.Width,
                height: p.Height,
                background: p.Background,
                border: p.Border,
                radius: p.Radius,
                onPointerDown: drag.Down,
                onPointerMove: drag.Move,
                onPointerUp: drag.Up,
                onPointerCancel: drag.Cancel
            )
        )
            ui.Children();
    }
}

[FramePath]
internal sealed class ThumbDrag
{
    internal readonly Handler<PointerEvent> Down;
    internal readonly Handler<PointerEvent> Move;
    internal readonly Handler<PointerEvent> Up;
    internal readonly Handler<PointerEvent> Cancel;
    internal Callback Started;
    internal Callback<Vector2> Moved;
    internal Callback<bool> Ended;
    Pointer _pointer;
    Vector2 _origin;
    Vector2 _last;

    public ThumbDrag()
    {
        Down = Handler<PointerEvent>.Of(this, static (d, ref e) => d.Pressed(ref e));
        Move = Handler<PointerEvent>.Of(this, static (d, ref e) => d.Moving(ref e));
        Up = Handler<PointerEvent>.Of(this, static (d, ref e) => d.Released(ref e));
        Cancel = Handler<PointerEvent>.Of(
            this,
            static (d, ref e) =>
            {
                if (d.Dragging && e.Pointer == d._pointer)
                    d.Finish(cancelled: true);
            }
        );
    }

    internal bool Dragging { get; private set; }

    internal void Reset() => Dragging = false;

    void Pressed(ref PointerEvent e)
    {
        if (Dragging || (e.Pointer.Kind == PointerKind.Mouse && e.Button != MouseButton.Left))
            return;
        e.Capture();
        e.PreventDefault();
        e.StopPropagation();
        _pointer = e.Pointer;
        _origin = e.Position;
        _last = e.ViewPosition;
        Dragging = true;
        Started.Invoke();
    }

    void Moving(ref PointerEvent e)
    {
        if (!Dragging || e.Pointer != _pointer || e.Phase != PointerPhase.Move)
            return;
        e.StopPropagation();
        if (e.ViewPosition == _last)
            return;
        _last = e.ViewPosition;
        Moved.Invoke(e.Position - _origin);
    }

    void Released(ref PointerEvent e)
    {
        if (
            !Dragging
            || e.Pointer != _pointer
            || (e.Pointer.Kind == PointerKind.Mouse && e.Button != MouseButton.Left)
        )
            return;
        e.StopPropagation();
        Finish(cancelled: false);
    }

    void Finish(bool cancelled)
    {
        Dragging = false;
        Ended.Invoke(cancelled);
    }
}

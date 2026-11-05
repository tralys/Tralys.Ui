using System.Numerics;

namespace Tralys.Ui.Testing;

public sealed partial class UiDriver
{
    MouseButtons _mouseHeld;

    /// <summary>Presses at the centre of <paramref name="target"/>'s visible bounds and keeps holding: the mouse's
    /// <paramref name="button"/>, or a finger or pen contact of its own, as the <paramref name="clicks"/>th press of a
    /// run of clicks.</summary>
    public HeldPointer Hold(
        Node target,
        PointerKind kind = PointerKind.Mouse,
        MouseButton button = MouseButton.Left,
        bool settle = true,
        int clicks = 1
    ) => Hold(Aim(target), kind, button, settle, clicks);

    /// <summary>Presses at <paramref name="at"/>, in layout units, whatever is there, and keeps holding, as
    /// <see cref="Hold(Node, PointerKind, MouseButton, bool, int)"/> does.</summary>
    public HeldPointer Hold(
        Vector2 at,
        PointerKind kind = PointerKind.Mouse,
        MouseButton button = MouseButton.Left,
        bool settle = true,
        int clicks = 1
    )
    {
        var pointer = Contact(kind);
        if (kind == PointerKind.Mouse)
        {
            View.PointerMove(pointer, Pixels(at), _mouseHeld, _mouseHeld != 0, default, _time);
            _mouseHeld |= Flag(button);
        }
        else
        {
            button = MouseButton.Left;
            if (kind == PointerKind.Pen)
                View.PointerMove(pointer, Pixels(at), MouseButtons.None, false, default, _time);
        }
        View.PointerDown(pointer, Pixels(at), button, default, _time, clicks);
        if (settle)
            Settle();
        return new HeldPointer(this, pointer, button, at);
    }

    /// <summary>A touch landing on <paramref name="from"/>, moving by <paramref name="by"/> in 4 steps spread over
    /// <paramref name="seconds"/>, and lifting at once, so content it pans coasts on at its speed.</summary>
    public void Fling(Node from, Vector2 by, float seconds = 0.1f, bool settle = true)
    {
        var start = Aim(from);
        var finger = Contact(PointerKind.Touch);
        View.PointerDown(finger, Pixels(start), MouseButton.Left, default, _time);
        for (var step = 1; step <= 4; step++)
        {
            if (seconds > 0f)
                Frame(seconds / 4f);
            View.PointerMove(
                finger,
                Pixels(start + by * (step / 4f)),
                MouseButtons.Left,
                true,
                default,
                _time
            );
        }
        View.PointerUp(finger, Pixels(start + by), MouseButton.Left, default, _time);
        if (settle)
            Settle();
    }

    /// <summary>The window losing the keyboard focus, or getting it back; a held pointer stays held.</summary>
    public void WindowFocus(bool focused, bool settle = true)
    {
        View.WindowFocus(focused);
        if (settle)
            Settle();
    }

    internal Vector2 CentreOf(Node target)
    {
        var visible = VisibleBounds(Acted(target));
        return new Vector2(visible.X + visible.Width / 2f, visible.Y + visible.Height / 2f);
    }

    internal void MoveHeld(HeldPointer held)
    {
        var mouse = held.Pointer.Kind == PointerKind.Mouse;
        View.PointerMove(
            held.Pointer,
            Pixels(held.Position),
            mouse ? _mouseHeld : MouseButtons.Left,
            !mouse || _mouseHeld != 0,
            default,
            _time
        );
    }

    internal void ReleaseHeld(HeldPointer held)
    {
        if (held.Pointer.Kind == PointerKind.Mouse)
            _mouseHeld &= ~Flag(held.Button);
        View.PointerUp(held.Pointer, Pixels(held.Position), held.Button, default, _time);
    }

    internal void CancelHeld(HeldPointer held)
    {
        if (held.Pointer.Kind == PointerKind.Mouse)
            _mouseHeld &= ~Flag(held.Button);
        View.PointerCancel(held.Pointer);
    }

    internal void LoseHeld(HeldPointer held)
    {
        var mouse = held.Pointer.Kind == PointerKind.Mouse;
        if (mouse)
            _mouseHeld &= ~Flag(held.Button);
        View.PointerMove(
            held.Pointer,
            Pixels(held.Position),
            mouse ? _mouseHeld : MouseButtons.None,
            mouse && _mouseHeld != 0,
            default,
            _time
        );
    }

    static MouseButtons Flag(MouseButton button) => (MouseButtons)(1 << (int)button);
}

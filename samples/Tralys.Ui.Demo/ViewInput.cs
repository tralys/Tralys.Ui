using Tralys.Ui.DevTools;

namespace Tralys.Ui.Demo;

/// <summary>Hands a window's input to a view, keeping the mouse buttons held between events, which each pointer move
/// reports.</summary>
sealed class ViewInput
{
    MouseButtons _held;

    /// <summary>Makes the call on <paramref name="view"/> that <paramref name="input"/> stands for.</summary>
    /// <returns>Whether the view took it.</returns>
    [FramePath]
    public bool Deliver(UiView view, in InputEvent input)
    {
        var modifiers = input.Modifiers;
        var now = view.Context.Now;
        var finger = new Pointer(PointerKind.Touch, input.Finger);
        switch (input.Kind)
        {
            case InputKind.PointerMove:
                return view.PointerMove(
                    Pointer.Mouse,
                    input.Position,
                    _held,
                    _held != MouseButtons.None,
                    modifiers,
                    now
                );
            case InputKind.TouchMove:
                return view.PointerMove(
                    finger,
                    input.Position,
                    MouseButtons.Left,
                    true,
                    modifiers,
                    now
                );
            case InputKind.PointerDown:
                _held |= Flag(input.Button);
                return view.PointerDown(
                    Pointer.Mouse,
                    input.Position,
                    input.Button,
                    modifiers,
                    now,
                    Math.Max(1, (int)input.Clicks)
                );
            case InputKind.TouchDown:
                return view.PointerDown(finger, input.Position, MouseButton.Left, modifiers, now);
            case InputKind.PointerUp:
                _held &= ~Flag(input.Button);
                return view.PointerUp(Pointer.Mouse, input.Position, input.Button, modifiers, now);
            case InputKind.TouchUp:
                return view.PointerUp(finger, input.Position, MouseButton.Left, modifiers, now);
            case InputKind.TouchCancel:
                view.PointerCancel(finger);
                return true;
            case InputKind.Wheel:
                return view.Wheel(input.Position, input.Wheel, modifiers);
            case InputKind.PointerLeave:
                view.PointerLeave(Pointer.Mouse);
                return true;
            case InputKind.KeyDown:
                return view.KeyDown(input.Key, modifiers, input.Repeat);
            case InputKind.KeyUp:
                return view.KeyUp(input.Key, modifiers);
            case InputKind.Text:
                return view.TextInput(input.Text);
            case InputKind.FocusLost:
                _held = MouseButtons.None;
                view.WindowFocus(false);
                return true;
            case InputKind.FocusGained:
                view.WindowFocus(true);
                return true;
            case InputKind.GamepadButtonDown:
                view.GamepadStyle = input.PadStyle;
                return view.Gamepad(input.PadButton, true);
            case InputKind.GamepadButtonUp:
                return view.Gamepad(input.PadButton, false);
            case InputKind.GamepadStick:
                view.GamepadStyle = input.PadStyle;
                return view.Stick(input.Stick, input.StickValue);
            default:
                return false;
        }
    }

    /// <summary>Makes the call on <paramref name="inspector"/> that <paramref name="input"/> stands for: it shares the
    /// window's input between its panel and the view it inspects, which takes the gamepad.</summary>
    /// <returns>Whether either view took it.</returns>
    [FramePath]
    public bool Deliver(Inspector inspector, in InputEvent input)
    {
        var modifiers = input.Modifiers;
        var now = inspector.Target.Context.Now;
        var finger = new Pointer(PointerKind.Touch, input.Finger);
        switch (input.Kind)
        {
            case InputKind.PointerMove:
                return inspector.PointerMove(
                    Pointer.Mouse,
                    input.Position,
                    _held,
                    _held != MouseButtons.None,
                    modifiers,
                    now
                );
            case InputKind.TouchMove:
                return inspector.PointerMove(
                    finger,
                    input.Position,
                    MouseButtons.Left,
                    true,
                    modifiers,
                    now
                );
            case InputKind.PointerDown:
                _held |= Flag(input.Button);
                return inspector.PointerDown(
                    Pointer.Mouse,
                    input.Position,
                    input.Button,
                    modifiers,
                    now,
                    Math.Max(1, (int)input.Clicks)
                );
            case InputKind.TouchDown:
                return inspector.PointerDown(
                    finger,
                    input.Position,
                    MouseButton.Left,
                    modifiers,
                    now
                );
            case InputKind.PointerUp:
                _held &= ~Flag(input.Button);
                return inspector.PointerUp(
                    Pointer.Mouse,
                    input.Position,
                    input.Button,
                    modifiers,
                    now
                );
            case InputKind.TouchUp:
                return inspector.PointerUp(
                    finger,
                    input.Position,
                    MouseButton.Left,
                    modifiers,
                    now
                );
            case InputKind.TouchCancel:
                inspector.PointerCancel(finger);
                return true;
            case InputKind.Wheel:
                return inspector.Wheel(input.Position, input.Wheel, modifiers);
            case InputKind.PointerLeave:
                inspector.PointerLeave(Pointer.Mouse);
                return true;
            case InputKind.KeyDown:
                return inspector.KeyDown(input.Key, modifiers, input.Repeat);
            case InputKind.KeyUp:
                return inspector.KeyUp(input.Key, modifiers);
            case InputKind.Text:
                return inspector.TextInput(input.Text);
            case InputKind.FocusLost:
                _held = MouseButtons.None;
                inspector.WindowFocus(false);
                return true;
            case InputKind.FocusGained:
                inspector.WindowFocus(true);
                return true;
            default:
                return Deliver(inspector.Target, input);
        }
    }

    [FramePath]
    static MouseButtons Flag(MouseButton button) => (MouseButtons)(1 << (int)button);
}

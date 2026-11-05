using System.Numerics;

namespace Tralys.Ui.Testing;

public sealed partial class UiDriver
{
    ulong _contacts;

    /// <summary>Clicks <paramref name="target"/> twice, the second press counted as a double click.</summary>
    public void DoubleClick(Node target, bool settle = true)
    {
        var pixels = Pixels(Aim(target));
        View.PointerMove(Pointer.Mouse, pixels, MouseButtons.None, false, default, _time);
        for (var clicks = 1; clicks <= 2; clicks++)
        {
            View.PointerDown(Pointer.Mouse, pixels, MouseButton.Left, default, _time, clicks);
            View.PointerUp(Pointer.Mouse, pixels, MouseButton.Left, default, _time);
        }
        if (settle)
            Settle();
    }

    /// <summary>A finger landing on and lifting from the centre of <paramref name="target"/>'s visible bounds.</summary>
    public void Tap(Node target, bool settle = true)
    {
        var pixels = Pixels(Aim(target));
        var finger = Contact(PointerKind.Touch);
        View.PointerDown(finger, pixels, MouseButton.Left, default, _time);
        View.PointerUp(finger, pixels, MouseButton.Left, default, _time);
        if (settle)
            Settle();
    }

    /// <summary>A finger held still on <paramref name="target"/> until its hold starts, then lifted.</summary>
    public void LongPress(Node target, bool settle = true)
    {
        var pixels = Pixels(Aim(target));
        var finger = Contact(PointerKind.Touch);
        View.PointerDown(finger, pixels, MouseButton.Left, default, _time);
        Frame((float)UiView.HoldTime.TotalSeconds);
        View.PointerUp(finger, pixels, MouseButton.Left, default, _time);
        if (settle)
            Settle();
    }

    /// <summary>Sends <paramref name="button"/> down then up.</summary>
    public void Press(GamepadButton button, bool settle = true)
    {
        View.Gamepad(button, true);
        View.Gamepad(button, false);
        if (settle)
            Settle();
    }

    /// <summary>Moves <paramref name="stick"/> to <paramref name="value"/>, -1 to 1 on each axis with Y down; a
    /// stick held still sends nothing more until it moves, its return to centre included.</summary>
    public void Stick(GamepadStick stick, Vector2 value, bool settle = true)
    {
        View.Stick(stick, value);
        if (settle)
            Settle();
    }

    /// <summary>Presses Tab, or Shift+Tab.</summary>
    public void Tab(bool shift = false, bool settle = true) =>
        Press(Key.Tab, shift ? ModifierKeys.Shift : ModifierKeys.None, settle);

    Pointer Contact(PointerKind kind) =>
        kind == PointerKind.Mouse ? Pointer.Mouse : new Pointer(kind, ++_contacts);

    Vector2 Aim(Node target)
    {
        View.VerifyAccess();
        var node = Acted(target);
        var visible = VisibleBounds(node);
        var point = new Vector2(visible.X + visible.Width / 2f, visible.Y + visible.Height / 2f);
        if (View.HitTest(Pixels(point)) is { } hit && !Related(hit, node))
            throw new InvalidOperationException(
                $"Input at {point} on {Describe(node)} would land on {Describe(hit)}, which is neither it nor its ancestor or descendant."
            );
        return point;
    }
}

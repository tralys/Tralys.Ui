using System.Numerics;

namespace Tralys.Ui.Testing;

public sealed partial class UiDriver
{
    /// <summary>Presses at the centre of <paramref name="from"/>'s visible bounds, moves by <paramref name="by"/> in 4
    /// equal steps, then releases.</summary>
    public void Drag(
        Node from,
        Vector2 by,
        PointerKind kind = PointerKind.Mouse,
        bool settle = true
    )
    {
        var start = Aim(from);
        var pointer = Contact(kind);
        if (kind != PointerKind.Touch)
            View.PointerMove(pointer, Pixels(start), MouseButtons.None, false, default, _time);
        View.PointerDown(pointer, Pixels(start), MouseButton.Left, default, _time);
        for (var step = 1; step <= 4; step++)
            View.PointerMove(
                pointer,
                Pixels(start + by * (step / 4f)),
                MouseButtons.Left,
                true,
                default,
                _time
            );
        View.PointerUp(pointer, Pixels(start + by), MouseButton.Left, default, _time);
        if (settle)
            Settle();
    }
}

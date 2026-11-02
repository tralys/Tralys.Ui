using System.Numerics;

namespace Tralys.Ui;

/// <summary>Where a press that may become a drag stops being a click.</summary>
public static class DragThreshold
{
    /// <summary>Whether a press by a pointer of <paramref name="kind"/> that has moved by <paramref name="offset"/>,
    /// in device-independent units, is a drag: a mouse at <see cref="GestureSlop.PointerDrag"/> or more, a touch or
    /// pen past <see cref="GestureSlop.Tap"/>.</summary>
    public static bool IsCrossed(PointerKind kind, Vector2 offset) =>
        kind == PointerKind.Mouse
            ? offset.LengthSquared() >= GestureSlop.PointerDrag * GestureSlop.PointerDrag
            : offset.LengthSquared() > GestureSlop.Tap * GestureSlop.Tap;
}

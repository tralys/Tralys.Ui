using System.Numerics;

namespace Tralys.Ui;

[Flags]
enum PopupEdge : byte
{
    None = 0,
    Top = 1,
    Bottom = 2,
    Left = 4,
    Right = 8,
    Vertical = Top | Bottom,
    Horizontal = Left | Right,
}

[FramePath]
static class PopupPositioner
{
    public static (PopupEdge Anchor, PopupEdge Gravity, PopupEdge FlipAxis) Rules(
        PlacementMode mode
    ) =>
        mode switch
        {
            PlacementMode.Bottom => (PopupEdge.Bottom, PopupEdge.Bottom, PopupEdge.Vertical),
            PlacementMode.BottomStart => (
                PopupEdge.Bottom | PopupEdge.Left,
                PopupEdge.Bottom | PopupEdge.Right,
                PopupEdge.Vertical
            ),
            PlacementMode.BottomEnd => (
                PopupEdge.Bottom | PopupEdge.Right,
                PopupEdge.Bottom | PopupEdge.Left,
                PopupEdge.Vertical
            ),
            PlacementMode.Top => (PopupEdge.Top, PopupEdge.Top, PopupEdge.Vertical),
            PlacementMode.TopStart => (
                PopupEdge.Top | PopupEdge.Left,
                PopupEdge.Top | PopupEdge.Right,
                PopupEdge.Vertical
            ),
            PlacementMode.TopEnd => (
                PopupEdge.Top | PopupEdge.Right,
                PopupEdge.Top | PopupEdge.Left,
                PopupEdge.Vertical
            ),
            PlacementMode.Left => (PopupEdge.Left, PopupEdge.Left, PopupEdge.Horizontal),
            PlacementMode.LeftStart => (
                PopupEdge.Top | PopupEdge.Left,
                PopupEdge.Bottom | PopupEdge.Left,
                PopupEdge.Horizontal
            ),
            PlacementMode.LeftEnd => (
                PopupEdge.Bottom | PopupEdge.Left,
                PopupEdge.Top | PopupEdge.Left,
                PopupEdge.Horizontal
            ),
            PlacementMode.Right => (PopupEdge.Right, PopupEdge.Right, PopupEdge.Horizontal),
            PlacementMode.RightStart => (
                PopupEdge.Top | PopupEdge.Right,
                PopupEdge.Bottom | PopupEdge.Right,
                PopupEdge.Horizontal
            ),
            PlacementMode.RightEnd => (
                PopupEdge.Bottom | PopupEdge.Right,
                PopupEdge.Top | PopupEdge.Right,
                PopupEdge.Horizontal
            ),
            PlacementMode.Pointer => (
                PopupEdge.Top | PopupEdge.Left,
                PopupEdge.Bottom | PopupEdge.Right,
                PopupEdge.Vertical
            ),
            _ => (
                PopupEdge.Top | PopupEdge.Left,
                PopupEdge.Bottom | PopupEdge.Right,
                PopupEdge.None
            ),
        };

    public static Rect Place(
        Size size,
        Rect anchorRect,
        PopupEdge anchor,
        PopupEdge gravity,
        PopupEdge flipAxis,
        Vector2 offset,
        Rect bounds
    )
    {
        var placed = Unconstrained(size, anchorRect, anchor, gravity, offset);
        var flipped = Unconstrained(
            size,
            anchorRect,
            Flip(anchor, flipAxis),
            Flip(gravity, flipAxis),
            offset
        );
        var width = size.Width;
        var height = size.Height;
        Constrain(ref placed.X, ref width, flipped.X, height, bounds.X, bounds.Right);
        Constrain(ref placed.Y, ref height, flipped.Y, width, bounds.Y, bounds.Bottom);
        return new Rect(placed.X, placed.Y, width, height);
    }

    static void Constrain(
        ref float position,
        ref float length,
        float flipped,
        float crossLength,
        float start,
        float end
    )
    {
        if (
            (position < start || position + length > end)
            && !(flipped < start || flipped + length > end)
        )
            position = flipped;

        position = MathF.Max(position, start);
        if (position + length > end)
            position = end - length;

        var resized = MathF.Max(position, start);
        var fitted = resized + length > end ? end - resized : length;
        if (fitted > 0f && crossLength > 0f)
        {
            position = resized;
            length = fitted;
        }
    }

    static Vector2 Unconstrained(
        Size size,
        Rect anchorRect,
        PopupEdge anchor,
        PopupEdge gravity,
        Vector2 offset
    ) => AnchorPoint(anchorRect, anchor) + Gravitate(size, gravity) + offset;

    static Vector2 AnchorPoint(Rect rect, PopupEdge edge)
    {
        var x =
            (edge & PopupEdge.Left) != 0 ? rect.X
            : (edge & PopupEdge.Right) != 0 ? rect.Right
            : rect.X + rect.Width / 2f;
        var y =
            (edge & PopupEdge.Top) != 0 ? rect.Y
            : (edge & PopupEdge.Bottom) != 0 ? rect.Bottom
            : rect.Y + rect.Height / 2f;
        return new Vector2(x, y);
    }

    static Vector2 Gravitate(Size size, PopupEdge gravity)
    {
        var x =
            (gravity & PopupEdge.Left) != 0 ? -size.Width
            : (gravity & PopupEdge.Right) != 0 ? 0f
            : -size.Width / 2f;
        var y =
            (gravity & PopupEdge.Top) != 0 ? -size.Height
            : (gravity & PopupEdge.Bottom) != 0 ? 0f
            : -size.Height / 2f;
        return new Vector2(x, y);
    }

    static PopupEdge Flip(PopupEdge edge, PopupEdge axis) =>
        (edge & axis) != 0 ? edge ^ axis : edge;
}

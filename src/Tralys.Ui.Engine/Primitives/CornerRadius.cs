namespace Tralys.Ui;

/// <summary>A radius for each corner of a rectangle, clockwise from the top left.</summary>
public readonly record struct CornerRadius(
    float TopLeft,
    float TopRight,
    float BottomRight,
    float BottomLeft
)
{
    public CornerRadius(float uniform)
        : this(uniform, uniform, uniform, uniform) { }

    public static implicit operator CornerRadius(float uniform) => new(uniform);

    internal CornerRadius Outer(Thickness border) =>
        new(
            TopLeft > 0f ? TopLeft + MathF.Max(border.Left, border.Top) * 0.5f : 0f,
            TopRight > 0f ? TopRight + MathF.Max(border.Right, border.Top) * 0.5f : 0f,
            BottomRight > 0f ? BottomRight + MathF.Max(border.Right, border.Bottom) * 0.5f : 0f,
            BottomLeft > 0f ? BottomLeft + MathF.Max(border.Left, border.Bottom) * 0.5f : 0f
        );

    internal CornerRadius Deflate(Thickness border) =>
        new(
            MathF.Max(0f, TopLeft - MathF.Max(border.Left, border.Top)),
            MathF.Max(0f, TopRight - MathF.Max(border.Right, border.Top)),
            MathF.Max(0f, BottomRight - MathF.Max(border.Right, border.Bottom)),
            MathF.Max(0f, BottomLeft - MathF.Max(border.Left, border.Bottom))
        );
}

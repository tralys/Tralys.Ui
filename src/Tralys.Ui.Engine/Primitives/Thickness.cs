namespace Tralys.Ui;

/// <summary>A width for each side of a rectangle.</summary>
public readonly record struct Thickness(float Left, float Top, float Right, float Bottom)
{
    public Thickness(float uniform)
        : this(uniform, uniform, uniform, uniform) { }

    public static implicit operator Thickness(float uniform) => new(uniform);

    public bool IsZero => Left == 0f && Top == 0f && Right == 0f && Bottom == 0f;

    internal static bool IsValid(Thickness value) =>
        FloatUtil.IsFiniteNonNegative(value.Left)
        && FloatUtil.IsFiniteNonNegative(value.Top)
        && FloatUtil.IsFiniteNonNegative(value.Right)
        && FloatUtil.IsFiniteNonNegative(value.Bottom);
}

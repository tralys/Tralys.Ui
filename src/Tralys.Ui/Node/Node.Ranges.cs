using System.Numerics;

namespace Tralys.Ui;

public sealed partial class Node
{
    static void InRange(ref LayoutStyle next, in LayoutStyle held)
    {
        next.RowGap = NotNegative(next.RowGap, held.RowGap);
        next.ColumnGap = NotNegative(next.ColumnGap, held.ColumnGap);
        next.Padding = NotNegative(next.Padding, held.Padding);
        next.Border = NotNegative(next.Border, held.Border);
        next.Width = Size(next.Width, held.Width);
        next.Height = Size(next.Height, held.Height);
        next.MinWidth = Size(next.MinWidth, held.MinWidth);
        next.MinHeight = Size(next.MinHeight, held.MinHeight);
        next.MaxWidth = Size(next.MaxWidth, held.MaxWidth);
        next.MaxHeight = Size(next.MaxHeight, held.MaxHeight);
        next.AspectRatio = NotNegative(next.AspectRatio, held.AspectRatio);
    }

    static void InRange(ref VisualStyle next, in VisualStyle held)
    {
        if (next.Opacity is { } opacity)
            next.Opacity = float.IsFinite(opacity) ? Math.Clamp(opacity, 0f, 1f) : held.Opacity;
        next.Radius = new CornerRadius(
            NotNegative(next.Radius.TopLeft, held.Radius.TopLeft),
            NotNegative(next.Radius.TopRight, held.Radius.TopRight),
            NotNegative(next.Radius.BottomRight, held.Radius.BottomRight),
            NotNegative(next.Radius.BottomLeft, held.Radius.BottomLeft)
        );
        if (!Finite(next.Transform.Matrix))
            next.Transform = held.Transform;
        if (
            next.TransformOrigin is { } origin
            && !(float.IsFinite(origin.X) && float.IsFinite(origin.Y))
        )
            next.TransformOrigin = held.TransformOrigin;
        var shadow = next.Shadow;
        if (
            !float.IsFinite(shadow.OffsetX)
            || !float.IsFinite(shadow.OffsetY)
            || !float.IsFinite(shadow.Blur)
            || !float.IsFinite(shadow.Spread)
        )
            next.Shadow = held.Shadow;
        else if (shadow.Blur < 0f)
            next.Shadow = shadow with { Blur = 0f };
    }

    static void InRange(ref InheritStyle next, in InheritStyle held)
    {
        next.FontSize = NotNegative(next.FontSize, held.FontSize);
        next.LineHeight = NotNegative(next.LineHeight, held.LineHeight);
        if (next.LetterSpacing is { } spacing && !float.IsFinite(spacing))
            next.LetterSpacing = held.LetterSpacing;
    }

    static void InRange(ref Placement next, in Placement held) =>
        next = next with
        {
            Grow = NotNegative(next.Grow, held.Grow),
            Shrink = next.Shrink is { } shrink ? NotNegative(shrink, held.Shrink ?? 1f) : null,
            Basis = Size(next.Basis, held.Basis),
            Margin = Finite(next.Margin, held.Margin),
            Left = next.Left.IsFinite ? next.Left : held.Left,
            Top = next.Top.IsFinite ? next.Top : held.Top,
            Right = next.Right.IsFinite ? next.Right : held.Right,
            Bottom = next.Bottom.IsFinite ? next.Bottom : held.Bottom,
        };

    static float NotNegative(float value, float held) =>
        float.IsFinite(value) ? MathF.Max(0f, value) : held;

    static Thickness NotNegative(Thickness value, Thickness held) =>
        new(
            NotNegative(value.Left, held.Left),
            NotNegative(value.Top, held.Top),
            NotNegative(value.Right, held.Right),
            NotNegative(value.Bottom, held.Bottom)
        );

    static Thickness Finite(Thickness value, Thickness held) =>
        new(
            float.IsFinite(value.Left) ? value.Left : held.Left,
            float.IsFinite(value.Top) ? value.Top : held.Top,
            float.IsFinite(value.Right) ? value.Right : held.Right,
            float.IsFinite(value.Bottom) ? value.Bottom : held.Bottom
        );

    static Length Size(Length value, Length held) => value.IsFinite ? value.NotNegative : held;

    internal static bool Finite(in Matrix3x2 m) =>
        float.IsFinite(m.M11)
        && float.IsFinite(m.M12)
        && float.IsFinite(m.M21)
        && float.IsFinite(m.M22)
        && float.IsFinite(m.M31)
        && float.IsFinite(m.M32);
}

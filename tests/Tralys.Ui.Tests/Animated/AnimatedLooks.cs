using System.Globalization;

namespace Tralys.Ui.Tests;

internal static class AnimatedLooks
{
    /// <summary>A colour as a draw trace writes it.</summary>
    public static string Hex(Color color) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"#{color.R:x2}{color.G:x2}{color.B:x2}{(color.A == 255 ? "" : color.A.ToString("x2", CultureInfo.InvariantCulture))}"
        );

    /// <summary>A draw trace line without the path of the node drawing it.</summary>
    public static string Primitive(string line) =>
        line[(line.IndexOf(": ", StringComparison.Ordinal) + 2)..];

    /// <summary>The clockwise turn <paramref name="node"/>'s transform makes, in degrees from 0 up to 360.</summary>
    public static float Turn(Node node)
    {
        var m = node.Visual.Transform.Matrix;
        var degrees = MathF.Atan2(m.M12, m.M11) * 180f / MathF.PI;
        return degrees < 0f ? degrees + 360f : degrees;
    }

    /// <summary>How far <paramref name="node"/>'s transform moves it sideways.</summary>
    public static float Shift(Node node) => node.Visual.Transform.Matrix.Translation.X;

    /// <summary>The root a component node lays out.</summary>
    public static Node Root(Node component)
    {
        foreach (var child in component.Children)
            return child;
        throw new InvalidOperationException("The component rendered nothing.");
    }
}

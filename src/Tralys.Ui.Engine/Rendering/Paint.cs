using System.Numerics;

namespace Tralys.Ui.Rendering;

public enum PaintKind : byte
{
    None,
    Solid,
    LinearGradient,
    RadialGradient,
    Image,
}

/// <summary>How a gradient's points are measured.</summary>
public enum GradientSpace : byte
{
    /// <summary>0 to 1 across the bounds of the primitive being painted.</summary>
    RelativeToBounds,

    /// <summary>The primitive's local coordinates.</summary>
    Absolute,
}

/// <summary>What fills a primitive: nothing, one colour, a gradient over a ramp of stops, or an image.</summary>
[FramePath]
public readonly struct Paint : IEquatable<Paint>
{
    Paint(
        PaintKind kind,
        Color color,
        GradientStops? stops,
        Vector2 start,
        Vector2 end,
        GradientSpace space,
        float opacity,
        PaintImage? image = null
    )
    {
        Kind = kind;
        Color = color;
        Stops = stops;
        Start = start;
        End = end;
        Space = space;
        Opacity = opacity;
        Image = image;
    }

    public PaintKind Kind { get; }

    /// <summary>A solid paint's colour.</summary>
    public Color Color { get; }

    public GradientStops? Stops { get; }

    /// <summary>A linear gradient's start, or a radial gradient's centre; the top left of the bounds an
    /// <see cref="GradientSpace.Absolute"/> image paint lays its image over.</summary>
    public Vector2 Start { get; }

    /// <summary>A linear gradient's end, or a radial gradient's radius on each axis; the size of the bounds an
    /// <see cref="GradientSpace.Absolute"/> image paint lays its image over.</summary>
    public Vector2 End { get; }

    /// <summary>Whether the paint's points are fractions of the painted bounds or local coordinates; an image paint
    /// lays its image over the painted bounds, or over the bounds its own points give.</summary>
    public GradientSpace Space { get; }

    public float Opacity { get; }

    /// <summary>An image paint's image.</summary>
    public PaintImage? Image { get; }

    public bool IsVisible =>
        Opacity > 0f
        && Kind switch
        {
            PaintKind.None => false,
            PaintKind.Solid => Color.A > 0,
            _ => true,
        };

    public static Paint Solid(Color color, float opacity = 1f) =>
        new(PaintKind.Solid, color, null, default, default, default, opacity);

    public static Paint Linear(
        GradientStops stops,
        Vector2 start,
        Vector2 end,
        GradientSpace space = GradientSpace.RelativeToBounds,
        float opacity = 1f
    )
    {
        ArgumentNullException.ThrowIfNull(stops);
        return new(PaintKind.LinearGradient, default, stops, start, end, space, opacity);
    }

    public static Paint Radial(
        GradientStops stops,
        Vector2 center,
        Vector2 radius,
        GradientSpace space = GradientSpace.RelativeToBounds,
        float opacity = 1f
    )
    {
        ArgumentNullException.ThrowIfNull(stops);
        return new(PaintKind.RadialGradient, default, stops, center, radius, space, opacity);
    }

    /// <summary><paramref name="image"/> laid over the painted bounds.</summary>
    public static Paint FromImage(PaintImage image, float opacity = 1f)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new(PaintKind.Image, default, null, default, default, default, opacity, image);
    }

    public static implicit operator Paint(Color color) => Solid(color);

    /// <summary>Whether both paint alike: the same kind, colour, points, space and opacity, and the same stops and
    /// image instances.</summary>
    public bool Equals(Paint other) =>
        Kind == other.Kind
        && Color == other.Color
        && ReferenceEquals(Stops, other.Stops)
        && Start == other.Start
        && End == other.End
        && Space == other.Space
        && Opacity.Equals(other.Opacity)
        && ReferenceEquals(Image, other.Image);

    public override bool Equals(object? obj) => obj is Paint other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Kind, Color, Stops, Start, End, Space, Opacity, Image);

    public static bool operator ==(Paint left, Paint right) => left.Equals(right);

    public static bool operator !=(Paint left, Paint right) => !left.Equals(right);

    internal bool IsOpaque => Kind == PaintKind.Solid && Color.A == 255 && Opacity >= 1f;

    internal Paint PinnedTo(Rect bounds)
    {
        if (Space == GradientSpace.Absolute || Kind is PaintKind.None or PaintKind.Solid)
            return this;

        var origin = new Vector2(bounds.X, bounds.Y);
        var size = new Vector2(bounds.Width, bounds.Height);
        if (Kind == PaintKind.Image)
            return new(Kind, default, null, origin, size, GradientSpace.Absolute, Opacity, Image);
        return Kind == PaintKind.LinearGradient
            ? Linear(
                Stops!,
                origin + Start * size,
                origin + End * size,
                GradientSpace.Absolute,
                Opacity
            )
            : Radial(Stops!, origin + Start * size, End * size, GradientSpace.Absolute, Opacity);
    }
}

using System.Numerics;

namespace Tralys.Ui;

/// <summary>An axis-aligned rectangle; <see cref="Y"/> grows downward.</summary>
public readonly record struct Rect(float X, float Y, float Width, float Height)
{
    /// <summary>A rectangle of <paramref name="size"/> at the origin.</summary>
    public Rect(Size size)
        : this(0f, 0f, size.Width, size.Height) { }

    public float Right => X + Width;

    public float Bottom => Y + Height;

    public Size Size => new(Width, Height);

    /// <summary>True when the rectangle covers no area, including when a side is NaN.</summary>
    public bool IsEmpty => !(Width > 0f && Height > 0f);

    public static Rect FromEdges(float left, float top, float right, float bottom) =>
        new(left, top, right - left, bottom - top);

    /// <summary>Whether <paramref name="point"/> lies in the rectangle. Its right and bottom edges belong to what lies
    /// beyond, so neighbours never both claim a point, and a rectangle with no area holds none.</summary>
    [FramePath]
    public bool Contains(Vector2 point) =>
        point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;

    /// <summary>The smallest axis-aligned rectangle holding this one under <paramref name="transform"/>.</summary>
    [FramePath]
    public Rect TransformedBy(in Matrix3x2 transform)
    {
        var a = Vector2.Transform(new Vector2(X, Y), transform);
        var b = Vector2.Transform(new Vector2(Right, Y), transform);
        var c = Vector2.Transform(new Vector2(X, Bottom), transform);
        var d = Vector2.Transform(new Vector2(Right, Bottom), transform);
        var min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
        var max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
        return FromEdges(min.X, min.Y, max.X, max.Y);
    }

    /// <summary>The overlap of both rectangles; an empty rectangle when they do not overlap.</summary>
    public Rect Intersect(Rect other)
    {
        var left = MathF.Max(X, other.X);
        var top = MathF.Max(Y, other.Y);
        var right = MathF.Min(Right, other.Right);
        var bottom = MathF.Min(Bottom, other.Bottom);
        return right > left && bottom > top
            ? FromEdges(left, top, right, bottom)
            : new Rect(left, top, 0f, 0f);
    }

    /// <summary>The smallest rectangle holding both.</summary>
    public Rect Union(Rect other) =>
        FromEdges(
            MathF.Min(X, other.X),
            MathF.Min(Y, other.Y),
            MathF.Max(Right, other.Right),
            MathF.Max(Bottom, other.Bottom)
        );

    /// <summary>The rectangle moved in by <paramref name="thickness"/> on each side, never below zero size.</summary>
    public Rect Deflate(Thickness thickness) =>
        new(
            X + thickness.Left,
            Y + thickness.Top,
            MathF.Max(0f, Width - thickness.Left - thickness.Right),
            MathF.Max(0f, Height - thickness.Top - thickness.Bottom)
        );

    /// <summary>The rectangle grown by <paramref name="dx"/> on the left and right and <paramref name="dy"/> on the top and bottom.</summary>
    public Rect Inflate(float dx, float dy) =>
        new(X - dx, Y - dy, Width + dx * 2f, Height + dy * 2f);
}

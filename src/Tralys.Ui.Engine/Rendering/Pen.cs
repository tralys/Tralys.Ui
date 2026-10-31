namespace Tralys.Ui.Rendering;

public enum PenLineJoin : byte
{
    /// <summary>Sharp corners, cut to a bevel where the miter would reach past <see cref="Pen.MiterLimit"/> half
    /// thicknesses.</summary>
    Miter,
    Bevel,
    Round,
}

public enum PenLineCap : byte
{
    /// <summary>Ends exactly at the end point.</summary>
    Flat,

    /// <summary>A half-thickness square past the end point.</summary>
    Square,
    Round,

    /// <summary>A point half a thickness past the end point.</summary>
    Triangle,
}

/// <summary>How a path's outline is stroked: its paint and thickness in local units, its joins and its end caps, and
/// optionally a dash pattern. The default pen draws nothing.</summary>
public readonly record struct Pen(
    Paint Paint,
    float Thickness,
    PenLineJoin LineJoin = PenLineJoin.Miter,
    PenLineCap StartLineCap = PenLineCap.Flat,
    PenLineCap EndLineCap = PenLineCap.Flat,
    float MiterLimit = 10f
)
{
    public bool IsVisible => Thickness > 0f && Paint.IsVisible;

    /// <summary>The dashes the outline is cut into, or null for a solid stroke. Each figure starts the pattern
    /// again.</summary>
    public DashArray? DashArray { get; init; }

    /// <summary>How far into <see cref="DashArray"/>, in stroke thicknesses, each figure starts it.</summary>
    public float DashOffset { get; init; }

    /// <summary>The caps on each dash's ends, except where a dash starts or ends an open figure, which take
    /// <see cref="StartLineCap"/> and <see cref="EndLineCap"/>.</summary>
    public PenLineCap DashCap { get; init; }

    /// <summary>Whether the stroke is cut into dashes: <see cref="DashArray"/> is set and its lengths sum to more
    /// than zero.</summary>
    public bool IsDashed => DashArray is { Period: > 0f };
}

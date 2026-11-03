namespace Tralys.Ui;

/// <summary>Where a node sits in its parent. On a component's root node it belongs to the component's caller.</summary>
[FramePath]
public readonly record struct Placement
{
    public float Grow { get; init; }

    /// <summary>How much it gives up when its line overflows; null is 1.</summary>
    public float? Shrink { get; init; }

    public Length Basis { get; init; }

    public AlignSelf AlignSelf { get; init; }

    public AlignSelf JustifySelf { get; init; }

    public Thickness Margin { get; init; }

    public Position Position { get; init; }

    public Length Left { get; init; }

    public Length Top { get; init; }

    public Length Right { get; init; }

    public Length Bottom { get; init; }

    public int ZIndex { get; init; }

    /// <summary>The grid column, from 1; 0 places it automatically.</summary>
    public int Column { get; init; }

    /// <summary>The grid row, from 1; 0 places it automatically.</summary>
    public int Row { get; init; }

    /// <summary>How many grid columns it spans; 0 is 1.</summary>
    public int ColumnSpan { get; init; }

    /// <summary>How many grid rows it spans; 0 is 1.</summary>
    public int RowSpan { get; init; }

    /// <summary>Keeps its layout space, drawing and hitting nothing.</summary>
    public bool Hidden { get; init; }

    /// <summary>Takes no layout space, and draws and hits nothing.</summary>
    public bool Collapsed { get; init; }

    public string? Id { get; init; }

    internal bool Same(in Placement other) =>
        LaysOutLike(other)
        && PaintsLike(other)
        && string.Equals(Id, other.Id, StringComparison.Ordinal);

    internal bool LaysOutLike(in Placement other) =>
        Grow.Equals(other.Grow)
        && Nullable.Equals(Shrink, other.Shrink)
        && Basis == other.Basis
        && AlignSelf == other.AlignSelf
        && JustifySelf == other.JustifySelf
        && Margin == other.Margin
        && Position == other.Position
        && Left == other.Left
        && Top == other.Top
        && Right == other.Right
        && Bottom == other.Bottom
        && Column == other.Column
        && Row == other.Row
        && ColumnSpan == other.ColumnSpan
        && RowSpan == other.RowSpan
        && Collapsed == other.Collapsed;

    internal bool PaintsLike(in Placement other) =>
        ZIndex == other.ZIndex && Hidden == other.Hidden;
}

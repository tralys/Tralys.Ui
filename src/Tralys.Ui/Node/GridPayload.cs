namespace Tralys.Ui;

[FramePath]
internal record struct GridStyle
{
    public GridTracks Columns;
    public GridTracks Rows;
    public Align JustifyItems;
    public int FirstColumn;

    /// <summary>What <c>Equals</c> says, without copying either style.</summary>
    public readonly bool Same(in GridStyle other) =>
        Columns.Same(other.Columns)
        && Rows.Same(other.Rows)
        && JustifyItems == other.JustifyItems
        && FirstColumn == other.FirstColumn;
}

/// <summary>A grid's tracks and item alignment, kept out of the layout style every node carries.</summary>
[FramePath]
internal sealed class GridPayload : NodePayload
{
    static readonly GridStyle None = default;

    public GridStyle Style;

    public static GridPayload Rent() => Pool<GridPayload>.TryRent() ?? new GridPayload();

    public static ref readonly GridStyle Of(Node grid) =>
        ref grid.Payload is GridPayload payload ? ref payload.Style : ref None;

    public bool Write(UiView view, Node node, in GridStyle style)
    {
        if (Style.Same(style))
            return false;
        Style = style;
        view.InvalidateMeasure(node);
        return true;
    }

    public override void Release(UiView view)
    {
        Style = default;
        Pool<GridPayload>.Return(this);
    }
}

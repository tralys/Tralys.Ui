namespace Tralys.Ui.Gallery;

public readonly record struct FittedGridProps
{
    /// <summary>The most columns, up to the number of children.</summary>
    public int MaxColumns { get; init; }

    public Length Width { get; init; }
}

/// <summary>A sample of a component an app builds on the core: a custom layout of equal cells a spacing step apart, in
/// the most columns, up to <see cref="FittedGridProps.MaxColumns"/>, whose cells still hold its widest child at its
/// widest, and one when none do; each row stands as tall as its own tallest cell. It measures its children and fits
/// its columns in the same layout pass.</summary>
[Children]
public sealed class FittedGrid : Component<FittedGridProps>
{
    protected override void Render(Ui ui, in FittedGridProps p)
    {
        using (
            ui.Layout(
                algorithm: FittedColumns.Instance,
                state: new Fitting(p.MaxColumns, ui.Theme.Spacing),
                width: p.Width
            )
        )
            ui.Children();
    }
}

readonly record struct Fitting(int MaxColumns, float Gap);

[FramePath]
sealed class FittedColumns : LayoutAlgorithm<Fitting>
{
    public static readonly FittedColumns Instance = new();

    public override Size Measure(
        ref LayoutContext cx,
        Fitting state,
        AvailableSpace width,
        AvailableSpace height
    )
    {
        var pad = cx.Padding;
        var across = pad.Left + pad.Right;
        var down = pad.Top + pad.Bottom;
        var count = cx.ChildCount;
        if (count == 0)
            return new Size(across, down);
        var room = MathF.Max(0f, width.Value - across);
        var columns = Columns(ref cx, state, room);
        var cell = width.IsDefinite
            ? AvailableSpace.Definite(Cell(room, columns, state.Gap))
            : width;
        var widest = 0f;
        var rows = 0f;
        for (var start = 0; start < count; start += columns)
        {
            var tallest = 0f;
            for (var i = start; i < Math.Min(start + columns, count); i++)
            {
                var size = cx.MeasureChild(i, cell, AvailableSpace.MaxContent);
                widest = MathF.Max(widest, size.Width);
                tallest = MathF.Max(tallest, size.Height);
            }
            rows += tallest + (start > 0 ? state.Gap : 0f);
        }
        return new Size(widest * columns + state.Gap * (columns - 1) + across, rows + down);
    }

    public override void Arrange(ref LayoutContext cx, Fitting state, Size size)
    {
        var pad = cx.Padding;
        var count = cx.ChildCount;
        var room = MathF.Max(0f, size.Width - pad.Left - pad.Right);
        var columns = Columns(ref cx, state, room);
        var cell = Cell(room, columns, state.Gap);
        var top = pad.Top;
        for (var start = 0; start < count; start += columns)
        {
            var end = Math.Min(start + columns, count);
            var tallest = 0f;
            for (var i = start; i < end; i++)
                tallest = MathF.Max(
                    tallest,
                    cx.MeasureChild(
                        i,
                        AvailableSpace.Definite(cell),
                        AvailableSpace.MaxContent
                    ).Height
                );
            for (var i = start; i < end; i++)
                cx.PlaceChild(
                    i,
                    new Rect(pad.Left + (i - start) * (cell + state.Gap), top, cell, tallest)
                );
            top += tallest + state.Gap;
        }
    }

    static int Columns(ref LayoutContext cx, Fitting state, float room)
    {
        var widest = 0f;
        for (var i = 0; i < cx.ChildCount; i++)
            widest = MathF.Max(
                widest,
                cx.MeasureChild(i, AvailableSpace.MaxContent, AvailableSpace.MaxContent).Width
            );
        for (var columns = Math.Min(state.MaxColumns, cx.ChildCount); columns > 1; columns--)
        {
            if (Cell(room, columns, state.Gap) >= widest)
                return columns;
        }
        return 1;
    }

    static float Cell(float room, int columns, float gap) =>
        MathF.Max(0f, (room - gap * (columns - 1)) / columns);
}

using System.Numerics;

namespace Tralys.Ui;

public sealed partial class UiView
{
    Node? _gapsOf;
    List<Rect>? _gaps;

    internal void TraceGaps(Node? container, List<Rect> into)
    {
        into.Clear();
        if (container is not null)
        {
            _gapsOf = container;
            _gaps = into;
            InvalidateArrange(container);
        }
        else if (ReferenceEquals(_gaps, into))
        {
            _gapsOf = null;
            _gaps = null;
        }
    }

    List<Rect>? GapsOf(Node container)
    {
        if (container != _gapsOf)
            return null;
        _gaps!.Clear();
        return _gaps;
    }

    void Gap(List<Rect> gaps, Rect content, in Matrix3x2 toView) =>
        gaps.Add(Snap(content.TransformedBy(toView)));

    void GridGaps(
        List<Rect> gaps,
        Vector2 origin,
        int colStart,
        int columns,
        int rowStart,
        int rows,
        float columnGap,
        float rowGap,
        float usedW,
        in Matrix3x2 toView
    )
    {
        var y = origin.Y;
        for (var r = 0; r < rows; r++)
        {
            var height = _tracks[rowStart + r].Size;
            if (columnGap > 0f)
            {
                var x = origin.X;
                for (var c = 0; c + 1 < columns; c++)
                {
                    x += _tracks[colStart + c].Size;
                    Gap(gaps, new Rect(x, y, columnGap, height), toView);
                    x += columnGap;
                }
            }
            y += height;
            if (rowGap > 0f && r + 1 < rows)
                Gap(gaps, new Rect(origin.X, y, usedW, rowGap), toView);
            y += rowGap;
        }
    }
}

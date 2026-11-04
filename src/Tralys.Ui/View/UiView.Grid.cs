using System.Numerics;

namespace Tralys.Ui;

public sealed partial class UiView
{
    ValueList<GridItem> _gridItems;
    ValueList<GridTrack> _tracks;
    ValueList<bool> _occupied;

    Size GridLayout(Node grid, Avail width, Avail height, bool perform)
    {
        ref readonly var style = ref grid.Layout;
        var tracks = GridPayload.Of(grid);
        var pad = style.Padding;
        var border = style.Border;
        var pbW = pad.Left + pad.Right + border.Left + border.Right;
        var pbH = pad.Top + pad.Bottom + border.Top + border.Bottom;
        var innerW = width.IsExact ? MathF.Max(0f, width.Value - pbW) : float.NaN;
        var innerH = height.IsExact ? MathF.Max(0f, height.Value - pbH) : float.NaN;
        var oneCell = tracks.Columns.Count == 0 && tracks.Rows.Count == 0;
        var columns = Math.Max(1, tracks.Columns.Count);

        var childStart = _layoutChildren.Count;
        CollectChildren(grid, ref _layoutChildren);
        var childEnd = _layoutChildren.Count;
        var itemStart = _gridItems.Count;
        for (var c = childStart; c < childEnd; c++)
        {
            var child = _layoutChildren[c];
            if (child.Place.Position == Position.Absolute)
                continue;
            _gridItems.Add(new GridItem { Node = child });
        }
        var count = _gridItems.Count - itemStart;
        var rows = oneCell ? OneCell(itemStart, count) : Place(tracks, itemStart, count, columns);

        var colStart = _tracks.Count;
        for (var t = 0; t < columns; t++)
            _tracks.Add(
                GridTrack.From(t < tracks.Columns.Count ? tracks.Columns[t] : Track.Auto, innerW)
            );
        var rowStart = _tracks.Count;
        for (var t = 0; t < rows; t++)
            _tracks.Add(
                GridTrack.From(t < tracks.Rows.Count ? tracks.Rows[t] : Track.Auto, innerH)
            );

        var cells = new Cells(colStart, style.ColumnGap, tracks.JustifyItems);
        SizeAxis(
            itemStart,
            count,
            colStart,
            columns,
            tracks.Columns,
            width,
            pbW,
            style.ColumnGap,
            horizontal: true,
            cells
        );
        SizeAxis(
            itemStart,
            count,
            rowStart,
            rows,
            tracks.Rows,
            height,
            pbH,
            style.RowGap,
            horizontal: false,
            cells
        );

        var usedW = Sum(colStart, columns, style.ColumnGap);
        var usedH = Sum(rowStart, rows, style.RowGap);
        var result = new Size(
            width.IsExact ? width.Value
                : width.Kind == AvailKind.AtMost ? MathF.Min(width.Value, usedW + pbW)
                : usedW + pbW,
            height.IsExact ? height.Value
                : height.Kind == AvailKind.AtMost ? MathF.Min(height.Value, usedH + pbH)
                : usedH + pbH
        );

        if (perform)
        {
            var origin = new Vector2(border.Left + pad.Left, border.Top + pad.Top);
            var toView = ContentToView(grid);
            var snappedToView = ContentSnappedToView(grid);
            for (var i = 0; i < count; i++)
            {
                var item = _gridItems[itemStart + i];
                var child = item.Node;
                var x = origin.X + Offset(colStart, item.Column, style.ColumnGap);
                var y = origin.Y + Offset(rowStart, item.Row, style.RowGap);
                var cellW = Span(colStart, item.Column, item.ColumnSpan, style.ColumnGap);
                var cellH = Span(rowStart, item.Row, item.RowSpan, style.RowGap);
                var m = child.Place.Margin;
                var justify = Effective(child.Place.JustifySelf, tracks.JustifyItems);
                var align = Effective(child.Place.AlignSelf, style.Align);
                var roomW = cellW - m.Left - m.Right;
                var roomH = cellH - m.Top - m.Bottom;
                child.PctBasis = new Vector2(cellW, cellH);
                var cellHeight =
                    align == Align.Stretch
                        ? Stretched(child, row: true, roomH)
                        : Avail.AtMost(roomH);
                var w = Measure(child, CellWidth(child, justify, roomW), cellHeight).Width;
                var h = Measure(child, Avail.Exact(w), cellHeight).Height;
                x += m.Left;
                y += m.Top;
                x += Aligned(justify, toView, horizontal: true, x, roomW, w);
                y += Aligned(align, toView, horizontal: false, y, roomH, h);
                Arrange(child, new Rect(x, y, w, h), toView, snappedToView);
            }
            if (GapsOf(grid) is { } gaps)
                GridGaps(
                    gaps,
                    origin,
                    colStart,
                    columns,
                    rowStart,
                    rows,
                    style.ColumnGap,
                    style.RowGap,
                    usedW,
                    toView
                );

            var frame = grid.QuarterTurned
                ? new Size(grid.Frame.Height, grid.Frame.Width)
                : grid.Frame.Size;
            var paddingBox = Rect.FromEdges(
                border.Left,
                border.Top,
                frame.Width - border.Right,
                frame.Height - border.Bottom
            );
            for (var c = childStart; c < childEnd; c++)
            {
                var child = _layoutChildren[c];
                if (child.Place.Position == Position.Absolute)
                    PlaceAbsolute(grid, child, paddingBox, pad);
            }
            grid.ContentExtent = Extent(grid, childStart, childEnd, pad);
        }

        _tracks.Truncate(colStart);
        _gridItems.Truncate(itemStart);
        _layoutChildren.Truncate(childStart);
        return result;
    }

    int OneCell(int itemStart, int count)
    {
        for (var i = 0; i < count; i++)
        {
            ref var item = ref _gridItems[itemStart + i];
            item.ColumnSpan = item.RowSpan = 1;
        }
        return 1;
    }

    float Aligned(
        Align align,
        in Matrix3x2 toView,
        bool horizontal,
        float start,
        float room,
        float size
    ) =>
        align switch
        {
            Align.Center => Centred(toView, horizontal, start, room, size),
            Align.End => room - size,
            _ => 0f,
        };

    int Place(in GridStyle style, int itemStart, int count, int columns)
    {
        var explicitRows = style.Rows.Count;
        var rows = Math.Max(1, explicitRows);
        _occupied.Clear();
        for (var i = 0; i < count; i++)
        {
            ref var item = ref _gridItems[itemStart + i];
            var place = item.Node.Place;
            if (place.Column <= 0 && place.Row <= 0)
                continue;
            item.Column = place.Column > 0 ? Math.Min(place.Column - 1, columns - 1) : -1;
            item.Row =
                place.Row > 0
                    ? (explicitRows > 0 ? Math.Min(place.Row - 1, explicitRows - 1) : place.Row - 1)
                    : -1;
            item.ColumnSpan = Math.Max(1, place.ColumnSpan);
            item.RowSpan = Math.Max(1, place.RowSpan);
            if (item.Column >= 0)
                item.ColumnSpan = Math.Min(item.ColumnSpan, columns - item.Column);
            if (item.Row >= 0 && explicitRows > 0)
                item.RowSpan = Math.Min(item.RowSpan, explicitRows - item.Row);
            if (item.Column < 0 || item.Row < 0)
                continue;
            Occupy(item.Row, item.Column, item.RowSpan, item.ColumnSpan, columns);
            rows = Math.Max(rows, item.Row + item.RowSpan);
            item.Placed = true;
        }

        for (var i = 0; i < count; i++)
        {
            ref var item = ref _gridItems[itemStart + i];
            if (item.Placed || item.Column < 0 == item.Row < 0)
                continue;
            if (item.Column >= 0)
            {
                var r = 0;
                while (!Free(r, item.Column, item.RowSpan, item.ColumnSpan, columns))
                    r++;
                item.Row = r;
            }
            else
            {
                var c = 0;
                while (
                    c + 1 < columns
                    && !Free(
                        item.Row,
                        c,
                        item.RowSpan,
                        Math.Min(item.ColumnSpan, columns - c),
                        columns
                    )
                )
                    c++;
                item.Column = c;
                item.ColumnSpan = Math.Min(item.ColumnSpan, columns - c);
            }
            Occupy(item.Row, item.Column, item.RowSpan, item.ColumnSpan, columns);
            rows = Math.Max(rows, item.Row + item.RowSpan);
            item.Placed = true;
        }

        var cursorRow = 0;
        var cursorColumn = Math.Clamp(style.FirstColumn - 1, 0, columns - 1);
        for (var i = 0; i < count; i++)
        {
            ref var item = ref _gridItems[itemStart + i];
            if (item.Placed)
                continue;
            var place = item.Node.Place;
            item.ColumnSpan = Math.Min(Math.Max(1, place.ColumnSpan), columns);
            item.RowSpan = Math.Max(1, place.RowSpan);
            while (true)
            {
                if (cursorColumn + item.ColumnSpan > columns)
                {
                    cursorRow++;
                    cursorColumn = 0;
                    continue;
                }
                if (Free(cursorRow, cursorColumn, item.RowSpan, item.ColumnSpan, columns))
                    break;
                cursorColumn++;
            }
            item.Row = cursorRow;
            item.Column = cursorColumn;
            Occupy(item.Row, item.Column, item.RowSpan, item.ColumnSpan, columns);
            rows = Math.Max(rows, item.Row + item.RowSpan);
            cursorColumn += item.ColumnSpan;
            item.Placed = true;
        }
        return rows;
    }

    bool Free(int row, int column, int rowSpan, int columnSpan, int columns)
    {
        for (var r = row; r < row + rowSpan; r++)
        {
            for (var c = column; c < column + columnSpan; c++)
            {
                var index = r * columns + c;
                if (index < _occupied.Count && _occupied[index])
                    return false;
            }
        }
        return true;
    }

    void Occupy(int row, int column, int rowSpan, int columnSpan, int columns)
    {
        var needed = (row + rowSpan) * columns;
        while (_occupied.Count < needed)
            _occupied.Add(false);
        for (var r = row; r < row + rowSpan; r++)
        {
            for (var c = column; c < column + columnSpan; c++)
                _occupied[r * columns + c] = true;
        }
    }

    void SizeAxis(
        int itemStart,
        int count,
        int trackStart,
        int trackCount,
        in GridTracks defined,
        Avail space,
        float padBorder,
        float gap,
        bool horizontal,
        Cells cells
    )
    {
        var inner = space.IsExact ? MathF.Max(0f, space.Value - padBorder) : float.NaN;
        SizeTracks(
            itemStart,
            count,
            trackStart,
            trackCount,
            inner,
            gap,
            horizontal,
            cells,
            minContent: space.Kind == AvailKind.MinContent
        );
        if (space.Kind != AvailKind.AtMost)
            return;
        var room = MathF.Max(0f, space.Value - padBorder);
        if (Sum(trackStart, trackCount, gap) <= room + Epsilon)
            return;
        for (var t = 0; t < trackCount; t++)
            _tracks[trackStart + t] = GridTrack.From(
                t < defined.Count ? defined[t] : Track.Auto,
                room
            );
        SizeTracks(
            itemStart,
            count,
            trackStart,
            trackCount,
            room,
            gap,
            horizontal,
            cells,
            minContent: false
        );
    }

    void SizeTracks(
        int itemStart,
        int count,
        int trackStart,
        int trackCount,
        float available,
        float gap,
        bool horizontal,
        Cells cells,
        bool minContent
    )
    {
        var definite = !float.IsNaN(available);
        var maxSpan = 1;
        for (var i = 0; i < count; i++)
        {
            var item = _gridItems[itemStart + i];
            var span = horizontal ? item.ColumnSpan : item.RowSpan;
            maxSpan = Math.Max(maxSpan, span);
            if (span != 1)
                continue;
            var at = trackStart + (horizontal ? item.Column : item.Row);
            var sizing = _tracks[at];
            if (!sizing.Min.IsContent && !sizing.Max.IsContent && sizing.Max.Kind != TrackKind.Fr)
                continue;
            var floor = sizing.Min.Kind switch
            {
                TrackKind.MaxContent => Contribution(item, horizontal, cells, max: true),
                TrackKind.MinContent => Contribution(item, horizontal, cells, max: false),
                TrackKind.Auto => MinimumContribution(item, horizontal, cells),
                _ => float.NegativeInfinity,
            };
            var ceiling = sizing.Max.Kind switch
            {
                TrackKind.MinContent => Contribution(item, horizontal, cells, max: false),
                TrackKind.Auto or TrackKind.MaxContent => Contribution(
                    item,
                    horizontal,
                    cells,
                    max: true
                ),
                _ => float.NegativeInfinity,
            };
            var content =
                sizing.Max.Kind == TrackKind.Fr && !definite
                    ? Contribution(item, horizontal, cells, max: true)
                    : float.NegativeInfinity;
            // Measuring a nested grid can grow _tracks, so the track is written only after every measure.
            ref var track = ref _tracks[at];
            track.Base = MathF.Max(track.Base, floor);
            track.Limit = MathF.Max(track.Limit, ceiling);
            track.Content = MathF.Max(track.Content, content);
        }

        for (var span = 2; span <= maxSpan; span++)
        {
            for (var i = 0; i < count; i++)
            {
                var item = _gridItems[itemStart + i];
                var start = horizontal ? item.Column : item.Row;
                if (
                    (horizontal ? item.ColumnSpan : item.RowSpan) != span
                    || SpansFlexible(trackStart + start, span)
                )
                    continue;
                var gaps = gap * (span - 1);
                Distribute(
                    trackStart + start,
                    span,
                    Contribution(item, horizontal, cells, max: false) - gaps,
                    limits: false
                );
                Distribute(
                    trackStart + start,
                    span,
                    Contribution(item, horizontal, cells, max: true) - gaps,
                    limits: true
                );
            }
        }

        var fixedSum = 0f;
        var weights = 0f;
        for (var t = 0; t < trackCount; t++)
        {
            ref var track = ref _tracks[trackStart + t];
            if (track.Max.Kind == TrackKind.Fr)
            {
                weights += track.Max.Value;
                continue;
            }
            if (float.IsNegativeInfinity(track.Limit) || track.Limit < track.Base)
                track.Limit = track.Base;
            fixedSum += track.Base;
        }
        var gapSum = gap * MathF.Max(0, trackCount - 1);

        // Under a min-content constraint CSS gives no free space, and the flex fraction is 0.
        if (minContent)
        {
            for (var t = 0; t < trackCount; t++)
            {
                ref var track = ref _tracks[trackStart + t];
                track.Size = track.Base;
            }
            return;
        }

        var free = definite ? available - fixedSum - gapSum : float.PositiveInfinity;
        Maximize(trackStart, trackCount, ref free);

        if (weights > 0f)
            Flexible(
                itemStart,
                count,
                trackStart,
                trackCount,
                definite ? available - gapSum : float.NaN,
                gap,
                weights,
                horizontal,
                cells
            );
        else if (definite && free > Epsilon)
            StretchAuto(trackStart, trackCount, free);

        for (var t = 0; t < trackCount; t++)
        {
            ref var track = ref _tracks[trackStart + t];
            track.Size = track.Base;
        }
    }

    float Contribution(in GridItem item, bool horizontal, Cells cells, bool max)
    {
        var child = item.Node;
        var m = child.Place.Margin;
        var areaW = SizingBasis(item, horizontal, cells);
        if (horizontal)
            return Measure(child, max ? Avail.MaxContent : Avail.MinContent, Avail.MaxContent).Width
                + m.Left
                + m.Right;

        var justify = Effective(child.Place.JustifySelf, cells.JustifyItems);
        var width = CellWidth(child, justify, areaW - m.Left - m.Right);
        return Measure(child, width, max ? Avail.MaxContent : Avail.MinContent).Height
            + m.Top
            + m.Bottom;
    }

    static Avail CellWidth(Node child, Align justify, float room) =>
        justify == Align.Stretch ? Stretched(child, row: false, room) : Avail.AtMost(room);

    // CSS's minimum contribution: an auto-sized item's min size, whose automatic value is 0 when it clips or scrolls.
    float MinimumContribution(in GridItem item, bool horizontal, Cells cells)
    {
        var child = item.Node;
        SizingBasis(item, horizontal, cells);
        var main = horizontal != child.QuarterTurned ? child.Layout.Width : child.Layout.Height;
        if (!float.IsNaN(main.Resolve(float.NaN)))
            return Contribution(item, horizontal, cells, max: false);
        var m = child.Place.Margin;
        var margins = horizontal ? m.Left + m.Right : m.Top + m.Bottom;
        var min = MainLimits(child, horizontal).Min;
        if (!float.IsNaN(min))
            return min + margins;
        return child.Layout.Overflow != Overflow.Visible
            ? margins
            : Contribution(item, horizontal, cells, max: false);
    }

    // A percentage of the cell is cyclic while its own track is sized, so it counts as auto there, as in CSS.
    float SizingBasis(in GridItem item, bool horizontal, Cells cells)
    {
        var areaW = horizontal
            ? float.NaN
            : Span(cells.ColumnStart, item.Column, item.ColumnSpan, cells.ColumnGap);
        item.Node.PctBasis = new Vector2(areaW, float.NaN);
        return areaW;
    }

    bool SpansFlexible(int start, int span)
    {
        for (var t = start; t < start + span; t++)
        {
            if (_tracks[t].Max.Kind == TrackKind.Fr)
                return true;
        }
        return false;
    }

    void Distribute(int start, int span, float needed, bool limits)
    {
        var have = 0f;
        var growable = 0;
        for (var t = start; t < start + span; t++)
        {
            ref var track = ref _tracks[t];
            have += limits ? Finite(track.Limit, track.Base) : track.Base;
            if (limits ? track.Max.IsContent : track.Min.IsContent)
                growable++;
        }
        var lack = needed - have;
        if (lack <= 0f || growable == 0)
            return;
        var each = lack / growable;
        for (var t = start; t < start + span; t++)
        {
            ref var track = ref _tracks[t];
            if (limits && track.Max.IsContent)
                track.Limit = Finite(track.Limit, track.Base) + each;
            else if (!limits && track.Min.IsContent)
                track.Base += each;
        }
    }

    static float Finite(float limit, float fallback) =>
        float.IsNegativeInfinity(limit) ? fallback : limit;

    void Maximize(int trackStart, int trackCount, ref float free)
    {
        for (var round = 0; round < trackCount && free > Epsilon; round++)
        {
            var growing = 0;
            for (var t = 0; t < trackCount; t++)
            {
                ref var track = ref _tracks[trackStart + t];
                if (track.Max.Kind != TrackKind.Fr && track.Base < track.Limit - Epsilon)
                    growing++;
            }
            if (growing == 0)
                return;
            var share = free / growing;
            for (var t = 0; t < trackCount; t++)
            {
                ref var track = ref _tracks[trackStart + t];
                if (track.Max.Kind == TrackKind.Fr || track.Base >= track.Limit - Epsilon)
                    continue;
                var grown = MathF.Min(track.Limit, track.Base + share);
                free -= grown - track.Base;
                track.Base = grown;
            }
        }
    }

    void StretchAuto(int trackStart, int trackCount, float free)
    {
        var auto = 0;
        for (var t = 0; t < trackCount; t++)
        {
            if (_tracks[trackStart + t].Max.Kind == TrackKind.Auto)
                auto++;
        }
        if (auto == 0)
            return;
        for (var t = 0; t < trackCount; t++)
        {
            ref var track = ref _tracks[trackStart + t];
            if (track.Max.Kind == TrackKind.Auto)
                track.Base += free / auto;
        }
    }

    void Flexible(
        int itemStart,
        int count,
        int trackStart,
        int trackCount,
        float available,
        float gap,
        float weights,
        bool horizontal,
        Cells cells
    )
    {
        float fraction;
        if (!float.IsNaN(available))
        {
            var rest = available;
            for (var t = 0; t < trackCount; t++)
            {
                ref var track = ref _tracks[trackStart + t];
                if (track.Max.Kind != TrackKind.Fr)
                    rest -= track.Base;
                track.Inflexible = false;
            }
            fraction = 0f;
            for (var round = 0; round <= trackCount; round++)
            {
                var left = rest;
                var weight = 0f;
                for (var t = 0; t < trackCount; t++)
                {
                    var track = _tracks[trackStart + t];
                    if (track.Max.Kind != TrackKind.Fr)
                        continue;
                    if (track.Inflexible)
                        left -= track.Base;
                    else
                        weight += track.Max.Value;
                }
                fraction = weight > 0f ? MathF.Max(0f, left) / weight : 0f;
                var held = false;
                for (var t = 0; t < trackCount; t++)
                {
                    ref var track = ref _tracks[trackStart + t];
                    if (track.Max.Kind != TrackKind.Fr || track.Inflexible)
                        continue;
                    if (track.Base > fraction * track.Max.Value + Epsilon)
                    {
                        track.Inflexible = true;
                        held = true;
                    }
                }
                if (!held)
                    break;
            }
        }
        else
        {
            fraction = 0f;
            for (var t = 0; t < trackCount; t++)
            {
                var track = _tracks[trackStart + t];
                if (track.Max.Kind == TrackKind.Fr && track.Max.Value > 0f)
                    fraction = MathF.Max(
                        fraction,
                        MathF.Max(track.Base, track.Content) / track.Max.Value
                    );
            }
            for (var i = 0; i < count; i++)
            {
                var item = _gridItems[itemStart + i];
                var start = trackStart + (horizontal ? item.Column : item.Row);
                var span = horizontal ? item.ColumnSpan : item.RowSpan;
                if (span == 1 || !SpansFlexible(start, span))
                    continue;
                var spanWeights = 0f;
                var fixedPart = gap * (span - 1);
                for (var t = start; t < start + span; t++)
                {
                    var track = _tracks[t];
                    if (track.Max.Kind == TrackKind.Fr)
                        spanWeights += track.Max.Value;
                    else
                        fixedPart += track.Base;
                }
                if (spanWeights > 0f)
                    fraction = MathF.Max(
                        fraction,
                        (Contribution(item, horizontal, cells, max: true) - fixedPart) / spanWeights
                    );
            }
        }

        for (var t = 0; t < trackCount; t++)
        {
            ref var track = ref _tracks[trackStart + t];
            if (track.Max.Kind == TrackKind.Fr && !track.Inflexible)
                track.Base = MathF.Max(track.Base, fraction * track.Max.Value);
        }
    }

    float Sum(int start, int count, float gap)
    {
        var sum = gap * MathF.Max(0, count - 1);
        for (var t = start; t < start + count; t++)
            sum += _tracks[t].Size;
        return sum;
    }

    float Offset(int start, int index, float gap)
    {
        var offset = 0f;
        for (var t = 0; t < index; t++)
            offset += _tracks[start + t].Size + gap;
        return offset;
    }

    float Span(int start, int index, int span, float gap)
    {
        var size = gap * (span - 1);
        for (var t = index; t < index + span; t++)
            size += _tracks[start + t].Size;
        return size;
    }

    readonly record struct Cells(int ColumnStart, float ColumnGap, Align JustifyItems);

    struct GridItem
    {
        public Node Node;
        public int Column;
        public int Row;
        public int ColumnSpan;
        public int RowSpan;
        public bool Placed;
    }

    struct GridTrack
    {
        public TrackSize Min;
        public TrackSize Max;
        public float Base;
        public float Limit;
        public float Content;
        public float Size;
        public bool Inflexible;

        public static GridTrack From(Track track, float available)
        {
            var min = Resolved(track.Min, available);
            var max = Resolved(track.Max, available);
            var result = new GridTrack
            {
                Min = min,
                Max = max,
                Limit = float.NegativeInfinity,
            };
            if (min.Kind == TrackKind.Px)
                result.Base = min.Value;
            if (max.Kind == TrackKind.Px)
                result.Limit = MathF.Max(max.Value, result.Base);
            return result;
        }

        static TrackSize Resolved(TrackSize size, float available) =>
            size.Kind != TrackKind.Percent ? size
            : float.IsNaN(available) ? new TrackSize(TrackKind.Auto, 0f)
            : new TrackSize(TrackKind.Px, available * size.Value / 100f);
    }
}

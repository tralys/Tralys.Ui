using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Tralys.Ui.Fonts;

/// <remarks>Caret and selection geometry reads the layout as <see cref="Update"/> last made it, so the buffer must not
/// change between that call and these.</remarks>
public sealed partial class TextLayout
{
    /// <summary>The line a caret before character <paramref name="index"/> sits on. Where a line wraps, its end and
    /// the next line's start are the same index: <paramref name="atLineEnd"/> picks the end of the line that wraps
    /// there, and is ignored at a line break.</summary>
    public int GetLineAt(int index, bool atLineEnd = false)
    {
        ThrowIfNotLaidOut();
        ref readonly var lastLine = ref _lines[_lineCount - 1];
        index = Math.Clamp(index, 0, lastLine.Start + lastLine.Length);
        var low = 0;
        var high = _lineCount - 1;
        while (low < high)
        {
            var middle = (low + high + 1) >> 1;
            if (_lines[middle].Start <= index)
                low = middle;
            else
                high = middle - 1;
        }

        if (atLineEnd && low > 0 && index == _lines[low].Start && WrapsAt(low - 1))
            low--;
        return low;
    }

    /// <summary>The last index a caret takes on <paramref name="line"/>: before its line break, where the next line
    /// starts when it wraps there, or before the ellipsis when trimming cut it.</summary>
    public int GetLineEnd(int line)
    {
        ThrowIfNotLaidOut();
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(line, _lineCount);
        return LineEnd(line);
    }

    /// <summary>The caret before character <paramref name="index"/>: a zero-width box from its line's top to its
    /// bottom, in layout space. <paramref name="atLineEnd"/> is as for <see cref="GetLineAt"/>.</summary>
    public Rect GetCaretRect(int index, bool atLineEnd = false)
    {
        var line = GetLineAt(index, atLineEnd);
        ref readonly var found = ref _lines[line];
        return new Rect(CaretX(line, index), found.Top, 0f, found.Height);
    }

    /// <summary>The caret index nearest <paramref name="point"/>, in layout space: on the line at its height, or the
    /// first or last line above or below them all, at the cluster boundary nearest its x. <paramref name="atLineEnd"/>
    /// says whether it is the end of a line that wraps there rather than the start of the next.</summary>
    public int HitTest(Vector2 point, out bool atLineEnd)
    {
        ThrowIfNotLaidOut();
        var line = 0;
        while (line < _lineCount - 1 && point.Y >= _lines[line].Top + _lines[line].Height)
            line++;

        var text = _buffer.Text;
        var start = _lines[line].Start;
        var end = LineEnd(line);
        var index = start;
        var x = CaretX(line, start);
        while (index < end)
        {
            var next = TextClusters.Next(text, index, end);
            var nextX = CaretX(line, next);
            if (point.X < (x + nextX) * 0.5f)
                break;
            index = next;
            x = nextX;
        }

        atLineEnd = index == end && WrapsAt(line);
        return index;
    }

    /// <summary>Writes into <paramref name="destination"/>, which has room for a box per line of the layout, the boxes
    /// in layout space that cover characters <paramref name="start"/> to <paramref name="end"/>: one for each line
    /// they touch, a line's break shown as a quarter of its height.</summary>
    /// <returns>How many boxes it wrote.</returns>
    public int GetSelectionRects(int start, int end, Span<Rect> destination) =>
        GetRects(start, end, destination, selecting: true);

    internal int GetTextRects(int start, int end, Span<Rect> destination) =>
        GetRects(start, end, destination, selecting: false);

    int GetRects(int start, int end, Span<Rect> destination, bool selecting)
    {
        ThrowIfNotLaidOut();
        if (end < start)
            (start, end) = (end, start);
        if (start == end)
            return 0;

        var first = GetLineAt(start);
        var last = GetLineAt(end, atLineEnd: true);
        var count = 0;
        for (var line = first; line <= last; line++)
        {
            ref readonly var found = ref _lines[line];
            var lineEnd = LineEnd(line);
            var left = CaretX(line, Math.Max(start, found.Start));
            var right = CaretX(
                line,
                Math.Min(end, selecting ? lineEnd : found.Start + found.MeasuredLength)
            );
            if (selecting && end > lineEnd && !WrapsAt(line) && line < _lineCount - 1)
                right += found.Height * 0.25f;
            if (right <= left)
                continue;

            if (count == destination.Length)
                throw new ArgumentException(
                    "The destination has no room for a box per line.",
                    nameof(destination)
                );
            destination[count++] = new Rect(left, found.Top, right - left, found.Height);
        }
        return count;
    }

    float CaretX(int line, int index)
    {
        ref readonly var found = ref _lines[line];
        index = Math.Clamp(index, found.Start, LineEnd(line));
        return found.X + _pen[index] - _pen[found.Start];
    }

    int LineEnd(int line)
    {
        ref readonly var found = ref _lines[line];
        var end = found.Start + found.Length;
        if (end == found.Start)
            return end;
        if (found.DrawnLength < found.Length)
            return found.Start + found.DrawnLength;

        var text = LaidOut.Text;
        var last = text[end - 1];
        if (!TextClusters.IsLineBreak(last))
            return end;
        return last == '\n' && end - 1 > found.Start && text[end - 2] == '\r' ? end - 2 : end - 1;
    }

    bool WrapsAt(int line) =>
        line < _lineCount - 1 && LineEnd(line) == _lines[line].Start + _lines[line].Length;

    TextBuffer LaidOut
    {
        get
        {
            ThrowIfNotLaidOut();
            return _buffer;
        }
    }

    [MemberNotNull(nameof(_buffer))]
    void ThrowIfNotLaidOut()
    {
        if (_buffer is null)
            throw new InvalidOperationException("Nothing has been laid out yet.");
    }
}

using System.Globalization;
using System.Text;
using Tralys.Ui.Fonts;

namespace Tralys.Ui;

// Indexes count UTF-16 units; the caret moves over whole grapheme clusters.
internal sealed partial class TextEditor
{
    char[] _chars = [];
    char[] _scratch = [];
    int _length;
    int _caret;
    int _anchor;
    Run _run;

    public ReadOnlySpan<char> Text => _chars.AsSpan(0, _length);

    public int Length => _length;

    public int Caret => _caret;

    public int Anchor => _anchor;

    public int SelectionStart => Math.Min(_caret, _anchor);

    public int SelectionEnd => Math.Max(_caret, _anchor);

    public bool HasSelection => _caret != _anchor;

    public ReadOnlySpan<char> SelectedText =>
        _chars.AsSpan(SelectionStart, SelectionEnd - SelectionStart);

    public int Version { get; private set; }

    // Both limits bind typing and inserting only; text set whole is not cut to them.
    public int MaxLength { get; set; }

    public int MaxBytes { get; set; }

    public bool AcceptsLineBreaks { get; set; } = true;

    public void Release()
    {
        ClearUndo();
        if (_length > 0)
            Version++;
        _length = _caret = _anchor = 0;
        SharedArrays<char>.Release(ref _chars);
        SharedArrays<char>.Release(ref _scratch);
    }

    [FramePath]
    public bool SetText(ReadOnlySpan<char> text)
    {
        ClearUndo();
        if (text.SequenceEqual(Text))
        {
            Collapse(_caret);
            return false;
        }

        text = Unalias(text);
        Reserve(text.Length);
        text.CopyTo(_chars);
        _length = text.Length;
        Version++;
        Collapse(TextClusters.Boundary(Text, Math.Min(_caret, _length)));
        return true;
    }

    // A run of these with no edit between undoes as one step, and one that ends where it began leaves none.
    [FramePath]
    public bool Replace(ReadOnlySpan<char> text)
    {
        if (text.SequenceEqual(Text))
        {
            if (_run != Run.Outside)
                _run = Run.None;
            return false;
        }

        text = Unalias(text);
        if (_run == Run.Outside && _outsideStep)
            Restate(text);
        else
        {
            Record(0, Text, text, _caret, _anchor);
            _outsideStep = _undoEnabled;
        }
        Splice(0, _length, text);
        Collapse(TextClusters.Boundary(Text, Math.Min(_caret, _length)));
        _run = Run.Outside;
        return true;
    }

    // Unlike MoveCaret, leaves a run of changes from outside open.
    [FramePath]
    public void PlaceCaret(int index)
    {
        var run = _run;
        MoveCaret(index, extend: false);
        if (run == Run.Outside)
            _run = run;
    }

    [FramePath]
    public void Select(int anchor, int caret)
    {
        _run = Run.None;
        var text = Text;
        _anchor = TextClusters.Boundary(text, Math.Clamp(anchor, 0, _length));
        _caret = TextClusters.Boundary(text, Math.Clamp(caret, 0, _length));
    }

    [FramePath]
    public void MoveCaret(int index, bool extend) => Select(extend ? _anchor : index, index);

    [FramePath]
    public void SelectAll() => Select(0, _length);

    [FramePath]
    public int NextCluster(int index) =>
        index >= _length ? _length : TextClusters.Next(Text, Math.Max(0, index), _length);

    [FramePath]
    public int PreviousCluster(int index) => TextClusters.Previous(Text, Math.Min(index, _length));

    [FramePath]
    public bool Type(ReadOnlySpan<char> text) =>
        Replace(SelectionStart, SelectionEnd, text, Run.Typing);

    [FramePath]
    public bool Insert(ReadOnlySpan<char> text) =>
        Replace(SelectionStart, SelectionEnd, text, Run.None);

    [FramePath]
    public bool Backspace(bool word = false)
    {
        if (HasSelection)
            return Replace(SelectionStart, SelectionEnd, default, Run.None);
        if (_caret == 0)
            return false;

        var start = word ? PreviousWord(_caret) : PreviousCluster(_caret);
        return Replace(start, _caret, default, word ? Run.None : Run.Backspace);
    }

    [FramePath]
    public bool Delete(bool word = false)
    {
        if (HasSelection)
            return Replace(SelectionStart, SelectionEnd, default, Run.None);
        if (_caret == _length)
            return false;

        var end = word ? NextWord(_caret) : NextCluster(_caret);
        return Replace(_caret, end, default, word ? Run.None : Run.Delete);
    }

    [FramePath]
    void Collapse(int index)
    {
        _caret = index;
        _anchor = index;
    }

    [FramePath]
    bool Replace(int start, int end, ReadOnlySpan<char> text, Run run)
    {
        text = Fit(text, start, end);
        if (text.IsEmpty && start == end)
        {
            _run = Run.None;
            return false;
        }

        if (run != Run.None && run == _run && Continues(run, start, end))
            Extend(run, start, end, text);
        else
            Record(start, Text[start..end], text, _caret, _anchor);

        Splice(start, end, text);
        Collapse(start + text.Length);
        _run = run;
        return true;
    }

    [FramePath]
    ReadOnlySpan<char> Fit(ReadOnlySpan<char> text, int start, int end)
    {
        var charRoom = MaxLength > 0 ? MaxLength - (_length - (end - start)) : int.MaxValue;
        var byteRoom = int.MaxValue;
        if (MaxBytes > 0)
        {
            var utf8 = Encoding.UTF8;
            byteRoom = MaxBytes - utf8.GetByteCount(Text[..start]) - utf8.GetByteCount(Text[end..]);
        }
        return Unalias(CutToRoom(Clean(text), Math.Max(0, charRoom), Math.Max(0, byteRoom)));
    }

    [FramePath]
    ReadOnlySpan<char> Clean(ReadOnlySpan<char> text)
    {
        var kept = 0;
        foreach (var c in text)
        {
            if (Keeps(c))
                kept++;
        }
        if (kept == text.Length)
            return text;

        SharedArrays<char>.Reserve(ref _scratch, kept);
        var written = 0;
        foreach (var c in text)
        {
            if (Keeps(c))
                _scratch[written++] = c;
        }
        return _scratch.AsSpan(0, written);
    }

    [FramePath]
    static ReadOnlySpan<char> CutToRoom(ReadOnlySpan<char> text, int charRoom, int byteRoom)
    {
        var utf8 = Encoding.UTF8;
        if (text.Length <= charRoom && utf8.GetByteCount(text) <= byteRoom)
            return text;

        var cut = 0;
        while (cut < text.Length)
        {
            var next = TextClusters.Next(text, cut, text.Length);
            byteRoom -= utf8.GetByteCount(text[cut..next]);
            if (next > charRoom || byteRoom < 0)
                break;
            cut = next;
        }
        return text[..cut];
    }

    bool Keeps(char c) =>
        c is '\n' or '\r' or (char)0x85 or (char)0x2028 or (char)0x2029
            ? AcceptsLineBreaks
            : c == '\t' || !(c < ' ' || c == '\u007F' || IsHiddenFormat(c));

    // A format character draws nothing and can reorder what surrounds it; the joiners hold emoji and letters together.
    [FramePath]
    static bool IsHiddenFormat(char c) =>
        c >= '\u00AD'
        && c is not ('\u200C' or '\u200D')
        && char.GetUnicodeCategory(c) == UnicodeCategory.Format;

    [FramePath]
    void Splice(int start, int end, ReadOnlySpan<char> text)
    {
        var removed = end - start;
        Reserve(_length - removed + text.Length);
        Array.Copy(_chars, end, _chars, start + text.Length, _length - end);
        text.CopyTo(_chars.AsSpan(start));
        _length += text.Length - removed;
        Version++;
    }

    [FramePath]
    void Reserve(int length) => SharedArrays<char>.Grow(ref _chars, length, _length);

    // An edit reads its text while it moves the characters, so its own characters are read from a copy.
    [FramePath]
    ReadOnlySpan<char> Unalias(ReadOnlySpan<char> text)
    {
        if (!text.Overlaps(_chars))
            return text;

        SharedArrays<char>.Reserve(ref _scratch, text.Length);
        text.CopyTo(_scratch);
        return _scratch.AsSpan(0, text.Length);
    }

    enum Run : byte
    {
        None,
        Typing,
        Backspace,
        Delete,
        Outside,
    }
}

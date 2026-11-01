namespace Tralys.Ui.Fonts;

/// <summary>The characters a view draws, in runs of <see cref="TextStyle"/>. It owns them, so setting text allocates
/// nothing once its capacity has grown to fit.</summary>
[FramePath]
public sealed class TextBuffer
{
    char[] _chars = [];
    TextRun[] _runs = [];
    int _runCount;
    TextStyle _style;
    int _length;
    bool _plain = true;

    public TextBuffer(TextStyle style)
    {
        TextStyle.ThrowIfDefault(style, nameof(style));
        _style = style;
    }

    /// <summary>The style <see cref="SetText(ReadOnlySpan{char})"/> draws in, and the one an empty buffer's line is
    /// measured in.</summary>
    public TextStyle Style
    {
        get => _style;
        set
        {
            TextStyle.ThrowIfDefault(value, nameof(value));
            if (!value.LaysOutLike(_style))
                LayoutVersion++;
            _style = value;
            if (_plain && _runCount == 1)
                _runs[0] = new TextRun(0, _length, value);
            Version++;
        }
    }

    public ReadOnlySpan<char> Text => _chars.AsSpan(0, _length);

    public ReadOnlySpan<TextRun> Runs => _runs.AsSpan(0, _runCount);

    /// <summary>Moves on whenever the text or a style does, so a layout knows to run again.</summary>
    public int Version { get; private set; }

    // Only a change that leaves every glyph and line where it was may move Version without this.
    internal int LayoutVersion { get; private set; }

    internal void Restyle(int index, in TextStyle style)
    {
        if ((uint)index >= (uint)_runCount)
            throw new ArgumentOutOfRangeException(nameof(index));
        TextStyle.ThrowIfDefault(style, nameof(style));

        ref var run = ref _runs[index];
        if (!style.LaysOutLike(run.Style))
            LayoutVersion++;
        run = new TextRun(run.Start, run.Length, style);
        Version++;
    }

    /// <summary>Replaces the text with <paramref name="text"/> in <see cref="Style"/>. Setting the text it already holds
    /// changes nothing, <see cref="Version"/> included.</summary>
    public void SetText(ReadOnlySpan<char> text)
    {
        if (_plain && text.SequenceEqual(Text))
            return;

        _length = 0;
        ClearRuns();
        AddRun(text, _style);
        _plain = true;
        Changed();
    }

    public void Clear()
    {
        _length = 0;
        ClearRuns();
        _plain = true;
        Changed();
    }

    internal void Release()
    {
        Clear();
        SharedArrays<char>.Release(ref _chars);
        SharedArrays<TextRun>.Release(ref _runs);
    }

    void ClearRuns()
    {
        Array.Clear(_runs, 0, _runCount);
        _runCount = 0;
    }

    /// <summary>Adds <paramref name="text"/> as a run in <paramref name="style"/>.</summary>
    public void Append(ReadOnlySpan<char> text, in TextStyle style)
    {
        TextStyle.ThrowIfDefault(style, nameof(style));
        AddRun(text, style);
        _plain = false;
        Changed();
    }

    void Changed()
    {
        Version++;
        LayoutVersion++;
    }

    void AddRun(ReadOnlySpan<char> text, in TextStyle style)
    {
        SharedArrays<char>.Grow(ref _chars, _length + text.Length, _length);
        text.CopyTo(_chars.AsSpan(_length));
        _length += text.Length;
        if (text.Length == 0)
            return;
        SharedArrays<TextRun>.Grow(ref _runs, _runCount + 1, _runCount);
        _runs[_runCount++] = new TextRun(_length - text.Length, text.Length, style);
    }
}

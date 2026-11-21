using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using Tralys.Ui.Fonts.Unicode;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.Fonts;

/// <summary>Lays out a <see cref="TextBuffer"/> into lines and positioned glyphs, and draws them through a
/// <see cref="GlyphAtlas"/>. Positions are in layout units and fractional; lines are measured at <see cref="Scale"/>
/// device pixels per unit, and each glyph is drawn where its pen lands, unrounded. Every array is reused, so laying out
/// and drawing allocate nothing once warm.</summary>
/// <remarks>Each character takes its font's glyph, kerned against the one before, left to right: no OpenType
/// substitution, bidirectional reordering or colour glyph is applied, and variation selectors and joiners draw
/// nothing.</remarks>
[FramePath]
public sealed partial class TextLayout
{
    const int TabSpaces = 4;
    internal const float Tolerance = 1e-3f;

    float _maxWidth = float.PositiveInfinity;
    float _maxHeight = float.PositiveInfinity;
    TextWrapping _wrapping;
    TextTrimming _trimming;
    TextAlignment _alignment;
    float _lineHeight = float.NaN;
    float _scale = 1f;
    Font? _cellFont;
    float _cellSize;
    float _cell;
    bool _dirty = true;
    TextBuffer? _buffer;
    int _version;
    int _layoutVersion;

    [ThreadStatic]
    static Scratch? t_scratch;

    float[] _pen = [];
    LayoutGlyph[] _glyphs = [];
    TextLine[] _lines = [];
    GlyphQuad[] _quads = [];
    QuadSource[] _quadSources = [];
    int _shapedCount;
    int _glyphCount;
    int _lineCount;
    int _quadCount;

    /// <summary>The width lines wrap or trim at; infinite for none.</summary>
    public float MaxWidth
    {
        get => _maxWidth;
        set =>
            Set(ref _maxWidth, float.IsNaN(value) ? float.PositiveInfinity : MathF.Max(0f, value));
    }

    /// <summary>With trimming, the height past which lines are dropped and the last one kept ends in an ellipsis.</summary>
    public float MaxHeight
    {
        get => _maxHeight;
        set =>
            Set(ref _maxHeight, float.IsNaN(value) ? float.PositiveInfinity : MathF.Max(0f, value));
    }

    public TextWrapping Wrapping
    {
        get => _wrapping;
        set => Set(ref _wrapping, value);
    }

    public TextTrimming Trimming
    {
        get => _trimming;
        set => Set(ref _trimming, value);
    }

    /// <summary>Where lines sit across <see cref="MaxWidth"/>, or across the widest line when that is infinite.</summary>
    public TextAlignment Alignment
    {
        get => _alignment;
        set => Set(ref _alignment, value);
    }

    /// <summary>The height of every line, its baseline at the same fraction of it as its fonts put it; NaN, the default,
    /// leaves each line as tall as its fonts make it.</summary>
    public float LineHeight
    {
        get => _lineHeight;
        set
        {
            if (!float.IsNaN(value))
                TextStyle.ThrowUnlessPositiveFinite(
                    value,
                    "A line height must be positive and finite, or NaN.",
                    nameof(value)
                );
            Set(ref _lineHeight, value);
        }
    }

    /// <summary>Device pixels per layout unit, such as a view's scale; each line rounds up to whole device pixels at
    /// it.</summary>
    public float Scale
    {
        get => _scale;
        set
        {
            TextStyle.ThrowUnlessPositiveFinite(
                value,
                "A scale must be positive and finite.",
                nameof(value)
            );
            Set(ref _scale, value);
        }
    }

    /// <summary>The widest line's width and the lines' total height.</summary>
    public Size Size { get; private set; }

    internal float MinContentWidth { get; private set; }

    public ReadOnlySpan<TextLine> Lines => _lines.AsSpan(0, _lineCount);

    public ReadOnlySpan<LayoutGlyph> Glyphs => _glyphs.AsSpan(0, _glyphCount);

    /// <summary>Lays <paramref name="buffer"/> out unless neither it nor a setting changed since the last call; when
    /// only what its styles paint changed, it takes the new paints without laying out again.</summary>
    /// <returns>Whether what it draws changed.</returns>
    public bool Update(TextBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (!_dirty && ReferenceEquals(buffer, _buffer))
        {
            if (buffer.Version == _version)
                return false;
            if (buffer.LayoutVersion == _layoutVersion)
            {
                _version = buffer.Version;
                return true;
            }
        }

        Layout(buffer);
        _buffer = buffer;
        _version = buffer.Version;
        _layoutVersion = buffer.LayoutVersion;
        _dirty = false;
        return true;
    }

    /// <summary>Records the layout with its top-left at <paramref name="origin"/> in local space, every stroke before
    /// any fill. It reads the buffer's styles, so the buffer must not change between <see cref="Update"/> and this.</summary>
    public void Draw(DrawingContext context, GlyphAtlas atlas, Vector2 origin = default)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        ThrowIfNotLaidOut();

        var runs = _buffer.Runs;
        ResolveQuads(runs, atlas, origin);

        var start = -1;
        for (var i = 0; i <= _quadCount; i++)
        {
            var ends =
                i == _quadCount || (start >= 0 && !_quadSources[i].Joins(_quadSources[start]));
            if (ends && start >= 0)
            {
                ref readonly var source = ref _quadSources[start];
                ref readonly var style = ref runs[source.Run].Style;
                context.DrawGlyphs(
                    source.Page,
                    _quads.AsSpan(start, i - start),
                    source.IsStroke ? style.Stroke : style.Fill
                );
                start = -1;
            }

            if (i < _quadCount && start < 0)
                start = i;
        }
    }

    void ResolveQuads(ReadOnlySpan<TextRun> runs, GlyphAtlas atlas, Vector2 origin)
    {
        _quadCount = 0;
        SharedArrays<GlyphQuad>.Reserve(ref _quads, _glyphCount * 2);
        SharedArrays<QuadSource>.Reserve(ref _quadSources, _glyphCount * 2);
        for (var pass = 0; pass < 2; pass++)
        {
            var stroke = pass == 0;
            for (var i = 0; i < _glyphCount; i++)
            {
                ref readonly var glyph = ref _glyphs[i];
                ref readonly var style = ref runs[glyph.Run].Style;
                if (stroke && !style.HasStroke)
                    continue;

                if (
                    !atlas.TryGet(
                        glyph.Font,
                        glyph.Glyph,
                        style.Size,
                        stroke ? style.StrokeWidth : 0f,
                        out var found
                    )
                )
                    continue;

                var perTexel = style.Size / GlyphAtlas.EmSize;
                var source = found.Source;
                _quads[_quadCount] = new GlyphQuad(
                    new Rect(
                        origin.X + glyph.X + found.Left * perTexel,
                        origin.Y + glyph.Y - found.Top * perTexel,
                        source.Width * perTexel,
                        source.Height * perTexel
                    ),
                    source
                );
                _quadSources[_quadCount++] = new QuadSource(found.Page, glyph.Run, stroke);
            }
        }
    }

    void Layout(TextBuffer buffer)
    {
        var text = buffer.Text;
        var runs = buffer.Runs;
        var length = text.Length;

        var work = t_scratch ??= new Scratch();
        ValueList.EnsureCapacity(ref work.Shaped, length);
        ValueList.EnsureCapacity(ref work.FirstShaped, length + 1);
        SharedArrays<float>.Reserve(ref _pen, length + 1);

        Shape(work, text, runs);
        FindBreaks(work, text);
        MinContentWidth = WidestSegment(work, length);
        BreakLines(work, text, runs, buffer.Style);
        var kept = TrimLines(work, text, runs);
        Emit(work, kept);
    }

    internal void Release()
    {
        SharedArrays<float>.Release(ref _pen);
        SharedArrays<LayoutGlyph>.Release(ref _glyphs);
        SharedArrays<TextLine>.Release(ref _lines);
        SharedArrays<GlyphQuad>.Release(ref _quads);
        SharedArrays<QuadSource>.Release(ref _quadSources);
        _glyphCount = _lineCount = _quadCount = 0;
        _buffer = null;
        _dirty = true;
        Size = default;
        MinContentWidth = 0f;
    }

    void Shape(Scratch work, ReadOnlySpan<char> text, ReadOnlySpan<TextRun> runs)
    {
        _shapedCount = 0;
        var pen = 0f;
        var previous = -1;
        for (var r = 0; r < runs.Length; r++)
        {
            ref readonly var run = ref runs[r];
            var font = run.Style.Font;
            var pixelSize = run.Style.Size * _scale;
            var spacing = run.Style.SpacingAdvance;
            var owedSpacing = 0f;
            var end = run.Start + run.Length;
            var i = run.Start;
            var clusterEnd = i;
            while (i < end)
            {
                if (spacing != 0f && i >= clusterEnd)
                    clusterEnd = TextClusters.Next(text, i, end);

                if (
                    Rune.DecodeFromUtf16(text[i..end], out var rune, out var consumed)
                    != OperationStatus.Done
                )
                    rune = Rune.ReplacementChar;

                var kind = Classify(rune);
                var tabular = run.Style.TabularDigits && rune.Value is >= '0' and <= '9';
                var used = font;
                var glyph = 0;
                var advance = 0f;
                var inset = 0f;
                if (kind != CharKind.Ignored)
                {
                    used = font.Collection.Resolve(
                        font,
                        kind == CharKind.Tab ? new Rune(' ') : rune,
                        out glyph
                    );
                    advance = used.GetAdvance(glyph, pixelSize) / _scale;
                    if (kind == CharKind.Tab)
                        advance *= TabSpaces;
                    if (tabular)
                    {
                        var cell = DigitCell(font, pixelSize) / _scale;
                        inset = (cell - advance) * 0.5f;
                        advance = cell;
                    }

                    if (previous >= 0 && !tabular)
                    {
                        ref readonly var prior = ref work.Shaped[previous];
                        if (prior.Font == used && prior.PixelSize == pixelSize)
                            pen += used.GetKerning(prior.Glyph, glyph, pixelSize) / _scale;
                    }
                }

                for (var k = 0; k < consumed; k++)
                {
                    _pen[i + k] = pen;
                    work.FirstShaped[i + k] = _shapedCount;
                }

                if (kind == CharKind.Ignored)
                {
                    previous = -1;
                }
                else
                {
                    work.Shaped[_shapedCount] = new Shaped(
                        used,
                        glyph,
                        r,
                        pen + inset,
                        pixelSize,
                        kind == CharKind.Drawn
                    );
                    previous = kind == CharKind.Tab || tabular ? -1 : _shapedCount;
                    _shapedCount++;
                    pen += advance;
                    owedSpacing = spacing;
                }

                i += consumed;
                if (owedSpacing != 0f && i >= clusterEnd)
                {
                    pen += owedSpacing;
                    owedSpacing = 0f;
                }
            }
        }

        _pen[text.Length] = pen;
        work.FirstShaped[text.Length] = _shapedCount;
    }

    float DigitCell(Font font, float pixelSize)
    {
        if (ReferenceEquals(font, _cellFont) && pixelSize == _cellSize)
            return _cell;

        var widest = 0f;
        for (var digit = '0'; digit <= '9'; digit++)
        {
            var used = font.Collection.Resolve(font, new Rune(digit), out var glyph);
            widest = MathF.Max(widest, used.GetAdvance(glyph, pixelSize));
        }
        _cellFont = font;
        _cellSize = pixelSize;
        _cell = widest;
        return widest;
    }

    void FindBreaks(Scratch work, ReadOnlySpan<char> text)
    {
        work.Breaks.Clear();
        if (text.IsEmpty)
            return;

        var breaker = new LineBreakEnumerator(text);
        while (breaker.MoveNext(out var lineBreak))
            work.Breaks.Add(lineBreak);
    }

    float WidestSegment(Scratch work, int length)
    {
        var widest = 0f;
        var start = 0;
        foreach (ref readonly var candidate in work.Breaks.AsSpan())
        {
            widest = MathF.Max(widest, Width(start, Math.Max(candidate.PositionMeasure, start)));
            start = Math.Max(start, candidate.PositionWrap);
        }
        return MathF.Max(widest, Width(start, length));
    }

    void BreakLines(
        Scratch work,
        ReadOnlySpan<char> text,
        ReadOnlySpan<TextRun> runs,
        in TextStyle fallback
    )
    {
        work.Pending.Clear();
        var length = text.Length;
        var limit = _wrapping == TextWrapping.Wrap ? _maxWidth + Tolerance : float.PositiveInfinity;
        var start = 0;
        var next = 0;
        var endedHard = false;
        while (start < length)
        {
            var chosen = -1;
            var overflowing = -1;
            var overflow = length;
            while (next < work.Breaks.Count)
            {
                ref readonly var candidate = ref work.Breaks[next];
                if (candidate.PositionWrap <= start)
                {
                    next++;
                    continue;
                }

                var measure = Math.Max(candidate.PositionMeasure, start);
                if (Width(start, measure) > limit)
                {
                    overflow = measure;
                    overflowing = next;
                    break;
                }

                chosen = next++;
                if (candidate.Required)
                    break;
            }

            var end = 0;
            if (chosen < 0)
            {
                end = FitClusters(text, start, overflow, limit);
                if (overflowing >= 0 && end == overflow)
                {
                    chosen = overflowing;
                    next = overflowing + 1;
                }
            }

            int wrap;
            if (chosen < 0)
            {
                wrap = end;
                endedHard = false;
            }
            else
            {
                ref readonly var taken = ref work.Breaks[chosen];
                end = Math.Max(taken.PositionMeasure, start);
                wrap = taken.PositionWrap;
                endedHard = taken.Required;
            }

            AddPending(work, runs, fallback, start, end, wrap);
            start = wrap;
        }

        if (length == 0 || endedHard)
            AddPending(work, runs, fallback, length, length, length);
    }

    void AddPending(
        Scratch work,
        ReadOnlySpan<TextRun> runs,
        in TextStyle fallback,
        int start,
        int end,
        int wrap
    )
    {
        ref var line = ref work.Pending.AddDefault();
        line.Start = start;
        line.End = end;
        line.Wrap = wrap;
        line.Width = Width(start, end);

        var last = Math.Max(wrap, start + 1);
        var measured = false;
        foreach (ref readonly var run in runs)
        {
            if (run.Start < last && run.Start + run.Length > start)
            {
                Measure(ref line, run.Style);
                measured = true;
            }
        }

        if (!measured)
            Measure(ref line, runs.IsEmpty ? fallback : runs[^1].Style);

        var natural = line.Height;
        if (!float.IsNaN(_lineHeight) && natural > 0f)
        {
            var stretch = _lineHeight / natural;
            line.Ascent *= stretch;
            line.Descent *= stretch;
            line.Gap *= stretch;
        }
    }

    void Measure(ref PendingLine line, in TextStyle style)
    {
        var metrics = style.Font.GetMetrics(style.Size * _scale);
        line.Ascent = MathF.Max(line.Ascent, metrics.Ascent / _scale);
        line.Descent = MathF.Max(line.Descent, metrics.Descent / _scale);
        line.Gap = MathF.Max(line.Gap, metrics.LineGap / _scale);
    }

    int TrimLines(Scratch work, ReadOnlySpan<char> text, ReadOnlySpan<TextRun> runs)
    {
        var kept = work.Pending.Count;
        if (_trimming == TextTrimming.None)
            return kept;

        if (float.IsFinite(_maxHeight))
        {
            var height = 0f;
            kept = 0;
            while (kept < work.Pending.Count)
            {
                var lineHeight = work.Pending[kept].Height;
                if (kept > 0 && height + lineHeight > _maxHeight + Tolerance)
                    break;
                height += lineHeight;
                kept++;
            }
        }

        for (var i = 0; i < kept; i++)
        {
            ref var line = ref work.Pending[i];
            var cut = i == kept - 1 && kept < work.Pending.Count;
            if (cut || line.Width > _maxWidth + Tolerance)
                Trim(work, ref line, text, runs);
        }
        return kept;
    }

    void Trim(
        Scratch work,
        ref PendingLine line,
        ReadOnlySpan<char> text,
        ReadOnlySpan<TextRun> runs
    )
    {
        var run = RunAt(runs, line.End > line.Start ? line.End - 1 : line.Start);
        var end = Cut(work, ref line, text, runs[run].Style);
        var cutRun = RunAt(runs, end > line.Start ? end - 1 : line.Start);
        if (cutRun != run)
        {
            run = cutRun;
            end = Cut(work, ref line, text, runs[run].Style);
        }

        line.End = end;
        line.Width = Width(line.Start, end) + line.EllipsisAdvance * line.EllipsisCount;
        line.EllipsisRun = run;
    }

    int Cut(Scratch work, ref PendingLine line, ReadOnlySpan<char> text, in TextStyle style)
    {
        var pixelSize = style.Size * _scale;
        var count = 1;
        var font = style.Font.Collection.Resolve(style.Font, new Rune('\u2026'), out var glyph);
        if (glyph == 0)
        {
            font = style.Font.Collection.Resolve(style.Font, new Rune('.'), out glyph);
            count = 3;
        }

        var advance = font.GetAdvance(glyph, pixelSize) / _scale + style.SpacingAdvance;
        var available = _maxWidth + Tolerance - advance * count;
        var end = line.End;
        if (Width(line.Start, end) > available)
        {
            var word =
                _trimming == TextTrimming.WordEllipsis
                    ? WordCut(work, line.Start, end, available)
                    : -1;
            end = word >= 0 ? word : CharacterCut(text, line.Start, end, available);
        }

        while (end > line.Start && char.IsWhiteSpace(text[end - 1]))
            end--;

        line.EllipsisFont = font;
        line.EllipsisGlyph = glyph;
        line.EllipsisCount = count;
        line.EllipsisAdvance = advance;
        return end;
    }

    int WordCut(Scratch work, int start, int end, float available)
    {
        var cut = -1;
        for (var i = 0; i < work.Breaks.Count; i++)
        {
            var measure = work.Breaks[i].PositionMeasure;
            if (measure <= start)
                continue;
            if (measure >= end || Width(start, measure) > available)
                break;
            cut = measure;
        }
        return cut;
    }

    int CharacterCut(ReadOnlySpan<char> text, int start, int end, float available)
    {
        var cut = start;
        while (cut < end)
        {
            var next = TextClusters.Next(text, cut, end);
            if (Width(start, next) > available)
                break;
            cut = next;
        }
        return cut;
    }

    int FitClusters(ReadOnlySpan<char> text, int start, int limitEnd, float limit)
    {
        var end = TextClusters.Next(text, start, limitEnd);
        while (end < limitEnd)
        {
            var next = TextClusters.Next(text, end, limitEnd);
            if (Width(start, next) > limit)
                break;
            end = next;
        }
        return end;
    }

    void Emit(Scratch work, int kept)
    {
        var widest = 0f;
        for (var i = 0; i < kept; i++)
            widest = MathF.Max(widest, work.Pending[i].Width);

        var span = float.IsFinite(_maxWidth) ? _maxWidth : widest;
        SharedArrays<TextLine>.Reserve(ref _lines, kept);
        SharedArrays<LayoutGlyph>.Reserve(ref _glyphs, _shapedCount + kept * 3);
        _glyphCount = 0;
        _lineCount = 0;
        var top = 0f;
        for (var i = 0; i < kept; i++)
        {
            ref readonly var line = ref work.Pending[i];
            var x = _alignment switch
            {
                TextAlignment.Center => (span - line.Width) * 0.5f,
                TextAlignment.Right => span - line.Width,
                _ => 0f,
            };
            var baseline = top + line.Ascent;
            var left = _pen[line.Start];
            for (var s = work.FirstShaped[line.Start]; s < work.FirstShaped[line.End]; s++)
            {
                ref readonly var shaped = ref work.Shaped[s];
                if (shaped.IsDrawn)
                    _glyphs[_glyphCount++] = new LayoutGlyph(
                        shaped.Font,
                        shaped.Glyph,
                        x + shaped.X - left,
                        baseline,
                        shaped.Run
                    );
            }

            if (line.EllipsisFont is { } ellipsisFont)
            {
                var ellipsisX = x + Width(line.Start, line.End);
                for (var k = 0; k < line.EllipsisCount; k++)
                    _glyphs[_glyphCount++] = new LayoutGlyph(
                        ellipsisFont,
                        line.EllipsisGlyph,
                        ellipsisX + k * line.EllipsisAdvance,
                        baseline,
                        line.EllipsisRun
                    );
            }

            _lines[_lineCount++] = new TextLine(
                line.Start,
                line.Wrap - line.Start,
                (line.EllipsisCount > 0 ? line.End : line.Wrap) - line.Start,
                x,
                top,
                baseline,
                line.Height
            )
            {
                MeasuredLength = line.End - line.Start,
            };
            top += line.Height;
        }

        Size = new Size(widest, top);
    }

    float Width(int start, int end) => _pen[end] - _pen[start];

    static int RunAt(ReadOnlySpan<TextRun> runs, int index)
    {
        for (var i = runs.Length - 1; i >= 0; i--)
        {
            if (runs[i].Start <= index)
                return i;
        }
        return 0;
    }

    static CharKind Classify(Rune rune)
    {
        var value = rune.Value;
        if (value == '\t')
            return CharKind.Tab;

        if (
            value < 0x20
            || value is >= 0x7F and <= 0x9F or 0x2028 or 0x2029
            || IsDefaultIgnorable(value)
        )
            return CharKind.Ignored;

        return Rune.GetUnicodeCategory(rune) == UnicodeCategory.SpaceSeparator
            ? CharKind.Blank
            : CharKind.Drawn;
    }

    static bool IsDefaultIgnorable(int value) =>
        value is 0x00AD or 0x034F or 0x061C or 0x3164 or 0xFEFF or 0xFFA0
        || value is >= 0x115F and <= 0x1160
        || value is >= 0x17B4 and <= 0x17B5
        || value is >= 0x180B and <= 0x180F
        || value is >= 0x200B and <= 0x200F
        || value is >= 0x202A and <= 0x202E
        || value is >= 0x2060 and <= 0x206F
        || value is >= 0xFE00 and <= 0xFE0F
        || value is >= 0xFFF0 and <= 0xFFF8
        || value is >= 0x1BCA0 and <= 0x1BCA3
        || value is >= 0x1D173 and <= 0x1D17A
        || value is >= 0xE0000 and <= 0xE0FFF;

    void Set<T>(ref T field, T value)
    {
        _dirty |= !EqualityComparer<T>.Default.Equals(field, value);
        field = value;
    }

    enum CharKind : byte
    {
        Drawn,
        Blank,
        Tab,
        Ignored,
    }

    readonly struct Shaped(Font font, int glyph, int run, float x, float pixelSize, bool isDrawn)
    {
        public Font Font { get; } = font;

        public int Glyph { get; } = glyph;

        public int Run { get; } = run;

        public float X { get; } = x;

        public float PixelSize { get; } = pixelSize;

        public bool IsDrawn { get; } = isDrawn;
    }

    struct PendingLine
    {
        public int Start;
        public int End;
        public int Wrap;
        public float Width;
        public float Ascent;
        public float Descent;
        public float Gap;
        public Font? EllipsisFont;
        public int EllipsisGlyph;
        public int EllipsisCount;
        public float EllipsisAdvance;
        public int EllipsisRun;

        public readonly float Height => Ascent + Descent + Gap;
    }

    sealed class Scratch
    {
        public Shaped[] Shaped = new Shaped[16];
        public int[] FirstShaped = new int[17];
        public ValueList<Unicode.LineBreak> Breaks = new(8);
        public ValueList<PendingLine> Pending = new(4);
    }

    readonly struct QuadSource(RenderTexture page, int run, bool isStroke)
    {
        public RenderTexture Page { get; } = page;

        public int Run { get; } = run;

        public bool IsStroke { get; } = isStroke;

        public bool Joins(in QuadSource other) =>
            ReferenceEquals(Page, other.Page) && Run == other.Run && IsStroke == other.IsStroke;
    }
}

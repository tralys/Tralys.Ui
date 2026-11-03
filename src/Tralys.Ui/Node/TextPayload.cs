using System.Diagnostics;
using System.Numerics;
using Tralys.Ui.Fonts;
using Tralys.Ui.Rendering;

namespace Tralys.Ui;

[FramePath]
internal record struct TextProps
{
    public float StrokeWidth;
    public Paint Stroke;
    public bool Underline;
    public bool Wrap;
    public TextTrimming Trimming;
    public int MaxLines;
    public Paint HighlightColor;
    public Paint HighlightBackground;
    public DropShadow? Shadow;

    /// <summary>What <c>Equals</c> says, without copying either.</summary>
    public readonly bool Same(in TextProps other) =>
        StrokeWidth.Equals(other.StrokeWidth)
        && Stroke == other.Stroke
        && Nullable.Equals(Shadow, other.Shadow)
        && Underline == other.Underline
        && Wrap == other.Wrap
        && Trimming == other.Trimming
        && MaxLines == other.MaxLines
        && HighlightColor == other.HighlightColor
        && HighlightBackground == other.HighlightBackground;
}

[FramePath]
internal sealed class TextPayload : NodePayload
{
    internal readonly TextBuffer Buffer;
    readonly TextLayout _layout = new();
    internal TextProps Props;
    internal Node? Owner;
    float _arranged = float.PositiveInfinity;
    float _lineHeight;
    TextAlign _align;
    int _maxContentVersion = -1;
    float _maxContentWidth;
    float _minContentWidth;
    float _scale;

    internal bool Rich;
    internal bool HasLinks;
    SharedList<Leaf> _leaves;
    SharedList<bool> _runUnderline;
    bool _highlighting;
    bool _contentDirty;
    char[] _source = [];
    int _sourceLength;
    char[] _query = [];
    int _queryLength;
    SharedList<int> _matches;
    Rect[] _rects = [];

    TextPayload(TextStyle style) => Buffer = new TextBuffer(style);

    internal static TextPayload Rent(UiView view, Node node)
    {
        var payload = Pool<TextPayload>.TryRent();
        if (payload is null)
        {
            var text = view.Context.Text;
            payload = new TextPayload(
                new TextStyle(text.Resolve(null, FontWeight.Normal, FontStyle.Normal), 14f, default)
            );
        }
        payload.Owner = node;
        node.Payload = payload;
        return payload;
    }

    public override void Release(UiView view)
    {
        _layout.Release();
        Buffer.Release();
        Props = default;
        Owner = null;
        _arranged = float.PositiveInfinity;
        _maxContentVersion = -1;
        Rich = HasLinks = _highlighting = _contentDirty = false;
        _leaves.Release();
        _runUnderline.Release();
        _sourceLength = _queryLength = 0;
        SharedArrays<char>.Release(ref _source);
        SharedArrays<char>.Release(ref _query);
        _matches.Release();
        SharedArrays<Rect>.Release(ref _rects);
        Pool<TextPayload>.Return(this);
    }

    internal void Invalidate() => _maxContentVersion = -1;

    internal bool SetText(
        UiView view,
        Node node,
        ReadOnlySpan<char> text,
        ReadOnlySpan<char> highlight = default
    )
    {
        Owner = node;
        var wasRich = Rich;
        if (wasRich)
        {
            Rich = HasLinks = false;
            _leaves.Clear();
            _runUnderline.Clear();
        }
        var shown = _highlighting ? _source.AsSpan(0, _sourceLength) : Buffer.Text;
        var textChanged = wasRich || !text.SequenceEqual(shown);
        if (!textChanged && highlight.SequenceEqual(_query.AsSpan(0, _queryLength)))
            return false;

        Store(ref _query, ref _queryLength, highlight);
        _highlighting = !highlight.IsEmpty;
        if (_highlighting)
        {
            Store(ref _source, ref _sourceLength, text);
            _contentDirty = true;
        }
        else
            Buffer.SetText(text);
        if (textChanged)
            view.InvalidateMeasure(node);
        view.MarkPaint(node);
        return true;
    }

    internal bool SetRich(UiView view, Node node)
    {
        Owner = node;
        if (Rich)
            return false;
        Rich = true;
        _highlighting = false;
        _queryLength = 0;
        _leaves.Clear();
        _runUnderline.Clear();
        Buffer.Clear();
        view.InvalidateMeasure(node);
        view.MarkPaint(node);
        return true;
    }

    static void Store(ref char[] into, ref int length, ReadOnlySpan<char> text)
    {
        SharedArrays<char>.Reserve(ref into, text.Length);
        text.CopyTo(into);
        length = text.Length;
    }

    static bool Same(in TextProps stored, in TextProps written)
    {
        var same = stored.Same(written);
        Debug.Assert(same == stored.Equals(written));
        return same;
    }

    internal bool WriteProps(UiView view, Node node, in TextProps props)
    {
        if (Same(Props, props))
            return false;
        var flows =
            Props.Wrap != props.Wrap
            || Props.Trimming != props.Trimming
            || Props.MaxLines != props.MaxLines
            || Props.StrokeWidth != props.StrokeWidth;
        if (Props.HighlightColor != props.HighlightColor)
            _contentDirty |= _highlighting;
        if (!Nullable.Equals(Props.Shadow, props.Shadow))
            view.MarkComposite();
        Props = props;
        if (flows)
        {
            node.Dirty |= Dirty.Env;
            view.MarkEnvBelow(node);
            view.InvalidateMeasure(node);
        }
        view.MarkPaint(node);
        return true;
    }

    internal void Restyle(UiView view, Node node)
    {
        ref readonly var env = ref node.Env;
        var font = view.Context.Text.Resolve(
            env.FontFamily,
            env.FontWeight == 0 ? FontWeight.Normal : env.FontWeight,
            env.FontStyle
        );
        var style = new TextStyle(
            font,
            env.FontSize > 0f ? env.FontSize : 14f,
            env.Color,
            Props.StrokeWidth,
            Props.Stroke,
            env.TabularDigits,
            (int)MathF.Round(env.LetterSpacing * 1000f)
        );
        var current = Buffer.Style;
        var lays =
            current.LaysOutLike(style) && _lineHeight == env.LineHeight && _align == env.TextAlign;
        var paints =
            current.Fill == style.Fill
            && current.Stroke == style.Stroke
            && current.StrokeWidth == style.StrokeWidth;
        _contentDirty |= _highlighting;
        if (lays && paints && !_highlighting)
            return;
        Buffer.Style = style;
        _lineHeight = env.LineHeight;
        _align = env.TextAlign;
        if (!lays)
        {
            _maxContentVersion = -1;
            view.InvalidateMeasure(node);
        }
        view.MarkPaint(node);
    }

    bool EnsureContent()
    {
        if (Owner is not { } owner)
            return false;
        if (Rich)
            return SyncRich(owner);
        if (!_contentDirty)
            return false;
        _contentDirty = false;
        if (_highlighting)
            FillHighlighted(owner);
        return true;
    }

    bool SyncRich(Node owner)
    {
        var index = 0;
        var structural = false;
        var restyled = false;
        Compare(owner, Props.Underline, ref index, ref structural, ref restyled);
        if (structural || index != _leaves.Count)
        {
            Buffer.Clear();
            _leaves.Clear();
            _runUnderline.Clear();
            HasLinks = false;
            Fill(owner, Props.Underline);
            return true;
        }
        if (!restyled)
            return false;
        for (var i = 0; i < _leaves.Count; i++)
        {
            ref var leaf = ref _leaves[i];
            var span = leaf.Payload;
            if (leaf.Version == span.Version && leaf.Underline == span.Underlined)
                continue;
            leaf.Version = span.Version;
            leaf.Underline = span.Underlined;
            if (span.RunIndex < 0)
                continue;
            if (span.Styled)
                Buffer.Restyle(span.RunIndex, span.Style);
            _runUnderline[span.RunIndex] = span.Underlined;
        }
        return true;
    }

    void Compare(Node parent, bool underline, ref int index, ref bool structural, ref bool restyled)
    {
        for (var child = parent._first; child is not null && !structural; child = child._next)
        {
            if (child.IsTransparent)
            {
                Compare(child, underline, ref index, ref structural, ref restyled);
                continue;
            }
            if (child.Payload is not SpanPayload span)
                continue;
            var under = underline || span.Props.Underline || span.Props.IsLink;
            if (span.Props.HasText)
            {
                if (index >= _leaves.Count || !ReferenceEquals(_leaves[index].Payload, span))
                {
                    structural = true;
                    return;
                }
                ref var leaf = ref _leaves[index];
                if (leaf.Version != span.Version || leaf.Underline != under)
                {
                    var shown = Buffer.Text;
                    if (
                        leaf.Link != span.Props.IsLink
                        || span.Start + span.Text.Length > shown.Length
                        || !span.Text.SequenceEqual(shown.Slice(span.Start, span.Text.Length))
                        || leaf.Length != span.Text.Length
                    )
                    {
                        structural = true;
                        return;
                    }
                    span.Underlined = under;
                    restyled = true;
                }
                index++;
            }
            Compare(child, under, ref index, ref structural, ref restyled);
        }
    }

    void Fill(Node parent, bool underline)
    {
        for (var child = parent._first; child is not null; child = child._next)
        {
            if (child.IsTransparent)
            {
                Fill(child, underline);
                continue;
            }
            if (child.Payload is not SpanPayload span)
                continue;
            var under = underline || span.Props.Underline || span.Props.IsLink;
            if (span.Props.HasText)
            {
                var runs = Buffer.Runs.Length;
                span.Start = Buffer.Text.Length;
                span.Underlined = under;
                Buffer.Append(span.Text, span.Styled ? span.Style : Buffer.Style);
                span.RunIndex = Buffer.Runs.Length > runs ? runs : -1;
                if (span.RunIndex >= 0)
                    _runUnderline.Add(under);
                HasLinks |= span.Props.IsLink;
                _leaves.Add(
                    new Leaf(span, child, span.Version, under, span.Text.Length, span.Props.IsLink)
                );
            }
            Fill(child, under);
        }
    }

    void FillHighlighted(Node owner)
    {
        var text = _source.AsSpan(0, _sourceLength);
        var query = _query.AsSpan(0, _queryLength);
        var plain = Buffer.Style;
        var fill =
            Props.HighlightColor.Kind != PaintKind.None ? Props.HighlightColor
            : owner.Env.Theme is { } theme ? theme.Color(Theming.ColorToken.FgFilledWarning)
            : plain.Fill;
        var marked = plain.WithFill(fill);
        _matches.Clear();
        Buffer.Clear();
        var at = 0;
        while (at < text.Length)
        {
            var found = text[at..].IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                Buffer.Append(text[at..], plain);
                break;
            }
            found += at;
            if (found > at)
                Buffer.Append(text[at..found], plain);
            Buffer.Append(text.Slice(found, query.Length), marked);
            _matches.Add(found);
            _matches.Add(found + query.Length);
            at = found + query.Length;
        }
    }

    TextTrimming Trimming =>
        Props.MaxLines > 0 && Props.Trimming == TextTrimming.None
            ? TextTrimming.CharacterEllipsis
            : Props.Trimming;

    bool Bounded => Props.Wrap || Trimming != TextTrimming.None;

    // Unbounded left-aligned lines sit the same at any width, so drawing keeps the max-content layout.
    float Drawn(float width) =>
        Bounded || _align is TextAlign.Center or TextAlign.Right ? width : float.PositiveInfinity;

    void Lay(TextLayout layout, float maxWidth, float scale)
    {
        layout.Scale = scale;
        layout.Wrapping = Props.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        layout.Trimming = Trimming;
        var size = Buffer.Style.Size;
        var lineHeight =
            _lineHeight > 0f ? _lineHeight
            : Props.MaxLines > 0 ? MathF.Ceiling(1.25f * size)
            : float.NaN;
        layout.LineHeight = lineHeight;
        layout.MaxHeight =
            Props.MaxLines > 0 ? Props.MaxLines * lineHeight : float.PositiveInfinity;
        layout.Alignment = _align switch
        {
            TextAlign.Center => TextAlignment.Center,
            TextAlign.Right => TextAlignment.Right,
            _ => TextAlignment.Left,
        };
        layout.MaxWidth = maxWidth;
        layout.Update(Buffer);
    }

    static float Ceil(float value, float scale) => MathF.Ceiling(value * scale - 1e-3f) / scale;

    void EnsureContentWidths(float scale)
    {
        if (_maxContentVersion == Buffer.LayoutVersion && _scale == scale)
            return;
        Lay(_layout, float.PositiveInfinity, scale);
        _maxContentWidth = Ceil(_layout.Size.Width, scale);
        _minContentWidth =
            Trimming != TextTrimming.None ? 0f
            : Props.Wrap ? MathF.Min(_maxContentWidth, Ceil(_layout.MinContentWidth, scale))
            : _maxContentWidth;
        _maxContentVersion = Buffer.LayoutVersion;
        _scale = scale;
    }

    internal Size Measure(UiView view, Avail width)
    {
        if (EnsureContent())
            view.MarkPaint(Owner!);
        var scale = view.Scale;
        EnsureContentWidths(scale);
        switch (width.Kind)
        {
            case AvailKind.MaxContent:
                Lay(_layout, float.PositiveInfinity, scale);
                return new Size(_maxContentWidth, _layout.Size.Height);
            case AvailKind.MinContent:
                Lay(_layout, Bounded ? _minContentWidth : float.PositiveInfinity, scale);
                return new Size(_minContentWidth, _layout.Size.Height);
            case AvailKind.Exact:
                Lay(_layout, Bounded ? width.Value : float.PositiveInfinity, scale);
                return new Size(width.Value, _layout.Size.Height);
            default:
                Lay(_layout, Bounded ? width.Value : float.PositiveInfinity, scale);
                var natural = _layout.Size.Width;
                return new Size(
                    MathF.Min(Ceil(natural, scale), MathF.Max(natural, width.Value)),
                    _layout.Size.Height
                );
        }
    }

    internal float Baseline(UiView view, float width)
    {
        EnsureContent();
        Lay(_layout, Bounded ? width : float.PositiveInfinity, view.Scale);
        var lines = _layout.Lines;
        return lines.Length > 0 ? lines[0].Baseline : _layout.Size.Height;
    }

    internal void Arrange(UiView view, float width)
    {
        _arranged = width;
        if (EnsureContent())
            view.MarkPaint(Owner!);
        Lay(_layout, Drawn(width), view.Scale);
        if (HasLinks)
            PlaceLinks(Owner!);
    }

    void PlaceLinks(Node owner)
    {
        for (var i = 0; i < _leaves.Count; i++)
        {
            var leaf = _leaves[i];
            if (!leaf.Payload.Props.IsLink)
                continue;
            var count = Rects(leaf.Payload);
            var union = default(Rect);
            for (var r = 0; r < count; r++)
                union = r == 0 ? _rects[r] : union.Union(_rects[r]);
            leaf.Node._bounds = union;
            leaf.Node._viewBounds = union.TransformedBy(owner.SnappedToView);
        }
    }

    int Rects(SpanPayload span)
    {
        SharedArrays<Rect>.Reserve(ref _rects, _layout.Lines.Length);
        return _layout.GetTextRects(span.Start, span.Start + span.Text.Length, _rects);
    }

    internal Node? LinkAt(UiView view, Node node, Vector2 point)
    {
        EnsureContent();
        if (!HasLinks)
            return null;
        Lay(_layout, Drawn(_arranged), view.Scale);
        for (var i = _leaves.Count - 1; i >= 0; i--)
        {
            var leaf = _leaves[i];
            if (!leaf.Payload.Props.IsLink || leaf.Node.Env.PointerEvents == PointerEvents.None)
                continue;
            var count = Rects(leaf.Payload);
            for (var r = 0; r < count; r++)
            {
                if (_rects[r].Contains(point))
                    return leaf.Node;
            }
        }
        return null;
    }

    internal void Record(DrawingContext dc, UiView view)
    {
        EnsureContent();
        var scale = view.Scale;
        Lay(_layout, Drawn(_arranged), scale);
        var theme = Owner?.Env.Theme;
        if (_highlighting)
            DrawHighlights(dc, theme);
        _layout.Draw(dc, view.Context.Text.Atlas);
        Underline(dc, scale);
        if (HasLinks && theme is not null)
            DrawFocusRings(dc, theme);
    }

    void DrawHighlights(DrawingContext dc, Theming.Theme? theme)
    {
        var paint =
            Props.HighlightBackground.Kind != PaintKind.None ? Props.HighlightBackground
            : theme is not null ? (Paint)theme.Color(Theming.ColorToken.BgWarning)
            : default;
        if (!paint.IsVisible)
            return;
        SharedArrays<Rect>.Reserve(ref _rects, _layout.Lines.Length);
        for (var i = 0; i + 1 < _matches.Count; i += 2)
        {
            var count = _layout.GetTextRects(_matches[i], _matches[i + 1], _rects);
            for (var r = 0; r < count; r++)
                dc.DrawRectangle(paint, _rects[r]);
        }
    }

    void DrawFocusRings(DrawingContext dc, Theming.Theme theme)
    {
        for (var i = 0; i < _leaves.Count; i++)
        {
            var leaf = _leaves[i];
            if (!leaf.Payload.Props.IsLink || (leaf.Node._states & InputStates.FocusVisible) == 0)
                continue;
            Paint ring = theme.Color(Theming.ColorToken.BorderFocus);
            var count = Rects(leaf.Payload);
            for (var r = 0; r < count; r++)
                dc.DrawBox(
                    _rects[r].Inflate(2f, 2f),
                    new CornerRadius(2f),
                    new Thickness(2f),
                    default,
                    ring
                );
        }
    }

    bool Underlined(int run) =>
        Rich ? run < _runUnderline.Count && _runUnderline[run] : Props.Underline;

    void Underline(DrawingContext dc, float scale)
    {
        if (!Props.Underline && _runUnderline.Count == 0)
            return;
        var glyphs = _layout.Glyphs;
        foreach (ref readonly var line in _layout.Lines)
        {
            var run = -1;
            var left = float.PositiveInfinity;
            var right = float.NegativeInfinity;
            foreach (ref readonly var glyph in glyphs)
            {
                if (glyph.Y != line.Baseline)
                    continue;
                if (glyph.Run != run)
                {
                    Rule(dc, scale, run, line.Baseline, left, right);
                    run = glyph.Run;
                    left = float.PositiveInfinity;
                    right = float.NegativeInfinity;
                }
                if (!Underlined(run))
                    continue;
                var style = Buffer.Runs[run].Style;
                left = MathF.Min(left, glyph.X);
                right = MathF.Max(
                    right,
                    glyph.X + glyph.Font.GetAdvance(glyph.Glyph, style.Size * scale) / scale
                );
            }
            Rule(dc, scale, run, line.Baseline, left, right);
        }
    }

    void Rule(DrawingContext dc, float scale, int run, float baseline, float left, float right)
    {
        if (run < 0 || right <= left)
            return;
        var style = Buffer.Runs[run].Style;
        var metrics = style.Font.GetMetrics(style.Size * scale);
        var thickness = MathF.Max(metrics.UnderlineThickness / scale, 1f / scale);
        dc.DrawRectangle(
            style.Fill,
            new Rect(left, baseline + metrics.UnderlinePosition / scale, right - left, thickness)
        );
    }

    record struct Leaf(
        SpanPayload Payload,
        Node Node,
        int Version,
        bool Underline,
        int Length,
        bool Link
    );
}

[FramePath]
public delegate void CanvasDraw<in TState>(DrawingContext dc, TState state, CanvasInfo info);

[FramePath]
public delegate bool CanvasHit<in TState>(TState state, Vector2 point, Size size);

/// <summary>What a canvas draws into: its content box from (0, 0) to <see cref="Size"/>.</summary>
[FramePath]
public readonly ref struct CanvasInfo
{
    readonly CanvasPaths _paths;

    internal CanvasInfo(Size size, float scale, CanvasPaths paths)
    {
        Size = size;
        Scale = scale;
        _paths = paths;
    }

    public Size Size { get; }

    /// <summary>Device pixels per layout unit.</summary>
    public float Scale { get; }

    /// <summary>A cleared scratch path, filled <see cref="FillRule.EvenOdd"/> until set; each call in one draw gives
    /// another, so one can be combined with another. Drawing a path copies it.</summary>
    public PathBuilder Path() => _paths.Next();
}

[FramePath]
internal abstract class CanvasPayload : NodePayload, Mvvm.IChangeObserver
{
    internal Mvvm.Dependencies _dependencies;
    internal Node? Node;
    internal UiView? View;

    public ref Mvvm.Dependencies Dependencies => ref _dependencies;

    public void OnChanged(object source, int property)
    {
        if (Node is { IsMounted: true } node)
            View!.MarkPaint(node);
    }

    internal void Attach(UiView view, Node node)
    {
        View = view;
        Node = node;
        if (!ReferenceEquals(_dependencies.Dispatcher, view.Context.Dispatcher))
            _dependencies = new Mvvm.Dependencies(view.Context.Dispatcher);
    }

    internal void Detach() => _dependencies.Clear();

    internal abstract void Draw(DrawingContext dc, Size size, float scale, CanvasPaths paths);

    internal abstract bool HasHitTest { get; }

    internal abstract bool HitTest(Vector2 point, Size size);
}

[FramePath]
internal sealed class CanvasPayload<TState> : CanvasPayload
{
    TState _state = default!;
    HeldText _held;
    CanvasDraw<TState>? _draw;
    CanvasHit<TState>? _hit;

    internal bool Write(TState state, CanvasDraw<TState> draw, CanvasHit<TState>? hit)
    {
        if (
            EqualityComparer<TState>.Default.Equals(_state, state)
            && ReferenceEquals(_draw, draw)
            && ReferenceEquals(_hit, hit)
        )
            return false;
        _state = _held.Hold(state);
        _draw = draw;
        _hit = hit;
        return true;
    }

    internal override void Draw(DrawingContext dc, Size size, float scale, CanvasPaths paths)
    {
        paths.Reset();
        var tracking = Mvvm.Observation.Track(this);
        try
        {
            _draw!(dc, _state, new CanvasInfo(size, scale, paths));
        }
        finally
        {
            tracking.Dispose();
        }
    }

    internal override bool HasHitTest => _hit is not null;

    internal override bool HitTest(Vector2 point, Size size) => _hit!(_state, point, size);

    public override void Release(UiView view)
    {
        _dependencies.Clear();
        _state = default!;
        _draw = null;
        _hit = null;
        Node = null;
        View = null;
        Pool<CanvasPayload<TState>>.Return(this);
    }
}

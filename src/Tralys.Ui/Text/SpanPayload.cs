using Tralys.Ui.Fonts;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui;

[FramePath]
internal record struct SpanProps
{
    public bool HasText;
    public bool Underline;
    public bool IsLink;
    public bool ColorSet;
}

[FramePath]
internal sealed class SpanPayload : NodePayload
{
    char[] _chars = new char[16];
    int _length;
    internal SpanProps Props;
    internal TextStyle Style;
    internal bool Styled;
    internal int Version;
    internal int Start;
    internal int RunIndex = -1;
    internal bool Underlined;
    internal Rect[] Rects = new Rect[2];

    internal ReadOnlySpan<char> Text => _chars.AsSpan(0, _length);

    internal static SpanPayload Rent() => Pool<SpanPayload>.TryRent() ?? new SpanPayload();

    public override void Release(UiView view)
    {
        SharedArrays<char>.Release(ref _chars);
        _length = 0;
        Props = default;
        Style = default;
        Styled = false;
        Version++;
        Start = 0;
        RunIndex = -1;
        Underlined = false;
        Pool<SpanPayload>.Return(this);
    }

    internal static Node? TextOf(Node node)
    {
        for (var n = node._parent; n is not null; n = n._parent)
        {
            if (n.Kind == NodeKind.Text)
                return n;
            if (n.Kind != NodeKind.Span && !n.IsTransparent)
                return null;
        }
        return null;
    }

    internal bool Write(UiView view, Node node, ReadOnlySpan<char> text, in SpanProps props)
    {
        var changed = false;
        if (Props != props)
        {
            var lays = Props.HasText == props.HasText && Props.IsLink == props.IsLink;
            var colors = Props.ColorSet == props.ColorSet && Props.IsLink == props.IsLink;
            Props = props;
            Version++;
            if (!lays)
                view.InvalidateMeasure(node);
            if (!colors && Styled)
                Restyle(view, node);
            changed = true;
        }
        if (!text.SequenceEqual(Text))
        {
            SharedArrays<char>.Reserve(ref _chars, text.Length);
            text.CopyTo(_chars);
            _length = text.Length;
            Version++;
            view.InvalidateMeasure(node);
            changed = true;
        }
        if (changed && TextOf(node) is { } owner)
            view.MarkPaint(owner);
        return changed;
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
            Fill(node),
            0f,
            default,
            env.TabularDigits,
            (int)MathF.Round(env.LetterSpacing * 1000f)
        );
        if (Styled && style.LaysOutLike(Style) && style.Fill == Style.Fill)
            return;
        var lays = !Styled || !style.LaysOutLike(Style);
        Style = style;
        Styled = true;
        Version++;
        if (lays)
            view.InvalidateMeasure(node);
        if (TextOf(node) is { } owner)
            view.MarkPaint(owner);
    }

    Paint Fill(Node node)
    {
        ref readonly var env = ref node.Env;
        if (!Props.IsLink || Props.ColorSet || env.Theme is not { } theme)
            return env.Color;
        var states = node._states;
        return theme.Color(
            (states & InputStates.Disabled) != 0 ? ColorToken.FgDisabled
            : (states & InputStates.Hovered) != 0 ? ColorToken.FgAccentHover
            : ColorToken.FgAccent
        );
    }

    internal void StatesChanged(UiView view, Node node, InputStates changed)
    {
        if (!Props.IsLink)
            return;
        if ((changed & (InputStates.Hovered | InputStates.Disabled)) != 0 && Styled)
            Restyle(view, node);
        if ((changed & InputStates.FocusVisible) != 0 && TextOf(node) is { } owner)
            view.MarkPaint(owner);
    }
}

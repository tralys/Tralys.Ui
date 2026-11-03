using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Fonts;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui;

/// <summary>Text formatted without allocating, and rich text: a <c>Text</c> scope whose children are spans and
/// links, laid out as one paragraph.</summary>
[FramePath]
public static class TextPrimitives
{
    /// <summary>A run of text formatted into the frame's arena, in the invariant culture, allocating nothing.</summary>
    public static void Text(
        this Ui ui,
        [InterpolatedStringHandlerArgument("ui")] ref UiFormat text,
        Paint color = default,
        string? fontFamily = null,
        float fontSize = 0,
        FontWeight fontWeight = default,
        FontStyle? fontStyle = null,
        float lineHeight = 0,
        float? letterSpacing = null,
        TextAlign textAlign = default,
        TextVariant? variant = null,
        bool? tabularDigits = null,
        float strokeWidth = 0,
        Paint stroke = default,
        DropShadow? shadow = null,
        bool underline = false,
        bool wrap = false,
        TextTrimming trimming = default,
        int maxLines = 0,
        TextValue highlight = default,
        Paint highlightColor = default,
        Paint highlightBackground = default,
        Callback onClick = default,
        float grow = 0,
        float? shrink = null,
        Length basis = default,
        AlignSelf alignSelf = default,
        AlignSelf justifySelf = default,
        Thickness margin = default,
        Position position = default,
        Length left = default,
        Length top = default,
        Length right = default,
        Length bottom = default,
        int zIndex = 0,
        int column = 0,
        int row = 0,
        int columnSpan = 0,
        int rowSpan = 0,
        bool hidden = false,
        bool collapsed = false,
        string? id = null,
        Length width = default,
        Length height = default,
        Length minWidth = default,
        Length minHeight = default,
        Length maxWidth = default,
        Length maxHeight = default,
        float aspectRatio = 0,
        float? opacity = null,
        Transform transform = default,
        Vector2? transformOrigin = null,
        Cursor? cursor = null,
        PointerEvents pointerEvents = default,
        NodeRef? nodeRef = null,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Text(
            text.ToText(),
            color: color,
            fontFamily: fontFamily,
            fontSize: fontSize,
            fontWeight: fontWeight,
            fontStyle: fontStyle,
            lineHeight: lineHeight,
            letterSpacing: letterSpacing,
            textAlign: textAlign,
            variant: variant,
            tabularDigits: tabularDigits,
            strokeWidth: strokeWidth,
            stroke: stroke,
            shadow: shadow,
            underline: underline,
            wrap: wrap,
            trimming: trimming,
            maxLines: maxLines,
            highlight: highlight,
            highlightColor: highlightColor,
            highlightBackground: highlightBackground,
            onClick: onClick,
            grow: grow,
            shrink: shrink,
            basis: basis,
            alignSelf: alignSelf,
            justifySelf: justifySelf,
            margin: margin,
            position: position,
            left: left,
            top: top,
            right: right,
            bottom: bottom,
            zIndex: zIndex,
            column: column,
            row: row,
            columnSpan: columnSpan,
            rowSpan: rowSpan,
            hidden: hidden,
            collapsed: collapsed,
            id: id,
            width: width,
            height: height,
            minWidth: minWidth,
            minHeight: minHeight,
            maxWidth: maxWidth,
            maxHeight: maxHeight,
            aspectRatio: aspectRatio,
            opacity: opacity,
            transform: transform,
            transformOrigin: transformOrigin,
            cursor: cursor,
            pointerEvents: pointerEvents,
            nodeRef: nodeRef,
            key: key,
            file: file,
            line: line
        );

    /// <summary>Rich text: its block holds spans and links, laid out as one paragraph that wraps across them. A span
    /// passes its own style to what it holds.</summary>
    public static Scope Text(
        this Ui ui,
        Paint color = default,
        string? fontFamily = null,
        float fontSize = 0,
        FontWeight fontWeight = default,
        FontStyle? fontStyle = null,
        float lineHeight = 0,
        float? letterSpacing = null,
        TextAlign textAlign = default,
        TextVariant? variant = null,
        bool? tabularDigits = null,
        float strokeWidth = 0,
        Paint stroke = default,
        DropShadow? shadow = null,
        bool underline = false,
        bool wrap = false,
        TextTrimming trimming = default,
        int maxLines = 0,
        float grow = 0,
        float? shrink = null,
        Length basis = default,
        AlignSelf alignSelf = default,
        AlignSelf justifySelf = default,
        Thickness margin = default,
        Position position = default,
        Length left = default,
        Length top = default,
        Length right = default,
        Length bottom = default,
        int zIndex = 0,
        int column = 0,
        int row = 0,
        int columnSpan = 0,
        int rowSpan = 0,
        bool hidden = false,
        bool collapsed = false,
        string? id = null,
        Length width = default,
        Length height = default,
        Length minWidth = default,
        Length minHeight = default,
        Length maxWidth = default,
        Length maxHeight = default,
        float aspectRatio = 0,
        float? opacity = null,
        Transform transform = default,
        Vector2? transformOrigin = null,
        Cursor? cursor = null,
        PointerEvents pointerEvents = default,
        NodeRef? nodeRef = null,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var b = ui.Builder;
        if (b.Discarding)
            return default;
        var depth = b.FrameDepth;
        var hooks = b.HookDepth;
        var node = b.Claim(NodeKind.Text, null, key, file, line, out _);
        var view = b.View;
        var payload = TextOf(view, node);
        var changed = payload.SetRich(view, node);
        changed |= payload.WriteProps(
            view,
            node,
            new TextProps
            {
                StrokeWidth = strokeWidth,
                Stroke = stroke,
                Shadow = shadow,
                Underline = underline,
                Wrap = wrap,
                Trimming = trimming,
                MaxLines = maxLines,
            }
        );
        Primitives.Write(
            view,
            node,
            new LayoutStyle
            {
                Width = width,
                Height = height,
                MinWidth = minWidth,
                MinHeight = minHeight,
                MaxWidth = maxWidth,
                MaxHeight = maxHeight,
                AspectRatio = aspectRatio,
            },
            new VisualStyle
            {
                Opacity = opacity,
                Transform = transform,
                TransformOrigin = transformOrigin,
            },
            new InheritStyle
            {
                Color = color,
                FontFamily = fontFamily,
                FontSize = fontSize,
                FontWeight = fontWeight,
                FontStyle = fontStyle,
                LineHeight = lineHeight,
                LetterSpacing = letterSpacing,
                TabularDigits = tabularDigits,
                TextAlign = textAlign,
                TextVariant = variant,
                PointerEvents = pointerEvents,
            },
            new InteractionProps { Cursor = cursor },
            new Placement
            {
                Grow = grow,
                Shrink = shrink,
                Basis = basis,
                AlignSelf = alignSelf,
                JustifySelf = justifySelf,
                Margin = margin,
                Position = position,
                Left = left,
                Top = top,
                Right = right,
                Bottom = bottom,
                ZIndex = zIndex,
                Column = column,
                Row = row,
                ColumnSpan = columnSpan,
                RowSpan = rowSpan,
                Hidden = hidden,
                Collapsed = collapsed,
                Id = id,
            },
            false,
            nodeRef,
            changed
        );
        b.Push(node);
        return new Scope(b, depth, hooks);
    }

    /// <summary>A run of rich text in its own style; unset style props inherit from what holds it. Outside a rich
    /// <c>Text</c> it opens a one-run text of its own.</summary>
    public static void Span(
        this Ui ui,
        TextValue text,
        Paint color = default,
        string? fontFamily = null,
        float fontSize = 0,
        FontWeight fontWeight = default,
        FontStyle? fontStyle = null,
        float? letterSpacing = null,
        bool underline = false,
        bool? tabularDigits = null,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var b = ui.Builder;
        if (b.Discarding)
            return;
        var depth = b.FrameDepth;
        var hooks = b.HookDepth;
        var opened = OpenText(b, key, file, line);
        WriteSpan(
            b,
            text.Span,
            new SpanProps { HasText = true, Underline = underline },
            new InheritStyle
            {
                Color = color,
                FontFamily = fontFamily,
                FontSize = fontSize,
                FontWeight = fontWeight,
                FontStyle = fontStyle,
                LetterSpacing = letterSpacing,
                TabularDigits = tabularDigits,
            },
            default,
            null,
            key,
            file,
            line
        );
        if (opened)
            b.Close(depth, hooks);
    }

    /// <summary>A style its block's spans and links take, unless they set their own.</summary>
    public static Scope Span(
        this Ui ui,
        Paint color = default,
        string? fontFamily = null,
        float fontSize = 0,
        FontWeight fontWeight = default,
        FontStyle? fontStyle = null,
        float? letterSpacing = null,
        bool underline = false,
        bool? tabularDigits = null,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var b = ui.Builder;
        if (b.Discarding)
            return default;
        var depth = b.FrameDepth;
        var hooks = b.HookDepth;
        OpenText(b, key, file, line);
        var node = WriteSpan(
            b,
            default,
            new SpanProps { Underline = underline },
            new InheritStyle
            {
                Color = color,
                FontFamily = fontFamily,
                FontSize = fontSize,
                FontWeight = fontWeight,
                FontStyle = fontStyle,
                LetterSpacing = letterSpacing,
                TabularDigits = tabularDigits,
            },
            default,
            null,
            key,
            file,
            line
        );
        b.Push(node);
        return new Scope(b, depth, hooks);
    }

    /// <summary>An underlined run that clicks: hit over its own glyphs, a Tab stop that Enter activates, with a focus
    /// ring around its glyphs. It draws in the theme's accent, its hover shade while hovered and the disabled colour
    /// while disabled, unless <paramref name="color"/> is set. <paramref name="onHoverChange"/> reports the pointer
    /// coming onto its glyphs and leaving them, and <paramref name="nodeRef"/> binds its node, so a tooltip anchored on
    /// it opens over the link. Outside a rich <c>Text</c> it opens a one-run text of its own.</summary>
    public static void Link(
        this Ui ui,
        TextValue text,
        Callback onClick = default,
        Paint color = default,
        FontWeight fontWeight = default,
        bool disabled = false,
        Callback<bool> onHoverChange = default,
        NodeRef? nodeRef = null,
        string? id = null,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var b = ui.Builder;
        if (b.Discarding)
            return;
        var depth = b.FrameDepth;
        var hooks = b.HookDepth;
        var opened = OpenText(b, key, file, line);
        WriteSpan(
            b,
            text.Span,
            new SpanProps
            {
                HasText = true,
                Underline = true,
                IsLink = true,
                ColorSet = color.Kind != PaintKind.None,
            },
            new InheritStyle
            {
                Color = color,
                FontWeight = fontWeight,
                Disabled = disabled,
            },
            new InteractionProps
            {
                OnClick = onClick,
                OnHoverChange = onHoverChange,
                Focusable = true,
                Cursor = Cursors.Hand,
            },
            id,
            key,
            file,
            line,
            nodeRef
        );
        if (opened)
            b.Close(depth, hooks);
    }

    static TextPayload TextOf(UiView view, Node node) =>
        node.Payload as TextPayload ?? TextPayload.Rent(view, node);

    static bool InRichText(Builder b)
    {
        for (var n = b.OpenNode; n is not null; n = n._parent)
        {
            if (n.Kind == NodeKind.Span)
                return true;
            if (n.Kind == NodeKind.Text)
                return n.Payload is TextPayload { Rich: true };
            if (!n.IsTransparent)
                return false;
        }
        return false;
    }

    static bool OpenText(Builder b, UiKey key, string file, int line)
    {
        if (InRichText(b))
            return false;
        var node = b.Claim(NodeKind.Text, null, key, file, line, out _);
        var view = b.View;
        var payload = TextOf(view, node);
        var changed = payload.SetRich(view, node);
        changed |= payload.WriteProps(view, node, default);
        Primitives.Write(
            view,
            node,
            default,
            default,
            default,
            default,
            default,
            false,
            null,
            changed
        );
        b.Push(node);
        return true;
    }

    static Node WriteSpan(
        Builder b,
        ReadOnlySpan<char> text,
        in SpanProps props,
        in InheritStyle inherit,
        in InteractionProps interaction,
        string? id,
        UiKey key,
        string file,
        int line,
        NodeRef? nodeRef = null
    )
    {
        var node = b.Claim(NodeKind.Span, null, key, file, line, out _);
        var view = b.View;
        if (node.Payload is not SpanPayload payload)
        {
            payload = SpanPayload.Rent();
            node.Payload = payload;
        }
        var changed = payload.Write(view, node, text, props);
        Primitives.Write(
            view,
            node,
            default,
            default,
            inherit,
            interaction,
            new Placement { Id = id },
            false,
            nodeRef,
            changed
        );
        return node;
    }
}

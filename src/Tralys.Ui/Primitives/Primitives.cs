using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Fonts;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui;

/// <summary>The primitives every component is built from.</summary>
[FramePath]
public static class Primitives
{
    /// <summary>A flex container: a row or column of its children, with decoration, an inherited environment and
    /// input. Its block holds its children.</summary>
    public static Scope Box(
        this Ui ui,
        Direction direction = default,
        FlexWrap wrap = default,
        Align align = default,
        Justify justify = default,
        AlignContent alignContent = default,
        float gap = 0,
        float? rowGap = null,
        float? columnGap = null,
        Thickness padding = default,
        Overflow overflow = default,
        ScrollHandle? scroll = null,
        int quarterTurns = 0,
        Paint background = default,
        Border border = default,
        CornerRadius radius = default,
        BoxShadow shadow = default,
        Paint mask = default,
        Paint color = default,
        string? fontFamily = null,
        float fontSize = 0,
        FontWeight fontWeight = default,
        FontStyle? fontStyle = null,
        float lineHeight = 0,
        float? letterSpacing = null,
        bool? tabularDigits = null,
        TextAlign textAlign = default,
        TextVariant? textVariant = null,
        bool disabled = false,
        Theme? theme = null,
        Density? density = null,
        InputStates forceStates = default,
        Callback onClick = default,
        Callback onDoubleClick = default,
        Callback onLongPress = default,
        PressMode pressMode = default,
        PressKeys pressKeys = default,
        Handler<PointerEvent> onContextMenu = default,
        Handler<PointerEvent> onPreviewPointerDown = default,
        Handler<PointerEvent> onPointerDown = default,
        Handler<PointerEvent> onPointerMove = default,
        Handler<PointerEvent> onPointerUp = default,
        Handler<PointerEvent> onPointerCancel = default,
        Handler<DragEvent> onDrag = default,
        Callback<bool> onHoverChange = default,
        Handler<WheelEvent> onWheel = default,
        Handler<KeyEvent> onKeyDown = default,
        Handler<KeyEvent> onKeyUp = default,
        Handler<GamepadEvent> onGamepad = default,
        Callback<FocusChange> onFocusChange = default,
        bool? focusable = null,
        int tabIndex = 0,
        bool autoFocus = false,
        Navigation tabNavigation = default,
        Navigation arrowNavigation = default,
        bool modal = false,
        bool containsLayers = false,
        bool selected = false,
        UiAction action = default,
        bool excludeSystemGestures = false,
        object? dragData = null,
        DragEffect dragEffects = default,
        Slot dragPreview = default,
        Callback<DragEffect> onDragEnd = default,
        Handler<DropEvent> onDragOver = default,
        Callback onDragLeave = default,
        Handler<DropEvent> onDrop = default,
        Panning panning = default,
        bool dragToPan = false,
        ScrollHandle? followScroll = null,
        Callback<ScrollChange> onScrollChange = default,
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
        var node = b.Claim(NodeKind.Box, null, key, file, line, out _);
        var scrolled = node.WriteScroll(
            new ScrollProps
            {
                Panning = panning,
                DragToPan = dragToPan,
                FollowScroll = followScroll,
                OnScrollChange = onScrollChange,
            },
            scroll,
            overflow >= Overflow.ScrollX
        );
        var interacts =
            node.Interaction is not null
            || !onClick.IsEmpty
            || pressMode != default
            || pressKeys != default
            || !onPreviewPointerDown.IsEmpty
            || !onPointerDown.IsEmpty
            || !onPointerMove.IsEmpty
            || !onPointerUp.IsEmpty
            || !onPointerCancel.IsEmpty
            || !onHoverChange.IsEmpty
            || !onWheel.IsEmpty
            || !onKeyDown.IsEmpty
            || !onKeyUp.IsEmpty
            || !onFocusChange.IsEmpty
            || focusable is not null
            || modal
            || containsLayers
            || cursor is not null
            || !onDoubleClick.IsEmpty
            || !onLongPress.IsEmpty
            || !onContextMenu.IsEmpty
            || !onDrag.IsEmpty
            || !onGamepad.IsEmpty
            || tabIndex != 0
            || autoFocus
            || tabNavigation != default
            || arrowNavigation != default
            || action.Name is not null
            || excludeSystemGestures
            || dragData is not null
            || dragEffects != default
            || !dragPreview.IsEmpty
            || !onDragEnd.IsEmpty
            || !onDragOver.IsEmpty
            || !onDragLeave.IsEmpty
            || !onDrop.IsEmpty;
        var interacted =
            (interacts || Checked)
            && WriteInteraction(
                node,
                interacts,
                onClick,
                pressMode,
                pressKeys,
                onPreviewPointerDown,
                onPointerDown,
                onPointerMove,
                onPointerUp,
                onPointerCancel,
                onHoverChange,
                onWheel,
                onKeyDown,
                onKeyUp,
                onFocusChange,
                focusable,
                modal,
                containsLayers,
                cursor,
                onDoubleClick,
                onLongPress,
                onContextMenu,
                onDrag,
                onGamepad,
                tabIndex,
                autoFocus,
                tabNavigation,
                arrowNavigation,
                action,
                excludeSystemGestures,
                dragData,
                dragEffects,
                dragPreview,
                onDragEnd,
                onDragOver,
                onDragLeave,
                onDrop
            );
        Write(
            b.View,
            node,
            new LayoutStyle
            {
                Direction = direction,
                Wrap = wrap,
                Align = align,
                Justify = justify,
                AlignContent = alignContent,
                RowGap = rowGap ?? gap,
                ColumnGap = columnGap ?? gap,
                Padding = padding,
                Border = border.Width,
                Overflow = overflow,
                QuarterTurns = (byte)(quarterTurns & 3),
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
                Background = background,
                BorderPaint = border.Paint,
                Radius = radius,
                Shadow = shadow,
                Mask = mask,
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
                TextVariant = textVariant,
                PointerEvents = pointerEvents,
                Disabled = disabled,
                Theme = theme,
                Density = density,
                ForceStates = forceStates,
            },
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
            selected,
            nodeRef,
            scrolled | interacted
        );
        if (modal)
            b.View.ModalWritten(node);
        b.Push(node);
        return new Scope(b, depth, hooks);
    }

    internal static void PanFromPressables(this Ui ui)
    {
        var b = ui.Builder;
        if (!b.Discarding)
            b.OpenNode.PansFromPressables = true;
    }

    /// <summary>A grid of tracks; children place by <c>column</c>/<c>row</c> (from 1, 0 placing automatically) and
    /// span by <c>columnSpan</c>/<c>rowSpan</c>. With no tracks it is one cell its children overlay.</summary>
    public static Scope Grid(
        this Ui ui,
        GridTracks columns = default,
        GridTracks rows = default,
        float gap = 0,
        float? rowGap = null,
        float? columnGap = null,
        Align align = default,
        Align justifyItems = default,
        Thickness padding = default,
        Overflow overflow = default,
        ScrollHandle? scroll = null,
        int firstColumn = 0,
        Paint background = default,
        Border border = default,
        CornerRadius radius = default,
        BoxShadow shadow = default,
        Paint mask = default,
        Paint color = default,
        string? fontFamily = null,
        float fontSize = 0,
        FontWeight fontWeight = default,
        FontStyle? fontStyle = null,
        float lineHeight = 0,
        float? letterSpacing = null,
        bool? tabularDigits = null,
        TextAlign textAlign = default,
        TextVariant? textVariant = null,
        bool disabled = false,
        Theme? theme = null,
        Density? density = null,
        InputStates forceStates = default,
        Callback onClick = default,
        Callback onDoubleClick = default,
        Callback onLongPress = default,
        PressMode pressMode = default,
        PressKeys pressKeys = default,
        Handler<PointerEvent> onContextMenu = default,
        Handler<PointerEvent> onPreviewPointerDown = default,
        Handler<PointerEvent> onPointerDown = default,
        Handler<PointerEvent> onPointerMove = default,
        Handler<PointerEvent> onPointerUp = default,
        Handler<PointerEvent> onPointerCancel = default,
        Handler<DragEvent> onDrag = default,
        Callback<bool> onHoverChange = default,
        Handler<WheelEvent> onWheel = default,
        Handler<KeyEvent> onKeyDown = default,
        Handler<KeyEvent> onKeyUp = default,
        Handler<GamepadEvent> onGamepad = default,
        Callback<FocusChange> onFocusChange = default,
        bool? focusable = null,
        int tabIndex = 0,
        bool autoFocus = false,
        Navigation tabNavigation = default,
        Navigation arrowNavigation = default,
        bool modal = false,
        bool containsLayers = false,
        bool selected = false,
        UiAction action = default,
        bool excludeSystemGestures = false,
        object? dragData = null,
        DragEffect dragEffects = default,
        Slot dragPreview = default,
        Callback<DragEffect> onDragEnd = default,
        Handler<DropEvent> onDragOver = default,
        Callback onDragLeave = default,
        Handler<DropEvent> onDrop = default,
        Panning panning = default,
        bool dragToPan = false,
        ScrollHandle? followScroll = null,
        Callback<ScrollChange> onScrollChange = default,
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
        var node = b.Claim(NodeKind.Grid, null, key, file, line, out _);
        var scrolled = node.WriteScroll(
            new ScrollProps
            {
                Panning = panning,
                DragToPan = dragToPan,
                FollowScroll = followScroll,
                OnScrollChange = onScrollChange,
            },
            scroll,
            overflow >= Overflow.ScrollX
        );
        if (node.Payload is not GridPayload grid)
            node.Payload = grid = GridPayload.Rent();
        var tracked = grid.Write(
            b.View,
            node,
            new GridStyle
            {
                Columns = columns,
                Rows = rows,
                JustifyItems = justifyItems,
                FirstColumn = firstColumn,
            }
        );
        var interacts =
            node.Interaction is not null
            || !onClick.IsEmpty
            || pressMode != default
            || pressKeys != default
            || !onPreviewPointerDown.IsEmpty
            || !onPointerDown.IsEmpty
            || !onPointerMove.IsEmpty
            || !onPointerUp.IsEmpty
            || !onPointerCancel.IsEmpty
            || !onHoverChange.IsEmpty
            || !onWheel.IsEmpty
            || !onKeyDown.IsEmpty
            || !onKeyUp.IsEmpty
            || !onFocusChange.IsEmpty
            || focusable is not null
            || modal
            || containsLayers
            || cursor is not null
            || !onDoubleClick.IsEmpty
            || !onLongPress.IsEmpty
            || !onContextMenu.IsEmpty
            || !onDrag.IsEmpty
            || !onGamepad.IsEmpty
            || tabIndex != 0
            || autoFocus
            || tabNavigation != default
            || arrowNavigation != default
            || action.Name is not null
            || excludeSystemGestures
            || dragData is not null
            || dragEffects != default
            || !dragPreview.IsEmpty
            || !onDragEnd.IsEmpty
            || !onDragOver.IsEmpty
            || !onDragLeave.IsEmpty
            || !onDrop.IsEmpty;
        var interacted =
            (interacts || Checked)
            && WriteInteraction(
                node,
                interacts,
                onClick,
                pressMode,
                pressKeys,
                onPreviewPointerDown,
                onPointerDown,
                onPointerMove,
                onPointerUp,
                onPointerCancel,
                onHoverChange,
                onWheel,
                onKeyDown,
                onKeyUp,
                onFocusChange,
                focusable,
                modal,
                containsLayers,
                cursor,
                onDoubleClick,
                onLongPress,
                onContextMenu,
                onDrag,
                onGamepad,
                tabIndex,
                autoFocus,
                tabNavigation,
                arrowNavigation,
                action,
                excludeSystemGestures,
                dragData,
                dragEffects,
                dragPreview,
                onDragEnd,
                onDragOver,
                onDragLeave,
                onDrop
            );
        Write(
            b.View,
            node,
            new LayoutStyle
            {
                RowGap = rowGap ?? gap,
                ColumnGap = columnGap ?? gap,
                Align = align,
                Padding = padding,
                Border = border.Width,
                Overflow = overflow,
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
                Background = background,
                BorderPaint = border.Paint,
                Radius = radius,
                Shadow = shadow,
                Mask = mask,
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
                TextVariant = textVariant,
                PointerEvents = pointerEvents,
                Disabled = disabled,
                Theme = theme,
                Density = density,
                ForceStates = forceStates,
            },
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
            selected,
            nodeRef,
            scrolled | tracked | interacted
        );
        if (modal)
            b.View.ModalWritten(node);
        b.Push(node);
        return new Scope(b, depth, hooks);
    }

    /// <summary>A run of text in one style; unset style props inherit. <paramref name="highlight"/> marks each
    /// case-insensitive match, left to right.</summary>
    public static void Text(
        this Ui ui,
        TextValue text,
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
    )
    {
        var b = ui.Builder;
        if (b.Discarding)
            return;
        var node = b.Claim(NodeKind.Text, null, key, file, line, out _);
        var view = b.View;
        if (node.Payload is not TextPayload payload)
            payload = TextPayload.Rent(view, node);
        var changed = payload.SetText(view, node, text.Span, highlight.Span);
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
                HighlightColor = highlightColor,
                HighlightBackground = highlightBackground,
            }
        );
        Write(
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
            changed | node.WriteClick(onClick, cursor)
        );
    }

    /// <summary>Custom drawing: <paramref name="draw"/>, a static callback, records what the canvas shows from
    /// <paramref name="state"/>, again only when the state differs, the size changes or something it read while
    /// drawing changes.</summary>
    public static void Canvas<TState>(
        this Ui ui,
        TState state,
        CanvasDraw<TState> draw,
        CanvasHit<TState>? hitTest = null,
        Size intrinsicSize = default,
        Callback onClick = default,
        PressMode pressMode = default,
        Handler<PointerEvent> onPointerDown = default,
        Handler<PointerEvent> onPointerMove = default,
        Handler<PointerEvent> onPointerUp = default,
        Handler<PointerEvent> onPointerCancel = default,
        Callback<bool> onHoverChange = default,
        Callback<FocusChange> onFocusChange = default,
        bool? focusable = null,
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
        ArgumentNullException.ThrowIfNull(draw);
        var b = ui.Builder;
        if (b.Discarding)
            return;
        var node = b.Claim(NodeKind.Canvas, null, key, file, line, out _);
        var view = b.View;
        if (node.Payload is not CanvasPayload<TState> payload)
        {
            node.Payload?.Release(view);
            payload = Pool<CanvasPayload<TState>>.TryRent() ?? new CanvasPayload<TState>();
            payload.Attach(view, node);
            node.Payload = payload;
        }
        var changed = payload.Write(state, draw, hitTest);
        if (changed)
            view.MarkPaint(node);
        Write(
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
                IntrinsicSize = intrinsicSize,
            },
            new VisualStyle
            {
                Opacity = opacity,
                Transform = transform,
                TransformOrigin = transformOrigin,
            },
            new InheritStyle { PointerEvents = pointerEvents },
            new InteractionProps
            {
                OnClick = onClick,
                PressMode = pressMode,
                OnPointerDown = onPointerDown,
                OnPointerMove = onPointerMove,
                OnPointerUp = onPointerUp,
                OnPointerCancel = onPointerCancel,
                OnHoverChange = onHoverChange,
                OnFocusChange = onFocusChange,
                Focusable = focusable,
                Cursor = cursor,
            },
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
    }

#if DEBUG
    const bool Checked = true;
#else
    const bool Checked = false;
#endif

    // Kept out of line so a caller's frame never holds, or zeroes, the 680-byte block.
    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool WriteInteraction(
        Node node,
        bool interacts,
        Callback onClick,
        PressMode pressMode,
        PressKeys pressKeys,
        Handler<PointerEvent> onPreviewPointerDown,
        Handler<PointerEvent> onPointerDown,
        Handler<PointerEvent> onPointerMove,
        Handler<PointerEvent> onPointerUp,
        Handler<PointerEvent> onPointerCancel,
        Callback<bool> onHoverChange,
        Handler<WheelEvent> onWheel,
        Handler<KeyEvent> onKeyDown,
        Handler<KeyEvent> onKeyUp,
        Callback<FocusChange> onFocusChange,
        bool? focusable,
        bool modal,
        bool containsLayers,
        Cursor? cursor,
        Callback onDoubleClick,
        Callback onLongPress,
        Handler<PointerEvent> onContextMenu,
        Handler<DragEvent> onDrag,
        Handler<GamepadEvent> onGamepad,
        int tabIndex,
        bool autoFocus,
        Navigation tabNavigation,
        Navigation arrowNavigation,
        UiAction action,
        bool excludeSystemGestures,
        object? dragData,
        DragEffect dragEffects,
        Slot dragPreview,
        Callback<DragEffect> onDragEnd,
        Handler<DropEvent> onDragOver,
        Callback onDragLeave,
        Handler<DropEvent> onDrop
    )
    {
        var props = new InteractionProps
        {
            OnClick = onClick,
            PressMode = pressMode,
            PressKeys = pressKeys,
            OnPreviewPointerDown = onPreviewPointerDown,
            OnPointerDown = onPointerDown,
            OnPointerMove = onPointerMove,
            OnPointerUp = onPointerUp,
            OnPointerCancel = onPointerCancel,
            OnHoverChange = onHoverChange,
            OnWheel = onWheel,
            OnKeyDown = onKeyDown,
            OnKeyUp = onKeyUp,
            OnFocusChange = onFocusChange,
            Focusable = focusable,
            Modal = modal,
            ContainsLayers = containsLayers,
            Cursor = cursor,
            OnDoubleClick = onDoubleClick,
            OnLongPress = onLongPress,
            OnContextMenu = onContextMenu,
            OnDrag = onDrag,
            OnGamepad = onGamepad,
            TabIndex = tabIndex,
            AutoFocus = autoFocus,
            TabNavigation = tabNavigation,
            ArrowNavigation = arrowNavigation,
            Action = action,
            ExcludeSystemGestures = excludeSystemGestures,
            DragData = dragData,
            DragEffects = dragEffects,
            DragPreview = dragPreview,
            OnDragEnd = onDragEnd,
            OnDragOver = onDragOver,
            OnDragLeave = onDragLeave,
            OnDrop = onDrop,
        };
        // An argument the callers' check leaves out would be dropped from every node it alone is set on.
        Debug.Assert(interacts || props == default);
        return node.WriteInteraction(props);
    }

    internal static void Write(
        UiView view,
        Node node,
        in LayoutStyle layout,
        in VisualStyle visual,
        in InheritStyle inherit,
        in InteractionProps interaction,
        in Placement place,
        bool selected,
        NodeRef? nodeRef,
        bool changed = false
    ) =>
        Write(
            view,
            node,
            layout,
            visual,
            inherit,
            place,
            selected,
            nodeRef,
            changed | node.WriteInteraction(interaction)
        );

    internal static void Write(
        UiView view,
        Node node,
        in LayoutStyle layout,
        in VisualStyle visual,
        in InheritStyle inherit,
        in Placement place,
        bool selected,
        NodeRef? nodeRef,
        bool changed
    )
    {
        changed |= node.WriteLayout(layout);
        changed |= node.WriteVisual(visual);
        changed |= node.WriteInherit(inherit);
        changed |= node.WritePlacement(Builder.EffectivePlacement(node, place));
        changed |= node.WriteSelected(selected);
        changed |= node.WriteRef(nodeRef);
        if (changed)
            view.Stats.NodeWrites++;
    }
}

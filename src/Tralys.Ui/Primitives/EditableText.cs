using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Fonts;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;

namespace Tralys.Ui;

/// <summary>The editable text primitive the text fields are built on.</summary>
[FramePath]
public static class EditableTextPrimitive
{
    /// <summary>Text the user edits in <c>text</c>, which its owner keeps: each edit writes into it, making no string,
    /// and after the update's render a change the owner made replaces what the field shows as an undo step.
    /// <c>onEdit</c> runs once after each edit, never for a change from outside. <c>onCommit</c> runs on blur, on leaving
    /// the view, on Enter, on Escape and on a whole replace, when the text changed since the last commit; Enter moves the
    /// focus to <c>next</c> when it can take it, else runs <c>onSubmit</c>; <c>onEscape</c> takes Escape before a layer
    /// can. <c>maxLength</c> (UTF-16 units) and <c>maxBytes</c> (UTF-8) bind only what typing and pasting
    /// insert.</summary>
    public static void EditableText(
        this Ui ui,
        ObservableText text,
        Callback onEdit = default,
        Callback onCommit = default,
        Callback onSubmit = default,
        Callback onEscape = default,
        Callback<TextSelection> onSelectionChange = default,
        TextValue placeholder = default,
        TextInputKind kind = default,
        bool multiline = false,
        bool wrap = false,
        bool readOnly = false,
        int maxLength = 0,
        int maxBytes = 0,
        bool masked = false,
        NodeRef? next = null,
        bool keepSelectionOnFocus = false,
        Paint color = default,
        float fontSize = 0,
        string? fontFamily = null,
        FontWeight fontWeight = default,
        float lineHeight = 0,
        TextAlign textAlign = default,
        Paint selectionColor = default,
        Paint caretColor = default,
        Paint placeholderColor = default,
        bool disabled = false,
        bool autoFocus = false,
        int tabIndex = 0,
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
        ArgumentNullException.ThrowIfNull(text);
        var b = ui.Builder;
        if (b.Discarding)
            return;
        var node = b.Claim(NodeKind.EditableText, null, key, file, line, out _);
        var view = b.View;
        if (node.Payload is not EditorPayload editor)
        {
            editor = EditorPayload.Rent(view, node);
            node.Payload = editor;
        }
        var changed = editor.Write(
            view,
            node,
            text,
            new EditorProps
            {
                OnEdit = onEdit,
                OnCommit = onCommit,
                OnSubmit = onSubmit,
                OnEscape = onEscape,
                OnSelectionChange = onSelectionChange,
                Kind = kind,
                Multiline = multiline,
                Wrap = wrap,
                ReadOnly = readOnly,
                Masked = masked,
                KeepSelectionOnFocus = keepSelectionOnFocus,
                MaxLength = maxLength,
                MaxBytes = maxBytes,
                TabIndex = tabIndex,
                Next = next,
                SelectionColor = selectionColor,
                CaretColor = caretColor,
                PlaceholderColor = placeholderColor,
            },
            placeholder.Span
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
                LineHeight = lineHeight,
                TextAlign = textAlign,
                PointerEvents = pointerEvents,
                Disabled = disabled,
            },
            new InteractionProps
            {
                OnPointerDown = editor.OnPointerDown,
                OnPointerMove = editor.OnPointerMove,
                OnPointerUp = editor.OnPointerUp,
                OnPointerCancel = editor.OnPointerCancel,
                OnKeyDown = editor.OnKeyDown,
                OnFocusChange = editor.OnFocusChange,
                Focusable = true,
                AutoFocus = autoFocus,
                Cursor = cursor ?? Cursors.IBeam,
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

    /// <summary>Takes a press that landed beside the editable text <paramref name="text"/> holds, such as on its field's
    /// padding, as a press on the text: called from the press's <c>onPointerDown</c>, it puts the caret at the boundary
    /// nearest the pointer (Shift extends, a double press selects the word, a triple press all), captures the pointer
    /// for the selection drag, and has the press focus the text as a press on it does, selecting nothing more. It leaves
    /// any other press alone, and any press when <paramref name="text"/> holds no enabled, shown editable text.</summary>
    public static void TakePress(this NodeRef text, ref PointerEvent e)
    {
        if (text.Node is { Payload: EditorPayload editor, View: { } view } node)
            view.PressOn(node, editor.OnPointerDown, ref e);
    }

    /// <summary>The selection in the editable text <paramref name="text"/> holds, in UTF-16 units of the text it
    /// shows; empty at 0 while it holds none.</summary>
    public static TextSelection EditedSelection(this NodeRef text) =>
        text.Node is { Payload: EditorPayload editor } ? editor.Selection : default;
}

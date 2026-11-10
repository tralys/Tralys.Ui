using System.Text;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct InputProps
{
    /// <summary>The text it edits, which its owner keeps.</summary>
    public required ObservableText Text { get; init; }

    /// <summary>Runs once after each edit, never for a change from outside.</summary>
    public Callback OnEdit { get; init; }

    public TextValue Placeholder { get; init; }

    /// <summary>The soft keyboard it raises; <see cref="TextInputKind.Password"/> also copies and cuts nothing and
    /// keeps no undo history.</summary>
    public TextInputKind Kind { get; init; }

    /// <summary>Shows a dot for each character; by default only while <see cref="Kind"/> is
    /// <see cref="TextInputKind.Password"/>.</summary>
    public bool? Masked { get; init; }

    /// <summary>Takes line breaks and wraps, as a text area does.</summary>
    public bool Multiline { get; init; }

    /// <summary>Wraps its lines without taking line breaks, so Enter still submits.</summary>
    public bool Wrap { get; init; }

    public bool ReadOnly { get; init; }

    /// <summary>The most UTF-16 units typing and pasting may make it hold; 0 for no limit.</summary>
    public int MaxLength { get; init; }

    /// <summary>The most UTF-8 bytes typing and pasting may make it hold, which its count then counts; 0 for no
    /// limit.</summary>
    public int MaxBytes { get; init; }

    /// <summary>How few characters, or bytes under <see cref="MaxBytes"/>, may remain before its count shows; null
    /// shows it whenever there is a limit.</summary>
    public int? CountWithin { get; init; }

    /// <summary>Shows a button that empties it while it is enabled, editable and has text.</summary>
    public bool Clearable { get; init; }

    /// <summary>Runs after the clear button reports the emptied value.</summary>
    public Callback OnClear { get; init; }

    public bool Invalid { get; init; }

    public FieldVariant Variant { get; init; }

    public ComponentSize? Size { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    /// <summary>Where the text and placeholder sit while narrower than the field and not wrapping.</summary>
    public TextAlign TextAlign { get; init; }

    /// <summary>A glyph before the text, in the placeholder colour.</summary>
    public IconGlyph LeadingIcon { get; init; }

    /// <summary>Inside the border before the text, its presses its own.</summary>
    public Slot Leading { get; init; }

    /// <summary>Inside the border after the text and the clear button, its presses its own.</summary>
    public Slot Trailing { get; init; }

    public bool Disabled { get; init; }

    public bool AutoFocus { get; init; }

    /// <summary>Keeps the selection when focus arrives by key or code, and puts the caret at the end when it
    /// autofocuses, rather than selecting all.</summary>
    public bool KeepSelectionOnFocus { get; init; }

    /// <summary>The field Enter moves the focus to, when it can take it, in place of submitting.</summary>
    public NodeRef? Next { get; init; }

    /// <summary>Bound to its text, the node that takes the focus, so another field can name it as its
    /// <see cref="Next"/>.</summary>
    public NodeRef? NodeRef { get; init; }

    public Callback OnSubmit { get; init; }

    public Callback OnCommit { get; init; }

    /// <summary>Runs as the caret or the selection moves, by an edit, a key or the pointer, as a field that follows its
    /// caret needs.</summary>
    public Callback<TextSelection> OnSelectionChange { get; init; }

    /// <summary>Runs on Escape, after committing the text, taking the key before a popup or the game.</summary>
    public Callback OnEscape { get; init; }
}

/// <summary>A text field over <c>ui.EditableText</c>, editing <see cref="InputProps.Text"/> in place: Enter reports
/// through <see cref="InputProps.OnSubmit"/>, and leaving it through <see cref="InputProps.OnCommit"/>. It wears the
/// theme's field palette for its state, with a danger border while <see cref="InputProps.Invalid"/>, even focused; a
/// <see cref="InputProps.Multiline"/> field wraps and takes line breaks. A press on its padding is a press on its text.
/// Inside its border sit the leading slot, a leading glyph, the text, a count against its limit, a clear button and the
/// trailing slot.</summary>
public sealed class Input : Component<InputProps>
{
    protected override void Render(Ui ui, in InputProps p)
    {
        var t = ui.Theme;
        var size = p.Size ?? ComponentSize.Md;
        var m = t.Metrics(size);
        var disabled = ui.IsDisabled;
        var palette = t.Field(p.Variant, ui.FieldState);
        var outline = ui.Animate(
            disabled ? palette.Border
            : p.Invalid ? t.Color(ColorToken.BorderDanger)
            : p.ReadOnly ? t.Color(ColorToken.BorderMuted)
            : palette.Border
        );
        var flush = p.Variant == FieldVariant.Flush;
        var border = flush ? new Thickness(0f, 0f, 0f, 1f) : new Thickness(1f);
        var lines = p.Multiline || p.Wrap;
        var text = p.NodeRef ?? ui.Ref();
        var cursor = disabled ? Cursors.Arrow : Cursors.IBeam;

        var value = p.Text.Span;
        var counted = p.MaxBytes > 0 ? Encoding.UTF8.GetByteCount(value) : value.Length;
        var max = p.MaxBytes > 0 ? p.MaxBytes : p.MaxLength;
        var count = max > 0 && max - counted <= (p.CountWithin ?? int.MaxValue);
        var clear = p.Clearable && !disabled && !p.ReadOnly && !value.IsEmpty;
        var after = (count && !lines) || clear || !p.Trailing.IsEmpty;

        var edgeY = MathF.Max(border.Top, border.Bottom);
        var padY = lines ? MathF.Max(0f, m.PadY - edgeY) : 0f;
        var padding = new Thickness(
            m.FieldPadX - (p.Leading.IsEmpty ? border.Left : 0f),
            padY,
            m.FieldPadX - (after ? 0f : border.Right),
            padY
        );
        using (
            ui.Box(
                align: lines ? Align.Stretch : Align.Center,
                width: p.Width,
                height: p.Height,
                minHeight: m.Height,
                padding: new Thickness(0f, edgeY - border.Top, 0f, edgeY - border.Bottom),
                background: ui.Animate(palette.Background),
                border: new Border(border, outline),
                radius: flush ? default : t.Radius(RadiusToken.Md),
                cursor: cursor,
                onPointerDown: Handler<PointerEvent>.Of(
                    text,
                    static (text, ref e) => text.TakePress(ref e)
                )
            )
        )
        {
            if (!p.Leading.IsEmpty)
                using (
                    ui.Box(
                        align: Align.Center,
                        shrink: 0f,
                        margin: new Thickness(-border.Left, 0f, 0f, 0f),
                        onPointerDown: InputClear.KeepPress
                    )
                )
                    p.Leading.Render(ui);

            using (ui.Box(align: lines ? Align.Start : Align.Center, grow: 1f, padding: padding))
            {
                if (!p.LeadingIcon.IsEmpty)
                    ui.Icon(
                        glyph: p.LeadingIcon,
                        size: m.FontSize,
                        color: t.Color(
                            disabled ? ColorToken.IconDisabled : ColorToken.FgPlaceholder
                        ),
                        shrink: 0f,
                        margin: m.GapEnd
                    );
                ui.EditableText(
                    text: p.Text,
                    onEdit: p.OnEdit,
                    onCommit: p.OnCommit,
                    onSelectionChange: p.OnSelectionChange,
                    onSubmit: p.OnSubmit,
                    onEscape: p.OnEscape,
                    placeholder: p.Placeholder,
                    kind: p.Kind,
                    multiline: p.Multiline,
                    wrap: lines,
                    readOnly: p.ReadOnly,
                    maxLength: p.MaxLength,
                    maxBytes: p.MaxBytes,
                    masked: p.Masked ?? p.Kind == TextInputKind.Password,
                    next: p.Next,
                    keepSelectionOnFocus: p.KeepSelectionOnFocus,
                    color: palette.Foreground,
                    fontSize: m.FontSize,
                    fontWeight: t.Weight(WeightToken.Interface),
                    textAlign: p.TextAlign,
                    selectionColor: t.Color(ColorToken.BgTextSelection),
                    caretColor: t.Color(ColorToken.Fg),
                    placeholderColor: t.Color(ColorToken.FgPlaceholder),
                    autoFocus: p.AutoFocus,
                    grow: 1f,
                    alignSelf: lines ? AlignSelf.Stretch : default,
                    cursor: cursor,
                    nodeRef: text
                );
                if (count && lines)
                {
                    var sm = t.Metrics(ComponentSize.Sm);
                    ui.Text(
                        ui.Format($"{counted}/{max}"),
                        color: palette.Foreground,
                        fontSize: m.FontSize,
                        fontWeight: t.Weight(WeightToken.Interface),
                        pointerEvents: PointerEvents.None,
                        position: Position.Absolute,
                        right: sm.PadX,
                        bottom: sm.PadY
                    );
                }
            }

            if (count && !lines)
                ui.Text(
                    ui.Format($"{counted}/{max}"),
                    color: palette.Foreground,
                    fontSize: m.FontSize,
                    fontWeight: t.Weight(WeightToken.Interface),
                    shrink: 0f,
                    margin: new Thickness(0f, 0f, Last(border, clear || !p.Trailing.IsEmpty), 0f)
                );
            if (clear)
                ui.InputClear(
                    onClick: Callback.Of(ui.Self(p), static h => Clear(h)),
                    margin: new Thickness(
                        0f,
                        -border.Top,
                        Last(border, !p.Trailing.IsEmpty),
                        -border.Bottom
                    )
                );
            if (!p.Trailing.IsEmpty)
                using (
                    ui.Box(
                        align: Align.Center,
                        shrink: 0f,
                        margin: new Thickness(0f, 0f, -border.Right, 0f),
                        onPointerDown: InputClear.KeepPress
                    )
                )
                    p.Trailing.Render(ui);
        }
    }

    static float Last(Thickness border, bool followed) => followed ? 8f : 8f - border.Right;

    static void Clear(Host<InputProps> host)
    {
        host.Props.Text.Clear();
        host.Props.OnClear.Invoke();
    }
}

internal readonly record struct InputClearProps
{
    public Callback OnClick { get; init; }
}

/// <summary>The small cross that empties an <see cref="Input"/>: a ghost-outlined secondary button at the extra-small
/// rung that never takes the focus, so the focus stays in the field.</summary>
internal sealed class InputClear : Component<InputClearProps>
{
    /// <summary>Keeps a press on a part of a field from reaching the field, which would take it for its text.</summary>
    internal static readonly Handler<PointerEvent> KeepPress = Handler<PointerEvent>.Of(
        static (ref e) => e.StopPropagation()
    );

    protected override void Render(Ui ui, in InputClearProps p)
    {
        var t = ui.Theme;
        var m = t.Metrics(ComponentSize.Xs);
        var look = ui.Animated(
            t.Palette(ColorScheme.Secondary, ComponentVariant.GhostOutlined, ui.ComponentState)
        );
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                width: m.Height,
                height: m.Height,
                background: look.Background,
                border: new Border(1f, look.Border),
                radius: t.Radius(RadiusToken.Md),
                cursor: Cursors.Hand,
                focusable: false,
                onClick: p.OnClick,
                onPointerDown: KeepPress
            )
        )
            ui.Icon(glyph: IconGlyph.Of(IconRole.X), size: m.IconSize, color: look.Foreground);
    }
}

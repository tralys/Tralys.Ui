using System.Runtime.CompilerServices;
using Tralys.Ui.Fonts;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct PickerFieldProps
{
    /// <summary>Whether the popup shows; null leaves it to the field, starting at <see cref="DefaultOpen"/>.</summary>
    public bool? Open { get; init; }

    public bool DefaultOpen { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    /// <summary>What the popup holds.</summary>
    public Slot Content { get; init; }

    /// <summary>The value the field shows; <see cref="Placeholder"/> while null.</summary>
    public TextValue Text { get; init; }

    public TextValue Placeholder { get; init; }

    /// <summary>A muted label before the field's value.</summary>
    public TextValue Sublabel { get; init; }

    /// <summary>Whether the field shows its turning chevron; true when null.</summary>
    public bool? HasArrow { get; init; }

    public FieldVariant Variant { get; init; }

    /// <summary>Where the popup opens against the field; <see cref="PlacementMode.BottomStart"/> by default.</summary>
    public PlacementMode? Placement { get; init; }

    /// <summary>The field's and the popup frame's rung; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    public Length Width { get; init; }

    /// <summary>The narrowest the popup gets, which is otherwise as wide as the field.</summary>
    public float PopupMinWidth { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A field that opens free content, a grid of tiles or a short form, in a popup where a
/// <see cref="Combobox{TItem}"/> would open a list. It shows <see cref="PickerFieldProps.Text"/>, else its placeholder,
/// and a click, Space or Enter on it toggles <see cref="PickerFieldProps.Open"/>, which reports through
/// <see cref="PickerFieldProps.OnOpenChange"/> and is the field's own when null. The popup is the shared floating frame,
/// padded as a block, as wide as the field or <see cref="PickerFieldProps.PopupMinWidth"/>; Escape or a press outside
/// closes it, and focus inside goes back to what had it. With no content, an open field only lights its border.</summary>
public sealed class PickerField : Component<PickerFieldProps>
{
    protected override void Render(Ui ui, in PickerFieldProps p)
    {
        var disclosure = ui.Remember(ui.State(p.DefaultOpen), static own => new Disclosure(own));
        disclosure.Open = p.Open;
        disclosure.OnOpenChange = p.OnOpenChange;
        ui.Effect(disclosure, ui.IsDisabled, Disclosure.ShutWhenDisabled);
        var open = (p.Open ?? disclosure.Own.Value) && !ui.IsDisabled;
        var field = ui.Ref();
        ui.PickerBox(
            open: open,
            text: p.Text,
            placeholder: p.Placeholder,
            sublabel: p.Sublabel,
            hasArrow: p.HasArrow ?? true,
            variant: p.Variant,
            size: p.Size ?? ComponentSize.Md,
            width: p.Width,
            onClick: disclosure.Toggle,
            field: field
        );
        if (!open || p.Content.IsEmpty)
            return;
        using (
            ui.Positioner(
                anchor: field,
                placement: p.Placement,
                matchAnchorWidth: true,
                restoreFocus: true,
                dismissOn: Dismiss.Escape | Dismiss.OutsidePress,
                direction: Direction.Column,
                minWidth: p.PopupMinWidth,
                onDismiss: disclosure.Close
            )
        )
        using (ui.OverlayFrame(size: p.Size, inset: OverlayInset.Block))
            p.Content.Render(ui);
    }
}

[FramePath]
internal static class PickerParts
{
    /// <summary>The closed box a picker opens from: a muted sublabel, the value in the strong foreground (its text, or
    /// what <paramref name="value"/> draws) or the placeholder, the sublabel and placeholder at the body size whatever
    /// the field's, and a chevron turned while open, on an input's fill and
    /// border, the border lit while open; <paramref name="valueColor"/> colours the value in place of the strong
    /// foreground.</summary>
    public static void PickerBox(
        this Ui ui,
        bool open,
        TextValue text,
        TextValue placeholder,
        TextValue sublabel,
        bool hasArrow,
        FieldVariant variant,
        ComponentSize size,
        Length width,
        Callback onClick,
        NodeRef field,
        Slot value = default,
        Handler<KeyEvent> onKeyDown = default,
        Paint? valueColor = null,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        var m = t.Metrics(size);
        var disabled = ui.IsDisabled;
        var flush = variant == FieldVariant.Flush;
        var palette = t.Field(variant, ui.FieldState);
        var radius = flush ? default : t.Radius(RadiusToken.Md);
        var shownColor =
            valueColor ?? t.Color(disabled ? ColorToken.FgDisabled : ColorToken.FgStrong);
        using (
            ui.Box(
                align: Align.Center,
                width: width,
                minHeight: m.Height,
                padding: m.FieldPaddingX,
                background: palette.Background,
                border: new Border(
                    flush ? new Thickness(0f, 0f, 0f, 1f) : new Thickness(1f),
                    open && !flush ? t.Color(ColorToken.BorderFocus) : palette.Border
                ),
                radius: radius,
                color: palette.Foreground,
                fontSize: m.FontSize,
                fontWeight: t.Weight(WeightToken.Interface),
                cursor: Cursors.Hand,
                onClick: onClick,
                onKeyDown: onKeyDown,
                nodeRef: field,
                key: key,
                file: file,
                line: line
            )
        )
        {
            using (ui.Box(grow: 1f, minWidth: 0f))
            {
                if (!sublabel.IsNull)
                    ui.Text(
                        sublabel,
                        color: t.Color(disabled ? ColorToken.FgDisabled : ColorToken.FgMuted),
                        variant: TextVariant.Body,
                        shrink: 0f,
                        margin: new Thickness(0f, 0f, 4f, 0f)
                    );
                if (!value.IsEmpty)
                    using (
                        ui.Box(
                            align: Align.Center,
                            grow: 1f,
                            minWidth: 0f,
                            overflow: Overflow.Clip,
                            color: shownColor
                        )
                    )
                        value.Render(ui);
                else if (text.IsNull)
                    ui.Text(
                        placeholder,
                        color: t.Color(disabled ? ColorToken.FgDisabled : ColorToken.FgPlaceholder),
                        variant: TextVariant.Body,
                        grow: 1f,
                        trimming: TextTrimming.CharacterEllipsis
                    );
                else
                    ui.Text(
                        text,
                        color: shownColor,
                        grow: 1f,
                        trimming: TextTrimming.CharacterEllipsis
                    );
                if (hasArrow)
                    using (
                        ui.Box(
                            shrink: 0f,
                            alignSelf: AlignSelf.Center,
                            margin: new Thickness(4f, 0f, 0f, 0f)
                        )
                    )
                        ui.Chevron(
                            color: t.Color(disabled ? ColorToken.IconDisabled : ColorToken.Icon),
                            size: m.IconSize,
                            open: open
                        );
            }
            if (ui.IsFocusVisible)
                ui.FocusRing(radius: radius);
        }
    }
}

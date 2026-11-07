using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>Whether an icon-only <see cref="Toggle"/>'s glyph dims while off.</summary>
public enum ToggleEmphasis : byte
{
    /// <summary>Dims while off only without a <see cref="ToggleProps.ActiveIcon"/>: one glyph's on and off.</summary>
    Auto,

    /// <summary>Always dims while off, so on reads as the emphasised state.</summary>
    State,

    /// <summary>Never dims: on and off are two modes, each in its scheme.</summary>
    Mode,
}

public readonly record struct ToggleProps
{
    public bool Value { get; init; }

    public Callback<bool> OnValueChange { get; init; }

    public TextValue Text { get; init; }

    /// <summary>The icon before the text, or the whole face of a toggle with no text.</summary>
    public IconGlyph Icon { get; init; }

    /// <summary>The icon an icon-only toggle shows in place of <see cref="Icon"/> while on.</summary>
    public IconGlyph ActiveIcon { get; init; }

    /// <summary>The toggle's tooltip while on, in place of a tooltip written around it, which shows while off.</summary>
    public TextValue ActiveTooltip { get; init; }

    public ToggleEmphasis Emphasis { get; init; }

    public ComponentSize? Size { get; init; }

    /// <summary>The glyph's size: the rung's icon size for an icon-only toggle, else its font size, by default.</summary>
    public float? IconSize { get; init; }

    /// <summary>The corners; the theme's <see cref="RadiusToken.Md"/> by default.</summary>
    public CornerRadius? Radius { get; init; }

    /// <summary>The scheme while off; <see cref="ColorScheme.Secondary"/> by default.</summary>
    public ColorScheme? Scheme { get; init; }

    /// <summary>The variant while off; <see cref="ComponentVariant.Ghost"/> by default for an icon-only toggle, else
    /// <see cref="ComponentVariant.Filled"/>.</summary>
    public ComponentVariant? Variant { get; init; }

    /// <summary>The scheme while on; <see cref="ColorScheme.Primary"/> by default, or
    /// <see cref="ColorScheme.Secondary"/> for an icon-only toggle.</summary>
    public ColorScheme? ActiveScheme { get; init; }

    /// <summary>The variant while on; <see cref="ComponentVariant.Filled"/> by default, or the off variant for an
    /// icon-only toggle.</summary>
    public ComponentVariant? ActiveVariant { get; init; }

    public bool Disabled { get; init; }

    public Callback OnClick { get; init; }
}

/// <summary>A controlled two-state button: it shows <see cref="ToggleProps.Value"/>, wearing its active look while
/// on, and reports a click through <see cref="ToggleProps.OnValueChange"/> as the flipped value without changing its
/// own, then runs <see cref="ToggleProps.OnClick"/>. With an icon and no text it is a square icon button whose glyph
/// dims while off as <see cref="ToggleProps.Emphasis"/> says.</summary>
public sealed class Toggle : Component<ToggleProps>
{
    protected override void Render(Ui ui, in ToggleProps p)
    {
        var t = ui.Theme;
        var m = t.Metrics(p.Size ?? ComponentSize.Md);
        ui.CanRun(p.OnClick);
        var iconOnly = p.Text.IsNull && !p.Icon.IsEmpty;
        var scheme = p.Scheme ?? ColorScheme.Secondary;
        var variant = p.Variant ?? (iconOnly ? ComponentVariant.Ghost : ComponentVariant.Filled);
        if (p.Value)
        {
            scheme = p.ActiveScheme ?? (iconOnly ? ColorScheme.Secondary : ColorScheme.Primary);
            variant = p.ActiveVariant ?? (iconOnly ? variant : ComponentVariant.Filled);
        }
        var target = t.Palette(scheme, variant, ui.ComponentState);
        var dims =
            iconOnly
            && !p.Value
            && p.Emphasis switch
            {
                ToggleEmphasis.State => true,
                ToggleEmphasis.Mode => false,
                _ => p.ActiveIcon.IsEmpty,
            };
        if (dims)
            target = target with { Foreground = t.Color(ColorToken.Icon) };
        var look = ui.Animated(target);
        var art =
            !iconOnly && scheme == ColorScheme.Primary && variant == ComponentVariant.Filled
                ? t.Skin(SkinPart.ButtonPrimary)
                : null;
        var radius = p.Radius ?? t.Radius(RadiusToken.Md);
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                gap: m.Gap,
                width: iconOnly ? m.Height : Length.Auto,
                height: iconOnly ? m.Height : Length.Auto,
                minHeight: iconOnly ? Length.Auto : m.Height,
                padding: iconOnly ? default(Thickness)
                    : art is null ? ButtonParts.Inside(m.PaddingX, 1f)
                    : m.PaddingX,
                background: art is null ? look.Background : default(Paint),
                border: art is null ? new Border(1f, look.Border) : default,
                radius: radius,
                fontSize: m.FontSize,
                fontWeight: t.Weight(WeightToken.Interface),
                opacity: ui.IsDisabled ? 0.5f : null,
                cursor: Cursors.Hand,
                onClick: Callback.Of(ui.Self(p), static h => Flip(h))
            )
        )
        {
            if (art is { } rest)
            {
                ui.Art(art: rest);
                if (ui.IsHovered && t.Skin(SkinPart.ButtonPrimaryHover) is { } hover)
                    ui.Art(art: hover);
            }
            if (iconOnly)
                ui.Icon(
                    glyph: p.Value && !p.ActiveIcon.IsEmpty ? p.ActiveIcon : p.Icon,
                    size: p.IconSize ?? m.IconSize,
                    color: look.Foreground
                );
            else
                ui.Label(
                    text: p.Text,
                    content: default,
                    iconLeft: p.Icon,
                    iconRight: default,
                    iconSize: p.IconSize ?? m.FontSize,
                    gap: m.Gap,
                    color: look.Foreground
                );
            if (!p.ActiveTooltip.IsNull)
                using (
                    ui.Tooltip(
                        text: p.Value ? p.ActiveTooltip : default,
                        position: Position.Absolute,
                        left: 0f,
                        top: 0f,
                        right: 0f,
                        bottom: 0f
                    )
                ) { }
            if (ui.IsFocusVisible)
                ui.FocusRing(radius: radius);
        }
    }

    [FramePath]
    static void Flip(Host<ToggleProps> h)
    {
        ref readonly var p = ref h.Props;
        p.OnValueChange.Invoke(!p.Value);
        p.OnClick.Invoke();
    }
}

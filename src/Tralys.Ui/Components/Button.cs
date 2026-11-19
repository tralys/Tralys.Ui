using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct ButtonProps
{
    public TextValue Text { get; init; }

    /// <summary>What the button shows when it has no <see cref="Text"/>.</summary>
    public Slot Content { get; init; }

    public IconGlyph IconLeft { get; init; }

    public IconGlyph IconRight { get; init; }

    public ComponentSize? Size { get; init; }

    /// <summary>The scheme; <see cref="ColorScheme.Primary"/> by default, <see cref="ColorScheme.Secondary"/> for a
    /// link.</summary>
    public ColorScheme? Scheme { get; init; }

    public ComponentVariant Variant { get; init; }

    /// <summary>Draws the active look, as the current page's button in a bar does.</summary>
    public bool Active { get; init; }

    /// <summary>The active look's scheme; <see cref="ColorScheme.Primary"/> by default.</summary>
    public ColorScheme? ActiveScheme { get; init; }

    /// <summary>The active look's variant; <see cref="ComponentVariant.Filled"/> by default.</summary>
    public ComponentVariant? ActiveVariant { get; init; }

    /// <summary>Pulses the outline and the text toward the theme's accent and back, to draw the eye to something
    /// waiting.</summary>
    public bool Flashing { get; init; }

    public bool Disabled { get; init; }

    public Callback OnClick { get; init; }

    /// <summary>Closes the <see cref="Popover"/> the button sits in once <see cref="OnClick"/> has run, but not when
    /// one made from a command refuses.</summary>
    public bool ClosesPopover { get; init; }

    /// <summary>The input action, such as <see cref="UiAction.Back"/>, whose key and gamepad button press the button
    /// from anywhere in its action scope.</summary>
    public UiAction Action { get; init; }

    public Length Width { get; init; }

    public Length MinWidth { get; init; }

    /// <summary>The height, never less than its size's rung.</summary>
    public Length Height { get; init; }

    /// <summary>Space between the border and the content; the size's padding by default.</summary>
    public Thickness? Padding { get; init; }

    /// <summary>Where the content sits along a button wider than it: centred by default;
    /// <see cref="Tralys.Ui.Justify.SpaceBetween"/> spreads it, its first glyph at the start and its last at the
    /// end.</summary>
    public Justify? Justify { get; init; }
}

/// <summary>The design system's button. A click, Enter, Space or its <see cref="ButtonProps.Action"/> runs
/// <see cref="ButtonProps.OnClick"/>, and one made from a command disables the button while it can't run. It wears the
/// theme's palette for its scheme, variant and state, or for its active scheme and variant while
/// <see cref="ButtonProps.Active"/>, at its size's rung, with an icon either side of its content; drawn filled
/// primary, it wears the skin's <see cref="SkinPart.ButtonPrimary"/> art when there is some. The
/// <see cref="ComponentVariant.Link"/> variant is bare text at its size's type in the link weight.</summary>
public sealed class Button : Component<ButtonProps>
{
    protected override void Render(Ui ui, in ButtonProps p)
    {
        var t = ui.Theme;
        var m = t.Metrics(p.Size ?? ComponentSize.Md);
        ui.CanRun(p.OnClick);
        var click = Callback.Of(ui.Self(p), static h => Run(h));
        if (p.Variant == ComponentVariant.Link)
        {
            var text = ui.Animated(
                t.Palette(p.Scheme ?? ColorScheme.Secondary, p.Variant, ui.ComponentState)
            ).Foreground;
            using (
                ui.Box(
                    align: Align.Center,
                    justify: p.Justify ?? Justify.Start,
                    gap: 4f,
                    width: p.Width,
                    height: p.Height,
                    minWidth: p.MinWidth,
                    padding: p.Padding ?? default,
                    fontSize: m.FontSize,
                    fontWeight: t.Weight(WeightToken.Link),
                    cursor: Cursors.Hand,
                    onClick: click,
                    action: p.Action
                )
            )
            {
                ui.LabelGlyphs(
                    text: p.Text,
                    content: p.Content,
                    iconLeft: p.IconLeft,
                    iconRight: p.IconRight,
                    iconSize: m.IconSize - 2f,
                    color: text
                );
                if (ui.IsFocusVisible)
                    ui.FocusRing(radius: t.Radius(RadiusToken.Sm));
            }
            return;
        }

        var scheme = p.Active
            ? p.ActiveScheme ?? ColorScheme.Primary
            : p.Scheme ?? ColorScheme.Primary;
        var variant = p.Active ? p.ActiveVariant ?? ComponentVariant.Filled : p.Variant;
        var look = ui.Animated(t.Palette(scheme, variant, ui.ComponentState));
        var color = look.Foreground;
        var outline = look.Border;
        if (p.Flashing)
        {
            var pulse = ui.Loop(0.5f, alternate: true);
            var accent = t.Color(ColorToken.BorderAccent);
            color = Color.Mix(color, accent, pulse);
            outline = Color.Mix(outline, accent, pulse);
        }
        var art =
            scheme == ColorScheme.Primary && variant == ComponentVariant.Filled
                ? t.Skin(SkinPart.ButtonPrimary)
                : null;
        var radius = t.Radius(RadiusToken.Md);
        var spreads =
            p.Justify is Justify.SpaceBetween or Justify.SpaceAround or Justify.SpaceEvenly;
        using (
            ui.Box(
                align: Align.Center,
                justify: spreads ? Justify.Start : p.Justify ?? Justify.Center,
                gap: m.Gap,
                width: p.Width,
                height: p.Height,
                minWidth: p.MinWidth,
                minHeight: m.Height,
                padding: p.Padding
                    ?? (art is null ? ButtonParts.Inside(m.PaddingX, 1f) : m.PaddingX),
                background: art is null ? look.Background : default(Paint),
                border: art is null ? new Border(1f, outline) : default,
                radius: radius,
                fontSize: m.FontSize,
                fontWeight: t.Weight(WeightToken.Interface),
                opacity: ui.IsDisabled ? 0.5f : null,
                cursor: Cursors.Hand,
                onClick: click,
                action: p.Action
            )
        )
        {
            if (art is { } rest)
            {
                ui.Art(art: rest);
                if (ui.IsHovered && t.Skin(SkinPart.ButtonPrimaryHover) is { } hover)
                    ui.Art(art: hover);
            }
            ui.Label(
                text: p.Text,
                content: p.Content,
                iconLeft: p.IconLeft,
                iconRight: p.IconRight,
                iconSize: m.FontSize,
                gap: m.Gap,
                color: color,
                justify: spreads ? p.Justify!.Value : Justify.Start,
                grow: spreads ? 1f : 0f
            );
            if (ui.IsFocusVisible)
                ui.FocusRing(radius: radius);
        }
    }

    [FramePath]
    static void Run(Host<ButtonProps> h)
    {
        ref readonly var p = ref h.Props;
        if (!p.OnClick.CanRun)
            return;
        p.OnClick.Invoke();
        if (p.ClosesPopover)
            h.Use(Popover.Context).Close.Invoke();
    }
}

using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct IconButtonProps
{
    public IconGlyph Icon { get; init; }

    public ComponentSize? Size { get; init; }

    /// <summary>The corners; the theme's <see cref="RadiusToken.Md"/> by default.</summary>
    public CornerRadius? Radius { get; init; }

    /// <summary>The scheme; <see cref="ColorScheme.Primary"/> by default.</summary>
    public ColorScheme? Scheme { get; init; }

    public ComponentVariant Variant { get; init; }

    /// <summary>Draws the active look.</summary>
    public bool Active { get; init; }

    /// <summary>The active look's scheme; <see cref="ColorScheme.Primary"/> by default.</summary>
    public ColorScheme? ActiveScheme { get; init; }

    /// <summary>The active look's variant; <see cref="ComponentVariant.Filled"/> by default.</summary>
    public ComponentVariant? ActiveVariant { get; init; }

    public bool Disabled { get; init; }

    public Callback OnClick { get; init; }

    /// <summary>The input action whose key and gamepad button press it from anywhere in its action scope.</summary>
    public UiAction Action { get; init; }
}

/// <summary>A square <see cref="Button"/> showing one glyph: as wide as its size's rung is tall, its glyph at the
/// rung's icon size.</summary>
public sealed class IconButton : Component<IconButtonProps>
{
    protected override void Render(Ui ui, in IconButtonProps p)
    {
        var t = ui.Theme;
        var m = t.Metrics(p.Size ?? ComponentSize.Md);
        ui.CanRun(p.OnClick);
        var scheme = p.Active
            ? p.ActiveScheme ?? ColorScheme.Primary
            : p.Scheme ?? ColorScheme.Primary;
        var variant = p.Active ? p.ActiveVariant ?? ComponentVariant.Filled : p.Variant;
        var look = ui.Animated(t.Palette(scheme, variant, ui.ComponentState));
        var radius = p.Radius ?? t.Radius(RadiusToken.Md);
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                width: m.Height,
                height: m.Height,
                background: look.Background,
                border: new Border(1f, look.Border),
                radius: radius,
                opacity: ui.IsDisabled ? 0.5f : null,
                cursor: Cursors.Hand,
                onClick: Callback.Of(ui.Self(p), static h => h.Props.OnClick.Invoke()),
                action: p.Action
            )
        )
        {
            ui.Icon(glyph: p.Icon, size: m.IconSize, color: look.Foreground);
            if (ui.IsFocusVisible)
                ui.FocusRing(radius: radius);
        }
    }
}

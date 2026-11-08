using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct TagProps
{
    public TextValue Text { get; init; }

    /// <summary>A 10-unit glyph before the text, in its colour.</summary>
    public IconGlyph Icon { get; init; }

    /// <summary>The scheme; <see cref="ColorScheme.Secondary"/> by default.</summary>
    public ColorScheme? Scheme { get; init; }

    /// <summary>The variant; <see cref="ComponentVariant.Subtle"/> by default.</summary>
    public ComponentVariant? Variant { get; init; }
}

/// <summary>A short label on its scheme, such as how a player relates to you beside their name: one line of caption
/// text, 16 tall, in the theme's palette for its scheme and variant at rest, or disabled. It labels and doesn't act,
/// so hovering and pressing leave it as it is, and input passes through it to what lies beneath.</summary>
public sealed class Tag : Component<TagProps>
{
    protected override void Render(Ui ui, in TagProps p)
    {
        var t = ui.Theme;
        ui.Pill(
            text: p.Text,
            icon: p.Icon,
            look: t.Palette(
                p.Scheme ?? ColorScheme.Secondary,
                p.Variant ?? ComponentVariant.Subtle,
                ui.LabelState()
            ),
            height: 16f,
            minWidth: 0f,
            padX: 6f,
            radius: t.Radius(RadiusToken.Md),
            border: 1f
        );
    }
}

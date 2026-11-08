using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>How an <see cref="Icon"/> looks when its props leave it to the icon.</summary>
public enum IconVariant : byte
{
    /// <summary>The glyph at 16, in the text colour around it.</summary>
    Plain,

    /// <summary>The look of an icon that explains what it sits beside: the <see cref="IconRole.Info"/> glyph in
    /// <see cref="ColorToken.FgMuted"/>, 2 smaller than the icon size of its rung.</summary>
    Info,
}

public readonly record struct IconProps
{
    /// <summary>The glyph; one of a role draws the glyph the context's icon set draws for it. The info variant draws
    /// <see cref="IconRole.Info"/> when unset.</summary>
    public IconGlyph Glyph { get; init; }

    /// <summary>The glyph's size, which wins over <see cref="Rung"/>; 0 leaves it to the icon.</summary>
    public float Size { get; init; }

    /// <summary>The size whose rung's icon size an unsized glyph takes, as the size of the row the icon sits in; a
    /// plain icon with none stands 16, an info icon with none takes <see cref="ComponentSize.Md"/>'s.</summary>
    public ComponentSize? Rung { get; init; }

    public IconVariant Variant { get; init; }

    /// <summary>The glyph's colour; the text colour around it by default.</summary>
    public Paint Color { get; init; }

    /// <summary>Draws the glyph upside down, as the chevron of a trigger whose popup is open.</summary>
    public bool Flipped { get; init; }
}

/// <summary>One glyph of an icon font, drawn as text in the font family the glyph names, centred in a box as wide as
/// its size and as tall as the font's line. An icon with no glyph, or of a role while the context has no icon set,
/// draws nothing and takes no space.</summary>
public sealed class Icon : Component<IconProps>
{
    const float DefaultSize = 16f;

    protected override void Render(Ui ui, in IconProps p)
    {
        var t = ui.Theme;
        var info = p.Variant == IconVariant.Info;
        var glyph = t.Icon(info && p.Glyph.IsEmpty ? IconGlyph.Of(IconRole.Info) : p.Glyph);
        if (glyph.IsEmpty)
            return;
        var size =
            p.Size > 0f ? p.Size
            : info ? t.Metrics(p.Rung ?? ComponentSize.Md).IconSize - 2f
            : p.Rung is { } rung ? t.Metrics(rung).IconSize
            : DefaultSize;
        var color =
            p.Color.Kind != PaintKind.None ? p.Color
            : info ? t.Color(ColorToken.FgMuted)
            : default(Paint);
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                width: size,
                transform: p.Flipped ? Transform.Scale(1f, -1f) : default
            )
        )
            ui.Text(glyph.Text, color: color, fontFamily: glyph.FontFamily, fontSize: size);
    }
}

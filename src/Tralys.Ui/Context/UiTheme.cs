using Tralys.Ui.Fonts;
using Tralys.Ui.Theming;

namespace Tralys.Ui;

/// <summary>The theme at a component's position, read as plain values at its density.</summary>
[FramePath]
public readonly struct UiTheme
{
    readonly Skin? _skin;
    readonly IconSet? _icons;

    internal UiTheme(Theme theme, Density density, Skin? skin, IconSet? icons)
    {
        Theme = theme;
        Density = density;
        _skin = skin;
        _icons = icons;
    }

    public Theme Theme { get; }

    public Density Density { get; }

    /// <summary>The unit spacing steps are multiples of.</summary>
    public float Spacing => Theme.Dimensions(Density).Spacing;

    public Color Color(ColorToken token) => Theme.Color(token);

    public Color Color(ComponentColor token) => Theme.Color(token);

    public ComponentPalette Palette(
        ColorScheme scheme,
        ComponentVariant variant,
        ComponentState state
    ) => Theme.Colors(scheme, variant, state);

    public ComponentPalette Field(FieldVariant variant, ComponentState state) =>
        Theme.FieldColors(variant, state);

    /// <summary>A rung of the size ramp, at this density.</summary>
    public ComponentMetrics Metrics(ComponentSize size) => Theme.Dimensions(Density).Size(size);

    public float FontSize(TextVariant variant) => Theme.Dimensions(Density).FontSize(variant);

    public CornerRadius Radius(RadiusToken token) => Theme.Dimensions(Density).Radius(token);

    public float Grip(GripToken token) => Theme.Dimensions(Density).Grip(token);

    public string FontFamily(FontToken token) => Theme.FontFamily(token);

    public FontWeight Weight(WeightToken token) => Theme.Weight(token);

    /// <summary>The theme's motion: a <paramref name="duration"/> through an <paramref name="easing"/>.</summary>
    public Transition Motion(DurationToken duration, EasingToken easing) =>
        new(Theme.Duration(duration), Theme.Easing(easing));

    /// <summary>The context's skin art for <paramref name="part"/>; null with no skin, or one that leaves the part
    /// out, when the component draws its vector look.</summary>
    public SkinPartImage? Skin(SkinPart part) =>
        _skin is { } skin && skin.TryGet(part, out var image) ? image : null;

    /// <summary>The glyph to draw for <paramref name="glyph"/>: itself, or for a role the one the context's icon set
    /// draws; none for a role while the context has no set.</summary>
    public IconGlyph Icon(IconGlyph glyph) =>
        glyph.Role is not { } role ? glyph
        : _icons is { } icons ? icons.Glyph(role)
        : default;
}

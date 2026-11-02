namespace Tralys.Ui.Theming;

/// <summary>Assembles a <see cref="Theme"/>; the token compiler's generated code is its caller, and warns of a slot of
/// the vocabulary no tokens fill, which keeps its zero.</summary>
public sealed class ThemeBuilder
{
    static readonly int ColorCount = Enum.GetValues<ColorToken>().Length;
    static readonly int ComponentCount = Enum.GetValues<ComponentColor>().Length;
    static readonly int DensityCount = Enum.GetValues<Density>().Length;
    static readonly int SizeCount = Enum.GetValues<ComponentSize>().Length;
    static readonly int TextCount = Enum.GetValues<TextVariant>().Length;
    static readonly int RadiusCount = Enum.GetValues<RadiusToken>().Length;
    static readonly int GripCount = Enum.GetValues<GripToken>().Length;

    readonly string _name;
    readonly Color[] _colors = new Color[ColorCount];
    readonly Color[] _componentColors = new Color[ComponentCount];
    readonly ComponentPalette[] _palettes = new ComponentPalette[
        Theme.SchemeCount * Theme.VariantCount * Theme.StateCount
    ];
    readonly ComponentPalette[] _fields = new ComponentPalette[
        Theme.FieldVariantCount * Theme.StateCount
    ];
    readonly string[] _families = new string[Enum.GetValues<FontToken>().Length];
    readonly int[] _weights = new int[Enum.GetValues<WeightToken>().Length];
    readonly float[] _spacing = new float[DensityCount];
    readonly ComponentMetrics[] _sizes = new ComponentMetrics[DensityCount * SizeCount];
    readonly float[] _fontSizes = new float[DensityCount * TextCount];
    readonly float[] _radii = new float[DensityCount * RadiusCount];
    readonly float[] _grips = new float[DensityCount * GripCount];
    readonly float[] _durations = new float[Enum.GetValues<DurationToken>().Length];
    readonly KeySpline[] _easings = new KeySpline[Enum.GetValues<EasingToken>().Length];

    public ThemeBuilder(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _name = name;
        Array.Fill(_families, "");
    }

    public void Color(ColorToken token, global::Tralys.Ui.Color color) =>
        _colors[(int)token] = color;

    public void Color(ComponentColor token, global::Tralys.Ui.Color color) =>
        _componentColors[(int)token] = color;

    public void Palette(
        ColorScheme scheme,
        ComponentVariant variant,
        ComponentState state,
        global::Tralys.Ui.Color background,
        global::Tralys.Ui.Color foreground,
        global::Tralys.Ui.Color border
    ) =>
        _palettes[Theme.PaletteIndex(scheme, variant, state)] = new(background, foreground, border);

    public void Field(
        FieldVariant variant,
        ComponentState state,
        global::Tralys.Ui.Color background,
        global::Tralys.Ui.Color foreground,
        global::Tralys.Ui.Color border
    ) => _fields[Theme.FieldIndex(variant, state)] = new(background, foreground, border);

    public void Font(FontToken token, string family)
    {
        ArgumentException.ThrowIfNullOrEmpty(family);
        _families[(int)token] = family;
    }

    public void Weight(WeightToken token, int weight) => _weights[(int)token] = weight;

    public void Spacing(Density density, float spacing) => _spacing[(int)density] = spacing;

    public void Size(Density density, ComponentSize size, ComponentMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        _sizes[(int)density * SizeCount + (int)size] = metrics;
    }

    public void FontSize(Density density, TextVariant variant, float size) =>
        _fontSizes[(int)density * TextCount + (int)variant] = size;

    public void Radius(Density density, RadiusToken token, float radius) =>
        _radii[(int)density * RadiusCount + (int)token] = radius;

    public void Grip(Density density, GripToken token, float size) =>
        _grips[(int)density * GripCount + (int)token] = size;

    public void Duration(DurationToken token, float seconds) => _durations[(int)token] = seconds;

    public void Easing(EasingToken token, KeySpline curve) => _easings[(int)token] = curve;

    public Theme Build()
    {
        var dimensions = new ThemeDimensions[DensityCount];
        for (var d = 0; d < DensityCount; d++)
        {
            var radii = new CornerRadius[RadiusCount];
            for (var i = 0; i < RadiusCount; i++)
                radii[i] = new CornerRadius(_radii[d * RadiusCount + i]);
            dimensions[d] = new ThemeDimensions(
                (Density)d,
                _spacing[d],
                _sizes.AsSpan(d * SizeCount, SizeCount).ToArray(),
                _fontSizes.AsSpan(d * TextCount, TextCount).ToArray(),
                radii,
                _grips.AsSpan(d * GripCount, GripCount).ToArray()
            );
        }

        return new Theme(
            _name,
            _colors,
            _componentColors,
            _palettes,
            _fields,
            _families,
            _weights,
            dimensions,
            _durations,
            _easings
        );
    }
}

namespace Tralys.Ui.Theming;

/// <summary>A control's three colours in one interaction state.</summary>
public readonly record struct ComponentPalette(Color Background, Color Foreground, Color Border);

/// <summary>A design system's values for one theme, compiled from the app's design tokens: a colour per
/// <see cref="ColorToken"/> and per <see cref="ComponentColor"/>, a palette per scheme, variant and state, the typography, the motion, and the dimensions
/// under each <see cref="Density"/>. Immutable, so contexts on any thread share one instance.</summary>
public sealed class Theme
{
    internal static readonly int SchemeCount = Enum.GetValues<ColorScheme>().Length;
    internal static readonly int VariantCount = Enum.GetValues<ComponentVariant>().Length;
    internal static readonly int FieldVariantCount = Enum.GetValues<FieldVariant>().Length;
    internal static readonly int StateCount = Enum.GetValues<ComponentState>().Length;

    readonly Color[] _colors;
    readonly Color[] _componentColors;
    readonly ComponentPalette[] _palettes;
    readonly ComponentPalette[] _fields;
    readonly string[] _families;
    readonly int[] _weights;
    readonly ThemeDimensions[] _dimensions;
    readonly float[] _durations;
    readonly KeySpline[] _easings;

    internal Theme(
        string name,
        Color[] colors,
        Color[] componentColors,
        ComponentPalette[] palettes,
        ComponentPalette[] fields,
        string[] families,
        int[] weights,
        ThemeDimensions[] dimensions,
        float[] durations,
        KeySpline[] easings
    )
    {
        Name = name;
        _colors = colors;
        _componentColors = componentColors;
        _palettes = palettes;
        _fields = fields;
        _families = families;
        _weights = weights;
        _dimensions = dimensions;
        _durations = durations;
        _easings = easings;
    }

    /// <summary>The theme's context in the token resolver.</summary>
    public string Name { get; }

    [FramePath]
    public global::Tralys.Ui.Color Color(ColorToken token) => _colors[(int)token];

    [FramePath]
    public global::Tralys.Ui.Color Color(ComponentColor token) => _componentColors[(int)token];

    /// <summary>What a control of <paramref name="scheme"/> and <paramref name="variant"/> draws with in
    /// <paramref name="state"/>.</summary>
    [FramePath]
    public ComponentPalette Colors(
        ColorScheme scheme,
        ComponentVariant variant,
        ComponentState state
    ) => _palettes[PaletteIndex(scheme, variant, state)];

    /// <summary>What a field of <paramref name="variant"/> draws with in <paramref name="state"/>.</summary>
    [FramePath]
    public ComponentPalette FieldColors(FieldVariant variant, ComponentState state) =>
        _fields[FieldIndex(variant, state)];

    /// <summary>The family name that fills <paramref name="token"/>.</summary>
    public string FontFamily(FontToken token) => _families[(int)token];

    /// <summary>The weight that fills <paramref name="token"/>, as text elements take it: what
    /// <c>{theme:Weight}</c> markup reads.</summary>
    [FramePath]
    public Fonts.FontWeight Weight(WeightToken token) => (Fonts.FontWeight)_weights[(int)token];

    [FramePath]
    public ThemeDimensions Dimensions(Density density) => _dimensions[(int)density];

    /// <summary>How long <paramref name="token"/>'s movement takes, in seconds.</summary>
    public float Duration(DurationToken token) => _durations[(int)token];

    /// <summary>The curve that fills <paramref name="token"/>, one shared per theme and token.</summary>
    public KeySpline Easing(EasingToken token) => _easings[(int)token];

    public override string ToString() => Name;

    internal static int PaletteIndex(
        ColorScheme scheme,
        ComponentVariant variant,
        ComponentState state
    ) => ((int)scheme * VariantCount + (int)variant) * StateCount + (int)state;

    internal static int FieldIndex(FieldVariant variant, ComponentState state) =>
        (int)variant * StateCount + (int)state;
}

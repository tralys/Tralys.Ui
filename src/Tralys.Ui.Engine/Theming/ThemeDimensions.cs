namespace Tralys.Ui.Theming;

/// <summary>A theme's dimensions under one <see cref="Density"/>: the size ramp, the type ramp, corner rounding and
/// grips, in layout units.</summary>
public sealed class ThemeDimensions
{
    readonly ComponentMetrics[] _sizes;
    readonly float[] _fontSizes;
    readonly CornerRadius[] _radii;
    readonly float[] _grips;

    internal ThemeDimensions(
        Density density,
        float spacing,
        ComponentMetrics[] sizes,
        float[] fontSizes,
        CornerRadius[] radii,
        float[] grips
    )
    {
        Density = density;
        Spacing = spacing;
        _sizes = sizes;
        _fontSizes = fontSizes;
        _radii = radii;
        _grips = grips;
    }

    public Density Density { get; }

    /// <summary>The unit spacing steps are multiples of.</summary>
    public float Spacing { get; }

    [FramePath]
    public ComponentMetrics Size(ComponentSize size) => _sizes[(int)size];

    /// <summary>The font size of a rung of the type ramp.</summary>
    [FramePath]
    public float FontSize(TextVariant variant) => _fontSizes[(int)variant];

    [FramePath]
    public CornerRadius Radius(RadiusToken token) => _radii[(int)token];

    [FramePath]
    public float Grip(GripToken token) => _grips[(int)token];
}

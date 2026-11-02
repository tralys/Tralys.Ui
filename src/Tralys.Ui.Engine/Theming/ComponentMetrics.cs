namespace Tralys.Ui.Theming;

/// <summary>What one rung of the size ramp sets on a control, in layout units. One instance stands for a rung under
/// one theme and density, so elements compare it by reference.</summary>
public sealed class ComponentMetrics
{
    public ComponentMetrics(
        float height,
        float padX,
        float padY,
        float fieldPadX,
        float gap,
        float fontSize,
        float iconSize,
        float measure
    )
    {
        Height = height;
        PadX = padX;
        PadY = padY;
        FieldPadX = fieldPadX;
        Gap = gap;
        FontSize = fontSize;
        IconSize = iconSize;
        Measure = measure;
    }

    /// <summary>The height a control of this size stands at least.</summary>
    public float Height { get; }

    public float PadX { get; }

    public float PadY { get; }

    /// <summary>The horizontal padding of a field, which sits its text nearer its edge than a button does.</summary>
    public float FieldPadX { get; }

    /// <summary>The space between a control's icon and its content.</summary>
    public float Gap { get; }

    public float FontSize { get; }

    public float IconSize { get; }

    /// <summary>The width a control of this size takes when nothing else sizes it.</summary>
    public float Measure { get; }

    public Thickness Padding => new(PadX, PadY, PadX, PadY);

    public Thickness PaddingX => new(PadX, 0f, PadX, 0f);

    public Thickness PaddingY => new(0f, PadY, 0f, PadY);

    public Thickness FieldPadding => new(FieldPadX, PadY, FieldPadX, PadY);

    public Thickness FieldPaddingX => new(FieldPadX, 0f, FieldPadX, 0f);

    /// <summary><see cref="Gap"/> before content that follows an icon.</summary>
    public Thickness GapStart => new(Gap, 0f, 0f, 0f);

    /// <summary><see cref="Gap"/> after an icon that content follows.</summary>
    public Thickness GapEnd => new(0f, 0f, Gap, 0f);

    /// <summary><see cref="PadX"/> on every side.</summary>
    public Thickness Inset => new(PadX);
}

namespace Tralys.Ui.Rendering;

/// <summary>A shadow a rounded box casts, as CSS's box-shadow draws it: the box moved by the offset and grown by
/// <see cref="Spread"/>, blurred by a Gaussian whose standard deviation is half <see cref="Blur"/>. An outer shadow
/// shows only outside its box, an inset one only inside it, where it shades in from the edges.</summary>
public readonly record struct BoxShadow(
    float OffsetX,
    float OffsetY,
    float Blur,
    float Spread,
    Color Color,
    bool IsInset = false
);

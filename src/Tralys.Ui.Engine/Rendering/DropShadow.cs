namespace Tralys.Ui.Rendering;

/// <summary>A blurred, offset copy of what a layer draws, painted beneath it, as WPF's DropShadowEffect draws it:
/// <see cref="Direction"/> in degrees counter-clockwise from +X, <see cref="ShadowDepth"/> and
/// <see cref="BlurRadius"/> in local units.</summary>
public readonly record struct DropShadow(
    Color Color,
    float Direction = 315f,
    float ShadowDepth = 5f,
    float BlurRadius = 5f,
    float Opacity = 1f
);

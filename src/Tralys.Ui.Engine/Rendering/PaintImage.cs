using Tralys.Ui.Media;

namespace Tralys.Ui.Rendering;

/// <summary>What an image <see cref="Paint"/> lays over the bounds it paints, as WPF's ImageBrush does without tiling:
/// the <see cref="Viewbox"/> of the image, scaled into those bounds as <see cref="Stretch"/> says and placed in them as
/// <see cref="AlignmentX"/> and <see cref="AlignmentY"/> say. Nothing paints outside it. A keyed
/// <see cref="Source"/> is resolved through the texture provider of the context of the view drawing it.</summary>
public sealed class PaintImage
{
    public PaintImage(ImageSource source) => Source = source;

    public ImageSource Source { get; }

    /// <summary>The part of the image painted: fractions of it, or texels when <see cref="ViewboxUnits"/> is
    /// <see cref="GradientSpace.Absolute"/>. The whole image by default.</summary>
    public Rect Viewbox { get; init; } = new(0f, 0f, 1f, 1f);

    public GradientSpace ViewboxUnits { get; init; }

    public Stretch Stretch { get; init; } = Stretch.Fill;

    public AlignmentX AlignmentX { get; init; } = AlignmentX.Center;

    public AlignmentY AlignmentY { get; init; } = AlignmentY.Center;
}

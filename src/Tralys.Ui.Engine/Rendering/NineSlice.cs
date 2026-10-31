namespace Tralys.Ui.Rendering;

public enum SliceFill : byte
{
    Stretch,

    /// <summary>The edges and centre repeat at their own size, the last tile cut short.</summary>
    Tile,
}

/// <summary>How an image is cut into a 3×3 grid: <see cref="Slice"/> insets in source texels, the corners drawn at
/// <see cref="Scale"/> destination units per texel and the edges and centre filling between them. Each cell's source
/// is pulled in by <see cref="TexelInset"/> texels, so filtering never reads the cell beside it.</summary>
public readonly record struct NineSlice(
    Thickness Slice,
    float Scale = 1f,
    SliceFill Fill = SliceFill.Stretch,
    float TexelInset = 0f
);

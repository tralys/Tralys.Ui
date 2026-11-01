using Tralys.Ui.Rendering;

namespace Tralys.Ui.Fonts.Tests;

sealed class MemoryTexture(int width, int height) : WritableTexture
{
    public byte[] Texels { get; } = new byte[width * height];

    public override int Width => width;

    public override int Height => height;

    public override void Write(
        int x,
        int y,
        int width,
        int height,
        ReadOnlySpan<byte> texels,
        int stride
    )
    {
        for (var i = 0; i < height; i++)
            texels.Slice(i * stride, width).CopyTo(Texels.AsSpan((y + i) * Width + x));
    }

    public override void Dispose() { }
}

sealed class MemoryTextureFactory : ITextureFactory
{
    public WritableTexture CreateWritable(int width, int height) =>
        new MemoryTexture(width, height);
}

// Pages that start full, as one a glyph atlas starts over on still holds what it packed before.
sealed class SoiledTextureFactory : ITextureFactory
{
    public WritableTexture CreateWritable(int width, int height)
    {
        var texture = new MemoryTexture(width, height);
        Array.Fill(texture.Texels, byte.MaxValue);
        return texture;
    }
}

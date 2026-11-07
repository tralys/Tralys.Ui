using Tralys.Ui.Rendering;

namespace Tralys.Ui.Gallery.Textures;

/// <summary>Textures that reach no GPU, for laying stories out and recording them without a renderer: written
/// textures keep nothing, and images resolve to their size alone, read from the gallery's asset files.</summary>
public sealed class MemoryTextures : ITextureFactory, ITextureProvider
{
    readonly Dictionary<string, RenderTexture?> _byKey = new(StringComparer.Ordinal);

    public WritableTexture CreateWritable(int width, int height) => new Written(width, height);

    public RenderTexture? Resolve(string source)
    {
        if (_byKey.TryGetValue(source, out var known))
            return known;

        RenderTexture? texture = null;
        if (AssetTextures.Locate(source) is { } path && ImageHeader.TryRead(path, out var header))
            texture = new Sized(header.Width, header.Height);
        _byKey.Add(source, texture);
        return texture;
    }

    sealed class Written(int width, int height) : WritableTexture
    {
        public override int Width => width;

        public override int Height => height;

        public override void Write(
            int x,
            int y,
            int width,
            int height,
            ReadOnlySpan<byte> texels,
            int stride
        ) { }

        public override void Dispose() { }
    }

    sealed class Sized(int width, int height) : RenderTexture
    {
        public override int Width => width;

        public override int Height => height;
    }
}

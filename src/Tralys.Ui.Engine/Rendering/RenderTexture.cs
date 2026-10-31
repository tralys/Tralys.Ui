namespace Tralys.Ui.Rendering;

/// <summary>A texture a renderer samples. Each backend defines the subclasses that reach its GPU. Its size is known
/// from the start, even while its pixels load; a backend draws a texture without pixels as nothing.</summary>
public abstract class RenderTexture
{
    [FramePath]
    public abstract int Width { get; }

    [FramePath]
    public abstract int Height { get; }

    /// <summary>Whether texels already carry premultiplied colour; straight-alpha texels are premultiplied as they are sampled.</summary>
    [FramePath]
    public virtual bool IsPremultiplied => false;

    /// <summary>Bumped when the texels are given up to be repacked, so source rectangles recorded before no longer
    /// address what they did. A glyph run recorded against an older generation draws nothing, and its buffer reports
    /// <see cref="RenderCommandBuffer.IsStale"/>.</summary>
    public int Generation { get; private set; }

    protected internal void BumpGeneration() => Generation++;

    /// <summary>What a frame samples for this texture as it is flattened: the texture itself, or for one living in a
    /// layer of an array, that array, with the layer in <paramref name="layer"/>. It is asked again before a frame is
    /// handed back unchanged, which rebuilds when the answer moved, so a texture may move to another layer, or into an
    /// array once its pixels land there; being asked every frame, it stays cheap.</summary>
    [FramePath]
    public virtual RenderTexture Bind(out int layer)
    {
        layer = 0;
        return this;
    }
}

/// <summary>One glyph of a run: where it lands in local space, and the texel rectangle of its coverage in the atlas.</summary>
public readonly record struct GlyphQuad(Rect Destination, Rect Source);

/// <summary>A texture Tralys.Ui writes into, such as a glyph atlas: one byte per texel, read from the red channel. Its
/// texels start as zero.</summary>
public abstract class WritableTexture : RenderTexture, IDisposable
{
    /// <summary>Replaces the texels of the region at (<paramref name="x"/>, <paramref name="y"/>) with
    /// <paramref name="texels"/>, <paramref name="stride"/> bytes per row; they draw from the next frame submitted.</summary>
    [FramePath]
    public abstract void Write(
        int x,
        int y,
        int width,
        int height,
        ReadOnlySpan<byte> texels,
        int stride
    );

    public abstract void Dispose();
}

/// <summary>Creates the textures Tralys.Ui writes into. Each backend implements it.</summary>
public interface ITextureFactory
{
    WritableTexture CreateWritable(int width, int height);
}

/// <summary>Finds the images views draw by name. The host implements it over its assets.</summary>
public interface ITextureProvider
{
    /// <summary>The texture named <paramref name="source"/>, or null when nothing has that name. Its size is known on
    /// return. While its pixels load, and after the host evicts them, it draws nothing; drawing it asks for them again,
    /// so a caller keeps the texture instead of resolving the name again.</summary>
    RenderTexture? Resolve(string source);
}

interface ITextureResolver
{
    [FramePath]
    RenderTexture? ResolveTexture(string key);
}

namespace Tralys.Ui.Rendering;

/// <summary>Same-sized layers in one texture, such as a pool of icons. Draws from any of its layers share a batch.
/// <see cref="RenderTexture.Width"/> and <see cref="RenderTexture.Height"/> are one layer's.</summary>
public abstract class RenderTextureArray : RenderTexture
{
    public abstract int LayerCount { get; }
}

/// <summary>One layer of a <see cref="RenderTextureArray"/>, drawn wherever a texture is.</summary>
public sealed class RenderTextureLayer : RenderTexture
{
    public RenderTextureLayer(RenderTextureArray array, int layer)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(layer);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(layer, array.LayerCount);
        Array = array;
        Layer = layer;
    }

    public RenderTextureArray Array { get; }

    public int Layer { get; }

    /// <inheritdoc/>
    [FramePath]
    public override RenderTexture Bind(out int layer)
    {
        layer = Layer;
        return Array;
    }

    public override int Width => Array.Width;

    public override int Height => Array.Height;
}

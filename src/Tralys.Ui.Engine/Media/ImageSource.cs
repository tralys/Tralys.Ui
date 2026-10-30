using Tralys.Ui.Rendering;

namespace Tralys.Ui.Media;

/// <summary>The image an element draws: a key that the <c>UiContext.TextureProvider</c> of the element's view
/// resolves, or a texture already in hand. A value type, so a literal or a bound string makes one without allocating;
/// the default draws nothing.</summary>
public readonly struct ImageSource : IEquatable<ImageSource>
{
    /// <summary>The image the host's provider knows as <paramref name="key"/>, such as an asset path.</summary>
    public ImageSource(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        Key = key;
    }

    /// <summary><paramref name="texture"/> itself, which no provider resolves, such as a render target or a layer of
    /// an icon array.</summary>
    public ImageSource(RenderTexture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        Texture = texture;
    }

    /// <summary>The name the provider resolves; null for a texture source or none.</summary>
    public string? Key { get; }

    /// <summary>The texture drawn as is; null for a keyed source or none.</summary>
    public RenderTexture? Texture { get; }

    public bool IsEmpty => Key is null && Texture is null;

    /// <summary>The image named <paramref name="key"/>; none when it is null or empty.</summary>
    public static implicit operator ImageSource(string? key) =>
        string.IsNullOrEmpty(key) ? default : new ImageSource(key);

    /// <summary><paramref name="texture"/> itself; none when it is null.</summary>
    public static implicit operator ImageSource(RenderTexture? texture) =>
        texture is null ? default : new ImageSource(texture);

    public bool Equals(ImageSource other) =>
        ReferenceEquals(Texture, other.Texture)
        && string.Equals(Key, other.Key, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ImageSource other && Equals(other);

    public override int GetHashCode() =>
        Key is { } key ? StringComparer.Ordinal.GetHashCode(key) : Texture?.GetHashCode() ?? 0;

    [FramePath]
    internal RenderTexture? Resolve(ITextureResolver? textures) =>
        Texture ?? (Key is { } key ? textures?.ResolveTexture(key) : null);

    public static bool operator ==(ImageSource left, ImageSource right) => left.Equals(right);

    public static bool operator !=(ImageSource left, ImageSource right) => !left.Equals(right);

    public override string ToString() => Key ?? Texture?.ToString() ?? "(none)";
}

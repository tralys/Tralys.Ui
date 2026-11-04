using Tralys.Ui.Media;
using Tralys.Ui.Rendering;

namespace Tralys.Ui;

public sealed partial class UiContext
{
    readonly Dictionary<string, RenderTexture?> _textures = new(StringComparer.Ordinal);
    Skin? _skin;

    /// <summary>The art components draw for their parts in place of their vector looks; null draws every part's vector
    /// look. A change re-renders every mounted host that read the theme, as a theme change does.</summary>
    /// <exception cref="InvalidOperationException">Set from another thread.</exception>
    public Skin? Skin
    {
        get => _skin;
        set
        {
            Dispatcher.VerifyAccess();
            if (ReferenceEquals(_skin, value))
                return;
            _skin = value;
            foreach (var view in _views)
                view.SkinChanged();
        }
    }

    /// <summary>The texture <paramref name="key"/> names through <see cref="Textures"/>, resolved once and kept for the
    /// context's life, as an image's is, so a canvas draws what an image of that key would; null without a provider or
    /// for a key it doesn't know.</summary>
    [FramePath]
    public RenderTexture? ResolveTexture(string key)
    {
        if (Textures is not { } provider)
            return null;
        if (_textures.TryGetValue(key, out var texture))
            return texture;
        texture = provider.Resolve(key);
        _textures[key] = texture;
        return texture;
    }

    /// <summary>The texture <paramref name="source"/> holds, or the one its key names, as
    /// <see cref="ResolveTexture(string)"/> resolves it.</summary>
    [FramePath]
    public RenderTexture? ResolveTexture(in ImageSource source) =>
        source.Texture ?? (source.Key is { } key ? ResolveTexture(key) : null);
}

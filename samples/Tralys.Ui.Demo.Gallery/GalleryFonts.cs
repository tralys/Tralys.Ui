using Tralys.Ui.Fonts;
using Tralys.Ui.FreeType;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.Gallery;

/// <summary>The faces stories are set in, all embedded: Inter, which text naming no
/// family is set in, its heavier weights, Cascadia Code and the Lucide icon font.</summary>
public sealed class GalleryFonts : IDisposable
{
    static readonly string[] Faces =
    [
        "Inter-Medium.otf",
        "Inter-SemiBold.otf",
        "Inter-Bold.otf",
        "CascadiaCode-Regular.otf",
        "Lucide.ttf",
    ];

    readonly FreeTypeBackend _backend = new();
    readonly FontCollection _fonts;

    /// <param name="textures">What the glyph atlas's pages are made with: a renderer's textures to draw, or memory
    /// ones to lay out without a GPU.</param>
    public GalleryFonts(ITextureFactory textures)
    {
        ArgumentNullException.ThrowIfNull(textures);
        _fonts = new FontCollection(_backend);
        var inter = _fonts.Add(Embedded("Inter-Regular.otf"));
        foreach (var name in Faces)
            _fonts.Add(Embedded(name));
        Services = new TextServices(_fonts, inter, textures);
    }

    public TextServices Services { get; }

    static byte[] Embedded(string name)
    {
        using var stream =
            typeof(GalleryFonts).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"{name} is not embedded.");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    public void Dispose()
    {
        Services.Dispose();
        _fonts.Dispose();
        _backend.Dispose();
    }
}

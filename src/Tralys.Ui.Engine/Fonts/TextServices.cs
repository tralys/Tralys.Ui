using Tralys.Ui.Rendering;

namespace Tralys.Ui.Fonts;

/// <summary>What one <c>UiContext</c>'s text elements set their text with: the faces they pick from, the face
/// they fall back to, and the glyph atlas they all draw through. The host builds it once per context, before any view
/// lays out text, and disposes it after the context's views and before the renderer that owns the atlas's
/// textures.</summary>
public sealed class TextServices : IDisposable
{
    readonly FontCollection _fonts;
    readonly Font _defaultFont;

    /// <param name="fonts">The faces a <c>FontFamily</c> names, with their fallback chain.</param>
    /// <param name="defaultFont">The face text that names no family, or a family the collection lacks, is set in;
    /// other weights and slants are looked up in its family.</param>
    /// <param name="textures">Creates the atlas's pages.</param>
    /// <param name="atlasPageSize">Each atlas page's width and height in texels.</param>
    /// <param name="atlasMaxPages">How many pages the atlas opens before its least recently used one starts over, which
    /// takes that page a second texture; a frame that draws more glyphs than they hold opens more.</param>
    public TextServices(
        FontCollection fonts,
        Font defaultFont,
        ITextureFactory textures,
        int atlasPageSize = 2048,
        int atlasMaxPages = GlyphAtlas.DefaultMaxPages
    )
    {
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(defaultFont);
        ArgumentNullException.ThrowIfNull(textures);
        if (defaultFont.Collection != fonts)
            throw new ArgumentException(
                "The default font belongs to another collection.",
                nameof(defaultFont)
            );

        _fonts = fonts;
        _defaultFont = defaultFont;
        Atlas = new GlyphAtlas(textures, atlasPageSize, atlasMaxPages);
    }

    public GlyphAtlas Atlas { get; }

    /// <summary>The face of <paramref name="family"/> nearest <paramref name="weight"/> and <paramref name="style"/>;
    /// for a null family, or one the collection has no face of, the nearest in the default font's family.</summary>
    [FramePath]
    public Font Resolve(string? family, FontWeight weight, FontStyle style)
    {
        var italic = style != FontStyle.Normal;
        if (family is null && (int)weight == _defaultFont.Weight && italic == _defaultFont.IsItalic)
            return _defaultFont;

        return (family is null ? null : _fonts.Find(family, (int)weight, italic))
            ?? _fonts.Find(_defaultFont.FamilyName, (int)weight, italic)
            ?? _defaultFont;
    }

    /// <summary>Disposes the atlas and its pages; the fonts stay the caller's.</summary>
    public void Dispose() => Atlas.Dispose();
}

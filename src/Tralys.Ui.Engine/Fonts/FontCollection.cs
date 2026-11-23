using System.Text;

namespace Tralys.Ui.Fonts;

/// <summary>The faces text can be set in, opened through one <see cref="IFontBackend"/>, FreeType unless another is
/// given, and the fallback chain tried, in order, for a character the requested face has no glyph for, such as a CJK
/// face behind a Latin one.</summary>
public sealed class FontCollection : IDisposable
{
    static readonly Rune Space = new(' ');

    readonly IFontBackend _backend;
    readonly FreeTypeBackend? _freeType;
    readonly List<Font> _fonts = [];
    readonly List<Font> _fallbacks = [];

    /// <summary>A collection on FreeType, whose faces and their library belong to the thread that uses it.</summary>
    public FontCollection() => _backend = _freeType = new FreeTypeBackend();

    public FontCollection(IFontBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        _backend = backend;
    }

    /// <summary>Opens face <paramref name="faceIndex"/> of <paramref name="fontData"/>, which the backend may read until
    /// the collection is disposed.</summary>
    public Font Add(ReadOnlyMemory<byte> fontData, int faceIndex = 0)
    {
        var font = new Font(this, _backend.LoadFace(fontData, faceIndex));
        _fonts.Add(font);
        return font;
    }

    /// <summary>Appends <paramref name="font"/> to the fallback chain.</summary>
    public void AddFallback(Font font)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (font.Collection != this)
            throw new ArgumentException("The font belongs to another collection.", nameof(font));

        if (!_fallbacks.Contains(font))
            _fallbacks.Add(font);
    }

    /// <summary>The face of <paramref name="familyName"/> CSS font matching picks for <paramref name="weight"/> and
    /// slant: the asked-for slant first, then for 400 to 500 the nearest weight up to 500, then lighter, then heavier;
    /// below 400 lighter first, above 500 heavier first. Null when the collection has no face of that family.</summary>
    [FramePath]
    public Font? Find(string familyName, int weight = 400, bool italic = false)
    {
        ArgumentNullException.ThrowIfNull(familyName);

        Font? best = null;
        var bestRank = long.MaxValue;
        foreach (var font in _fonts)
        {
            if (!string.Equals(font.FamilyName, familyName, StringComparison.OrdinalIgnoreCase))
                continue;

            var rank = WeightRank(weight, font.Weight) + (font.IsItalic == italic ? 0L : 1L << 40);
            if (rank < bestRank)
            {
                best = font;
                bestRank = rank;
            }
        }
        return best;
    }

    [FramePath]
    static long WeightRank(int wanted, int candidate)
    {
        var distance = Math.Abs(candidate - wanted);
        int tier;
        if (wanted is >= 400 and <= 500)
            tier =
                candidate >= wanted && candidate <= 500 ? 0
                : candidate < wanted ? 1
                : 2;
        else if (wanted < 400)
            tier = candidate <= wanted ? 0 : 1;
        else
            tier = candidate >= wanted ? 0 : 1;
        return ((long)tier << 20) + distance;
    }

    /// <summary>The face that draws <paramref name="rune"/> for text set in <paramref name="font"/>: the font itself,
    /// taking its own space for a space it has no glyph for, else the first fallback with a glyph for it, else the
    /// font, whose glyph 0 draws as a missing-glyph box.</summary>
    [FramePath]
    public Font Resolve(Font font, Rune rune, out int glyph)
    {
        ArgumentNullException.ThrowIfNull(font);

        glyph = font.Face.GetGlyphIndex(rune);
        if (glyph != 0)
            return font;

        if (Rune.IsWhiteSpace(rune) && font.Face.GetGlyphIndex(Space) is var space and not 0)
        {
            glyph = space;
            return font;
        }

        foreach (var fallback in _fallbacks)
        {
            if (fallback == font)
                continue;

            glyph = fallback.Face.GetGlyphIndex(rune);
            if (glyph != 0)
                return fallback;
        }

        glyph = 0;
        return font;
    }

    /// <summary>Disposes every face the collection opened, and the FreeType library it opened them in.</summary>
    public void Dispose()
    {
        foreach (var font in _fonts)
            font.Face.Dispose();

        _fonts.Clear();
        _fallbacks.Clear();
        _freeType?.Dispose();
    }
}

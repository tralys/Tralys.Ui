using System.Numerics;
using System.Text;

namespace Tralys.Ui.Fonts;

/// <summary>Opens font faces for the text engine, which does layout, kerning, fallback, scaling, distance fields and
/// atlas packing itself; a backend only reads what a face's files hold.</summary>
public interface IFontBackend
{
    /// <summary>Opens face <paramref name="faceIndex"/> of a TrueType, OpenType (CFF included) or collection file. The
    /// backend may read <paramref name="fontData"/> until the face is disposed.</summary>
    IFontFace LoadFace(ReadOnlyMemory<byte> fontData, int faceIndex = 0);
}

/// <summary>One face of a font, measured in its font units, <see cref="UnitsPerEm"/> to the em, and unhinted; the text
/// engine scales every size from them.</summary>
public interface IFontFace : IDisposable
{
    string FamilyName { get; }

    /// <summary>The OS/2 weight class: 400 regular, 700 bold.</summary>
    int Weight { get; }

    bool IsItalic { get; }

    /// <summary>The font units in an em, which the values in its tables are measured in.</summary>
    int UnitsPerEm { get; }

    /// <summary>The face's line metrics in font units, as its tables give them: <see cref="FaceMetrics.LineGap"/> is
    /// the face's own gap between lines, which may be negative where its line is shorter than its ascent and
    /// descent.</summary>
    FaceMetrics Metrics { get; }

    /// <summary>The face's glyph for <paramref name="rune"/>, or 0 when it has none and a fallback face is tried.</summary>
    [FramePath]
    int GetGlyphIndex(Rune rune);

    /// <summary>How far the pen moves past <paramref name="glyph"/>, in font units.</summary>
    [FramePath]
    float GetAdvance(int glyph);

    /// <summary>Replays <paramref name="glyph"/>'s outline into <paramref name="sink"/> in font units, y up: each
    /// contour a <see cref="IGlyphOutlineSink.MoveTo"/> and the segments from there, closing back to where it
    /// started.</summary>
    /// <returns>False when the glyph has no outline, such as a space, or one the face cannot read; the sink then
    /// discards whatever it was given.</returns>
    [FramePath]
    bool TryReadOutline<TSink>(int glyph, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct;

    /// <summary>The raw bytes of the sfnt table <paramref name="tag"/>, such as <c>kern</c> or <c>GPOS</c>, valid
    /// until the face is disposed; empty when the face has none.</summary>
    ReadOnlyMemory<byte> GetTable(uint tag);
}

/// <summary>Takes a glyph's outline from <see cref="IFontFace.TryReadOutline{TSink}"/>.</summary>
public interface IGlyphOutlineSink
{
    /// <summary>Starts a contour at <paramref name="point"/>, closing the one before it.</summary>
    void MoveTo(Vector2 point);

    void LineTo(Vector2 point);

    void QuadraticTo(Vector2 control, Vector2 point);

    void CubicTo(Vector2 control1, Vector2 control2, Vector2 point);
}

/// <summary>A face's line metrics. <see cref="Ascent"/> and <see cref="Descent"/> are both positive, above and below
/// the baseline; <see cref="UnderlinePosition"/> is below the baseline when positive.</summary>
public readonly record struct FaceMetrics(
    float Ascent,
    float Descent,
    float LineGap,
    float UnderlinePosition,
    float UnderlineThickness
);

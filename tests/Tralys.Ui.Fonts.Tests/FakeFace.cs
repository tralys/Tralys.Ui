using System.Numerics;
using System.Text;

namespace Tralys.Ui.Fonts.Tests;

sealed class FakeFace(byte[] kern, int unitsPerEm, int weight = 400) : IFontFace
{
    public string FamilyName => "Fake";

    public int Weight => weight;

    public bool IsItalic => false;

    public int UnitsPerEm => unitsPerEm;

    public FaceMetrics Metrics =>
        new(unitsPerEm * 0.8f, unitsPerEm * 0.2f, 0f, unitsPerEm * 0.1f, unitsPerEm * 0.05f);

    public Dictionary<int, Vector2[][]> Outlines { get; } = [];

    public int GetGlyphIndex(Rune rune) =>
        rune.Value is >= 'A' and <= 'Z' ? rune.Value - 'A' + 1 : 0;

    public float GetAdvance(int glyph) => unitsPerEm;

    public bool TryReadOutline<TSink>(int glyph, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        if (!Outlines.TryGetValue(glyph, out var contours))
            return false;

        foreach (var contour in contours)
        {
            sink.MoveTo(contour[0]);
            foreach (var point in contour.AsSpan(1))
                sink.LineTo(point);
        }
        return true;
    }

    public ReadOnlyMemory<byte> GetTable(uint tag) =>
        tag switch
        {
            0x6B65726E => kern,
            _ => default,
        };

    public void Dispose() { }

    public static Vector2[] Square(float left, float bottom, float right, float top) =>
        [new(left, bottom), new(right, bottom), new(right, top), new(left, top)];
}

sealed class FakeBackend(params IFontFace[] faces) : IFontBackend
{
    int _next;

    public IFontFace LoadFace(ReadOnlyMemory<byte> fontData, int faceIndex = 0) => faces[_next++];
}

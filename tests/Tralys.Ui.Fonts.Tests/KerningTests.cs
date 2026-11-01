using System.Text;

namespace Tralys.Ui.Fonts.Tests;

public sealed class KerningTests
{
    [Test]
    public async Task AGposClassPair_PullsTheSecondGlyphIn()
    {
        using var fonts = new TestFonts();

        var (advance, x) = SecondGlyph(fonts.Inter, "AV");

        // Inter kerns A V by -140 of 2048 units through PairPos format 2.
        await Assert.That(x).IsEqualTo(advance - 140 * 64 / 2048f);
    }

    [Test]
    public async Task AGposGlyphPair_PullsTheSecondGlyphIn()
    {
        using var fonts = new TestFonts();

        var (advance, x) = SecondGlyph(fonts.Inter, "F.");

        // Inter kerns F period by -69 of 2048 units through PairPos format 1.
        await Assert.That(x).IsEqualTo(advance - 69 * 64 / 2048f);
    }

    [Test]
    public async Task AKernTablePair_PullsTheSecondGlyphIn_WhenTheFaceHasNoGpos()
    {
        var face = new FakeFace(KernTable(left: 1, right: 22, value: -100), unitsPerEm: 1000);
        using var collection = new FontCollection(new FakeBackend(face));
        var font = collection.Add(ReadOnlyMemory<byte>.Empty);

        var (advance, x) = SecondGlyph(font, "AV", size: 10f);

        await Assert.That(advance).IsEqualTo(10f);
        await Assert.That(x).IsEqualTo(9f);
    }

    [Test]
    public async Task AnEmWideGlyph_AdvancesExactlyItsPixelSize()
    {
        using var fonts = new TestFonts();

        var face = fonts.Collection.Resolve(fonts.Cjk, new Rune('你'), out var glyph);

        await Assert.That(face.GetAdvance(glyph, 12f)).IsEqualTo(12f);
    }

    static (float Advance, float X) SecondGlyph(Font font, string pair, float size = 64f)
    {
        var layout = new TextLayout();
        TestFonts.SetText(font, pair, size, Color.FromRgb(255, 255, 255), layout);

        var first = font.Collection.Resolve(font, new Rune(pair[0]), out var glyph);
        return (first.GetAdvance(glyph, size), layout.Glyphs[1].X);
    }

    static byte[] KernTable(int left, int right, short value)
    {
        byte[] table =
        [
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            20,
            0,
            1,
            0,
            1,
            0,
            6,
            0,
            0,
            0,
            0,
            (byte)(left >> 8),
            (byte)left,
            (byte)(right >> 8),
            (byte)right,
            (byte)(value >> 8),
            (byte)value,
        ];
        return table;
    }
}

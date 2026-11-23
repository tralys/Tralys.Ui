using System.Numerics;
using System.Runtime.InteropServices;
using FreeType.Net;
using static FreeType.Net.FT;

namespace Tralys.Ui.Fonts.Tests;

public sealed class GlyphFieldTests
{
    const int LevelsPerTexel = 32;

    [Test]
    public async Task ATexel_HoldsItsDistanceFromTheEdge_RoundedToTheNearestLevel()
    {
        using var fake = new FakeFonts(FakeFace.Square(0.1f, 0.1f, 9.9f, 9.9f));
        var glyph = fake.Field();

        await Assert.That(LevelAt(glyph, 0.5, 5.5)).IsEqualTo((byte)141);
        await Assert.That(LevelAt(glyph, -0.5, 5.5)).IsEqualTo((byte)109);
        await Assert.That(LevelAt(glyph, 5.5, 5.5)).IsEqualTo((byte)255);
    }

    [Test]
    public async Task ContoursThatOverlapWindingTheSameWay_FillTheirOverlap()
    {
        using var fake = new FakeFonts(FakeFace.Square(0, 0, 6, 6), FakeFace.Square(3, 3, 9, 9));

        await Assert.That(LevelAt(fake.Field(), 4.5, 4.5)).IsGreaterThan((byte)128);
    }

    [Test]
    public async Task AnEdgeInsideAnotherContour_IsNoEdgeOfTheField()
    {
        using var fake = new FakeFonts(FakeFace.Square(0, 0, 6, 6), FakeFace.Square(3, 3, 9, 9));

        await Assert
            .That(LevelAt(fake.Field(), 6.5, 4.5))
            .IsEqualTo((byte)(128 + 1.5 * LevelsPerTexel));
    }

    [Test]
    public async Task AStroke_IsInsideWithinHalfItsWidthOfTheOutline_AndFallsOffAsFarPastIt()
    {
        using var fake = new FakeFonts(FakeFace.Square(0, 0, 10, 10));
        var glyph = fake.Field(strokeWidth: 6f);

        await Assert.That(LevelAt(glyph, 5.5, 0.5)).IsEqualTo((byte)(128 + 2.5 * LevelsPerTexel));
        await Assert.That(LevelAt(glyph, 5.5, 5.5)).IsEqualTo((byte)(128 - 1.5 * LevelsPerTexel));
        await Assert.That(LevelAt(glyph, 5.5, -5.5)).IsEqualTo((byte)(128 - 2.5 * LevelsPerTexel));
    }

    [Test]
    [Arguments("Inter-Regular.otf", 0x21, 0x7E)]
    [Arguments("Lucide.ttf", 0xE000, 0xE0FF)]
    public async Task AFacesField_IsInsideWhereFreeTypeFillsItsOutline_AndOutsideWhereItLeavesItEmpty(
        string file,
        int first,
        int last
    )
    {
        var data = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", file));
        using var fonts = new FontCollection();
        var font = fonts.Add(data);
        using var atlas = new GlyphAtlas(new MemoryTextureFactory());

        var glyphs = 0;
        var disagreements = 0;
        for (var rune = first; rune <= last; rune++)
        {
            var glyph = font.Face.GetGlyphIndex(new System.Text.Rune(rune));
            if (glyph == 0)
                continue;

            glyphs++;
            disagreements += Disagreements(atlas, font, data, glyph);
        }

        await Assert.That(glyphs).IsGreaterThan(20);
        await Assert.That(disagreements).IsEqualTo(0);
    }

    // A texel FreeType fills fully lies half a texel inside the outline, one it leaves clear with its neighbours as far out.
    static int Disagreements(GlyphAtlas atlas, Font font, byte[] data, int glyph)
    {
        var coverage = CoverageOf(
            data,
            glyph,
            out var left,
            out var top,
            out var width,
            out var height
        );
        if (coverage is null)
            return 0;
        if (!atlas.TryGet(font, glyph, GlyphAtlas.EmSize, 0f, out var field))
            return 1;

        var count = 0;
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var level = LevelAt(field, left + column + 0.5, top - row - 0.5);
                var full = coverage[row * width + column] == 255;
                var clear = true;
                for (var y = Math.Max(0, row - 1); y <= Math.Min(height - 1, row + 1); y++)
                {
                    for (var x = Math.Max(0, column - 1); x <= Math.Min(width - 1, column + 1); x++)
                        clear &= coverage[y * width + x] == 0;
                }

                if ((full && level < 136) || (clear && level > 120))
                    count++;
            }
        }
        return count;
    }

    static unsafe byte[]? CoverageOf(
        byte[] data,
        int glyph,
        out int left,
        out int top,
        out int width,
        out int height
    )
    {
        left = top = width = height = 0;
        FT_LibraryRec* library;
        FT_Init_FreeType(&library);
        try
        {
            fixed (byte* font = data)
            {
                FT_FaceRec* face;
                FT_New_Memory_Face(library, font, new CLong(data.Length), new CLong(0), &face);
                FT_Set_Char_Size(
                    face,
                    new CLong(0),
                    new CLong((nint)(GlyphAtlas.EmSize * 64)),
                    0,
                    0
                );
                if (
                    FT_Load_Glyph(face, (uint)glyph, FT_LOAD.FT_LOAD_NO_HINTING)
                        != FT_Error.FT_Err_Ok
                    || FT_Render_Glyph(face->glyph, FT_Render_Mode.FT_RENDER_MODE_NORMAL)
                        != FT_Error.FT_Err_Ok
                    || face->glyph->bitmap.width == 0
                )
                    return null;

                var bitmap = face->glyph->bitmap;
                left = face->glyph->bitmap_left;
                top = face->glyph->bitmap_top;
                width = (int)bitmap.width;
                height = (int)bitmap.rows;
                var texels = new byte[width * height];
                for (var row = 0; row < height; row++)
                    new ReadOnlySpan<byte>(bitmap.buffer + row * bitmap.pitch, width).CopyTo(
                        texels.AsSpan(row * width)
                    );
                FT_Done_Face(face);
                return texels;
            }
        }
        finally
        {
            FT_Done_FreeType(library);
        }
    }

    static byte LevelAt(in AtlasGlyph glyph, double x, double y)
    {
        var page = (MemoryTexture)glyph.Page;
        var column = (int)glyph.Source.X + (int)Math.Floor(x) - glyph.Left;
        var row = (int)glyph.Source.Y + glyph.Top - 1 - (int)Math.Floor(y);
        return page.Texels[row * page.Width + column];
    }

    // A font unit is a texel at the atlas's em size, so outlines are traced in texels.
    sealed class FakeFonts : IDisposable
    {
        readonly FontCollection _collection;
        readonly Font _font;
        readonly GlyphAtlas _atlas = new(new MemoryTextureFactory(), pageSize: 128);

        public FakeFonts(params Vector2[][] contours)
        {
            var face = new FakeFace([], (int)GlyphAtlas.EmSize);
            face.Outlines[1] = contours;
            _collection = new FontCollection(new FakeBackend(face));
            _font = _collection.Add(ReadOnlyMemory<byte>.Empty);
        }

        public AtlasGlyph Field(float strokeWidth = 0f)
        {
            _atlas.TryGet(_font, 1, GlyphAtlas.EmSize, strokeWidth, out var glyph);
            return glyph;
        }

        public void Dispose()
        {
            _atlas.Dispose();
            _collection.Dispose();
        }
    }
}

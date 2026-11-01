using System.Text;
using TUnit.Assertions.Enums;

namespace Tralys.Ui.Fonts.Tests;

public sealed class GlyphAtlasTests
{
    [Test]
    public async Task APageStartingOver_LeavesTheTexelsAlreadyFlattenedListsRead()
    {
        using var fonts = new TestFonts();
        using var atlas = new GlyphAtlas(new MemoryTextureFactory(), pageSize: 64, maxPages: 1);
        var glyph = fonts.Inter.Face.GetGlyphIndex(new Rune('A'));
        atlas.TryGet(fonts.Inter, glyph, 20f, 0f, out var first);
        var read = Texels(first);

        foreach (var letter in "WMBCDEFGHK")
        {
            if (atlas.ResetCount > 0)
                break;
            atlas.BeginFrame();
            atlas.TryGet(
                fonts.Inter,
                fonts.Inter.Face.GetGlyphIndex(new Rune(letter)),
                20f,
                0f,
                out _
            );
        }

        await Assert.That(atlas.ResetCount).IsGreaterThan(0);
        await Assert.That(Texels(first)).IsEquivalentTo(read, CollectionOrdering.Matching);
    }

    [Test]
    public async Task GlyphsPackedSideBySide_StandInClearedTexelsApart_WhateverThePageHeldBefore()
    {
        using var fonts = new TestFonts();
        using var atlas = new GlyphAtlas(new SoiledTextureFactory(), pageSize: 128);
        atlas.TryGet(
            fonts.Inter,
            fonts.Inter.Face.GetGlyphIndex(new Rune('H')),
            20f,
            0f,
            out var first
        );
        atlas.TryGet(
            fonts.Inter,
            fonts.Inter.Face.GetGlyphIndex(new Rune('I')),
            20f,
            0f,
            out var second
        );

        await Assert.That(second.Source.Y).IsEqualTo(first.Source.Y);
        await Assert.That(second.Source.X - first.Source.Right).IsGreaterThanOrEqualTo(1f);
        await Assert.That(Around(first)).All().Satisfy(texel => texel.IsEqualTo((byte)0));
        await Assert.That(Around(second)).All().Satisfy(texel => texel.IsEqualTo((byte)0));
    }

    static byte[] Around(in AtlasGlyph glyph)
    {
        var page = (MemoryTexture)glyph.Page;
        var source = glyph.Source;
        var texels = new List<byte>();
        for (var y = (int)source.Y - 1; y <= (int)source.Bottom; y++)
        {
            for (var x = (int)source.X - 1; x <= (int)source.Right; x++)
            {
                var inside =
                    x >= source.X && x < source.Right && y >= source.Y && y < source.Bottom;
                if (!inside && x >= 0 && y >= 0 && x < page.Width && y < page.Height)
                    texels.Add(page.Texels[y * page.Width + x]);
            }
        }
        return [.. texels];
    }

    static byte[] Texels(in AtlasGlyph glyph)
    {
        var page = (MemoryTexture)glyph.Page;
        var source = glyph.Source;
        var texels = new byte[(int)source.Width * (int)source.Height];
        for (var row = 0; row < (int)source.Height; row++)
            page.Texels.AsSpan(
                    ((int)source.Y + row) * page.Width + (int)source.X,
                    (int)source.Width
                )
                .CopyTo(texels.AsSpan(row * (int)source.Width));
        return texels;
    }
}

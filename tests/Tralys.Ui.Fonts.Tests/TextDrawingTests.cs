using System.Numerics;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.Fonts.Tests;

public sealed class TextDrawingTests
{
    static readonly Color Red = Color.FromRgb(255, 0, 0);
    static readonly Color Blue = Color.FromRgb(0, 0, 255);
    static readonly Color Black = Color.FromRgb(0, 0, 0);

    [Test]
    public async Task AStroke_DrawsBeforeTheFill_AndCoversMore()
    {
        using var fonts = new TestFonts();
        using var atlas = new GlyphAtlas(new MemoryTextureFactory());
        var buffer = new TextBuffer(new TextStyle(fonts.Inter, 32f, Red, 4f, Black));
        buffer.SetText("A");

        var list = Flatten(buffer, atlas);

        await Assert.That(list.QuadCount).IsEqualTo(2);
        await Assert.That(list.Vertices[0].Fill).IsEqualTo(Black.ToPremultipliedRgba(1f));
        await Assert.That(list.Vertices[4].Fill).IsEqualTo(Red.ToPremultipliedRgba(1f));
        await Assert.That(Width(list, 0)).IsGreaterThan(Width(list, 1));
    }

    [Test]
    public async Task EachRun_DrawsInItsOwnFill()
    {
        using var fonts = new TestFonts();
        using var atlas = new GlyphAtlas(new MemoryTextureFactory());
        var buffer = new TextBuffer(new TextStyle(fonts.Inter, 16f, Red));
        buffer.Append("AB", new TextStyle(fonts.Inter, 16f, Red));
        buffer.Append("CD", new TextStyle(fonts.Inter, 16f, Blue));

        var list = Flatten(buffer, atlas);

        await Assert.That(list.QuadCount).IsEqualTo(4);
        await Assert.That(list.Vertices[4].Fill).IsEqualTo(Red.ToPremultipliedRgba(1f));
        await Assert.That(list.Vertices[8].Fill).IsEqualTo(Blue.ToPremultipliedRgba(1f));
    }

    [Test]
    public async Task ARunRecordedBeforeItsPageStartsOver_IsStaleAndDrawsNothing_UntilRecordedAgain()
    {
        using var fonts = new TestFonts();
        using var atlas = new GlyphAtlas(new MemoryTextureFactory(), pageSize: 64, maxPages: 1);
        var labelLayout = new TextLayout();
        TestFonts.SetText(fonts.Inter, "A", 16f, Red, labelLayout);
        var recorded = new RenderCommandBuffer();
        labelLayout.Draw(recorded.Open(), atlas);

        atlas.BeginFrame();
        var otherLayout = new TextLayout();
        TestFonts.SetText(fonts.Inter, "W", 40f, Blue, otherLayout);
        otherLayout.Draw(new RenderCommandBuffer().Open(), atlas);

        var stale = Flatten(recorded);
        var wasStale = recorded.IsStale;
        labelLayout.Draw(recorded.Open(), atlas);
        var fresh = Flatten(recorded);

        await Assert.That(atlas.ResetCount).IsGreaterThan(0);
        await Assert.That(wasStale).IsTrue();
        await Assert.That(stale.QuadCount).IsEqualTo(0);
        await Assert.That(recorded.IsStale).IsFalse();
        await Assert.That(fresh.QuadCount).IsEqualTo(1);
    }

    [Test]
    public async Task AGlyph_LandsWhereItsPenIs_ScaledFromItsField()
    {
        using var fonts = new TestFonts();
        using var atlas = new GlyphAtlas(new MemoryTextureFactory());
        var layout = new TextLayout();
        TestFonts.SetText(fonts.Inter, "A", 13.5f, Red, layout);
        var glyph = layout.Glyphs[0];
        var (left, top, width) = FieldOf(fonts.Inter, glyph.Glyph);
        var recorded = new RenderCommandBuffer();
        var origin = new Vector2(8.3f, 8.3f);
        layout.Draw(recorded.Open(), atlas, origin);

        var list = Flatten(recorded);
        var perTexel = 13.5f / GlyphAtlas.EmSize;
        var inset = GlyphAtlas.Inset;
        var corner = list.Vertices[0];

        await Assert
            .That(corner.Position.X)
            .IsEqualTo(origin.X + glyph.X + (left + inset) * perTexel)
            .Within(1e-4f);
        await Assert
            .That(corner.Position.Y)
            .IsEqualTo(origin.Y + glyph.Y - (top - inset) * perTexel)
            .Within(1e-4f);
        await Assert.That(Width(list, 0)).IsEqualTo((width - 2 * inset) * perTexel).Within(1e-4f);
        await Assert
            .That(corner.Shape)
            .IsEqualTo(new Vector4(atlas.PageSize, atlas.PageSize, 0f, 0f));
    }

    static (int Left, int Top, int Width) FieldOf(Font font, int glyph)
    {
        using var atlas = new GlyphAtlas(new MemoryTextureFactory());
        atlas.TryGet(font, glyph, GlyphAtlas.EmSize, 0f, out var field);
        return (
            field.Left + GlyphAtlas.Inset,
            field.Top - GlyphAtlas.Inset,
            (int)field.Source.Width + 2 * GlyphAtlas.Inset
        );
    }

    static DrawList Flatten(TextBuffer buffer, GlyphAtlas atlas)
    {
        var layout = new TextLayout();
        layout.Update(buffer);
        var recorded = new RenderCommandBuffer();
        layout.Draw(recorded.Open(), atlas, new Vector2(8f, 8f));
        return Flatten(recorded);
    }

    static DrawList Flatten(RenderCommandBuffer recorded)
    {
        var ramps = new GradientRampAtlas();
        var list = new DrawList(ramps);
        ramps.BeginFrame();
        list.Begin(256, 256);
        list.Draw(recorded);
        return list;
    }

    static float Width(DrawList list, int quad) =>
        list.Vertices[quad * 4 + 1].Position.X - list.Vertices[quad * 4].Position.X;
}

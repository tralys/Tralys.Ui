using System.Text;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Fonts.Tests;

public sealed class LineBreakingTests
{
    static readonly Color White = Color.FromRgb(255, 255, 255);

    [Test]
    public async Task AWrappedLine_BreaksAfterTheSpace_NotInsideTheWord()
    {
        using var fonts = new TestFonts();
        var layout = Layout(
            fonts.Inter,
            "hello world",
            maxWidth: Width(fonts.Inter, "hello world") - 1f
        );

        await Assert.That(layout.Lines.Length).IsEqualTo(2);
        await Assert.That(layout.Lines[1].Start).IsEqualTo(6);
    }

    [Test]
    public async Task Ideographs_BreakBetweenEachOther()
    {
        using var fonts = new TestFonts();
        var layout = Layout(
            fonts.Inter,
            "Hi 你好世界",
            maxWidth: Width(fonts.Inter, "Hi 你好") + 1f
        );

        await Assert.That(layout.Lines.Length).IsEqualTo(2);
        await Assert.That(layout.Lines[1].Start).IsEqualTo(5);
    }

    [Test]
    public async Task AWordWiderThanTheLine_TakesItsLineAloneWithNoBlankLineAfter()
    {
        using var fonts = new TestFonts();
        var layout = Layout(fonts.Inter, "A B", maxWidth: 0f);

        await Assert.That(layout.Lines.Length).IsEqualTo(2);
    }

    [Test]
    public async Task AnEmojiNewerThanTheData_BreaksBeforeTheWordAfterIt()
    {
        using var fonts = new TestFonts();
        var layout = Layout(
            fonts.Inter,
            "\U0001FAE9 next",
            maxWidth: Width(fonts.Inter, "\U0001FAE9 next") - 1f
        );

        await Assert.That(layout.Lines.Length).IsEqualTo(2);
        await Assert.That(layout.Lines[1].Start).IsEqualTo(3);
    }

    [Test]
    public async Task TextTooSmallToSet_TakesNoSpaceRatherThanThrowing()
    {
        using var driver = UiDriver.Render(static ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (ui.Text(fontSize: 0.005f, id: "block"))
                ui.Span("x", underline: true);
        });

        await Assert.That(driver.Get("block").Bounds.Height).IsEqualTo(0f);
    }

    [Test]
    public async Task ACharacterThePrimaryFaceLacks_DrawsFromTheFallback()
    {
        using var fonts = new TestFonts();
        var layout = Layout(fonts.Inter, "A你", maxWidth: float.PositiveInfinity);

        await Assert.That(layout.Glyphs[0].Font).IsSameReferenceAs(fonts.Inter);
        await Assert.That(layout.Glyphs[1].Font).IsSameReferenceAs(fonts.Cjk);
        await Assert.That(layout.Glyphs[1].Glyph).IsNotEqualTo(0);
    }

    [Test]
    public async Task ASpaceThePrimaryFaceLacks_TakesThatFacesOwnSpace()
    {
        using var fonts = new TestFonts();
        var layout = Layout(fonts.Inter, "A\u3000B", maxWidth: float.PositiveInfinity);

        await Assert.That(layout.Glyphs[1].Font).IsSameReferenceAs(fonts.Inter);
        await Assert.That(layout.Glyphs[1].Glyph).IsNotEqualTo(0);
    }

    [Test]
    public async Task AnOverflowingLine_EndsInAnEllipsisWithinTheWidth()
    {
        using var fonts = new TestFonts();
        var layout = new TextLayout { MaxWidth = 60f, Trimming = TextTrimming.CharacterEllipsis };

        TestFonts.SetText(fonts.Inter, "The quick brown fox", 16f, White, layout);

        await Assert.That(layout.Lines.Length).IsEqualTo(1);
        await Assert
            .That(layout.Glyphs[^1].Glyph)
            .IsEqualTo(fonts.Inter.Face.GetGlyphIndex(new Rune('\u2026')));
        await Assert.That(layout.Size.Width).IsLessThanOrEqualTo(60f);
    }

    [Test]
    public async Task ABoxOverATrimmedLinesCutText_StopsAtTheEllipsis()
    {
        using var fonts = new TestFonts();
        var layout = new TextLayout { MaxWidth = 60f, Trimming = TextTrimming.CharacterEllipsis };
        const string text = "The quick brown fox";

        TestFonts.SetText(fonts.Inter, text, 16f, White, layout);
        var boxes = new Rect[1];
        var hidden = layout.GetSelectionRects(text.Length - 3, text.Length, boxes);
        var shown = layout.GetSelectionRects(0, text.Length, boxes);

        await Assert.That(hidden).IsEqualTo(0);
        await Assert.That(shown).IsEqualTo(1);
        await Assert.That(boxes[0].Right).IsLessThanOrEqualTo(layout.Glyphs[^1].X);
    }

    static TextLayout Layout(Font font, string text, float maxWidth)
    {
        var layout = new TextLayout { MaxWidth = maxWidth, Wrapping = TextWrapping.Wrap };
        TestFonts.SetText(font, text, 16f, White, layout);
        return layout;
    }

    static float Width(Font font, string text)
    {
        var layout = new TextLayout();
        TestFonts.SetText(font, text, 16f, White, layout);
        return layout.Size.Width;
    }
}

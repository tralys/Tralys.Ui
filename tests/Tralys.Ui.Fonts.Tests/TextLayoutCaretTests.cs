namespace Tralys.Ui.Fonts.Tests;

public sealed class TextLayoutCaretTests
{
    [Test]
    public async Task ACaretAtALineEnd_StaysOnALineThatWrapsThereButNotBeforeALineBreak()
    {
        using var fonts = new TestFonts();
        var buffer = new TextBuffer(new TextStyle(fonts.Inter, 16f, default));
        var layout = new TextLayout();
        buffer.SetText("hello world");
        layout.Update(buffer);
        layout.MaxWidth = layout.Size.Width - 1f;
        layout.Wrapping = TextWrapping.Wrap;
        layout.Update(buffer);
        var wrapped = layout.GetLineAt(6, atLineEnd: true);

        buffer.SetText("hello\nworld");
        layout.Update(buffer);
        var broken = layout.GetLineAt(6, atLineEnd: true);

        await Assert.That(wrapped).IsEqualTo(0);
        await Assert.That(broken).IsEqualTo(1);
    }

    [Test]
    public async Task CharacterSpacing_MovesCaretsAndGlyphsPerCluster_LeavingAMarkOnItsBase()
    {
        const string Text = "éx";
        using var fonts = new TestFonts();
        var plain = new TextLayout();
        var plainBuffer = new TextBuffer(new TextStyle(fonts.Inter, 16f, default));
        plainBuffer.SetText(Text);
        plain.Update(plainBuffer);
        var spaced = new TextLayout();
        var spacedBuffer = new TextBuffer(
            new TextStyle(fonts.Inter, 16f, default, characterSpacing: 100)
        );
        spacedBuffer.SetText(Text);
        spaced.Update(spacedBuffer);

        await Assert.That(spaced.Glyphs[1].X).IsEqualTo(plain.Glyphs[1].X);
        await Assert.That(spaced.Glyphs[2].X - plain.Glyphs[2].X).IsEqualTo(1.6f).Within(0.001f);
        await Assert
            .That(spaced.GetCaretRect(2).X - plain.GetCaretRect(2).X)
            .IsEqualTo(1.6f)
            .Within(0.001f);
        await Assert.That(spaced.Size.Width - plain.Size.Width).IsEqualTo(3.2f).Within(0.001f);
    }
}

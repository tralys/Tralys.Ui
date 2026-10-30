namespace Tralys.Ui.Engine.Tests;

public sealed class ColorTests
{
    [Test]
    public async Task Parse_ReadsHexAsMarkupWritesIt_AlphaFirst_OpaqueWhenLeftOut()
    {
        await Assert.That(Color.Parse("#80FF0040")).IsEqualTo(new Color(0x80, 0xFF, 0x00, 0x40));
        await Assert.That(Color.Parse("#FF0040")).IsEqualTo(new Color(0xFF, 0xFF, 0x00, 0x40));
        await Assert.That(Color.Parse("#8F04")).IsEqualTo(new Color(0x88, 0xFF, 0x00, 0x44));
    }

    [Test]
    public async Task TryParse_RejectsWhatIsNoHexColour()
    {
        string[] malformed = ["#12345", "#FF0000 ", "##FF0000", "80FF0040"];

        var accepted = malformed.Where(static hex => Color.TryParse(hex, out _)).ToList();

        await Assert.That(accepted).IsEmpty();
    }
}

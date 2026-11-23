namespace Tralys.Ui.Fonts.Tests;

public sealed class FaceMetricsTests
{
    [Test]
    public async Task AFaceWithALineGap_RoundsItsLineUpToAWholePixel()
    {
        using var fonts = new FontCollection();
        var lucide = fonts.Add(
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "Lucide.ttf"))
        );

        var metrics = lucide.GetMetrics(14f);

        await Assert.That(metrics.Ascent + metrics.Descent + metrics.LineGap).IsEqualTo(16f);
    }

    [Test]
    public async Task AFace_SetsHalfItsOwnLineGapAboveTheBaseline()
    {
        using var fonts = new FontCollection();
        var lucide = fonts.Add(
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "Lucide.ttf"))
        );

        var metrics = lucide.GetMetrics(16f);

        await Assert.That(metrics.Ascent).IsEqualTo(16.72f).Within(1e-3f);
        await Assert
            .That(metrics.Ascent + metrics.Descent + metrics.LineGap)
            .IsEqualTo(18f)
            .Within(1e-4f);
    }
}

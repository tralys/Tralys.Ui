namespace Tralys.Ui.Docking.Tests;

public sealed class DockSplitSizingTests
{
    [Test]
    public async Task NormalizeProportions_PositiveValues_NormalizesToOne()
    {
        var (first, second) = DockSplitSizing.NormalizeProportions(2, 6);

        await Assert.That(first).IsEqualTo(0.25f);
        await Assert.That(second).IsEqualTo(0.75f);
    }

    [Test]
    public async Task NormalizeProportions_InvalidValues_FallsBackToEqualSplit()
    {
        var (first, second) = DockSplitSizing.NormalizeProportions(0, float.NaN);

        await Assert.That(first).IsEqualTo(0.5f);
        await Assert.That(second).IsEqualTo(0.5f);
    }
}

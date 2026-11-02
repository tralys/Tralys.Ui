namespace Tralys.Ui.Engine.Tests;

public sealed class AnimationTests
{
    [Test]
    public async Task AKeySpline_MapsProgressAlongItsCurve()
    {
        var ease = new KeySpline(0.25f, 0.1f, 0.25f, 1f);

        await Assert.That(ease.Ease(0.5)).IsEqualTo(0.8024).Within(0.001);
    }
}

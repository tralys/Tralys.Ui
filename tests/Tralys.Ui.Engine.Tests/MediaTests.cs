using System.Numerics;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.Engine.Tests;

public sealed class MediaTests
{
    [Test]
    public async Task APathsBounds_HugItsCurves_NotTheirControlPoints()
    {
        var arch = new PathBuilder();
        arch.MoveTo(Vector2.Zero);
        arch.QuadraticTo(new Vector2(10f, -20f), new Vector2(20f, 0f));
        arch.Close();

        await Assert.That(PathMath.Bounds(arch)).IsEqualTo(new Rect(0f, -10f, 20f, 10f));
    }

    [Test]
    public async Task ARoundedRectangle_LimitsAnOverLargeRadiusToHalfItself()
    {
        var pill = new PathBuilder();
        pill.AddRoundedRectangle(new Rect(0f, 0f, 100f, 20f), new Vector2(50f, 50f));

        await Assert.That(PathMath.Bounds(pill)).IsEqualTo(new Rect(0f, 0f, 100f, 20f));
    }
}

using System.Numerics;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.Engine.Tests;

public sealed class PathCombinerTests
{
    [Test]
    public async Task AUnionOfBoxesSharingAnEdge_KeepsTheEdgeWhereOneBoxsSideMeetsTheOthersCorner()
    {
        var first = new PathBuilder();
        first.AddRectangle(new Rect(0f, 0f, 10f, 10f));
        var second = new PathBuilder();
        second.AddRectangle(new Rect(5f, 0f, 10f, 10f));

        first.Combine(second, GeometryCombineMode.Union);

        await Assert.That(PathMath.FillContains(first, new Vector2(7.5f, 0.5f))).IsTrue();
        await Assert.That(PathMath.FillContains(first, new Vector2(7.5f, 9.5f))).IsTrue();
        await Assert.That(PathMath.FillContains(first, new Vector2(7.5f, 10.5f))).IsFalse();
    }
}

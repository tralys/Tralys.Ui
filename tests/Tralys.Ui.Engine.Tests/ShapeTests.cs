using Tralys.Ui.Rendering;

namespace Tralys.Ui.Engine.Tests;

public sealed class ShapeTests
{
    static readonly Color White = Color.FromRgb(255, 255, 255);

    [Test]
    public async Task ARectangleWithCircularCornersAndAnEllipse_EachDrawAsOneQuadInsteadOfAMesh()
    {
        var box = new Rect(1f, 1f, 398f, 398f);
        var rectangle = Draw(dc =>
            dc.DrawBox(box, new CornerRadius(7f), new Thickness(2f), White, White)
        );
        var ellipse = Draw(dc => dc.DrawEllipse(box, White, 2f, White));

        await Assert.That(rectangle.Vertices.Length).IsEqualTo(4);
        await Assert.That(rectangle.QuadCount).IsEqualTo(1);
        await Assert.That(ellipse.Vertices.Length).IsEqualTo(4);
        await Assert.That(ellipse.QuadCount).IsEqualTo(1);
    }

    static DrawList Draw(Action<DrawingContext> draw)
    {
        var ramps = new GradientRampAtlas();
        var list = new DrawList(ramps);
        var buffer = new RenderCommandBuffer();
        draw(buffer.Open());
        ramps.BeginFrame();
        list.Begin(400, 400);
        list.Draw(buffer);
        return list;
    }
}

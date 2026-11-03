using System.Numerics;
using Tralys.Ui.Rendering;
using static Tralys.Ui.Bgfx.Tests.TestColors;

namespace Tralys.Ui.Bgfx.Tests;

[NotInParallel(GpuDevice.Key)]
public sealed class ShapeGpuTests
{
    static readonly Color Green = Color.FromRgb(0, 200, 0);

    // A path's curves flatten to chords up to a quarter pixel inside the primitive's exact edge.
    const int Flattening = 64;

    [Test]
    public async Task ARoundedRectangle_DrawsThePixelsOfItsOutlineFilledAndStroked()
    {
        using var gpu = new GpuDevice();
        var outline = new PathBuilder();
        outline.AddRoundedRectangle(new Rect(10, 10, 44, 28), new Vector2(8));

        var drawn = gpu.Render(
            64,
            48,
            dc =>
            {
                dc.DrawRoundedRectangle(Green, new Rect(10, 10, 44, 28), new CornerRadius(8));
                dc.DrawBox(
                    new Rect(8, 8, 48, 32),
                    new CornerRadius(10),
                    new Thickness(4),
                    default,
                    HalfWhite
                );
            }
        );
        var traced = gpu.Render(64, 48, dc => dc.DrawPath(outline, Green, new Pen(HalfWhite, 4)));

        await Assert.That(drawn.DistanceTo(traced)).IsLessThanOrEqualTo(Flattening);
    }

    [Test]
    public async Task AnEllipse_DrawsThePixelsOfItsOutlineFilledAndStroked()
    {
        using var gpu = new GpuDevice();
        var outline = new PathBuilder();
        outline.AddEllipse(new Rect(9.5f, 9.5f, 45, 29));

        var drawn = gpu.Render(64, 48, dc => dc.DrawEllipse(new Rect(8, 8, 48, 32), Green, 3, Red));
        var traced = gpu.Render(64, 48, dc => dc.DrawPath(outline, Green, new Pen(Red, 3)));

        await Assert.That(drawn.DistanceTo(traced)).IsLessThanOrEqualTo(Flattening);
    }
}

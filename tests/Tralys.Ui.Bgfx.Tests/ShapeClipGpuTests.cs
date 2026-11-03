using System.Numerics;
using Tralys.Ui.Rendering;
using static Tralys.Ui.Bgfx.Tests.TestColors;

namespace Tralys.Ui.Bgfx.Tests;

[NotInParallel(GpuDevice.Key)]
public sealed class ShapeClipGpuTests
{
    [Test]
    public async Task ARoundedClip_HidesWhatFallsOutsideItsCorners()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(
            64,
            64,
            dc =>
            {
                dc.PushClip(new Rect(0, 0, 64, 64), new CornerRadius(24));
                dc.DrawRectangle(White, new Rect(0, 0, 64, 64));
            }
        );

        await Assert.That(frame[2, 2]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[61, 61]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[32, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[32, 1]).IsEqualTo(Pixel.Opaque(White));
    }

    [Test]
    public async Task ARotatedClip_ClipsToItsTurnedRectangle_NotItsBoundingBox()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(
            64,
            64,
            dc =>
            {
                dc.PushTransform(Matrix3x2.CreateRotation(MathF.PI / 4, new Vector2(32, 32)));
                dc.PushClip(new Rect(16, 16, 32, 32));
                dc.DrawRectangle(White, new Rect(-64, -64, 192, 192));
            }
        );

        await Assert.That(frame[32, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[12, 12]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[32, 12]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame.SoftPixels()).IsGreaterThan(0);
    }

    [Test]
    public async Task ARoundedClip_SoftensItsCurve()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(
            64,
            64,
            dc =>
            {
                dc.PushClip(new Rect(0, 0, 64, 64), new CornerRadius(24));
                dc.DrawRectangle(White, new Rect(0, 0, 64, 64));
            }
        );

        await Assert.That(frame.SoftPixels()).IsGreaterThan(0);
    }

    [Test]
    public async Task APathClip_SoftensItsEdge()
    {
        using var gpu = new GpuDevice();
        var circle = new PathBuilder();
        circle.AddEllipse(new Rect(16, 16, 32, 32));

        var frame = gpu.Render(
            64,
            64,
            dc =>
            {
                dc.PushClip(circle);
                dc.DrawRectangle(White, new Rect(0, 0, 64, 64));
            }
        );

        await Assert.That(frame[32, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[18, 18]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame.SoftPixels()).IsGreaterThan(0);
    }

    [Test]
    public async Task PoppingAClip_LetsItsParentClipDrawWhereTheChildCut()
    {
        using var gpu = new GpuDevice();
        var circle = new PathBuilder();
        circle.AddEllipse(new Rect(16, 16, 32, 32));

        var frame = gpu.Render(
            64,
            64,
            dc =>
            {
                dc.PushClip(new Rect(8, 8, 48, 48), new CornerRadius(4));
                dc.PushClip(circle);
                dc.DrawRectangle(White, new Rect(0, 0, 64, 64));
                dc.Pop();
                dc.DrawRectangle(Red, new Rect(0, 0, 32, 64));
            }
        );

        await Assert.That(frame[24, 32]).IsEqualTo(Pixel.Opaque(Red));
        await Assert.That(frame[12, 32]).IsEqualTo(Pixel.Opaque(Red));
        await Assert.That(frame[40, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[52, 12]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[4, 32]).IsEqualTo(Pixel.Clear);
    }
}

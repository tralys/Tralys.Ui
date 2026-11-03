using System.Numerics;
using Bgfx.Net;
using Tralys.Ui.Rendering;
using static Tralys.Ui.Bgfx.Tests.TestColors;

namespace Tralys.Ui.Bgfx.Tests;

[NotInParallel(GpuDevice.Key)]
public sealed class LayerGpuTests
{
    [Test]
    public async Task ADropShadowWithoutAViewForItsBlur_DrawsItsContentAlone()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.RenderList(
            64,
            64,
            list =>
            {
                list.PushDropShadow(
                    new DropShadow(White, Direction: 270, ShadowDepth: 8, BlurRadius: 6)
                );
                list.Draw(Box(Red, new Rect(16, 16, 32, 24)));
                list.Pop();
            },
            layerViews: 1
        );

        await Assert.That(frame[32, 30]).IsEqualTo(Pixel.Opaque(Red));
        await Assert.That(frame[14, 28]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[32, 44]).IsEqualTo(Pixel.Clear);
        await Assert.That(gpu.DroppedLayers).IsEqualTo(1);
    }

    [Test]
    public async Task LayersOfOnePass_ShareATarget_AndABlurNeverReadsTheNeighbourPackedBesideIt()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.RenderList(
            64,
            64,
            list =>
            {
                list.PushDropShadow(new DropShadow(White, ShadowDepth: 0, BlurRadius: 9));
                list.Draw(Box(Red, new Rect(8, 8, 8, 8)));
                list.Pop();
                list.PushLayer(0.5f);
                list.Draw(Box(White, new Rect(40, 40, 24, 24)));
                list.Draw(Box(White, new Rect(44, 44, 16, 16)));
                list.Pop();
                list.PushDropShadow(new DropShadow(White, ShadowDepth: 0, BlurRadius: 9));
                list.Draw(Box(Red, new Rect(40, 8, 8, 8)));
                list.Pop();
            },
            layerViews: 2
        );

        await Assert.That(gpu.DroppedLayers).IsEqualTo(0);
        await Assert.That(frame[17, 12].G).IsGreaterThan((byte)50);
        await Assert.That(frame[49, 12].G).IsGreaterThan((byte)50);
        await Assert
            .That(frame[25, 12].DistanceTo(Pixel.Clear))
            .IsLessThanOrEqualTo(8)
            .Because("the layer packed to the right of the first shadow's is bright to its edge");
    }

    [Test]
    public async Task AClipWithoutAView_DrawsIntoItsParentInsideItsBoundingBox()
    {
        using var gpu = new GpuDevice();
        var circle = new PathBuilder();
        circle.AddEllipse(new Rect(16, 16, 32, 32));

        var frame = gpu.RenderList(
            64,
            64,
            list =>
            {
                list.PushClip(new Rect(8, 8, 48, 48), new CornerRadius(24));
                list.Draw(Box(Red, new Rect(0, 0, 64, 64)));
                list.PushClip(circle);
                list.Draw(Box(White, new Rect(0, 0, 64, 64)));
                list.Pop();
                list.Pop();
            },
            layerViews: 0
        );

        await Assert.That(gpu.DroppedLayers).IsEqualTo(2);
        await Assert.That(frame[4, 4]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[9, 9]).IsEqualTo(Pixel.Opaque(Red));
        await Assert.That(frame[17, 17]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[12, 32]).IsEqualTo(Pixel.Opaque(Red));
    }

    [Test]
    public async Task ABatchLeftOutOfALayer_LeavesItsPlaceInTheLayerOffTheBatchAfterIt()
    {
        using var gpu = new GpuDevice();
        using var loaded = BgfxTexture2D.Create(1, 1, TextureFormat.RGBA8, [255, 255, 255, 255]);
        using var evicted = BgfxTexture2D.Create(1, 1, TextureFormat.RGBA8, [255, 255, 255, 255]);
        evicted.Dispose();

        var frame = gpu.RenderList(
            64,
            64,
            list =>
            {
                list.PushLayer(0.5f);
                list.Draw(GpuDevice.Record(dc => dc.DrawImage(loaded, new Rect(32, 32, 16, 16))));
                list.Draw(GpuDevice.Record(dc => dc.DrawImage(evicted, new Rect(32, 32, 16, 16))));
                list.Pop();
            }
        );

        await Assert.That(frame[40, 40].DistanceTo(Pixel.Half)).IsLessThanOrEqualTo(2);
        await Assert.That(frame[8, 8]).IsEqualTo(Pixel.Clear);
    }

    static RenderCommandBuffer Box(Color color, Rect rect)
    {
        var buffer = new RenderCommandBuffer();
        buffer.Open().DrawRectangle(color, rect);
        return buffer;
    }

    [Test]
    public async Task ALayersOpacity_FadesOverlappingPrimitivesAsOne()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.RenderList(
            64,
            64,
            list =>
            {
                list.PushLayer(0.5f);
                list.Draw(Box(White, new Rect(0, 0, 40, 64)));
                list.Draw(Box(White, new Rect(24, 0, 40, 64)));
                list.Pop();
            }
        );

        await Assert.That(frame[8, 32].DistanceTo(Pixel.Half)).IsLessThanOrEqualTo(2);
        await Assert.That(frame[32, 32].DistanceTo(Pixel.Half)).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task AnOpacityMask_TakesItsAlphaFromAPaintOrAnImage()
    {
        using var gpu = new GpuDevice();
        var fade = new GradientStops([
            new(0f, Color.FromArgb(255, 255, 255, 255)),
            new(1f, Color.FromArgb(0, 255, 255, 255)),
        ]);
        using var stencil = BgfxTexture2D.Create(
            2,
            1,
            TextureFormat.RGBA8,
            [255, 255, 255, 255, 255, 255, 255, 0],
            flags: (ulong)(SamplerFlags.Point | SamplerFlags.UClamp | SamplerFlags.VClamp)
        );

        var frame = gpu.RenderList(
            64,
            64,
            list =>
            {
                list.PushOpacityMask(
                    Paint.Linear(fade, new Vector2(0, 0), new Vector2(1, 0)),
                    new Rect(0, 0, 32, 64)
                );
                list.Draw(Box(White, new Rect(0, 0, 32, 64)));
                list.Pop();
                list.PushOpacityMask(
                    Paint.FromImage(new PaintImage(stencil)),
                    new Rect(32, 0, 32, 64)
                );
                list.Draw(Box(White, new Rect(32, 0, 32, 64)));
                list.Pop();
            }
        );

        await Assert.That(frame[1, 32].R).IsGreaterThan((byte)230);
        await Assert.That(frame[30, 32].R).IsLessThan((byte)30);
        await Assert.That(frame[40, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[56, 32]).IsEqualTo(Pixel.Clear);
    }

    [Test]
    [Arguments(12f, 2)]
    [Arguments(24f, 4)]
    public async Task ADropShadowBlurredOnACoarserTarget_LandsWithinAFewLevelsOfTheFullResolutionBlur(
        float blurRadius,
        int downsample
    )
    {
        using var gpu = new GpuDevice();
        var card = new PathBuilder();
        card.AddEllipse(new Rect(92, 30, 36, 36));
        void Draw(DrawList list)
        {
            list.PushDropShadow(
                new DropShadow(White, Direction: 300, ShadowDepth: 5, BlurRadius: blurRadius)
            );
            list.Draw(
                GpuDevice.Record(dc =>
                {
                    dc.DrawRectangle(Red, new Rect(23, 21, 41, 37));
                    dc.DrawRectangle(Red, new Rect(70, 17, 1, 60));
                    dc.DrawRectangle(Red, new Rect(84, 75, 50, 1));
                    dc.DrawPath(card, Red, default);
                })
            );
            list.Pop();
        }

        var full = gpu.RenderList(160, 96, Draw, shadowDownsample: 1);
        var coarse = gpu.RenderList(160, 96, Draw, shadowDownsample: downsample);

        await Assert.That(coarse.DistanceTo(full)).IsLessThanOrEqualTo(6);
    }

    [Test]
    public async Task AnElementsShadow_FallsOutsideItsOwnClip_AndItsMask_FadesAcrossItsRenderSize()
    {
        using var gpu = new GpuDevice();
        var fade = new GradientStops([
            new(0f, Color.FromArgb(255, 0, 0, 0)),
            new(1f, Color.FromArgb(0, 0, 0, 0)),
        ]);
        var bounds = new Rect(0, 0, 32, 24);

        var frame = gpu.RenderList(
            64,
            64,
            list =>
            {
                list.PushTransform(Matrix3x2.CreateTranslation(16, 16));
                list.PushDropShadow(
                    new DropShadow(White, Direction: 270, ShadowDepth: 8, BlurRadius: 2)
                );
                list.PushClip(bounds);
                list.PushOpacityMask(
                    Paint.Linear(fade, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f)),
                    bounds
                );
                list.Draw(GpuDevice.Record(dc => dc.DrawRectangle(Red, bounds)));
                list.Pop();
                list.Pop();
                list.Pop();
                list.Pop();
            }
        );

        await Assert.That(frame[18, 28].R).IsGreaterThan((byte)220);
        await Assert.That(frame[47, 28].R).IsLessThan((byte)40);
        await Assert
            .That(frame[20, 44].G)
            .IsGreaterThan((byte)150)
            .Because("270° points down, past the clip");
    }

    [Test]
    public async Task ATurnedElementsShadow_TurnsWithIt_AndFallsPastTheSlotItsContentIsClippedTo()
    {
        using var gpu = new GpuDevice();

        var turned = Matrix3x2.CreateRotation(MathF.PI) * Matrix3x2.CreateTranslation(48, 16);
        Matrix3x2.Invert(turned, out var toSlot);

        var frame = gpu.RenderList(
            64,
            64,
            list =>
            {
                list.PushTransform(Matrix3x2.CreateTranslation(0, 24));
                list.PushTransform(turned);
                list.PushDropShadow(
                    new DropShadow(White, Direction: 180, ShadowDepth: 8, BlurRadius: 2)
                );
                list.PushTransform(toSlot);
                list.PushClip(new Rect(0, 0, 32, 16));
                list.PushTransform(turned);
                list.Draw(GpuDevice.Record(dc => dc.DrawRectangle(Red, new Rect(0, 0, 48, 16))));
                for (var i = 0; i < 6; i++)
                    list.Pop();
            }
        );

        await Assert.That(frame[4, 32]).IsEqualTo(Pixel.Opaque(Red));
        await Assert
            .That(frame[36, 32].G)
            .IsGreaterThan((byte)200)
            .Because("180° turned by 180° points right");
        await Assert.That(frame[46, 32]).IsEqualTo(Pixel.Clear);
    }

    [Test]
    public async Task ADropShadow_FallsInItsDirection_BlurredBeneathTheContent_EvenUnderAShapeClip()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.RenderList(
            64,
            64,
            list =>
            {
                list.PushClip(new Rect(0, 0, 64, 64), new CornerRadius(8));
                list.PushDropShadow(
                    new DropShadow(White, Direction: 270, ShadowDepth: 8, BlurRadius: 6)
                );
                list.Draw(Box(Red, new Rect(16, 16, 32, 24)));
                list.Pop();
                list.Pop();
            }
        );

        await Assert.That(frame[32, 30]).IsEqualTo(Pixel.Opaque(Red));
        await Assert.That(frame[32, 44].G).IsGreaterThan((byte)200);
        await Assert.That(frame[32, 47].G).IsBetween((byte)60, (byte)200);
        await Assert.That(frame[32, 12]).IsEqualTo(Pixel.Clear).Because("270° points down");
    }
}

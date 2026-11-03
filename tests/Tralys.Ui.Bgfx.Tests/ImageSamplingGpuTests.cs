using System.Numerics;
using Tralys.Ui.Rendering;
using static Tralys.Ui.Bgfx.Tests.TestColors;

namespace Tralys.Ui.Bgfx.Tests;

[NotInParallel(GpuDevice.Key)]
public sealed class ImageSamplingGpuTests
{
    const int StripeCount = 9;
    static readonly Matrix3x2 Turned = Matrix3x2.CreateRotation(MathF.PI / 6, new Vector2(32, 32));

    [Test]
    public async Task AShrunkenImage_AveragesWhatEachPixelCovers_InEveryWayItDraws()
    {
        using var gpu = new GpuDevice();
        var stripes = new byte[StripeCount * StripeCount * 4];
        for (var i = 0; i < stripes.Length; i += 4)
        {
            var shade = (byte)(i / 4 % StripeCount % 2 == 0 ? 255 : 0);
            stripes[i] = shade;
            stripes[i + 1] = shade;
            stripes[i + 2] = shade;
            stripes[i + 3] = 255;
        }
        using var plain = BgfxTexture2D.CreateMipmapped(StripeCount, StripeCount, stripes);
        using var array = new BgfxTextureArray(StripeCount, StripeCount, 2);
        array.Write(1, stripes, StripeCount * 4);
        var layer = new RenderTextureLayer(array, 1);

        var frame = gpu.Render(
            12,
            3,
            dc =>
            {
                dc.DrawImage(plain, new Rect(0, 0, 3, 3));
                dc.DrawImage(layer, new Rect(3, 0, 3, 3));
                dc.DrawRectangle(Paint.FromImage(new PaintImage(plain)), new Rect(6, 0, 3, 3));
                dc.DrawRectangle(Paint.FromImage(new PaintImage(layer)), new Rect(9, 0, 3, 3));
            }
        );

        for (var x = 0; x < 12; x++)
            await Assert.That(frame[x, 1].G).IsBetween((byte)80, (byte)175);
    }

    [Test]
    public async Task AFrameOfASheet_KeepsOutWhatLiesBesideIt_ShrunkenOrEnlarged()
    {
        using var gpu = new GpuDevice();
        var texels = new byte[8 * 8 * 4];
        for (var i = 0; i < texels.Length; i += 4)
        {
            var isFrame = i / 4 % 8 < 3;
            texels[i] = (byte)(isFrame ? 255 : 0);
            texels[i + 1] = (byte)(isFrame ? 0 : 255);
            texels[i + 3] = 255;
        }
        using var sheet = BgfxTexture2D.CreateMipmapped(8, 8, texels);
        var frameTexels = new Rect(0, 0, 3, 3);

        var frame = gpu.Render(
            64,
            32,
            dc =>
            {
                dc.DrawImage(sheet, new Rect(0, 0, 1, 1), frameTexels);
                dc.DrawImage(sheet, new Rect(8, 0, 30, 30), frameTexels);
            }
        );

        await Assert.That(frame[0, 0]).IsEqualTo(Pixel.Opaque(Red));
        await Assert.That(frame[37, 15]).IsEqualTo(Pixel.Opaque(Red));
    }

    [Test]
    public async Task AMipLevel_AveragesColourByHowMuchOfItShows()
    {
        using var gpu = new GpuDevice();
        using var image = BgfxTexture2D.CreateMipmapped(2, 1, [255, 0, 0, 255, 0, 0, 0, 0]);

        var frame = gpu.Render(4, 4, dc => dc.DrawImage(image, new Rect(0, 0, 1, 1)));

        await Assert.That(frame[0, 0].R).IsBetween((byte)124, (byte)132);
    }

    [Test]
    public async Task AMipLevelOfAnOddWidth_WeighsEveryTexel()
    {
        using var gpu = new GpuDevice();
        using var image = BgfxTexture2D.CreateMipmapped(
            3,
            1,
            [255, 255, 255, 255, 255, 255, 255, 255, 0, 0, 0, 255]
        );

        var frame = gpu.Render(4, 4, dc => dc.DrawImage(image, new Rect(0, 0, 1, 1)));

        await Assert.That(frame[0, 0].G).IsBetween((byte)166, (byte)174);
    }

    [Test]
    public async Task ATurnedImage_SoftensItsEdges()
    {
        using var gpu = new GpuDevice();
        using var image = BgfxTexture2D.CreateMipmapped(1, 1, [255, 255, 255, 255]);

        var frame = gpu.RenderList(
            64,
            64,
            list =>
            {
                list.PushTransform(Turned);
                list.Draw(GpuDevice.Record(dc => dc.DrawImage(image, new Rect(16, 16, 32, 32))));
                list.Pop();
            }
        );

        await Assert.That(frame[32, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame.SoftPixels()).IsGreaterThan(0);
    }

    [Test]
    public async Task ANineSliceOnFractionalPixels_SoftensOnlyItsOuterEdges()
    {
        using var gpu = new GpuDevice();
        using var image = BgfxTexture2D.CreateMipmapped(8, 8, WhiteTexels(8));

        var frame = gpu.Render(
            64,
            64,
            dc =>
                dc.DrawImage(
                    image,
                    new Rect(10.5f, 10.5f, 30, 30),
                    new Rect(0, 0, 8, 8),
                    new NineSlice(new Thickness(2))
                )
        );

        await Assert.That(frame[10, 25].G).IsBetween((byte)100, (byte)160);
        await Assert.That(frame[12, 25]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[38, 25]).IsEqualTo(Pixel.Opaque(White));
    }

    [Test]
    public async Task ATurnedImageBrush_SoftensWhereItStopsInsideItsShape()
    {
        using var gpu = new GpuDevice();
        using var image = BgfxTexture2D.CreateMipmapped(11, 1, WhiteTexels(11, 1));
        var band = Paint.FromImage(new PaintImage(image) { Stretch = Stretch.Uniform });

        var frame = gpu.RenderList(
            64,
            64,
            list =>
            {
                list.PushTransform(Turned);
                list.Draw(
                    GpuDevice.Record(dc => dc.DrawRectangle(band, new Rect(-100, -100, 264, 264)))
                );
                list.Pop();
            }
        );

        await Assert.That(frame[32, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame.SoftPixels()).IsGreaterThan(0);
    }

    [Test]
    public async Task AnImageBrushFlushWithItsShape_SoftensAsASolidFillDoes()
    {
        using var gpu = new GpuDevice();
        using var image = BgfxTexture2D.CreateMipmapped(1, 1, [255, 255, 255, 255]);

        Frame Turning(Paint fill) =>
            gpu.RenderList(
                64,
                64,
                list =>
                {
                    list.PushTransform(Turned);
                    list.Draw(
                        GpuDevice.Record(dc => dc.DrawRectangle(fill, new Rect(16, 16, 32, 32)))
                    );
                    list.Pop();
                }
            );

        var brushed = Turning(Paint.FromImage(new PaintImage(image)));
        var solid = Turning(White);

        var worst = 0;
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
                worst = Math.Max(worst, brushed[x, y].DistanceTo(solid[x, y]));
        }
        await Assert.That(worst).IsLessThanOrEqualTo(6);
    }

    static byte[] WhiteTexels(int width, int height = 0)
    {
        var texels = new byte[width * (height > 0 ? height : width) * 4];
        Array.Fill(texels, (byte)255);
        return texels;
    }
}

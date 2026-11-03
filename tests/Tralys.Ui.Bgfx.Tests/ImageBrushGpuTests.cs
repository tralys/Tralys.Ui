using Bgfx.Net;
using Tralys.Ui.Rendering;
using static Tralys.Ui.Bgfx.Tests.TestColors;

namespace Tralys.Ui.Bgfx.Tests;

[NotInParallel(GpuDevice.Key)]
public sealed class ImageBrushGpuTests
{
    [Test]
    public async Task AnImageBrushFill_KeepsItsImageWhole_AlignedInTheBounds_AndPaintsNothingBesideIt()
    {
        using var gpu = new GpuDevice();
        using var image = BgfxTexture2D.Create(
            2,
            1,
            TextureFormat.RGBA8,
            [255, 0, 0, 255, 0, 255, 0, 255],
            flags: (ulong)(SamplerFlags.Point | SamplerFlags.UClamp | SamplerFlags.VClamp)
        );

        var fill = Paint.FromImage(
            new PaintImage(image) { Stretch = Stretch.Uniform, AlignmentY = AlignmentY.Bottom }
        );

        var frame = gpu.Render(
            32,
            32,
            ui =>
            {
                using (ui.Box(width: 32, height: 32, background: fill)) { }
            }
        );

        await Assert.That(frame[8, 8]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[8, 24]).IsEqualTo(Pixel.Opaque(Red));
        await Assert.That(frame[24, 24]).IsEqualTo(Pixel.Opaque(Green));
    }

    [Test]
    public async Task AnImageBrushMask_FromALayerOfAnArray_FadesTheElementByItsAlpha()
    {
        using var gpu = new GpuDevice();
        using var array = new BgfxTextureArray(1, 2, 2);
        array.Write(1, [255, 255, 255, 0, 255, 255, 255, 255], 4);

        var mask = Paint.FromImage(new PaintImage(new RenderTextureLayer(array, 1)));

        var frame = gpu.Render(
            32,
            32,
            ui =>
            {
                using (ui.Box(width: 32, height: 32, background: Red, mask: mask)) { }
            }
        );

        await Assert.That(frame[16, 4]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[16, 28]).IsEqualTo(Pixel.Opaque(Red));
    }
}

using Bgfx.Net;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using static Tralys.Ui.Bgfx.Tests.TestColors;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Bgfx.Tests;

[NotInParallel(GpuDevice.Key)]
public sealed class RenderTargetGpuTests
{
    const int Size = 8;
    const ushort PanelView = GpuDevice.FirstLayerView;

    [Test]
    public async Task AnImageOfATarget_DrawsItsTexelsAsAlreadyPremultiplied()
    {
        using var gpu = new GpuDevice();
        using var renderer = new BgfxViewRenderer(gpu.Rendering);
        using var target = new BgfxRenderTarget(Size, Size);
        using var panel = UiDriver.Render(
            new Swatch { Background = HalfWhite }.Draw,
            new(Size, Size)
        );

        gpu.Rendering.BeginFrame();
        renderer.RenderTo(panel.View, target, PanelView);
        var frame = gpu.Render(Size, Size, ui => ui.Image(target, width: Size, height: Size));

        await Assert.That(frame[4, 4].DistanceTo(Pixel.Half)).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task AMippedTarget_AveragesWhatAViewDrewIntoItsNextMip_ByTheTimeALaterViewRuns()
    {
        const int Stripes = 4;
        const ushort SamplingView = GpuDevice.RenderView + 2;
        using var gpu = new GpuDevice();
        using var renderer = new BgfxViewRenderer(gpu.Rendering);
        using var target = gpu.OpenTarget(Stripes, Stripes, mips: true);
        using var sampling = new BgfxRenderTarget(Stripes, Stripes);
        gpu.Rendering.BeginFrame();
        var list = renderer.DrawList;
        list.Begin(Stripes, Stripes);
        list.Draw(
            GpuDevice.Record(dc =>
            {
                dc.DrawRectangle(White, new Rect(0, 0, 1, Stripes));
                dc.DrawRectangle(White, new Rect(2, 0, 1, Stripes));
            })
        );

        renderer.Submit(GpuDevice.RenderView, Stripes, Stripes, GpuDevice.FirstLayerView, 0);
        Bx.SetViewFrameBuffer(SamplingView, sampling.FrameBuffer);
        Bx.SetViewRect(SamplingView, 0, 0, Stripes, Stripes, 0f, 1f);
        Bx.Touch(SamplingView);
        Bx.Frame(0);
        var mip = gpu.ReadBack(target, mip: 1);

        await Assert.That(mip[0, 0].DistanceTo(Pixel.Half)).IsLessThanOrEqualTo(2);
        await Assert.That(mip[1, 1].DistanceTo(Pixel.Half)).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task AnUnchangedView_DrawsNothing_AndItsTargetKeepsItsPixels()
    {
        using var gpu = new GpuDevice();
        using var renderer = new BgfxViewRenderer(gpu.Rendering);
        using var target = new BgfxRenderTarget(Size, Size);
        using var panel = UiDriver.Render(new Swatch { Background = Red }.Draw, new(Size, Size));

        gpu.Rendering.BeginFrame();
        var first = renderer.RenderTo(panel.View, target, PanelView);
        Bx.Frame(0);
        gpu.Rendering.BeginFrame();
        var second = renderer.RenderTo(panel.View, target, PanelView);
        Bx.Frame(0);
        var frame = gpu.ReadBack(target);

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
        await Assert.That(frame[4, 4]).IsEqualTo(Pixel.Opaque(Red));
    }

    [Test]
    public async Task AnImageWhosePixelsArriveLate_DrawsOnceTheyDo_ThenStopsDrawing()
    {
        using var gpu = new GpuDevice();
        using var renderer = new BgfxViewRenderer(gpu.Rendering);
        using var target = new BgfxRenderTarget(Size, Size);
        using var pixels = BgfxTexture2D.Create(1, 1, TextureFormat.RGBA8, [255, 0, 0, 255]);
        var loading = new LoadingTexture();
        using var panel = UiDriver.Render(
            ui => ui.Image(loading, width: Size, height: Size),
            new(Size, Size)
        );

        gpu.Rendering.BeginFrame();
        renderer.RenderTo(panel.View, target, PanelView);
        Bx.Frame(0);
        gpu.Rendering.BeginFrame();
        var waiting = renderer.RenderTo(panel.View, target, PanelView);
        Bx.Frame(0);
        loading.Handle = pixels.Handle;
        gpu.Rendering.BeginFrame();
        var arrived = renderer.RenderTo(panel.View, target, PanelView);
        Bx.Frame(0);
        gpu.Rendering.BeginFrame();
        var settled = renderer.RenderTo(panel.View, target, PanelView);
        var frame = gpu.ReadBack(target);

        await Assert.That(waiting).IsFalse();
        await Assert.That(arrived).IsTrue();
        await Assert.That(settled).IsFalse();
        await Assert.That(frame[4, 4]).IsEqualTo(Pixel.Opaque(Red));
    }

    [Test]
    public async Task ATargetAsked_AttachesADepthBuffer_AndNoneOtherwise()
    {
        using var gpu = new GpuDevice();
        using var plain = new BgfxRenderTarget(Size, Size);
        using var deep = new BgfxRenderTarget(Size, Size, depth: true);

        await Assert.That(Bx.GetTexture(plain.FrameBuffer, 1).Valid).IsFalse();
        await Assert.That(Bx.GetTexture(deep.FrameBuffer, 1).Valid).IsTrue();
    }

    internal sealed class LoadingTexture : RenderTexture, IBgfxTexture
    {
        public override int Width => Size;

        public override int Height => Size;

        public TextureHandle Handle { get; set; } = new(ushort.MaxValue);
    }
}

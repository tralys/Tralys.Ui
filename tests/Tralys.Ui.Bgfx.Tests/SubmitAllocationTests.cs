using System.Numerics;
using Bgfx.Net;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Bgfx.Tests;

[NotInParallel(GpuDevice.Key)]
public sealed class SubmitAllocationTests
{
    const int Width = 1280;
    const int Height = 720;

    [Test]
    public async Task RenderingAViewIntoATarget_AllocatesNothingOnceWarm_ChangedOrNot()
    {
        using var gpu = new GpuDevice();
        using var renderer = new BgfxViewRenderer(gpu.Rendering);
        using var target = new BgfxRenderTarget(64, 64);
        var swatch = new Swatch { Background = Color.FromRgb(60, 60, 80) };
        using var driver = UiDriver.Render(swatch.Draw, new Size(64, 64));
        var view = driver.View;
        var tick = 0;
        var drawn = 0;
        var skipped = 0;

        void Frame()
        {
            tick++;
            if (tick % 4 == 0)
                swatch.Opacity = tick % 8 == 0 ? 1f : 0.5f;
            view.Update();
            gpu.Rendering.BeginFrame();
            if (renderer.RenderTo(view, target, GpuDevice.RenderView))
                drawn++;
            else
                skipped++;
            Bx.Frame(0);
        }

        var worst = AllocationGate.WorstFrameBytes(Frame);

        await Assert.That(drawn).IsGreaterThan(0);
        await Assert.That(skipped).IsGreaterThan(0);
        await Assert.That(worst).IsEqualTo(0L);
    }

    [Test]
    public async Task AViewWaitingOnAnImagesPixels_AllocatesNothingOnceWarm()
    {
        using var gpu = new GpuDevice();
        using var renderer = new BgfxViewRenderer(gpu.Rendering);
        using var target = new BgfxRenderTarget(64, 64);
        var loading = new RenderTargetGpuTests.LoadingTexture();
        using var driver = UiDriver.Render(
            ui => ui.Image(loading, width: 64, height: 64),
            new Size(64, 64)
        );
        var view = driver.View;

        void Frame()
        {
            view.Update();
            gpu.Rendering.BeginFrame();
            renderer.RenderTo(view, target, GpuDevice.RenderView);
            Bx.Frame(0);
        }

        var worst = AllocationGate.WorstFrameBytes(Frame);

        await Assert.That(worst).IsEqualTo(0L);
    }

    [Test]
    public async Task SubmittingAChangingHudFrame_AllocatesNothingOnceWarm()
    {
        using var gpu = new GpuDevice(rampRows: 2);
        using var target = gpu.OpenTarget(Width, Height);
        using var renderer = new BgfxViewRenderer(gpu.Rendering);
        var list = renderer.DrawList;
        using var icon = BgfxTexture2D.Create(2, 2, TextureFormat.RGBA8, new byte[16]);
        using var atlas = new BgfxWritableTexture(64, 16);
        using var icons = new BgfxTextureArray(8, 8, 4);
        RenderTextureLayer[] iconLayers =
        [
            new(icons, 0),
            new(icons, 1),
            new(icons, 2),
            new(icons, 3),
        ];
        var coverage = new byte[8 * 12];
        GradientStops[] gradients =
        [
            new([new(0f, Color.FromRgb(220, 40, 40)), new(1f, Color.FromRgb(110, 0, 0))]),
            new([new(0f, Color.FromRgb(40, 40, 220)), new(1f, Color.FromRgb(0, 0, 110))]),
            new([new(0f, Color.FromRgb(40, 220, 40)), new(1f, Color.FromRgb(0, 110, 0))]),
        ];
        var hud = new RenderCommandBuffer();
        var layered = new RenderCommandBuffer();
        var masked = new RenderCommandBuffer();
        var card = new RenderCommandBuffer();
        var glyphs = new GlyphQuad[8];
        var pie = new PathBuilder();
        var tick = 0;

        void Frame()
        {
            tick++;
            gpu.Rendering.BeginFrame();
            atlas.Write(tick % 8 * 8, 0, 8, 12, coverage, 8);

            var fraction = 0.5f + 0.5f * MathF.Sin(tick * 0.1f);
            var layer = layered.Open();
            layer.DrawRectangle(Color.FromRgb(30, 30, 30), new Rect(250, 4, 30, 20));
            layer.DrawEllipse(new Rect(265, 4, 20, 20), Color.FromRgb(90, 90, 90), 0, default);
            masked.Open().DrawRectangle(Color.FromRgb(200, 200, 200), new Rect(300, 4, 64, 20));
            card.Open()
                .DrawRoundedRectangle(
                    Color.FromRgb(60, 60, 80),
                    new Rect(400, 30, 60, 24),
                    new CornerRadius(4)
                );
            var dc = hud.Open();
            dc.DrawBoxShadow(
                new Rect(48, 8, 200 * fraction, 16),
                new CornerRadius(4),
                new BoxShadow(0, 2, 6 * fraction, 1, Color.FromArgb(160, 0, 0, 0))
            );
            dc.DrawBox(
                new Rect(48, 8, 200 * fraction, 16),
                new CornerRadius(4),
                new Thickness(1),
                Paint.Linear(gradients[tick % 3], new Vector2(0, 0), new Vector2(0, 1)),
                Color.FromRgb(0, 0, 0)
            );
            dc.DrawImage(icon, new Rect(8, 8, 32, 32));
            var sweep = tick % 60 / 60f * MathF.Tau;
            pie.Clear();
            pie.MoveTo(new Vector2(20, 44));
            pie.LineTo(new Vector2(20, 30));
            pie.ArcTo(
                new Vector2(20 + 14 * MathF.Sin(sweep), 44 - 14 * MathF.Cos(sweep)),
                new Vector2(14, 14),
                0f,
                sweep > MathF.PI,
                true
            );
            pie.Close();
            dc.PushClip(new Rect(4, 28, 32, 32), new CornerRadius(6 + tick % 4));
            dc.PushClip(pie);
            dc.DrawRectangle(Color.FromRgb(20, 20, 20), new Rect(4, 28, 32, 32));
            dc.Pop();
            dc.Pop();
            dc.DrawPath(
                pie,
                Color.FromArgb(120, 0, 0, 0),
                new Pen(Color.FromRgb(255, 255, 255), 1.5f, PenLineJoin.Round)
            );
            dc.DrawArc(
                new Vector2(20, 44),
                14,
                4,
                -MathF.PI / 2,
                tick % 60 / 60f * MathF.Tau,
                Color.FromRgb(240, 200, 60)
            );
            var count = 2 + tick % 3;
            for (var i = 0; i < count; i++)
                glyphs[i] = new GlyphQuad(
                    new Rect(56 + i * 8, 40, 8, 12),
                    new Rect(i * 8, 0, 8, 12)
                );
            dc.DrawGlyphs(atlas, glyphs.AsSpan(0, count), Color.FromRgb(255, 255, 255));
            dc.DrawImage(iconLayers[tick % 4], new Rect(256, 8, 24, 24));

            list.Begin(Width, Height);
            list.PushLayer(0.6f + 0.3f * fraction);
            list.Draw(layered);
            list.Pop();
            list.PushOpacityMask(
                Paint.Linear(gradients[0], new Vector2(0, 0), new Vector2(1, 0), opacity: fraction),
                new Rect(300, 4, 64, 20)
            );
            list.Draw(masked);
            list.Pop();
            list.PushDropShadow(new DropShadow(Color.FromRgb(0, 0, 0), 315, 3, 24 + tick % 8));
            list.Draw(card);
            list.Pop();
            list.PushDropShadow(new DropShadow(Color.FromRgb(0, 0, 0), 270, 1 + tick % 3, 6));
            list.Draw(hud);
            list.Pop();
            renderer.Submit(
                GpuDevice.RenderView,
                Width,
                Height,
                GpuDevice.FirstLayerView,
                GpuDevice.LayerViews
            );
            Bx.Frame(0);
        }

        var worst = AllocationGate.WorstFrameBytes(Frame);

        await Assert.That(list.Batches.Length).IsGreaterThan(1);
        await Assert.That(list.Layers.Length).IsGreaterThanOrEqualTo(4);
        await Assert.That(worst).IsEqualTo(0L);
    }
}

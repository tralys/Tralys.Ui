using System.Numerics;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Engine.Tests;

public sealed class DrawListAllocationTests
{
    [Test]
    public async Task AChangingDashboardFrame_AllocatesNothingOnceWarm()
    {
        var ramps = new GradientRampAtlas();
        var list = new DrawList(ramps);
        var atlas = new TestTexture(256, 256);
        var icon = new TestTexture(32, 32);
        var icons = new TestTextureArray(32, 32, 8);
        var iconLayers = new RenderTextureLayer[8];
        for (var i = 0; i < iconLayers.Length; i++)
            iconLayers[i] = new RenderTextureLayer(icons, i);
        var health = new GradientStops([
            new(0f, Color.FromRgb(220, 40, 40)),
            new(1f, Color.FromRgb(110, 0, 0)),
        ]);
        var imageLine = Paint.FromImage(new PaintImage(icon));
        var chrome = new RenderCommandBuffer();
        var bar = new RenderCommandBuffer();
        var layered = new RenderCommandBuffer();
        var card = new RenderCommandBuffer();
        var cooldown = new RenderCommandBuffer();
        var label = new RenderCommandBuffer();
        var glyphs = new GlyphQuad[8];
        var pie = new PathBuilder();
        var badge = new PathBuilder();
        var notch = new PathBuilder();
        badge.AddEllipse(new Rect(290, 40, 20, 20));
        notch.AddRectangle(new Rect(300, 40, 10, 10));
        var reused = new DrawList(ramps);
        var backdrop = new RenderCommandBuffer();
        backdrop
            .Open()
            .DrawRectangle(
                Paint.Linear(health, new Vector2(0, 0), new Vector2(1, 1)),
                new Rect(0, 0, 64, 64)
            );
        ramps.BeginFrame();
        reused.Begin(64, 64);
        reused.Draw(backdrop);
        var tick = 0;

        var dc = chrome.Open();
        dc.DrawImage(
            icon,
            new Rect(0, 0, 320, 64),
            new Rect(0, 0, 32, 32),
            new NineSlice(new Thickness(8), 1f, SliceFill.Tile)
        );
        dc.DrawBox(
            new Rect(0, 0, 320, 64),
            new CornerRadius(6),
            new Thickness(2),
            Color.FromArgb(200, 20, 20, 30),
            Color.FromRgb(90, 90, 120)
        );
        dc.DrawImage(icon, new Rect(8, 8, 32, 32));
        dc.DrawEllipse(
            new Rect(280, 8, 24, 24),
            Color.FromRgb(40, 160, 60),
            2,
            Color.FromRgb(0, 0, 0)
        );

        void Frame()
        {
            tick++;
            ramps.BeginFrame();
            if (!reused.RetainRamps())
                throw new InvalidOperationException("The reused list lost its ramp row.");

            var fraction = 0.5f + 0.5f * MathF.Sin(tick * 0.1f);
            var layer = layered.Open();
            layer.DrawRectangle(Color.FromRgb(30, 30, 30), new Rect(250, 4, 30, 20));
            layer.DrawEllipse(new Rect(265, 4, 20, 20), Color.FromRgb(90, 90, 90), 0, default);
            card.Open()
                .DrawRoundedRectangle(
                    Color.FromRgb(60, 60, 80),
                    new Rect(200, 30, 60, 24),
                    new CornerRadius(4)
                );
            var bars = bar.Open();
            bars.DrawBoxShadow(
                new Rect(48, 8, 200 * fraction, 16),
                new CornerRadius(4),
                new BoxShadow(0, 2, 6 * fraction, 1, Color.FromArgb(160, 0, 0, 0))
            );
            bars.DrawBox(
                new Rect(48, 8, 200 * fraction, 16),
                new CornerRadius(4),
                new Thickness(1),
                Paint.Linear(health, new Vector2(0, 0), new Vector2(0, 1)),
                Color.FromRgb(0, 0, 0)
            );
            bars.DrawLine(
                new Vector2(48, 30),
                new Vector2(48 + 200 * fraction, 30),
                2,
                Color.FromRgb(255, 255, 255)
            );
            bars.DrawLine(new Vector2(48, 34), new Vector2(48 + 200 * fraction, 34), 2, imageLine);

            var arc = cooldown.Open();
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
            arc.DrawPath(
                pie,
                Color.FromArgb(120, 0, 0, 0),
                new Pen(Color.FromRgb(255, 255, 255), 1.5f, PenLineJoin.Round)
            );
            arc.PushClip(badge);
            arc.DrawPath(notch, Color.FromRgb(200, 0, 0), new Pen(Color.FromRgb(0, 0, 0), 1f));
            arc.Pop();
            arc.PushClip(new Rect(4, 28, 32, 32), new CornerRadius(6 + tick % 4));
            arc.DrawArc(
                new Vector2(20, 44),
                14,
                4,
                -MathF.PI / 2,
                tick % 60 / 60f * MathF.Tau,
                Color.FromRgb(240, 200, 60)
            );
            arc.Pop();

            var count = 2 + tick % 3;
            for (var i = 0; i < count; i++)
                glyphs[i] = new GlyphQuad(
                    new Rect(56 + i * 8, 40, 8, 12),
                    new Rect(i * 8, 0, 8, 12)
                );
            var text = label.Open();
            text.DrawGlyphs(atlas, glyphs.AsSpan(0, count), Color.FromRgb(255, 255, 255));
            text.DrawImage(iconLayers[tick % 8], new Rect(256, 8, 24, 24));

            list.Begin(1280, 720);
            list.PushTransform(Matrix3x2.CreateTranslation(16, 16));
            list.PushOpacity(0.9f);
            list.PushClip(new Rect(0, 0, 320, 64));
            list.Draw(chrome);
            list.PushLayer(0.6f + 0.3f * fraction);
            list.Draw(layered);
            list.Pop();
            list.PushDropShadow(new DropShadow(Color.FromRgb(0, 0, 0), 315, 3, 24 + tick % 8));
            list.Draw(card);
            list.Pop();
            list.PushDropShadow(new DropShadow(Color.FromRgb(0, 0, 0), 270, 1 + tick % 3, 6));
            list.Draw(bar);
            list.Pop();
            list.Draw(cooldown);
            list.PushOpacityMask(
                Paint.Linear(health, new Vector2(0, 0), new Vector2(1, 0), opacity: fraction),
                new Rect(56, 40, 64, 12)
            );
            list.Draw(label);
            list.Pop();
            list.Pop();
            list.Pop();
            list.Pop();
        }

        var worst = AllocationGate.WorstFrameBytes(Frame);

        await Assert.That(list.QuadCount).IsGreaterThan(0);
        await Assert.That(worst).IsEqualTo(0L);
    }

    [Test]
    public async Task AClipGainingEdgesEveryFrame_AllocatesNothingUntilItsStorageDoubles()
    {
        var ramps = new GradientRampAtlas();
        var list = new DrawList(ramps);
        var saw = new PathBuilder();
        var teeth = 32;

        void Frame()
        {
            teeth++;
            saw.Clear();
            saw.MoveTo(new Vector2(0, 10));
            for (var i = 0; i < teeth; i++)
            {
                saw.LineTo(new Vector2(i * 2 + 1, 0));
                saw.LineTo(new Vector2(i * 2 + 2, 10));
            }
            saw.LineTo(new Vector2(teeth * 2, 20));
            saw.LineTo(new Vector2(0, 20));
            saw.Close();
            ramps.BeginFrame();
            list.Begin(256, 64);
            list.PushClip(saw);
            list.Pop();
        }

        // Past 59 teeth the mesh itself outgrows its first storage.
        var worst = AllocationGate.WorstFrameBytes(Frame, frames: 8, windows: 3, warmup: 2);

        await Assert.That(worst).IsEqualTo(0L);
    }
}

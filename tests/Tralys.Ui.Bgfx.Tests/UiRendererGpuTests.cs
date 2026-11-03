using System.Numerics;
using System.Runtime.CompilerServices;
using Bgfx.Net;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using static Tralys.Ui.Bgfx.Tests.TestColors;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Bgfx.Tests;

[NotInParallel(GpuDevice.Key)]
public sealed class UiRendererGpuTests
{
    [Test]
    public async Task ABox_FillsInsideItsBorder_AndPaintsTheBand()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(
            64,
            64,
            dc => dc.DrawBox(new Rect(8, 8, 48, 48), default, new Thickness(8), Green, Red)
        );

        await Assert.That(frame[32, 32]).IsEqualTo(Pixel.Opaque(Green));
        await Assert.That(frame[12, 32]).IsEqualTo(Pixel.Opaque(Red));
        await Assert.That(frame[4, 32]).IsEqualTo(Pixel.Clear);
    }

    [Test]
    public async Task ABorderOrStrokeThatFillsItsShape_CoversItsMiddleFully()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(
            32,
            32,
            dc =>
            {
                dc.PushTransform(Matrix3x2.CreateScale(1.5f));
                dc.DrawBox(new Rect(4, 4, 2, 8), default, new Thickness(1), Green, White);
                dc.DrawEllipse(new Rect(10, 4, 2, 2), Green, 1, White);
            }
        );

        await Assert.That(frame[7, 12]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[16, 7]).IsEqualTo(Pixel.Opaque(White));
    }

    [Test]
    public async Task TheOrigin_IsTheTopLeftPixel()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(64, 64, dc => dc.DrawRectangle(White, new Rect(0, 0, 16, 16)));

        await Assert.That(frame[4, 4]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[4, 59]).IsEqualTo(Pixel.Clear);
    }

    [Test]
    public async Task CornerRadii_RunClockwiseFromTheTopLeft()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(
            64,
            64,
            dc =>
                dc.DrawRoundedRectangle(
                    White,
                    new Rect(0, 0, 64, 64),
                    new CornerRadius(24, 0, 0, 0)
                )
        );

        await Assert.That(frame[1, 1]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[62, 1]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[62, 62]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[1, 62]).IsEqualTo(Pixel.Opaque(White));
    }

    [Test]
    public async Task AClip_HidesWhatFallsOutsideIt()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(
            64,
            64,
            dc =>
            {
                dc.PushClip(new Rect(0, 0, 32, 64));
                dc.DrawRectangle(White, new Rect(0, 0, 64, 64));
            }
        );

        await Assert.That(frame[16, 10]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[48, 10]).IsEqualTo(Pixel.Clear);
    }

    [Test]
    public async Task AClippedImage_KeepsItsTexelsWhereTheyWere()
    {
        using var gpu = new GpuDevice();
        using var columns = RedThenGreen();

        var frame = gpu.Render(
            64,
            64,
            dc =>
            {
                dc.PushClip(new Rect(24, 0, 40, 64));
                dc.DrawImage(columns, new Rect(0, 0, 64, 64));
            }
        );

        await Assert.That(frame[26, 32]).IsEqualTo(Pixel.Opaque(Red));
        await Assert.That(frame[40, 32]).IsEqualTo(Pixel.Opaque(Green));
    }

    [Test]
    public async Task AClippedImage_UnderAQuarterTurn_KeepsItsTexelsWhereTheyWere()
    {
        using var gpu = new GpuDevice();
        using var columns = RedThenGreen();

        var frame = gpu.Render(
            64,
            64,
            dc =>
            {
                dc.PushClip(new Rect(0, 24, 64, 40));
                dc.PushTransform(new Matrix3x2(0, 1, -1, 0, 64, 0));
                dc.DrawImage(columns, new Rect(0, 0, 64, 64));
            }
        );

        await Assert.That(frame[32, 26]).IsEqualTo(Pixel.Opaque(Red));
        await Assert.That(frame[32, 40]).IsEqualTo(Pixel.Opaque(Green));
    }

    [Test]
    public async Task ATurnedBox_StaysInsideAClipItCrosses()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(
            64,
            64,
            dc =>
            {
                dc.PushClip(new Rect(0, 0, 32, 64));
                dc.PushTransform(Matrix3x2.CreateRotation(MathF.PI / 4, new Vector2(32, 32)));
                dc.DrawRectangle(White, new Rect(16, 16, 32, 32));
            }
        );

        await Assert.That(frame[28, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[36, 32]).IsEqualTo(Pixel.Clear);
    }

    [Test]
    public async Task AnEdgeUnderAStretch_LosesNoCoverage()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(
            64,
            64,
            dc =>
            {
                dc.PushTransform(Matrix3x2.CreateScale(4, 2));
                dc.DrawRectangle(White, new Rect(2, 2, 8, 8));
            }
        );

        await Assert.That(frame[24, 3].G + frame[24, 4].G).IsBetween(253, 257);
    }

    [Test]
    public async Task AListSubmittedAgainAsItWas_UploadsNothing_AndARebuiltOne_DrawsItsChange()
    {
        using var gpu = new GpuDevice();
        using var renderer = new BgfxViewRenderer(gpu.Rendering);
        using var target = gpu.OpenTarget(8, 8);
        Rebuild(gpu, renderer, Red);
        renderer.Submit(GpuDevice.RenderView, 8, 8, 0, 0);
        Bx.Frame(0);

        gpu.Rendering.BeginFrame();
        renderer.Submit(GpuDevice.RenderView, 8, 8, 0, 0);
        var resubmitted = renderer.UploadedBytes;
        Bx.Frame(0);
        Rebuild(gpu, renderer, Green);
        renderer.Submit(GpuDevice.RenderView, 8, 8, 0, 0);
        var rebuilt = renderer.UploadedBytes;
        var frame = gpu.ReadBack(target);

        await Assert.That(resubmitted).IsEqualTo(0);
        await Assert.That(rebuilt).IsGreaterThan(0);
        await Assert.That(frame[4, 4]).IsEqualTo(Pixel.Opaque(Green));
    }

    [Test]
    public async Task APatchedFrame_UploadsOnlyItsPatchedVertices_AndDrawsTheChange()
    {
        using var gpu = new GpuDevice();
        using var renderer = new BgfxViewRenderer(gpu.Rendering);
        using var target = gpu.OpenTarget(8, 8);
        var swatch = new Swatch { Background = Red };
        using var driver = UiDriver.Render(swatch.Draw, new Size(8, 8));
        var view = driver.View;
        gpu.Rendering.BeginFrame();
        renderer.Render(view, GpuDevice.RenderView);
        Bx.Frame(0);
        swatch.Background = Blue;
        view.Update();
        gpu.Rendering.BeginFrame();
        renderer.Render(view, GpuDevice.RenderView);
        Bx.Frame(0);

        swatch.Background = Green;
        view.Update();
        gpu.Rendering.BeginFrame();
        renderer.Render(view, GpuDevice.RenderView);
        var uploaded = renderer.UploadedBytes;
        var frame = gpu.ReadBack(target);

        await Assert.That(uploaded).IsEqualTo(4 * Unsafe.SizeOf<UiVertex>());
        await Assert.That(frame[4, 4]).IsEqualTo(Pixel.Opaque(Green));
    }

    static void Rebuild(GpuDevice gpu, BgfxViewRenderer renderer, Color color)
    {
        gpu.Rendering.BeginFrame();
        var list = renderer.DrawList;
        list.Begin(8, 8);
        list.Draw(GpuDevice.Record(dc => dc.DrawRectangle(color, new Rect(0, 0, 8, 8))));
    }

    [Test]
    public async Task TwoViewsOnOneDevice_EachDrawTheirOwnGradientInTheSameFrame()
    {
        const ushort SecondView = GpuDevice.RenderView + 2;
        using var gpu = new GpuDevice();
        using var first = new BgfxViewRenderer(gpu.Rendering);
        using var second = new BgfxViewRenderer(gpu.Rendering);
        using var firstTarget = gpu.OpenTarget(8, 8);
        using var secondTarget = new BgfxRenderTarget(8, 8);
        Bx.SetViewFrameBuffer(SecondView, secondTarget.FrameBuffer);
        Bx.SetViewClear(SecondView, (ushort)ClearFlags.Color, 0x000000ff, 1f, 0);

        gpu.Rendering.BeginFrame();
        FillWithGradient(first, Red);
        first.Submit(GpuDevice.RenderView, 8, 8, 0, 0);
        FillWithGradient(second, Green);
        second.Submit(SecondView, 8, 8, 0, 0);
        var firstFrame = gpu.ReadBack(firstTarget);
        var secondFrame = gpu.ReadBack(secondTarget);

        await Assert.That(firstFrame[4, 4]).IsEqualTo(Pixel.Opaque(Red));
        await Assert.That(secondFrame[4, 4]).IsEqualTo(Pixel.Opaque(Green));
    }

    static void FillWithGradient(BgfxViewRenderer renderer, Color color)
    {
        var stops = new GradientStops([new(0f, color), new(1f, color)]);
        var list = renderer.DrawList;
        list.Begin(8, 8);
        list.Draw(
            GpuDevice.Record(dc =>
                dc.DrawRectangle(
                    Paint.Linear(stops, new Vector2(0, 0), new Vector2(1, 0)),
                    new Rect(0, 0, 8, 8)
                )
            )
        );
    }

    [Test]
    public async Task ALinearGradient_RunsFromItsStartToItsEnd()
    {
        using var gpu = new GpuDevice();
        var stops = new GradientStops([new(0f, Red), new(1f, Blue)]);

        var frame = gpu.Render(
            64,
            8,
            dc =>
                dc.DrawRectangle(
                    Paint.Linear(stops, new Vector2(0, 0), new Vector2(1, 0)),
                    new Rect(0, 0, 64, 8)
                )
        );

        await Assert.That(frame[1, 4].DistanceTo(Pixel.Opaque(Red))).IsLessThanOrEqualTo(12);
        await Assert.That(frame[62, 4].DistanceTo(Pixel.Opaque(Blue))).IsLessThanOrEqualTo(12);
    }

    [Test]
    public async Task AnArc_SweepsClockwiseFromItsStartAngle()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(
            64,
            64,
            dc => dc.DrawArc(new Vector2(32, 32), 30, 30, -MathF.PI / 2, MathF.PI / 2, White)
        );

        await Assert.That(frame[48, 16]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[16, 16]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[48, 48]).IsEqualTo(Pixel.Clear);
    }

    [Test]
    public async Task ABoxShadow_FallsOffAcrossItsEdges_AndShowsOnlyOnItsSideOfTheBox()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(
            128,
            64,
            dc =>
            {
                dc.DrawBoxShadow(
                    new Rect(16, 16, 24, 24),
                    default,
                    new BoxShadow(8, 0, 8, 0, White)
                );
                dc.DrawBoxShadow(
                    new Rect(80, 16, 32, 32),
                    default,
                    new BoxShadow(0, 0, 4, 4, White, IsInset: true)
                );
            }
        );

        await Assert.That(frame[44, 28].R).IsGreaterThan((byte)180);
        await Assert.That(frame[47, 28].R).IsBetween((byte)90, (byte)170);
        await Assert.That(frame[56, 28].R).IsLessThan((byte)20);
        await Assert
            .That(frame[10, 28].R)
            .IsLessThan((byte)20)
            .Because("the offset moves it right");
        await Assert
            .That(frame[30, 28])
            .IsEqualTo(Pixel.Clear)
            .Because("it never shows under its box");
        await Assert.That(frame[82, 32].R).IsGreaterThan((byte)180);
        await Assert.That(frame[96, 32]).IsEqualTo(Pixel.Clear);
        await Assert
            .That(frame[76, 32])
            .IsEqualTo(Pixel.Clear)
            .Because("an inset one stays inside");
    }

    [Test]
    public async Task AnArrayImage_SamplesTheLayerItNames()
    {
        using var gpu = new GpuDevice();
        using var icons = new BgfxTextureArray(1, 1, 2);
        icons.Write(0, [255, 0, 0, 255], 4);
        icons.Write(1, [0, 255, 0, 255], 4);

        var frame = gpu.Render(
            64,
            64,
            dc =>
            {
                dc.DrawImage(new RenderTextureLayer(icons, 1), new Rect(0, 0, 32, 64));
                dc.DrawImage(new RenderTextureLayer(icons, 0), new Rect(32, 0, 32, 64));
            }
        );

        await Assert.That(frame[16, 32]).IsEqualTo(Pixel.Opaque(Green));
        await Assert.That(frame[48, 32]).IsEqualTo(Pixel.Opaque(Red));
    }

    [Test]
    public async Task DistanceFieldGlyphs_ReadTheirEdgeWhereTheFieldCrosses128()
    {
        using var gpu = new GpuDevice();
        using var atlas = BgfxTexture2D.Create(
            8,
            1,
            TextureFormat.R8,
            [224, 192, 160, 128, 96, 64, 32, 0],
            flags: (ulong)(SamplerFlags.UClamp | SamplerFlags.VClamp)
        );

        var frame = gpu.Render(
            64,
            64,
            dc =>
                dc.DrawGlyphs(
                    atlas,
                    [new GlyphQuad(new Rect(0, 0, 64, 64), new Rect(0, 0, 8, 1))],
                    White
                )
        );

        await Assert.That(frame[20, 32]).IsEqualTo(Pixel.Opaque(White));
        await Assert.That(frame[36, 32]).IsEqualTo(Pixel.Clear);
    }

    [Test]
    public async Task AGlyphShrunkToFewPixels_LeavesTheFieldsOutsideClear()
    {
        using var gpu = new GpuDevice();
        using var atlas = BgfxTexture2D.Create(
            64,
            64,
            TextureFormat.R8,
            new byte[64 * 64],
            flags: (ulong)(SamplerFlags.UClamp | SamplerFlags.VClamp)
        );

        var frame = gpu.Render(
            16,
            16,
            dc =>
                dc.DrawGlyphs(
                    atlas,
                    [new GlyphQuad(new Rect(0, 0, 8, 8), new Rect(0, 0, 64, 64))],
                    White
                )
        );

        await Assert.That(frame[4, 4]).IsEqualTo(Pixel.Clear);
    }

    [Test]
    public async Task AnImageAndAGlyphInOneDraw_EachSampleTheirOwnTexture()
    {
        using var gpu = new GpuDevice();
        using var image = BgfxTexture2D.Create(1, 1, TextureFormat.RGBA8, [255, 0, 0, 255]);
        using var atlas = BgfxTexture2D.Create(
            2,
            1,
            TextureFormat.R8,
            [255, 0],
            flags: (ulong)(SamplerFlags.Point | SamplerFlags.UClamp | SamplerFlags.VClamp)
        );

        var frame = gpu.Render(
            64,
            16,
            dc =>
            {
                dc.DrawImage(image, new Rect(0, 0, 32, 16));
                dc.DrawGlyphs(
                    atlas,
                    [new GlyphQuad(new Rect(32, 0, 32, 16), new Rect(0, 0, 1, 1))],
                    White
                );
            }
        );

        await Assert.That(frame[16, 8]).IsEqualTo(Pixel.Opaque(Red));
        await Assert.That(frame[48, 8]).IsEqualTo(Pixel.Opaque(White));
    }

    [Test]
    public async Task AnImageWithNoPixelsYet_LeavesTheGlyphsInItsDrawShowing()
    {
        using var gpu = new GpuDevice();
        using var atlas = BgfxTexture2D.Create(
            2,
            1,
            TextureFormat.R8,
            [255, 0],
            flags: (ulong)(SamplerFlags.Point | SamplerFlags.UClamp | SamplerFlags.VClamp)
        );
        var loading = new RenderTargetGpuTests.LoadingTexture();

        var frame = gpu.Render(
            64,
            16,
            dc =>
            {
                dc.DrawImage(loading, new Rect(0, 0, 32, 16));
                dc.DrawGlyphs(
                    atlas,
                    [new GlyphQuad(new Rect(32, 0, 32, 16), new Rect(0, 0, 1, 1))],
                    White
                );
            }
        );

        await Assert.That(frame[16, 8]).IsEqualTo(Pixel.Clear);
        await Assert.That(frame[48, 8]).IsEqualTo(Pixel.Opaque(White));
    }

    [Test]
    public async Task AWrittenRegion_LandsAtItsOffsetAndStride_OverZeroedTexels()
    {
        using var gpu = new GpuDevice();
        using var atlas = new BgfxTextureFactory().CreateWritable(4, 4);
        atlas.Write(2, 1, 2, 2, [255, 255, 0, 255, 255, 0], 3);

        var frame = gpu.Render(
            96,
            32,
            dc =>
                dc.DrawGlyphs(
                    atlas,
                    [
                        new GlyphQuad(new Rect(0, 0, 32, 32), new Rect(2, 1, 1, 1)),
                        new GlyphQuad(new Rect(32, 0, 32, 32), new Rect(2, 2, 1, 1)),
                        new GlyphQuad(new Rect(64, 0, 32, 32), new Rect(0, 0, 1, 1)),
                    ],
                    White
                )
        );

        await Assert.That(frame[16, 16].DistanceTo(Pixel.Opaque(White))).IsLessThanOrEqualTo(8);
        await Assert.That(frame[48, 16].DistanceTo(Pixel.Opaque(White))).IsLessThanOrEqualTo(8);
        await Assert.That(frame[80, 16].DistanceTo(Pixel.Clear)).IsLessThanOrEqualTo(8);
    }

    [Test]
    public async Task AGlyphsLevelEdge_RampsAsUnderAnEvenScale_HoweverFarItIsSquashedAcross()
    {
        using var gpu = new GpuDevice();
        using var field = new BgfxTextureFactory().CreateWritable(16, 16);
        var texels = new byte[16 * 16];
        for (var row = 0; row < 16; row++)
            texels.AsSpan(row * 16, 16).Fill((byte)Math.Clamp(368 - 32 * row, 0, 255));
        field.Write(0, 0, 16, 16, texels, 16);
        var white = new GradientStops([new(0f, White), new(1f, White)]);
        GlyphQuad[] solid = [new(new Rect(0f, 0.25f, 16f, 16f), new Rect(0f, 0f, 16f, 16f))];
        GlyphQuad[] painted = [new(new Rect(32f, 0.25f, 16f, 16f), new Rect(0f, 0f, 16f, 16f))];

        Frame Draw(float across) =>
            gpu.Render(
                64,
                24,
                dc =>
                {
                    dc.PushTransform(Matrix3x2.CreateScale(across, 1f));
                    dc.DrawGlyphs(field, solid, White);
                    dc.DrawGlyphs(field, painted, Paint.Linear(white, Vector2.Zero, Vector2.One));
                }
            );

        var even = Draw(1f);
        var squashed = Draw(0.25f);

        for (var y = 0; y < 24; y++)
        {
            await Assert.That(squashed[2, y].DistanceTo(even[8, y])).IsLessThanOrEqualTo(2);
            await Assert.That(squashed[10, y].DistanceTo(even[40, y])).IsLessThanOrEqualTo(2);
        }
    }

    [Test]
    public async Task HalfOpacity_BlendsPremultipliedOverWhatIsBelow()
    {
        using var gpu = new GpuDevice();

        var frame = gpu.Render(
            64,
            64,
            dc =>
            {
                dc.PushOpacity(0.5f);
                dc.DrawRectangle(White, new Rect(0, 0, 64, 64));
            }
        );

        await Assert.That(frame[32, 32].DistanceTo(Pixel.Half)).IsLessThanOrEqualTo(1);
    }

    [Test]
    public async Task Images_ArePremultipliedOnlyWhenTheirTexelsAreStraight()
    {
        using var gpu = new GpuDevice();
        using var straight = BgfxTexture2D.Create(1, 1, TextureFormat.RGBA8, [255, 255, 255, 128]);
        using var premultiplied = BgfxTexture2D.Create(
            1,
            1,
            TextureFormat.RGBA8,
            [128, 128, 128, 128],
            premultiplied: true
        );

        var frame = gpu.Render(
            64,
            64,
            dc =>
            {
                dc.DrawImage(straight, new Rect(0, 0, 32, 64));
                dc.DrawImage(premultiplied, new Rect(32, 0, 32, 64));
            }
        );

        await Assert.That(frame[16, 32].DistanceTo(Pixel.Half)).IsLessThanOrEqualTo(1);
        await Assert.That(frame[48, 32].DistanceTo(Pixel.Half)).IsLessThanOrEqualTo(1);
    }

    static BgfxTexture2D RedThenGreen() =>
        BgfxTexture2D.Create(
            2,
            1,
            TextureFormat.RGBA8,
            [255, 0, 0, 255, 0, 255, 0, 255],
            flags: (ulong)(SamplerFlags.Point | SamplerFlags.UClamp | SamplerFlags.VClamp)
        );

    [Test]
    public async Task TheDrawCalls_CountTheLastSubmitsDraws_AndNoneForAFrameThatDrawsNothing()
    {
        using var gpu = new GpuDevice();
        using var renderer = new BgfxViewRenderer(gpu.Rendering);
        using var target = new BgfxRenderTarget(64, 64);
        var swatch = new Swatch { Background = Green };
        using var painted = UiDriver.Render(swatch.Draw, new Size(64, 64));
        using var bare = UiDriver.Render(static _ => { }, new Size(64, 64));

        gpu.Rendering.BeginFrame();
        renderer.RenderTo(painted.View, target, GpuDevice.RenderView);
        Bx.Frame(0);
        var drawn = renderer.DrawCalls;
        var batches = renderer.DrawList.Batches.Length;
        gpu.Rendering.BeginFrame();
        renderer.RenderTo(bare.View, target, GpuDevice.RenderView);
        Bx.Frame(0);

        await Assert.That(drawn).IsEqualTo(batches);
        await Assert.That(drawn).IsGreaterThan(0);
        await Assert.That(renderer.DrawCalls).IsEqualTo(0);
    }
}

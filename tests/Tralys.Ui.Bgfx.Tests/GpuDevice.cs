using Bgfx.Net;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Bgfx.Tests;

/// <summary>A real device with no surface, on the OS's own renderer: Direct3D 11 on Windows, Metal on macOS, Vulkan
/// elsewhere. bgfx is one process-global device, so every test holding one runs under <see cref="Key"/>.</summary>
sealed unsafe class GpuDevice : IDisposable
{
    static readonly RendererType Renderer =
        OperatingSystem.IsWindows() ? RendererType.Direct3D11
        : OperatingSystem.IsMacOS() ? RendererType.Metal
        : RendererType.Vulkan;

    public const string Key = "bgfx";
    public const ushort FirstLayerView = 0;
    public const ushort LayerViews = 8;
    public const ushort RenderView = FirstLayerView + LayerViews;
    const ushort BlitView = RenderView + 1;

    public GpuDevice(int rampRows = GradientRampAtlas.DefaultRows)
    {
        Bx.RenderFrame(0);
        Init init;
        Bx.InitCtor(&init);
        init.Type = Renderer;
        // bgfx creates a headless device only for a 0x0 back buffer.
        init.SwapChain.Width = 0;
        init.SwapChain.Height = 0;
        if (!Bx.Init(&init) || Bx.GetRendererType() != Renderer)
            throw new InvalidOperationException(
                $"bgfx could not start {Renderer} without a surface."
            );
        Rendering = new BgfxRenderDevice(rampRows);
    }

    public BgfxRenderDevice Rendering { get; }

    /// <summary>The last frame's layers drawn without their effect.</summary>
    public int DroppedLayers { get; private set; }

    /// <summary>An RGBA8 target, cleared to opaque black, that <see cref="RenderView"/> draws into.</summary>
    public BgfxRenderTarget OpenTarget(int width, int height, bool mips = false)
    {
        var target = new BgfxRenderTarget(width, height, mips);
        Bx.SetViewFrameBuffer(RenderView, target.FrameBuffer);
        Bx.SetViewClear(RenderView, (ushort)ClearFlags.Color, 0x000000ff, 1f, 0);
        return target;
    }

    /// <summary>Draws over an opaque black clear and reads the frame back; a drop shadow blurs at
    /// 1/<paramref name="shadowDownsample"/> resolution unless that is zero.</summary>
    public Frame Render(
        int width,
        int height,
        Action<DrawingContext> draw,
        ushort layerViews = LayerViews,
        int shadowDownsample = 0
    )
    {
        var buffer = Record(draw);
        return RenderList(width, height, list => list.Draw(buffer), layerViews, shadowDownsample);
    }

    public Frame RenderList(
        int width,
        int height,
        Action<DrawList> draw,
        ushort layerViews = LayerViews,
        int shadowDownsample = 0
    )
    {
        using var renderer = new BgfxViewRenderer(Rendering);
        Rendering.BeginFrame();
        var list = renderer.DrawList;
        list.ShadowDownsample = shadowDownsample;
        list.Begin(width, height);
        draw(list);
        return Submit(renderer, width, height, layerViews);
    }

    public static RenderCommandBuffer Record(Action<DrawingContext> draw)
    {
        var buffer = new RenderCommandBuffer();
        draw(buffer.Open());
        return buffer;
    }

    /// <summary>Mounts <paramref name="root"/> on a view of <paramref name="width"/> by <paramref name="height"/> and
    /// draws it, as <see cref="Render(int, int, Action{DrawingContext}, ushort, int)"/> does.</summary>
    public Frame Render(int width, int height, Action<Ui> root)
    {
        using var renderer = new BgfxViewRenderer(Rendering);
        using var driver = UiDriver.Render(root, new Size(width, height));
        Rendering.BeginFrame();
        driver.View.Render(renderer.DrawList);
        return Submit(renderer, width, height, LayerViews);
    }

    Frame Submit(BgfxViewRenderer renderer, int width, int height, ushort layerViews)
    {
        using var target = OpenTarget(width, height);
        renderer.Submit(RenderView, width, height, FirstLayerView, layerViews);
        DroppedLayers = renderer.DroppedLayers;
        return ReadBack(target);
    }

    public Frame ReadBack(BgfxRenderTarget target, byte mip = 0)
    {
        var w = (ushort)(target.Width >> mip);
        var h = (ushort)(target.Height >> mip);
        var readback = Bx.CreateTexture2d(
            w,
            h,
            false,
            1,
            TextureFormat.RGBA8,
            (ulong)(TextureFlags.BlitDst | TextureFlags.ReadBack),
            null,
            0
        );

        var source = new TextureRegion
        {
            Handle = target.Color,
            Mip = mip,
            Width = w,
            Height = h,
            Depth = 1,
        };
        var destination = new TextureRegion
        {
            Handle = readback,
            Width = w,
            Height = h,
            Depth = 1,
        };
        Bx.Blit(BlitView, &destination, &source);

        var pixels = new byte[w * h * 4];
        fixed (byte* data = pixels)
        {
            var ready = Bx.ReadTexture(&destination, data);
            var frames = 0;
            while (Bx.Frame(0) < ready)
            {
                if (++frames == 16)
                    throw new InvalidOperationException("The readback never landed.");
            }
        }

        Bx.DestroyTexture(readback);
        return new Frame(w, pixels);
    }

    public void Dispose()
    {
        Rendering.Dispose();
        Bx.Shutdown();
    }
}

readonly record struct Pixel(byte R, byte G, byte B, byte A)
{
    public static readonly Pixel Clear = new(0, 0, 0, 255);
    public static readonly Pixel Half = new(128, 128, 128, 255);

    public static Pixel Opaque(Color color) => new(color.R, color.G, color.B, 255);

    public int DistanceTo(Pixel other) =>
        Math.Max(
            Math.Max(Math.Abs(R - other.R), Math.Abs(G - other.G)),
            Math.Max(Math.Abs(B - other.B), Math.Abs(A - other.A))
        );
}

static class TestColors
{
    public static readonly Color White = Color.FromRgb(255, 255, 255);
    public static readonly Color HalfWhite = Color.FromArgb(128, 255, 255, 255);
    public static readonly Color Red = Color.FromRgb(255, 0, 0);
    public static readonly Color Green = Color.FromRgb(0, 255, 0);
    public static readonly Color Blue = Color.FromRgb(0, 0, 255);
}

sealed class Frame(int width, byte[] rgba)
{
    public Pixel this[int x, int y]
    {
        get
        {
            var i = (y * width + x) * 4;
            return new Pixel(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]);
        }
    }

    /// <summary>How many pixels' green lies well between none and all, as only a soft edge of white over black does.</summary>
    public int SoftPixels()
    {
        var count = 0;
        for (var i = 1; i < rgba.Length; i += 4)
        {
            if (rgba[i] is > 16 and < 240)
                count++;
        }
        return count;
    }

    /// <summary>The largest difference of any channel of any pixel from <paramref name="other"/>'s.</summary>
    public int DistanceTo(Frame other)
    {
        var worst = 0;
        for (var i = 0; i < rgba.Length; i++)
            worst = Math.Max(worst, Math.Abs(rgba[i] - other.Bytes[i]));
        return worst;
    }

    byte[] Bytes => rgba;
}

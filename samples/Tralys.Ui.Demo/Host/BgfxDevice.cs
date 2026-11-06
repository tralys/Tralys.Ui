using Bgfx.Net;
using Tralys.Ui.Bgfx;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Demo;

sealed unsafe class BgfxDevice : IDisposable
{
    // A shadow blurred at a coarser resolution takes three passes, and each clip, mask or fade around it one more.
    public const ushort LayerViews = 8;

    const uint ClearColor = 0x1b2029ff;

    SwapChain _swapChain;
    readonly uint _resetFlags;
    readonly bool _headless;
    bool _disposed;

    /// <summary>Starts bgfx on <paramref name="renderer"/>, on the GPU of <paramref name="vendor"/> and, unless it
    /// is zero, <paramref name="device"/> when there is one, or for <see cref="PciIdFlags.SoftwareRasterizer"/> on
    /// Vulkan, a CPU device such as lavapipe.</summary>
    public BgfxDevice(
        SdlWindow window,
        RendererType renderer,
        bool headless,
        bool vsync,
        PciIdFlags vendor = PciIdFlags.None,
        ushort device = 0
    )
    {
        var supported = SupportedRenderers();
        if (Array.IndexOf(supported, renderer) < 0)
            throw new DemoException(
                $"This platform's bgfx has no {Name(renderer)} renderer; it has {string.Join(", ", Array.ConvertAll(supported, Name))}."
            );

        // Rendering before init keeps bgfx on this thread instead of starting its render thread.
        Bx.RenderFrame(0);
        Init init;
        Bx.InitCtor(&init);
        init.Type = renderer;
        init.VendorId = (ushort)vendor;
        init.DeviceId = device;
        init.Fallback = 0;
        init.Reset = vsync ? (uint)ResetFlags.Vsync : 0u;
        if (headless)
        {
            init.SwapChain.Width = 0;
            init.SwapChain.Height = 0;
        }
        else
        {
            init.PlatformData.Type = Fill(ref init.SwapChain, window);
        }

        if (!Bx.Init(&init))
            throw new DemoException($"bgfx could not start its {Name(renderer)} renderer.");
        if (Bx.GetRendererType() != renderer)
        {
            var started = Bx.GetRendererType();
            Bx.Shutdown();
            throw new DemoException(
                $"bgfx started {Name(started)} when asked for {Name(renderer)}."
            );
        }

        try
        {
            Rendering = new BgfxRenderDevice();
        }
        catch (NotSupportedException e)
        {
            Bx.Shutdown();
            throw new DemoException(e.Message);
        }

        _swapChain = init.SwapChain;
        _resetFlags = init.Reset;
        _headless = headless;
        Renderer = renderer;
    }

    public RendererType Renderer { get; }

    /// <summary>What every view's renderer on this device shares.</summary>
    public BgfxRenderDevice Rendering { get; }

    /// <summary>The PCI vendor id of the device bgfx started on; lavapipe reports Mesa's.</summary>
    public static ushort VendorId => Bx.GetCaps()->VendorId;

    /// <summary>The PCI device id of the device bgfx started on.</summary>
    public static ushort DeviceId => Bx.GetCaps()->DeviceId;

    public static RendererType Default =>
        OperatingSystem.IsWindows() ? RendererType.Direct3D11
        : OperatingSystem.IsMacOS() ? RendererType.Metal
        : RendererType.Vulkan;

    public static string Name(RendererType renderer) =>
        renderer switch
        {
            RendererType.Noop => "no-op",
            RendererType.Direct3D11 => "Direct3D 11",
            RendererType.Direct3D12 => "Direct3D 12",
            RendererType.OpenGL => "OpenGL",
            RendererType.OpenGLES => "OpenGL ES",
            _ => renderer.ToString(),
        };

    /// <summary>A frame buffer drawing to <paramref name="window"/>'s own swap chain, made as the first window's
    /// was; the caller destroys it.</summary>
    public FrameBufferHandle CreateWindowTarget(SdlWindow window)
    {
        var swapChain = _swapChain;
        Fill(ref swapChain, window);
        return Bx.CreateFrameBufferFromSwapChain(&swapChain);
    }

    /// <summary>Resizes <paramref name="target"/>'s swap chain to <paramref name="window"/>'s size, in place: creating
    /// another for the same window would destroy this one twice.</summary>
    [FramePath]
    public void ResizeWindowTarget(FrameBufferHandle target, SdlWindow window)
    {
        var swapChain = _swapChain;
        Fill(ref swapChain, window);
        Bx.UpdateSwapChain(target, &swapChain);
    }

    /// <summary>Clears <paramref name="view"/> to the demo's backdrop at the start of each frame.</summary>
    public static void ClearView(ushort view) =>
        Bx.SetViewClear(view, (ushort)ClearFlags.Color, ClearColor, 1f, 0);

    /// <summary>Clears <paramref name="view"/> to <paramref name="color"/> at the start of each frame.</summary>
    public static void ClearView(ushort view, Color color) =>
        Bx.SetViewClear(
            view,
            (ushort)ClearFlags.Color,
            (uint)(color.R << 24 | color.G << 16 | color.B << 8 | color.A),
            1f,
            0
        );

    static NativeWindowHandleType Fill(ref SwapChain swapChain, SdlWindow window)
    {
        var (nwh, ndt, type) = window.Surface();
        swapChain.Nwh = (void*)nwh;
        swapChain.Ndt = (void*)ndt;
        swapChain.Width = (uint)window.PixelWidth;
        swapChain.Height = (uint)window.PixelHeight;
        return type;
    }

    [FramePath]
    public void Resize(int width, int height)
    {
        if (_headless)
            return;

        _swapChain.Width = (uint)width;
        _swapChain.Height = (uint)height;
        fixed (SwapChain* swapChain = &_swapChain)
            Bx.Reset(_resetFlags, swapChain);
    }

    static RendererType[] SupportedRenderers()
    {
        var supported = stackalloc RendererType[(int)RendererType.Count];
        var count = Bx.GetSupportedRenderers((byte)RendererType.Count, supported);
        return new ReadOnlySpan<RendererType>(supported, count).ToArray();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Rendering.Dispose();
        Bx.Shutdown();
    }
}

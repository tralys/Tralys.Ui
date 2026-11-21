using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Bgfx.Net;
using Tralys.Ui.Bgfx;
using Tralys.Ui.Gallery.Textures;
using Tralys.Ui.Theming;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Gallery.Web;

[SupportedOSPlatform("browser")]
static unsafe partial class GalleryPage
{
    // A shadow blurred at a coarser resolution takes three passes, and each clip, mask or fade around it one more.
    const ushort LayerViews = 8;
    const ushort ViewId = LayerViews;
    const double LongestFrameSeconds = 0.1;

    static readonly BrowserHost Host = new();
    static SwapChain _swapChain;
    static uint _resetFlags;
    static Session? _session;
    static Theme? _cleared;
    static int _addressed = -1;
    static TimeSpan _now;
    static double _previousFrame;

    [JSImport("showStory", "main.js")]
    private static partial void ShowStory(string key);

    [JSImport("openLink", "main.js")]
    private static partial void OpenTab(string url);

    [JSExport]
    internal static void Start(int width, int height, double scale, string? story)
    {
        LandAssets();
        StartRenderer(width, height);
        var device = new BgfxRenderDevice();
        var renderer = new BgfxViewRenderer(device);
        var fonts = new GalleryFonts(new BgfxTextureFactory());
        var images = new AssetTextures();
        var context = new UiContext(fonts.Services, images) { Icons = Icons.Set };
        var view = new UiView(context, Host, (float)scale);
        var model = new GalleryModel(story, states: false, windows: null)
        {
            Copy = static text => Host.SetClipboardText(text),
            OpenLink = static url => OpenTab(url),
        };
        _session = new Session(device, renderer, fonts, images, context, view, model);
        Resize(width, height, scale);
        context.Advance(_now);
        view.Mount(Slot.Of(model, GalleryShell.Root));
    }

    static Session Started =>
        _session
        ?? throw new InvalidOperationException("The page starts the gallery before it calls it.");

    [JSExport]
    [FramePath]
    internal static void Frame(double seconds)
    {
        var elapsed = _previousFrame == 0 ? 0 : seconds - _previousFrame;
        _previousFrame = seconds;
        _now += TimeSpan.FromSeconds(Math.Clamp(elapsed, 0, LongestFrameSeconds));
        var session = Started;
        session.Context.Advance(_now);
        session.Images.Pump();
        session.View.Update();
        session.Device.BeginFrame();
        Clear(session.Context);
        session.Renderer.Render(session.View, ViewId, 0, LayerViews);
        Bx.Frame(0);
        if (session.Model.Shown != _addressed)
        {
            _addressed = session.Model.Shown;
            ShowStory(session.Model.Stories[_addressed].Key);
        }
    }

    [JSExport]
    [FramePath]
    internal static void Resize(int width, int height, double scale)
    {
        _swapChain.Width = (uint)width;
        _swapChain.Height = (uint)height;
        fixed (SwapChain* swapChain = &_swapChain)
            Bx.Reset(_resetFlags, swapChain);
        var view = Started.View;
        view.Scale = (float)scale;
        view.Size = new Size(width / (float)scale, height / (float)scale);
    }

    [JSExport]
    internal static void PointerMove(double x, double y, int buttons, int modifiers) =>
        Started.View.PointerMove(
            Pointer.Mouse,
            At(x, y),
            Buttons(buttons),
            buttons != 0,
            (ModifierKeys)modifiers,
            _now
        );

    [JSExport]
    internal static void PointerDown(double x, double y, int button, int modifiers, int clicks) =>
        Started.View.PointerDown(
            Pointer.Mouse,
            At(x, y),
            (MouseButton)button,
            (ModifierKeys)modifiers,
            _now,
            clicks
        );

    [JSExport]
    internal static void PointerUp(double x, double y, int button, int modifiers) =>
        Started.View.PointerUp(
            Pointer.Mouse,
            At(x, y),
            (MouseButton)button,
            (ModifierKeys)modifiers,
            _now
        );

    [JSExport]
    internal static void PointerLeave() => Started.View.PointerLeave(Pointer.Mouse);

    [JSExport]
    internal static void Wheel(double x, double y, double right, double up, int modifiers) =>
        Started.View.Wheel(At(x, y), new Vector2((float)right, (float)up), (ModifierKeys)modifiers);

    [JSExport]
    internal static void TouchDown(int finger, double x, double y, int modifiers) =>
        Started.View.PointerDown(
            Touch(finger),
            At(x, y),
            MouseButton.Left,
            (ModifierKeys)modifiers,
            _now
        );

    [JSExport]
    internal static void TouchMove(int finger, double x, double y, int modifiers) =>
        Started.View.PointerMove(
            Touch(finger),
            At(x, y),
            MouseButtons.Left,
            true,
            (ModifierKeys)modifiers,
            _now
        );

    [JSExport]
    internal static void TouchUp(int finger, double x, double y, int modifiers) =>
        Started.View.PointerUp(
            Touch(finger),
            At(x, y),
            MouseButton.Left,
            (ModifierKeys)modifiers,
            _now
        );

    [JSExport]
    internal static void TouchCancel(int finger) => Started.View.PointerCancel(Touch(finger));

    [JSExport]
    internal static bool KeyDown(string code, string name, int modifiers, bool repeat, string text)
    {
        var view = Started.View;
        var taken = view.KeyDown(BrowserKeys.ToKey(code, name), (ModifierKeys)modifiers, repeat);
        if (text.Length > 0)
            taken |= view.TextInput(text);
        return taken;
    }

    [JSExport]
    internal static void KeyUp(string code, string name, int modifiers) =>
        Started.View.KeyUp(BrowserKeys.ToKey(code, name), (ModifierKeys)modifiers);

    // A paste event is the one way a page reads the clipboard without awaiting.
    [JSExport]
    internal static void Paste(string text)
    {
        Host.Paste(text);
        var view = Started.View;
        view.KeyDown(Key.V, ModifierKeys.Control, false);
        view.KeyUp(Key.V, ModifierKeys.Control);
    }

    [JSExport]
    internal static void Activate() => Started.View.WindowFocus(true);

    [JSExport]
    internal static void Deactivate() => Started.View.WindowFocus(false);

    static Vector2 At(double x, double y) => new((float)x, (float)y);

    static Pointer Touch(int finger) => new(PointerKind.Touch, (ulong)finger);

    // The DOM numbers the right button before the middle one.
    static MouseButtons Buttons(int buttons) =>
        (MouseButtons)(buttons & ~6)
        | ((buttons & 2) != 0 ? MouseButtons.Right : 0)
        | ((buttons & 4) != 0 ? MouseButtons.Middle : 0);

    [FramePath]
    static void Clear(UiContext context)
    {
        if (ReferenceEquals(_cleared, context.Theme))
            return;

        _cleared = context.Theme;
        var color = _cleared.Color(ColorToken.Bg);
        Bx.SetViewClear(
            ViewId,
            (ushort)ClearFlags.Color,
            (uint)(color.R << 24 | color.G << 16 | color.B << 8 | color.A),
            1f,
            0
        );
    }

    sealed class Session(
        BgfxRenderDevice device,
        BgfxViewRenderer renderer,
        GalleryFonts fonts,
        AssetTextures images,
        UiContext context,
        UiView view,
        GalleryModel model
    )
    {
        public BgfxRenderDevice Device { get; } = device;

        public BgfxViewRenderer Renderer { get; } = renderer;

        public GalleryFonts Fonts { get; } = fonts;

        public AssetTextures Images { get; } = images;

        public UiContext Context { get; } = context;

        public UiView View { get; } = view;

        public GalleryModel Model { get; } = model;
    }

    static void StartRenderer(int width, int height)
    {
        // Rendering before init keeps bgfx on this thread instead of starting its render thread.
        Bx.RenderFrame(0);
        Init init;
        Bx.InitCtor(&init);
        init.Type = RendererType.WebGPU;
        init.Fallback = 0;
        init.Reset = (uint)ResetFlags.Vsync;
        init.SwapChain.Nwh = (void*)Marshal.StringToHGlobalAnsi("#canvas");
        init.SwapChain.Width = (uint)width;
        init.SwapChain.Height = (uint)height;
        init.PlatformData.Context = Bx.ImportPageWebGpuDevice();
        if (!Bx.Init(&init) || Bx.GetRendererType() != RendererType.WebGPU)
            throw new InvalidOperationException("bgfx could not start its WebGPU renderer.");

        _swapChain = init.SwapChain;
        _resetFlags = init.Reset;
    }

    // Stories name their images by path under the asset folder, and a page has no files but the ones written here.
    static void LandAssets()
    {
        var assembly = typeof(GalleryPage).Assembly;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            var path = Path.Combine(AppContext.BaseDirectory, name);
            if (Path.GetDirectoryName(path) is { } directory)
                Directory.CreateDirectory(directory);
            using var resource =
                assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException(
                    $"The listed resource {name} does not open."
                );
            using var file = File.Create(path);
            resource.CopyTo(file);
        }
    }
}

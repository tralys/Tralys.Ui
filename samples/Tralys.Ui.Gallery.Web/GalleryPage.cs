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
    static GalleryFonts _fonts = null!;
    static AssetTextures _images = null!;
    static UiContext _context = null!;
    static UiView _view = null!;
    static GalleryModel _model = null!;
    static BgfxRenderDevice _device = null!;
    static BgfxViewRenderer _renderer = null!;
    static Theme? _cleared;
    static int _addressed = -1;
    static TimeSpan _now;
    static double _previousFrame;

    [JSImport("showStory", "main.js")]
    private static partial void ShowStory(string key);

    [JSExport]
    internal static void Start(int width, int height, double scale, string? story)
    {
        LandAssets();
        StartRenderer(width, height);
        _device = new BgfxRenderDevice();
        _renderer = new BgfxViewRenderer(_device);
        _fonts = new GalleryFonts(new BgfxTextureFactory());
        _images = new AssetTextures();
        _context = new UiContext(_fonts.Services, _images) { Icons = Icons.Set };
        _view = new UiView(_context, Host, (float)scale);
        _model = new GalleryModel(story, states: false, windows: null)
        {
            Copy = static text => Host.SetClipboardText(text),
        };
        Resize(width, height, scale);
        _context.Advance(_now);
        _view.Mount(Slot.Of(_model, GalleryShell.Root));
    }

    [JSExport]
    [FramePath]
    internal static void Frame(double seconds)
    {
        var elapsed = _previousFrame == 0 ? 0 : seconds - _previousFrame;
        _previousFrame = seconds;
        _now += TimeSpan.FromSeconds(Math.Clamp(elapsed, 0, LongestFrameSeconds));
        _context.Advance(_now);
        _model.HoldStates(_view);
        _images.Pump();
        _view.Update();
        _device.BeginFrame();
        Clear();
        _renderer.Render(_view, ViewId, 0, LayerViews);
        Bx.Frame(0);
        if (_model.Shown != _addressed)
        {
            _addressed = _model.Shown;
            ShowStory(_model.Stories[_addressed].Key);
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
        _view.Scale = (float)scale;
        _view.Size = new Size(width / (float)scale, height / (float)scale);
    }

    [JSExport]
    internal static void PointerMove(double x, double y, int buttons, int modifiers) =>
        _view.PointerMove(
            Pointer.Mouse,
            At(x, y),
            Buttons(buttons),
            buttons != 0,
            (ModifierKeys)modifiers,
            _now
        );

    [JSExport]
    internal static void PointerDown(double x, double y, int button, int modifiers, int clicks) =>
        _view.PointerDown(
            Pointer.Mouse,
            At(x, y),
            (MouseButton)button,
            (ModifierKeys)modifiers,
            _now,
            clicks
        );

    [JSExport]
    internal static void PointerUp(double x, double y, int button, int modifiers) =>
        _view.PointerUp(
            Pointer.Mouse,
            At(x, y),
            (MouseButton)button,
            (ModifierKeys)modifiers,
            _now
        );

    [JSExport]
    internal static void PointerLeave() => _view.PointerLeave(Pointer.Mouse);

    [JSExport]
    internal static void Wheel(double x, double y, double right, double up, int modifiers) =>
        _view.Wheel(At(x, y), new Vector2((float)right, (float)up), (ModifierKeys)modifiers);

    [JSExport]
    internal static void TouchDown(int finger, double x, double y, int modifiers) =>
        _view.PointerDown(Touch(finger), At(x, y), MouseButton.Left, (ModifierKeys)modifiers, _now);

    [JSExport]
    internal static void TouchMove(int finger, double x, double y, int modifiers) =>
        _view.PointerMove(
            Touch(finger),
            At(x, y),
            MouseButtons.Left,
            true,
            (ModifierKeys)modifiers,
            _now
        );

    [JSExport]
    internal static void TouchUp(int finger, double x, double y, int modifiers) =>
        _view.PointerUp(Touch(finger), At(x, y), MouseButton.Left, (ModifierKeys)modifiers, _now);

    [JSExport]
    internal static void TouchCancel(int finger) => _view.PointerCancel(Touch(finger));

    [JSExport]
    internal static bool KeyDown(string code, string name, int modifiers, bool repeat, string text)
    {
        var taken = _view.KeyDown(BrowserKeys.ToKey(code, name), (ModifierKeys)modifiers, repeat);
        if (text.Length > 0)
            taken |= _view.TextInput(text);
        return taken;
    }

    [JSExport]
    internal static void KeyUp(string code, string name, int modifiers) =>
        _view.KeyUp(BrowserKeys.ToKey(code, name), (ModifierKeys)modifiers);

    // A paste event is the one way a page reads the clipboard without awaiting.
    [JSExport]
    internal static void Paste(string text)
    {
        Host.Paste(text);
        _view.KeyDown(Key.V, ModifierKeys.Control, false);
        _view.KeyUp(Key.V, ModifierKeys.Control);
    }

    [JSExport]
    internal static void Activate() => _view.WindowFocus(true);

    [JSExport]
    internal static void Deactivate() => _view.WindowFocus(false);

    static Vector2 At(double x, double y) => new((float)x, (float)y);

    static Pointer Touch(int finger) => new(PointerKind.Touch, (ulong)finger);

    // The DOM numbers the right button before the middle one.
    static MouseButtons Buttons(int buttons) =>
        (MouseButtons)(buttons & ~6)
        | ((buttons & 2) != 0 ? MouseButtons.Right : 0)
        | ((buttons & 4) != 0 ? MouseButtons.Middle : 0);

    [FramePath]
    static void Clear()
    {
        if (ReferenceEquals(_cleared, _context.Theme))
            return;

        _cleared = _context.Theme;
        var color = _cleared.Color(ColorToken.Bg);
        Bx.SetViewClear(
            ViewId,
            (ushort)ClearFlags.Color,
            (uint)(color.R << 24 | color.G << 16 | color.B << 8 | color.A),
            1f,
            0
        );
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
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var resource = assembly.GetManifestResourceStream(name)!;
            using var file = File.Create(path);
            resource.CopyTo(file);
        }
    }
}

using System.Diagnostics;
using Bgfx.Net;
using SDL;
using Tralys.Ui.Bgfx;
using Tralys.Ui.Demo.Panel;
using Tralys.Ui.Demo.Workload;
using Tralys.Ui.DevTools;
using Tralys.Ui.Gallery;
using Tralys.Ui.Gallery.Textures;
using Tralys.Ui.Theming;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Demo;

/// <summary>A window, or without one a target of its own, showing one scene across the whole view over the theme's
/// background, drawn through bgfx each frame, with the windows its docks tear panels out into.</summary>
sealed unsafe class DemoHost : IDisposable
{
    // bgfx runs views in id order, so each view's id must follow the ids of the views it composites.
    const ushort FirstPanelLayerView = 0;
    const ushort PanelViewId = FirstPanelLayerView + BgfxDevice.LayerViews;
    const ushort FirstLayerView = PanelViewId + 1;
    const ushort ViewId = FirstLayerView + BgfxDevice.LayerViews;
    const ushort FirstInspectorLayerView = ViewId + 1;
    const ushort InspectorViewId = FirstInspectorLayerView + BgfxDevice.LayerViews;
    internal const ushort ReadBackView = InspectorViewId + 1;
    const double HeadlessFrameSeconds = 1.0 / 60.0;
    const double LongestFrameSeconds = 0.1;

    readonly SdlWindow _window;
    readonly BgfxDevice _device;
    readonly BgfxViewRenderer _renderer;
    readonly GalleryFonts _fonts;
    readonly AssetTextures _images = new();
    readonly UiContext _context;
    readonly UiView _view;
    readonly IScene _scene;
    readonly DockWindows? _dockWindows;
    readonly OffscreenTarget? _target;
    readonly ViewInput _input = new();
    readonly float? _scale;
    readonly bool _headless;
    readonly string _rendererName;
    InspectorPane? _inspector;
    Theme? _cleared;
    TimeSpan _now;
    long _previousFrame;
    FrameTiming _last;
    uint _frameNumber;
    int _droppedInPanel;
    int _droppedInView;
    bool _disposed;

    DemoHost(SdlWindow window, BgfxDevice device, DemoOptions options)
    {
        _window = window;
        _device = device;
        _headless = options.Headless;
        _scale = options.Scale;
        _rendererName = BgfxDevice.Name(device.Renderer);
        _renderer = new BgfxViewRenderer(device.Rendering);
        _fonts = new GalleryFonts(new BgfxTextureFactory());
        _context = new UiContext(_fonts.Services, _images, options.Theme, options.Density)
        {
            Icons = Icons.Set,
        };
        _view = new UiView(_context, new SdlViewHost(window));
        _context.Advance(_now);
        if (!options.Headless)
            _dockWindows = new DockWindows(window, device, _context, _scale);
        _scene = options.Scene switch
        {
            DemoScene.Workload => new WorkloadScene(),
            DemoScene.Panel => new PanelScene(device.Rendering, _context),
            DemoScene.Story when options.Story is { } story => new StoryScene(story, _dockWindows),
            _ => new GalleryScene(options.Story, options.States, _dockWindows, window),
        };
        if (options.Headless && device.Renderer != RendererType.Noop)
        {
            _target = new OffscreenTarget(window.PixelWidth, window.PixelHeight);
            Bx.SetViewFrameBuffer(ViewId, _target.FrameBuffer);
            Bx.SetViewFrameBuffer(InspectorViewId, _target.FrameBuffer);
        }
        if (options.DevTools)
            OpenInspector();
        ApplySize();
        _scene.Mount(_view);
        Retitle();
        _previousFrame = Stopwatch.GetTimestamp();
    }

    /// <exception cref="DemoException">The window or the renderer could not start.</exception>
    public static DemoHost Start(DemoOptions options)
    {
        var window = options.Headless
            ? SdlWindow.OpenHeadless(options.Width, options.Height)
            : SdlWindow.Open("Tralys.Ui demo", options.Width, options.Height);
        BgfxDevice? device = null;
        try
        {
            device = new BgfxDevice(
                window,
                options.Renderer
                    ?? (
                        options.Headless && options.Screenshot is null
                            ? RendererType.Noop
                            : BgfxDevice.Default
                    ),
                options.Headless,
                options.VSync,
                options.Vendor,
                options.Device
            );
            return new DemoHost(window, device, options);
        }
        catch
        {
            device?.Dispose();
            window.Dispose();
            throw;
        }
    }

    public string Description =>
        $"Tralys.Ui demo on {_rendererName} ({_window.VideoDriver}), {_view.PixelWidth}x{_view.PixelHeight} at {_view.Scale:0.##}";

    public UiView View => _view;

    /// <summary>The inspector over the view while it is open: from the start with <c>--devtools</c>, and as F12
    /// toggles it.</summary>
    public Inspector? Inspector => _inspector?.Inspector;

    public FrameTiming LastFrame => _last;

    public WorkloadScene? Workload => _scene as WorkloadScene;

    public PanelScene? Panel => _scene as PanelScene;

    public GalleryScene? Gallery => _scene as GalleryScene;

    [FramePath]
    public bool Frame()
    {
        var start = Stopwatch.GetTimestamp();
        while (_window.Poll(out var input, out var from))
        {
            if (ReferenceEquals(from, _window))
                Deliver(input);
            else
                _dockWindows?.Deliver(from, input);
        }
        if (_window.CloseRequested)
            return false;
        if (_window.TakeResize())
            Resize();
        var pumped = Stopwatch.GetTimestamp();

        var seconds = _headless
            ? HeadlessFrameSeconds
            : Math.Min(
                Stopwatch.GetElapsedTime(_previousFrame, start).TotalSeconds,
                LongestFrameSeconds
            );
        _previousFrame = start;
        _now += TimeSpan.FromSeconds(seconds);
        _context.Advance(_now);
        _scene.Frame(_view, _rendererName, _last);
        _images.Pump();
        _view.Update();
        _inspector?.Inspector.Update();
        _dockWindows?.Update();
        var updated = Stopwatch.GetTimestamp();

        _device.Rendering.BeginFrame();
        if (_scene is PanelScene panel)
        {
            panel.Draw(PanelViewId, FirstPanelLayerView, BgfxDevice.LayerViews);
            ReportDropped(panel.Renderer, ref _droppedInPanel, "panel");
        }
        var rebuilt = _view.Render(_renderer.DrawList);
        _inspector?.Inspector.Render(_inspector.Renderer.DrawList);
        var rendered = Stopwatch.GetTimestamp();

        Clear();
        _renderer.Submit(
            ViewId,
            _view.PixelWidth,
            _view.PixelHeight,
            FirstLayerView,
            BgfxDevice.LayerViews
        );
        ReportDropped(_renderer, ref _droppedInView, "view");
        if (_inspector is { Inspector.View: var inspectorView } pane)
            pane.Renderer.Submit(
                InspectorViewId,
                inspectorView.PixelWidth,
                inspectorView.PixelHeight,
                FirstInspectorLayerView,
                BgfxDevice.LayerViews
            );
        _dockWindows?.Render();
        var submitted = Stopwatch.GetTimestamp();

        _frameNumber = Bx.Frame(0);
        var presented = Stopwatch.GetTimestamp();
        _dockWindows?.DestroyClosed();

        _last = new FrameTiming(
            pumped - start,
            updated - pumped,
            rendered - updated,
            submitted - rendered,
            presented - submitted,
            Bx.GetStats()->NumDraw,
            _renderer.DrawList.QuadCount,
            rebuilt
        );
        return true;
    }

    [FramePath]
    void Deliver(in InputEvent input)
    {
        if (input.Kind is InputKind.KeyDown or InputKind.KeyUp && input.Key == Key.F12)
        {
            if (input.Kind == InputKind.KeyDown && !input.Repeat)
                ToggleInspector();
            return;
        }
        var taken = _inspector is { } pane
            ? _input.Deliver(pane.Inspector, input)
            : _input.Deliver(_view, input);
        if (
            !taken
            && input is { Kind: InputKind.KeyDown, Repeat: false }
            && (input.Modifiers & ModifierKeys.Control) != 0
            && _scene.Shortcut(_view, input.Key)
        )
            Retitle();
    }

    void OpenInspector()
    {
        _inspector = new InspectorPane(
            new Inspector(_view, new SdlViewHost(_window))
            {
                OpenSource = static link => SDL3.SDL_OpenURL(link),
            },
            new BgfxViewRenderer(_device.Rendering)
        );
    }

    void ToggleInspector()
    {
        if (_inspector is null)
            OpenInspector();
        else
        {
            _inspector.Dispose();
            _inspector = null;
        }
        ApplySize();
    }

    [FramePath]
    void Clear()
    {
        if (ReferenceEquals(_cleared, _context.Theme))
            return;

        _cleared = _context.Theme;
        BgfxDevice.ClearView(ViewId, _cleared.Color(ColorToken.Bg));
    }

    [ColdPath]
    void Retitle() => _window.SetTitle($"{_scene.Title(_context)} · {_rendererName}");

    static void ReportDropped(BgfxViewRenderer renderer, ref int reported, string drawn)
    {
        var dropped = renderer.DroppedLayers;
        if (dropped == reported)
            return;

        reported = dropped;
        Span<char> line = stackalloc char[256];
        if (
            dropped > 0
            && line.TryWrite(
                $"The {drawn} drew {dropped} layers without their effect, past its {BgfxDevice.LayerViews} layer views or out of room in a target.",
                out var written
            )
        )
            Console.Error.WriteLine(line[..written]);
    }

    /// <summary>Writes the next frame to <paramref name="path"/> plus <c>.tga</c>: the window's back buffer, read back by
    /// bgfx, or without a window, the target the view draws into.</summary>
    /// <exception cref="DemoException">Nothing was written.</exception>
    public void Screenshot(string path)
    {
        try
        {
            WriteScreenshot(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new DemoException($"Could not write a screenshot to {path}.tga: {e.Message}");
        }
    }

    void WriteScreenshot(string path)
    {
        const int MostFrames = 8;
        var written = path + ".tga";
        if (Path.GetDirectoryName(written) is { } directory)
            Directory.CreateDirectory(directory);
        File.Delete(written);
        if (_target is { } target)
        {
            var ready = target.Read(ReadBackView);
            for (var i = 0; i < MostFrames && _frameNumber < ready; i++)
            {
                if (!Frame())
                    break;
            }
            if (_frameNumber >= ready)
                target.Save(written);
        }
        else
        {
            Bx.RequestScreenShot(new FrameBufferHandle(ushort.MaxValue), path);
            for (var i = 0; i < MostFrames && !File.Exists(written); i++)
            {
                if (!Frame())
                    break;
            }
        }
        if (!File.Exists(written))
            throw new DemoException($"bgfx wrote no screenshot to {written}.");
        Console.WriteLine($"Wrote {written}.");
    }

    [FramePath]
    void Resize()
    {
        if (_window.PixelWidth == 0 || _window.PixelHeight == 0)
            return;

        _device.Resize(_window.PixelWidth, _window.PixelHeight);
        ApplySize();
    }

    [FramePath]
    void ApplySize()
    {
        var scale = _scale ?? _window.DisplayScale;
        _view.Scale = scale;
        var size = new Size(_window.PixelWidth / scale, _window.PixelHeight / scale);
        if (_inspector is { } pane)
            pane.Inspector.Resize(size);
        else
            _view.Size = size;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _dockWindows?.Dispose();
        _inspector?.Dispose();
        _view.Dispose();
        _scene.Dispose();
        _context.Dispose();
        _fonts.Dispose();
        _images.Dispose();
        _renderer.Dispose();
        _target?.Dispose();
        _device.Dispose();
        _dockWindows?.DestroyClosed();
        _window.Dispose();
    }

    sealed class InspectorPane(Inspector inspector, BgfxViewRenderer renderer) : IDisposable
    {
        public Inspector Inspector { get; } = inspector;

        public BgfxViewRenderer Renderer { get; } = renderer;

        public void Dispose()
        {
            Inspector.Dispose();
            Renderer.Dispose();
        }
    }
}

interface IScene : IDisposable
{
    string Title(UiContext context);

    void Mount(UiView view);

    [FramePath]
    void Frame(UiView view, string renderer, in FrameTiming last);

    bool Shortcut(UiView view, Key key);
}

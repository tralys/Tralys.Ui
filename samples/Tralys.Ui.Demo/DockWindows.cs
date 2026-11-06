using System.Numerics;
using Bgfx.Net;
using Tralys.Ui.Bgfx;
using Tralys.Ui.Docking;
using Bx = Bgfx.Net.Bgfx;

namespace Tralys.Ui.Demo;

sealed class DockWindows(SdlWindow main, BgfxDevice device, UiContext context, float? scale)
    : IDockWindowHost,
        IDisposable
{
    const ushort FirstViewId = DemoHost.ReadBackView + 1;
    const ushort ViewsPerWindow = BgfxDevice.LayerViews + 1;

    readonly List<Window> _windows = [];
    readonly List<SdlWindow> _closed = [];
    readonly BgfxDevice _device = device;
    readonly UiContext _context = context;
    readonly float? _scale = scale;

    public Rect MainWindowBounds => main.Bounds;

    public float MainWindowDensity => main.PixelDensity;

    public Vector2 CursorPosition => SdlWindow.GlobalCursor;

    public IDockWindow Open(FloatingWindow window, Vector2 position, Slot content)
    {
        var toPoints = (_scale ?? main.DisplayScale) / main.PixelDensity;
        var sdl = SdlWindow.OpenChild(
            window.Group.ActiveElement?.Title ?? "Floating",
            (int)position.X,
            (int)position.Y,
            (int)(window.ClampWidth(window.Width) * toPoints),
            (int)(window.ClampHeight(window.Height) * toPoints)
        );
        var opened = new Window(this, sdl, NextViewId(), content);
        _windows.Add(opened);
        return opened;
    }

    ushort NextViewId()
    {
        for (var id = FirstViewId; ; id += ViewsPerWindow)
        {
            var taken = false;
            foreach (var window in _windows)
                taken |= window.FirstView == id;
            if (!taken)
                return id;
        }
    }

    [FramePath]
    public void Deliver(SdlWindow from, in InputEvent input)
    {
        foreach (var window in _windows)
        {
            if (ReferenceEquals(window.Sdl, from))
            {
                window.Deliver(input);
                return;
            }
        }
    }

    [FramePath]
    public void Update()
    {
        for (var i = _windows.Count - 1; i >= 0; i--)
            _windows[i].Update();
    }

    [FramePath]
    public void Render()
    {
        foreach (var window in _windows)
            window.Render();
    }

    /// <summary>Destroys the windows closed since the last call. Call it once bgfx has run the frame they closed in,
    /// which presents their swap chains one last time and then destroys them: Wayland ends the whole connection when a
    /// surface goes first.</summary>
    [FramePath]
    public void DestroyClosed()
    {
        for (var i = 0; i < _closed.Count; i++)
            _closed[i].Dispose();
        _closed.Clear();
    }

    /// <summary>Closes every window; they wait for <see cref="DestroyClosed"/>.</summary>
    public void Dispose()
    {
        for (var i = _windows.Count - 1; i >= 0; i--)
            _windows[i].Close();
    }

    sealed class Window : IDockWindow
    {
        readonly DockWindows _host;
        readonly BgfxViewRenderer _renderer;
        readonly FrameBufferHandle _target;
        readonly UiView _view;
        readonly ViewInput _input = new();
        bool _closed;

        public Window(DockWindows host, SdlWindow sdl, ushort firstView, Slot content)
        {
            _host = host;
            _renderer = new BgfxViewRenderer(host._device.Rendering);
            Sdl = sdl;
            FirstView = firstView;
            _target = host._device.CreateWindowTarget(sdl);
            BgfxDevice.ClearView(ViewId);
            _view = new UiView(host._context, new SdlViewHost(sdl));
            ApplySize();
            _view.Mount(content);
        }

        public SdlWindow Sdl { get; }

        public ushort FirstView { get; }

        ushort ViewId => (ushort)(FirstView + BgfxDevice.LayerViews);

        public Vector2 Position
        {
            get => new(Sdl.Bounds.X, Sdl.Bounds.Y);
            set => Sdl.MoveTo(value);
        }

        float Scale => _host._scale ?? Sdl.DisplayScale;

        public Vector2 Size => new Vector2(Sdl.PixelWidth, Sdl.PixelHeight) / Scale;

        public event Action? Closed;

        [FramePath]
        public void Update()
        {
            if (Sdl.CloseRequested)
            {
                Closed?.Invoke();
                Close();
                return;
            }
            if (Sdl.TakeResize() && Sdl.PixelWidth > 0 && Sdl.PixelHeight > 0)
            {
                _host._device.ResizeWindowTarget(_target, Sdl);
                ApplySize();
            }
            _view.Update();
        }

        [FramePath]
        void ApplySize()
        {
            _view.Scale = Scale;
            _view.Size = new Size(Sdl.PixelWidth / Scale, Sdl.PixelHeight / Scale);
        }

        [FramePath]
        public void Render()
        {
            // Resetting the main window points every view back at its back buffer.
            Bx.SetViewFrameBuffer(ViewId, _target);
            _renderer.Render(_view, ViewId, FirstView, BgfxDevice.LayerViews);
        }

        [FramePath]
        public void Deliver(in InputEvent input) => _input.Deliver(_view, input);

        public void Close()
        {
            if (_closed)
                return;

            _closed = true;
            _host._windows.Remove(this);
            _view.Dispose();
            Bx.SetViewFrameBuffer(ViewId, new FrameBufferHandle(ushort.MaxValue));
            Bx.DestroyFrameBuffer(_target);
            _renderer.Dispose();
            _host._closed.Add(Sdl);
        }
    }
}

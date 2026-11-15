using System.Numerics;

namespace Tralys.Ui.Docking;

/// <summary>What a <see cref="Dock"/> asks of the app to tear panels out into windows of their own: where its own
/// window and the pointer are on the desktop, and new windows. Desktop points are in the units the host's windows are
/// placed in.</summary>
public interface IDockWindowHost
{
    /// <summary>The dock's own window on the desktop.</summary>
    [FramePath]
    Rect MainWindowBounds { get; }

    /// <summary>The dock's view pixels per desktop unit.</summary>
    [FramePath]
    float MainWindowDensity { get; }

    /// <summary>The pointer on the desktop, wherever it is.</summary>
    [FramePath]
    Vector2 CursorPosition { get; }

    /// <summary>Opens a window for <paramref name="window"/> with its top-left at <paramref name="position"/>, and
    /// mounts <paramref name="content"/> in a view of its own there, sharing the dock's thread.</summary>
    IDockWindow Open(FloatingWindow window, Vector2 position, Slot content);
}

/// <summary>A window a panel was torn out into. The host draws the view it mounted the content in, routes the window's
/// input to it and keeps its size in step with the window.</summary>
public interface IDockWindow
{
    /// <summary>The window's top-left on the desktop.</summary>
    [FramePath]
    Vector2 Position { get; set; }

    /// <summary>The window's size, in its view's units.</summary>
    Vector2 Size { get; }

    void Close();

    /// <summary>Raised when the window closes other than through <see cref="Close"/>, as the user closing it.</summary>
    event Action? Closed;
}

/// <summary>A floating window torn out of its dock into a window of the host's, whose drags it reports back to the
/// dock in desktop terms.</summary>
[FramePath]
internal sealed class TornOut : IDockDragSink, IDisposable
{
    readonly DockModel _dock;
    readonly FloatingWindow _window;
    readonly Action _onClosed;
    readonly Slot _content;
    IDockWindow? _host;
    Vector2 _grab;
    bool _disposed;
    internal UiView? View;

    public TornOut(DockModel dock, FloatingWindow window, Vector2 grab)
    {
        _dock = dock;
        _window = window;
        _grab = grab;
        _content = Slot.Of(this, static (ui, t) => ui.DockTornOut(torn: t));
        _onClosed = DockHandlers.Closing(this);
    }

    internal FloatingWindow Window => _window;

    internal DockModel Dock => _dock;

    internal void Open(IDockWindowHost host, Vector2 position)
    {
        var opened = host.Open(_window, position, _content);
        _host = opened;
        var size = opened.Size;
        _window.Width = size.X;
        _window.Height = size.Y;
        opened.Closed += _onClosed;
    }

    internal void Closed()
    {
        if (!_disposed)
            _dock.TornOutClosed(_window);
    }

    public void MoveTo(Vector2 cursor, float density)
    {
        if (_host is not { } host)
            return;
        var toDesktop = (View?.Scale ?? 1f) / density;
        host.Position = cursor - _grab * toDesktop;
    }

    void IDockDragSink.DragStarted(bool byTitle, DockElement element, Vector2 point, Vector2 grab)
    {
        if (byTitle)
            _grab = grab;
        _dock.CrossStarted(this);
    }

    void IDockDragSink.DragMoved(bool byTitle, Vector2 point, bool overStrip) =>
        _dock.CrossMoved(this, byTitle, overStrip);

    void IDockDragSink.Dropped(bool byTitle, DockElement element, Vector2 point) =>
        _dock.CrossDropped(this, byTitle, element);

    void IDockDragSink.DragEnded() => _dock.CrossEnded(this);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_host is { } host)
        {
            host.Closed -= _onClosed;
            host.Close();
        }
    }
}

internal readonly record struct DockTornOutProps
{
    public TornOut? Torn { get; init; }
}

/// <summary>The panel of a window torn out of a dock, filling the window.</summary>
internal sealed class DockTornOut : Component<DockTornOutProps>
{
    protected override void Render(Ui ui, in DockTornOutProps p)
    {
        if (p.Torn is not { } torn)
            return;
        ui.Effect(torn, 0, static (t, _, cx) => t.View = cx.View);
        ui.Provide(Dock.Context, new DockContext(torn.Dock, torn, torn.Dock.Host.Props.Content));
        ui.DockFloat(window: torn.Window, hostedAlone: true);
    }
}

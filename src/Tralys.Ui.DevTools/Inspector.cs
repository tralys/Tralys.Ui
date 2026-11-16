using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.DevTools;

/// <summary>Inspects a <see cref="UiView"/> from a view of its own, on its own <see cref="UiContext"/> on the target's
/// thread: the target's one tree, components shown as components with their props, render counts and what they read,
/// fragments collapsed and layers under their owners; states held on the component a selected part belongs to; a
/// picker; and overlays over the target of the selected node's box, the gaps between its items and its resolved
/// environment, the node a press would pick, and what repainted. Its view spans the window both share, drawn over the
/// target, which gets all but <see cref="PanelWidth"/> on the left; the window's input goes through the inspector,
/// which sends each release to the view its press went to.</summary>
public sealed class Inspector : IDisposable
{
    /// <summary>The panel's width, in layout units.</summary>
    public const float PanelWidth = 360f;

    static readonly ConditionalWeakTable<UiView, Inspector> s_attached = new();

    readonly UiContext _context;
    readonly InspectorModel _model;
    readonly List<Press> _presses = [];
    readonly List<(Key Key, Route Route)> _keys = [];
    readonly List<(Pointer Pointer, Route Route)> _over = [];
    readonly List<Node> _recorded = [];
    long _mark;
    long _own;
    int _atlasResets;
    bool _measured;
    bool _disposed;

    /// <param name="target">The view to inspect; a view takes one inspector at a time.</param>
    /// <param name="host">The window hosting the inspector's own view.</param>
    public Inspector(UiView target, IViewHost host)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(host);
        if (!s_attached.TryAdd(target, this))
            throw new InvalidOperationException("The view already has an inspector.");
        Target = target;
        var shared = target.Context;
        _context = new UiContext(shared.Text, shared.Textures, shared.Theme, shared.Density);
        if (shared.Icons is { } icons)
            _context.Icons = icons;
        _context.Advance(shared.Now);
        View = new UiView(_context, host, target.Scale)
        {
            Size = new Size(target.Size.Width + PanelWidth, target.Size.Height),
        };
        _model = new InspectorModel(target);
        target.Recorded = _recorded;
        View.Mount<InspectorRoot, InspectorProps>(new InspectorProps { Model = _model });
    }

    /// <summary>The inspected view.</summary>
    public UiView Target { get; }

    internal InspectorModel Model => _model;

    /// <summary>The inspector's own view, as wide as the window: the panel at its right edge and, over the target,
    /// the overlays, which take no input.</summary>
    public UiView View { get; }

    /// <summary>Opens a node's source, handed as a <c>vscode://file</c> link to the file and line it was written at;
    /// the panel's Open button shows only while it is set.</summary>
    public Action<string>? OpenSource
    {
        get => _model.OpenSource;
        set => _model.OpenSource = value;
    }

    /// <summary>The node the panel describes; null for none. Selecting another releases the states held on the last
    /// one's component.</summary>
    public Node? Selected
    {
        get => _model.Selected;
        set
        {
            if (value is not null && !InspectorModel.IsWithin(value, Target.Root))
                throw new ArgumentException(
                    "The node is not in the inspected view.",
                    nameof(value)
                );
            _model.Select(value);
        }
    }

    /// <summary>Whether a press over the target selects what it lands on instead of reaching it; a pick ends
    /// picking.</summary>
    public bool IsPicking
    {
        get => _model.Picking;
        set => SetPicking(value);
    }

    /// <summary>The states held, through <see cref="UiView.ForceStates"/>, on the component the selected node belongs
    /// to, on top of what others hold there.</summary>
    public InputStates Held
    {
        get => _model.Held;
        set => _model.ChangeHeld(value);
    }

    /// <summary>Selects the deepest node the target shows at <paramref name="point"/>, in its layout units, through its
    /// layers and z-order as a press would go, then deeper, so a label in a button or a box that takes no input is
    /// picked too; its ancestors' rows open, and picking ends.</summary>
    /// <returns>The node selected; null where the target shows nothing.</returns>
    public Node? Pick(Vector2 point)
    {
        _model.Select(_model.Pick(point));
        SetPicking(false);
        return Selected;
    }

    /// <summary>Sizes the window both views share, in layout units: the target takes all but the panel's width.</summary>
    public void Resize(Size window)
    {
        Target.Size = new Size(MathF.Max(0f, window.Width - PanelWidth), window.Height);
        View.Size = window;
    }

    /// <summary>Brings the panel and overlays up to date with the target, moves the inspector's clock to the target's
    /// and updates the inspector's view. Call it once a frame, after the target's <see cref="UiView.Update"/>.</summary>
    public void Update()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var start = GC.GetAllocatedBytesForCurrentThread();
        var resets = Target.Context.Text.Atlas.ResetCount;
        if (_measured)
            _model.CountFrame(start - _mark - _own, resets - _atlasResets);
        _atlasResets = resets;
        _measured = true;
        _own = 0;
        _model.Refresh();
        _model.Recorded(_recorded);
        View.Scale = Target.Scale;
        _context.Theme = Target.Context.Theme;
        _context.Density = Target.Context.Density;
        if (_context.Icons is null && Target.Context.Icons is { } icons)
            _context.Icons = icons;
        _context.Advance(Target.Context.Now);
        View.Update();
        _mark = GC.GetAllocatedBytesForCurrentThread();
    }

    /// <summary>Renders the inspector's view into <paramref name="list"/>, as <see cref="UiView.Render"/> does, keeping
    /// what it allocates out of the target's frame bytes.</summary>
    public bool Render(DrawList list)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var rebuilt = View.Render(list);
        _own += GC.GetAllocatedBytesForCurrentThread() - before;
        return rebuilt;
    }

    /// <summary>A press at <paramref name="at"/>, in the window's pixels: over the panel it goes to the inspector's
    /// view, while picking it picks, and otherwise it goes to the target, taking the keys back from the panel.</summary>
    /// <returns>Whether either view took it.</returns>
    public bool PointerDown(
        Pointer p,
        Vector2 at,
        MouseButton b,
        ModifierKeys m,
        TimeSpan t,
        int clicks = 1
    )
    {
        var route =
            OverPanel(at) ? Route.Panel
            : IsPicking ? Route.Picker
            : Route.Target;
        var held = PressOf(p, b);
        if (held >= 0)
        {
            var stale = _presses[held];
            _presses.RemoveAt(held);
            Release(stale.Route, p, at, b, m, t);
        }
        _presses.Add(new Press(p, b, route));
        Enter(p, route);
        switch (route)
        {
            case Route.Panel:
                return View.PointerDown(p, at, b, m, t, clicks);
            case Route.Picker:
                Pick(at / Target.Scale);
                return true;
            default:
                View.ClearFocusByPointer();
                return Target.PointerDown(p, at, b, m, t, clicks);
        }
    }

    /// <summary>A pointer moving to <paramref name="at"/>, in the window's pixels: to the view holding its press, else
    /// the one under it; a view it leaves hears it leave, and while picking the target hears nothing.</summary>
    public bool PointerMove(
        Pointer p,
        Vector2 at,
        MouseButtons buttons,
        bool inContact,
        ModifierKeys m,
        TimeSpan t
    )
    {
        var held = PressOf(p, null);
        var route =
            held >= 0 ? _presses[held].Route
            : OverPanel(at) ? Route.Panel
            : IsPicking ? Route.Picker
            : Route.Target;
        Enter(p, route);
        switch (route)
        {
            case Route.Panel:
                return View.PointerMove(p, at, buttons, inContact, m, t);
            case Route.Picker:
                _model.HoverAt(at / Target.Scale);
                return true;
            default:
                return Target.PointerMove(p, at, buttons, inContact, m, t);
        }
    }

    /// <summary>A press lifting at <paramref name="at"/>, in the window's pixels: to the view its press went to.</summary>
    public bool PointerUp(Pointer p, Vector2 at, MouseButton b, ModifierKeys m, TimeSpan t)
    {
        var held = PressOf(p, b);
        if (held < 0)
            return false;
        var route = _presses[held].Route;
        _presses.RemoveAt(held);
        return Release(route, p, at, b, m, t);
    }

    /// <summary>The system cancelled pointer <paramref name="p"/>: each view holding a press of it hears so.</summary>
    public void PointerCancel(Pointer p)
    {
        for (var i = _presses.Count - 1; i >= 0; i--)
        {
            if (_presses[i].Pointer != p)
                continue;
            var route = _presses[i].Route;
            _presses.RemoveAt(i);
            if (route == Route.Panel)
                View.PointerCancel(p);
            else if (route == Route.Target)
                Target.PointerCancel(p);
        }
    }

    /// <summary>Pointer <paramref name="p"/> left the window.</summary>
    public void PointerLeave(Pointer p) => Enter(p, Route.None);

    /// <summary>A key going down: Ctrl+Shift+C starts or ends picking and Escape ends it; anything else goes to the
    /// panel while it holds the focus, else to the target, and its release goes where it went.</summary>
    /// <returns>Whether the UI took it.</returns>
    public bool KeyDown(Key k, ModifierKeys m, bool repeat)
    {
        var at = KeyOf(k);
        Route route;
        if (k == Key.C && m == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (!repeat || at < 0)
                SetPicking(!IsPicking);
            route = Route.Picker;
        }
        else if (k == Key.Escape && IsPicking)
        {
            SetPicking(false);
            route = Route.Picker;
        }
        else
            route =
                repeat && at >= 0 ? _keys[at].Route
                : View.Focused is not null ? Route.Panel
                : Route.Target;
        if (at >= 0)
            _keys[at] = (k, route);
        else
            _keys.Add((k, route));
        return route switch
        {
            Route.Panel => View.KeyDown(k, m, repeat),
            Route.Target => Target.KeyDown(k, m, repeat),
            _ => true,
        };
    }

    /// <summary>A key coming up: to the view its press went to.</summary>
    public bool KeyUp(Key k, ModifierKeys m)
    {
        var at = KeyOf(k);
        var route = at >= 0 ? _keys[at].Route : Route.Target;
        if (at >= 0)
            _keys.RemoveAt(at);
        return route switch
        {
            Route.Panel => View.KeyUp(k, m),
            Route.Target => Target.KeyUp(k, m),
            _ => true,
        };
    }

    /// <summary>Text typed: to the panel while it holds the focus, else to the target.</summary>
    public bool TextInput(ReadOnlySpan<char> text) =>
        View.Focused is not null ? View.TextInput(text) : Target.TextInput(text);

    /// <summary>The wheel turning at <paramref name="at"/>, in the window's pixels: to the view under it.</summary>
    public bool Wheel(Vector2 at, Vector2 notches, ModifierKeys m) =>
        OverPanel(at) ? View.Wheel(at, notches, m) : Target.Wheel(at, notches, m);

    /// <summary>The window gained or lost the keyboard focus: both views hear it.</summary>
    public void WindowFocus(bool focused)
    {
        Target.WindowFocus(focused);
        View.WindowFocus(focused);
    }

    void SetPicking(bool picking)
    {
        if (picking == _model.Picking)
            return;
        _model.ChangePicking(picking);
        if (!picking)
            return;
        for (var i = 0; i < _over.Count; i++)
        {
            if (_over[i].Route == Route.Target && PressOf(_over[i].Pointer, null) < 0)
            {
                Target.PointerLeave(_over[i].Pointer);
                _over[i] = (_over[i].Pointer, Route.Picker);
            }
        }
    }

    void Enter(Pointer p, Route route)
    {
        var at = -1;
        for (var i = 0; i < _over.Count; i++)
        {
            if (_over[i].Pointer == p)
                at = i;
        }
        var was = at >= 0 ? _over[at].Route : Route.None;
        if (was == route)
            return;
        if (was == Route.Target)
            Target.PointerLeave(p);
        else if (was == Route.Panel)
            View.PointerLeave(p);
        else if (was == Route.Picker)
            _model.HoverAt(null);
        if (at >= 0)
            _over[at] = (p, route);
        else
            _over.Add((p, route));
    }

    bool Release(Route route, Pointer p, Vector2 at, MouseButton b, ModifierKeys m, TimeSpan t) =>
        route switch
        {
            Route.Panel => View.PointerUp(p, at, b, m, t),
            Route.Target => Target.PointerUp(p, at, b, m, t),
            _ => true,
        };

    int PressOf(Pointer p, MouseButton? b)
    {
        for (var i = 0; i < _presses.Count; i++)
        {
            if (_presses[i].Pointer == p && (b is null || _presses[i].Button == b))
                return i;
        }
        return -1;
    }

    int KeyOf(Key k)
    {
        for (var i = 0; i < _keys.Count; i++)
        {
            if (_keys[i].Key == k)
                return i;
        }
        return -1;
    }

    bool OverPanel(Vector2 at) =>
        at.X >= Target.Size.Width * Target.Scale || View.HitTest(at) is not null;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _model.Release();
        if (ReferenceEquals(Target.Recorded, _recorded))
            Target.Recorded = null;
        s_attached.Remove(Target);
        View.Dispose();
        _context.Dispose();
    }

    readonly record struct Press(Pointer Pointer, MouseButton Button, Route Route);

    enum Route : byte
    {
        None,
        Target,
        Panel,
        Picker,
    }
}

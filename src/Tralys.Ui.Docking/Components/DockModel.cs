using System.Numerics;
using Tralys.Ui.Components;

namespace Tralys.Ui.Docking;

// Points are in the view units of the dragging part's view.
internal interface IDockDragSink
{
    void DragStarted(bool byTitle, DockElement element, Vector2 point, Vector2 grab);

    [FramePath]
    void DragMoved(bool byTitle, Vector2 point, bool overStrip);

    void Dropped(bool byTitle, DockElement element, Vector2 point);

    void DragEnded();
}

internal readonly record struct DockLook(
    bool Shown,
    string? Label,
    Vector2 LabelAt,
    Rect Edges,
    Rect CenterBox,
    bool CenterShown,
    int RootLit,
    Rect Compass,
    bool TabsOnly,
    int CompassLit,
    Rect RootPreview,
    Rect GroupPreview
)
{
    public const float RootSize = 40f;
    public const float CompassSize = 48f;
    const float CompassGap = 6f;

    static readonly Vector2[] Steps =
    [
        new(-1f, 0f),
        new(1f, 0f),
        new(0f, -1f),
        new(0f, 1f),
        Vector2.Zero,
    ];

    public bool RootShown(int i) => i < 4 || CenterShown;

    public Rect RootBox(int i) =>
        i switch
        {
            0 => Within(Edges, 0f, 0.5f, RootSize),
            1 => Within(Edges, 1f, 0.5f, RootSize),
            2 => Within(Edges, 0.5f, 0f, RootSize),
            3 => Within(Edges, 0.5f, 1f, RootSize),
            _ => CenterBox,
        };

    public bool CompassShown(int i) =>
        Compass.Width > 0f && (!TabsOnly || i == DockModel.CenterIndex);

    public Rect CompassBox(int i)
    {
        var step = Steps[i];
        return new Rect(
            Compass.X + (Compass.Width - CompassSize) / 2f + step.X * (CompassSize + CompassGap),
            Compass.Y + (Compass.Height - CompassSize) / 2f + step.Y * (CompassSize + CompassGap),
            CompassSize,
            CompassSize
        );
    }

    public static Rect Within(Rect area, float alongX, float alongY, float size) =>
        new(
            area.X + (area.Width - size) * alongX,
            area.Y + (area.Height - size) * alongY,
            size,
            size
        );
}

/// <summary>What a <see cref="Dock"/> keeps between renders: its edges' sizes, the tab groups it shows, the drag in
/// flight and the windows it tore out.</summary>
[FramePath]
internal sealed class DockModel : IDockDragSink, IDisposable
{
    internal const int LeftEdge = 0;
    internal const int RightEdge = 1;
    internal const int TopEdge = 2;
    internal const int BottomEdge = 3;
    internal const int CenterIndex = 4;

    const float RootMargin = 20f;
    const float LabelOffset = 15f;
    const float TearOutInsetX = 30f;
    const float TearOutInsetY = 12f;

    static readonly DockPosition[] RootPositions =
    [
        DockPosition.Left,
        DockPosition.Right,
        DockPosition.Top,
        DockPosition.Bottom,
        DockPosition.Center,
    ];

    internal static readonly DockDirection[] Directions =
    [
        DockDirection.Left,
        DockDirection.Right,
        DockDirection.Top,
        DockDirection.Bottom,
        DockDirection.Center,
    ];

    internal readonly TabDragGroup Strips = new();
    internal readonly List<TabGroupEntry> Groups = [];
    internal readonly Callback<float>[] EdgeChange = new Callback<float>[4];
    internal readonly Callback<float> EdgeCommit;
    readonly Dictionary<FloatingWindow, TornOut> _tornOut = [];
    readonly EventHandler<FloatingWindowEventArgs> _onFloatRemoved;
    internal Host<DockProps> Host;
    internal NodeRef Root = null!;
    internal NodeRef Regions = null!;
    internal NodeRef Center = null!;
    internal State<int> Sizing = null!;
    internal State<int> Torn = null!;
    internal State<DockLook>? Look;
    internal UiView? View;
    DockManager? _attached;
    DockPanelSizes _applied;
    DockPanelSizes _live;
    DockLook _look;
    TornOut? _crossSource;
    TabGroupEntry? _hover;
    DockPosition? _rootHit;
    DockDirection? _compassHit;
    DockGroup? _stripGroup;
    int _stripGap;
    Vector2 _grab;
    bool _dragging;
    bool _leftWindow;

    public DockModel()
    {
        for (var i = 0; i < EdgeChange.Length; i++)
            EdgeChange[i] = Callback<float>.Of(
                this,
                Edges.Of[i],
                static (m, edge, size) => m.EdgeChanged(edge.Index, size)
            );
        EdgeCommit = Callback<float>.Of(this, static (m, _) => m.EdgeCommitted());
        _onFloatRemoved = DockHandlers.Untearing(this);
    }

    DockManager? Manager => Host.Props.Manager;

    internal void Attach(DockManager? manager)
    {
        if (ReferenceEquals(manager, _attached))
            return;
        _attached?.FloatingWindowRemoved -= _onFloatRemoved;
        CloseTornOut();
        _attached = manager;
        manager?.FloatingWindowRemoved += _onFloatRemoved;
    }

    public void Dispose()
    {
        EndDrag();
        _attached?.FloatingWindowRemoved -= _onFloatRemoved;
        _attached = null;
        CloseTornOut();
    }

    internal static float SizeOf(in DockPanelSizes sizes, int edge)
    {
        var size = edge switch
        {
            LeftEdge => sizes.LeftWidth,
            RightEdge => sizes.RightWidth,
            TopEdge => sizes.TopHeight,
            _ => sizes.BottomHeight,
        };
        return size > 0f ? size
            : edge is LeftEdge or RightEdge ? 300f
            : 150f;
    }

    // Only when the manager's sizes change, so a render mid-drag can't snap an edge back.
    internal DockPanelSizes Shown(DockPanelSizes fromManager)
    {
        if (fromManager != _applied)
        {
            _applied = fromManager;
            _live = fromManager;
        }
        return _live;
    }

    void EdgeChanged(int edge, float size)
    {
        switch (edge)
        {
            case LeftEdge:
                _live.LeftWidth = size;
                break;
            case RightEdge:
                _live.RightWidth = size;
                break;
            case TopEdge:
                _live.TopHeight = size;
                break;
            default:
                _live.BottomHeight = size;
                break;
        }
        Sizing.Value++;
    }

    // Whole units: a fractional origin blurs a render target drawn into the panel.
    void EdgeCommitted()
    {
        if (Manager is not { } manager)
            return;
        var recorded = new DockPanelSizes
        {
            LeftWidth = MathF.Round(SizeOf(_live, LeftEdge)),
            RightWidth = MathF.Round(SizeOf(_live, RightEdge)),
            TopHeight = MathF.Round(SizeOf(_live, TopEdge)),
            BottomHeight = MathF.Round(SizeOf(_live, BottomEdge)),
        };
        _applied = recorded;
        _live = recorded;
        Sizing.Value++;
        manager.ResizePanels(recorded);
        RaiseResize();
    }

    internal void RaiseResize() => Host.Props.OnResize.Invoke();

    internal void KeepFloatsWithin(Size area)
    {
        if (Manager is not { } manager)
            return;
        var windows = manager.FloatingWindows;
        for (var i = 0; i < windows.Count; i++)
            windows[i].KeepWithin(area.Width, area.Height);
    }

    internal TabGroupEntry? EntryOf(DockGroup group)
    {
        foreach (var entry in Groups)
        {
            if (ReferenceEquals(entry.Group, group))
                return entry;
        }
        return null;
    }

    internal DockGroup? GroupOf(UiKey key)
    {
        foreach (var entry in Groups)
        {
            if (entry.Group is { } group && UiKey.Of(group) == key)
                return group;
        }
        return null;
    }

    Vector2 Origin =>
        Root.Node is { } root ? new Vector2(root.ViewBounds.X, root.ViewBounds.Y) : default;

    Vector2 Local(Vector2 viewPoint) => viewPoint - Origin;

    Rect LocalBounds(NodeRef node)
    {
        var at = node.Node?.ViewBounds ?? default;
        var origin = Origin;
        return new Rect(at.X - origin.X, at.Y - origin.Y, at.Width, at.Height);
    }

    void IDockDragSink.DragStarted(bool byTitle, DockElement element, Vector2 point, Vector2 grab)
    {
        _dragging = true;
        _leftWindow = false;
        _crossSource = null;
        _grab = byTitle ? grab : default;
        ShowOverlay(byTitle ? null : element.Title);
        MoveLabel(Local(point));
        Publish();
    }

    void IDockDragSink.DragMoved(bool byTitle, Vector2 point, bool overStrip)
    {
        if (!_dragging)
            return;

        var local = Local(point);
        MoveLabel(local);
        if (
            Host.Props.WindowHost is { } host
            && !_leftWindow
            && !host.MainWindowBounds.Contains(host.CursorPosition)
        )
            _leftWindow = true;

        if (overStrip)
        {
            ClearTargets();
            Publish();
            return;
        }

        _stripGroup = byTitle ? StripAt(point, out _stripGap) : null;
        if (_stripGroup is { } strip)
        {
            ClearTargets();
            Strips.Hint(UiKey.Of(strip), _stripGap);
            Publish();
            return;
        }
        Strips.Hint(null, 0);
        UpdateTargets(local);
        Publish();
    }

    void IDockDragSink.Dropped(bool byTitle, DockElement element, Vector2 point)
    {
        if (!_dragging)
            return;

        if (
            _rootHit is null
            && _compassHit is null
            && _stripGroup is null
            && Host.Props.WindowHost is { } host
        )
        {
            var cursor = host.CursorPosition;
            if (!host.MainWindowBounds.Contains(cursor) || _leftWindow)
            {
                TearOut(byTitle, element, cursor);
                return;
            }
        }
        DropOn(byTitle, element, Local(point));
    }

    void IDockDragSink.DragEnded() => EndDrag();

    DockGroup? StripAt(Vector2 point, out int gap)
    {
        gap = 0;
        if (View is not { } view || Strips.HitTest(view, point, out gap) is not { } key)
            return null;
        return GroupOf(key);
    }

    void ShowOverlay(string? label)
    {
        var size = Root.Node?.ViewBounds.Size ?? default;
        var center = LocalBounds(Center);
        _look = new DockLook(
            Shown: true,
            Label: label,
            LabelAt: default,
            Edges: new Rect(
                RootMargin,
                RootMargin,
                size.Width - RootMargin * 2f,
                size.Height - RootMargin * 2f
            ),
            CenterBox: DockLook.Within(center, 0.5f, 0.5f, DockLook.RootSize),
            CenterShown: CenterTakesDrops(),
            RootLit: -1,
            Compass: default,
            TabsOnly: false,
            CompassLit: -1,
            RootPreview: default,
            GroupPreview: default
        );
        _hover = null;
        _rootHit = null;
        _compassHit = null;
    }

    bool CenterTakesDrops() =>
        Manager is { } manager
        && (Host.Props.CenterDockable ?? true)
        && !Dock.HasContent(manager.CenterGroup);

    void MoveLabel(Vector2 local) =>
        _look = _look with { LabelAt = new Vector2(local.X + LabelOffset, local.Y + LabelOffset) };

    void Publish()
    {
        if (Look is { } look)
            look.Value = _look;
    }

    void ClearTargets()
    {
        _rootHit = null;
        _compassHit = null;
        _hover = null;
        _look = _look with
        {
            RootLit = -1,
            Compass = default,
            CompassLit = -1,
            RootPreview = default,
            GroupPreview = default,
        };
    }

    void UpdateTargets(Vector2 local)
    {
        _rootHit = null;
        _compassHit = null;
        var rootHit = -1;
        for (var i = 0; i < 5 && rootHit < 0; i++)
        {
            if (_look.RootShown(i) && _look.RootBox(i).Contains(local))
                rootHit = i;
        }
        if (rootHit >= 0)
        {
            _rootHit = RootPositions[rootHit];
            _hover = null;
            var area =
                rootHit == CenterIndex
                    ? LocalBounds(Center)
                    : new Rect(Root.Node?.ViewBounds.Size ?? default);
            _look = _look with
            {
                RootLit = rootHit,
                RootPreview = Slice(area, Directions[rootHit], 0.25f),
                Compass = default,
                CompassLit = -1,
                GroupPreview = default,
            };
            return;
        }

        var hover = HoveredGroup(local);
        _hover = hover;
        if (hover is null)
        {
            _look = _look with
            {
                RootLit = -1,
                RootPreview = default,
                Compass = default,
                CompassLit = -1,
                GroupPreview = default,
            };
            return;
        }

        var bounds = LocalBounds(hover.Ref);
        var tabsOnly = hover.Group is { Position: DockPosition.Floating };
        var looked = _look with
        {
            RootLit = -1,
            RootPreview = default,
            Compass = bounds,
            TabsOnly = tabsOnly,
        };
        var compassHit = -1;
        for (var i = 0; i < 5 && compassHit < 0; i++)
        {
            if (looked.CompassShown(i) && looked.CompassBox(i).Contains(local))
                compassHit = i;
        }
        if (compassHit >= 0)
            _compassHit = Directions[compassHit];
        _look = looked with
        {
            CompassLit = compassHit,
            GroupPreview = compassHit >= 0 ? Slice(bounds, Directions[compassHit], 0.5f) : default,
        };
    }

    TabGroupEntry? HoveredGroup(Vector2 local)
    {
        if (Manager is not { } manager)
            return null;
        var floats = manager.FloatingWindows;
        for (var i = floats.Count - 1; i >= 0; i--)
        {
            if (IsTornOut(floats[i]))
                continue;
            if (EntryOf(floats[i].Group) is { } entry && Covers(entry, local))
                return entry;
        }
        foreach (var entry in Groups)
        {
            if (entry.Group is { Position: not DockPosition.Floating } && Covers(entry, local))
                return entry;
        }
        return null;
    }

    bool Covers(TabGroupEntry entry, Vector2 local) =>
        entry.Ref.Node is { ViewBounds.Width: > 0f } node
        && InView(node)
        && LocalBounds(entry.Ref).Contains(local);

    bool InView(Node node)
    {
        var root = View?.Root;
        for (Node? at = node; at is not null; at = at.Parent)
        {
            if (ReferenceEquals(at, root))
                return true;
        }
        return false;
    }

    static Rect Slice(Rect area, DockDirection side, float fraction)
    {
        var width = area.Width * fraction;
        var height = area.Height * fraction;
        return side switch
        {
            DockDirection.Left => new Rect(area.X, area.Y, width, area.Height),
            DockDirection.Right => new Rect(
                area.X + area.Width - width,
                area.Y,
                width,
                area.Height
            ),
            DockDirection.Top => new Rect(area.X, area.Y, area.Width, height),
            DockDirection.Bottom => new Rect(
                area.X,
                area.Y + area.Height - height,
                area.Width,
                height
            ),
            _ => area,
        };
    }

    void DropOn(bool byTitle, DockElement element, Vector2 local)
    {
        if (Manager is not { } manager)
            return;

        DockElement[] tabs = byTitle ? [.. element.Parent!.Elements] : [element];
        var first = tabs[0];
        int? gap = null;

        if (_rootHit is { } root)
        {
            manager.Dock(first, root);
        }
        else if (_stripGroup is { } stripGroup)
        {
            if (ReferenceEquals(first.Parent, stripGroup))
            {
                element.Activate();
                return;
            }

            gap = _stripGap;
            manager.DockToGroup(first, stripGroup, gap);
        }
        else if (_compassHit is { } direction && _hover?.Group is { } target)
        {
            // Splitting a group against its only element would leave an empty side.
            if (
                ReferenceEquals(first.Parent, target)
                && (direction == DockDirection.Center || target.Elements.Count <= 1)
            )
                return;

            if (direction == DockDirection.Center)
                manager.DockToGroup(first, target);
            else
                manager.DockBySplit(first, target, direction);
        }
        else
        {
            if (
                byTitle
                || element.Parent is { Position: DockPosition.Floating, Elements.Count: <= 1 }
            )
                return;

            manager.Float(element, local.X, local.Y);
            element.Activate();
            return;
        }

        var landed = first.Parent!;
        for (var i = 1; i < tabs.Length; i++)
            manager.DockToGroup(tabs[i], landed, gap + i);
        element.Activate();
    }

    void EndDrag()
    {
        Strips.Hint(null, 0);
        _rootHit = null;
        _compassHit = null;
        _hover = null;
        _look = default;
        Publish();
        _dragging = false;
        _stripGroup = null;
        _crossSource = null;
    }

    internal bool IsTornOut(FloatingWindow window) => _tornOut.ContainsKey(window);

    void TearOut(bool byTitle, DockElement element, Vector2 cursor)
    {
        if (Manager is not { } manager || Host.Props.WindowHost is not { } host)
            return;

        var whole = byTitle || element.Parent?.Elements.Count == 1;
        var floating = (whole ? element.Parent?.Window : null) ?? manager.Float(element, 0f, 0f);
        var grab = _grab == default ? new Vector2(TearOutInsetX, TearOutInsetY) : _grab;
        var toDesktop = (View?.Scale ?? 1f) / host.MainWindowDensity;
        var torn = new TornOut(this, floating, grab);
        _tornOut[floating] = torn;
        Torn.Value++;
        torn.Open(host, cursor - grab * toDesktop);
        element.Activate();
    }

    internal void Untear(FloatingWindow window)
    {
        if (!_tornOut.Remove(window, out var torn))
            return;
        torn.Dispose();
        Torn.Value++;
    }

    void CloseTornOut()
    {
        if (_tornOut.Count == 0)
            return;
        foreach (var torn in new List<TornOut>(_tornOut.Values))
            torn.Dispose();
        _tornOut.Clear();
        Torn.Value++;
    }

    internal void TornOutClosed(FloatingWindow window)
    {
        window.RequestClose();
        Untear(window);
    }

    Vector2 MainLocal(Vector2 desktop)
    {
        var host = Host.Props.WindowHost!;
        var bounds = host.MainWindowBounds;
        var viewPoint =
            (desktop - new Vector2(bounds.X, bounds.Y))
            * host.MainWindowDensity
            / (View?.Scale ?? 1f);
        return Local(viewPoint);
    }

    internal void CrossStarted(TornOut source)
    {
        if (_dragging)
            return;

        _dragging = true;
        _crossSource = source;
        _grab = default;
        ShowOverlay(null);
        Publish();
    }

    internal void CrossMoved(TornOut source, bool byTitle, bool overStrip)
    {
        if (
            !_dragging
            || !ReferenceEquals(_crossSource, source)
            || Host.Props.WindowHost is not { } host
        )
            return;

        var cursor = host.CursorPosition;
        if (byTitle)
            source.MoveTo(cursor, host.MainWindowDensity);
        if (!overStrip && host.MainWindowBounds.Contains(cursor))
            UpdateTargets(MainLocal(cursor));
        else
            ClearTargets();
        Publish();
    }

    internal void CrossDropped(TornOut source, bool byTitle, DockElement element)
    {
        if (!_dragging || !ReferenceEquals(_crossSource, source))
            return;

        if (_rootHit is not null || _compassHit is not null)
        {
            DropOn(byTitle, element, default);
            return;
        }

        if (
            byTitle
            || element.Parent is not { Elements.Count: > 1 }
            || Host.Props.WindowHost is not { } host
        )
            return;

        var cursor = host.CursorPosition;
        if (host.MainWindowBounds.Contains(cursor))
            DropOn(byTitle, element, MainLocal(cursor));
        else
            TearOut(byTitle, element, cursor);
    }

    internal void CrossEnded(TornOut source)
    {
        if (ReferenceEquals(_crossSource, source))
            EndDrag();
    }

    sealed class Edges(int index)
    {
        public static readonly Edges[] Of = [new(0), new(1), new(2), new(3)];

        public int Index { get; } = index;
    }
}

internal static class DockHandlers
{
    [ColdPath]
    public static EventHandler<FloatingWindowEventArgs> Untearing(DockModel model) =>
        (_, e) => model.Untear(e.Window);

    [ColdPath]
    public static Action Closing(TornOut torn) => () => torn.Closed();
}

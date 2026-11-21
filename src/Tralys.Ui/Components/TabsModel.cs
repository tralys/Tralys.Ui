using System.Numerics;

namespace Tralys.Ui.Components;

[FramePath]
internal sealed class TabsModel : IDisposable
{
    readonly List<TabEntry> _tabs = [];
    internal readonly Handler<KeyEvent> KeyDown;
    internal TabsProps Props;
    internal readonly State<UiKey> Own;
    internal readonly State<int> Version;
    internal readonly State<TabSeam> Seam;
    internal readonly NodeRef Root;
    internal readonly NodeRef List;
    internal UiView? View;
    internal int Losses;
    UiKey _selected;
    UiKey _lost;
    TabDragGroup? _group;
    TabEntry? _dragged;
    int _reorderedFrom = -1;

    public TabsModel(
        State<UiKey> own,
        State<int> version,
        State<TabSeam> seam,
        NodeRef root,
        NodeRef list
    )
    {
        Own = own;
        Version = version;
        Seam = seam;
        Root = root;
        List = list;
        KeyDown = Handler<KeyEvent>.Of(this, static (m, ref e) => m.Pressed(ref e));
    }

    internal TabActivation Activation => Props.ActivationMode;

    internal void Register(TabEntry tab)
    {
        _tabs.Add(tab);
        Version.Value++;
    }

    internal void Unregister(TabEntry tab)
    {
        var at = IndexOf(tab);
        if (at < 0)
            return;
        _tabs.RemoveAt(at);
        if (ReferenceEquals(_dragged, tab))
            DragEnded(tab);
        if (tab.Value.IsKeyed && (tab.Value == _selected || tab.Value == _lost))
        {
            var next = _tabs.Count > 0 ? Step(Math.Min(at, _tabs.Count - 1), -1, wraps: true) : -1;
            _lost = next >= 0 ? _tabs[next].Value : default;
            Losses++;
        }
        Version.Value++;
    }

    internal UiKey ResolveNone()
    {
        Order();
        _lost = default;
        return _selected = default;
    }

    internal UiKey Resolve(UiKey value)
    {
        Order();
        if (value.IsKeyed && IndexOf(value) >= 0)
        {
            _lost = default;
            return _selected = value;
        }
        if (_lost.IsKeyed && IndexOf(_lost) is >= 0 and var lost && Eligible(_tabs[lost]))
            return _selected = _lost;
        var first = Step(0, 1, wraps: false);
        return _selected = first >= 0 ? _tabs[first].Value : value;
    }

    internal void ReportLost()
    {
        var current = Props.Value ?? Own.Peek;
        if (_lost.IsKeyed && !(current.IsKeyed && IndexOf(current) >= 0))
            Report(_lost);
    }

    internal void Select(UiKey value)
    {
        if (value.IsKeyed && value != _selected)
            Report(value);
    }

    void Report(UiKey value)
    {
        if (Props.Value is null)
            Own.Value = value;
        Props.OnValueChange.Invoke(value);
    }

    internal void Activate(TabEntry tab, bool select)
    {
        if (tab.Ref is not { } target)
            return;
        target.Focus(visible: true);
        target.ScrollIntoView();
        if (select)
            Select(tab.Value);
    }

    internal void TabKey(TabEntry tab, ref KeyEvent e)
    {
        if ((e.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Gui)) != 0)
            return;
        Order();
        var at = IndexOf(tab);
        var next = e.Key switch
        {
            Key.Left or Key.Up => Step(at - 1, -1, wraps: true),
            Key.Right or Key.Down => Step(at + 1, 1, wraps: true),
            Key.Home => Step(0, 1, wraps: false),
            Key.End => Step(_tabs.Count - 1, -1, wraps: false),
            _ => -2,
        };
        if (next == -2)
            return;
        e.PreventDefault();
        e.StopPropagation();
        if (next >= 0)
            Activate(_tabs[next], Activation == TabActivation.Automatic);
    }

    void Pressed(ref KeyEvent e)
    {
        var modifiers = e.Modifiers;
        if ((modifiers & (ModifierKeys.Alt | ModifierKeys.Gui)) != 0)
            return;
        Order();
        int next;
        if (e.Key == Key.Tab && (modifiers & ModifierKeys.Control) != 0)
        {
            var back = (modifiers & ModifierKeys.Shift) != 0;
            var step = back ? -1 : 1;
            var at = IndexOf(_selected);
            next =
                at >= 0
                    ? Step(at + step, step, wraps: true)
                    : Step(back ? _tabs.Count - 1 : 0, step, wraps: true);
            e.PreventDefault();
            e.StopPropagation();
            if (next >= 0)
                Activate(_tabs[next], select: true);
            return;
        }
        if ((modifiers & ModifierKeys.Control) != 0 || e.Key is not (Key.Home or Key.End))
            return;
        next =
            e.Key == Key.Home ? Step(0, 1, wraps: false) : Step(_tabs.Count - 1, -1, wraps: false);
        e.PreventDefault();
        e.StopPropagation();
        if (next >= 0)
            Select(_tabs[next].Value);
    }

    internal void Dragged(TabEntry tab, ref DragEvent e)
    {
        switch (e.Phase)
        {
            case DragPhase.Start:
                if (e.Pointer.Kind != PointerKind.Mouse && !e.IsHeld)
                    e.PreventDefault();
                else
                {
                    _dragged = tab;
                    _reorderedFrom = -1;
                }
                return;
            case DragPhase.Move when ReferenceEquals(_dragged, tab):
                DragTo(tab, tab.PressedAt + e.Offset);
                return;
            case DragPhase.End when ReferenceEquals(_dragged, tab):
                Drop(tab, tab.PressedAt + e.Offset);
                return;
            case DragPhase.Cancel when ReferenceEquals(_dragged, tab):
                DragEnded(tab);
                return;
        }
    }

    void DragEnded(TabEntry tab)
    {
        _dragged = null;
        _group?.Land(null, 0);
        Props.OnTabDragEnd.Invoke(tab.Value);
    }

    void DragTo(TabEntry tab, Vector2 point)
    {
        var target = Target(point, out var gap);
        if (ReferenceEquals(target, this))
        {
            _group?.Land(null, 0);
            if (Props.Reorderable)
                ReorderTo(tab, gap);
        }
        else
            _group?.Land(target, gap);
        Props.OnTabDrag.Invoke(new TabDrag(tab.Value, point, target?.Props.GroupKey));
    }

    void Drop(TabEntry tab, Vector2 point)
    {
        var target = Target(point, out var gap);
        _dragged = null;
        _group?.Land(null, 0);
        if (target is null)
            Props.OnTabTearOff.Invoke(new TabTearOff(tab.Value, point));
        else if (!ReferenceEquals(target, this))
            Props.OnTabMove.Invoke(new TabMove(tab.Value, target.Props.GroupKey, gap));
        Props.OnTabDragEnd.Invoke(tab.Value);
    }

    // A reorder waits for the app to move the tab, so moves before that layout don't reorder against stale places.
    void ReorderTo(TabEntry tab, int gap)
    {
        Order();
        var from = IndexOf(tab);
        if (from < 0 || from == _reorderedFrom)
            return;
        _reorderedFrom = -1;
        var to = gap > from ? gap - 1 : gap;
        if (to == from)
            return;
        _reorderedFrom = from;
        Props.OnReorder.Invoke(new TabReorder(tab.Value, from, to));
    }

    TabsModel? Target(Vector2 point, out int gap)
    {
        if (_group is { } group && View is { } view)
            return group.Find(view, point, null, out gap);
        var covers = Covers(point);
        gap = covers ? GapAt(point.X) : 0;
        return covers ? this : null;
    }

    internal bool Covers(Vector2 point)
    {
        if (Root.Node is not { } root)
            return false;
        var across = root.ViewBounds;
        if (List.Node is not { ViewBounds.Height: > 0f } list)
            return across.Contains(point);
        var row = list.ViewBounds;
        return new Rect(across.X, row.Y, across.Width, row.Height).Contains(point);
    }

    internal int GapAt(float x)
    {
        Order();
        var gap = 0;
        for (var i = 0; i < _tabs.Count; i++)
        {
            if (!LaysOut(_tabs[i], out var bounds))
                continue;
            if (x < bounds.X + bounds.Width / 2f)
                break;
            gap = i + 1;
        }
        return gap;
    }

    internal void ShowSeam(int gap)
    {
        if (Root.Node is not { } root)
            return;
        var origin = root.ViewBounds;
        if (List.Node is not { ViewBounds.Height: > 0f } list)
        {
            var start = List.Node?.ViewBounds.X ?? origin.X;
            Seam.Value = new TabSeam(true, start - origin.X, 0f, origin.Height);
            return;
        }
        Order();
        var x = float.NaN;
        for (var i = Math.Max(gap, 0); i < _tabs.Count && float.IsNaN(x); i++)
        {
            if (LaysOut(_tabs[i], out var bounds))
                x = bounds.X;
        }
        for (var i = Math.Min(gap, _tabs.Count) - 1; i >= 0 && float.IsNaN(x); i--)
        {
            if (LaysOut(_tabs[i], out var bounds))
                x = bounds.X + bounds.Width;
        }
        if (float.IsNaN(x))
        {
            HideSeam();
            return;
        }
        var row = list.ViewBounds;
        Seam.Value = new TabSeam(true, x - origin.X, row.Y - origin.Y, row.Height);
    }

    internal void HideSeam() => Seam.Value = default;

    internal void Join(TabDragGroup? group)
    {
        if (ReferenceEquals(group, _group))
            return;
        _group?.Leave(this);
        _group = group;
        group?.Join(this);
    }

    internal bool IsShownIn(UiView view)
    {
        if (!ReferenceEquals(View, view) || Root.Node is not { } root)
            return false;
        for (var node = root; node is not null; node = node.Parent)
        {
            if (node.Placement.Hidden || node.Placement.Collapsed)
                return false;
            if (ReferenceEquals(node, view.Root))
                return true;
        }
        return false;
    }

    int Step(int from, int step, bool wraps)
    {
        var count = _tabs.Count;
        var at = from;
        for (var n = 0; n < count; n++, at += step)
        {
            if ((uint)at >= (uint)count)
            {
                if (!wraps)
                    return -1;
                at = (at % count + count) % count;
            }
            if (Eligible(_tabs[at]))
                return at;
        }
        return -1;
    }

    bool Eligible(TabEntry tab)
    {
        if (tab.Ref?.Node is not { Parent: not null } node)
            return false;
        if ((node.States & InputStates.Disabled) != 0)
            return false;
        for (var at = node; at is not null && !ReferenceEquals(at, Root.Node); at = at.Parent)
        {
            if (at.Placement.Hidden || at.Placement.Collapsed)
                return false;
        }
        return true;
    }

    bool LaysOut(TabEntry tab, out Rect bounds)
    {
        bounds = default;
        if (tab.Ref?.Node is not { Parent: not null } node)
            return false;
        for (var at = node; at is not null && !ReferenceEquals(at, Root.Node); at = at.Parent)
        {
            if (at.Placement.Collapsed)
                return false;
        }
        bounds = node.ViewBounds;
        return true;
    }

    int IndexOf(TabEntry tab)
    {
        for (var i = 0; i < _tabs.Count; i++)
        {
            if (ReferenceEquals(_tabs[i], tab))
                return i;
        }
        return -1;
    }

    int IndexOf(UiKey value)
    {
        for (var i = 0; i < _tabs.Count; i++)
        {
            if (_tabs[i].Value == value)
                return i;
        }
        return -1;
    }

    // Insertion sort by tree order: the tabs are in order but for the few that moved or mounted since.
    void Order()
    {
        for (var i = 0; i < _tabs.Count; i++)
        {
            if (_tabs[i].Ref?.Node is not { Parent: not null })
                return;
        }
        for (var i = 1; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            var j = i - 1;
            while (j >= 0 && TreeOrder.Before(tab.Ref?.Node, _tabs[j].Ref?.Node))
            {
                _tabs[j + 1] = _tabs[j];
                j--;
            }
            _tabs[j + 1] = tab;
        }
    }

    public void Dispose()
    {
        if (_dragged is { } dragged)
            DragEnded(dragged);
        Join(null);
        _tabs.Clear();
    }
}

[FramePath]
internal sealed class TabEntry : IDisposable
{
    readonly TabsModel? _model;
    internal readonly Callback Click;
    internal readonly Handler<PointerEvent> PointerDown;
    internal readonly Handler<KeyEvent> KeyDown;
    internal readonly Callback<FocusChange> FocusChange;
    internal readonly Handler<DragEvent> Drag;
    internal UiKey Value;
    internal NodeRef? Ref;
    internal Vector2 PressedAt;
    bool _selectedOnPress;

    public TabEntry(TabsModel? model)
    {
        _model = model;
        Click = Callback.Of(this, static t => t.Clicked());
        PointerDown = Handler<PointerEvent>.Of(this, static (t, ref e) => t.Pressed(ref e));
        KeyDown = Handler<KeyEvent>.Of(this, static (t, ref e) => t.Keyed(ref e));
        FocusChange = Callback<FocusChange>.Of(this, static (t, f) => t.Focused(f));
        Drag = Handler<DragEvent>.Of(this, static (t, ref e) => t._model?.Dragged(t, ref e));
        model?.Register(this);
    }

    // A left press selects as it lands, so the click its release makes selects nothing more.
    void Clicked()
    {
        if (_selectedOnPress)
        {
            _selectedOnPress = false;
            return;
        }
        _model?.Select(Value);
    }

    void Pressed(ref PointerEvent e)
    {
        _selectedOnPress = false;
        if (e.Pointer.Kind == PointerKind.Mouse && e.Button != MouseButton.Left)
            return;
        PressedAt = e.ViewPosition;
        if (e.Pointer.Kind != PointerKind.Mouse)
            return;
        _selectedOnPress = true;
        _model?.Select(Value);
    }

    void Keyed(ref KeyEvent e)
    {
        _selectedOnPress = false;
        _model?.TabKey(this, ref e);
    }

    void Focused(FocusChange change)
    {
        if (
            change.Focused
            && change.Cause is FocusCause.Tab or FocusCause.Arrow
            && _model is { Activation: TabActivation.Automatic } model
        )
            model.Select(Value);
    }

    public void Dispose() => _model?.Unregister(this);
}

[FramePath]
internal static class TreeOrder
{
    public static bool Before(Node? a, Node? b)
    {
        if (a is null || b is null)
            return false;
        var depthA = Depth(a);
        var depthB = Depth(b);
        var x = a;
        var y = b;
        for (; depthA > depthB && x.Parent is { } up; depthA--)
            x = up;
        for (; depthB > depthA && y.Parent is { } up; depthB--)
            y = up;
        if (ReferenceEquals(x, y))
            return ReferenceEquals(a, x) && !ReferenceEquals(a, b);
        while (!ReferenceEquals(x.Parent, y.Parent))
        {
            if (x.Parent is null || y.Parent is null)
                return false;
            x = x.Parent;
            y = y.Parent;
        }
        if (x.Parent is not { } parent)
            return false;
        foreach (var child in parent.Children)
        {
            if (ReferenceEquals(child, x))
                return true;
            if (ReferenceEquals(child, y))
                return false;
        }
        return false;
    }

    static int Depth(Node node)
    {
        var depth = 0;
        for (var at = node.Parent; at is not null; at = at.Parent)
            depth++;
        return depth;
    }
}

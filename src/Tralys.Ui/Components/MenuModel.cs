using System.Diagnostics.CodeAnalysis;

namespace Tralys.Ui.Components;

[FramePath]
internal sealed class MenuLevel : IDisposable
{
    static readonly TimeSpan HoverDelay = TimeSpan.FromMilliseconds(400);

    readonly List<MenuEntry> _rows = [];
    internal readonly Handler<KeyEvent> KeyDown;
    internal readonly Callback CloseAll;
    internal MenuLevel? Parent;
    internal MenuEntry? Header;
    internal Callback OnClose;
    internal bool IsBar;
    internal State<MenuEntry?>? OpenRow;
    internal State<bool>? Reserve;
    internal NodeRef? Panel;
    internal UiView? View;
    internal bool FocusFirst;
    internal bool TakesFocus;
    internal State<int>? Closed;
    MenuEntry? _hovered;
    MenuEntry? _pendingHeader;
    bool _pendingVisible;
    TimeSpan _due;
    bool _ticking;
    bool _disposed;

    MenuLevel()
    {
        KeyDown = Handler<KeyEvent>.Of(this, static (l, ref e) => l.Pressed(ref e));
        CloseAll = Callback.Of(this, static l => l.Root.Close());
    }

    internal static MenuLevel Rent()
    {
        var level = Pool<MenuLevel>.TryRent() ?? new MenuLevel();
        level._disposed = false;
        return level;
    }

    MenuLevel Root
    {
        get
        {
            var level = this;
            while (level.Parent is { } parent)
                level = parent;
            return level;
        }
    }

    [MemberNotNullWhen(true, nameof(Parent))]
    bool InDropdown => Parent is { IsBar: true };

    internal void Bind(MenuLevel? parent, MenuEntry? header, Callback onClose, bool isBar)
    {
        Parent = parent;
        OnClose = onClose;
        IsBar = isBar;
        if (!ReferenceEquals(Header, header))
        {
            if (Header is { } old && ReferenceEquals(old.Child, this))
                old.Child = null;
            Header = header;
        }
        if (header is not null)
            header.Child = this;
    }

    internal void Register(MenuEntry entry) => _rows.Add(entry);

    internal void Unregister(MenuEntry entry)
    {
        _rows.Remove(entry);
        if (_disposed)
            return;
        if (ReferenceEquals(_hovered, entry))
            _hovered = null;
        if (ReferenceEquals(_pendingHeader, entry))
            _pendingHeader = null;
        if (OpenRow is { } openRow && ReferenceEquals(openRow.Peek, entry))
            openRow.Value = null;
        RefreshReserve();
    }

    internal void RefreshReserve()
    {
        if (_disposed || Reserve is not { } reserve)
            return;
        var any = false;
        for (var i = 0; i < _rows.Count && !any; i++)
            any = _rows[i].WantsIcon;
        reserve.Value = any;
    }

    internal bool IsOpen(MenuEntry entry) => ReferenceEquals(OpenRow?.Value, entry);

    internal bool IsOpenNow(MenuEntry entry) => ReferenceEquals(OpenRow?.Peek, entry);

    internal void SetOpen(MenuEntry? entry)
    {
        if (OpenRow is not { } openRow)
            return;
        var was = openRow.Peek;
        if (ReferenceEquals(was, entry))
            return;
        if (
            _hovered is { } hovered
            && (ReferenceEquals(hovered, was) || ReferenceEquals(hovered, entry))
        )
            _hovered = null;
        openRow.Value = entry;
    }

    internal void Open(MenuEntry entry, bool focusFirst)
    {
        if (!entry.HasItems)
            return;
        if (IsOpenNow(entry))
        {
            if (focusFirst)
                entry.Child?.FocusFirstRow();
            return;
        }
        entry.FocusFirstOnOpen = focusFirst;
        SetOpen(entry);
    }

    void Close()
    {
        if (IsBar)
            SetOpen(null);
        else
            OnClose.Invoke();
    }

    internal void Shown(UiView view)
    {
        View = view;
        if (FocusFirst)
            FocusFirstRow();
        else if (TakesFocus)
            Panel?.Focus();
    }

    // A header asked to take the focus while the dropdown holds it would wait for the dropdown to give it back.
    internal void CloseTo(MenuEntry? header, bool visible)
    {
        _pendingHeader = header;
        _pendingVisible = visible;
        SetOpen(null);
        if (Closed is { } closed)
            closed.Value++;
    }

    internal void DropdownClosed()
    {
        if (_pendingHeader is not { } header)
            return;
        _pendingHeader = null;
        header.Ref?.Focus(visible: _pendingVisible);
    }

    internal void FocusFirstRow()
    {
        Order();
        Focus(Step(-1, 1));
    }

    static void Focus(MenuEntry? entry)
    {
        if (entry is null)
            return;
        entry.Ref?.Focus(visible: true);
        entry.Ref?.ScrollIntoView();
    }

    internal void Hovered(MenuEntry entry)
    {
        if (IsBar)
        {
            if (OpenRow?.Peek is { } open && !ReferenceEquals(open, entry))
                Switch(entry, focusFirst: false, keys: false);
            return;
        }
        entry.Ref?.Focus();
        if (View is not { } view)
            return;
        _hovered = entry;
        _due = view.Context.Now + HoverDelay;
        Tick(view);
    }

    void Tick(UiView view)
    {
        if (_ticking)
            return;
        _ticking = true;
        view.Context.Dispatcher.Post(this, static l => l.Ticked());
    }

    void Ticked()
    {
        _ticking = false;
        if (_disposed || _hovered is not { } entry || View is not { } view)
            return;
        if (view.Context.Now < _due)
        {
            Tick(view);
            return;
        }
        _hovered = null;
        if (
            entry.Ref?.Node is not { } node
            || (node.States & InputStates.Hovered) == 0
            || (node.States & InputStates.Disabled) != 0
        )
            return;
        entry.FocusFirstOnOpen = false;
        SetOpen(entry.HasItems ? entry : null);
    }

    internal void HeaderPressed(MenuEntry entry)
    {
        if (IsOpenNow(entry))
        {
            CloseTo(entry, visible: false);
            return;
        }
        Switch(entry, focusFirst: false, keys: false);
    }

    void Switch(MenuEntry entry, bool focusFirst, bool keys)
    {
        if (!entry.HasItems)
        {
            CloseTo(entry, visible: keys);
            return;
        }
        if (IsOpenNow(entry))
        {
            if (focusFirst)
                entry.Child?.FocusFirstRow();
            return;
        }
        entry.FocusFirstOnOpen = focusFirst;
        SetOpen(entry);
    }

    void Move(MenuEntry? from, int step)
    {
        Order();
        var at = IndexOf(from);
        if (Step(at, step) is not { } target)
            return;
        if (OpenRow?.Peek is not null)
            Switch(target, focusFirst: true, keys: true);
        else
            target.Ref?.Focus(visible: true);
    }

    void Pressed(ref KeyEvent e)
    {
        if ((e.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Gui)) != 0)
            return;
        Order();
        var focused = FocusedRow();
        if (IsBar)
        {
            if (focused is not null && BarKey(focused, e.Key))
                Take(ref e);
            return;
        }
        if (focused is null && !PanelFocused())
        {
            if (e.Key == Key.Right && InDropdown)
            {
                Parent.Move(Header, 1);
                Take(ref e);
            }
            return;
        }
        if (MenuKey(focused, e.Key))
            Take(ref e);
    }

    bool BarKey(MenuEntry focused, Key key)
    {
        switch (key)
        {
            case Key.Right or Key.Left:
                Move(focused, key == Key.Right ? 1 : -1);
                return true;
            case Key.Down or Key.Enter or Key.Space when focused.HasItems && Eligible(focused):
                Switch(focused, focusFirst: true, keys: true);
                return true;
            case Key.Down:
                return true;
            default:
                return false;
        }
    }

    bool MenuKey(MenuEntry? focused, Key key)
    {
        var at = focused is null ? -1 : IndexOf(focused);
        switch (key)
        {
            case Key.Down:
                Focus(Step(at, 1));
                return true;
            case Key.Up:
                Focus(Step(at < 0 ? _rows.Count : at, -1));
                return true;
            case Key.Home:
                Focus(Step(-1, 1));
                return true;
            case Key.End:
                Focus(Step(_rows.Count, -1));
                return true;
            case Key.Right
            or Key.Enter
            or Key.Space when focused is { HasItems: true } && Eligible(focused):
                Open(focused, focusFirst: true);
                return true;
            case Key.Right when InDropdown:
                Parent.Move(Header, 1);
                return true;
            case Key.Left when focused is not null && IsOpenNow(focused):
                SetOpen(null);
                return true;
            case Key.Left when InDropdown:
                Parent.Move(Header, -1);
                return true;
            case Key.Left when Parent is not null:
                Header?.Ref?.Focus(visible: true);
                Parent.SetOpen(null);
                return true;
            case Key.Escape when InDropdown && OpenRow?.Peek is null:
                Parent.CloseTo(Header, visible: true);
                return true;
            case Key.Left or Key.Right when Parent is null:
                return true;
            default:
                return false;
        }
    }

    static void Take(ref KeyEvent e)
    {
        e.PreventDefault();
        e.StopPropagation();
    }

    MenuEntry? FocusedRow()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Ref?.Node is { } node && (node.States & InputStates.Focused) != 0)
                return _rows[i];
        }
        return null;
    }

    bool PanelFocused() => Panel?.Node is { } panel && (panel.States & InputStates.Focused) != 0;

    MenuEntry? Step(int from, int step)
    {
        var count = _rows.Count;
        for (var n = 1; n <= count; n++)
        {
            var i = ((from + step * n) % count + count) % count;
            if (Eligible(_rows[i]))
                return _rows[i];
        }
        return null;
    }

    bool Eligible(MenuEntry entry)
    {
        if (entry.Ref?.Node is not { Parent: not null } node)
            return false;
        if ((node.States & InputStates.Disabled) != 0)
            return false;
        for (var at = node; at is not null && !ReferenceEquals(at, Panel?.Node); at = at.Parent)
        {
            if (at.Placement.Hidden || at.Placement.Collapsed)
                return false;
        }
        return true;
    }

    int IndexOf(MenuEntry? entry)
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            if (ReferenceEquals(_rows[i], entry))
                return i;
        }
        return -1;
    }

    void Order()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Ref?.Node is not { Parent: not null })
                return;
        }
        for (var i = 1; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var j = i - 1;
            while (j >= 0 && TreeOrder.Before(row.Ref?.Node, _rows[j].Ref?.Node))
            {
                _rows[j + 1] = _rows[j];
                j--;
            }
            _rows[j + 1] = row;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (Header is { } header && ReferenceEquals(header.Child, this))
            header.Child = null;
        _rows.Clear();
        _hovered = _pendingHeader = null;
        _ticking = false;
        Parent = null;
        Header = null;
        OnClose = default;
        IsBar = FocusFirst = TakesFocus = false;
        OpenRow = null;
        Reserve = null;
        Closed = null;
        Panel = null;
        View = null;
        Pool<MenuLevel>.Return(this);
    }
}

[FramePath]
internal sealed class MenuEntry : IDisposable
{
    MenuLevel? _level;
    bool _disposed;
    internal readonly Callback Click;
    internal readonly Callback CloseSubmenu;
    internal readonly Handler<PointerEvent> PointerMove;
    internal readonly Callback<bool> HoverChange;
    internal readonly Handler<PointerEvent> HeaderPress;
    internal Host<MenuItemProps> Host;
    internal NodeRef? Ref;
    internal MenuLevel? Child;
    internal bool FocusFirstOnOpen;
    internal bool HasItems;
    internal bool WantsIcon;
    bool _rested;

    MenuEntry()
    {
        Click = Callback.Of(this, static m => m.Clicked());
        CloseSubmenu = Callback.Of(
            this,
            static m =>
            {
                if (m._level is { } level && level.IsOpenNow(m))
                    level.SetOpen(null);
            }
        );
        PointerMove = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Moved(ref e));
        HoverChange = Callback<bool>.Of(
            this,
            static (m, hovered) =>
            {
                if (!hovered)
                    m._rested = false;
            }
        );
        HeaderPress = Handler<PointerEvent>.Of(this, static (m, ref e) => m.HeaderPressed(ref e));
    }

    internal static MenuEntry Rent(MenuLevel? level)
    {
        var entry = Pool<MenuEntry>.TryRent() ?? new MenuEntry();
        entry._disposed = false;
        entry._level = level;
        level?.Register(entry);
        return entry;
    }

    internal bool IsOpen => _level is { } level && level.IsOpen(this);

    internal void Update(bool hasItems, bool wantsIcon)
    {
        HasItems = hasItems;
        if (!hasItems && _level is { } level && level.IsOpenNow(this))
            level.SetOpen(null);
        if (wantsIcon == WantsIcon)
            return;
        WantsIcon = wantsIcon;
        _level?.RefreshReserve();
    }

    void Clicked()
    {
        if (HasItems)
        {
            if (_level is { IsBar: false } level)
                level.Open(this, focusFirst: false);
            return;
        }
        Activate();
    }

    internal void Activate()
    {
        var p = Host.Props;
        if (!p.OnClick.CanRun)
            return;
        if (p.Checkable)
            p.OnCheckedChange.Invoke(!p.Checked);
        p.OnClick.Invoke();
        if (!p.StaysOpen)
            _level?.CloseAll.Invoke();
    }

    void Moved(ref PointerEvent e)
    {
        if (_rested || e.Pointer.Kind == PointerKind.Touch || _level is not { } level)
            return;
        _rested = true;
        level.Hovered(this);
    }

    void HeaderPressed(ref PointerEvent e)
    {
        if (e.Pointer.Kind == PointerKind.Mouse && e.Button != MouseButton.Left)
            return;
        e.PreventDefault();
        _level?.HeaderPressed(this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _level?.Unregister(this);
        _level = null;
        Host = default;
        Ref = null;
        Child = null;
        FocusFirstOnOpen = HasItems = WantsIcon = _rested = false;
        Pool<MenuEntry>.Return(this);
    }
}

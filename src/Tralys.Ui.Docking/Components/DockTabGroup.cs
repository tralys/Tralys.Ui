using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Docking;

internal readonly record struct DockTabGroupProps
{
    public DockGroup? Group { get; init; }
}

internal readonly record struct DockTabGroupContext(TabGroupEntry? Entry);

/// <summary>A leaf group's tabs over the selected one's panel: a press anywhere in it activates its active element,
/// and its tabs reorder, move to another strip, or drag out to dock elsewhere.</summary>
internal sealed class DockTabGroup : Component<DockTabGroupProps>
{
    internal static readonly ContextKey<DockTabGroupContext> Context = new("DockTabGroup", default);

    protected override void Render(Ui ui, in DockTabGroupProps p)
    {
        var c = ui.Use(Dock.Context);
        var groupRef = ui.Ref();
        var entry = ui.Remember(
            (c.Dock, groupRef),
            static d => new TabGroupEntry(d.Dock, d.groupRef)
        );
        entry.Group = p.Group;
        entry.Sink = c.Sink;
        ui.Provide(Context, new DockTabGroupContext(entry));
        if (p.Group is not { } group)
            return;
        var elements = group.Elements;
        if (elements.Count == 0)
            return;

        var active = group.ActiveElement;
        using (
            ui.Box(
                direction: Direction.Column,
                minWidth: 0f,
                minHeight: 0f,
                background: ui.Theme.Color(ColorToken.Bg),
                onPointerDown: entry.Press,
                nodeRef: entry.Ref
            )
        )
        using (
            ui.Tabs(
                value: active?.Id,
                onValueChange: entry.Select,
                reorderable: true,
                dragGroup: c.Dock?.Strips,
                groupKey: UiKey.Of(group),
                onReorder: entry.Reorder,
                onTabMove: entry.Move,
                onTabTearOff: entry.TearOff,
                onTabDrag: entry.Drag,
                onTabDragEnd: entry.DragEnd,
                grow: 1f
            )
        )
        {
            using (ui.TabsList())
            {
                for (var i = 0; i < elements.Count; i++)
                    ui.DockTab(element: elements[i], key: UiKey.Of(elements[i]));
            }
            if (active is not null)
                using (ui.TabsPanel(value: active.Id, key: UiKey.Of(active), grow: 1f))
                    ui.DockPanel(element: active, index: IndexOf(elements, active), grow: 1f);
        }
    }

    static int IndexOf(IReadOnlyList<DockElement> elements, DockElement element)
    {
        for (var i = 0; i < elements.Count; i++)
        {
            if (ReferenceEquals(elements[i], element))
                return i;
        }
        return -1;
    }
}

internal readonly record struct DockTabProps
{
    public DockElement? Element { get; init; }
}

/// <summary>A tab of a dock's group: its title, its unsaved-changes dot, a close button while hovered if it can close,
/// and a menu that closes it, the rest of its group, or all of its group.</summary>
internal sealed class DockTab : Component<DockTabProps>
{
    protected override void Render(Ui ui, in DockTabProps p)
    {
        var group = ui.Use(DockTabGroup.Context).Entry;
        var tab = ui.Remember(static () => new TabEntry());
        tab.Element = p.Element;
        tab.Group = group;
        if (p.Element is not { } element)
            return;
        using (ui.Box(onPointerDown: tab.Down))
        using (ui.ContextMenu(menu: tab.Menu))
            ui.TabsTab(value: element.Id, content: tab.Label);
    }
}

internal readonly record struct DockTabLabelProps
{
    public DockElement? Element { get; init; }

    /// <summary>Whether the tab is hovered, which shows its close button when its element can close.</summary>
    public bool Hovered { get; init; }

    public Callback OnClose { get; init; }
}

internal sealed class DockTabLabel : Component<DockTabLabelProps>
{
    protected override void Render(Ui ui, in DockTabLabelProps p)
    {
        if (p.Element is not { } element)
            return;
        var t = ui.Theme;
        var closes = p.Hovered && element.CanClose;
        using (ui.Grid())
        {
            ui.Text(
                element.Title,
                alignSelf: AlignSelf.Center,
                margin: new Thickness(10f, 5f, 20f, 5f)
            );
            if (element.IsDirty)
                using (
                    ui.Box(
                        width: 6f,
                        height: 6f,
                        radius: 3f,
                        background: t.Color(ColorToken.Fg),
                        margin: new Thickness(3f, 0f, 0f, 0f),
                        alignSelf: AlignSelf.Center,
                        justifySelf: AlignSelf.Start
                    )
                ) { }
            if (closes)
                ui.IconButton(
                    icon: IconGlyph.Of(IconRole.X),
                    size: ComponentSize.Xs,
                    scheme: ColorScheme.Secondary,
                    variant: ComponentVariant.Ghost,
                    onClick: p.OnClose,
                    margin: new Thickness(0f, 0f, 2f, 0f),
                    alignSelf: AlignSelf.Center,
                    justifySelf: AlignSelf.End
                );
        }
    }
}

internal readonly record struct DockPanelProps
{
    public DockElement? Element { get; init; }

    public int Index { get; init; }
}

/// <summary>The selected element's panel, drawn by the dock's content slot, inset when the element is a tool.</summary>
internal sealed class DockPanel : Component<DockPanelProps>
{
    protected override void Render(Ui ui, in DockPanelProps p)
    {
        var c = ui.Use(Dock.Context);
        if (p.Element is not { } element)
            return;
        using (
            ui.Box(
                direction: Direction.Column,
                minWidth: 0f,
                minHeight: 0f,
                padding: element.DockType == DockType.Tool ? 8f : 0f
            )
        )
            c.Content.Render(ui, element, p.Index);
    }
}

[FramePath]
internal sealed class TabGroupEntry : IDisposable
{
    internal readonly Callback<UiKey> Select;
    internal readonly Handler<PointerEvent> Press;
    internal readonly Callback<TabReorder> Reorder;
    internal readonly Callback<TabMove> Move;
    internal readonly Callback<TabTearOff> TearOff;
    internal readonly Callback<TabDrag> Drag;
    internal readonly Callback<UiKey> DragEnd;
    internal readonly NodeRef Ref;
    readonly DockModel? _dock;
    internal DockGroup? Group;
    internal IDockDragSink? Sink;
    internal DockElement? Pressed;
    bool _dragging;

    public TabGroupEntry(DockModel? dock, NodeRef groupRef)
    {
        _dock = dock;
        Ref = groupRef;
        dock?.Groups.Add(this);
        Select = Callback<UiKey>.Of(this, static (g, key) => g.Selected(key));
        Press = Handler<PointerEvent>.Of(this, static (g, ref e) => g.Pressing());
        Reorder = Callback<TabReorder>.Of(this, static (g, r) => g.Reordered(r));
        Move = Callback<TabMove>.Of(this, static (g, m) => g.Moved(m));
        TearOff = Callback<TabTearOff>.Of(this, static (g, t) => g.TornOff(t));
        Drag = Callback<TabDrag>.Of(this, static (g, d) => g.Dragged(d));
        DragEnd = Callback<UiKey>.Of(this, static (g, _) => g.EndDrag());
    }

    // A drop can unmount this group, and then the strip's end of the drag reaches no one.
    public void Dispose()
    {
        EndDrag();
        _dock?.Groups.Remove(this);
    }

    DockElement? Find(UiKey key)
    {
        if (Group is not { } group)
            return null;
        var elements = group.Elements;
        for (var i = 0; i < elements.Count; i++)
        {
            if (key == elements[i].Id)
                return elements[i];
        }
        return null;
    }

    void Selected(UiKey key)
    {
        if (Find(key) is { } element && !ReferenceEquals(Group?.ActiveElement, element))
            element.Activate();
    }

    void Pressing()
    {
        var pressed = Pressed;
        Pressed = null;
        if (pressed is null || ReferenceEquals(pressed, Group?.ActiveElement))
            Group?.ActiveElement?.Activate();
    }

    void Reordered(TabReorder reorder)
    {
        if (Find(reorder.Value) is { } element)
            Group?.MoveTo(element, reorder.To);
    }

    void Moved(TabMove move)
    {
        if (
            Find(move.Value) is not { } element
            || _dock?.GroupOf(move.Target) is not { } target
            || element.Manager is not { } manager
        )
            return;

        manager.DockToGroup(element, target, move.Gap);
        element.Activate();
    }

    void Dragged(TabDrag drag)
    {
        if (!_dragging)
        {
            if (Find(drag.Value) is not { } element)
                return;
            _dragging = true;
            Sink?.DragStarted(false, element, drag.Position, default);
        }
        Sink?.DragMoved(false, drag.Position, drag.Over is not null);
    }

    void TornOff(TabTearOff tearOff)
    {
        if (Find(tearOff.Value) is { } element)
            Sink?.Dropped(false, element, tearOff.Position);
    }

    void EndDrag()
    {
        if (!_dragging)
            return;

        _dragging = false;
        Sink?.DragEnded();
    }
}

[FramePath]
internal sealed class TabEntry
{
    internal readonly Slot Label;
    internal readonly Slot Menu;
    internal readonly Handler<PointerEvent> Down;
    readonly Callback _close;
    readonly Callback _closeOthers;
    readonly Callback _closeAll;
    internal DockElement? Element;
    internal TabGroupEntry? Group;

    public TabEntry()
    {
        _close = Callback.Of(this, static t => t.Element?.Close());
        _closeOthers = Callback.Of(this, static t => t.CloseWhere(t.Element));
        _closeAll = Callback.Of(this, static t => t.CloseWhere(null));
        Down = Handler<PointerEvent>.Of(this, static (t, ref e) => t.Group?.Pressed = t.Element);
        Label = Slot.Of(
            this,
            static (ui, t) =>
                ui.DockTabLabel(element: t.Element, hovered: ui.IsHovered, onClose: t._close)
        );
        Menu = Slot.Of(
            this,
            static (ui, t) =>
            {
                ui.MenuItem(
                    text: "Close",
                    disabled: t.Element?.CanClose != true,
                    onClick: t._close
                );
                ui.MenuItem(text: "Close Others", onClick: t._closeOthers);
                ui.MenuItem(text: "Close All", onClick: t._closeAll);
            }
        );
    }

    void CloseWhere(DockElement? spared)
    {
        if (Group?.Group is not { Manager: { } manager } group)
            return;

        var elements = group.Elements;
        var targets = new List<DockElement>(elements.Count);
        for (var i = 0; i < elements.Count; i++)
        {
            if (!ReferenceEquals(elements[i], spared))
                targets.Add(elements[i]);
        }
        manager.Close(targets);
    }
}

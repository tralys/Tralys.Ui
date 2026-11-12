using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct GroupedListProps<TGroup, TItem>
{
    /// <summary>The groups, each a header row followed by its items while it is open.</summary>
    public IItemsSource<TGroup>? Groups { get; init; }

    /// <summary>A group's identity, by which it stays folded as the groups are rebuilt.</summary>
    public KeyOf<TGroup>? GroupKey { get; init; }

    /// <summary>A group's items; a change to them reaches the list.</summary>
    public Func<TGroup, IItemsSource<TItem>?>? GroupItems { get; init; }

    /// <summary>What a header shows while <see cref="Header"/> is empty.</summary>
    public TextOf<TGroup>? GroupText { get; init; }

    public Slot<TGroup> Header { get; init; }

    public KeyOf<TItem>? ItemKey { get; init; }

    /// <summary>What an item's row shows while <see cref="Item"/> is empty.</summary>
    public TextOf<TItem>? ItemText { get; init; }

    public Slot<TItem> Item { get; init; }

    /// <summary>The selected item's key; never a group's.</summary>
    public UiKey Value { get; init; }

    public Callback<UiKey> OnValueChange { get; init; }

    /// <summary>Enter on the focused item, or a double click on one.</summary>
    public Callback<UiKey> OnItemActivate { get; init; }

    /// <summary>The folded groups' keys; null leaves the folds to the list.</summary>
    public ISelection? CollapsedGroups { get; init; }

    /// <summary>A group folding or unfolding, as a toggle of its key, which <c>SelectionSet.Apply</c> takes over
    /// <see cref="Groups"/>.</summary>
    public Callback<SelectionChange> OnCollapsedGroupsChange { get; init; }

    /// <summary>Builds every row, however many the viewport shows, as a short catalog does.</summary>
    public bool RealizeAll { get; init; }

    /// <summary>The rows' size; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A list of <see cref="GroupedListProps{TGroup, TItem}.Groups"/>, each a header row followed by its items, in
/// one virtualized list. <see cref="GroupedListProps{TGroup, TItem}.Value"/> is an item's key, which a press or an
/// arrow reaching an item reports through <see cref="GroupedListProps{TGroup, TItem}.OnValueChange"/>; a header never
/// selects.</summary>
/// <remarks>A press, Enter or Space on a header folds or unfolds its group, as Left on an open one and Right on a folded
/// one do, and its chevron turns with it. The folds are <see cref="GroupedListProps{TGroup, TItem}.CollapsedGroups"/>, or the
/// list's own while that is null, and each fold reports through
/// <see cref="GroupedListProps{TGroup, TItem}.OnCollapsedGroupsChange"/>. An item in two groups shows selected in the row a
/// press or an arrow reached, and a value set from outside scrolls its row into view.</remarks>
public sealed partial class GroupedList<TGroup, TItem> : Component<GroupedListProps<TGroup, TItem>>
{
    protected override void Render(Ui ui, in GroupedListProps<TGroup, TItem> p)
    {
        var groups = p.Groups ?? throw Missing(nameof(p.Groups));
        var groupKey = p.GroupKey ?? throw Missing(nameof(p.GroupKey));
        var groupItems = p.GroupItems ?? throw Missing(nameof(p.GroupItems));
        var itemKey = p.ItemKey ?? throw Missing(nameof(p.ItemKey));
        var model = ui.Remember(static () => new GroupedListModel<TGroup, TItem>());
        model.Host = ui.Self(p);
        model.Picked = ui.State(-1L);
        var folds = p.CollapsedGroups ?? model.OwnFolds;
        var rows = model.Rows;
        rows.Bind(ui.Builder.View, groups, groupKey, groupItems, itemKey, folds, folds.Version);
        rows.Select(p.Value, model.Picked.Value);
        var scroll = ui.ScrollHandle();
        if (p.Value != model.Revealed && rows.SelectedIndex >= 0)
        {
            model.Revealed = p.Value;
            scroll.ScrollToIndex(rows.SelectedIndex);
        }
        ui.Provide(
            GroupedListModel<TGroup, TItem>.Context,
            new GroupedRowsContext<TGroup, TItem>(
                p.Header,
                p.GroupText,
                p.Item,
                p.ItemText,
                p.Size ?? ComponentSize.Md
            )
        );
        using (
            ui.Box(
                direction: Direction.Column,
                width: p.Width,
                height: p.Height,
                onKeyDown: ListEnds.UpDown
            )
        )
        {
            ui.VirtualList(
                items: rows,
                itemKey: GroupedRows<TGroup, TItem>.ById,
                row: model.Row,
                padding: new Thickness(
                    0f,
                    0f,
                    ListBarParts.Space(scroll, ScrollbarVisibility.Auto),
                    0f
                ),
                scroll: scroll,
                realizeAll: p.RealizeAll,
                selectionMode: SelectionMode.Single,
                selection: model.Selection(),
                onSelectionChange: model.Pick,
                onAction: p.OnItemActivate.IsEmpty ? default : model.Activate,
                grow: 1f
            );
            ui.ListBar(scroll: scroll, visibility: ScrollbarVisibility.Auto);
        }
    }

    static ArgumentException Missing(string prop) => new($"A grouped list needs its {prop}.", prop);
}

internal readonly record struct GroupedRowsContext<TGroup, TItem>(
    Slot<TGroup> Header,
    TextOf<TGroup>? GroupText,
    Slot<TItem> Item,
    TextOf<TItem>? ItemText,
    ComponentSize Size
);

[FramePath]
internal sealed class GroupedListModel<TGroup, TItem> : IDisposable
{
    internal static readonly ContextKey<GroupedRowsContext<TGroup, TItem>> Context = new(
        "GroupedList"
    );

    internal readonly GroupedRows<TGroup, TItem> Rows = new();
    internal readonly KeySet OwnFolds = new();
    readonly SelectedRow<TGroup, TItem> _one;
    readonly SelectedRow<TGroup, TItem> _other;
    SelectedRow<TGroup, TItem> _shown;
    internal readonly Slot<GroupedEntry<TGroup, TItem>> Row;
    internal readonly Callback<SelectionChange> Pick;
    internal readonly Callback<UiKey> Activate;
    internal Host<GroupedListProps<TGroup, TItem>> Host;
    internal State<long> Picked = null!;
    internal UiKey Revealed;

    public GroupedListModel()
    {
        _one = new SelectedRow<TGroup, TItem>(Rows);
        _other = new SelectedRow<TGroup, TItem>(Rows);
        _shown = _one;
        Row = Slot<GroupedEntry<TGroup, TItem>>.Of(
            this,
            static (ui, m, entry, _) =>
            {
                // A row the list has yet to rebind, as one a closed collapsible holds, can outlive its entry.
                if (entry.Released)
                    return;
                var c = ui.Use(Context);
                if (entry.IsHeader)
                {
                    using (
                        ui.ListBoxRow(
                            size: c.Size,
                            onClick: Callback.Of(m, entry, static (m, e) => m.Toggle(e.GroupKey)),
                            onKeyDown: Handler<KeyEvent>.Of(
                                m,
                                entry,
                                static (m, e, ref k) => m.HeaderKey(e, ref k)
                            )
                        )
                    )
                    {
                        if (!c.Header.IsEmpty)
                            using (ui.Box(grow: 1f))
                                c.Header.Render(ui, entry.Group, 0);
                        else if (c.GroupText is { } text)
                            ui.Text(text(entry.Group), grow: 1f);
                        var turn = ui.Animate(entry.Open ? 1f : 0f);
                        using (
                            ui.Box(
                                margin: new Thickness(4f, 0f, 0f, 0f),
                                shrink: 0f,
                                transform: Transform.Rotate(turn * 90f)
                            )
                        )
                            ui.Icon(
                                glyph: IconGlyph.Of(IconRole.ChevronRight),
                                size: 14f,
                                color: ui.Theme.Color(ColorToken.Icon)
                            );
                    }
                    return;
                }
                using (ui.ListBoxRow(size: c.Size))
                {
                    if (!c.Item.IsEmpty)
                        c.Item.Render(ui, entry.Item, 0);
                    else if (c.ItemText is { } text)
                        ui.Text(text(entry.Item));
                }
            }
        );
        Pick = Callback<SelectionChange>.Of(this, static (m, change) => m.Picking(change));
        Activate = Callback<UiKey>.Of(this, static (m, key) => m.Activating(key));
    }

    ref readonly GroupedListProps<TGroup, TItem> Props => ref Host.Props;

    /// <summary>The selected row, live through rebuilds, and a new selection whenever it changes, so the list re-reads
    /// which rows are selected.</summary>
    internal ISelection Selection()
    {
        if (_shown.Id != Rows.SelectedId)
        {
            _shown = ReferenceEquals(_shown, _one) ? _other : _one;
            _shown.Id = Rows.SelectedId;
        }
        return _shown;
    }

    void Picking(in SelectionChange change)
    {
        if (
            change.Action != SelectionAction.Replace
            || Rows.Find(change.Key) is not { IsHeader: false } entry
        )
            return;
        Picked.Value = entry.Id;
        if (entry.ItemKey != Props.Value)
            Props.OnValueChange.Invoke(entry.ItemKey);
    }

    void Activating(UiKey key)
    {
        if (Rows.Find(key) is { IsHeader: false } entry)
            Props.OnItemActivate.Invoke(entry.ItemKey);
    }

    void Toggle(UiKey group)
    {
        if (Props.CollapsedGroups is null)
            OwnFolds.Toggle(group);
        Props.OnCollapsedGroupsChange.Invoke(
            new SelectionChange(SelectionAction.Toggle, group, group)
        );
    }

    void HeaderKey(GroupedEntry<TGroup, TItem> header, ref KeyEvent e)
    {
        if (e.Modifiers != ModifierKeys.None)
            return;
        switch (e.Key)
        {
            case Key.Left when header.Open:
            case Key.Right when !header.Open:
            case Key.Enter or Key.Space:
                Toggle(header.GroupKey);
                break;
            case Key.Left or Key.Right:
                break;
            default:
                return;
        }
        e.PreventDefault();
        e.StopPropagation();
    }

    public void Dispose() => Rows.Dispose();
}

[FramePath]
internal sealed class SelectedRow<TGroup, TItem>(GroupedRows<TGroup, TItem> rows) : ISelection
{
    internal long Id = -1;

    public int Version => 0;

    public bool Contains(UiKey key) => key.TryGetLong(out var id) && id == rows.SelectedId;
}

/// <summary>A row of a <see cref="GroupedList{TGroup, TItem}"/>: a group's header, or one of its items.</summary>
[FramePath]
internal sealed class GroupedEntry<TGroup, TItem>
{
    internal long Id;
    internal bool IsHeader;
    internal bool Open;
    internal bool Shown;
    internal int Seen;
    internal bool Released;
    internal UiKey GroupKey;
    internal UiKey ItemKey;
    internal TGroup Group = default!;
    internal TItem Item = default!;
}

/// <summary>The rows of a grouped list, flattened: each group's header, then its items while it is open. One entry per
/// group and item key outlives a rebuild, so a row keeps its identity, and a folded group keeps its items'.</summary>
[FramePath]
internal sealed class GroupedRows<TGroup, TItem>
    : IItemsSource<GroupedEntry<TGroup, TItem>>,
        IItemsSourceListener,
        IDisposable
{
    internal static readonly KeyOf<GroupedEntry<TGroup, TItem>> ById = static entry => entry.Id;

    readonly List<GroupedEntry<TGroup, TItem>> _rows = [];
    readonly Dictionary<
        (UiKey Group, UiKey Item, bool Header),
        GroupedEntry<TGroup, TItem>
    > _entries = [];
    readonly Dictionary<long, GroupedEntry<TGroup, TItem>> _byId = [];
    readonly Queue<(GroupedEntry<TGroup, TItem> Entry, int DroppedAt)> _dropped = new();
    readonly Stack<GroupedEntry<TGroup, TItem>> _free = new();
    List<IItemsSource> _held = [];
    List<IItemsSource> _holding = [];
    ItemsSourceListeners _listeners;
    IItemsSource<TGroup>? _groups;
    KeyOf<TGroup>? _groupKey;
    Func<TGroup, IItemsSource<TItem>?>? _groupItems;
    KeyOf<TItem>? _itemKey;
    ISelection? _folds;
    int _foldVersion;
    UiView? _view;
    long _nextId;
    int _stamp;
    UiKey _value;
    long _picked = -1;

    public long SelectedId { get; private set; } = -1;

    public int SelectedIndex { get; private set; } = -1;

    public int Count => _rows.Count;

    public object? this[int index] => _rows[index];

    public GroupedEntry<TGroup, TItem> ItemAt(int index) => _rows[index];

    public int IndexOf(object? item) =>
        item is GroupedEntry<TGroup, TItem> entry ? _rows.IndexOf(entry) : -1;

    public void AddListener(IItemsSourceListener listener) => _listeners.Add(listener);

    public void RemoveListener(IItemsSourceListener listener) => _listeners.Remove(listener);

    public GroupedEntry<TGroup, TItem>? Find(UiKey key) =>
        key.TryGetLong(out var id) && _byId.TryGetValue(id, out var entry) && entry.Shown
            ? entry
            : null;

    public void Bind(
        UiView view,
        IItemsSource<TGroup> groups,
        KeyOf<TGroup> groupKey,
        Func<TGroup, IItemsSource<TItem>?> groupItems,
        KeyOf<TItem> itemKey,
        ISelection folds,
        int foldVersion
    )
    {
        _view = view;
        if (
            ReferenceEquals(groups, _groups)
            && ReferenceEquals(groupKey, _groupKey)
            && ReferenceEquals(groupItems, _groupItems)
            && ReferenceEquals(itemKey, _itemKey)
            && ReferenceEquals(folds, _folds)
            && foldVersion == _foldVersion
        )
            return;
        if (!ReferenceEquals(groups, _groups))
        {
            _groups?.RemoveListener(this);
            groups.AddListener(this);
            _groups = groups;
        }
        _groupKey = groupKey;
        _groupItems = groupItems;
        _itemKey = itemKey;
        _folds = folds;
        _foldVersion = foldVersion;
        Rebuild();
    }

    public void Select(UiKey value, long picked)
    {
        if (value == _value && picked == _picked)
            return;
        _value = value;
        _picked = picked;
        Choose();
    }

    void Choose()
    {
        SelectedId = -1;
        SelectedIndex = -1;
        if (!_value.IsKeyed)
            return;
        if (
            _byId.TryGetValue(_picked, out var picked)
            && picked is { IsHeader: false, Shown: true }
            && picked.ItemKey == _value
        )
        {
            SelectedId = picked.Id;
            SelectedIndex = _rows.IndexOf(picked);
            return;
        }
        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] is { IsHeader: false } row && row.ItemKey == _value)
            {
                SelectedId = row.Id;
                SelectedIndex = i;
                return;
            }
        }
    }

    void Rebuild()
    {
        var groups = _groups!;
        var groupKey = _groupKey!;
        var itemKey = _itemKey!;
        _stamp++;
        _rows.Clear();
        _holding.Clear();
        for (var g = 0; g < groups.Count; g++)
        {
            var group = groups.ItemAt(g);
            var key = groupKey(group);
            var open = !_folds!.Contains(key);
            var header = Entry(key, default, header: true, group, default!, shown: true);
            header.Open = open;
            _rows.Add(header);
            if (_groupItems!(group) is not { } items)
                continue;
            if (!_holding.Contains(items))
                _holding.Add(items);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items.ItemAt(i);
                var entry = Entry(key, itemKey(item), header: false, group, item, shown: open);
                if (open)
                    _rows.Add(entry);
            }
        }
        for (var i = 0; i < _held.Count; i++)
        {
            if (!_holding.Contains(_held[i]))
                _held[i].RemoveListener(this);
        }
        for (var i = 0; i < _holding.Count; i++)
        {
            if (!_held.Contains(_holding[i]))
                _holding[i].AddListener(this);
        }
        (_held, _holding) = (_holding, _held);
        Release();
        foreach (var (key, entry) in _entries)
        {
            if (entry.Seen == _stamp)
                continue;
            _entries.Remove(key);
            _byId.Remove(entry.Id);
            if (_view is { } view)
                _dropped.Enqueue((entry, view.UpdateStamp));
        }
        Choose();
        _listeners.Raise(this, ItemsChange.Reset);
    }

    GroupedEntry<TGroup, TItem> Entry(
        UiKey group,
        UiKey key,
        bool header,
        TGroup owner,
        TItem item,
        bool shown
    )
    {
        if (!_entries.TryGetValue((group, key, header), out var entry))
        {
            entry = Reused() ?? new GroupedEntry<TGroup, TItem>();
            entry.Id = _nextId++;
            entry.IsHeader = header;
            entry.Open = false;
            entry.Released = false;
            entry.GroupKey = group;
            entry.ItemKey = key;
            _entries.Add((group, key, header), entry);
            _byId.Add(entry.Id, entry);
        }
        entry.Group = owner;
        entry.Item = item;
        entry.Seen = _stamp;
        entry.Shown = shown;
        return entry;
    }

    // A row the list has yet to rebind may still render a dropped entry in the update it was dropped in.
    bool Settled(int droppedAt) => _view is { } view && droppedAt + 1 < view.UpdateStamp;

    GroupedEntry<TGroup, TItem>? Reused()
    {
        Release();
        return _free.TryPop(out var entry) ? entry : null;
    }

    void Release()
    {
        while (_dropped.TryPeek(out var dropped) && Settled(dropped.DroppedAt))
        {
            _dropped.Dequeue();
            dropped.Entry.Group = default!;
            dropped.Entry.Item = default!;
            dropped.Entry.Released = true;
            _free.Push(dropped.Entry);
        }
    }

    void IItemsSourceListener.OnItemsChanged(IItemsSource source, in ItemsChange change)
    {
        if (_groups is not null)
            Rebuild();
    }

    public void Dispose()
    {
        _groups?.RemoveListener(this);
        _groups = null;
        for (var i = 0; i < _held.Count; i++)
            _held[i].RemoveListener(this);
        _held.Clear();
    }
}

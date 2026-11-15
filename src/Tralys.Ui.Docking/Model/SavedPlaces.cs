namespace Tralys.Ui.Docking;

internal sealed class SavedPlaces
{
    readonly List<SavedPlace> _places = [];
    readonly Dictionary<string, SavedPlace> _waiting = new(StringComparer.Ordinal);

    public void Remember(
        DockManager manager,
        DockPosition position,
        List<string> ids,
        string? activeId,
        SavedFloatingWindow? window = null,
        bool hidden = false
    )
    {
        SavedPlace? place = null;
        foreach (var id in ids)
        {
            if (manager.GetElement(id) is not null || _waiting.ContainsKey(id))
                continue;

            place ??= new SavedPlace(position, ids, activeId, window, hidden);
            _waiting[id] = place;
        }
        if (place is not null)
            _places.Add(place);
    }

    public void Place(DockManager manager, DockElement element)
    {
        if (!_waiting.Remove(element.Id, out var place))
            return;

        if (place.Hidden)
        {
            if (place.Position.IsRootRegion())
                element.Position = place.Position;
            return;
        }

        if (HostOf(manager, place) is { } host)
        {
            var active = host.ActiveElement;
            host.Insert(element, SlotFor(place, element.Id, host.Elements, static e => e.Id));
            if (place.ActiveId != element.Id)
                host.ActiveElement = active;
            manager.RaiseLayoutChanged();
        }
        else if (place.Window is { } window)
            manager.Float(element, window.X, window.Y, window.Width, window.Height);
        else if (place.Position.IsRootRegion())
            manager.Dock(element, place.Position);
    }

    public void WriteBack(
        DockManager manager,
        SavedLayout layout,
        Dictionary<DockGroup, List<string>> tabs
    )
    {
        foreach (var place in _places)
        {
            var waiting = new List<string>();
            foreach (var id in place.Ids)
            {
                if (
                    _waiting.TryGetValue(id, out var waitingAt) && ReferenceEquals(waitingAt, place)
                )
                    waiting.Add(id);
            }
            if (waiting.Count == 0)
                continue;

            if (place.Hidden)
            {
                foreach (var id in waiting)
                    layout.HiddenElements.Add(
                        new SavedElementPosition { Id = id, Position = place.Position }
                    );
                continue;
            }

            var into =
                HostOf(manager, place) is { } host ? tabs.GetValueOrDefault(host)
                : place.Window is null && manager.RootGroup(place.Position) is { } root
                    ? tabs.GetValueOrDefault(FirstLeaf(root))
                : null;
            if (into is null)
            {
                var window = place.Window ?? new SavedFloatingWindow();
                layout.FloatingWindows.Add(
                    new SavedFloatingWindow
                    {
                        X = window.X,
                        Y = window.Y,
                        Width = window.Width,
                        Height = window.Height,
                        Elements = waiting,
                        ActiveElementId = waiting.Contains(place.ActiveId ?? "")
                            ? place.ActiveId
                            : null,
                    }
                );
                continue;
            }

            foreach (var id in waiting)
                into.Insert(SlotFor(place, id, into, static tab => tab), id);
        }
    }

    static DockGroup? HostOf(DockManager manager, SavedPlace place)
    {
        foreach (var id in place.Ids)
        {
            if (manager.GetElement(id)?.Parent is { } parent && parent.Position == place.Position)
                return parent;
        }
        return null;
    }

    static DockGroup FirstLeaf(DockGroup group)
    {
        while (group.IsSplitGroup)
            group = group.Children[0];
        return group;
    }

    static int SlotFor<T>(SavedPlace place, string id, IReadOnlyList<T> tabs, Func<T, string> idOf)
    {
        var at = place.Ids.IndexOf(id);
        var slot = 0;
        for (var i = 0; i < tabs.Count; i++)
        {
            var saved = place.Ids.IndexOf(idOf(tabs[i]));
            if (saved >= 0 && saved < at)
                slot = i + 1;
        }
        return slot;
    }

    sealed class SavedPlace(
        DockPosition position,
        List<string> ids,
        string? activeId,
        SavedFloatingWindow? window,
        bool hidden
    )
    {
        public DockPosition Position { get; } = position;

        public List<string> Ids { get; } = ids;

        public string? ActiveId { get; } = activeId;

        public SavedFloatingWindow? Window { get; } = window;

        public bool Hidden { get; } = hidden;
    }
}

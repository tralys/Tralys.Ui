using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Docking;

/// <summary>The model of a dock: its registered elements, the five root groups they dock into, its floating windows,
/// and the layout that saves and loads all of them. Its <see cref="ActiveElement"/>, <see cref="PanelSizes"/> and
/// <see cref="FloatingWindows"/> report their reads, so a component that read one renders again as it changes.</summary>
public sealed class DockManager : IObservable
{
    const int ActiveBit = 0;
    const int SizesBit = 1;
    const int FloatsBit = 2;

    private readonly ObservableList<DockElement> _elements = [];
    private readonly Dictionary<string, DockElement> _elementsById = [];
    private readonly List<FloatingWindow> _floatingWindows = [];
    private readonly DockGroup[] _roots;
    private readonly IReadOnlyList<FloatingWindow> _floatingView;
    private DockElement? _activeElement;
    private DockPanelSizes _panelSizes;
    private ObserverSet _observers;

    ref ObserverSet IObservable.Observers => ref _observers;

    /// <summary>Every registered element, in registration order. It reports each registration and removal, so a list
    /// over it follows it without allocating.</summary>
    public IReadOnlyObservableList<DockElement> Elements => _elements;

    /// <summary>The open floating windows, the last opened on top.</summary>
    public IReadOnlyList<FloatingWindow> FloatingWindows
    {
        get
        {
            Observation.Read(this, FloatsBit);
            return _floatingView;
        }
    }

    public DockElement? ActiveElement
    {
        get
        {
            Observation.Read(this, ActiveBit);
            return _activeElement;
        }
    }

    public DockGroup LeftGroup { get; }
    public DockGroup RightGroup { get; }
    public DockGroup TopGroup { get; }
    public DockGroup BottomGroup { get; }
    public DockGroup CenterGroup { get; }

    /// <summary>Saved and loaded with the layout, so splitter drags survive a restart.</summary>
    public DockPanelSizes PanelSizes
    {
        get
        {
            Observation.Read(this, SizesBit);
            return _panelSizes;
        }
        internal set
        {
            if (_panelSizes == value)
                return;
            _panelSizes = value;
            _observers.Notify(this, SizesBit);
        }
    }

    internal void ResizePanels(DockPanelSizes sizes)
    {
        if (sizes == _panelSizes)
            return;

        PanelSizes = sizes;
        RaiseLayoutChanged();
    }

    public event EventHandler<ActiveElementChangedEventArgs>? ActiveElementChanged;

    /// <summary>Raised after a dock, a float, a hide, a tab's reorder, a resize of a split or an edge region, a
    /// floating window's close, an unregister, a layout load or a reset: anything a saved layout records but a floating
    /// window's bounds, which <see cref="FloatingWindow.BoundsChanged"/> reports.</summary>
    public event EventHandler? LayoutChanged;

    /// <summary>Null closes every element here.</summary>
    public IDockCloseHandler? CloseHandler { get; set; }

    public event EventHandler<FloatingWindowEventArgs>? FloatingWindowAdded;

    /// <summary>Raised when a floating window is closed or emptied.</summary>
    public event EventHandler<FloatingWindowEventArgs>? FloatingWindowRemoved;

    public DockManager()
    {
        _floatingView = _floatingWindows.AsReadOnly();
        LeftGroup = CreateRootGroup(DockPosition.Left);
        RightGroup = CreateRootGroup(DockPosition.Right);
        TopGroup = CreateRootGroup(DockPosition.Top);
        BottomGroup = CreateRootGroup(DockPosition.Bottom);
        CenterGroup = CreateRootGroup(DockPosition.Center);
        _roots = [LeftGroup, RightGroup, TopGroup, BottomGroup, CenterGroup];
    }

    private DockGroup CreateRootGroup(DockPosition position)
    {
        var group = new DockGroup(position);
        group.SetManager(this);
        return group;
    }

    /// <summary>Null for the positions that have no root region.</summary>
    public DockGroup? RootGroup(DockPosition position) =>
        position switch
        {
            DockPosition.Left => LeftGroup,
            DockPosition.Right => RightGroup,
            DockPosition.Top => TopGroup,
            DockPosition.Bottom => BottomGroup,
            DockPosition.Center => CenterGroup,
            _ => null,
        };

    /// <inheritdoc cref="Register(DockElement)"/>
    /// <returns>The element made for <paramref name="source"/>, which its <see cref="IDockSource.DockElement"/> then
    /// holds.</returns>
    public DockElement Register(IDockSource source, DockType type = DockType.Tool)
    {
        ArgumentNullException.ThrowIfNull(source);

        var element = new DockElement(source.ContentId, source.Title, type)
        {
            Source = source,
            Content = source.Content,
            IsDirty = source.IsDirty,
        };

        Register(element);
        source.DockElement = element;
        return element;
    }

    /// <summary>Adds the element to the dock, hidden, unless the last layout loaded placed an element of its ID that
    /// was not registered then: it takes that place, beside the tabs it shared, or hidden there.</summary>
    /// <exception cref="InvalidOperationException">An element with the same ID is registered.</exception>
    public void Register(DockElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (_elementsById.ContainsKey(element.Id))
            throw new InvalidOperationException(
                $"An element with ID '{element.Id}' is already registered."
            );

        _elementsById[element.Id] = element;
        element.Manager = this;
        _elements.Add(element);
        Unplaced.Place(this, element);
    }

    // Kept until each element registers, or the next load or reset forgets them.
    internal SavedPlaces Unplaced { get; set; } = new();

    internal void RequireRegistered(DockElement element)
    {
        if (!ReferenceEquals(GetElement(element.Id), element))
            throw NotRegistered(element);
    }

    static InvalidOperationException NotRegistered(DockElement element) =>
        new($"The element '{element.Id}' is not registered with this dock manager.");

    public bool Unregister(DockElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (!_elements.Remove(element))
            return false;

        _elementsById.Remove(element.Id);
        element.Parent?.Remove(element);
        element.Manager = null;
        if (element.Source is { } source)
            source.DockElement = null;
        return true;
    }

    /// <summary>One user close of several elements: those the close handler handles go to it in a single
    /// request, and the rest close here. True when any closed here.</summary>
    public bool Close(IReadOnlyList<DockElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        var handler = CloseHandler;
        List<DockElement>? handed = null;
        var closedHere = false;
        for (var i = 0; i < elements.Count; i++)
        {
            var element = elements[i];
            if (!element.CanClose || element.Manager != this)
                continue;

            if (handler?.Handles(element) == true)
            {
                (handed ??= []).Add(element);
                continue;
            }

            element.Hide();
            element.IsClosed = true;
            element.Source?.OnClosed();
            closedHere = true;
        }

        if (handed is not null)
            handler!.RequestClose(handed);
        return closedHere;
    }

    public DockElement? GetElement(string id)
    {
        return _elementsById.TryGetValue(id, out var element) ? element : null;
    }

    /// <summary>Throws for a position with no root region, or an element this manager never registered, leaving the
    /// element where it was.</summary>
    public void Dock(DockElement element, DockPosition position)
    {
        ArgumentNullException.ThrowIfNull(element);
        RequireRegistered(element);
        var root = RootGroup(position) ?? throw new ArgumentOutOfRangeException(nameof(position));

        // A split group's own elements never show, so the element goes to a leaf.
        ResolveLeafFor(root).Insert(element);

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Center tabs into the target; the rest split it, which a floating target refuses.</summary>
    public void DockRelativeTo(DockElement element, DockElement target, DockDirection direction)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(target);
        RequireRegistered(element);

        if (target.Parent == null)
            throw new InvalidOperationException("Target element is not docked.");

        var targetGroup = target.Parent;

        switch (direction)
        {
            case DockDirection.Center:
                DockToGroup(element, targetGroup);
                break;

            case DockDirection.Left:
            case DockDirection.Right:
            case DockDirection.Top:
            case DockDirection.Bottom:
                DockBySplit(element, targetGroup, direction);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }

    /// <summary>The single canonical split, so the resulting tree is identical regardless of entry point. A split group
    /// splits at the leaf a dock into it would land in, an empty group takes the element as its tab, and a floating
    /// group holds only tabs and throws. What the group showed stays shown on its side.</summary>
    public void DockBySplit(DockElement element, DockGroup targetGroup, DockDirection side)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(targetGroup);
        RequireRegistered(element);
        if (
            side
            is not (
                DockDirection.Left
                or DockDirection.Right
                or DockDirection.Top
                or DockDirection.Bottom
            )
        )
            throw new ArgumentException(
                "Split side must be Left, Right, Top, or Bottom.",
                nameof(side)
            );
        if (targetGroup.Position == DockPosition.Floating)
            throw new ArgumentException(
                "A floating window holds only tabs, so it cannot split.",
                nameof(targetGroup)
            );

        targetGroup = ResolveLeafFor(targetGroup);

        // A lone tab split against its own group would leave an empty side.
        if (ReferenceEquals(element.Parent, targetGroup) && targetGroup.Elements.Count <= 1)
            return;

        if (targetGroup.Elements.Count == 0)
        {
            DockToGroup(element, targetGroup);
            return;
        }

        var orientation = side is DockDirection.Left or DockDirection.Right
            ? SplitOrientation.Horizontal
            : SplitOrientation.Vertical;

        // Split first, so detaching the element collapsing another branch leaves this one whole.
        var (first, second) = SplitGroup(targetGroup, orientation);

        var newSide = side is DockDirection.Left or DockDirection.Top ? first : second;
        var existingSide = side is DockDirection.Left or DockDirection.Top ? second : first;

        if (ReferenceEquals(element.Parent, targetGroup))
            targetGroup.Detach(element, clearParent: false);

        var active = targetGroup.ActiveElement;
        foreach (var existingElement in new List<DockElement>(targetGroup.Elements))
            existingSide.Insert(existingElement);
        existingSide.ActiveElement = active;

        newSide.Insert(element);

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A supplied index inserts at that tab position; otherwise the element is appended. A split group passes
    /// the element to one of its leaves.</summary>
    public void DockToGroup(DockElement element, DockGroup group, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(group);
        RequireRegistered(element);

        ResolveLeafFor(group).Insert(element, index ?? int.MaxValue);

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    public FloatingWindow Float(
        DockElement element,
        float x = 100,
        float y = 100,
        float width = 400,
        float height = 300
    )
    {
        ArgumentNullException.ThrowIfNull(element);
        RequireRegistered(element);

        var floatGroup = new DockGroup(DockPosition.Floating);
        floatGroup.SetManager(this);
        floatGroup.Insert(element);

        var floatingWindow = new FloatingWindow(floatGroup, x, y, width, height) { Manager = this };

        _floatingWindows.Add(floatingWindow);
        _observers.Notify(this, FloatsBit);

        FloatingWindowAdded?.Invoke(this, new FloatingWindowEventArgs(floatingWindow));
        LayoutChanged?.Invoke(this, EventArgs.Empty);
        return floatingWindow;
    }

    private (DockGroup first, DockGroup second) SplitGroup(
        DockGroup group,
        SplitOrientation orientation
    )
    {
        var first = new DockGroup(group.Position) { SizeProportion = 0.5f };
        var second = new DockGroup(group.Position) { SizeProportion = 0.5f };

        first.SetManager(this);
        second.SetManager(this);

        group.Orientation = orientation;
        group.AttachChild(first);
        group.AttachChild(second);

        return (first, second);
    }

    public string SaveLayout() => LayoutSerializer.Serialize(this);

    /// <summary>Throws <see cref="InvalidDataException"/>, leaving the manager as it was, for a layout that is
    /// unreadable or of another format version. An open document the layout leaves hidden docks back as
    /// <see cref="ResetLayout"/> docks it. An element the layout names that is not registered yet takes its place once
    /// it registers, and until then <see cref="SaveLayout"/> keeps that place.</summary>
    public void LoadLayout(string layout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layout);
        LayoutSerializer.Deserialize(this, layout);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Hides every element and forgets where each sat, and where a loaded layout put those not registered
    /// yet, except the open documents, which dock back where <see cref="DockElement.Show"/> would put them: the
    /// centre, unless their source names another region.</summary>
    public void ResetLayout()
    {
        var active = _activeElement;
        Unplaced = new();
        var documents = ClearLayout();
        DockBack(documents);
        if (active?.Parent is not null)
            active.Activate();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    // Root instances are kept and only emptied, or the control's container bindings go stale.
    internal List<DockElement> ClearLayout()
    {
        var documents = new List<DockElement>();
        for (var i = 0; i < _elements.Count; i++)
        {
            if (_elements[i] is { DockType: DockType.Document, Parent: not null } document)
                documents.Add(document);
        }

        // So FloatingWindowRemoved fires and their hosts go.
        foreach (var window in _floatingWindows.ToList())
            window.Discard();

        foreach (var root in _roots)
            root.Clear();

        for (var i = 0; i < _elements.Count; i++)
            _elements[i].Position = DockPosition.None;

        SetActiveElement(null);
        return documents;
    }

    // Unlike a panel, nothing lists a hidden document to show it again.
    internal void DockBack(List<DockElement> documents)
    {
        foreach (var document in documents)
        {
            if (document.Parent is null && document.Manager == this)
                ResolveLeafFor(RootGroup(document.ResolveShowPosition())!).Insert(document);
        }
    }

    internal bool IsRootGroup(DockGroup group) => ReferenceEquals(RootGroup(group.Position), group);

    internal void SetActiveElement(DockElement? element)
    {
        if (_activeElement != element)
        {
            _activeElement = element;
            _observers.Notify(this, ActiveBit);
            ActiveElementChanged?.Invoke(
                this,
                element?.Activated ?? ActiveElementChangedEventArgs.None
            );
        }
    }

    internal void RaiseLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);

    internal void NotifyGroupEmpty(DockGroup group)
    {
        if (IsRootGroup(group))
        {
            return;
        }

        if (group.Window is { } window)
        {
            window.Discard();
            return;
        }

        var parentGroup = group.Parent;
        parentGroup?.DetachChild(group);

        if (parentGroup != null)
            NormalizeFrom(parentGroup);
    }

    // A wrapper left behind would collapse its edge region to nothing.
    internal static void NormalizeFrom(DockGroup start)
    {
        for (var current = start; current != null; current = current.Parent)
            current.CollapseRedundantWrapper();
    }

    internal void RemoveFloatingWindow(FloatingWindow window)
    {
        _floatingWindows.Remove(window);
        _observers.Notify(this, FloatsBit);
        window.Group.Window = null;

        FloatingWindowRemoved?.Invoke(this, new FloatingWindowEventArgs(window));
    }

    // The active element's leaf, so a new tab lands beside what the user sees.
    internal DockGroup ResolveLeafFor(DockGroup group)
    {
        if (
            _activeElement?.Parent is { } preferred
            && (ReferenceEquals(preferred, group) || IsDescendant(group, preferred))
            && !preferred.IsSplitGroup
        )
            return preferred;

        var current = group;
        while (current.IsSplitGroup)
            current = current.Children[0];
        return current;
    }

    private static bool IsDescendant(DockGroup ancestor, DockGroup candidate)
    {
        for (var p = candidate.Parent; p != null; p = p.Parent)
        {
            if (ReferenceEquals(p, ancestor))
                return true;
        }
        return false;
    }
}

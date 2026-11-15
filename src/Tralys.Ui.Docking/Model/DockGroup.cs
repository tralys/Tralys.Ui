using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Docking;

/// <summary>A node of a dock's layout: a group of tabbed elements, or a split of two child groups. Its
/// <see cref="Elements"/>, <see cref="Children"/>, <see cref="ActiveElement"/>, <see cref="Orientation"/> and
/// <see cref="SizeProportion"/> report their reads, so a component that read one renders again as it changes.</summary>
public sealed class DockGroup : IObservable
{
    const int ElementsBit = 0;
    const int ChildrenBit = 1;
    const int ActiveBit = 2;
    const int SizeBit = 3;
    const int OrientationBit = 4;

    private readonly List<DockElement> _elements = [];
    private readonly List<DockGroup> _children = [];
    private readonly IReadOnlyList<DockElement> _elementsView;
    private readonly IReadOnlyList<DockGroup> _childrenView;
    private DockElement? _activeElement;
    private float _sizeProportion = 1.0f;
    private SplitOrientation _orientation = SplitOrientation.Horizontal;
    private ObserverSet _observers;

    ref ObserverSet IObservable.Observers => ref _observers;

    public IReadOnlyList<DockElement> Elements
    {
        get
        {
            Observation.Read(this, ElementsBit);
            return _elementsView;
        }
    }

    public DockElement? ActiveElement
    {
        get
        {
            Observation.Read(this, ActiveBit);
            return _activeElement;
        }
        set
        {
            if (_activeElement != value && (value == null || _elements.Contains(value)))
            {
                _activeElement = value;
                _observers.Notify(this, ActiveBit);
                ActiveElementChanged?.Invoke(
                    this,
                    value?.Activated ?? ActiveElementChangedEventArgs.None
                );
            }
        }
    }

    public DockPosition Position { get; internal set; }

    public DockGroup? Parent { get; internal set; }

    public IReadOnlyList<DockGroup> Children
    {
        get
        {
            Observation.Read(this, ChildrenBit);
            return _childrenView;
        }
    }

    public DockManager? Manager { get; internal set; }

    /// <summary>The open floating window this group backs, if any.</summary>
    public FloatingWindow? Window { get; internal set; }

    public bool IsTabbedGroup => Elements.Count > 0 && Children.Count == 0;

    public bool IsSplitGroup => Children.Count > 0;

    public SplitOrientation Orientation
    {
        get
        {
            Observation.Read(this, OrientationBit);
            return _orientation;
        }
        internal set
        {
            if (_orientation == value)
                return;
            _orientation = value;
            _observers.Notify(this, OrientationBit);
        }
    }

    /// <summary>Relative to sibling groups, 0 to 1. A change in a dock raises its manager's
    /// <see cref="DockManager.LayoutChanged"/>, and a dock control shows it.</summary>
    public float SizeProportion
    {
        get
        {
            Observation.Read(this, SizeBit);
            return _sizeProportion;
        }
        set
        {
            if (_sizeProportion == value)
                return;

            _sizeProportion = value;
            _observers.Notify(this, SizeBit);
            Manager?.RaiseLayoutChanged();
        }
    }

    public event EventHandler<ActiveElementChangedEventArgs>? ActiveElementChanged;

    /// <summary>Lets a tab strip update one tab at a time, so each tab owns its own subscriptions.</summary>
    public event EventHandler<DockElementsChangedEventArgs>? ElementsChanged;

    private void RaiseElementsChanged(DockElementsChangedEventArgs args)
    {
        _observers.Notify(this, ElementsBit);
        ElementsChanged?.Invoke(this, args);
    }

    private void SetActive(DockElement? element)
    {
        if (_activeElement == element)
            return;
        _activeElement = element;
        _observers.Notify(this, ActiveBit);
    }

    private void SetSize(float value)
    {
        if (_sizeProportion == value)
            return;
        _sizeProportion = value;
        _observers.Notify(this, SizeBit);
    }

    public DockGroup(DockPosition position = DockPosition.None)
    {
        Position = position;
        _elementsView = _elements.AsReadOnly();
        _childrenView = _children.AsReadOnly();
    }

    public void Add(DockElement element) => InsertAt(element, int.MaxValue);

    /// <summary>Hides the element, as <see cref="DockElement.Hide"/> does; false when it is not in this group.</summary>
    public bool Remove(DockElement element)
    {
        if (!Detach(element))
            return false;

        if (Manager is { } manager)
        {
            if (manager.ActiveElement == element)
                manager.SetActiveElement(ActiveElement);
            manager.RaiseLayoutChanged();
        }
        return true;
    }

    // For a caller partway through a layout change, which settles the active element and notifies for itself.
    internal bool Detach(DockElement element, bool clearParent = true)
    {
        var index = _elements.IndexOf(element);
        if (index < 0)
            return false;

        _elements.RemoveAt(index);
        if (clearParent)
            element.Parent = null;

        // The tab that slid into the removed slot takes over, or the new last one.
        // Ahead of the event: a strip left to reselect on its own would activate that tab in the manager.
        if (_activeElement == element)
        {
            ActiveElement =
                _elements.Count > 0 ? _elements[Math.Min(index, _elements.Count - 1)] : null;
        }

        RaiseElementsChanged(
            new DockElementsChangedEventArgs(DockElementsChange.Removed, element, index)
        );

        if (_elements.Count == 0 && _children.Count == 0)
        {
            Manager?.NotifyGroupEmpty(this);
        }

        return true;
    }

    /// <summary>Moves the element to another tab position, clamping the index. A move in a dock raises its manager's
    /// <see cref="DockManager.LayoutChanged"/>.</summary>
    public void MoveTo(DockElement element, int index)
    {
        if (!_elements.Contains(element))
            throw new InvalidOperationException("Element is not in this group");

        // Clamped, not thrown: callers compute indices from drag positions.
        var oldIndex = _elements.IndexOf(element);
        _elements.Remove(element);
        var clamped = Math.Clamp(index, 0, _elements.Count);
        _elements.Insert(clamped, element);
        if (clamped == oldIndex)
            return;

        RaiseElementsChanged(
            new DockElementsChangedEventArgs(DockElementsChange.Moved, element, clamped, oldIndex)
        );
        Manager?.RaiseLayoutChanged();
    }

    // For a load, which notifies once it is done.
    internal void RestoreSizeProportion(float value) => SetSize(value);

    // One resize of a split, notified once.
    internal void ResizeSplit(float first, float second)
    {
        if (_children.Count < 2)
            return;

        var changed =
            _children[0]._sizeProportion != first || _children[1]._sizeProportion != second;
        _children[0].SetSize(first);
        _children[1].SetSize(second);
        if (changed)
            Manager?.RaiseLayoutChanged();
    }

    /// <summary>Docks the element here, as <see cref="DockManager.DockToGroup"/> does. The index is clamped.</summary>
    public void InsertAt(DockElement element, int index)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (Manager is { } manager)
            manager.DockToGroup(element, this, index);
        else
            Insert(element, index);
    }

    // For a caller partway through a layout change, which notifies for itself.
    internal void Insert(DockElement element, int index = int.MaxValue)
    {
        if (_elements.Contains(element))
            return;

        var oldParent = element.Parent;
        var clamped = Math.Clamp(index, 0, _elements.Count);
        _elements.Insert(clamped, element);

        // Ahead of the parent, so a listener told of the new state reads where it now is.
        element.Position = Position;
        element.Manager = Manager;
        element.Parent = this;

        // Ahead of the event: a strip left to select its first tab on its own would activate it in the manager.
        ActiveElement = element;

        RaiseElementsChanged(
            new DockElementsChangedEventArgs(DockElementsChange.Added, element, clamped)
        );

        // Last: emptying the old group can collapse this one into its parent, which must carry the element along.
        if (oldParent is not null && !ReferenceEquals(oldParent, this))
            oldParent.Detach(element, clearParent: false);
    }

    /// <summary>Makes <paramref name="group"/> a child of this split, taking it from any other parent. Tabs already here
    /// move into a first child beside it; in a dock, a group left with one child takes on what the child holds.</summary>
    /// <exception cref="ArgumentException"><paramref name="group"/> is this group, one around it, a dock's region or a
    /// float's.</exception>
    /// <exception cref="InvalidOperationException">This split already holds two groups, or in a dock,
    /// <paramref name="group"/> holds an element the dock's manager never registered.</exception>
    public void AddChild(DockGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (_children.Contains(group))
            return;

        for (var around = this; around != null; around = around.Parent)
        {
            if (ReferenceEquals(around, group))
                throw new ArgumentException("A group cannot hold itself.", nameof(group));
        }
        if (group.Window is not null || group.Manager?.IsRootGroup(group) == true)
            throw new ArgumentException(
                "A dock's regions and floats stay where they are.",
                nameof(group)
            );
        if (_children.Count == 2)
            throw new InvalidOperationException("A split holds two groups.");
        if (Manager is { } owner)
            group.RequireRegisteredWith(owner);

        if (_elements.Count > 0)
            MoveTabsIntoChild();

        var oldParent = group.Parent;
        oldParent?.DetachChild(group);
        AttachChild(group);
        group.SetManager(Manager);
        group.SetPosition(Position);

        if (Manager is { } manager)
        {
            DockManager.NormalizeFrom(this);
            if (oldParent is not null)
                DockManager.NormalizeFrom(oldParent);
            manager.RaiseLayoutChanged();
        }
    }

    /// <summary>Takes <paramref name="group"/> out of this split. In a dock it hides the tabs in it, as
    /// <see cref="Remove"/> does, and the split takes on what the child left holds.</summary>
    /// <returns>False when <paramref name="group"/> is not a child of this one.</returns>
    public bool RemoveChild(DockGroup group)
    {
        if (!DetachChild(group))
            return false;

        if (Manager is not { } manager)
            return true;

        group.HideTabs();
        group.SetManager(null);
        if (_elements.Count == 0 && _children.Count == 0)
            manager.NotifyGroupEmpty(this);
        else
            DockManager.NormalizeFrom(this);
        manager.RaiseLayoutChanged();
        return true;
    }

    internal void AttachChild(DockGroup group)
    {
        group.Parent = this;
        group.Manager = Manager;
        _children.Add(group);
        _observers.Notify(this, ChildrenBit);
    }

    internal bool DetachChild(DockGroup group)
    {
        if (!_children.Remove(group))
            return false;

        group.Parent = null;
        _observers.Notify(this, ChildrenBit);
        return true;
    }

    void MoveTabsIntoChild()
    {
        var first = new DockGroup(Position);
        AttachChild(first);
        foreach (var element in _elements)
        {
            element.Parent = first;
            first._elements.Add(element);
        }
        first._activeElement = _activeElement;
        _elements.Clear();
        SetActive(null);
        RaiseElementsChanged(new DockElementsChangedEventArgs(DockElementsChange.Reset));
    }

    void RequireRegisteredWith(DockManager manager)
    {
        foreach (var element in _elements)
            manager.RequireRegistered(element);
        foreach (var child in _children)
            child.RequireRegisteredWith(manager);
    }

    void HideTabs()
    {
        foreach (var element in _elements.ToList())
            element.Hide();
        foreach (var child in _children)
            child.HideTabs();
    }

    void SetPosition(DockPosition position)
    {
        Position = position;
        foreach (var element in _elements)
            element.Position = position;
        foreach (var child in _children)
            child.SetPosition(position);
    }

    internal void SetManager(DockManager? manager)
    {
        Manager = manager;
        foreach (var element in _elements)
        {
            element.Manager = manager;
        }
        foreach (var child in _children)
        {
            child.SetManager(manager);
        }
    }

    internal void Clear()
    {
        foreach (var element in new List<DockElement>(_elements))
        {
            Detach(element);
        }
        foreach (var child in new List<DockGroup>(_children))
        {
            DetachChild(child);
            child.Clear();
        }
    }

    // A root left wrapping one child would bind its region to that child and collapse it to nothing.
    internal void CollapseRedundantWrapper()
    {
        var absorbed = false;
        while (_elements.Count == 0 && _children.Count == 1)
        {
            var only = _children[0];
            _children.Clear();
            _orientation = only._orientation;

            foreach (var grandChild in only._children)
            {
                grandChild.Parent = this;
                grandChild.Manager = Manager;
                _children.Add(grandChild);
            }
            foreach (var element in only._elements)
            {
                element.Parent = this;
                element.Position = Position;
                element.Manager = Manager;
                _elements.Add(element);
            }
            SetActive(
                only._activeElement != null && _elements.Contains(only._activeElement)
                    ? only._activeElement
                    : _activeElement
            );

            only._children.Clear();
            only._elements.Clear();
            only.Parent = null;
            absorbed = true;
        }

        // The elements arrived without per-item events, so a strip bound here rebuilds.
        if (absorbed)
        {
            _observers.Notify(this, ChildrenBit);
            _observers.Notify(this, OrientationBit);
            RaiseElementsChanged(new DockElementsChangedEventArgs(DockElementsChange.Reset));
        }
    }

    public override string ToString() =>
        $"DockGroup: {Position} ({Elements.Count} elements, {Children.Count} children)";
}

public enum SplitOrientation
{
    Horizontal,
    Vertical,
}

using System.Collections;
using System.Runtime.InteropServices;
using Tralys.Ui.Controls;

namespace Tralys.Ui;

/// <summary>The children of each row of a tree, as a <see cref="TreeRows{T}"/> reads them; the children of null are the
/// rows at the tree's top. The tree asks only for the children of the rows it lays out, unless it filters, so it reads
/// nothing a collapsed row hides.</summary>
public interface ITreeChildren<T>
    where T : TreeRow
{
    [FramePath]
    int ChildCount(T? parent);

    [FramePath]
    T ChildAt(T? parent, int index);
}

/// <summary>A tree's rows as one flat list, for a list that virtualizes them as a <c>TreeView</c> does not, a
/// <c>ListBox</c> showing them taking a tree view's keys: each row at the tree's top and, beneath every expanded
/// row, its children, depth first.</summary>
/// <remarks>The rows are the <see cref="TreeRow"/>s <see cref="ITreeChildren{T}"/> hands, each told its depth and
/// whether it has children as it is laid out, and kept by reference through a <see cref="RowList{T}"/>, so a row keeps
/// its container as rows around it come and go. A row reached twice shows once, where it is first reached. The tree
/// shows no rows until it is first laid out with <see cref="Refresh"/>.</remarks>
public sealed class TreeRows<T>
    : IItemsSource<T>,
        IReadOnlyList<T>,
        IItemsSourceListener,
        ITreeRowsOwner
    where T : TreeRow
{
    readonly ITreeChildren<T> _children;
    readonly RowList<T> _rows = new();
    readonly HashSet<T> _reached = new(ReferenceEqualityComparer.Instance);
    readonly List<Sibling> _siblings = [];
    readonly Comparison<Sibling> _bySibling;
    readonly Action _refilter;
    Step[] _steps = [];
    int _stepCount;
    ItemsSourceListeners _listeners;
    RowFilter? _filter;
    IComparer<T>? _comparer;
    int _deferrals;
    bool _pending;

    public TreeRows(ITreeChildren<T> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        _children = children;
        _bySibling = CompareSiblings;
        _refilter = OnFilterRefreshed;
        _rows.AddListener(this);
    }

    /// <summary>Which rows show: each the filter matches, and every row above one, so a row the filter leaves out shows
    /// while a row beneath it does; null shows every row. Its <see cref="RowFilter.Refresh"/> lays the rows out again
    /// while the tree has listeners: a tree none listens to is not kept alive by its filter, and lays its rows out
    /// afresh once one does.</summary>
    public RowFilter? Filter
    {
        get => _filter;
        set
        {
            if (ReferenceEquals(value, _filter))
                return;

            if (!_listeners.IsEmpty)
            {
                _filter?.Refreshed -= _refilter;
                value?.Refreshed += _refilter;
            }
            _filter = value;
            Refresh();
        }
    }

    /// <summary>The order each row's children show in, ties keeping the order <see cref="ITreeChildren{T}"/> hands
    /// them in; null keeps that order.</summary>
    public IComparer<T>? Comparer
    {
        get => _comparer;
        set
        {
            if (ReferenceEquals(value, _comparer))
                return;

            _comparer = value;
            Refresh();
        }
    }

    public int Count
    {
        [FramePath]
        get => _rows.Count;
    }

    public T this[int index]
    {
        [FramePath]
        get => _rows[index];
    }

    object? IItemsSource.this[int index] => _rows[index];

    [FramePath]
    T IItemsSource<T>.ItemAt(int index) => _rows[index];

    [FramePath]
    public int IndexOf(object? item) => _rows.IndexOf(item);

    public void AddListener(IItemsSourceListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        if (_listeners.IsEmpty && _filter is { } filter)
        {
            Refresh();
            filter.Refreshed += _refilter;
        }
        _listeners.Add(listener);
    }

    public void RemoveListener(IItemsSourceListener listener)
    {
        _listeners.Remove(listener);
        if (_listeners.IsEmpty)
            _filter?.Refreshed -= _refilter;
    }

    public List<T>.Enumerator GetEnumerator() => _rows.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => _rows.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _rows.GetEnumerator();

    /// <summary>Lays the rows out again, as a change the tree cannot hear of needs: rows its children gained or lost,
    /// or a change to what its filter matches or its comparer compares by. A row expanding or collapsing, or a new
    /// filter or comparer, lays them out by itself.</summary>
    /// <returns>Whether the rows shown changed.</returns>
    [FramePath]
    public bool Refresh()
    {
        if (_deferrals > 0)
        {
            _pending = true;
            return false;
        }

        var changed = false;
        _deferrals++;
        try
        {
            do
            {
                _pending = false;
                changed |= Lay();
            } while (_pending);
        }
        finally
        {
            _deferrals--;
        }
        return changed;
    }

    /// <summary>Holds the rows as they are, however rows expand or collapse or the filter or order change, until the
    /// value handed back is disposed, which then lays them out once for all of it.</summary>
    [FramePath]
    public DeferredRefresh DeferRefresh()
    {
        _deferrals++;
        return new DeferredRefresh(this);
    }

    [FramePath]
    void ITreeRowsOwner.OnExpandedChanged() => Refresh();

    [FramePath]
    void ITreeRowsOwner.EndDeferral()
    {
        if (_deferrals > 0 && --_deferrals == 0 && _pending)
            Refresh();
    }

    [FramePath]
    void IItemsSourceListener.OnItemsChanged(IItemsSource source, in ItemsChange change)
    {
        if (ReferenceEquals(source, _rows))
            _listeners.Raise(this, change);
    }

    [FramePath]
    void OnFilterRefreshed() => Refresh();

    [FramePath]
    bool Lay()
    {
        _stepCount = 0;
        WalkChildren(null, -1, 0);

        for (var i = _stepCount - 1; i >= 0; i--)
        {
            ref var step = ref _steps[i];
            if (step.ChildrenWalked)
                step.HasChildren = step.ShownChild;
            step.Shown = step.Matched || step.ShownChild;
            if (step.Shown && step.Parent >= 0)
                _steps[step.Parent].ShownChild = true;
        }

        for (var i = 0; i < _stepCount; )
        {
            ref var step = ref _steps[i];
            if (!step.Shown)
            {
                i = step.End;
                continue;
            }

            var row = step.Row;
            row.Depth = step.Depth;
            row.HasChildren = step.HasChildren;
            _rows.Add(row);
            i = row.IsExpanded ? i + 1 : step.End;
        }

        Array.Clear(_steps, 0, _stepCount);
        _reached.Clear();
        return _rows.Apply();
    }

    [FramePath]
    void WalkChildren(T? parent, int parentStep, int depth)
    {
        var count = _children.ChildCount(parent);
        var start = _siblings.Count;
        for (var i = 0; i < count; i++)
            _siblings.Add(new Sibling(_children.ChildAt(parent, i), i));
        if (_comparer is not null && count > 1)
            CollectionsMarshal.AsSpan(_siblings).Slice(start, count).Sort(_bySibling);

        for (var i = 0; i < count; i++)
            Walk(_siblings[start + i].Row, parentStep, depth);
        _siblings.RemoveRange(start, count);
    }

    [FramePath]
    void Walk(T row, int parentStep, int depth)
    {
        if (!_reached.Add(row))
            return;

        row.Owner = this;
        var index = _stepCount++;
        ValueList.EnsureCapacity(ref _steps, _stepCount);
        _steps[index] = new Step
        {
            Row = row,
            Parent = parentStep,
            Depth = depth,
            Matched = _filter?.Matches(row) ?? true,
        };

        if (_filter is not null || row.IsExpanded)
        {
            _steps[index].ChildrenWalked = true;
            WalkChildren(row, index, depth + 1);
        }
        else
            _steps[index].HasChildren = _children.ChildCount(row) > 0;
        _steps[index].End = _stepCount;
    }

    [FramePath]
    int CompareSiblings(Sibling a, Sibling b)
    {
        var order = _comparer!.Compare(a.Row, b.Row);
        return order != 0 ? order : a.Order.CompareTo(b.Order);
    }

    readonly struct Sibling(T row, int order)
    {
        public T Row { get; } = row;

        public int Order { get; } = order;
    }

    struct Step
    {
        public T Row;
        public int Parent;
        public int Depth;
        public int End;
        public bool Matched;
        public bool ChildrenWalked;
        public bool HasChildren;
        public bool ShownChild;
        public bool Shown;
    }
}

/// <summary>Holds the rows of the <see cref="TreeRows{T}"/> that handed it out as they are until it is disposed, which
/// is done once.</summary>
public readonly struct DeferredRefresh : IDisposable
{
    readonly ITreeRowsOwner? _rows;

    internal DeferredRefresh(ITreeRowsOwner rows) => _rows = rows;

    public void Dispose() => _rows?.EndDeferral();
}

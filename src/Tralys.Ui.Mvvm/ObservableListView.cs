namespace Tralys.Ui.Mvvm;

/// <summary>Makes <see cref="ObservableListView{TItem, TRow}"/>s.</summary>
public static class ObservableListView
{
    /// <summary>The rows <paramref name="transform"/> makes of <paramref name="items"/>, kept in step with them; the
    /// source owns the rows, and disposes each as its item leaves. A change to what its filter read is heard on the
    /// thread of <paramref name="dispatcher"/>, posted there from any other.</summary>
    public static ObservableListView<TItem, TRow> ToView<TItem, TRow>(
        this ObservableList<TItem> items,
        Func<TItem, TRow> transform,
        IUiDispatcher? dispatcher = null
    )
        where TRow : class
    {
        ArgumentNullException.ThrowIfNull(transform);
        return new ObservableListView<TItem, TRow>(items, transform, dispatcher);
    }

    /// <summary>The items themselves as rows, to filter in place; the source owns none of them. A change to what its
    /// filter read is heard on the thread of <paramref name="dispatcher"/>, posted there from any other.</summary>
    public static ObservableListView<T, T> ToView<T>(
        this ObservableList<T> items,
        IUiDispatcher? dispatcher = null
    )
        where T : class => new(items, null, dispatcher);
}

/// <summary>The rows of an <see cref="ObservableList{T}"/>, kept in step with its items, which a filter shows or hides
/// in place. The filter's reads are tracked: a change to what it read of one item checks that row again, and any
/// other checks every row, so a filter reading a query re-runs as the query changes. Each change names only the rows
/// it touched, and reading which rows it shows is tracked too.</summary>
public sealed class ObservableListView<TItem, TRow>
    : IFilteredRows,
        IChangeObserver,
        IObservable,
        IDisposable
    where TRow : class
{
    readonly ObservableList<TItem> _items;
    readonly Func<TItem, TRow>? _transform;
    readonly ObservableList<TRow> _rows;
    readonly RowsView _rowsView;
    readonly IDisposable _subscription;
    bool[] _shown = [];
    int _shownCount;
    TRow[] _made = [];
    Func<TItem, bool>? _filter;
    Dependencies _dependencies;
    ObserverSet _observers;
    Dictionary<object, int>? _indexes;
    bool _indexesStale = true;
    bool _readsItems;
    int _departed;
    bool _disposed;

    internal ObservableListView(
        ObservableList<TItem> items,
        Func<TItem, TRow>? transform,
        IUiDispatcher? dispatcher
    )
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
        _transform = transform;
        _dependencies = new Dependencies(dispatcher);
        _rows = transform is null
            ? (ObservableList<TRow>)(object)items
            : new ObservableList<TRow>();
        _rowsView = new RowsView(_rows);
        _subscription = items.Subscribe(OnItemsChanged);
        Rebuild();
    }

    /// <summary>Every row, shown or hidden, in the order of their items; none once disposed.</summary>
    public IReadOnlyList<TRow> Rows => _disposed ? [] : _rows;

    IReadOnlyList<object> IFilteredRows.Rows => _rowsView;

    public int ShownCount
    {
        get
        {
            Observation.Read(this, 0);
            return _shownCount;
        }
    }

    public event RowsChangedHandler? Changed;

    ref Dependencies IChangeObserver.Dependencies => ref _dependencies;

    ref ObserverSet IObservable.Observers => ref _observers;

    public bool IsShown(int index)
    {
        Observation.Read(this, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            (uint)index,
            (uint)Rows.Count,
            nameof(index)
        );
        return _shown[index];
    }

    /// <summary>Whether <paramref name="row"/> is among the rows and the filter shows it.</summary>
    public bool IsShown(TRow row)
    {
        Observation.Read(this, 0);
        if (_disposed)
            return false;
        var rows = _rows.Items;
        for (var i = 0; i < rows.Length; i++)
        {
            if (ReferenceEquals(rows[i], row))
                return _shown[i];
        }
        return false;
    }

    /// <summary>Shows only the rows whose item <paramref name="filter"/> accepts, every row when it is null.</summary>
    public ObservableListView<TItem, TRow> Filter(Func<TItem, bool>? filter)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _filter = filter;
        Refresh();
        return this;
    }

    /// <summary>Checks every row against the filter again, for a filter that reads what is not tracked.</summary>
    public void Refresh()
    {
        if (_disposed)
            return;

        var items = _items.Items;
        var flipped = false;
        var scope = Observation.Track(this);
        try
        {
            for (var i = 0; i < items.Length; i++)
                flipped |= SetShown(i, Matches(items[i]));
        }
        finally
        {
            scope.Dispose();
        }
        _departed = 0;

        if (flipped)
            Raise(RowChange.Matched(0, items.Length));
    }

    /// <summary>Checks the row at <paramref name="index"/> against the filter again, for a filter that reads what is
    /// not tracked.</summary>
    public void Refresh(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            (uint)index,
            (uint)_items.Items.Length,
            nameof(index)
        );
        if (!_disposed && SetShown(index, MatchTracked(_items.Items[index])))
            Raise(RowChange.Matched(index, 1));
    }

    void IChangeObserver.OnChanged(object source, int property)
    {
        if (_disposed || _filter is null)
            return;

        // The filter read the items' shape, which only the structural change that follows brings up to date.
        if (ReferenceEquals(source, _items))
        {
            _readsItems = true;
            return;
        }

        var index = IndexOfItem(source);
        if (index < 0)
        {
            Refresh();
            return;
        }

        Refresh(index);
    }

    /// <summary>Stops following the items and empties the view, telling its readers once, before the rows it made
    /// are disposed: the items may change after, and a reader still holding the view must not see them.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _subscription.Dispose();
        _dependencies.Clear();
        _rowsView.Detach();
        _shown = [];
        _shownCount = 0;
        Raise(RowChange.Reset);
        Changed = null;
        if (_transform is null)
            return;

        var rows = _rows.Items;
        for (var i = 0; i < rows.Length; i++)
            (rows[i] as IDisposable)?.Dispose();
    }

    void OnItemsChanged(ObservableList<TItem> _, in ListChange<TItem> change)
    {
        if (_disposed)
            return;

        _indexesStale = true;
        switch (change.Kind)
        {
            case ListChangeKind.Add:
                Inserted(change.NewIndex, change.NewItems);
                Raise(RowChange.Added(change.NewIndex, change.Count));
                break;
            case ListChangeKind.Remove:
                Removed(change.OldIndex, change.Count);
                Raise(RowChange.Removed(change.OldIndex, change.Count));
                break;
            case ListChangeKind.Replace:
                Replaced(change.NewIndex, change.NewItems);
                Raise(RowChange.Replaced(change.NewIndex, change.Count));
                break;
            case ListChangeKind.Move:
                Moved(change.OldIndex, change.NewIndex);
                Raise(RowChange.Moved(change.OldIndex, change.NewIndex, 1));
                break;
            default:
                Rebuild();
                Raise(RowChange.Reset);
                break;
        }

        // A full pass drops the links to departed items, so it runs once a quarter of the rows have left.
        if (
            _readsItems
            || (_filter is not null && _departed > Math.Max(16, _items.Items.Length / 4))
        )
        {
            _readsItems = false;
            Refresh();
        }
    }

    void Inserted(int index, ReadOnlySpan<TItem> items)
    {
        var count = items.Length;
        var size = _rows.Items.Length + (_transform is null ? 0 : count);
        Grow(ref _shown, size);
        Array.Copy(_shown, index, _shown, index + count, size - count - index);
        MatchRange(index, items);

        if (_transform is null)
            return;
        Grow(ref _made, count);
        for (var i = 0; i < count; i++)
            _made[i] = _transform(items[i]);
        try
        {
            _rows.InsertRange(index, _made.AsSpan(0, count));
        }
        finally
        {
            Array.Clear(_made, 0, count);
        }
    }

    void Removed(int index, int count)
    {
        _departed += count;
        for (var i = index; i < index + count; i++)
        {
            if (_shown[i])
                _shownCount--;
        }
        var size = _rows.Items.Length - (_transform is null ? 0 : count);
        Array.Copy(_shown, index + count, _shown, index, size - index);

        if (_transform is null)
            return;
        Grow(ref _made, count);
        _rows.Items.Slice(index, count).CopyTo(_made);
        try
        {
            _rows.RemoveRange(index, count);
            DisposeMade(count);
        }
        finally
        {
            Array.Clear(_made, 0, count);
        }
    }

    void Replaced(int index, ReadOnlySpan<TItem> items)
    {
        var count = items.Length;
        _departed += count;
        for (var i = index; i < index + count; i++)
        {
            if (_shown[i])
                _shownCount--;
            _shown[i] = false;
        }
        MatchRange(index, items);

        if (_transform is null)
            return;
        Grow(ref _made, count);
        _rows.Items.Slice(index, count).CopyTo(_made);
        try
        {
            for (var i = 0; i < count; i++)
                _rows[index + i] = _transform(items[i]);
            DisposeMade(count);
        }
        finally
        {
            Array.Clear(_made, 0, count);
        }
    }

    void Moved(int from, int to)
    {
        var shown = _shown[from];
        if (from < to)
            Array.Copy(_shown, from + 1, _shown, from, to - from);
        else
            Array.Copy(_shown, to, _shown, to + 1, from - to);
        _shown[to] = shown;

        if (_transform is not null)
            _rows.Move(from, to);
    }

    void Rebuild()
    {
        var items = _items.Items;
        if (_transform is not null)
        {
            var old = _rows.Items.Length;
            Grow(ref _made, old);
            _rows.Items.CopyTo(_made);
            try
            {
                _rows.Clear();
                DisposeMade(old);
            }
            finally
            {
                Array.Clear(_made, 0, old);
            }

            Grow(ref _made, items.Length);
            for (var i = 0; i < items.Length; i++)
                _made[i] = _transform(items[i]);
            try
            {
                _rows.AddRange(_made.AsSpan(0, items.Length));
            }
            finally
            {
                Array.Clear(_made, 0, items.Length);
            }
        }

        _shown = new bool[items.Length];
        _shownCount = 0;
        _dependencies.Clear();
        _readsItems = false;
        _departed = 0;
        MatchRange(0, items);
    }

    void MatchRange(int index, ReadOnlySpan<TItem> items)
    {
        if (_filter is null)
        {
            for (var i = 0; i < items.Length; i++)
                _shown[index + i] = true;
            _shownCount += items.Length;
            return;
        }

        var scope = Observation.Track(this);
        try
        {
            for (var i = 0; i < items.Length; i++)
            {
                var shown = _filter(items[i]);
                _shown[index + i] = shown;
                if (shown)
                    _shownCount++;
            }
        }
        finally
        {
            scope.Complete(keepDropped: true);
        }
    }

    bool MatchTracked(TItem item)
    {
        var scope = Observation.Track(this);
        try
        {
            return Matches(item);
        }
        finally
        {
            scope.Complete(keepDropped: true);
        }
    }

    bool Matches(TItem item) => _filter is null || _filter(item);

    bool SetShown(int index, bool shown)
    {
        if (_shown[index] == shown)
            return false;
        _shown[index] = shown;
        _shownCount += shown ? 1 : -1;
        return true;
    }

    // Built again only after the items change, so a run of item changes looks each one up at once.
    int IndexOfItem(object source)
    {
        if (source is not TItem)
            return -1;

        var indexes = _indexes ??= new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        if (_indexesStale)
        {
            indexes.Clear();
            var items = _items.Items;
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] is { } item && !indexes.TryAdd(item, i))
                    indexes[item] = -1;
            }
            _indexesStale = false;
        }
        return indexes.TryGetValue(source, out var index) ? index : -1;
    }

    void DisposeMade(int count)
    {
        for (var i = 0; i < count; i++)
            (_made[i] as IDisposable)?.Dispose();
    }

    void Raise(in RowChange change)
    {
        _observers.Notify(this, 0);
        Changed?.Invoke(this, change);
    }

    sealed class RowsView(ObservableList<TRow> rows) : IReadOnlyList<object>
    {
        IReadOnlyList<TRow> _rows = rows;

        public object this[int index] => _rows[index];

        public int Count => _rows.Count;

        public void Detach() => _rows = [];

        public IEnumerator<object> GetEnumerator()
        {
            foreach (var row in _rows)
                yield return row;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }

    static void Grow<T>(ref T[] array, int needed)
    {
        if (array.Length >= needed)
            return;
        var grown = new T[Math.Max(needed, array.Length * 2)];
        Array.Copy(array, grown, array.Length);
        array = grown;
    }
}

using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

sealed class MatchingItemsSource : MatchingRows, IItemsSource
{
    public MatchingItemsSource(IFilteredRows source)
        : base(source) { }

    public object? this[int index]
    {
        [FramePath]
        get => Shown(index);
    }

    [FramePath]
    protected override bool Matches(IFilteredRows source, int index) => source.IsShown(index);
}

abstract class MatchingRows : IItemFilter, IItemsSourceListener, IObservable
{
    readonly IFilteredRows _source;
    readonly RowItems _rows;
    readonly FilteredItems _shown;
    ItemsSourceListeners _listeners;
    ObserverSet _observers;

    protected MatchingRows(IFilteredRows source)
    {
        _source = source;
        _rows = new RowItems(source);
        _shown = new FilteredItems(this, null);
        _shown.Follow(_rows, ItemsChange.Reset);
        _shown.AddListener(this);
        source.Changed += OnRowsChanged;
    }

    ref ObserverSet IObservable.Observers => ref _observers;

    public int Count
    {
        [FramePath]
        get
        {
            Observation.Read(this, 0);
            return _shown.Count;
        }
    }

    bool IItemFilter.IsFiltering
    {
        [FramePath]
        get => true;
    }

    [FramePath]
    public int IndexOf(object? item)
    {
        Observation.Read(this, 0);
        return _shown.IndexOf(item);
    }

    public void AddListener(IItemsSourceListener listener) => _listeners.Add(listener);

    public void RemoveListener(IItemsSourceListener listener) => _listeners.Remove(listener);

    [FramePath]
    public int ShownIndex(int sourceIndex) => _shown.ShownIndex(sourceIndex);

    [FramePath]
    protected object? Shown(int index)
    {
        Observation.Read(this, 0);
        return _shown[index];
    }

    [FramePath]
    protected abstract bool Matches(IFilteredRows source, int index);

    [FramePath]
    bool IItemFilter.Matches(int index) => Matches(_source, index);

    [FramePath]
    void OnRowsChanged(IFilteredRows source, in RowChange change)
    {
        switch (change.Kind)
        {
            case RowChangeKind.Add:
                _shown.Follow(_rows, ItemsChange.Added(change.NewIndex, change.Count));
                break;
            case RowChangeKind.Remove:
                _shown.Follow(_rows, ItemsChange.Removed(change.OldIndex, change.Count));
                break;
            case RowChangeKind.Replace:
                _shown.Follow(_rows, ItemsChange.Replaced(change.NewIndex, change.Count));
                break;
            case RowChangeKind.Move:
                _shown.Follow(
                    _rows,
                    ItemsChange.Moved(change.OldIndex, change.NewIndex, change.Count)
                );
                break;
            case RowChangeKind.Match when change.Count == 1:
                _shown.Recheck(change.NewIndex);
                break;
            case RowChangeKind.Match:
                _shown.Refilter();
                break;
            default:
                _shown.Follow(_rows, ItemsChange.Reset);
                break;
        }
    }

    [FramePath]
    void IItemsSourceListener.OnItemsChanged(IItemsSource source, in ItemsChange change)
    {
        _listeners.Raise((IItemsSource)this, change);
        _observers.Notify(this, 0);
    }

    sealed class RowItems(IFilteredRows source) : IItemsSource
    {
        public int Count
        {
            [FramePath]
            get => source.Rows.Count;
        }

        public object? this[int index]
        {
            [FramePath]
            get => source.Rows[index];
        }

        [FramePath]
        public int IndexOf(object? item)
        {
            var rows = source.Rows;
            for (var i = 0; i < rows.Count; i++)
            {
                if (ReferenceEquals(rows[i], item))
                    return i;
            }
            return -1;
        }

        public void AddListener(IItemsSourceListener listener) { }

        public void RemoveListener(IItemsSourceListener listener) { }
    }
}

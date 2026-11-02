using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

/// <summary>Adapts collections to <see cref="IItemsSource"/>. A collection has one adapter for as long as it lives, so
/// asking again hands a list the source it already shows, and the adapter subscribes to the collection once however
/// many lists show it.</summary>
public static class ItemsSource
{
    /// <summary>The source showing <paramref name="items"/>, which reads a struct item without boxing through
    /// <see cref="IItemsSource{T}.ItemAt"/>. It follows a <see cref="Tralys.Ui.Mvvm.ObservableList{T}"/>'s changes without
    /// allocating; any other list is read as it is and never heard from.</summary>
    /// <exception cref="ArgumentException"><paramref name="items"/> raises the BCL <c>INotifyCollectionChanged</c>,
    /// which no list here hears: hold the items in an <see cref="Tralys.Ui.Mvvm.ObservableList{T}"/>.</exception>
    public static IItemsSource<T> From<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items as IItemsSource<T> ?? Adapters<T>.Table.GetValue(items, Adapters<T>.Create);
    }

    /// <summary>The source showing a list of values, each kept boxed until it changes. It is read as it is and never
    /// heard from.</summary>
    public static IItemsSource FromValues<T>(IReadOnlyList<T> items)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(items);
        return ValueAdapters<T>.Table.GetValue(items, static list => new ValueItemsSource<T>(list));
    }

    /// <summary>The source showing the rows of <paramref name="source"/> its filter shows, kept in step with each
    /// <see cref="IFilteredRows.Changed"/>, which names the rows it touched, so a change costs only those rows and a row
    /// that stays keeps its container.</summary>
    /// <remarks>A set of filtered rows has one for as long as it lives, so asking again hands a list the source it already
    /// shows.</remarks>
    [FramePath]
    [return: NotNullIfNotNull(nameof(source))]
    public static IItemsSource? Matching(IFilteredRows? source) =>
        source is null
            ? null
            : MatchingSources.GetValue(source, static rows => new MatchingItemsSource(rows));

    /// <summary>As <see cref="Matching(IFilteredRows?)"/>, showing the <typeparamref name="T"/> rows that match. A list
    /// showing it takes no slot for a row that does not, and indexes from outside it are the filtered rows'.</summary>
    [FramePath]
    [return: NotNullIfNotNull(nameof(source))]
    public static IItemsSource<T>? Matching<T>(IFilteredRows? source)
        where T : class =>
        source is null ? null : TypedMatching<T>.Table.GetValue(source, TypedMatching<T>.Create);

    static readonly ConditionalWeakTable<IFilteredRows, IItemsSource> MatchingSources = new();

    static class TypedMatching<T>
        where T : class
    {
        public static readonly ConditionalWeakTable<IFilteredRows, IItemsSource<T>> Table = new();

        public static readonly ConditionalWeakTable<
            IFilteredRows,
            IItemsSource<T>
        >.CreateValueCallback Create = static rows => new MatchingItemsSource<T>(rows);
    }

    static class ValueAdapters<T>
        where T : struct
    {
        public static readonly ConditionalWeakTable<IReadOnlyList<T>, IItemsSource> Table = new();
    }

    static class Adapters<T>
    {
        public static readonly ConditionalWeakTable<IReadOnlyList<T>, IItemsSource<T>> Table =
            new();

        public static readonly ConditionalWeakTable<
            IReadOnlyList<T>,
            IItemsSource<T>
        >.CreateValueCallback Create = static items =>
            items switch
            {
                Tralys.Ui.Mvvm.ObservableList<T> tracked => new TrackedItemsSource<T>(tracked),
                IItemsSource untyped => new TypedItemsSource<T>(items, untyped),
                INotifyCollectionChanged => throw new ArgumentException(
                    $"{items.GetType()} raises INotifyCollectionChanged, which no list hears; hold the items in an ObservableList.",
                    nameof(items)
                ),
                _ => new ListItemsSource<T>(items),
            };
    }
}

sealed class ValueItemsSource<T>(IReadOnlyList<T> items) : IItemsSource
    where T : struct
{
    readonly ItemBoxes<T> _boxes = new();

    public int Count
    {
        [FramePath]
        get => items.Count;
    }

    public object? this[int index]
    {
        [FramePath]
        get => _boxes.Of(index, items[index]);
    }

    [FramePath]
    public int IndexOf(object? item)
    {
        if (item is not T typed)
            return -1;

        for (var i = 0; i < items.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(items[i], typed))
                return i;
        }
        return -1;
    }

    public void AddListener(IItemsSourceListener listener) { }

    public void RemoveListener(IItemsSourceListener listener) { }
}

abstract class ItemsSourceAdapter<T>(IReadOnlyList<T> items) : IItemsSource<T>
{
    readonly ItemBoxes<T> _boxes = new();

    public int Count
    {
        [FramePath]
        get
        {
            Track();
            return items.Count;
        }
    }

    public object? this[int index]
    {
        [FramePath]
        get
        {
            Track();
            return _boxes.Of(index, items[index]);
        }
    }

    [FramePath]
    public T ItemAt(int index)
    {
        Track();
        return items[index];
    }

    [FramePath]
    protected virtual void Track() { }

    [FramePath]
    public int IndexOf(object? item)
    {
        Track();
        var count = items.Count;
        if (item is null)
        {
            for (var i = 0; i < count; i++)
            {
                if (items[i] is null)
                    return i;
            }
            return -1;
        }

        if (item is not T typed)
            return -1;

        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < count; i++)
        {
            if (comparer.Equals(items[i], typed))
                return i;
        }
        return -1;
    }

    public abstract void AddListener(IItemsSourceListener listener);

    public abstract void RemoveListener(IItemsSourceListener listener);
}

abstract class ChangingItemsSource<T>(IReadOnlyList<T> items)
    : ItemsSourceAdapter<T>(items),
        IObservable
{
    ItemsSourceListeners _listeners;
    ObserverSet _observers;

    public ref ObserverSet Observers => ref _observers;

    public override void AddListener(IItemsSourceListener listener) => _listeners.Add(listener);

    public override void RemoveListener(IItemsSourceListener listener) =>
        _listeners.Remove(listener);

    [FramePath]
    protected override void Track() => Observation.Read(this, 0);

    [FramePath]
    protected void Raise(in ItemsChange change)
    {
        _listeners.Raise(this, change);
        _observers.Notify(this, 0);
    }
}

// Never heard from, so it keeps no listeners: one list, an empty array above all, is shared by every thread showing it.
sealed class ListItemsSource<T>(IReadOnlyList<T> items) : ItemsSourceAdapter<T>(items)
{
    public override void AddListener(IItemsSourceListener listener) =>
        ArgumentNullException.ThrowIfNull(listener);

    public override void RemoveListener(IItemsSourceListener listener) { }
}

sealed class TrackedItemsSource<T> : ChangingItemsSource<T>
{
    public TrackedItemsSource(Tralys.Ui.Mvvm.ObservableList<T> list)
        : base(list) => list.Subscribe(OnChanged);

    [FramePath]
    protected override void Track() { }

    [FramePath]
    void OnChanged(Tralys.Ui.Mvvm.ObservableList<T> _, in ListChange<T> change)
    {
        switch (change.Kind)
        {
            case ListChangeKind.Add:
                Raise(ItemsChange.Added(change.NewIndex, change.Count));
                break;
            case ListChangeKind.Remove:
                Raise(ItemsChange.Removed(change.OldIndex, change.Count));
                break;
            case ListChangeKind.Replace:
                Raise(ItemsChange.Replaced(change.NewIndex, change.Count));
                break;
            case ListChangeKind.Move:
                Raise(ItemsChange.Moved(change.OldIndex, change.NewIndex, change.Count));
                break;
            default:
                Raise(ItemsChange.Reset);
                break;
        }
    }
}

// Re-raised as this source's, so listeners see the source they were handed.
sealed class TypedItemsSource<T> : IItemsSource<T>, IItemsSourceListener
{
    readonly IReadOnlyList<T> _items;
    readonly IItemsSource _source;
    ItemsSourceListeners _listeners;

    public TypedItemsSource(IReadOnlyList<T> items, IItemsSource source)
    {
        _items = items;
        _source = source;
        source.AddListener(this);
    }

    public int Count
    {
        [FramePath]
        get => _source.Count;
    }

    public object? this[int index]
    {
        [FramePath]
        get => _source[index];
    }

    [FramePath]
    public T ItemAt(int index) => _items[index];

    [FramePath]
    public int IndexOf(object? item) => _source.IndexOf(item);

    public void AddListener(IItemsSourceListener listener) => _listeners.Add(listener);

    public void RemoveListener(IItemsSourceListener listener) => _listeners.Remove(listener);

    [FramePath]
    void IItemsSourceListener.OnItemsChanged(IItemsSource source, in ItemsChange change) =>
        _listeners.Raise(this, change);
}

// Shared by every thread showing the list: each read takes it once, and a box a race loses is only boxed again.
sealed class ItemBoxes<T>
{
    object?[] _boxes = [];

    [FramePath]
    public object? Of(int index, T value)
    {
        if (!typeof(T).IsValueType)
            return Unsafe.As<T, object?>(ref value);

        var boxes = _boxes;
        if (boxes.Length <= index)
            boxes = Grow(boxes, index + 1);

        var held = boxes[index];
        if (held is T kept && EqualityComparer<T>.Default.Equals(kept, value))
            return held;

        var box = Box(value);
        boxes[index] = box;
        return box;
    }

    [ColdPath]
    static object? Box(T value) => value;

    [FramePath]
    object?[] Grow(object?[] boxes, int needed)
    {
        var grown = new object?[Math.Max(needed, boxes.Length * 2)];
        Array.Copy(boxes, grown, boxes.Length);
        while (true)
        {
            var current = Interlocked.CompareExchange(ref _boxes, grown, boxes);
            if (ReferenceEquals(current, boxes))
                return grown;
            if (current.Length >= grown.Length)
                return current;
            boxes = current;
        }
    }
}

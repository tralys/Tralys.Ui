using System.Collections;
using System.Runtime.CompilerServices;

namespace Tralys.Ui.Mvvm;

public enum ListChangeKind : byte
{
    Add,
    Remove,
    Replace,
    Move,

    /// <summary>Anything else: the whole list is to be read again.</summary>
    Reset,
}

/// <summary>One change to an <see cref="ObservableList{T}"/>, which the list already shows. Its spans hold only while
/// the handler runs.</summary>
public readonly ref struct ListChange<T>
{
    internal ListChange(
        ListChangeKind kind,
        int oldIndex,
        int newIndex,
        ReadOnlySpan<T> oldItems,
        ReadOnlySpan<T> newItems
    )
    {
        Kind = kind;
        OldIndex = oldIndex;
        NewIndex = newIndex;
        OldItems = oldItems;
        NewItems = newItems;
    }

    public ListChangeKind Kind { get; }

    /// <summary>Where the removed, replaced or moved items were; -1 for an add or a reset.</summary>
    public int OldIndex { get; }

    /// <summary>Where the added, replaced or moved items are now; -1 for a remove or a reset.</summary>
    public int NewIndex { get; }

    /// <summary>The items removed or replaced, and for a reset every item the list held before it; empty for an add or
    /// a move.</summary>
    public ReadOnlySpan<T> OldItems { get; }

    /// <summary>The items added, put in place of others, or moved, and for a reset every item the list holds after it;
    /// empty for a remove.</summary>
    public ReadOnlySpan<T> NewItems { get; }

    /// <summary>How many items were added, removed, replaced or moved; 0 for a reset.</summary>
    public int Count => Kind == ListChangeKind.Remove ? OldItems.Length : NewItems.Length;
}

/// <summary>Hears each change of a list, as a struct whose spans hold only while it runs.</summary>
public delegate void ListChangedHandler<T>(ObservableList<T> list, in ListChange<T> change);

/// <summary>An <see cref="ObservableList{T}"/> as its owner hands it out: read, and heard changing, but changed only by
/// the owner.</summary>
public interface IReadOnlyObservableList<T> : IReadOnlyList<T>
{
    /// <summary>Tells <paramref name="handler"/> of every change from now on, until the returned subscription is
    /// disposed.</summary>
    IDisposable Subscribe(ListChangedHandler<T> handler);
}

/// <summary>A list whose reads are tracked: <see cref="Count"/>, the indexer and enumeration read its shape, and each
/// change raises one <see cref="ListChange{T}"/>, carrying the items it added or removed, and re-runs what read the
/// shape. It is changed only on the thread of the contexts observing it, and never by a handler hearing a
/// change.</summary>
public sealed class ObservableList<T> : IList<T>, IReadOnlyObservableList<T>, IObservable
{
    const int Shape = 0;

    T[] _items;
    int _count;
    int _version;
    T[] _removed = [];
    T[] _incoming = [];
    ListChangedHandler<T>? _changed;
    bool _raising;
    ObserverSet _observers;
    int[] _targets = [];
    int[] _tails = [];
    int[] _links = [];
    bool[] _settled = [];

    public ObservableList() => _items = [];

    public ObservableList(int capacity) => _items = new T[capacity];

    ref ObserverSet IObservable.Observers => ref _observers;

    public int Count
    {
        get
        {
            Observation.Read(this, Shape);
            return _count;
        }
    }

    bool ICollection<T>.IsReadOnly => false;

    internal ReadOnlySpan<T> Items => _items.AsSpan(0, _count);

    public T this[int index]
    {
        get
        {
            Observation.Read(this, Shape);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
                (uint)index,
                (uint)_count,
                nameof(index)
            );
            return _items[index];
        }
        set
        {
            VerifyWritable();
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
                (uint)index,
                (uint)_count,
                nameof(index)
            );
            ReplaceCore(index, 1, new ReadOnlySpan<T>(in value));
        }
    }

    /// <summary>Tells <paramref name="handler"/> of every change from now on, until the returned subscription is
    /// disposed.</summary>
    public IDisposable Subscribe(ListChangedHandler<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _changed += handler;
        return new Subscription(this, handler);
    }

    public void Add(T item) => Insert(_count, item);

    public void Insert(int index, T item) => InsertRange(index, new ReadOnlySpan<T>(in item));

    public void AddRange(ReadOnlySpan<T> items) => InsertRange(_count, items);

    /// <summary>Adds every one of <paramref name="items"/> at the end, as one change.</summary>
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        VerifyWritable();
        var start = _count;
        if (items is ICollection<T> collection)
        {
            EnsureCapacity(start + collection.Count);
            collection.CopyTo(_items, start);
            _count = start + collection.Count;
        }
        else
        {
            try
            {
                foreach (var item in items)
                {
                    EnsureCapacity(_count + 1);
                    _items[_count++] = item;
                    _version++;
                }
            }
            catch
            {
                Array.Clear(_items, start, _count - start);
                _count = start;
                throw;
            }
        }

        if (_count == start)
            return;
        _version++;
        Raise(ListChangeKind.Add, -1, start, [], _items.AsSpan(start, _count - start));
    }

    /// <summary>Inserts <paramref name="items"/> at <paramref name="index"/>, as one change.</summary>
    public void InsertRange(int index, ReadOnlySpan<T> items)
    {
        VerifyWritable();
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)index, (uint)_count, nameof(index));
        if (items.IsEmpty)
            return;
        InsertCore(index, items);
    }

    public void RemoveAt(int index) => RemoveRange(index, 1);

    /// <summary>Removes the first item equal to <paramref name="item"/>.</summary>
    /// <returns>Whether one was found.</returns>
    public bool Remove(T item)
    {
        var index = Array.IndexOf(_items, item, 0, _count);
        if (index < 0)
            return false;
        RemoveAt(index);
        return true;
    }

    /// <summary>Removes the <paramref name="count"/> items from <paramref name="index"/>, as one change.</summary>
    public void RemoveRange(int index, int count)
    {
        VerifyWritable();
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _count - count, nameof(index));
        if (count == 0)
            return;
        RemoveCore(index, count);
    }

    /// <summary>Moves the item at <paramref name="from"/> so that it ends at <paramref name="to"/>.</summary>
    public void Move(int from, int to)
    {
        VerifyWritable();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            (uint)from,
            (uint)_count,
            nameof(from)
        );
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)to, (uint)_count, nameof(to));
        if (from != to)
            MoveCore(from, to);
    }

    /// <summary>Removes every item, as one reset that carries them, so a list refilled before it next lays out keeps
    /// the rows of the items that come back.</summary>
    public void Clear()
    {
        VerifyWritable();
        var count = _count;
        if (count == 0)
            return;

        Grow(ref _removed, count);
        Array.Copy(_items, 0, _removed, 0, count);
        Array.Clear(_items, 0, count);
        _count = 0;
        _version++;
        try
        {
            Raise(ListChangeKind.Reset, -1, -1, _removed.AsSpan(0, count), []);
        }
        finally
        {
            ClearRemoved(count);
        }
    }

    /// <summary>Replaces every item with <paramref name="items"/>, as one reset that carries both, so a list showing them
    /// keeps the rows of the items that stay and handlers never see the list empty between.</summary>
    public void ReplaceAll(ReadOnlySpan<T> items)
    {
        VerifyWritable();
        var count = _count;
        if (count == 0 && items.IsEmpty)
            return;

        Grow(ref _removed, count);
        Array.Copy(_items, 0, _removed, 0, count);
        Array.Clear(_items, 0, count);
        EnsureCapacity(items.Length);
        items.CopyTo(_items);
        _count = items.Length;
        _version++;
        try
        {
            Raise(
                ListChangeKind.Reset,
                -1,
                -1,
                _removed.AsSpan(0, count),
                _items.AsSpan(0, _count)
            );
        }
        finally
        {
            ClearRemoved(count);
        }
    }

    /// <inheritdoc cref="ReplaceAll(ReadOnlySpan{T})"/>
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var count = 0;
        if (items is ICollection<T> collection)
        {
            Grow(ref _incoming, collection.Count);
            collection.CopyTo(_incoming, 0);
            count = collection.Count;
        }
        else
        {
            foreach (var item in items)
            {
                if (count == _incoming.Length)
                    Array.Resize(ref _incoming, Math.Max(4, count * 2));
                _incoming[count++] = item;
            }
        }

        try
        {
            ReplaceAll(_incoming.AsSpan(0, count));
        }
        finally
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                Array.Clear(_incoming, 0, count);
        }
    }

    /// <summary>Makes the list hold <paramref name="desired"/>, in its order. An item sharing its key with one of them
    /// stays, moved into place, and is handed to <paramref name="update"/> with it, or replaced by it where they differ
    /// when there is no <paramref name="update"/>; the other items leave, and the desired items no item shares a key
    /// with come in. Listeners hear only those changes, a run of them as one, unless more items would move than stay
    /// where they are, which they hear of as one reset.</summary>
    public void Sync<TKey>(
        ReadOnlySpan<T> desired,
        Func<T, TKey> keyOf,
        Action<T, T>? update = null
    )
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(keyOf);
        VerifyWritable();

        var wanted = SyncKeys<TKey>.Rent();
        try
        {
            for (var j = 0; j < desired.Length; j++)
                wanted.TryAdd(keyOf(desired[j]), j);

            Grow(ref _targets, Math.Max(_count, desired.Length));
            for (var i = 0; i < _count; i++)
            {
                var target = -1;
                var key = keyOf(_items[i]);
                if (wanted.TryGetValue(key, out var at) && at >= 0)
                {
                    target = at;
                    wanted[key] = -1;
                }
                _targets[i] = target;
            }

            RemoveUnwanted();
            if (!Reorder(desired, update))
                return;
            InsertWanted(desired);
            Refresh(desired, update);
        }
        finally
        {
            SyncKeys<TKey>.Return(wanted);
        }
    }

    public int IndexOf(T item)
    {
        Observation.Read(this, Shape);
        return Array.IndexOf(_items, item, 0, _count);
    }

    public bool Contains(T item) => IndexOf(item) >= 0;

    public void CopyTo(T[] array, int arrayIndex)
    {
        Observation.Read(this, Shape);
        Array.Copy(_items, 0, array, arrayIndex, _count);
    }

    public Enumerator GetEnumerator()
    {
        Observation.Read(this, Shape);
        return new Enumerator(this);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    void InsertCore(int index, ReadOnlySpan<T> items)
    {
        var count = items.Length;
        EnsureCapacity(_count + count);
        Array.Copy(_items, index, _items, index + count, _count - index);
        items.CopyTo(_items.AsSpan(index));
        _count += count;
        _version++;
        Raise(ListChangeKind.Add, -1, index, [], _items.AsSpan(index, count));
    }

    void RemoveCore(int index, int count)
    {
        Grow(ref _removed, count);
        Array.Copy(_items, index, _removed, 0, count);
        _count -= count;
        Array.Copy(_items, index + count, _items, index, _count - index);
        Array.Clear(_items, _count, count);
        _version++;
        try
        {
            Raise(ListChangeKind.Remove, index, -1, _removed.AsSpan(0, count), []);
        }
        finally
        {
            ClearRemoved(count);
        }
    }

    void ReplaceCore(int index, int count, ReadOnlySpan<T> items)
    {
        Grow(ref _removed, count);
        Array.Copy(_items, index, _removed, 0, count);
        items.CopyTo(_items.AsSpan(index));
        _version++;
        try
        {
            Raise(
                ListChangeKind.Replace,
                index,
                index,
                _removed.AsSpan(0, count),
                _items.AsSpan(index, count)
            );
        }
        finally
        {
            ClearRemoved(count);
        }
    }

    void MoveCore(int from, int to)
    {
        var item = _items[from];
        if (from < to)
            Array.Copy(_items, from + 1, _items, from, to - from);
        else
            Array.Copy(_items, to, _items, to + 1, from - to);
        _items[to] = item;
        _version++;
        Raise(ListChangeKind.Move, from, to, [], _items.AsSpan(to, 1));
    }

    void RemoveUnwanted()
    {
        var kept = 0;
        for (var end = _count; end > 0; )
        {
            if (_targets[end - 1] >= 0)
            {
                end--;
                continue;
            }

            var start = end - 1;
            while (start > 0 && _targets[start - 1] < 0)
                start--;
            RemoveCore(start, end - start);
            end = start;
        }

        for (var i = 0; i < _targets.Length && kept < _count; i++)
        {
            if (_targets[i] >= 0)
                _targets[kept++] = _targets[i];
        }
    }

    bool Reorder(ReadOnlySpan<T> desired, Action<T, T>? update)
    {
        var count = _count;
        var kept = KeepLongestRun(count);
        if (count - kept > kept)
        {
            ResetTo(desired, update);
            return false;
        }

        for (var pass = 0; pass < count; pass++)
        {
            var from = -1;
            for (var i = 0; i < count; i++)
            {
                if (!_settled[i] && (from < 0 || _targets[i] < _targets[from]))
                    from = i;
            }
            if (from < 0)
                break;

            var target = _targets[from];
            var to = count - 1;
            for (var i = 0; i < count; i++)
            {
                if (i != from && _settled[i] && _targets[i] > target)
                {
                    to = i > from ? i - 1 : i;
                    break;
                }
            }

            ShiftScratch(from, to, target);
            if (from != to)
                MoveCore(from, to);
        }
        return true;
    }

    void ShiftScratch(int from, int to, int target)
    {
        if (from < to)
        {
            Array.Copy(_targets, from + 1, _targets, from, to - from);
            Array.Copy(_settled, from + 1, _settled, from, to - from);
        }
        else if (to < from)
        {
            Array.Copy(_targets, to, _targets, to + 1, from - to);
            Array.Copy(_settled, to, _settled, to + 1, from - to);
        }
        _targets[to] = target;
        _settled[to] = true;
    }

    int KeepLongestRun(int count)
    {
        Grow(ref _tails, count);
        Grow(ref _links, count);
        Grow(ref _settled, count);
        Array.Clear(_settled, 0, count);

        var length = 0;
        for (var i = 0; i < count; i++)
        {
            var target = _targets[i];
            var low = 0;
            var high = length;
            while (low < high)
            {
                var middle = (low + high) >>> 1;
                if (_targets[_tails[middle]] < target)
                    low = middle + 1;
                else
                    high = middle;
            }
            _links[i] = low > 0 ? _tails[low - 1] : -1;
            _tails[low] = i;
            if (low == length)
                length++;
        }

        for (var i = length > 0 ? _tails[length - 1] : -1; i >= 0; i = _links[i])
            _settled[i] = true;
        return length;
    }

    void InsertWanted(ReadOnlySpan<T> desired)
    {
        var placed = 0;
        for (var j = 0; j < desired.Length; )
        {
            if (placed < _count && _targets[placed] == j)
            {
                placed++;
                j++;
                continue;
            }

            var end = j + 1;
            while (end < desired.Length && (placed >= _count || _targets[placed] != end))
                end++;
            InsertCore(j, desired[j..end]);
            Array.Copy(_targets, placed, _targets, placed + end - j, _count - (end - j) - placed);
            for (var k = j; k < end; k++)
                _targets[placed++] = -1;
            j = end;
        }
    }

    void Refresh(ReadOnlySpan<T> desired, Action<T, T>? update)
    {
        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < _count; )
        {
            if (_targets[i] < 0)
            {
                i++;
                continue;
            }

            if (update is not null)
            {
                Update(update, _items[i], desired[i]);
                i++;
                continue;
            }

            if (comparer.Equals(_items[i], desired[i]))
            {
                i++;
                continue;
            }

            var end = i + 1;
            while (
                end < _count && _targets[end] >= 0 && !comparer.Equals(_items[end], desired[end])
            )
                end++;
            ReplaceCore(i, end - i, desired[i..end]);
            i = end;
        }
    }

    void ResetTo(ReadOnlySpan<T> desired, Action<T, T>? update)
    {
        var count = _count;
        Grow(ref _links, desired.Length);
        Array.Fill(_links, -1, 0, desired.Length);
        for (var i = 0; i < count; i++)
            _links[_targets[i]] = i;

        if (update is not null)
        {
            for (var j = 0; j < desired.Length; j++)
            {
                if (_links[j] >= 0)
                    Update(update, _items[_links[j]], desired[j]);
            }
        }

        Grow(ref _removed, count);
        Array.Copy(_items, 0, _removed, 0, count);
        EnsureCapacity(desired.Length);
        Array.Clear(_items, 0, count);
        for (var j = 0; j < desired.Length; j++)
            _items[j] = update is null || _links[j] < 0 ? desired[j] : _removed[_links[j]];
        _count = desired.Length;
        _version++;
        try
        {
            Raise(
                ListChangeKind.Reset,
                -1,
                -1,
                _removed.AsSpan(0, count),
                _items.AsSpan(0, _count)
            );
        }
        finally
        {
            ClearRemoved(count);
        }
    }

    void Raise(
        ListChangeKind kind,
        int oldIndex,
        int newIndex,
        ReadOnlySpan<T> oldItems,
        ReadOnlySpan<T> newItems
    )
    {
        _raising = true;
        try
        {
            _observers.Notify(this, Shape);
            _changed?.Invoke(this, new ListChange<T>(kind, oldIndex, newIndex, oldItems, newItems));
        }
        finally
        {
            _raising = false;
        }
    }

    void Update(Action<T, T> update, T kept, T desired)
    {
        _raising = true;
        try
        {
            update(kept, desired);
        }
        finally
        {
            _raising = false;
        }
    }

    void VerifyWritable()
    {
        if (_raising)
            throw new InvalidOperationException(
                "An ObservableList changed while a handler heard of a change: make the change once it has, as by posting it."
            );
        if (_observers.IsOffThread())
            throw new InvalidOperationException(
                "An observed ObservableList changes only on its UI thread; post the change there with Dispatcher.Post."
            );
    }

    void EnsureCapacity(int needed)
    {
        if (_items.Length < needed)
            Array.Resize(ref _items, Math.Max(needed, Math.Max(4, _items.Length * 2)));
    }

    void ClearRemoved(int count)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            Array.Clear(_removed, 0, count);
    }

    static void Grow<TElement>(ref TElement[] array, int needed)
    {
        if (array.Length < needed)
            array = new TElement[Math.Max(needed, array.Length * 2)];
    }

    sealed class Subscription(ObservableList<T> list, ListChangedHandler<T> handler) : IDisposable
    {
        ObservableList<T>? _list = list;

        public void Dispose()
        {
            if (_list is null)
                return;
            _list._changed -= handler;
            _list = null;
        }
    }

    static class SyncKeys<TKey>
        where TKey : notnull
    {
        [ThreadStatic]
        static Dictionary<TKey, int>? t_keys;

        public static Dictionary<TKey, int> Rent()
        {
            var keys = t_keys ?? new Dictionary<TKey, int>();
            t_keys = null;
            return keys;
        }

        public static void Return(Dictionary<TKey, int> keys)
        {
            keys.Clear();
            t_keys = keys;
        }
    }

    /// <summary>Walks the list without allocating, and throws once the list changes under it.</summary>
    public struct Enumerator : IEnumerator<T>
    {
        readonly ObservableList<T> _list;
        readonly int _version;
        int _index;

        internal Enumerator(ObservableList<T> list)
        {
            _list = list;
            _version = list._version;
            _index = -1;
        }

        public readonly T Current => _list._items[_index];

        readonly object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_version != _list._version)
                throw new InvalidOperationException(
                    "The ObservableList changed while it was enumerated."
                );
            return ++_index < _list._count;
        }

        public void Reset() => _index = -1;

        public readonly void Dispose() { }
    }
}

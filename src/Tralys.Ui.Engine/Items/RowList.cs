using System.Collections;
using System.Runtime.InteropServices;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

/// <summary>The rows a list shows, replaced as a whole: <see cref="Add"/> lays out the next rows and
/// <see cref="Apply"/> shows them, telling the list only what changed between, so every row that stays keeps its
/// container, and the focus and selection on it.</summary>
/// <remarks>A row is known by reference. The most rows that can stay in the order they were in stay where they are, and
/// each other row that stays moves to its place, unless more rows would move than stay, which the list hears of as one
/// reset. Between two rows that stay where they are, new rows take the places of the rows that left, so a run of rows
/// filled again leaves the list scrolled where it was. Reading it is tracked, so a render that reads it renders again
/// as it applies a change.</remarks>
public sealed class RowList<T> : IItemsSource<T>, IReadOnlyList<T>, IObservable
    where T : class?
{
    readonly List<T> _rows = [];
    readonly List<T> _next = [];
    readonly Dictionary<object, int> _places = new(ReferenceEqualityComparer.Instance);
    int[] _targets = [];
    int[] _tails = [];
    int[] _links = [];
    int[] _tags = [];
    Fate[] _fates = [];
    ItemsSourceListeners _listeners;
    ObserverSet _observers;
    int _runStart;
    int _runLength;
    bool _runReplaces;
    int _removed;
    bool _applying;

    enum Fate : byte
    {
        New,
        Kept,
        Moving,
        Placed,
    }

    ref ObserverSet IObservable.Observers => ref _observers;

    public int Count
    {
        [FramePath]
        get
        {
            Observation.Read(this, 0);
            return _rows.Count;
        }
    }

    public T this[int index]
    {
        [FramePath]
        get
        {
            Observation.Read(this, 0);
            return _rows[index];
        }
    }

    object? IItemsSource.this[int index] => this[index];

    [FramePath]
    T IItemsSource<T>.ItemAt(int index) => this[index];

    [FramePath]
    public int IndexOf(object? item)
    {
        Observation.Read(this, 0);
        return item is T row ? _rows.IndexOf(row)
            : item is null ? _rows.IndexOf(default!)
            : -1;
    }

    public void AddListener(IItemsSourceListener listener) => _listeners.Add(listener);

    public void RemoveListener(IItemsSourceListener listener) => _listeners.Remove(listener);

    public List<T>.Enumerator GetEnumerator() => _rows.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => _rows.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _rows.GetEnumerator();

    /// <summary>Lays out <paramref name="row"/> after the rows added since the last <see cref="Apply"/>, to show once
    /// it runs.</summary>
    [FramePath]
    public void Add(T row)
    {
        ThrowIfApplying();
        _next.Add(row);
    }

    /// <summary>Shows the rows added since the last apply in place of those shown, none if none were added, and tells
    /// the listeners what changed.</summary>
    /// <returns>Whether the rows differ from those shown before.</returns>
    [FramePath]
    public bool Apply()
    {
        ThrowIfApplying();
        var shown = _rows.Count;
        var next = _next.Count;
        var prefix = 0;
        while (prefix < shown && prefix < next && ReferenceEquals(_rows[prefix], _next[prefix]))
            prefix++;

        var suffix = 0;
        while (
            suffix < shown - prefix
            && suffix < next - prefix
            && ReferenceEquals(_rows[shown - suffix - 1], _next[next - suffix - 1])
        )
            suffix++;

        var leaving = shown - prefix - suffix;
        var coming = next - prefix - suffix;
        if (leaving == 0 && coming == 0)
        {
            _next.Clear();
            return false;
        }

        _applying = true;
        try
        {
            if (leaving == 0)
                Insert(prefix, coming);
            else if (coming == 0)
                Remove(prefix, leaving);
            else
                Merge(prefix, leaving, coming);
        }
        finally
        {
            _applying = false;
            _places.Clear();
            _next.Clear();
        }
        return true;
    }

    [FramePath]
    void Merge(int prefix, int leaving, int coming)
    {
        var nextEnd = prefix + coming;
        ValueList.EnsureCapacity(ref _targets, leaving);
        ValueList.EnsureCapacity(ref _tails, leaving);
        ValueList.EnsureCapacity(ref _links, leaving);
        ValueList.EnsureCapacity(ref _tags, leaving);
        ValueList.EnsureCapacity(ref _fates, _next.Count);
        Array.Clear(_fates, prefix, coming);

        for (var j = prefix; j < nextEnd; j++)
        {
            if (_next[j] is { } row && !_places.TryAdd(row, j))
                _places[row] = -1;
        }

        var staying = 0;
        for (var i = 0; i < leaving; i++)
        {
            var target = -1;
            if (_rows[prefix + i] is { } row && _places.TryGetValue(row, out var at) && at >= 0)
            {
                target = at;
                _places[row] = -1;
                _fates[at] = Fate.Moving;
                staying++;
            }
            _targets[i] = target;
        }

        var kept = KeepLongestRun(leaving);
        if (staying - kept > kept)
        {
            _rows.Clear();
            _rows.AddRange(_next);
            Raise(ItemsChange.Reset);
            return;
        }

        var tagged = Settle(prefix, leaving, kept, nextEnd);
        MoveIntoPlace(prefix, nextEnd, tagged);
        for (var j = prefix; j < nextEnd; )
        {
            if (_fates[j] != Fate.New)
            {
                j++;
                continue;
            }

            var end = j + 1;
            while (end < nextEnd && _fates[end] == Fate.New)
                end++;
            Insert(j, end - j);
            j = end;
        }
    }

    [FramePath]
    int KeepLongestRun(int leaving)
    {
        var length = 0;
        for (var i = 0; i < leaving; i++)
        {
            var target = _targets[i];
            if (target < 0)
                continue;

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

        var at = length - 1;
        for (var i = length > 0 ? _tails[at] : -1; i >= 0; i = _links[i])
        {
            _tails[at--] = i;
            _fates[_targets[i]] = Fate.Kept;
        }
        return length;
    }

    [FramePath]
    int Settle(int prefix, int leaving, int kept, int nextEnd)
    {
        var tags = 0;
        var anchor = 0;
        var cursor = prefix;
        var gapEnd = kept > 0 ? _targets[_tails[0]] : nextEnd;
        _removed = 0;
        _runLength = 0;
        for (var i = 0; i < leaving; i++)
        {
            var target = _targets[i];
            if (target >= 0)
            {
                Flush();
                _tags[tags++] = target;
                if (anchor < kept && _tails[anchor] == i)
                {
                    cursor = target + 1;
                    anchor++;
                    gapEnd = anchor < kept ? _targets[_tails[anchor]] : nextEnd;
                }
                continue;
            }

            while (cursor < gapEnd && _fates[cursor] != Fate.New)
                cursor++;
            var replaces = cursor < gapEnd;
            if (_runLength > 0 && _runReplaces != replaces)
                Flush();

            var at = prefix + i - _removed;
            if (_runLength == 0)
            {
                _runStart = at;
                _runReplaces = replaces;
            }
            _runLength++;
            if (!replaces)
                continue;

            _rows[at] = _next[cursor];
            _fates[cursor] = Fate.Placed;
            _tags[tags++] = cursor++;
        }
        Flush();
        return tags;
    }

    [FramePath]
    void Flush()
    {
        if (_runLength == 0)
            return;

        var start = _runStart;
        var length = _runLength;
        _runLength = 0;
        if (_runReplaces)
        {
            Raise(ItemsChange.Replaced(start, length));
            return;
        }

        _rows.RemoveRange(start, length);
        _removed += length;
        Raise(ItemsChange.Removed(start, length));
    }

    [FramePath]
    void MoveIntoPlace(int prefix, int nextEnd, int tagged)
    {
        for (var j = prefix; j < nextEnd; j++)
        {
            if (_fates[j] != Fate.Moving)
                continue;

            var from = -1;
            var after = -1;
            for (var k = 0; k < tagged; k++)
            {
                var tag = _tags[k];
                if (tag == j)
                    from = k;
                else if (tag < j)
                    after = k;
            }

            var to = after < from ? after + 1 : after;
            if (to == from)
                continue;

            Shift(_tags.AsSpan(0, tagged), from, to);
            Shift(CollectionsMarshal.AsSpan(_rows).Slice(prefix), from, to);
            Raise(ItemsChange.Moved(prefix + from, prefix + to, 1));
        }
    }

    [FramePath]
    static void Shift<TItem>(Span<TItem> items, int from, int to)
    {
        var moved = items[from];
        if (from < to)
            items.Slice(from + 1, to - from).CopyTo(items.Slice(from));
        else
            items.Slice(to, from - to).CopyTo(items.Slice(to + 1));
        items[to] = moved;
    }

    [FramePath]
    void Insert(int at, int count)
    {
        var size = _rows.Count;
        CollectionsMarshal.SetCount(_rows, size + count);
        var rows = CollectionsMarshal.AsSpan(_rows);
        rows.Slice(at, size - at).CopyTo(rows.Slice(at + count));
        CollectionsMarshal.AsSpan(_next).Slice(at, count).CopyTo(rows.Slice(at));
        Raise(ItemsChange.Added(at, count));
    }

    [FramePath]
    void Remove(int at, int count)
    {
        _rows.RemoveRange(at, count);
        Raise(ItemsChange.Removed(at, count));
    }

    [FramePath]
    void Raise(in ItemsChange change)
    {
        _listeners.Raise(this, change);
        _observers.Notify(this, 0);
    }

    [FramePath]
    void ThrowIfApplying()
    {
        if (_applying)
            throw new InvalidOperationException(
                "Rows are laid out between applies, not while the listeners hear of one."
            );
    }
}

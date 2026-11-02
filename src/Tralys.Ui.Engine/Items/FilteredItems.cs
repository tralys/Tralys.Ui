namespace Tralys.Ui;

interface IItemFilter
{
    bool IsFiltering { [FramePath] get; }

    [FramePath]
    bool Matches(int index);
}

// Its holder hands it the source's changes, and refilters it as the filter's answers change.
sealed class FilteredItems(IItemFilter filter, IItemsSource? source) : IItemsSource
{
    IItemsSource? _source = source;
    int[] _map = [];
    int _front;
    int _back;
    bool _all = true;
    ItemsSourceListeners _listeners;

    public int Count
    {
        [FramePath]
        get => _all ? _source?.Count ?? 0 : _front + _back;
    }

    public object? this[int index]
    {
        [FramePath]
        get =>
            (uint)index < (uint)Count
                ? _source![SourceIndex(index)]
                : throw new ArgumentOutOfRangeException(nameof(index));
    }

    // Equal items match alike, so the source's first is the first shown.
    [FramePath]
    public int IndexOf(object? item) => _source is { } items ? ShownIndex(items.IndexOf(item)) : -1;

    public void AddListener(IItemsSourceListener listener) => _listeners.Add(listener);

    public void RemoveListener(IItemsSourceListener listener) => _listeners.Remove(listener);

    // While a refilter tells its runs, the shown items lie either side of a gap in the map.
    [FramePath]
    public int SourceIndex(int index) =>
        _all ? index
        : index < _front ? _map[index]
        : _map[_map.Length - _back + index - _front];

    [FramePath]
    public int ShownIndex(int index)
    {
        if (_all)
            return (uint)index < (uint)Count ? index : -1;

        var at = ShownBefore(index);
        return at < _front + _back && SourceIndex(at) == index ? at : -1;
    }

    [FramePath]
    public int ShownBefore(int index)
    {
        if (_all)
            return Math.Clamp(index, 0, Count);

        var low = 0;
        var high = _front + _back;
        while (low < high)
        {
            var middle = (low + high) >>> 1;
            if (SourceIndex(middle) < index)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    [FramePath]
    public void Follow(IItemsSource? source, in ItemsChange change)
    {
        if (change.Kind == ItemsChangeKind.Reset || !ReferenceEquals(source, _source))
        {
            Reset(source);
            return;
        }
        if (_all)
        {
            _listeners.Raise(this, change);
            return;
        }

        switch (change.Kind)
        {
            case ItemsChangeKind.Add:
                Insert(change.NewIndex, change.Count);
                break;
            case ItemsChangeKind.Remove:
                Remove(change.OldIndex, change.Count);
                break;
            case ItemsChangeKind.Replace:
                Replace(change.OldIndex, change.Count);
                break;
            default:
                Move(change);
                break;
        }
    }

    [FramePath]
    public void Refilter()
    {
        var filters = filter.IsFiltering;
        if (_all && !filters)
            return;

        var source = _source;
        var count = source?.Count ?? 0;
        ValueList.EnsureCapacity(ref _map, count);
        var map = _map;
        var end = map.Length;
        if (_all)
        {
            for (var i = 0; i < count; i++)
                map[end - count + i] = i;
            _back = count;
        }
        else
        {
            Array.Copy(map, 0, map, end - _front, _front);
            _back = _front;
        }
        _front = 0;
        _all = false;

        var runAt = 0;
        var run = 0;
        var adding = false;
        for (var i = 0; i < count; i++)
        {
            var was = _back > 0 && map[end - _back] == i;
            var now = !filters || filter.Matches(i);
            if (was == now)
            {
                if (!was)
                    continue;
                Flush(runAt, ref run, adding);
                _back--;
                map[_front++] = i;
                continue;
            }

            if (run > 0 && adding != now)
                Flush(runAt, ref run, adding);
            if (run == 0)
            {
                runAt = _front;
                adding = now;
            }
            run++;
            if (now)
                map[_front++] = i;
            else
                _back--;
        }
        Flush(runAt, ref run, adding);
        _all = !filters;
    }

    [FramePath]
    public void Recheck(int index)
    {
        if (_all)
        {
            Refilter();
            return;
        }

        var at = ShownBefore(index);
        var was = at < _front && _map[at] == index;
        if (was == filter.Matches(index))
            return;

        if (was)
        {
            Array.Copy(_map, at + 1, _map, at, _front - at - 1);
            _front--;
            _listeners.Raise(this, ItemsChange.Removed(at, 1));
            return;
        }

        ValueList.EnsureCapacity(ref _map, _front + 1);
        Array.Copy(_map, at, _map, at + 1, _front - at);
        _map[at] = index;
        _front++;
        _listeners.Raise(this, ItemsChange.Added(at, 1));
    }

    [FramePath]
    void Flush(int at, ref int run, bool adding)
    {
        if (run == 0)
            return;

        var length = run;
        run = 0;
        _listeners.Raise(
            this,
            adding ? ItemsChange.Added(at, length) : ItemsChange.Removed(at, length)
        );
    }

    [FramePath]
    void Reset(IItemsSource? source)
    {
        _source = source;
        _all = !filter.IsFiltering;
        _front = 0;
        _back = 0;
        if (!_all && source is not null)
        {
            var count = source.Count;
            ValueList.EnsureCapacity(ref _map, count);
            for (var i = 0; i < count; i++)
            {
                if (filter.Matches(i))
                    _map[_front++] = i;
            }
        }
        _listeners.Raise(this, ItemsChange.Reset);
    }

    [FramePath]
    void Insert(int index, int count)
    {
        var shown = _front;
        var at = ShownBefore(index);
        ValueList.EnsureCapacity(ref _map, shown + count);
        var map = _map;
        for (var i = shown - 1; i >= at; i--)
            map[i + count] = map[i] + count;

        var added = 0;
        for (var i = index; i < index + count; i++)
        {
            if (filter.Matches(i))
                map[at + added++] = i;
        }
        Array.Copy(map, at + count, map, at + added, shown - at);
        _front = shown + added;
        if (added > 0)
            _listeners.Raise(this, ItemsChange.Added(at, added));
    }

    [FramePath]
    void Remove(int index, int count)
    {
        var shown = _front;
        var at = ShownBefore(index);
        var end = ShownBefore(index + count);
        var map = _map;
        for (var i = end; i < shown; i++)
            map[at + i - end] = map[i] - count;

        _front = shown - (end - at);
        if (end > at)
            _listeners.Raise(this, ItemsChange.Removed(at, end - at));
    }

    [FramePath]
    void Replace(int index, int count)
    {
        var shown = _front;
        var at = ShownBefore(index);
        var end = ShownBefore(index + count);
        ValueList.EnsureCapacity(ref _map, at + count + shown - end);
        var map = _map;
        Array.Copy(map, end, map, at + count, shown - end);

        var now = 0;
        for (var i = index; i < index + count; i++)
        {
            if (filter.Matches(i))
                map[at + now++] = i;
        }
        Array.Copy(map, at + count, map, at + now, shown - end);

        var was = end - at;
        _front = shown - was + now;
        var kept = Math.Min(was, now);
        if (kept > 0)
            _listeners.Raise(this, ItemsChange.Replaced(at, kept));
        if (was > kept)
            _listeners.Raise(this, ItemsChange.Removed(at + kept, was - kept));
        else if (now > kept)
            _listeners.Raise(this, ItemsChange.Added(at + kept, now - kept));
    }

    [FramePath]
    void Move(in ItemsChange change)
    {
        var from = change.OldIndex;
        var to = change.NewIndex;
        if (from == to)
            return;

        var at = ShownBefore(from);
        var end = ShownBefore(from + change.Count);
        int start;
        int stop;
        int target;
        if (to < from)
        {
            start = ShownBefore(to);
            stop = end;
            target = start;
            Rotate(start, stop, at - start);
        }
        else
        {
            start = at;
            stop = ShownBefore(to + change.Count);
            target = at + stop - end;
            Rotate(start, stop, end - at);
        }

        var map = _map;
        for (var i = start; i < stop; i++)
            map[i] = change.Follow(map[i]);
        if (end > at && target != at)
            _listeners.Raise(this, ItemsChange.Moved(at, target, end - at));
    }

    [FramePath]
    void Rotate(int start, int stop, int by)
    {
        var span = _map.AsSpan(start, stop - start);
        span[..by].Reverse();
        span[by..].Reverse();
        span.Reverse();
    }
}

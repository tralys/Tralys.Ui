namespace Tralys.Ui;

// A mutable struct: keep it in a field that is not readonly, and never copy it.
[FramePath]
struct HandlerList<T>
{
    const int MinCapacity = 4;

    T[]? _items;
    int _count;
    int _raising;
    bool _holes;

    public readonly int Count => _count;

    public readonly bool IsRaising => _raising > 0;

    public readonly T this[int index] => _items![index];

    public void Add(T handler)
    {
        if (_items is null || _count == _items.Length)
            Array.Resize(ref _items, Math.Max(MinCapacity, _count * 2));
        _items![_count++] = handler;
    }

    public void RemoveAt(int index)
    {
        if (_raising > 0)
        {
            PunchHole(index);
            return;
        }

        var items = _items!;
        _count--;
        Array.Copy(items, index + 1, items, index, _count - index);
        items[_count] = default!;
    }

    public void RemoveAtSwap(int index)
    {
        if (_raising > 0)
        {
            PunchHole(index);
            return;
        }

        var items = _items!;
        items[index] = items[--_count];
        items[_count] = default!;
    }

    public int Enter()
    {
        _raising++;
        return _count;
    }

    public bool Exit()
    {
        if (--_raising > 0 || !_holes)
            return false;

        var items = _items!;
        var kept = 0;
        for (var i = 0; i < _count; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(items[i], default!))
                items[kept++] = items[i];
        }
        Array.Clear(items, kept, _count - kept);
        _count = kept;
        _holes = false;
        return true;
    }

    void PunchHole(int index)
    {
        _items![index] = default!;
        _holes = true;
    }
}

using System.Numerics;
using System.Runtime.CompilerServices;

namespace Tralys.Ui;

[FramePath]
static class SharedArrays<T>
{
    const int MinLength = 4;

    [ThreadStatic]
    static Stack<T[]>?[]? t_shelves;

    // Only its own size: one that took a bigger array would leave the next asking that size short.
    public static T[] Rent(int minimum)
    {
        var length = LengthFor(minimum);
        if (t_shelves?[BitOperations.Log2((uint)length)] is { Count: > 0 } shelf)
            return shelf.Pop();
        PoolMisses.Add();
        return new T[length];
    }

    public static void Return(T[] array)
    {
        if (array.Length < MinLength || !BitOperations.IsPow2(array.Length))
            return;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            Array.Clear(array);
        var shelves = t_shelves ??= new Stack<T[]>?[32];
        (shelves[BitOperations.Log2((uint)array.Length)] ??= new Stack<T[]>()).Push(array);
    }

    public static void Reserve(ref T[] array, int needed)
    {
        if (array.Length >= needed)
            return;
        var old = array;
        array = Rent(Math.Max(needed, old.Length * 2));
        Return(old);
    }

    public static void Grow(ref T[] array, int needed, int count)
    {
        if (array.Length >= needed)
            return;
        var old = array;
        array = Rent(Math.Max(needed, old.Length * 2));
        old.AsSpan(0, count).CopyTo(array);
        Return(old);
    }

    public static void Release(ref T[] array)
    {
        Return(array);
        array = [];
    }

    static int LengthFor(int minimum) =>
        minimum <= MinLength ? MinLength : (int)BitOperations.RoundUpToPowerOf2((uint)minimum);
}

// No span or ref into it may outlive a growth or a release: the array goes back to the shelf.
[FramePath]
struct SharedList<T>
{
    T[]? _items;
    int _count;

    public readonly int Count => _count;

    public readonly ref T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
                ThrowIndex();
            return ref _items![index];
        }
    }

    public readonly Span<T> AsSpan() => _items.AsSpan(0, _count);

    public void Add(T item)
    {
        Reserve(_count + 1);
        _items![_count++] = item;
    }

    public void Insert(int index, T item)
    {
        Reserve(_count + 1);
        Array.Copy(_items!, index, _items!, index + 1, _count - index);
        _items![index] = item;
        _count++;
    }

    public void RemoveAt(int index)
    {
        _count--;
        Array.Copy(_items!, index + 1, _items!, index, _count - index);
        _items![_count] = default!;
    }

    public readonly void Move(int from, int to)
    {
        var item = _items![from];
        if (from > to)
            Array.Copy(_items, to, _items, to + 1, from - to);
        else
            Array.Copy(_items, from + 1, _items, from, to - from);
        _items[to] = item;
    }

    public void Clear() => Truncate(0);

    public void Truncate(int count)
    {
        if (count >= _count)
            return;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            Array.Clear(_items!, count, _count - count);
        _count = count;
    }

    public void Release()
    {
        _count = 0;
        if (_items is null)
            return;
        SharedArrays<T>.Return(_items);
        _items = null;
    }

    public void TrimExcess()
    {
        if (_items is null || _items.Length <= 4 || _count * 4 > _items.Length)
            return;
        if (_count == 0)
        {
            Release();
            return;
        }
        var old = _items;
        _items = SharedArrays<T>.Rent(_count * 2);
        old.AsSpan(0, _count).CopyTo(_items);
        SharedArrays<T>.Return(old);
    }

    void Reserve(int needed)
    {
        if (_items is null)
            _items = SharedArrays<T>.Rent(needed);
        else
            SharedArrays<T>.Grow(ref _items, needed, _count);
    }

    static void ThrowIndex() => throw new IndexOutOfRangeException();
}

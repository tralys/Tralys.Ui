using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tralys.Ui;

static class ValueList
{
    [FramePath]
    public static void EnsureCapacity<T>(ref T[] array, int needed)
    {
        if (array.Length < needed)
            Array.Resize(ref array, Math.Max(needed, array.Length * 2));
    }
}

// A mutable struct: keep it in a field that is not readonly, and never copy it.
struct ValueList<T>
{
    const int MinCapacity = 4;

    T[]? _items;
    int _count;

    public ValueList(int capacity) => _items = new T[capacity];

    readonly T[] Items => _items ?? [];

    public readonly int Count
    {
        [FramePath]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    public readonly ref T this[int index]
    {
        [FramePath]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_count)
                ThrowIndex();
            // Reaches past the covariance check a reference type's element address would otherwise pay on every read.
            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Items), index);
        }
    }

    // A span taken before the list grows goes on showing the old array.
    [FramePath]
    public readonly Span<T> AsSpan() => _items.AsSpan(0, _count);

    [FramePath]
    public readonly Span<T> AsSpan(int start, int length) => AsSpan().Slice(start, length);

    [FramePath]
    public void Reserve(int capacity)
    {
        if ((_items?.Length ?? 0) < capacity)
            Array.Resize(ref _items, Math.Max(capacity, Math.Max(MinCapacity, _count * 2)));
    }

    [FramePath]
    public void Add(in T item)
    {
        if (_items is null || _count == _items.Length)
            Grow();
        _items[_count++] = item;
    }

    [FramePath]
    public ref T AddDefault()
    {
        if (_items is null || _count == _items.Length)
            Grow();
        _items.AsSpan(_count, 1).Clear();
        return ref _items[_count++];
    }

    [FramePath]
    public void Insert(int index, T item)
    {
        if (_items is null || _count == _items.Length)
            Grow();
        Array.Copy(_items, index, _items, index + 1, _count - index);
        _items[index] = item;
        _count++;
    }

    [FramePath]
    public void RemoveAt(int index)
    {
        var items = Items;
        Array.Copy(items, index + 1, items, index, _count - index - 1);
        Truncate(_count - 1);
    }

    [FramePath]
    public void RemoveAtSwap(int index)
    {
        var items = Items;
        items[index] = items[_count - 1];
        Truncate(_count - 1);
    }

    [FramePath]
    public T Pop()
    {
        var item = Items[_count - 1];
        Truncate(_count - 1);
        return item;
    }

    // Into a new array, leaving the old one intact for a pass already walking it.
    public void RemoveAtDetached(int index)
    {
        var old = Items;
        var items = new T[old.Length];
        Array.Copy(old, items, index);
        Array.Copy(old, index + 1, items, index, _count - index - 1);
        _items = items;
        _count--;
    }

    // Leaves the array intact for a pass already walking it.
    public void ClearDetached()
    {
        _items = null;
        _count = 0;
    }

    [FramePath]
    public readonly void Move(int from, int to)
    {
        var items = Items;
        var item = items[from];
        if (from > to)
            Array.Copy(items, to, items, to + 1, from - to);
        else
            Array.Copy(items, from + 1, items, from, to - from);
        items[to] = item;
    }

    [FramePath]
    public void Clear() => Truncate(0);

    [FramePath]
    public void Truncate(int count)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() && _count > count)
            Array.Clear(Items, count, _count - count);
        _count = count;
    }

    [FramePath]
    public void RemoveFirst(int count)
    {
        var kept = _count - count;
        if (kept > 0)
            Array.Copy(Items, count, Items, 0, kept);
        Truncate(kept);
    }

    [FramePath]
    public readonly int IndexOf(T item) => _count == 0 ? -1 : Array.IndexOf(Items, item, 0, _count);

    [MethodImpl(MethodImplOptions.NoInlining)]
    [FramePath]
    [MemberNotNull(nameof(_items))]
    void Grow() => Array.Resize(ref _items, Math.Max(MinCapacity, _count * 2));

    static void ThrowIndex() => throw new IndexOutOfRangeException();
}

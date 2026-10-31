using System.Numerics;
using System.Runtime.CompilerServices;

namespace Tralys.Ui.Mvvm;

[FramePath]
static class SharedArrays<T>
{
    const int MinLength = 4;

    [ThreadStatic]
    static Stack<T[]>?[]? t_shelves;

    public static T[] Rent(int minimum)
    {
        var length =
            minimum <= MinLength ? MinLength : (int)BitOperations.RoundUpToPowerOf2((uint)minimum);
        if (t_shelves?[BitOperations.Log2((uint)length)] is { Count: > 0 } shelf)
            return shelf.Pop();
        PoolMisses.Add();
        return new T[length];
    }

    public static void Return(T[] array)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            Array.Clear(array);
        var shelves = t_shelves ??= new Stack<T[]>?[32];
        (shelves[BitOperations.Log2((uint)array.Length)] ??= new Stack<T[]>()).Push(array);
    }
}

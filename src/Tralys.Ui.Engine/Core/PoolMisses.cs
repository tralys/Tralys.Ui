namespace Tralys.Ui;

// A rent a shared pool had to allocate for; a second walk of the same screens takes all it needs back.
internal static class PoolMisses
{
    [ThreadStatic]
    static long t_count;

    public static long Count => t_count;

    public static void Add() => t_count++;
}

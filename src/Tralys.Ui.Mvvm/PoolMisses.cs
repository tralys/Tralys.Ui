namespace Tralys.Ui.Mvvm;

// A rent one of this assembly's shared pools had to allocate for.
internal static class PoolMisses
{
    [ThreadStatic]
    static long t_count;

    public static long Count => t_count;

    public static void Add() => t_count++;
}

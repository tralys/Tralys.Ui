using System.Collections.Immutable;

namespace Tralys.Ui.CodeGen;

// An ImmutableArray compares by reference, so a pipeline step carrying one never reads as cached.
readonly struct EquatableArray<T>(ImmutableArray<T> items) : IEquatable<EquatableArray<T>>
    where T : IEquatable<T>
{
    public ImmutableArray<T> Items => items.IsDefault ? [] : items;

    public bool Equals(EquatableArray<T> other)
    {
        var mine = Items;
        var theirs = other.Items;
        if (mine.Length != theirs.Length)
            return false;

        for (var i = 0; i < mine.Length; i++)
        {
            if (!mine[i].Equals(theirs[i]))
                return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in Items)
            hash = unchecked((hash * 31) + item.GetHashCode());
        return hash;
    }
}

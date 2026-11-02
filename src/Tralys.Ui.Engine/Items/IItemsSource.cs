namespace Tralys.Ui;

/// <summary>The items an <c>ItemsControl</c> shows: a list read by index that tells its listeners of each change
/// once the change has happened. <see cref="ItemsSource.From{T}"/> adapts the collections Tralys.Ui knows; a collection
/// can also implement this itself, keeping its listeners in an <see cref="ItemsSourceListeners"/>, which lets a
/// listener stop or start listening while it is told of a change, and refuses a change raised while one is told of
/// another.</summary>
public interface IItemsSource
{
    [FramePath]
    int Count { get; }

    [FramePath]
    object? this[int index] { get; }

    /// <summary>Where <paramref name="item"/> first appears, or -1.</summary>
    [FramePath]
    int IndexOf(object? item);

    void AddListener(IItemsSourceListener listener);

    void RemoveListener(IItemsSourceListener listener);
}

/// <summary>Hears the changes of the <see cref="IItemsSource"/>s it listens to.</summary>
public interface IItemsSourceListener
{
    /// <summary>Runs after <paramref name="source"/> changed as <paramref name="change"/> says. It does not change the
    /// source, whose other listeners have yet to hear of this change; a change that follows from it is posted.</summary>
    [FramePath]
    void OnItemsChanged(IItemsSource source, in ItemsChange change);
}

public enum ItemsChangeKind : byte
{
    Add,
    Remove,
    Replace,
    Move,

    /// <summary>Anything else: the whole list is to be read again.</summary>
    Reset,
}

/// <summary>One change to an <see cref="IItemsSource"/>, which the source already shows.</summary>
public readonly struct ItemsChange
{
    ItemsChange(ItemsChangeKind kind, int oldIndex, int newIndex, int count)
    {
        Kind = kind;
        OldIndex = oldIndex;
        NewIndex = newIndex;
        Count = count;
    }

    public ItemsChangeKind Kind { get; }

    /// <summary>Where the removed, replaced or moved items were; -1 for an add or a reset.</summary>
    public int OldIndex { get; }

    /// <summary>Where the added, replaced or moved items are now; -1 for a remove or a reset.</summary>
    public int NewIndex { get; }

    /// <summary>How many items were added, removed, replaced or moved; 0 for a reset.</summary>
    public int Count { get; }

    public static ItemsChange Reset => new(ItemsChangeKind.Reset, -1, -1, 0);

    public static ItemsChange Added(int index, int count) =>
        new(ItemsChangeKind.Add, -1, index, count);

    public static ItemsChange Removed(int index, int count) =>
        new(ItemsChangeKind.Remove, index, -1, count);

    public static ItemsChange Replaced(int index, int count) =>
        new(ItemsChangeKind.Replace, index, index, count);

    public static ItemsChange Moved(int oldIndex, int newIndex, int count) =>
        new(ItemsChangeKind.Move, oldIndex, newIndex, count);

    [FramePath]
    internal int Follow(int index)
    {
        if (index < 0)
            return -1;

        var end = OldIndex + Count;
        switch (Kind)
        {
            case ItemsChangeKind.Add:
                return NewIndex <= index ? index + Count : index;
            case ItemsChangeKind.Remove when index >= end:
                return index - Count;
            case ItemsChangeKind.Remove or ItemsChangeKind.Replace:
                return index >= OldIndex && index < end ? -1 : index;
            case ItemsChangeKind.Move when index >= OldIndex && index < end:
                return index - OldIndex + NewIndex;
            case ItemsChangeKind.Move:
                if (index >= end)
                    index -= Count;
                return index >= NewIndex ? index + Count : index;
            default:
                return -1;
        }
    }
}

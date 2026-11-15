namespace Tralys.Ui.Docking;

public sealed class ActiveElementChangedEventArgs(DockElement? newElement) : EventArgs
{
    internal static readonly ActiveElementChangedEventArgs None = new(null);

    public DockElement? NewElement { get; } = newElement;
}

public sealed class FloatingWindowEventArgs(FloatingWindow window) : EventArgs
{
    public FloatingWindow Window { get; } = window;
}

public enum DockElementsChange
{
    Added,
    Removed,
    Moved,

    /// <summary>Anything else: the group's elements are to be read again.</summary>
    Reset,
}

/// <summary>One change to a <see cref="DockGroup"/>'s elements: <see cref="Element"/> added at, removed from or moved
/// to <see cref="Index"/>, moved from <see cref="OldIndex"/>.</summary>
public sealed class DockElementsChangedEventArgs(
    DockElementsChange change,
    DockElement? element = null,
    int index = -1,
    int oldIndex = -1
) : EventArgs
{
    public DockElementsChange Change { get; } = change;

    public DockElement? Element { get; } = element;

    public int Index { get; } = index;

    public int OldIndex { get; } = oldIndex;
}

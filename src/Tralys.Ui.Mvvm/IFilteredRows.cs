namespace Tralys.Ui.Mvvm;

/// <summary>Rows a filter shows or hides in place: every row stays among <see cref="Rows"/>, the source keeps whether
/// the filter shows each, and each change names the rows it touched, so a list showing them does work only for
/// those.</summary>
public interface IFilteredRows
{
    /// <summary>Every row, shown or hidden.</summary>
    IReadOnlyList<object> Rows { get; }

    /// <summary>How many rows the filter shows.</summary>
    int ShownCount { get; }

    /// <summary>Whether the filter shows the row at <paramref name="index"/>.</summary>
    bool IsShown(int index);

    /// <summary>After each change to the rows or to which of them the filter shows.</summary>
    event RowsChangedHandler? Changed;
}

/// <summary>Hears each change of an <see cref="IFilteredRows"/>.</summary>
public delegate void RowsChangedHandler(IFilteredRows rows, in RowChange change);

public enum RowChangeKind : byte
{
    Add,
    Remove,
    Replace,
    Move,

    /// <summary>Whether the filter shows the rows in the range may have changed; the rows did not.</summary>
    Match,

    /// <summary>Anything else: every row is to be read again.</summary>
    Reset,
}

/// <summary>One change to an <see cref="IFilteredRows"/>, which the source already shows.</summary>
public readonly struct RowChange
{
    RowChange(RowChangeKind kind, int oldIndex, int newIndex, int count)
    {
        Kind = kind;
        OldIndex = oldIndex;
        NewIndex = newIndex;
        Count = count;
    }

    public RowChangeKind Kind { get; }

    /// <summary>Where the removed, replaced, moved or rematched rows were; -1 for an add or a reset.</summary>
    public int OldIndex { get; }

    /// <summary>Where the added, replaced, moved or rematched rows are now; -1 for a remove or a reset.</summary>
    public int NewIndex { get; }

    /// <summary>How many rows the change touched; 0 for a reset.</summary>
    public int Count { get; }

    public static RowChange Reset => new(RowChangeKind.Reset, -1, -1, 0);

    public static RowChange Added(int index, int count) => new(RowChangeKind.Add, -1, index, count);

    public static RowChange Removed(int index, int count) =>
        new(RowChangeKind.Remove, index, -1, count);

    public static RowChange Replaced(int index, int count) =>
        new(RowChangeKind.Replace, index, index, count);

    public static RowChange Moved(int oldIndex, int newIndex, int count) =>
        new(RowChangeKind.Move, oldIndex, newIndex, count);

    public static RowChange Matched(int index, int count) =>
        new(RowChangeKind.Match, index, index, count);
}

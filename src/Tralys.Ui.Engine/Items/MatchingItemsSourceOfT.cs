using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

interface IShownIndexes
{
    [FramePath]
    int ShownIndex(int sourceIndex);
}

sealed class MatchingItemsSource<T> : MatchingRows, IItemsSource<T>, IShownIndexes
    where T : class
{
    public MatchingItemsSource(IFilteredRows source)
        : base(source) { }

    public object? this[int index]
    {
        [FramePath]
        get => Shown(index);
    }

    [FramePath]
    public T ItemAt(int index) =>
        Shown(index) as T
        ?? throw new InvalidOperationException("Only a row that is a T is shown.");

    [FramePath]
    protected override bool Matches(IFilteredRows source, int index) =>
        source.Rows[index] is T && source.IsShown(index);
}

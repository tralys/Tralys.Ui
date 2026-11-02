namespace Tralys.Ui;

/// <summary>An <see cref="IItemsSource"/> whose items read as <typeparamref name="T"/>, so a struct item reads without
/// boxing. <see cref="ItemsSource.From{T}"/> and <see cref="ItemsSource.Matching{T}"/> make one.</summary>
public interface IItemsSource<T> : IItemsSource
{
    /// <summary>The item at <paramref name="index"/>.</summary>
    [FramePath]
    T ItemAt(int index);
}

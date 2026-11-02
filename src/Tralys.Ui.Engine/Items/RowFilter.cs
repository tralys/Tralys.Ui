namespace Tralys.Ui.Controls;

/// <summary>Which rows a <c>DataTable</c> holding its rows, or a <see cref="TreeRows{T}"/>, shows. What it matches
/// is the view model's to change, which then calls <see cref="Refresh"/> for every table or tree showing it to filter
/// again.</summary>
public abstract class RowFilter
{
    internal event Action? Refreshed;

    [FramePath]
    public abstract bool Matches(object? row);

    /// <summary>Has every table in a view showing the filter filter its rows again, once its layout settles, and every
    /// tree with listeners lay its rows out again at once.</summary>
    [FramePath]
    public void Refresh() => Refreshed?.Invoke();
}

/// <summary>A <see cref="RowFilter"/> showing the <typeparamref name="TRow"/> rows <c>matches</c> accepts.</summary>
public sealed class RowFilter<TRow>(Func<TRow, bool> matches) : RowFilter
    where TRow : class
{
    readonly Func<TRow, bool> _matches =
        matches ?? throw new ArgumentNullException(nameof(matches));

    [FramePath]
    public override bool Matches(object? row) => row is TRow typed && _matches(typed);
}

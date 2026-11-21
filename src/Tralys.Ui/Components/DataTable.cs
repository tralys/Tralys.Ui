using Tralys.Ui.Media;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct DataTableProps<TItem>
{
    public IItemsSource<TItem>? Items { get; init; }

    public KeyOf<TItem>? ItemKey { get; init; }

    public TableColumn<TItem>[]? Columns { get; init; }

    /// <summary>The page shown, from 1. The pager reports the page it turns to through <see cref="OnPageChange"/>, and
    /// so does the table as too few rows leave it on the last there is, or, while it holds its rows, as a new sort,
    /// search, filter or <see cref="Items"/> takes it back to the first.</summary>
    public int Page { get; init; }

    public Callback<int> OnPageChange { get; init; }

    /// <summary>The rows a page shows; 0 shows every row on one page.</summary>
    public int PageSize { get; init; }

    /// <summary>How many rows there are in all while the app pages them: <see cref="Items"/> is then the page's rows,
    /// shown as given. Null, the table holds every row in <see cref="Items"/>, and filters, sorts and pages them
    /// itself.</summary>
    public int? TotalItems { get; init; }

    public TableSort Sort { get; init; }

    /// <summary>A header click's sort; with none, headers don't sort.</summary>
    public Callback<TableSort> OnSortChange { get; init; }

    /// <summary>Shows only the rows where some column's <see cref="TableColumn{TItem}.Text"/> holds it, ignoring case,
    /// while the table holds its rows; while the app pages them, the search the page answers, so an empty page under
    /// one shows <see cref="NoMatchText"/>.</summary>
    public TextValue SearchText { get; init; }

    /// <summary>Shows only the rows it accepts, while the table holds its rows. What it reads as it runs is tracked, so a
    /// change there filters again.</summary>
    public Predicate<TItem>? Filter { get; init; }

    /// <summary>Whether rows are still arriving: a table with none isn't empty yet, and one with too few for
    /// <see cref="Page"/> stays there, showing what it has.</summary>
    public bool Loading { get; init; }

    public TextValue EmptyText { get; init; }

    public ImageSource EmptyEmblem { get; init; }

    /// <summary>Shown in place of <see cref="EmptyText"/> while the rows are there but none matches the search or
    /// filter.</summary>
    public TextValue NoMatchText { get; init; }

    public UiKey Value { get; init; }

    public Callback<UiKey> OnValueChange { get; init; }

    public bool Selectable { get; init; }

    public bool Resizable { get; init; }

    public bool? Reorderable { get; init; }

    public ComponentSize? Size { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A <see cref="Table{TItem}"/> over a pager, a page of its rows at a time, with an empty state over it while
/// it has no rows.</summary>
/// <remarks>Given no <see cref="DataTableProps{TItem}.TotalItems"/> it holds its rows: it shows those
/// <see cref="DataTableProps{TItem}.Filter"/> accepts and whose text holds
/// <see cref="DataTableProps{TItem}.SearchText"/>, ordered by the column <see cref="DataTableProps{TItem}.Sort"/> names,
/// keeping tied rows in their order. A new page, sort, search, filter or page size scrolls the rows back to the top.
/// The pager, a rule above it, shows only while there's more than one page. Its rows never size it: it stands at its
/// height, or the height its caller grows it to.</remarks>
public sealed partial class DataTable<TItem> : Component<DataTableProps<TItem>>
{
    protected override void Render(Ui ui, in DataTableProps<TItem> p)
    {
        var items = p.Items ?? throw Missing(nameof(p.Items));
        var itemKey = p.ItemKey ?? throw Missing(nameof(p.ItemKey));
        var columns = p.Columns ?? throw Missing(nameof(p.Columns));
        var model = ui.Remember(
            (Changed: ui.State(0), Scroll: ui.ScrollHandle()),
            static hooks => new DataTableModel<TItem>(hooks.Changed, hooks.Scroll)
        );
        model.Host = ui.Self(p);
        var holds = p.TotalItems is null;
        model.Listen(holds ? items : null);
        var total =
            p.TotalItems ?? model.Rows.Query(items, columns, p.SearchText, p.Filter, p.Sort);
        var pages = DataTableModel<TItem>.PagesFor(total, p.PageSize);
        var page = Math.Clamp(
            Math.Max(1, p.Page),
            1,
            p.Loading && holds && p.PageSize > 0 ? int.MaxValue : pages
        );
        if (holds)
        {
            var perPage = p.PageSize > 0 ? p.PageSize : Math.Max(total, 1);
            var start = (page - 1) * perPage;
            model.Rows.Show(start, Math.Clamp(total - start, 0, perPage));
        }
        var query = model.Query(holds ? items : model, p.SearchText, p.Filter, p.Sort);
        ui.Effect(
            model,
            new DataTablePage(query, page, p.PageSize, holds),
            static (m, d, _) => m.Settle(d)
        );
        var empty = !p.Loading && (holds ? total == 0 : items.Count == 0);
        using (ui.Box(direction: Direction.Column, width: p.Width, height: p.Height))
        {
            using (ui.Box(direction: Direction.Column, grow: 1f, basis: 0f))
            {
                ui.Table(
                    items: holds ? model.Rows : items,
                    itemKey: itemKey,
                    columns: columns,
                    value: p.Value,
                    onValueChange: p.OnValueChange,
                    selectable: p.Selectable,
                    sort: p.Sort,
                    onSortChange: p.OnSortChange,
                    resizable: p.Resizable,
                    reorderable: p.Reorderable,
                    size: p.Size,
                    scroll: model.Scroll,
                    grow: 1f,
                    basis: 0f
                );
                if (empty)
                    using (
                        ui.Box(
                            align: Align.Center,
                            justify: Justify.Center,
                            pointerEvents: PointerEvents.None,
                            position: Position.Absolute,
                            left: 0f,
                            top: 0f,
                            right: 0f,
                            bottom: 0f
                        )
                    )
                        ui.EmptyState(
                            text: p.EmptyText,
                            emblem: p.EmptyEmblem,
                            noMatchText: p.NoMatchText,
                            searchMiss: holds ? items.Count > 0 : !p.SearchText.Span.IsEmpty
                        );
            }
            if (pages > 1)
            {
                ui.Separator();
                ui.Pagination(
                    value: page,
                    onValueChange: p.OnPageChange,
                    total: total,
                    pageSize: p.PageSize,
                    margin: new Thickness(8f)
                );
            }
        }
    }

    static ArgumentException Missing(string prop) => new($"A data table needs its {prop}.", prop);
}

internal readonly record struct DataTablePage(int Query, int Page, int PageSize, bool Holds);

[FramePath]
internal sealed class DataTableModel<TItem> : IItemsSourceListener, IDisposable
{
    internal readonly DataTableRows<TItem> Rows = new();
    internal Host<DataTableProps<TItem>> Host;
    internal readonly State<int> Changed;
    internal readonly ScrollHandle Scroll;
    IItemsSource<TItem>? _listening;
    object? _items;
    object? _filter;
    TableSort _sort;
    char[] _search = [];
    int _searchLength = -1;
    int _query;
    DataTablePage _settled = new(-1, 0, 0, false);

    internal DataTableModel(State<int> changed, ScrollHandle scroll)
    {
        Changed = changed;
        Scroll = scroll;
    }

    internal static int PagesFor(int total, int perPage) =>
        perPage <= 0 ? 1 : Math.Max(1, (total + perPage - 1) / perPage);

    internal void Listen(IItemsSource<TItem>? items)
    {
        if (ReferenceEquals(items, _listening))
            return;
        _listening?.RemoveListener(this);
        _listening = items;
        items?.AddListener(this);
    }

    void IItemsSourceListener.OnItemsChanged(IItemsSource source, in ItemsChange change)
    {
        if (!ReferenceEquals(source, _listening))
            return;
        Rows.Stale();
        Changed.Value = Changed.Peek + 1;
    }

    /// <summary>The query's number, which moves on whenever what the rows are drawn from, or how, changes.</summary>
    internal int Query(object items, TextValue search, object? filter, TableSort sort)
    {
        var text = search.Span;
        var searched = search.IsNull ? -1 : text.Length;
        if (
            ReferenceEquals(items, _items)
            && ReferenceEquals(filter, _filter)
            && sort == _sort
            && searched == _searchLength
            && text.SequenceEqual(_search.AsSpan(0, Math.Max(0, searched)))
        )
            return _query;
        if (_items is not null)
            _query++;
        _items = items;
        _filter = filter;
        _sort = sort;
        _searchLength = searched;
        if (_search.Length < text.Length)
            _search = new char[Math.Max(text.Length, _search.Length * 2)];
        text.CopyTo(_search);
        return _query;
    }

    internal void Settle(DataTablePage now)
    {
        var before = _settled;
        _settled = now;
        if (before.Query < 0)
            return;
        var requeried = now.Query != before.Query;
        if (requeried || now.Page != before.Page || now.PageSize != before.PageSize)
            Scroll.ScrollTo(null, 0f);
        var want = requeried && now.Holds ? 1 : now.Page;
        ref readonly var p = ref Host.Props;
        if (want != Math.Max(1, p.Page))
            p.OnPageChange.Invoke(want);
    }

    public void Dispose()
    {
        _listening?.RemoveListener(this);
        _listening = null;
    }
}

/// <summary>The rows a <see cref="DataTable{TItem}"/> holding its rows shows: a page of those its search and filter
/// match, in its sort's order, ties in their source order.</summary>
[FramePath]
internal sealed class DataTableRows<TItem> : IItemsSource<TItem>
{
    IItemsSource<TItem>? _source;
    int[] _order = [];
    int[] _merged = [];
    int[] _window = [];
    int _matched;
    int _start;
    int _count;
    bool _stale = true;
    ItemsSourceListeners _listeners;

    public int Count => _count;

    public object? this[int index] =>
        _source is { } source
            ? source[_order[_start + index]]
            : throw new ArgumentOutOfRangeException(nameof(index));

    public TItem ItemAt(int index) =>
        _source is { } source
            ? source.ItemAt(_order[_start + index])
            : throw new ArgumentOutOfRangeException(nameof(index));

    public int IndexOf(object? item)
    {
        for (var i = 0; i < _count; i++)
        {
            if (Equals(this[i], item))
                return i;
        }
        return -1;
    }

    public void AddListener(IItemsSourceListener listener) => _listeners.Add(listener);

    public void RemoveListener(IItemsSourceListener listener) => _listeners.Remove(listener);

    internal void Stale() => _stale = true;

    /// <returns>How many rows match.</returns>
    internal int Query(
        IItemsSource<TItem> source,
        TableColumn<TItem>[] columns,
        TextValue search,
        Predicate<TItem>? filter,
        TableSort sort
    )
    {
        if (!ReferenceEquals(source, _source))
            _stale = true;
        _source = source;
        var count = source.Count;
        if (_order.Length < count)
            _order = new int[Math.Max(count, _order.Length * 2)];
        var text = search.Span;
        _matched = 0;
        for (var i = 0; i < count; i++)
        {
            var row = source.ItemAt(i);
            if (filter is not null && !filter(row))
                continue;
            if (!text.IsEmpty && !Holds(row, columns, text))
                continue;
            _order[_matched++] = i;
        }
        if (sort.Direction != SortDirection.None && SortedBy(columns, sort.Key) is { } column)
            Sort(new Ordering(source, column, sort.Direction == SortDirection.Descending));
        return _matched;
    }

    internal void Show(int start, int count)
    {
        if (!_stale && start == _start && count == _count)
        {
            var same = true;
            for (var i = 0; i < count && same; i++)
                same = _window[i] == _order[start + i];
            if (same)
                return;
        }
        _stale = false;
        _start = start;
        _count = count;
        if (_window.Length < count)
            _window = new int[Math.Max(count, _window.Length * 2)];
        if (count > 0)
            _order.AsSpan(start, count).CopyTo(_window);
        _listeners.Raise(this, ItemsChange.Reset);
    }

    static bool Holds(TItem row, TableColumn<TItem>[] columns, ReadOnlySpan<char> text)
    {
        for (var i = 0; i < columns.Length; i++)
        {
            if (
                columns[i].Text is { } shown
                && shown(row).Span.Contains(text, StringComparison.OrdinalIgnoreCase)
            )
                return true;
        }
        return false;
    }

    static TableColumn<TItem>? SortedBy(TableColumn<TItem>[] columns, UiKey key)
    {
        for (var i = 0; i < columns.Length; i++)
        {
            if (columns[i].SortKey == key)
                return columns[i];
        }
        return null;
    }

    // A bottom-up merge sort over a buffer kept between sorts, as the runtime's sorts allocate a comparer each call.
    void Sort(in Ordering by)
    {
        var count = _matched;
        if (_merged.Length < _order.Length)
            _merged = new int[_order.Length];
        var from = _order;
        var to = _merged;
        for (var width = 1; width < count; width *= 2)
        {
            for (var start = 0; start < count; start += 2 * width)
            {
                var middle = Math.Min(start + width, count);
                var end = Math.Min(start + 2 * width, count);
                var left = start;
                var right = middle;
                for (var at = start; at < end; at++)
                    to[at] =
                        left < middle && (right >= end || by.Compare(from[left], from[right]) <= 0)
                            ? from[left++]
                            : from[right++];
            }
            (from, to) = (to, from);
        }
        if (!ReferenceEquals(from, _order))
            Array.Copy(from, _order, count);
    }

    readonly struct Ordering(IItemsSource<TItem> source, TableColumn<TItem> column, bool descending)
    {
        public int Compare(int x, int y)
        {
            var a = source.ItemAt(x);
            var b = source.ItemAt(y);
            var by = column.Compare is { } compare ? compare(a, b) : CompareText(a, b);
            if (by == 0)
                return x.CompareTo(y);
            return descending ? -by : by;
        }

        int CompareText(TItem a, TItem b)
        {
            if (column.Text is not { } text)
                return 0;
            var left = text(a).Span;
            var right = text(b).Span;
            var folded = left.CompareTo(right, StringComparison.OrdinalIgnoreCase);
            return folded != 0 ? folded : left.CompareTo(right, StringComparison.Ordinal);
        }
    }
}

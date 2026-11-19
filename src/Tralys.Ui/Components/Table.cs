using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>Which way a table's rows are ordered by its sort key.</summary>
public enum SortDirection : byte
{
    None,
    Ascending,
    Descending,
}

/// <summary>What a table is sorted by: the <see cref="TableColumn{TItem}.SortKey"/> of one of its columns, and which
/// way.</summary>
public readonly record struct TableSort(UiKey Key, SortDirection Direction);

/// <summary>One column of a <see cref="Table{TItem}"/>: its header, its width, and what each row shows in it.</summary>
public sealed class TableColumn<TItem>
{
    public TextValue Header { get; init; }

    /// <summary>What the header shows in place of <see cref="Header"/>.</summary>
    public Slot HeaderContent { get; init; }

    /// <summary>What an info icon beside the header explains; with none, the header shows no icon.</summary>
    public TextValue Info { get; init; }

    /// <summary>What a click on the header sorts by; unkeyed, the header doesn't sort.</summary>
    public UiKey SortKey { get; init; }

    /// <summary>The width every row gives the column; null fits it to the widest header or cell shown so far, which it
    /// never narrows from.</summary>
    public float? Width { get; init; }

    /// <summary>Whether the column grows, sharing equally with the table's other fill columns the width the rest leave
    /// in the rows' viewport, never narrower than its own.</summary>
    public bool Fill { get; init; }

    /// <summary>The text each row shows in the column while it has no <see cref="Cell"/>; also what a
    /// <see cref="DataTable{TItem}"/> searches, and sorts by without <see cref="Compare"/>.</summary>
    public TextOf<TItem>? Text { get; init; }

    /// <summary>What each row shows in the column.</summary>
    public Slot<TItem> Cell { get; init; }

    /// <summary>How a <see cref="DataTable{TItem}"/> holding its rows orders them by the column; by
    /// <see cref="Text"/>, ignoring case, without it.</summary>
    public Comparison<TItem>? Compare { get; init; }
}

/// <summary>How one row of a <see cref="Table{TItem}"/> draws where it differs from the rest.</summary>
public readonly record struct TableRowLook
{
    /// <summary>A fill under the row while it is neither selected nor lit, such as a group row's band.</summary>
    public Paint Background { get; init; }

    /// <summary>The row's opacity, its cells' controls included, which stay enabled, as a row that is off but
    /// editable dims; 1 when null.</summary>
    public float? Opacity { get; init; }
}

public readonly record struct TableProps<TItem>
{
    public IItemsSource<TItem>? Items { get; init; }

    public KeyOf<TItem>? ItemKey { get; init; }

    public TableColumn<TItem>[]? Columns { get; init; }

    /// <summary>The selected row's key, shown selected while <see cref="Selectable"/>.</summary>
    public UiKey Value { get; init; }

    public Callback<UiKey> OnValueChange { get; init; }

    /// <summary>Whether a press or an arrow selects a row; off, rows only light under the pointer.</summary>
    public bool Selectable { get; init; }

    /// <summary>Enter on the focused row, or a double click on one, as a list's item activation.</summary>
    public Callback<UiKey> OnRowActivate { get; init; }

    public TableSort Sort { get; init; }

    /// <summary>A click on a header with a sort key reports its sort here: descending first, then ascending, then
    /// none. With none, headers don't sort.</summary>
    public Callback<TableSort> OnSortChange { get; init; }

    /// <summary>Whether a gripper at each header's right edge drags its column wider or narrower.</summary>
    public bool Resizable { get; init; }

    /// <summary>Whether a header dragged along the header row moves its column there; true by default.</summary>
    public bool? Reorderable { get; init; }

    /// <summary>Whether columns wider than the table scroll sideways, the header row with the rows; true by default.
    /// Off, they're clipped.</summary>
    public bool? ScrollsSideways { get; init; }

    /// <summary>When the bar down the rows' right edge, under the header row, shows; while it does, the rows stop short
    /// of it.</summary>
    public ScrollbarVisibility VerticalScrollbar { get; init; }

    /// <summary>When the bar along the table's bottom edge shows, while <see cref="ScrollsSideways"/> columns wider than
    /// the table scroll; while it does, the rows stop short of it.</summary>
    public ScrollbarVisibility HorizontalScrollbar { get; init; }

    /// <summary>The rows' size; <see cref="ComponentSize.Sm"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    /// <summary>What the table shows over its rows while it has none.</summary>
    public TextValue EmptyText { get; init; }

    /// <summary>What a row <see cref="IsFullRow"/> accepts shows across every column, in place of its cells.</summary>
    public Slot<TItem> FullRow { get; init; }

    public Predicate<TItem>? IsFullRow { get; init; }

    /// <summary>The inset of what a full row shows; the cells' own, 6 each side, by default.</summary>
    public Thickness? FullRowInset { get; init; }

    /// <summary>Rows it accepts are disabled: greyed out, and never selected or focused.</summary>
    public Predicate<TItem>? IsRowDisabled { get; init; }

    /// <summary>How each row draws where it differs from the rest: a band under it, a dimmed opacity.</summary>
    public Func<TItem, TableRowLook>? RowLook { get; init; }

    /// <summary>The rows' scroll position; the table's own when null.</summary>
    public ScrollHandle? Scroll { get; init; }

    /// <summary>Builds every row, however many the viewport shows, as a short table sized to its rows does.</summary>
    public bool RealizeAll { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    public bool Disabled { get; init; }
}

public readonly record struct TableHeaderRowProps<TItem>
{
    public TableColumn<TItem>[]? Columns { get; init; }

    public TableSort Sort { get; init; }
}

public readonly record struct TableRowProps<TItem>
{
    public TItem Item { get; init; }

    public int Index { get; init; }

    public TableColumn<TItem>[]? Columns { get; init; }

    /// <summary>The row's size; <see cref="ComponentSize.Sm"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    public Slot<TItem> FullRow { get; init; }

    public Predicate<TItem>? IsFullRow { get; init; }

    public Thickness? FullRowInset { get; init; }

    /// <summary>How the row draws where it differs from the rest.</summary>
    public TableRowLook Look { get; init; }

    public bool Disabled { get; init; }
}

internal readonly record struct TableContext(
    TableLayout? Layout,
    int Version,
    int Rows,
    float Gutter
);

/// <summary>A <see cref="Table{TItem}"/>'s context key and parts.</summary>
public static partial class Table
{
    internal static readonly ContextKey<TableContext> Context = new("Table");

    internal static readonly Thickness CellPadding = new(6f, 0f, 6f, 0f);

    /// <summary>Space past the last column, so the header row's rule runs beside the rows' last cell.</summary>
    internal const float RowEnd = 2f;

    /// <summary>A table's header row: a header per column, in the table's column order, at the columns' widths. Inside
    /// a <see cref="Table{TItem}"/> it sorts, resizes and reorders as the table allows; on its own it shows the
    /// columns' headers at their own widths.</summary>
    public sealed partial class HeaderRow<TItem> : Component<TableHeaderRowProps<TItem>>
    {
        protected override void Render(Ui ui, in TableHeaderRowProps<TItem> p)
        {
            var columns = p.Columns ?? [];
            var c = ui.Use(Context);
            var layout = c.Layout;
            var t = ui.Theme;
            var drop = ui.State<DropLine>(default);
            if (layout is not null)
                layout.Drop = drop;
            var line = drop.Value;
            var disabled = ui.IsDisabled;
            using (
                ui.Box(
                    padding: new Thickness(0f, 0f, RowEnd + c.Gutter, 0f),
                    border: new Border(
                        new Thickness(0f, 0f, 0f, 1f),
                        t.Color(disabled ? ColorToken.BorderDisabled : ColorToken.Border)
                    ),
                    color: t.Color(disabled ? ColorToken.FgDisabled : ColorToken.FgMuted),
                    fontSize: t.FontSize(TextVariant.Caption),
                    fontWeight: t.Weight(WeightToken.Interface),
                    onDrag: layout?.HeaderDrag ?? default,
                    onWheel: layout?.HeaderWheel ?? default,
                    nodeRef: layout?.HeaderRow
                )
            )
            {
                var count = columns.Length;
                for (var shown = 0; shown < count; shown++)
                {
                    var at = layout?.ColumnAt(shown) ?? shown;
                    var column = columns[at];
                    var sorts = layout is { Sorts: true } && column.SortKey.IsKeyed;
                    var size = TableLayout.SizeOf(layout, at, column.Width, column.Fill);
                    ui.TableHeaderCell(
                        index: at,
                        header: column.Header,
                        headerContent: column.HeaderContent,
                        info: column.Info,
                        sortKey: column.SortKey,
                        sorts: sorts,
                        sorted: sorts && p.Sort.Key == column.SortKey
                            ? p.Sort.Direction
                            : SortDirection.None,
                        width: size.Width,
                        minWidth: size.MinWidth,
                        measures: size.Measures,
                        basis: size.Basis,
                        grow: size.Grow,
                        shrink: 0f,
                        zIndex: count - shown,
                        key: at
                    );
                }
                if (line.Shown)
                    using (
                        ui.Box(
                            width: DropLine.Width,
                            background: t.Color(ColorToken.BorderAccent),
                            pointerEvents: PointerEvents.None,
                            position: Position.Absolute,
                            left: line.X - DropLine.Width / 2f,
                            top: 0f,
                            bottom: 0f
                        )
                    ) { }
            }
        }
    }

    /// <summary>A table's row: a cell per column, in the table's column order, at the columns' widths, each centred
    /// down the row, lit under the pointer and marked while selected. Inside a <see cref="Table{TItem}"/> it shares the
    /// table's column widths; on its own it lays its cells out at the columns' own.</summary>
    public sealed partial class Row<TItem> : Component<TableRowProps<TItem>>
    {
        protected override void Render(Ui ui, in TableRowProps<TItem> p)
        {
            var columns = p.Columns ?? [];
            var c = ui.Use(Context);
            var layout = c.Layout;
            var t = ui.Theme;
            var m = t.Metrics(p.Size ?? ComponentSize.Sm);
            var selected = ui.IsSelected;
            var lit = ui.Animate(
                selected ? t.Color(ComponentColor.RowBgSelected)
                : ui.IsHovered ? t.Color(ColorToken.StateHover)
                : default
            );
            var color =
                ui.IsDisabled ? t.Color(ColorToken.FgDisabled)
                : selected ? t.Color(ColorToken.FgAccent)
                : default(Paint);
            var full = p.IsFullRow is { } spans && spans(p.Item);
            using (
                ui.Box(
                    minHeight: m.Height,
                    padding: new Thickness(0f, m.PadY, full ? 0f : RowEnd, m.PadY),
                    radius: t.Radius(RadiusToken.Md),
                    background: lit.A > 0 || !p.Look.Background.IsVisible ? lit : p.Look.Background,
                    opacity: p.Look.Opacity,
                    color: color,
                    fontSize: m.FontSize,
                    fontWeight: t.Weight(WeightToken.Interface)
                )
            )
            {
                if (full)
                    using (
                        ui.Box(
                            direction: Direction.Column,
                            justify: Justify.Center,
                            grow: 1f,
                            padding: p.FullRowInset ?? CellPadding
                        )
                    )
                        p.FullRow.Render(ui, p.Item, p.Index);
                else
                    Cells(ui, columns, layout, p.Item, p.Index);
            }
        }

        [FramePath]
        static void Cells(
            Ui ui,
            TableColumn<TItem>[] columns,
            TableLayout? layout,
            TItem item,
            int index,
            UiKey key = default,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0
        )
        {
            using var _ = ui.Fragment(key: key, file: file, line: line);
            var count = columns.Length;
            for (var shown = 0; shown < count; shown++)
            {
                var at = layout?.ColumnAt(shown) ?? shown;
                var column = columns[at];
                var size = TableLayout.SizeOf(layout, at, column.Width, column.Fill);
                var measure = layout is not null && size.Measures ? ui.Ref(key: at) : null;
                using (
                    ui.Box(
                        direction: Direction.Column,
                        justify: Justify.Center,
                        padding: CellPadding,
                        overflow: Overflow.Clip,
                        width: size.Width,
                        minWidth: size.MinWidth,
                        basis: size.Basis,
                        grow: size.Grow,
                        shrink: 0f,
                        key: at
                    )
                )
                {
                    if (measure is not null)
                        using (
                            ui.Box(
                                direction: Direction.Column,
                                alignSelf: AlignSelf.Start,
                                nodeRef: measure
                            )
                        )
                            Content(ui, column, item, index);
                    else
                        Content(ui, column, item, index);
                }
                if (measure is not null)
                    ui.Effect(
                        layout!,
                        new Grown(at, measure.Size.Width + CellPadding.Left + CellPadding.Right),
                        static (l, g, _) => l.Grow(g.Column, g.Width),
                        key: at
                    );
            }
        }

        [FramePath]
        static void Content(
            Ui ui,
            TableColumn<TItem> column,
            TItem item,
            int index,
            UiKey key = default,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0
        )
        {
            using var _ = ui.Fragment(key: key, file: file, line: line);
            if (!column.Cell.IsEmpty)
                column.Cell.Render(ui, item, index);
            else if (column.Text is { } text)
                ui.Text(text(item));
        }
    }
}

internal readonly record struct Grown(int Column, float Width);

internal readonly record struct TableRowShape<TItem>(
    TableColumn<TItem>[]? Columns,
    ComponentSize? Size,
    Slot<TItem> FullRow,
    Predicate<TItem>? IsFullRow,
    Thickness? FullRowInset,
    Predicate<TItem>? IsRowDisabled,
    Func<TItem, TableRowLook>? RowLook
);

internal readonly record struct DropLine(bool Shown, float X)
{
    public const float Width = 2f;
}

/// <summary>A scrolling table of <see cref="TableProps{TItem}.Items"/>, a row each, under a header row of its
/// <see cref="TableProps{TItem}.Columns"/>. Given a height, or grown by its caller from <c>basis: 0</c>, it builds only
/// the rows in view, reused as it scrolls; with neither it stands as tall as all its rows, and builds them all to
/// measure itself.</summary>
/// <remarks>Rows light under the pointer; while <see cref="TableProps{TItem}.Selectable"/>, a press or an arrow reports
/// the row's key through <see cref="TableProps{TItem}.OnValueChange"/>, and the row whose key is
/// <see cref="TableProps{TItem}.Value"/> shows selected. A click on a header with a sort key reports the next sort
/// through <see cref="TableProps{TItem}.OnSortChange"/>, and the header of the column
/// <see cref="TableProps{TItem}.Sort"/> names shows its direction. A header dragged along the header row moves its
/// column there, and with <see cref="TableProps{TItem}.Resizable"/> a gripper at its right edge drags its width; both
/// are the table's own. Columns wider than the table scroll sideways with the header row, and a wheel turn or a finger
/// on the header row scrolls the rows.</remarks>
public sealed partial class Table<TItem> : Component<TableProps<TItem>>
{
    protected override void Render(Ui ui, in TableProps<TItem> p)
    {
        var items = p.Items ?? throw Missing(nameof(p.Items));
        var itemKey = p.ItemKey ?? throw Missing(nameof(p.ItemKey));
        var columns = p.Columns ?? throw Missing(nameof(p.Columns));
        var t = ui.Theme;
        var model = ui.Remember(static () => new TableModel<TItem>());
        model.Host = ui.Self(p);
        model.Emptied = ui.State(0);
        model.Listen(items);
        model.Choose(p.Selectable ? p.Value : default);
        var layout = model.Layout;
        layout.Version = ui.State(0);
        layout.Own = ui.ScrollHandle();
        layout.Sideways = ui.ScrollHandle();
        layout.Rows = p.Scroll ?? layout.Own;
        layout.HeaderRow = ui.Ref();
        layout.Sync(columns);
        layout.Sorts = !p.OnSortChange.IsEmpty;
        layout.Reorderable = p.Reorderable ?? true;
        layout.Resizable = p.Resizable;
        layout.ScrollsSideways = p.ScrollsSideways ?? true;
        layout.OnSort = model.SortClicked;
        var rows = layout.Rows;
        var sideways = p.ScrollsSideways ?? true;
        var sidewaysBar = sideways ? p.HorizontalScrollbar : ScrollbarVisibility.Disabled;
        var bar = ListBarParts.Space(rows, p.VerticalScrollbar);
        var under = ListBarParts.SidewaysSpace(layout.Sideways, sidewaysBar);
        ui.Provide(
            Table.Context,
            new TableContext(layout, layout.Version.Value, model.RowsVersion(p), bar)
        );
        var m = t.Metrics(p.Size ?? ComponentSize.Sm);
        var across = sideways ? Length.Auto : Length.Full;
        using (ui.Box(direction: Direction.Column, width: p.Width, height: p.Height))
        {
            using (
                ui.Box(
                    direction: Direction.Column,
                    grow: 1f,
                    overflow: Overflow.ScrollX,
                    scroll: layout.Sideways,
                    panning: Panning.HorizontalOnly
                )
            )
            {
                using (
                    ui.Box(
                        direction: Direction.Column,
                        overflow: sideways ? Overflow.Visible : Overflow.Clip,
                        width: across,
                        minWidth: Length.Full
                    )
                )
                    ui.TableHeaderRow(columns: columns, sort: p.Sort);
                ui.VirtualList(
                    items: items,
                    itemKey: itemKey,
                    row: model.Row,
                    rowHeight: m.Height,
                    realizeAll: p.RealizeAll,
                    scroll: layout.Rows,
                    selectionMode: p.Selectable ? SelectionMode.Single : SelectionMode.None,
                    selection: model,
                    onSelectionChange: model.Selected,
                    onAction: p.OnRowActivate,
                    padding: new Thickness(0f, 0f, bar, 0f),
                    grow: 1f,
                    width: across,
                    minWidth: Length.Full
                );
            }
            ui.ListBar(
                scroll: rows,
                visibility: p.VerticalScrollbar,
                top: layout.HeaderRow.Size.Height,
                bottom: under
            );
            ui.SidewaysListBar(scroll: layout.Sideways, visibility: sidewaysBar, right: bar);
            if (items.Count == 0 && !p.EmptyText.IsNull)
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
                    ui.EmptyState(text: p.EmptyText);
        }
    }

    static ArgumentException Missing(string prop) => new($"A table needs its {prop}.", prop);
}

[FramePath]
internal sealed class TableModel<TItem> : ISelection, IObservable, IItemsSourceListener, IDisposable
{
    internal readonly TableLayout Layout = new();
    internal readonly Slot<TItem> Row;
    internal readonly Callback<SelectionChange> Selected;
    internal readonly Callback<UiKey> SortClicked;
    internal Host<TableProps<TItem>> Host;
    internal State<int> Emptied = null!;
    IItemsSource<TItem>? _items;
    bool _empty;
    UiKey _value;
    int _version;
    ObserverSet _observers;
    TableRowShape<TItem> _rowShape;
    int _rowsVersion;

    public TableModel()
    {
        Row = Slot<TItem>.Of(
            this,
            static (ui, m, item, index) =>
            {
                // The list renders a row again only for its own item; this read also renders it as the table's columns change.
                _ = ui.Use(Table.Context);
                ref readonly var p = ref m.Host.Props;
                ui.TableRow(
                    item: item,
                    index: index,
                    columns: p.Columns,
                    size: p.Size,
                    fullRow: p.FullRow,
                    isFullRow: p.IsFullRow,
                    fullRowInset: p.FullRowInset,
                    look: p.RowLook is { } look ? look(item) : default,
                    disabled: p.IsRowDisabled is { } disabled && disabled(item)
                );
            }
        );
        Selected = Callback<SelectionChange>.Of(this, static (m, change) => m.Select(change));
        SortClicked = Callback<UiKey>.Of(this, static (m, key) => m.SortBy(key));
    }

    public ref ObserverSet Observers => ref _observers;

    public int Version
    {
        get
        {
            Observation.Read(this, 0);
            return _version;
        }
    }

    public bool Contains(UiKey key)
    {
        Observation.Read(this, 0);
        return _value.IsKeyed && key == _value;
    }

    /// <summary>Moves on whenever what every row renders from changes, since the list re-renders a row only for its
    /// own item.</summary>
    internal int RowsVersion(in TableProps<TItem> p)
    {
        var shape = new TableRowShape<TItem>(
            p.Columns,
            p.Size,
            p.FullRow,
            p.IsFullRow,
            p.FullRowInset,
            p.IsRowDisabled,
            p.RowLook
        );
        if (shape != _rowShape)
        {
            _rowShape = shape;
            _rowsVersion++;
        }
        return _rowsVersion;
    }

    internal void Choose(UiKey value)
    {
        if (value == _value)
            return;
        _value = value;
        _version++;
        _observers.Notify(this, 0);
    }

    internal void Listen(IItemsSource<TItem> items)
    {
        if (ReferenceEquals(items, _items))
            return;
        _items?.RemoveListener(this);
        _items = items;
        _empty = items.Count == 0;
        items.AddListener(this);
    }

    void IItemsSourceListener.OnItemsChanged(IItemsSource source, in ItemsChange change)
    {
        if (!ReferenceEquals(source, _items) || (source.Count == 0) == _empty)
            return;
        _empty = !_empty;
        Emptied.Value = Emptied.Peek + 1;
    }

    void Select(SelectionChange change)
    {
        ref readonly var p = ref Host.Props;
        var key = change.Action switch
        {
            SelectionAction.Replace or SelectionAction.Toggle or SelectionAction.Range =>
                change.Key,
            _ => default,
        };
        if (key != p.Value)
            p.OnValueChange.Invoke(key);
    }

    void SortBy(UiKey key)
    {
        ref readonly var p = ref Host.Props;
        var direction = p.Sort.Key == key ? p.Sort.Direction : SortDirection.None;
        direction = direction switch
        {
            SortDirection.None => SortDirection.Descending,
            SortDirection.Descending => SortDirection.Ascending,
            _ => SortDirection.None,
        };
        p.OnSortChange.Invoke(new TableSort(key, direction));
    }

    public void Dispose()
    {
        _items?.RemoveListener(this);
        _items = null;
    }
}

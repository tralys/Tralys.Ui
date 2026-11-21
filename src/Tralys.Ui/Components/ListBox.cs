using System.Runtime.CompilerServices;
using Tralys.Ui.Media;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>How a <see cref="ListBox{TItem}"/> lays its items out.</summary>
public enum ListLayout : byte
{
    /// <summary>One column of rows.</summary>
    Rows,

    /// <summary>Cards in equal slots, as many columns as fit.</summary>
    Grid,

    /// <summary>One row of items side by side, scrolling sideways, as a strip of categories: Left and Right move
    /// between them, and a reorderable one drags them along the row.</summary>
    Horizontal,
}

/// <summary>When a <see cref="ListBox.Row"/> is lit.</summary>
public enum RowLight : byte
{
    /// <summary>While the pointer is over it, as a list's row is.</summary>
    Hover,

    /// <summary>While it has the focus, which a pointer moving over it gives it, so one row of a dropdown is lit at a
    /// time, whichever of the pointer and the keyboard moved last.</summary>
    Focus,
}

/// <summary>The first and last index of the items a list shows; (0, −1) while it shows none.</summary>
public readonly record struct ItemRange(int First, int Last);

public readonly record struct ListBoxProps<TItem>
{
    public IItemsSource<TItem>? Items { get; init; }

    public KeyOf<TItem>? ItemKey { get; init; }

    /// <summary>What a row shows while <see cref="Item"/> is empty, and the text typing finds an item by.</summary>
    public TextOf<TItem>? ItemText { get; init; }

    /// <summary>What a row shows inside the row's look.</summary>
    public Slot<TItem> Item { get; init; }

    /// <summary>What a card of the <see cref="ListLayout.Grid"/> layout shows; <see cref="Item"/> while empty.</summary>
    public Slot<TItem> Card { get; init; }

    /// <summary>Whether an item's row is disabled: it greys out, and the arrows pass over it.</summary>
    public Func<TItem, bool>? ItemDisabled { get; init; }

    /// <summary><see cref="Tralys.Ui.SelectionMode.Single"/> by default.</summary>
    public SelectionMode? SelectionMode { get; init; }

    /// <summary>The selected item's key, under single selection.</summary>
    public UiKey Value { get; init; }

    public Callback<UiKey> OnValueChange { get; init; }

    /// <summary>The selected keys, under multiple selection.</summary>
    public ISelection? Selection { get; init; }

    /// <summary>What multiple selection asks of <see cref="Selection"/>, which <c>SelectionSet.Apply</c> takes.</summary>
    public Callback<SelectionChange> OnSelectionChange { get; init; }

    /// <summary>Enter on the focused item, or a double click on one.</summary>
    public Callback<UiKey> OnItemActivate { get; init; }

    public ListLayout Layout { get; init; }

    /// <summary>A card's width in the grid layout: the narrowest a column gets, and the width
    /// <see cref="ItemHeight"/> is in proportion to under <see cref="KeepAspect"/>; every item's width in the
    /// horizontal layout, where 0 lets each take its content's.</summary>
    public float ItemWidth { get; init; }

    public float ItemHeight { get; init; }

    /// <summary>The grid's column count, its cards widened to fill the width; 0 fits as many as
    /// <see cref="ItemWidth"/> allows.</summary>
    public int Columns { get; init; }

    /// <summary>Widens the grid's cards so the columns that fit fill the width.</summary>
    public bool FillWidth { get; init; }

    /// <summary>Grows a widened card's height in proportion, for content that scales with its card.</summary>
    public bool KeepAspect { get; init; }

    /// <summary>Every row's height, never less than the theme's for <see cref="Size"/>; 0 lets each row take its
    /// content's.</summary>
    public float RowHeight { get; init; }

    /// <summary>The items shown, reported after the layout that changed them, while every row stands one height: in
    /// the grid layout, or with <see cref="RowHeight"/>.</summary>
    public Callback<ItemRange> OnVisibleRangeChange { get; init; }

    /// <summary>Shown while there are no items.</summary>
    public TextValue EmptyText { get; init; }

    public ImageSource EmptyEmblem { get; init; }

    /// <summary>Builds every row, however many the viewport shows, as a short catalog does.</summary>
    public bool RealizeAll { get; init; }

    /// <summary>Lets a row of the <see cref="ListLayout.Rows"/> layout be dragged to a new place, which
    /// <see cref="OnReorder"/> reports. A mouse picks a row up by its press, and a finger or pen by holding it, so a
    /// swipe scrolls the list.</summary>
    public bool Reorderable { get; init; }

    /// <summary>A row dropped where it moves its item; the app moves it, and the list then reports the item through
    /// <see cref="OnValueChange"/>, or as the selection under multiple selection.</summary>
    public Callback<ListReorder> OnReorder { get; init; }

    /// <summary>Whether an item may move as asked: before a row is picked up, with its own index as both ends, and
    /// before a drop reports. Null lets every move through.</summary>
    public Func<ListReorder, bool>? CanReorder { get; init; }

    public ScrollHandle? Scroll { get; init; }

    /// <summary>When the bar down the list's right edge shows; while it does, the rows stop short of it.</summary>
    public ScrollbarVisibility VerticalScrollbar { get; init; }

    /// <summary>When the bar along the bottom edge of a <see cref="ListLayout.Horizontal"/> list shows; while it does,
    /// the items stop short of it.</summary>
    public ScrollbarVisibility HorizontalScrollbar { get; init; }

    /// <summary>Binds the list's own node, which holds its rows, so code can focus it; its arrows then step from the
    /// first row.</summary>
    public NodeRef? NodeRef { get; init; }

    /// <summary>The rows' size; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    /// <summary>Whether the selected row shows on the selected fill as well as in the accent: true by default; false
    /// marks it by its accent text alone.</summary>
    public bool? FillsSelection { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    public bool Disabled { get; init; }
}

public readonly record struct ListBoxRowProps
{
    /// <summary>The row's height and padding rung; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    public RowLight Light { get; init; }

    /// <summary>Shows the row selected, as a list shows the row it selects.</summary>
    public bool Selected { get; init; }

    /// <summary>Whether a selected row shows on the selected fill as well as in the accent: true by default.</summary>
    public bool? FillsSelection { get; init; }

    /// <summary>Whether the row shows lit, in place of what <see cref="Light"/> says, as a combobox lights the row its
    /// keys or the pointer reached while its text keeps the focus.</summary>
    public bool? Lit { get; init; }

    /// <summary>The least the row stands; the theme's height for <see cref="Size"/> when null.</summary>
    public float? MinHeight { get; init; }

    /// <summary>Focuses the row as it first shows.</summary>
    public bool AutoFocus { get; init; }

    public bool Disabled { get; init; }

    /// <summary>A press on the row, or Enter or Space on it while it has the focus and its list leaves the key.</summary>
    public Callback OnClick { get; init; }

    public Handler<KeyEvent> OnKeyDown { get; init; }

    internal RowSpot? Reorder { get; init; }
}

/// <summary>What a <see cref="ListBox.Row"/> tells what it holds: whether it shows selected and lit, as its own
/// look does, so content in a list, dropdown or table row can match it.</summary>
public readonly record struct ListBoxRowContext(bool Selected, bool Lit);

/// <summary>A <see cref="ListBox{TItem}"/>'s parts.</summary>
public static partial class ListBox
{
    /// <summary>The nearest row's state, for content a row holds; outside a row, neither selected nor lit.</summary>
    public static readonly ContextKey<ListBoxRowContext> RowContext = new("ListBox.Row", default);

    internal const float CardGap = 8f;
    internal const float CardFrame = 2f;
    internal const float SlotAround = CardGap + CardFrame;

    /// <summary>A <see cref="Row"/> given props the generated call can't set.</summary>
    [FramePath]
    internal static Scope ListBoxRow(
        this Ui ui,
        in ListBoxRowProps props,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) => ui.ComponentWithChildren<Row, ListBoxRowProps>(props, default, key, file, line);

    /// <summary>The look every list, dropdown and table row shares: lit, pressed and selected on a rounded fill at the
    /// size ramp's height and padding, its text in the accent while selected and greyed while disabled, under a hand
    /// cursor while it can be pressed. It reads selection from the list that hosts it, and tells what it holds through
    /// <see cref="RowContext"/>.</summary>
    [Children]
    public sealed class Row : Component<ListBoxRowProps>
    {
        protected override void Render(Ui ui, in ListBoxRowProps p)
        {
            var t = ui.Theme;
            var m = t.Metrics(p.Size ?? ComponentSize.Md);
            var radius = t.Radius(RadiusToken.Md);
            var selected = p.Selected || ui.IsSelected;
            var disabled = ui.IsDisabled;
            var lit = p.Lit ?? (p.Light == RowLight.Focus ? ui.IsFocused : ui.IsHovered);
            var spot = p.Reorder;
            var list = spot?.List;
            var fill =
                selected && (p.FillsSelection ?? true) ? t.Color(ComponentColor.RowBgSelected)
                : ui.IsPressed && p.Light == RowLight.Hover ? t.Color(ColorToken.StatePressed)
                : lit ? t.Color(ColorToken.StateHover)
                : default(Paint);
            var color =
                disabled ? t.Color(ColorToken.FgDisabled)
                : selected ? t.Color(ColorToken.FgAccent)
                : default(Paint);
            var ringed = ui.IsFocusVisible && p.Light == RowLight.Hover;
            ui.Provide(RowContext, new ListBoxRowContext(selected, lit));
            var root = ui.Ref();
            if (spot is not null)
                spot.Root = root;
            using (
                ui.Box(
                    align: Align.Center,
                    minHeight: p.MinHeight ?? m.Height,
                    padding: ringed
                        ? ButtonParts.Inside(m.Padding, FocusRingCalls.Width)
                        : m.Padding,
                    background: fill,
                    border: ringed
                        ? new Border(FocusRingCalls.Width, t.Color(ColorToken.BorderFocus))
                        : default,
                    radius: radius,
                    color: color,
                    fontSize: m.FontSize,
                    fontWeight: t.Weight(WeightToken.Interface),
                    cursor: disabled ? null : Cursors.Hand,
                    onPointerMove: p.Light == RowLight.Focus
                        ? Handler<PointerEvent>.Of(root, static (root, ref e) => Moved(root, ref e))
                        : default,
                    onClick: p.OnClick,
                    onKeyDown: p.OnKeyDown,
                    autoFocus: p.AutoFocus,
                    dragData: list,
                    dragEffects: list is null ? default : DragEffect.Move,
                    onDragEnd: list?.End ?? default,
                    onDrag: spot?.Drag ?? default,
                    onDragOver: spot?.Over ?? default,
                    onDragLeave: list?.Leave ?? default,
                    onDrop: spot?.Drop ?? default,
                    nodeRef: root
                )
            )
                ui.Children();
        }

        // A row the list scrolls under a resting pointer isn't the pointer moving, so only a move focuses it.
        [FramePath]
        static void Moved(NodeRef root, ref PointerEvent e)
        {
            if (
                e.Phase == PointerPhase.Move
                && root.Node is { } node
                && (node.States & InputStates.Focused) == 0
            )
                root.Focus();
        }
    }
}

/// <summary>A scrolling list of <see cref="ListBoxProps{TItem}.Items"/> that builds only the rows in view, each showing
/// <see cref="ListBoxProps{TItem}.Item"/> in the shared row look, or a grid of cards under
/// <see cref="ListLayout.Grid"/>. Single selection is <see cref="ListBoxProps{TItem}.Value"/>, which a press, a tap or an
/// arrow reaching an item reports through <see cref="ListBoxProps{TItem}.OnValueChange"/>; multiple selection is
/// <see cref="ListBoxProps{TItem}.Selection"/>, whose changes the list asks for through
/// <see cref="ListBoxProps{TItem}.OnSelectionChange"/>. The list never writes either.</summary>
/// <remarks>The list is one Tab stop: the arrows, Home, End, Page Up and Page Down move between enabled items and
/// scroll to the one they reach, and typing finds an item by <see cref="ListBoxProps{TItem}.ItemText"/>. Under multiple
/// selection the arrows move without selecting, Space toggles the item they reached, Control toggles a pressed item and
/// Shift selects a range. Enter or a double click activates the focused item. A grid fits as many columns of
/// <see cref="ListBoxProps{TItem}.ItemWidth"/> as its width holds, or exactly
/// <see cref="ListBoxProps{TItem}.Columns"/>, each card inside an 8-unit gap.</remarks>
public sealed partial class ListBox<TItem> : Component<ListBoxProps<TItem>>
{
    protected override void Render(Ui ui, in ListBoxProps<TItem> p)
    {
        var items = p.Items ?? throw Missing(nameof(p.Items));
        var itemKey = p.ItemKey ?? throw Missing(nameof(p.ItemKey));
        var t = ui.Theme;
        var size = p.Size ?? ComponentSize.Md;
        var model = ui.Remember(static () => ListBoxModel<TItem>.Rent());
        model.Host = ui.Self(p);
        var scroll = p.Scroll ?? ui.ScrollHandle();
        var mode = p.SelectionMode ?? SelectionMode.Single;
        var grid = p.Layout == ListLayout.Grid;
        var sideways = p.Layout == ListLayout.Horizontal;
        var rowHeight = p.RowHeight > 0f ? MathF.Max(p.RowHeight, t.Metrics(size).Height) : 0f;
        var full = grid ? ListBoxModel<TItem>.Fit(p, scroll.Viewport.Width) : default;
        var shows = grid
            ? p.VerticalScrollbar == ScrollbarVisibility.Visible
                || (
                    p.VerticalScrollbar == ScrollbarVisibility.Auto
                    && ListBoxModel<TItem>.Overflows(full, items.Count, scroll.Viewport.Height)
                )
            : !sideways && ScrollAreaBar.Shows(scroll, p.VerticalScrollbar, vertical: true);
        var bar = shows ? ScrollAreaBar.Space : 0f;
        var fit = shows && grid ? ListBoxModel<TItem>.Fit(p, scroll.Viewport.Width - bar) : full;
        var reorders = p.Reorderable && !grid;
        model.Frame = reorders ? ui.Ref() : null;
        model.Sideways = sideways;
        model.Line = reorders ? ui.State<ReorderLine>(default) : null;
        ui.Provide(
            ListBoxModel<TItem>.Rows,
            new ListBoxRows<TItem>(
                p.Item,
                p.Card.IsEmpty ? p.Item : p.Card,
                p.ItemText,
                p.ItemDisabled,
                size,
                rowHeight,
                grid,
                reorders ? model : null,
                itemKey,
                p.FillsSelection ?? true
            )
        );
        var fixedHeight =
            grid ? fit.SlotHeight
            : sideways ? 0f
            : rowHeight;
        model.KeepFirst(scroll, grid ? fit.Columns : 1, fixedHeight);
        if (!p.OnVisibleRangeChange.IsEmpty)
            ui.Effect(
                model,
                fixedHeight > 0f
                    ? ShownRange(scroll, fixedHeight, grid ? fit.Columns : 1, items.Count)
                    : default,
                static (m, range, _) => m.Report(range)
            );
        using (
            ui.Box(
                direction: Direction.Column,
                width: p.Width,
                height: p.Height,
                onKeyDown: grid ? ListEnds.Arrows
                    : sideways ? ListEnds.LeftRight
                    : ListEnds.UpDown,
                nodeRef: reorders ? model.Frame : null
            )
        )
        {
            ui.VirtualList(
                items: items,
                itemKey: itemKey,
                row: model.Row,
                rowHeight: sideways ? p.ItemWidth : fixedHeight,
                fixedRowHeight: sideways ? p.ItemWidth > 0f : fixedHeight > 0f,
                columns: grid ? fit.Columns : 1,
                horizontal: sideways,
                padding: new Thickness(0f, 0f, fit.Spare + bar, 0f),
                scroll: scroll,
                realizeAll: p.RealizeAll,
                selectionMode: mode,
                selection: mode == SelectionMode.Multiple ? p.Selection : model.Shown.Of(p.Value),
                onSelectionChange: mode == SelectionMode.Multiple
                    ? p.OnSelectionChange
                    : model.Picked,
                onAction: p.OnItemActivate,
                typeahead: p.ItemText,
                empty: p.EmptyText.IsNull ? default : model.Empty,
                grow: 1f,
                nodeRef: p.NodeRef
            );
            if (sideways)
                ui.SidewaysListBar(scroll: scroll, visibility: p.HorizontalScrollbar);
            else
                ui.ListBar(
                    scroll: scroll,
                    visibility: shows ? ScrollbarVisibility.Visible : ScrollbarVisibility.Hidden
                );
            if (model.Line?.Value is { Shown: true } line)
                ui.Canvas(
                    state: new ReorderLineLook(line, t.Color(ColorToken.BorderAccent)),
                    draw: ReorderLineLook.Draw,
                    position: Position.Absolute,
                    left: 0f,
                    top: 0f,
                    right: 0f,
                    bottom: 0f,
                    pointerEvents: PointerEvents.None
                );
        }
    }

    [FramePath]
    static ItemRange ShownRange(ScrollHandle scroll, float slot, int columns, int count)
    {
        var viewport = scroll.Viewport.Height;
        if (count == 0 || viewport <= 0f)
            return new ItemRange(0, -1);
        var top = scroll.Offset.Y;
        var first = Math.Max(0, (int)MathF.Floor(top / slot)) * columns;
        var last = Math.Min(count, (int)MathF.Ceiling((top + viewport) / slot) * columns) - 1;
        return first > last ? new ItemRange(0, -1) : new ItemRange(first, last);
    }

    static ArgumentException Missing(string prop) => new($"A list box needs its {prop}.", prop);
}

internal readonly record struct ListBoxRows<TItem>(
    Slot<TItem> Item,
    Slot<TItem> Card,
    TextOf<TItem>? Text,
    Func<TItem, bool>? Disabled,
    ComponentSize Size,
    float RowHeight,
    bool Grid,
    RowReorder? Reorder,
    KeyOf<TItem> Key,
    bool FillsSelection
);

internal readonly record struct GridFit(int Columns, float SlotHeight, float Spare);

[FramePath]
internal sealed class ListBoxModel<TItem> : RowReorder, IDisposable
{
    internal static readonly ContextKey<ListBoxRows<TItem>> Rows = new("ListBox", default);

    static readonly Thickness CardInset = new(ListBox.CardGap / 2f);

    internal readonly Slot<TItem> Row;
    internal readonly Slot Empty;
    internal readonly Callback<SelectionChange> Picked;
    internal readonly ShownKey Shown = new();
    internal Host<ListBoxProps<TItem>> Host;
    ItemRange _reported = new(0, -1);
    int _columns = 1;
    float _rowHeight;

    public ListBoxModel()
    {
        Row = Slot<TItem>.Of(
            this,
            static (ui, _, item, index) =>
            {
                var c = ui.Use(Rows);
                var disabled = c.Disabled is { } off && off(item);
                if (c.Grid)
                {
                    using (
                        ui.Box(
                            direction: Direction.Column,
                            disabled: disabled,
                            cursor: disabled ? null : Cursors.Hand,
                            margin: CardInset
                        )
                    )
                    using (ui.Card(highlighted: ui.IsHovered, selected: ui.IsSelected, grow: 1f))
                        c.Card.Render(ui, item, index);
                    return;
                }
                RowSpot? spot = null;
                if (c.Reorder is { } reorder)
                {
                    spot = ui.Remember(static () => new RowSpot());
                    spot.List = reorder;
                    spot.Key = c.Key(item);
                    spot.Index = index;
                }
                using (
                    ui.ListBoxRow(
                        new ListBoxRowProps
                        {
                            Size = c.Size,
                            MinHeight = c.RowHeight > 0f ? c.RowHeight : null,
                            FillsSelection = c.FillsSelection,
                            Disabled = disabled,
                            Reorder = spot,
                        }
                    )
                )
                {
                    if (!c.Item.IsEmpty)
                        c.Item.Render(ui, item, index);
                    else if (c.Text is { } text)
                        ui.Text(text(item));
                }
            }
        );
        Empty = Slot.Of(
            this,
            static (ui, m) =>
            {
                ref readonly var p = ref m.Host.Props;
                using (
                    ui.Box(
                        direction: Direction.Column,
                        align: Align.Center,
                        justify: Justify.Center
                    )
                )
                    ui.EmptyState(text: p.EmptyText, emblem: p.EmptyEmblem);
            }
        );
        Picked = Callback<SelectionChange>.Of(this, static (m, change) => m.Pick(change));
    }

    internal static ListBoxModel<TItem> Rent() =>
        Pool<ListBoxModel<TItem>>.TryRent() ?? new ListBoxModel<TItem>();

    public void Dispose()
    {
        Host = default;
        _reported = new(0, -1);
        _columns = 1;
        _rowHeight = 0f;
        ForgetReorder();
        Pool<ListBoxModel<TItem>>.Return(this);
    }

    void Pick(in SelectionChange change)
    {
        ref readonly var p = ref Host.Props;
        if (change.Action == SelectionAction.Replace && change.Key != p.Value)
            p.OnValueChange.Invoke(change.Key);
    }

    internal override bool CanMove(in ListReorder move) =>
        Host.Props.CanReorder is not { } can || can(move);

    internal override int IndexOf(UiKey key)
    {
        ref readonly var p = ref Host.Props;
        if (p.Items is not { } items || p.ItemKey is not { } keyOf)
            return -1;
        for (var i = 0; i < items.Count; i++)
        {
            if (keyOf(items.ItemAt(i)) == key)
                return i;
        }
        return -1;
    }

    internal override void Moved(in ListReorder move)
    {
        ref readonly var p = ref Host.Props;
        p.OnReorder.Invoke(move);
        var picked = new SelectionChange(SelectionAction.Replace, move.Value, move.Value);
        switch (p.SelectionMode ?? SelectionMode.Single)
        {
            case SelectionMode.Single:
                Pick(picked);
                break;
            case SelectionMode.Multiple:
                p.OnSelectionChange.Invoke(picked);
                break;
        }
    }

    /// <summary>Keeps the item first in view in the first row shown as the columns or the row height change, while
    /// a row height says which item that is.</summary>
    internal void KeepFirst(ScrollHandle scroll, int columns, float rowHeight)
    {
        if (
            (columns != _columns || rowHeight != _rowHeight)
            && _rowHeight > 0f
            && scroll.Offset.Y is > 0f and var offset
        )
            scroll.ScrollToIndex((int)(offset / _rowHeight) * _columns, ScrollAlign.Start);
        _columns = columns;
        _rowHeight = rowHeight;
    }

    internal void Report(ItemRange range)
    {
        if (range == _reported)
            return;
        _reported = range;
        Host.Props.OnVisibleRangeChange.Invoke(range);
    }

    // Cards that fit only once narrowed beside the bar keep it, so the bar never comes and goes with their size.
    internal static bool Overflows(in GridFit full, int count, float viewport) =>
        viewport > 0f && (count + full.Columns - 1) / full.Columns * full.SlotHeight > viewport;

    internal static GridFit Fit(in ListBoxProps<TItem> p, float width)
    {
        var around = ListBox.SlotAround;
        var columns =
            width <= 0f ? Math.Max(1, p.Columns)
            : p.Columns > 0 ? p.Columns
            : Math.Max(1, (int)MathF.Floor(width / MathF.Max(1f, p.ItemWidth + around)));
        var widens = p.Columns > 0 || p.FillWidth;
        var itemWidth =
            widens && width > 0f
                ? MathF.Max(1f, MathF.Floor(width / columns) - around)
                : p.ItemWidth;
        var itemHeight =
            widens && p.KeepAspect && p.ItemWidth > 0f
                ? MathF.Round(itemWidth * p.ItemHeight / p.ItemWidth)
                : p.ItemHeight;
        var spare = width > 0f ? MathF.Max(0f, width - columns * (itemWidth + around)) : 0f;
        return new GridFit(columns, MathF.Max(1f, itemHeight + around), spare);
    }
}

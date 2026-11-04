using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

/// <summary>An item's identity in a list: its row keeps its host, state and focus while the key stays.</summary>
[FramePath]
public delegate UiKey KeyOf<in TItem>(TItem item);

/// <summary>The text an item is found by, as typeahead does.</summary>
[FramePath]
public delegate TextValue TextOf<in TItem>(TItem item);

public enum SelectionMode : byte
{
    None,
    Single,
    Multiple,
}

public enum SelectionAction : byte
{
    /// <summary>The key alone.</summary>
    Replace,

    /// <summary>The key in or out.</summary>
    Toggle,

    /// <summary>The keys from the anchor to the key.</summary>
    Range,

    /// <summary>Every key.</summary>
    All,

    /// <summary>No key.</summary>
    Clear,
}

/// <summary>A selection a list asks for; the list never writes its selection.</summary>
public readonly record struct SelectionChange(SelectionAction Action, UiKey Key, UiKey Anchor);

/// <summary>The keys a list shows selected.</summary>
public interface ISelection
{
    bool Contains(UiKey key);

    /// <summary>Changes whenever the keys do; reading it is tracked.</summary>
    int Version { get; }
}

/// <summary>The list primitive.</summary>
[FramePath]
public static class ListPrimitives
{
    /// <summary>A scrolling list that renders only the rows in view and within <c>cacheLength</c> of it, each from
    /// <c>row</c> during layout, keyed by <c>itemKey</c>. Its offset is
    /// <c>scroll</c>'s, or its own. Its grid row placement is <c>gridRow</c>, as <c>row</c> names the row slot.</summary>
    public static void VirtualList<TItem>(
        this Ui ui,
        IItemsSource<TItem> items,
        KeyOf<TItem> itemKey,
        Slot<TItem> row,
        float rowHeight = 0,
        bool fixedRowHeight = false,
        int columns = 0,
        bool horizontal = false,
        float gap = 0,
        Thickness padding = default,
        ScrollHandle? scroll = null,
        bool realizeAll = false,
        float? cacheLength = null,
        SelectionMode selectionMode = default,
        ISelection? selection = null,
        Callback<SelectionChange> onSelectionChange = default,
        Callback<UiKey> onAction = default,
        TextOf<TItem>? typeahead = null,
        Slot empty = default,
        bool autoFocus = false,
        int tabIndex = 0,
        float grow = 0,
        float? shrink = null,
        Length basis = default,
        AlignSelf alignSelf = default,
        AlignSelf justifySelf = default,
        Thickness margin = default,
        Position position = default,
        Length left = default,
        Length top = default,
        Length right = default,
        Length bottom = default,
        int zIndex = 0,
        int column = 0,
        int gridRow = 0,
        int columnSpan = 0,
        int rowSpan = 0,
        bool hidden = false,
        bool collapsed = false,
        string? id = null,
        Length width = default,
        Length height = default,
        Length minWidth = default,
        Length minHeight = default,
        Length maxWidth = default,
        Length maxHeight = default,
        float aspectRatio = 0,
        float? opacity = null,
        Transform transform = default,
        Vector2? transformOrigin = null,
        Cursor? cursor = null,
        PointerEvents pointerEvents = default,
        NodeRef? nodeRef = null,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(itemKey);
        var b = ui.Builder;
        if (b.Discarding)
            return;
        var node = b.Claim(NodeKind.VirtualList, null, key, file, line, out _);
        var view = b.View;
        if (node.Payload is not ListPayload<TItem> payload)
        {
            (node.Payload as ListPayload)?.UnmountRows();
            node.Payload?.Release(view);
            payload = Pool<ListPayload<TItem>>.TryRent() ?? new ListPayload<TItem>();
            payload.Attach(view, node, b.CurrentHost, file, line);
            node.Payload = payload;
        }
        var changed = payload.Write(
            new ListProps<TItem>
            {
                Items = items,
                ItemKey = itemKey,
                Row = row,
                RowHeight = rowHeight,
                FixedRowHeight = fixedRowHeight,
                Columns = Math.Max(1, columns),
                Horizontal = horizontal,
                RealizeAll = realizeAll,
                CacheLength = cacheLength,
                SelectionMode = selectionMode,
                Selection = selection,
                OnSelectionChange = onSelectionChange,
                OnAction = onAction,
                Typeahead = typeahead,
                Empty = empty,
            }
        );
        changed |= node.WriteScroll(
            new ScrollProps
            {
                Panning = horizontal ? Panning.HorizontalFirst : Panning.VerticalFirst,
            },
            scroll,
            scrolls: true
        );
        Primitives.Write(
            view,
            node,
            new LayoutStyle
            {
                Direction = horizontal ? Direction.Row : Direction.Column,
                RowGap = gap,
                ColumnGap = gap,
                Padding = padding,
                Overflow = horizontal ? Overflow.ScrollX : Overflow.ScrollY,
                Width = width,
                Height = height,
                MinWidth = minWidth,
                MinHeight = minHeight,
                MaxWidth = maxWidth,
                MaxHeight = maxHeight,
                AspectRatio = aspectRatio,
            },
            new VisualStyle
            {
                Opacity = opacity,
                Transform = transform,
                TransformOrigin = transformOrigin,
            },
            new InheritStyle { PointerEvents = pointerEvents },
            payload.Interaction(cursor, autoFocus, tabIndex),
            new Placement
            {
                Grow = grow,
                Shrink = shrink,
                Basis = basis,
                AlignSelf = alignSelf,
                JustifySelf = justifySelf,
                Margin = margin,
                Position = position,
                Left = left,
                Top = top,
                Right = right,
                Bottom = bottom,
                ZIndex = zIndex,
                Column = column,
                Row = gridRow,
                ColumnSpan = columnSpan,
                RowSpan = rowSpan,
                Hidden = hidden,
                Collapsed = collapsed,
                Id = id,
            },
            false,
            nodeRef,
            changed
        );
    }

    /// <summary>As the <see cref="IItemsSource{T}"/> overload, showing <paramref name="items"/> through
    /// <see cref="ItemsSource.From{T}"/>, whose changes reach the list without rendering its caller.</summary>
    public static void VirtualList<TItem>(
        this Ui ui,
        ObservableList<TItem> items,
        KeyOf<TItem> itemKey,
        Slot<TItem> row,
        float rowHeight = 0,
        bool fixedRowHeight = false,
        int columns = 0,
        bool horizontal = false,
        float gap = 0,
        Thickness padding = default,
        ScrollHandle? scroll = null,
        bool realizeAll = false,
        float? cacheLength = null,
        SelectionMode selectionMode = default,
        ISelection? selection = null,
        Callback<SelectionChange> onSelectionChange = default,
        Callback<UiKey> onAction = default,
        TextOf<TItem>? typeahead = null,
        Slot empty = default,
        bool autoFocus = false,
        int tabIndex = 0,
        float grow = 0,
        float? shrink = null,
        Length basis = default,
        AlignSelf alignSelf = default,
        AlignSelf justifySelf = default,
        Thickness margin = default,
        Position position = default,
        Length left = default,
        Length top = default,
        Length right = default,
        Length bottom = default,
        int zIndex = 0,
        int column = 0,
        int gridRow = 0,
        int columnSpan = 0,
        int rowSpan = 0,
        bool hidden = false,
        bool collapsed = false,
        string? id = null,
        Length width = default,
        Length height = default,
        Length minWidth = default,
        Length minHeight = default,
        Length maxWidth = default,
        Length maxHeight = default,
        float aspectRatio = 0,
        float? opacity = null,
        Transform transform = default,
        Vector2? transformOrigin = null,
        Cursor? cursor = null,
        PointerEvents pointerEvents = default,
        NodeRef? nodeRef = null,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.VirtualList(
            items: ItemsSource.From(items),
            itemKey: itemKey,
            row: row,
            rowHeight: rowHeight,
            fixedRowHeight: fixedRowHeight,
            columns: columns,
            horizontal: horizontal,
            gap: gap,
            padding: padding,
            scroll: scroll,
            realizeAll: realizeAll,
            cacheLength: cacheLength,
            selectionMode: selectionMode,
            selection: selection,
            onSelectionChange: onSelectionChange,
            onAction: onAction,
            typeahead: typeahead,
            empty: empty,
            autoFocus: autoFocus,
            tabIndex: tabIndex,
            grow: grow,
            shrink: shrink,
            basis: basis,
            alignSelf: alignSelf,
            justifySelf: justifySelf,
            margin: margin,
            position: position,
            left: left,
            top: top,
            right: right,
            bottom: bottom,
            zIndex: zIndex,
            column: column,
            gridRow: gridRow,
            columnSpan: columnSpan,
            rowSpan: rowSpan,
            hidden: hidden,
            collapsed: collapsed,
            id: id,
            width: width,
            height: height,
            minWidth: minWidth,
            minHeight: minHeight,
            maxWidth: maxWidth,
            maxHeight: maxHeight,
            aspectRatio: aspectRatio,
            opacity: opacity,
            transform: transform,
            transformOrigin: transformOrigin,
            cursor: cursor,
            pointerEvents: pointerEvents,
            nodeRef: nodeRef,
            key: key,
            file: file,
            line: line
        );
}

[FramePath]
internal record struct ListProps<TItem>
{
    public IItemsSource<TItem> Items;
    public KeyOf<TItem> ItemKey;
    public Slot<TItem> Row;
    public float RowHeight;
    public bool FixedRowHeight;
    public int Columns;
    public bool Horizontal;
    public bool RealizeAll;
    public float? CacheLength;
    public SelectionMode SelectionMode;
    public ISelection? Selection;
    public Callback<SelectionChange> OnSelectionChange;
    public Callback<UiKey> OnAction;
    public TextOf<TItem>? Typeahead;
    public Slot Empty;
}

internal sealed class VirtualListRow;

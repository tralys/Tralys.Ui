using Tralys.Ui.Fonts;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct CollectionPickerProps<TItem>
{
    public IItemsSource<TItem>? Items { get; init; }

    public KeyOf<TItem>? ItemKey { get; init; }

    /// <summary>What the field shows of the picked item, what the search matches, and what a row or tile shows while
    /// its slot is empty.</summary>
    public TextOf<TItem>? ItemText { get; init; }

    /// <summary>What a row of the <see cref="ListLayout.Rows"/> layout shows inside the row's look.</summary>
    public Slot<TItem> Item { get; init; }

    /// <summary>What a tile of the <see cref="ListLayout.Grid"/> layout shows; <see cref="Item"/> while empty.</summary>
    public Slot<TItem> Card { get; init; }

    /// <summary>The picked item's key.</summary>
    public UiKey Value { get; init; }

    public Callback<UiKey> OnValueChange { get; init; }

    /// <summary>Whether the popup shows; null leaves it to the field, starting at <see cref="DefaultOpen"/>.</summary>
    public bool? Open { get; init; }

    public bool DefaultOpen { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    /// <summary>How the popup lays the items out; null leaves it to its view-mode button, starting at
    /// <see cref="DefaultLayout"/>.</summary>
    public ListLayout? Layout { get; init; }

    /// <summary><see cref="ListLayout.Grid"/> when null.</summary>
    public ListLayout? DefaultLayout { get; init; }

    public Callback<ListLayout> OnLayoutChange { get; init; }

    /// <summary>A tile's width in the grid; 80 when 0.</summary>
    public float ItemWidth { get; init; }

    /// <summary>A tile's height in the grid; 96 when 0.</summary>
    public float ItemHeight { get; init; }

    /// <summary>Shown while nothing is picked; "Select…" when null.</summary>
    public TextValue Placeholder { get; init; }

    /// <summary>A muted label before the field's value.</summary>
    public TextValue Sublabel { get; init; }

    public FieldVariant Variant { get; init; }

    public ComponentSize? Size { get; init; }

    public Length Width { get; init; }

    /// <summary>The narrowest the popup gets, which is otherwise as wide as the field; 360 when null.</summary>
    public float? PopupMinWidth { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A picker field over a collection: its popup holds a search box over the items, as a grid of tiles or a list
/// of rows, and a button that switches between the two. <see cref="CollectionPickerProps{TItem}.Value"/> is the picked
/// item's key, which it reports through <see cref="CollectionPickerProps{TItem}.OnValueChange"/>; the field shows that
/// item's text, else the placeholder.</summary>
/// <remarks>Opening focuses the search, which keeps the items whose text contains it, ignoring case. A click on an item
/// picks it and closes; a pressable inside an item owns its own press. Down from the search moves the focus into the
/// items, where the arrows pick each item they reach and Enter closes; Enter in the search with one item left picks it
/// and closes. Escape and a press outside close it, and closing forgets the search.</remarks>
public sealed partial class CollectionPicker<TItem> : Component<CollectionPickerProps<TItem>>
{
    internal const float MaxPopupHeight = 400f;

    protected override void Render(Ui ui, in CollectionPickerProps<TItem> p)
    {
        var items = p.Items ?? throw Missing(nameof(p.Items));
        var itemKey = p.ItemKey ?? throw Missing(nameof(p.ItemKey));
        var itemText = p.ItemText ?? throw Missing(nameof(p.ItemText));
        var model = ui.Remember(
            (
                Own: ui.State(p.DefaultOpen),
                OwnLayout: ui.State(p.DefaultLayout ?? ListLayout.Grid),
                Scroll: ui.ScrollHandle(),
                List: ui.Ref(),
                Search: ui.Ref()
            ),
            static hooks => new CollectionPickerModel<TItem>(
                hooks.Own,
                hooks.OwnLayout,
                hooks.Scroll,
                hooks.List,
                hooks.Search
            )
        );
        model.Host = ui.Self(p);
        model.Shown.Bind(items, itemText);
        var size = p.Size ?? ComponentSize.Md;
        ui.Effect(model, ui.IsDisabled, CollectionPickerModel<TItem>.ShutWhenDisabled);
        var open = (p.Open ?? model.Own.Value) && !ui.IsDisabled;
        var field = ui.Ref();
        ui.PickerBox(
            open: open,
            text: model.PickedText(),
            placeholder: p.Placeholder.IsNull ? "Select…" : p.Placeholder,
            sublabel: p.Sublabel,
            hasArrow: true,
            variant: p.Variant,
            size: size,
            width: p.Width,
            onClick: model.Toggle,
            field: field
        );
        if (!open)
            return;
        using (
            ui.Positioner(
                anchor: field,
                matchAnchorWidth: true,
                restoreFocus: true,
                dismissOn: Dismiss.Escape | Dismiss.OutsidePress,
                direction: Direction.Column,
                minWidth: p.PopupMinWidth ?? 360f,
                onDismiss: model.Close
            )
        )
            ui.CollectionPickerPopup<TItem>(
                model: model,
                itemKey: itemKey,
                layout: p.Layout ?? model.OwnLayout.Value,
                size: size
            );
    }

    static ArgumentException Missing(string prop) =>
        new($"A collection picker needs its {prop}.", prop);
}

internal readonly record struct CollectionPickerPopupProps<TItem>
{
    public required CollectionPickerModel<TItem> Model { get; init; }

    public required KeyOf<TItem> ItemKey { get; init; }

    public ListLayout Layout { get; init; }

    public ComponentSize Size { get; init; }
}

internal sealed partial class CollectionPickerPopup<TItem>
    : Component<CollectionPickerPopupProps<TItem>>
{
    protected override void Render(Ui ui, in CollectionPickerPopupProps<TItem> p)
    {
        var model = p.Model;
        var props = model.Host.Props;
        var t = ui.Theme;
        var m = t.Metrics(p.Size);
        var grid = p.Layout == ListLayout.Grid;
        var extent = model.Scroll.Extent.Height;
        var most = CollectionPicker<TItem>.MaxPopupHeight - m.Height - 10f;
        var height = Math.Clamp(extent > 0f ? extent : most, 80f, most);
        using (ui.OverlayFrame(size: p.Size))
        {
            using (ui.Box(align: Align.Center))
            {
                using (ui.Box(direction: Direction.Column, grow: 1f, onKeyDown: model.SearchKey))
                {
                    model.Query.Set(model.Shown.Query);
                    ui.SearchInput(
                        text: model.Query,
                        onEdit: model.Typed,
                        onClear: model.Cleared,
                        placeholder: "Search…",
                        variant: FieldVariant.Flush,
                        size: p.Size,
                        autoFocus: true,
                        nodeRef: model.Search
                    );
                }
                using (
                    ui.Box(
                        width: 1f,
                        alignSelf: AlignSelf.Stretch,
                        background: t.Color(ColorToken.Border)
                    )
                ) { }
                using (ui.Tooltip(text: grid ? "List view" : "Grid view"))
                    ui.Toggle(
                        value: grid,
                        onValueChange: model.LayoutToggled,
                        icon: IconGlyph.Of(IconRole.Grid),
                        activeIcon: IconGlyph.Of(IconRole.List),
                        size: p.Size
                    );
            }
            ui.ListBox(
                items: model.Shown,
                itemKey: p.ItemKey,
                itemText: props.ItemText,
                item: model.Row,
                card: model.Tile,
                value: props.Value,
                onValueChange: props.OnValueChange,
                onItemActivate: model.Activated,
                layout: p.Layout,
                itemWidth: props.ItemWidth > 0f ? props.ItemWidth : 80f,
                itemHeight: props.ItemHeight > 0f ? props.ItemHeight : 96f,
                emptyText: "No results",
                scroll: model.Scroll,
                nodeRef: model.List,
                size: p.Size,
                height: height
            );
        }
    }
}

internal readonly record struct CollectionPickerEntryProps<TItem>
{
    public required CollectionPickerModel<TItem> Model { get; init; }

    public required TItem Item { get; init; }

    public int Index { get; init; }

    public bool Tile { get; init; }
}

/// <summary>An item's row or tile content, pressable so a click picks it while a pressable inside keeps its own
/// press.</summary>
internal sealed partial class CollectionPickerEntry<TItem>
    : Component<CollectionPickerEntryProps<TItem>>
{
    protected override void Render(Ui ui, in CollectionPickerEntryProps<TItem> p)
    {
        var props = p.Model.Host.Props;
        var slot = p.Tile && !props.Card.IsEmpty ? props.Card : props.Item;
        using (
            ui.Box(
                direction: p.Tile ? Direction.Column : Direction.Row,
                align: Align.Center,
                justify: p.Tile ? Justify.Center : default,
                focusable: false,
                onClick: Callback.Of(ui.Self(p), static h => h.Props.Model.Pick(h.Props.Item))
            )
        )
        {
            if (!slot.IsEmpty)
                slot.Render(ui, p.Item, p.Index);
            else if (props.ItemText is { } itemText)
                ui.Text(
                    itemText(p.Item),
                    grow: p.Tile ? 0f : 1f,
                    textAlign: p.Tile ? TextAlign.Center : default,
                    trimming: TextTrimming.CharacterEllipsis
                );
        }
    }
}

[FramePath]
internal sealed class CollectionPickerModel<TItem> : IDisposable
{
    internal readonly TextMatches<TItem> Shown = new();
    internal readonly Callback Toggle;
    internal readonly Callback Close;
    internal readonly Callback Typed;
    internal readonly Callback Cleared;
    internal readonly Callback<bool> LayoutToggled;
    internal readonly Callback<UiKey> Activated;
    internal readonly Handler<KeyEvent> SearchKey;
    internal readonly Slot<TItem> Row;
    internal readonly Slot<TItem> Tile;
    internal Host<CollectionPickerProps<TItem>> Host;
    internal readonly State<bool> Own;
    internal readonly State<ListLayout> OwnLayout;
    internal readonly ScrollHandle Scroll;
    internal readonly NodeRef List;
    internal readonly NodeRef Search;
    internal readonly ObservableText Query = new();
    int _picked = -1;

    public CollectionPickerModel(
        State<bool> own,
        State<ListLayout> ownLayout,
        ScrollHandle scroll,
        NodeRef list,
        NodeRef search
    )
    {
        Own = own;
        OwnLayout = ownLayout;
        Scroll = scroll;
        List = list;
        Search = search;
        Toggle = Callback.Of(this, static m => m.SetOpen(!m.IsOpen));
        Close = Callback.Of(this, static m => m.SetOpen(false));
        Typed = Callback.Of(this, static m => m.Shown.Match(m.Query.Span));
        Cleared = Callback.Of(this, static m => m.Shown.Match(""));
        LayoutToggled = Callback<bool>.Of(
            this,
            static (m, grid) => m.SetLayout(grid ? ListLayout.Grid : ListLayout.Rows)
        );
        Activated = Callback<UiKey>.Of(this, static (m, _) => m.SetOpen(false));
        SearchKey = Handler<KeyEvent>.Of(this, static (m, ref e) => m.Searched(ref e));
        Row = Slot<TItem>.Of(
            this,
            static (ui, m, item, index) =>
                ui.CollectionPickerEntry<TItem>(
                    model: m,
                    item: item,
                    index: index,
                    tile: false,
                    grow: 1f
                )
        );
        Tile = Slot<TItem>.Of(
            this,
            static (ui, m, item, index) =>
                ui.CollectionPickerEntry<TItem>(
                    model: m,
                    item: item,
                    index: index,
                    tile: true,
                    grow: 1f
                )
        );
    }

    ref readonly CollectionPickerProps<TItem> Props => ref Host.Props;

    bool IsOpen => Props.Open ?? Own.Peek;

    internal TextValue PickedText()
    {
        ref readonly var p = ref Props;
        if (
            !p.Value.IsKeyed
            || p.Items is not { } items
            || p.ItemKey is not { } key
            || p.ItemText is not { } text
        )
            return default;
        if (_picked >= 0 && _picked < items.Count && key(items.ItemAt(_picked)) == p.Value)
            return text(items.ItemAt(_picked));
        for (var i = 0; i < items.Count; i++)
        {
            if (key(items.ItemAt(i)) != p.Value)
                continue;
            _picked = i;
            return text(items.ItemAt(i));
        }
        _picked = -1;
        return default;
    }

    internal void Pick(TItem item)
    {
        if (Props.ItemKey is { } itemKey)
        {
            var key = itemKey(item);
            if (key != Props.Value)
                Props.OnValueChange.Invoke(key);
        }
        SetOpen(false);
    }

    void Searched(ref KeyEvent e)
    {
        if (e.Modifiers != ModifierKeys.None)
            return;
        switch (e.Key)
        {
            case Key.Down:
                List.Focus(visible: true);
                break;
            case Key.Enter when Shown.Count == 1:
                Pick(Shown.ItemAt(0));
                break;
            default:
                return;
        }
        e.PreventDefault();
        e.StopPropagation();
    }

    void SetLayout(ListLayout layout)
    {
        if ((Props.Layout ?? OwnLayout.Peek) == layout)
            return;
        if (Props.Layout is null)
            OwnLayout.Value = layout;
        Props.OnLayoutChange.Invoke(layout);
    }

    internal static readonly EffectFn<CollectionPickerModel<TItem>, bool> ShutWhenDisabled =
        static (m, disabled, _) =>
        {
            if (disabled)
                m.SetOpen(false);
        };

    void SetOpen(bool open)
    {
        if (!open)
            Shown.Match("");
        if (IsOpen == open)
            return;
        if (Props.Open is null)
            Own.Value = open;
        Props.OnOpenChange.Invoke(open);
    }

    public void Dispose() => Shown.Dispose();
}

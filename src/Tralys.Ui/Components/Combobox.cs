using Tralys.Ui.Fonts;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct ComboboxProps<TItem>
{
    public UiKey Value { get; init; }

    public Callback<UiKey> OnValueChange { get; init; }

    public bool? Open { get; init; }

    public bool DefaultOpen { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    public IItemsSource<TItem>? Items { get; init; }

    public KeyOf<TItem>? ItemKey { get; init; }

    public TextOf<TItem>? ItemText { get; init; }

    public Slot<TItem> Item { get; init; }

    public TextValue Placeholder { get; init; }

    /// <summary>A muted label before the field's text.</summary>
    public TextValue Sublabel { get; init; }

    public FieldVariant Variant { get; init; }

    public ComponentSize? Size { get; init; }

    public Length Width { get; init; }

    /// <summary>The narrowest the popup gets, which is otherwise as wide as the field.</summary>
    public float PopupMinWidth { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>What a <see cref="Combobox{TItem}"/> tells its <see cref="Combobox.Item"/>s: the selected value, the
/// highlighted one, and how to pick or highlight one.</summary>
public readonly record struct ComboboxContext(
    UiKey Value,
    UiKey Active,
    Callback<UiKey> Select,
    Callback<UiKey> Activate,
    ComponentSize Size
);

public readonly record struct ComboboxItemProps
{
    public UiKey Value { get; init; }

    public TextValue Text { get; init; }

    /// <summary>What the row shows when it has no <see cref="Text"/>.</summary>
    public Slot Content { get; init; }
}

/// <summary>A <see cref="Combobox{TItem}"/>'s context key and parts.</summary>
public static partial class Combobox
{
    public static readonly ContextKey<ComboboxContext> Context = new("Combobox", default);

    /// <summary>A row of a combobox's popup in the shared row look: lit while highlighted, selected while it holds the
    /// value. A pointer moving over it highlights it, and a click on it picks it, leaving the focus in the combobox's
    /// text; a pressable in its <see cref="ComboboxItemProps.Content"/> owns its own press.</summary>
    public sealed class Item : Component<ComboboxItemProps>
    {
        protected override void Render(Ui ui, in ComboboxItemProps p)
        {
            var c = ui.Use(Context);
            var self = ui.Self(p);
            using (
                ui.Box(
                    direction: Direction.Column,
                    height: Length.Full,
                    onPointerMove: Handler<PointerEvent>.Of(
                        self,
                        static (h, ref e) => Moved(h, ref e)
                    )
                )
            )
            using (
                ui.Box(
                    direction: Direction.Column,
                    grow: 1f,
                    focusable: false,
                    onClick: Callback.Of(
                        self,
                        static h => h.Use(Context).Select.Invoke(h.Props.Value)
                    )
                )
            )
            using (
                ui.ListBoxRow(
                    size: c.Size,
                    selected: c.Value.IsKeyed && c.Value == p.Value,
                    lit: c.Active.IsKeyed && c.Active == p.Value,
                    minHeight: 0f,
                    grow: 1f
                )
            )
            {
                if (p.Text.IsNull)
                    p.Content.Render(ui);
                else
                    ui.Text(p.Text, grow: 1f, trimming: TextTrimming.CharacterEllipsis);
            }
        }

        [FramePath]
        static void Moved(Host<ComboboxItemProps> h, ref PointerEvent e)
        {
            if (e.Phase == PointerPhase.Move)
                h.Use(Context).Activate.Invoke(h.Props.Value);
        }
    }
}

/// <summary>A text field that filters <see cref="ComboboxProps{TItem}.Items"/> into a popup list to pick from,
/// controlled by <see cref="ComboboxProps{TItem}.Value"/>, the picked item's key, which it reports through
/// <see cref="ComboboxProps{TItem}.OnValueChange"/>. Its <see cref="ComboboxProps{TItem}.Open"/> reports through
/// <see cref="ComboboxProps{TItem}.OnOpenChange"/>, and is its own when null, starting at
/// <see cref="ComboboxProps{TItem}.DefaultOpen"/>.</summary>
/// <remarks>Closed, the field shows the selected item's text. Typing opens the popup on the items whose text contains
/// what was typed, ignoring case, and lights the first; Up or Down opens it on every item, lighting the selected one,
/// and so does a click on its chevron. While it is open, Up and Down move the light without wrapping, PageUp and
/// PageDown move it a popup's height, Enter picks the lit item and closes, Tab picks it and moves on, and Escape (or
/// gamepad East), Alt+Up, a press outside and the focus leaving close it without picking, putting the selected item's
/// text back. Focus stays in the field throughout. Rows render through <see cref="ComboboxProps{TItem}.Item"/>, a slot
/// of <c>ui.ComboboxItem</c>, or show <see cref="ComboboxProps{TItem}.ItemText"/> by default; the popup is a
/// <c>VirtualList</c> of at most eight rows on the shared floating frame, in a <c>Positioner</c> as wide as the field,
/// which fades out as it closes.</remarks>
public sealed partial class Combobox<TItem> : Component<ComboboxProps<TItem>>
{
    protected override void Render(Ui ui, in ComboboxProps<TItem> p)
    {
        var items = p.Items ?? throw Missing(nameof(p.Items));
        var itemKey = p.ItemKey ?? throw Missing(nameof(p.ItemKey));
        var itemText = p.ItemText ?? throw Missing(nameof(p.ItemText));
        var t = ui.Theme;
        var size = p.Size ?? ComponentSize.Md;
        var m = t.Metrics(size);
        var model = ui.Remember(
            (
                OpenState: ui.State(p.DefaultOpen),
                Filtering: ui.State(false),
                Active: ui.State<UiKey>(default),
                Scroll: ui.ScrollHandle(),
                Text: ui.Ref()
            ),
            static hooks => new ComboboxModel<TItem>(
                hooks.OpenState,
                hooks.Filtering,
                hooks.Active,
                hooks.Scroll,
                hooks.Text
            )
        );
        model.Host = ui.Self(p);
        model.Shown.Bind(items, itemText);
        var field = ui.Ref();

        var disabled = ui.IsDisabled;
        var open = (p.Open ?? model.OpenState.Value) && !disabled;
        var filtering = model.Filtering.Value;
        if (open && !filtering)
            model.Shown.Match("");
        var count = model.Shown.ShownCount;
        ui.Provide(
            Combobox.Context,
            new ComboboxContext(p.Value, model.Active.Value, model.Select, model.Activate, size)
        );

        var flush = p.Variant == FieldVariant.Flush;
        var palette = t.Field(p.Variant, ui.FieldState);
        using (
            ui.Box(
                align: Align.Center,
                width: p.Width,
                minHeight: m.Height,
                padding: m.FieldPaddingX,
                background: palette.Background,
                border: new Border(
                    flush ? new Thickness(0f, 0f, 0f, 1f) : new Thickness(1f),
                    open && !flush ? t.Color(ColorToken.BorderFocus) : palette.Border
                ),
                radius: flush ? default : t.Radius(RadiusToken.Md),
                fontSize: m.FontSize,
                fontWeight: t.Weight(WeightToken.Interface),
                cursor: disabled ? Cursors.Arrow : Cursors.IBeam,
                onPointerDown: model.PressField,
                onKeyDown: model.KeyDown,
                nodeRef: field
            )
        )
        {
            if (!p.Sublabel.IsNull)
                ui.Text(
                    p.Sublabel,
                    color: t.Color(ColorToken.FgMuted),
                    shrink: 0f,
                    margin: new Thickness(0f, 0f, 4f, 0f)
                );
            if (!filtering)
                model.Search.Set(model.SelectedText().Span);
            ui.EditableText(
                text: model.Search,
                onEdit: model.Typed,
                placeholder: p.Placeholder,
                color: palette.Foreground,
                fontSize: m.FontSize,
                fontWeight: t.Weight(WeightToken.Interface),
                selectionColor: t.Color(ColorToken.BgTextSelection),
                caretColor: t.Color(ColorToken.Fg),
                placeholderColor: t.Color(ColorToken.FgPlaceholder),
                grow: 1,
                nodeRef: model.Text
            );
            using (
                ui.Box(
                    shrink: 0f,
                    margin: new Thickness(4f, 0f, 0f, 0f),
                    cursor: disabled ? Cursors.Arrow : Cursors.Hand,
                    focusable: false,
                    onClick: model.Toggle,
                    onPointerDown: InputClear.KeepPress
                )
            )
                ui.Chevron(
                    color: t.Color(disabled ? ColorToken.IconDisabled : ColorToken.Icon),
                    size: m.IconSize,
                    open: open
                );
        }

        if (!open)
            return;
        using (
            ui.Positioner(
                anchor: field,
                matchAnchorWidth: true,
                restoreFocus: true,
                dismissOn: Dismiss.Escape | Dismiss.OutsidePress | Dismiss.FocusOutside,
                dismissExempt: field,
                direction: Direction.Column,
                minWidth: p.PopupMinWidth,
                onDismiss: model.Dismiss
            )
        )
            ui.ComboboxPopup<TItem>(
                model: model,
                item: p.Item.IsEmpty ? model.DefaultRow : p.Item,
                itemKey: itemKey,
                size: size,
                rowHeight: m.Height,
                listHeight: Math.Min(count, ComboboxModel<TItem>.Rows) * m.Height,
                empty: count == 0
            );
    }

    static ArgumentException Missing(string prop) => new($"A combobox needs its {prop}.", prop);
}

internal readonly record struct ComboboxPopupProps<TItem>
{
    public required ComboboxModel<TItem> Model { get; init; }

    public Slot<TItem> Item { get; init; }

    public required KeyOf<TItem> ItemKey { get; init; }

    public ComponentSize Size { get; init; }

    public float RowHeight { get; init; }

    public float ListHeight { get; init; }

    public bool Empty { get; init; }
}

internal sealed partial class ComboboxPopup<TItem> : Component<ComboboxPopupProps<TItem>>
{
    protected override void Render(Ui ui, in ComboboxPopupProps<TItem> p)
    {
        var model = p.Model;
        using (
            ui.Box(
                direction: Direction.Column,
                opacity: ui.Presence(),
                tabNavigation: Navigation.None,
                arrowNavigation: Navigation.None
            )
        )
        using (ui.OverlayFrame(size: p.Size))
        {
            if (p.Empty)
                ui.EmptyState(text: "No results");
            else
                using (ui.Box(direction: Direction.Column, height: p.ListHeight))
                {
                    ui.VirtualList(
                        items: model.Shown,
                        itemKey: p.ItemKey,
                        row: p.Item,
                        rowHeight: p.RowHeight,
                        fixedRowHeight: true,
                        padding: new Thickness(
                            0f,
                            0f,
                            ListBarParts.Space(model.Scroll, ScrollbarVisibility.Auto),
                            0f
                        ),
                        scroll: model.Scroll,
                        tabIndex: -1,
                        grow: 1f
                    );
                    ui.ListBar(scroll: model.Scroll, visibility: ScrollbarVisibility.Auto);
                }
        }
    }
}

[FramePath]
internal sealed class ComboboxModel<TItem> : IDisposable
{
    internal const int Rows = 8;

    internal readonly TextMatches<TItem> Shown = new();
    internal readonly Callback<UiKey> Select;
    internal readonly Callback<UiKey> Activate;
    internal readonly Callback Typed;
    internal readonly Callback Toggle;
    internal readonly Callback Dismiss;
    internal readonly Handler<KeyEvent> KeyDown;
    internal readonly Handler<PointerEvent> PressField;
    internal readonly Slot<TItem> DefaultRow;
    internal Host<ComboboxProps<TItem>> Host;
    internal readonly State<bool> OpenState;
    internal readonly State<bool> Filtering;
    internal readonly State<UiKey> Active;
    internal readonly ScrollHandle Scroll;
    internal readonly NodeRef Text;
    internal readonly ObservableText Search = new();
    int _selected = -1;
    int _lit = -1;

    public ComboboxModel(
        State<bool> openState,
        State<bool> filtering,
        State<UiKey> active,
        ScrollHandle scroll,
        NodeRef text
    )
    {
        OpenState = openState;
        Filtering = filtering;
        Active = active;
        Scroll = scroll;
        Text = text;
        Select = Callback<UiKey>.Of(this, static (m, key) => m.Pick(key, close: true));
        Activate = Callback<UiKey>.Of(this, static (m, key) => m.Light(key));
        Typed = Callback.Of(this, static m => m.Type());
        Toggle = Callback.Of(this, static m => m.ToggleOpen());
        Dismiss = Callback.Of(this, static m => m.Close());
        KeyDown = Handler<KeyEvent>.Of(this, static (m, ref e) => m.Pressed(ref e));
        PressField = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Press(ref e));
        DefaultRow = Slot<TItem>.Of(
            this,
            static (ui, m, item, _) =>
            {
                ref readonly var p = ref m.Props;
                if (p.ItemKey is { } itemKey && p.ItemText is { } itemText)
                    ui.ComboboxItem(value: itemKey(item), text: itemText(item));
            }
        );
    }

    ref readonly ComboboxProps<TItem> Props => ref Host.Props;

    bool IsOpen => Props.Open ?? OpenState.Peek;

    internal TextValue SelectedText()
    {
        ref readonly var p = ref Props;
        if (
            !p.Value.IsKeyed
            || p.Items is not { } items
            || p.ItemKey is not { } key
            || p.ItemText is not { } text
        )
            return default;
        if (_selected >= 0 && _selected < items.Count && key(items.ItemAt(_selected)) == p.Value)
            return text(items.ItemAt(_selected));
        for (var i = 0; i < items.Count; i++)
        {
            if (key(items.ItemAt(i)) != p.Value)
                continue;
            _selected = i;
            return text(items.ItemAt(i));
        }
        _selected = -1;
        return default;
    }

    void Type()
    {
        Filtering.Value = true;
        Shown.Match(Search.Span);
        LightAt(Shown.Count > 0 ? 0 : -1);
        SetOpen(true);
    }

    void Pressed(ref KeyEvent e)
    {
        var modifiers = e.Modifiers;
        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Gui)) != 0)
            return;
        var alt = (modifiers & ModifierKeys.Alt) != 0;
        var open = IsOpen;
        switch (e.Key)
        {
            case Key.Up when open && alt:
            case Key.Escape when open:
                Close();
                break;
            case Key.Down or Key.Up when !open:
                OpenOn(down: e.Key == Key.Down);
                break;
            case Key.Down or Key.Up when !alt:
                Move(e.Key == Key.Down ? 1 : -1);
                break;
            case Key.PageDown or Key.PageUp when open && !alt:
                Move(e.Key == Key.PageDown ? Rows - 1 : 1 - Rows);
                break;
            case Key.Enter when open && LitIndex() >= 0:
                Pick(Active.Peek, close: true);
                break;
            case Key.Tab when open && (modifiers & ModifierKeys.Shift) == 0 && LitIndex() >= 0:
                Pick(Active.Peek, close: false);
                return;
            default:
                return;
        }
        e.StopPropagation();
        e.PreventDefault();
    }

    void Press(ref PointerEvent e) => Text.TakePress(ref e);

    void ToggleOpen()
    {
        if (IsOpen)
        {
            Close();
            return;
        }
        OpenOn(down: true);
        Text.Focus();
    }

    void OpenOn(bool down)
    {
        Filtering.Value = false;
        Shown.Match("");
        var at = Props.ItemKey is { } itemKey ? Shown.IndexOfKey(Props.Value, itemKey) : -1;
        LightAt(
            at >= 0 ? at
            : down ? 0
            : Shown.Count - 1
        );
        SetOpen(true);
    }

    void Move(int by)
    {
        var count = Shown.Count;
        if (count == 0)
            return;
        var at = LitIndex();
        LightAt(at < 0 ? (by > 0 ? 0 : count - 1) : Math.Clamp(at + by, 0, count - 1));
    }

    void Light(UiKey key)
    {
        _lit = -1;
        Active.Value = key;
    }

    void LightAt(int index)
    {
        _lit = index;
        Active.Value =
            index >= 0 && Props.ItemKey is { } itemKey ? itemKey(Shown.ItemAt(index)) : default;
        if (index >= 0)
            Scroll.ScrollToIndex(index);
    }

    int LitIndex()
    {
        var key = Active.Peek;
        if (!key.IsKeyed || Props.ItemKey is not { } itemKey)
            return -1;
        if (_lit >= 0 && _lit < Shown.Count && itemKey(Shown.ItemAt(_lit)) == key)
            return _lit;
        return _lit = Shown.IndexOfKey(key, itemKey);
    }

    void Pick(UiKey key, bool close)
    {
        Filtering.Value = false;
        if (key != Props.Value)
            Props.OnValueChange.Invoke(key);
        if (close)
            Close();
    }

    void Close()
    {
        Filtering.Value = false;
        SetOpen(false);
    }

    void SetOpen(bool open)
    {
        if (IsOpen == open)
            return;
        if (Props.Open is null)
            OpenState.Value = open;
        Props.OnOpenChange.Invoke(open);
    }

    public void Dispose() => Shown.Dispose();
}

/// <summary>The items of a source whose text contains a query, ignoring case, in the source's order; every item for an
/// empty one. A null item is never shown. It tells its listeners a reset whenever it matches again, and a render that read
/// <see cref="ShownCount"/> or <see cref="Query"/> renders again.</summary>
[FramePath]
internal sealed class TextMatches<TItem>
    : IItemsSource<TItem>,
        IItemsSourceListener,
        IObservable,
        IDisposable
{
    IItemsSource<TItem>? _source;
    TextOf<TItem>? _text;
    char[] _query = [];
    int _queryLength;
    int[] _map = [];
    int _count;
    ItemsSourceListeners _listeners;
    ObserverSet _observers;

    public ref ObserverSet Observers => ref _observers;

    public int Count => _count;

    public int ShownCount
    {
        get
        {
            Observation.Read(this, 0);
            return _count;
        }
    }

    public ReadOnlySpan<char> Query
    {
        get
        {
            Observation.Read(this, 0);
            return _query.AsSpan(0, _queryLength);
        }
    }

    public object? this[int index] =>
        _source is { } source
            ? source[_map[index]]
            : throw new ArgumentOutOfRangeException(nameof(index));

    public TItem ItemAt(int index) =>
        _source is { } source
            ? source.ItemAt(_map[index])
            : throw new ArgumentOutOfRangeException(nameof(index));

    public int IndexOf(object? item)
    {
        if (_source is not { } source)
            return -1;
        var at = source.IndexOf(item);
        if (at < 0)
            return -1;
        var shown = Array.BinarySearch(_map, 0, _count, at);
        return shown >= 0 ? shown : -1;
    }

    public int IndexOfKey(UiKey key, KeyOf<TItem> itemKey)
    {
        if (!key.IsKeyed)
            return -1;
        for (var i = 0; i < _count; i++)
        {
            if (itemKey(ItemAt(i)) == key)
                return i;
        }
        return -1;
    }

    public void Bind(IItemsSource<TItem> source, TextOf<TItem> text)
    {
        if (ReferenceEquals(source, _source) && ReferenceEquals(text, _text))
            return;
        if (!ReferenceEquals(source, _source))
        {
            _source?.RemoveListener(this);
            source.AddListener(this);
            _source = source;
        }
        _text = text;
        Refilter();
    }

    public void Match(ReadOnlySpan<char> query)
    {
        if (query.SequenceEqual(_query.AsSpan(0, _queryLength)))
            return;
        ValueList.EnsureCapacity(ref _query, query.Length);
        query.CopyTo(_query);
        _queryLength = query.Length;
        Refilter();
    }

    void Refilter()
    {
        var source = _source;
        var text = _text;
        var count = source?.Count ?? 0;
        if (_map.Length < count)
            _map = new int[Math.Max(count, _map.Length * 2)];
        var shown = 0;
        var query = _query.AsSpan(0, _queryLength);
        if (source is not null)
        {
            for (var i = 0; i < count; i++)
            {
                var item = source.ItemAt(i);
                if (
                    item is not null
                    && (
                        query.IsEmpty
                        || text is not null
                            && text(item).Span.Contains(query, StringComparison.OrdinalIgnoreCase)
                    )
                )
                    _map[shown++] = i;
            }
        }
        _count = shown;
        _listeners.Raise(this, ItemsChange.Reset);
        _observers.Notify(this, 0);
    }

    public void AddListener(IItemsSourceListener listener) => _listeners.Add(listener);

    public void RemoveListener(IItemsSourceListener listener) => _listeners.Remove(listener);

    void IItemsSourceListener.OnItemsChanged(IItemsSource source, in ItemsChange change) =>
        Refilter();

    public void Dispose()
    {
        _source?.RemoveListener(this);
        _source = null;
        _text = null;
        _count = 0;
    }
}

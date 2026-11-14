using Tralys.Ui.Fonts;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct SelectProps<TItem>
{
    public UiKey Value { get; init; }

    public Callback<UiKey> OnValueChange { get; init; }

    public bool? Open { get; init; }

    public bool DefaultOpen { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    public IItemsSource<TItem>? Items { get; init; }

    public KeyOf<TItem>? ItemKey { get; init; }

    /// <summary>What a row and the closed box show of an item while <see cref="Item"/> is empty, and the text typing
    /// and the search find it by; without it, they find no item.</summary>
    public TextOf<TItem>? ItemText { get; init; }

    /// <summary>A row of the dropdown, a slot of <c>ui.SelectItem</c>, through which the closed box also shows the
    /// selected item while <see cref="ValueContent"/> is empty.</summary>
    public Slot<TItem> Item { get; init; }

    /// <summary>What the closed box shows of the selected item, where it should differ from its row.</summary>
    public Slot<TItem> ValueContent { get; init; }

    /// <summary>Whether an item is disabled: its row greys out, and the keys and typing pass over it.</summary>
    public Func<TItem, bool>? ItemDisabled { get; init; }

    /// <summary>Leads the dropdown with a search box, which hides the items whose text doesn't contain its
    /// own.</summary>
    public bool Searchable { get; init; }

    /// <summary>Builds every row of the dropdown, however many it shows, as a short list does.</summary>
    public bool RealizeAll { get; init; }

    /// <summary>Lays the dropdown's items in this many equal columns, as a picker of glyphs does, the arrows moving
    /// across and down them; one column by default.</summary>
    public int Columns { get; init; }

    /// <summary>Shown in place of the value while nothing is selected.</summary>
    public TextValue Placeholder { get; init; }

    /// <summary>A muted label before the value.</summary>
    public TextValue Sublabel { get; init; }

    /// <summary>Whether the box shows its turning chevron; true when null.</summary>
    public bool? HasArrow { get; init; }

    public FieldVariant Variant { get; init; }

    /// <summary>The box's and the rows' rung; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    public Length Width { get; init; }

    /// <summary>The narrowest the dropdown gets, which is otherwise as wide as the box.</summary>
    public float PopupMinWidth { get; init; }

    public bool Disabled { get; init; }
}

public readonly record struct SelectItemProps
{
    public UiKey Value { get; init; }

    public TextValue Text { get; init; }

    /// <summary>What the row shows when it has no <see cref="Text"/>.</summary>
    public Slot Content { get; init; }
}

internal readonly record struct SelectContext(
    UiKey Value,
    UiKey Target,
    Callback<UiKey> Pick,
    Callback<UiKey> Focused,
    ComponentSize Size
);

internal readonly record struct SelectRowContext(bool Disabled, bool Face);

internal readonly record struct SelectRows<TItem>(Slot<TItem> Item, Func<TItem, bool>? Disabled);

/// <summary>A <see cref="Select{TItem}"/>'s parts.</summary>
public static partial class Select
{
    internal static readonly ContextKey<SelectContext> Context = new("Select");

    internal static readonly ContextKey<SelectRowContext> RowContext = new("Select.Row");

    /// <summary>A row of a select's dropdown in the shared row look: selected while it holds the value, lit while it
    /// has the focus, which a pointer moving over it gives it. A click on it, or Enter or Space while it has the
    /// focus, picks it and closes the dropdown; a pressable in its <see cref="SelectItemProps.Content"/> owns its own
    /// press. In the closed box it shows only its text, ending in an ellipsis, or its content.</summary>
    public sealed class Item : Component<SelectItemProps>
    {
        protected override void Render(Ui ui, in SelectItemProps p)
        {
            var row = ui.Use(RowContext);
            if (row.Face)
            {
                if (p.Text.IsNull)
                    using (ui.Box(align: Align.Center))
                        p.Content.Render(ui);
                else
                    ui.Text(p.Text, trimming: TextTrimming.CharacterEllipsis);
                return;
            }
            var c = ui.Use(Context);
            var self = ui.Self(p);
            var root = ui.Ref();
            ui.Effect(
                root,
                c.Target.IsKeyed && c.Target == p.Value,
                static (r, target, _) =>
                {
                    if (target)
                        r.Focus();
                }
            );
            using (
                ui.Box(
                    direction: Direction.Column,
                    disabled: row.Disabled,
                    onPointerMove: Handler<PointerEvent>.Of(
                        root,
                        static (r, ref e) => Moved(r, ref e)
                    ),
                    onClick: Callback.Of(
                        self,
                        static h => h.Use(Context).Pick.Invoke(h.Props.Value)
                    ),
                    onFocusChange: Callback<FocusChange>.Of(
                        self,
                        static (h, change) =>
                            h.Use(Context).Focused.Invoke(change.Focused ? h.Props.Value : default)
                    ),
                    nodeRef: root
                )
            )
            using (
                ui.ListBoxRow(
                    size: c.Size,
                    selected: c.Value.IsKeyed && c.Value == p.Value,
                    lit: ui.IsFocused,
                    minHeight: 0f,
                    disabled: row.Disabled
                )
            )
            {
                if (p.Text.IsNull)
                    p.Content.Render(ui);
                else
                    ui.Text(p.Text, grow: 1f, trimming: TextTrimming.CharacterEllipsis);
            }
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

/// <summary>A closed box showing the selected item of <see cref="SelectProps{TItem}.Items"/>, which opens them in a
/// dropdown beneath it. <see cref="SelectProps{TItem}.Value"/> is the selected item's key, which it reports through
/// <see cref="SelectProps{TItem}.OnValueChange"/>; its <see cref="SelectProps{TItem}.Open"/> reports through
/// <see cref="SelectProps{TItem}.OnOpenChange"/>, and is its own when null, starting at
/// <see cref="SelectProps{TItem}.DefaultOpen"/>. It writes neither.</summary>
/// <remarks>A press on the box, Space, Enter, F4, Alt+Down or Alt+Up opens it, focusing the selected row, else the
/// first enabled one, or with <see cref="SelectProps{TItem}.Searchable"/> its search box. While it's closed and
/// focused, Down and Up select the item after or before without wrapping, Home and End the first and last, and typing
/// the next item whose text starts with what was typed: a letter typed again steps through the items starting with
/// it, and a pause over a second starts afresh. Open, the rows take the focus, and the same keys move it instead, Page
/// Up and Page Down by a page, Tab and Shift+Tab one row; a pointer moving over a row focuses it. A click on a row, or
/// Enter or Space on the focused one, picks it and closes; F4, Alt+Down and Alt+Up pick the focused row and close;
/// Escape, a press outside and the focus leaving close without picking, and closing gives the focus back to the box.
/// The keys and typing pass over a disabled item. In the search box, Down or Tab reaches the first match, and Enter
/// closes when the selected item is among the matches, else picks the only one. The dropdown stands at most 400 tall
/// on the shared floating frame, as wide as the box.</remarks>
public sealed partial class Select<TItem> : Component<SelectProps<TItem>>
{
    internal const float MaxPopupHeight = 400f;

    protected override void Render(Ui ui, in SelectProps<TItem> p)
    {
        var items = p.Items ?? throw Missing(nameof(p.Items));
        var itemKey = p.ItemKey ?? throw Missing(nameof(p.ItemKey));
        if (p.ItemText is null && p.Item.IsEmpty)
            throw Missing(nameof(p.ItemText));
        var size = p.Size ?? ComponentSize.Md;
        var model = ui.Remember(static () => new SelectModel<TItem>());
        model.Host = ui.Self(p);
        model.OpenState = ui.State(p.DefaultOpen);
        model.Moves = ui.State(0);
        model.Scroll = ui.ScrollHandle();
        model.Shown.Bind(items, p.ItemText ?? SelectModel<TItem>.NoText);
        ui.Effect(model, 0, static (m, _, cx) => m.View = cx.View);
        _ = model.Moves.Value;
        ui.Effect(model, ui.IsDisabled, SelectModel<TItem>.ShutWhenDisabled);
        var open = (p.Open ?? model.OpenState.Value) && !ui.IsDisabled;
        model.Follow(open);
        var selected = model.SelectedIndex();
        ui.Provide(
            Select.Context,
            new SelectContext(p.Value, model.Target, model.Pick, model.Focused, size)
        );
        ui.Provide(Select.RowContext, new SelectRowContext(Disabled: false, Face: true));
        ui.Provide(
            SelectModel<TItem>.Rows,
            new SelectRows<TItem>(p.Item.IsEmpty ? model.DefaultItem : p.Item, p.ItemDisabled)
        );
        var face = selected >= 0 && !(p.ValueContent.IsEmpty && p.Item.IsEmpty);
        var field = ui.Ref();
        ui.PickerBox(
            open: open,
            text: selected >= 0 && !face ? p.ItemText!(items.ItemAt(selected)) : default,
            placeholder: p.Placeholder,
            sublabel: p.Sublabel,
            hasArrow: p.HasArrow ?? true,
            variant: p.Variant,
            size: size,
            width: p.Width,
            onClick: model.Toggle,
            field: field,
            onKeyDown: model.BoxKey,
            value: face ? model.Face : default,
            valueColor: ui.Theme.Field(p.Variant, ui.FieldState).Foreground
        );
        if (!open)
            return;
        using (
            ui.Positioner(
                anchor: field,
                matchAnchorWidth: true,
                restoreFocus: true,
                dismissOn: Dismiss.Escape | Dismiss.OutsidePress | Dismiss.FocusOutside,
                direction: Direction.Column,
                minWidth: p.PopupMinWidth,
                onDismiss: model.Close
            )
        )
            ui.SelectPopup<TItem>(
                model: model,
                items: p.Searchable ? model.Shown : items,
                itemKey: itemKey,
                size: size,
                searchable: p.Searchable,
                realizeAll: p.RealizeAll,
                columns: Math.Max(1, p.Columns)
            );
    }

    static ArgumentException Missing(string prop) => new($"A select needs its {prop}.", prop);
}

internal readonly record struct SelectPopupProps<TItem>
{
    public SelectModel<TItem>? Model { get; init; }

    public IItemsSource<TItem>? Items { get; init; }

    public KeyOf<TItem>? ItemKey { get; init; }

    public ComponentSize Size { get; init; }

    public bool Searchable { get; init; }

    public bool RealizeAll { get; init; }

    public int Columns { get; init; }
}

internal sealed partial class SelectPopup<TItem> : Component<SelectPopupProps<TItem>>
{
    const float Frame = 10f;

    protected override void Render(Ui ui, in SelectPopupProps<TItem> p)
    {
        var model = p.Model!;
        var m = ui.Theme.Metrics(p.Size);
        model.SearchText = ui.Ref();
        var count = p.Searchable ? model.Shown.ShownCount : p.Items!.Count;
        var extent = model.Scroll.Extent.Height;
        var most = Select<TItem>.MaxPopupHeight - Frame - (p.Searchable ? m.Height : 0f);
        var rows = (count + p.Columns - 1) / p.Columns;
        var height = MathF.Min(extent > 0f ? extent : rows * m.Height, most);
        using (
            ui.Box(direction: Direction.Column, opacity: ui.Presence(), onKeyDown: model.PopupKey)
        )
        using (ui.OverlayFrame(size: p.Size))
        {
            if (p.Searchable)
                using (ui.Box(direction: Direction.Column, onKeyDown: model.SearchKey))
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
                        nodeRef: model.SearchText
                    );
                }
            if (count == 0 && model.Shown.Query.Length > 0)
                ui.EmptyState(text: "No results");
            else
                using (ui.Box(direction: Direction.Column, height: height))
                {
                    ui.VirtualList(
                        items: p.Items!,
                        itemKey: p.ItemKey!,
                        row: SelectModel<TItem>.Row,
                        columns: p.Columns,
                        padding: new Thickness(
                            0f,
                            0f,
                            ListBarParts.Space(model.Scroll, ScrollbarVisibility.Auto),
                            0f
                        ),
                        scroll: model.Scroll,
                        realizeAll: p.RealizeAll,
                        grow: 1f
                    );
                    ui.ListBar(scroll: model.Scroll, visibility: ScrollbarVisibility.Auto);
                }
        }
    }
}

[FramePath]
internal sealed class SelectModel<TItem> : IDisposable
{
    const int NotTyped = -2;
    const int MaxTyped = 32;

    static readonly TimeSpan TypingPause = TimeSpan.FromSeconds(1);

    internal static readonly TextOf<TItem> NoText = static _ => "";

    internal static readonly ContextKey<SelectRows<TItem>> Rows = new("Select.Rows");

    internal static readonly Slot<TItem> Row = Slot<TItem>.Of(
        static (ui, item, index) =>
        {
            var rows = ui.Use(Rows);
            ui.Provide(
                Select.RowContext,
                new SelectRowContext(rows.Disabled is { } off && off(item), Face: false)
            );
            rows.Item.Render(ui, item, index);
        }
    );

    internal readonly TextMatches<TItem> Shown = new();
    internal readonly Callback Toggle;
    internal readonly Callback Close;
    internal readonly Callback<UiKey> Pick;
    internal readonly Callback<UiKey> Focused;
    internal readonly Callback Typed;
    internal readonly Callback Cleared;
    internal readonly Handler<KeyEvent> BoxKey;
    internal readonly Handler<KeyEvent> PopupKey;
    internal readonly Handler<KeyEvent> SearchKey;
    internal readonly Slot<TItem> DefaultItem;
    internal readonly Slot Face;
    internal Host<SelectProps<TItem>> Host;
    internal State<bool> OpenState = null!;
    internal State<int> Moves = null!;
    internal ScrollHandle Scroll = null!;
    internal NodeRef SearchText = null!;
    internal readonly ObservableText Query = new();
    internal UiView? View;
    internal UiKey Target;
    UiKey _lit;
    int _litAt = -1;
    int _selected = -1;
    bool _wasOpen;
    readonly char[] _typed = new char[MaxTyped];
    int _typedLength;
    TimeSpan _typedAt;

    public SelectModel()
    {
        Toggle = Callback.Of(this, static m => m.SetOpen(!m.IsOpen));
        Close = Callback.Of(this, static m => m.SetOpen(false));
        Pick = Callback<UiKey>.Of(
            this,
            static (m, key) =>
            {
                m.Choose(key);
                m.SetOpen(false);
            }
        );
        Focused = Callback<UiKey>.Of(this, static (m, key) => m.Arrived(key));
        Typed = Callback.Of(this, static m => m.Shown.Match(m.Query.Span));
        Cleared = Callback.Of(this, static m => m.Shown.Match(""));
        BoxKey = Handler<KeyEvent>.Of(this, static (m, ref e) => m.OnBox(ref e));
        PopupKey = Handler<KeyEvent>.Of(this, static (m, ref e) => m.OnPopup(ref e));
        SearchKey = Handler<KeyEvent>.Of(this, static (m, ref e) => m.OnSearch(ref e));
        DefaultItem = Slot<TItem>.Of(
            this,
            static (ui, m, item, _) =>
                ui.SelectItem(value: m.Props.ItemKey!(item), text: m.Props.ItemText!(item))
        );
        Face = Slot.Of(this, static (ui, m) => m.RenderFace(ui));
    }

    ref readonly SelectProps<TItem> Props => ref Host.Props;

    bool IsOpen => Props.Open ?? OpenState.Peek;

    IItemsSource<TItem> Listed => Props.Searchable ? Shown : Props.Items!;

    internal void Follow(bool open)
    {
        if (open == _wasOpen)
            return;
        _wasOpen = open;
        _typedLength = 0;
        _lit = default;
        Target = default;
        if (!open)
            return;
        Shown.Match("");
        if (Props.Searchable)
            return;
        var listed = Listed;
        var at = IndexOf(listed, Props.Value, -1);
        if (at < 0 || !Enabled(listed.ItemAt(at)))
            at = Navigable(listed, 0, 1);
        if (at < 0)
            return;
        Target = Props.ItemKey!(listed.ItemAt(at));
        Scroll.ScrollToIndex(at);
    }

    internal int SelectedIndex() => _selected = IndexOf(Props.Items!, Props.Value, _selected);

    int IndexOf(IItemsSource<TItem> source, UiKey key, int hint)
    {
        if (!key.IsKeyed)
            return -1;
        if (hint >= 0 && hint < source.Count && Is(source.ItemAt(hint), key))
            return hint;
        for (var i = 0; i < source.Count; i++)
        {
            if (Is(source.ItemAt(i), key))
                return i;
        }
        return -1;
    }

    bool Is(TItem item, UiKey key) => item is not null && Props.ItemKey!(item) == key;

    void RenderFace(Ui ui)
    {
        ref readonly var p = ref Props;
        var slot = p.ValueContent.IsEmpty ? p.Item : p.ValueContent;
        slot.Render(ui, p.Items!.ItemAt(_selected), _selected);
    }

    void OnBox(ref KeyEvent e)
    {
        var modifiers = e.Modifiers;
        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Gui)) != 0)
            return;
        if (Toggles(e.Key, modifiers))
        {
            if (IsOpen)
                PickLit();
            else
                SetOpen(true);
            Take(ref e);
            return;
        }
        if ((modifiers & ModifierKeys.Alt) != 0)
            return;
        if (IsOpen)
        {
            OnOpen(ref e);
            return;
        }
        var items = Props.Items!;
        var from = SelectedIndex();
        var to = Step(e.Key, items, from);
        if (to == NotTyped)
            to = Typeahead(e.Key, items, from);
        if (to == NotTyped)
            return;
        if (to >= 0 && to != from)
            Choose(Props.ItemKey!(items.ItemAt(to)));
        Take(ref e);
    }

    void OnPopup(ref KeyEvent e)
    {
        var modifiers = e.Modifiers;
        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Gui)) != 0)
            return;
        if (Toggles(e.Key, modifiers))
        {
            PickLit();
            Take(ref e);
            return;
        }
        if ((modifiers & ModifierKeys.Alt) == 0)
            OnOpen(ref e);
    }

    void OnOpen(ref KeyEvent e)
    {
        var lit = LitIndex();
        var listed = Listed;
        switch (e.Key)
        {
            case Key.Tab:
                if (lit < 0)
                    return;
                var back = (e.Modifiers & ModifierKeys.Shift) != 0;
                var next = Navigable(listed, lit + (back ? -1 : 1), back ? -1 : 1);
                if (next >= 0)
                    Light(next);
                else if (back && Props.Searchable)
                    SearchText.Focus(visible: true);
                break;
            case Key.Down or Key.Up or Key.Home or Key.End or Key.PageDown or Key.PageUp:
                if (lit < 0)
                {
                    var from = IndexOf(listed, Props.Value, -1);
                    var to = Step(
                        e.Key switch
                        {
                            Key.PageDown => Key.Down,
                            Key.PageUp => Key.Up,
                            _ => e.Key,
                        },
                        listed,
                        from
                    );
                    if (to >= 0)
                        Light(to);
                }
                break;
            default:
                if (Props.Searchable)
                    return;
                var found = Typeahead(
                    e.Key,
                    listed,
                    lit >= 0 ? lit : IndexOf(listed, Props.Value, -1)
                );
                if (found == NotTyped)
                    return;
                if (found >= 0)
                    Light(found);
                break;
        }
        Take(ref e);
    }

    void OnSearch(ref KeyEvent e)
    {
        var modifiers = e.Modifiers;
        if (Toggles(e.Key, modifiers))
            return;
        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Gui)) == 0)
        {
            switch (e.Key)
            {
                case Key.Down:
                case Key.Tab when (modifiers & ModifierKeys.Shift) == 0:
                    var first = Navigable(Shown, 0, 1);
                    if (first >= 0)
                        Light(first);
                    Take(ref e);
                    return;
                case Key.Tab or Key.Up or Key.PageUp or Key.PageDown:
                    Take(ref e);
                    return;
                case Key.Enter:
                    Submit();
                    Take(ref e);
                    return;
            }
        }
        e.StopPropagation();
    }

    void Submit()
    {
        if (IndexOf(Shown, Props.Value, -1) >= 0)
        {
            SetOpen(false);
            return;
        }
        var only = Navigable(Shown, 0, 1);
        if (only >= 0 && Navigable(Shown, only + 1, 1) < 0)
        {
            Choose(Props.ItemKey!(Shown.ItemAt(only)));
            SetOpen(false);
        }
    }

    static bool Toggles(Key key, ModifierKeys modifiers) =>
        (modifiers & ModifierKeys.Alt) != 0 ? key is Key.Up or Key.Down : key == Key.F4;

    static void Take(ref KeyEvent e)
    {
        e.StopPropagation();
        e.PreventDefault();
    }

    int Step(Key key, IItemsSource<TItem> source, int from) =>
        key switch
        {
            Key.Down => Navigable(source, from + 1, 1),
            Key.Up => from < 0 ? -1 : Navigable(source, from - 1, -1),
            Key.Home => Navigable(source, 0, 1),
            Key.End => Navigable(source, source.Count - 1, -1),
            _ => NotTyped,
        };

    int Navigable(IItemsSource<TItem> source, int index, int step)
    {
        for (var count = source.Count; index >= 0 && index < count; index += step)
        {
            if (Enabled(source.ItemAt(index)))
                return index;
        }
        return -1;
    }

    bool Enabled(TItem item) =>
        item is not null && (Props.ItemDisabled is not { } off || !off(item));

    int Typeahead(Key key, IItemsSource<TItem> source, int from)
    {
        char typed;
        if (key is >= Key.A and <= Key.Z)
            typed = (char)('a' + (key - Key.A));
        else if (key is >= Key.D0 and <= Key.D9)
            typed = (char)('0' + (key - Key.D0));
        else
            return NotTyped;
        var now = View?.Context.Now ?? default;
        if (now - _typedAt > TypingPause)
            _typedLength = 0;
        _typedAt = now;
        if (_typedLength < MaxTyped)
            _typed[_typedLength++] = typed;
        var prefix = _typed.AsSpan(0, _typedLength);
        var repeated = true;
        for (var i = 1; i < prefix.Length; i++)
            repeated &= prefix[i] == prefix[0];
        return repeated ? Starting(source, prefix[..1], from + 1) : Starting(source, prefix, from);
    }

    int Starting(IItemsSource<TItem> source, ReadOnlySpan<char> prefix, int from)
    {
        var count = source.Count;
        if (Props.ItemText is not { } text || count == 0)
            return -1;
        from = Math.Clamp(from, 0, count);
        for (var n = 0; n < count; n++)
        {
            var i = (from + n) % count;
            var item = source.ItemAt(i);
            if (
                Enabled(item)
                && text(item).Span.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            )
                return i;
        }
        return -1;
    }

    int LitIndex() => _litAt = IndexOf(Listed, _lit, _litAt);

    void Light(int index)
    {
        Scroll.ScrollToIndex(index, ScrollAlign.Nearest);
        var key = Props.ItemKey!(Listed.ItemAt(index));
        if (key == _lit)
            return;
        Target = key;
        _litAt = index;
        Moves.Value++;
    }

    void Arrived(UiKey key)
    {
        _lit = key;
        if (!key.IsKeyed || key != Target)
            return;
        Target = default;
        Moves.Value++;
    }

    void PickLit()
    {
        if (_lit.IsKeyed)
            Choose(_lit);
        SetOpen(false);
    }

    void Choose(UiKey key)
    {
        if (key != Props.Value)
            Props.OnValueChange.Invoke(key);
    }

    internal static readonly EffectFn<SelectModel<TItem>, bool> ShutWhenDisabled = static (
        m,
        disabled,
        _
    ) =>
    {
        if (disabled)
            m.SetOpen(false);
    };

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

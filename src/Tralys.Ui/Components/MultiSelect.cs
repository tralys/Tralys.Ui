using Tralys.Ui.Fonts;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct MultiSelectProps<TItem>
{
    public IItemsSource<TItem>? Items { get; init; }

    public KeyOf<TItem>? ItemKey { get; init; }

    /// <summary>What a row and the closed field show of an item while <see cref="Item"/> is empty.</summary>
    public TextOf<TItem>? ItemText { get; init; }

    /// <summary>What a row shows after its tick, and the closed field of its first ticked item.</summary>
    public Slot<TItem> Item { get; init; }

    /// <summary>Whether an item's row is disabled: it greys out, and the arrows pass over it.</summary>
    public Func<TItem, bool>? ItemDisabled { get; init; }

    /// <summary>The ticked items' keys.</summary>
    public ISelection? Value { get; init; }

    /// <summary>A row ticked or unticked, as a toggle of its key, which <c>SelectionSet.Apply</c> takes.</summary>
    public Callback<SelectionChange> OnValueChange { get; init; }

    /// <summary>Whether the dropdown shows; null leaves it to the field, starting at <see cref="DefaultOpen"/>.</summary>
    public bool? Open { get; init; }

    public bool DefaultOpen { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    /// <summary>Shown while nothing is ticked.</summary>
    public TextValue Placeholder { get; init; }

    /// <summary>A muted label before the field's value.</summary>
    public TextValue Sublabel { get; init; }

    public FieldVariant Variant { get; init; }

    /// <summary>The field's size; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    public Length Width { get; init; }

    /// <summary>The narrowest the dropdown gets, which is otherwise as wide as the field.</summary>
    public float PopupMinWidth { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A picker field over a set of keys: each item is one row of its dropdown, a ticked row's key is in
/// <see cref="MultiSelectProps{TItem}.Value"/>, and the closed field shows the first ticked item, in item order, and
/// how many more as <c>+N</c>, or the placeholder while nothing is ticked.</summary>
/// <remarks>A press on the field, or Down, Space or Enter while it has the focus, opens the dropdown, focusing its first
/// enabled row; a press on a row, or Space or Enter on the focused one, ticks or unticks it and leaves the dropdown
/// open. The arrows, Home, End, Page Up and Page Down move between the enabled rows, a pointer moving over a row
/// focuses it, and the focused row is the one lit. Escape, a press outside and the focus leaving close it, handing the
/// focus back to the field. <see cref="MultiSelectProps{TItem}.Open"/> reports through
/// <see cref="MultiSelectProps{TItem}.OnOpenChange"/>, and the ticks through
/// <see cref="MultiSelectProps{TItem}.OnValueChange"/>; the field never writes either.</remarks>
public sealed partial class MultiSelect<TItem> : Component<MultiSelectProps<TItem>>
{
    const float MaxPopupHeight = 400f;

    protected override void Render(Ui ui, in MultiSelectProps<TItem> p)
    {
        var items = p.Items ?? throw Missing(nameof(p.Items));
        var itemKey = p.ItemKey ?? throw Missing(nameof(p.ItemKey));
        var size = p.Size ?? ComponentSize.Md;
        var model = ui.Remember(static () => new MultiSelectModel<TItem>());
        model.Host = ui.Self(p);
        model.Own = ui.State(p.DefaultOpen);
        model.Changes = ui.State(0);
        model.Listen(items);
        _ = model.Changes.Value;
        ui.Effect(model, ui.IsDisabled, MultiSelectModel<TItem>.ShutWhenDisabled);
        var open = (p.Open ?? model.Own.Value) && !ui.IsDisabled;
        var first = -1;
        var ticked = 0;
        if (p.Value is { } value)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (!value.Contains(itemKey(items.ItemAt(i))))
                    continue;
                if (first < 0)
                    first = i;
                ticked++;
            }
        }
        model.First = first;
        model.Ticked = ticked;
        var field = ui.Ref();
        ui.PickerBox(
            open: open,
            text: default,
            placeholder: p.Placeholder,
            sublabel: p.Sublabel,
            hasArrow: true,
            variant: p.Variant,
            size: size,
            width: p.Width,
            onClick: model.Toggle,
            field: field,
            value: first >= 0 ? model.Summary : default,
            onKeyDown: model.FieldKey
        );

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
                onDismiss: model.Close
            )
        )
            ui.MultiSelectPopup<TItem>(
                model: model,
                value: p.Value,
                items: items,
                itemKey: itemKey,
                size: size,
                maxHeight: MaxPopupHeight
            );
    }

    static ArgumentException Missing(string prop) => new($"A multi-select needs its {prop}.", prop);
}

internal readonly record struct MultiSelectPopupProps<TItem>
{
    public MultiSelectModel<TItem>? Model { get; init; }

    public ISelection? Value { get; init; }

    public IItemsSource<TItem>? Items { get; init; }

    public KeyOf<TItem>? ItemKey { get; init; }

    public ComponentSize Size { get; init; }

    public float MaxHeight { get; init; }
}

internal sealed partial class MultiSelectPopup<TItem> : Component<MultiSelectPopupProps<TItem>>
{
    const float Inset = 4f;

    protected override void Render(Ui ui, in MultiSelectPopupProps<TItem> p)
    {
        var model = p.Model!;
        var items = p.Items!;
        var t = ui.Theme;
        var scroll = ui.ScrollHandle();
        var props = model.Host.Props;
        model.FirstEnabled = -1;
        for (var i = 0; i < items.Count && model.FirstEnabled < 0; i++)
        {
            if (props.ItemDisabled is not { } off || !off(items.ItemAt(i)))
                model.FirstEnabled = i;
        }
        model.PopupShown = ui.State(false);
        model.Opening = !model.PopupShown.Peek;
        ui.Effect(model, 0, static (m, _, _) => m.PopupShown.Value = true);
        var most = MathF.Max(0f, p.MaxHeight - 2f * Inset - 2f);
        var extent = scroll.Extent.Height;
        var height = MathF.Min(extent > 0f ? extent : items.Count * t.Metrics(p.Size).Height, most);
        using (
            ui.Box(
                direction: Direction.Column,
                opacity: ui.Presence(),
                tabNavigation: Navigation.Contained,
                onKeyDown: MultiSelectModel<TItem>.PopupKey
            )
        )
        using (ui.OverlayFrame(size: p.Size))
        using (ui.Box(direction: Direction.Column, height: height))
        {
            ui.VirtualList(
                items: items,
                itemKey: p.ItemKey!,
                row: model.Row,
                padding: new Thickness(
                    0f,
                    0f,
                    ListBarParts.Space(scroll, ScrollbarVisibility.Auto),
                    0f
                ),
                scroll: scroll,
                selectionMode: SelectionMode.Multiple,
                selection: model.Ticks.Of(p.Value),
                onSelectionChange: model.Picked,
                onAction: model.Activate,
                typeahead: props.ItemText,
                grow: 1f
            );
            ui.ListBar(scroll: scroll, visibility: ScrollbarVisibility.Auto);
        }
    }
}

[FramePath]
internal sealed class MultiSelectModel<TItem> : IItemsSourceListener, IDisposable
{
    internal static readonly Handler<KeyEvent> PopupKey = Handler<KeyEvent>.Of(
        static (ref KeyEvent e) =>
        {
            if (
                e.Key
                is Key.Up
                    or Key.Down
                    or Key.Home
                    or Key.End
                    or Key.PageUp
                    or Key.PageDown
                    or Key.Tab
            )
            {
                e.PreventDefault();
                e.StopPropagation();
            }
        }
    );

    internal readonly Slot<TItem> Row;
    internal readonly Slot Summary;
    internal readonly ShownSelection Ticks = new();
    internal readonly Callback Toggle;
    internal readonly Callback Close;
    internal readonly Callback<SelectionChange> Picked;
    internal readonly Callback<UiKey> Activate;
    internal readonly Handler<KeyEvent> FieldKey;
    internal Host<MultiSelectProps<TItem>> Host;
    internal State<bool> Own = null!;
    internal State<int> Changes = null!;
    internal int FirstEnabled = -1;
    internal int First;
    internal int Ticked;
    internal bool Opening;
    internal State<bool> PopupShown = null!;
    IItemsSource<TItem>? _items;

    public MultiSelectModel()
    {
        Row = Slot<TItem>.Of(
            this,
            static (ui, m, item, index) =>
            {
                ref readonly var p = ref m.Host.Props;
                using (
                    ui.ListBoxRow(
                        size: ui.Use(Overlay.Frame.Context).Size,
                        light: RowLight.Focus,
                        minHeight: 0f,
                        autoFocus: m.Opening && index == m.FirstEnabled,
                        disabled: p.ItemDisabled is { } off && off(item)
                    )
                )
                {
                    using (
                        ui.Box(
                            justify: Justify.Center,
                            width: 16f,
                            shrink: 0f,
                            margin: new Thickness(0f, 0f, 8f, 0f),
                            hidden: !ui.IsSelected
                        )
                    )
                        ui.Icon(glyph: IconGlyph.Of(IconRole.Check), size: 14f);
                    if (!p.Item.IsEmpty)
                        using (ui.Box(align: Align.Center, grow: 1f))
                            p.Item.Render(ui, item, index);
                    else if (p.ItemText is { } text)
                        ui.Text(text(item), grow: 1f, trimming: TextTrimming.CharacterEllipsis);
                }
            }
        );
        Summary = Slot.Of(
            this,
            static (ui, m) =>
            {
                ref readonly var p = ref m.Host.Props;
                var item = p.Items!.ItemAt(m.First);
                using (ui.Box(align: Align.Center, grow: 1f))
                {
                    if (!p.Item.IsEmpty)
                        p.Item.Render(ui, item, m.First);
                    else if (p.ItemText is { } text)
                        ui.Text(text(item), trimming: TextTrimming.CharacterEllipsis);
                }
                if (m.Ticked > 1)
                    ui.Text(
                        ui.Format($"+{m.Ticked - 1}"),
                        color: ui.Theme.Color(
                            ui.IsDisabled ? ColorToken.FgDisabled : ColorToken.FgMuted
                        ),
                        shrink: 0f,
                        margin: new Thickness(4f, 0f, 0f, 0f)
                    );
            }
        );
        Toggle = Callback.Of(this, static m => m.SetOpen(!m.IsOpen));
        Close = Callback.Of(this, static m => m.SetOpen(false));
        Picked = Callback<SelectionChange>.Of(this, static (m, change) => m.Pick(change));
        Activate = Callback<UiKey>.Of(this, static (m, key) => m.Tick(key));
        FieldKey = Handler<KeyEvent>.Of(this, static (m, ref e) => m.Field(ref e));
    }

    ref readonly MultiSelectProps<TItem> Props => ref Host.Props;

    bool IsOpen => Props.Open ?? Own.Peek;

    internal static readonly EffectFn<MultiSelectModel<TItem>, bool> ShutWhenDisabled = static (
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
            Own.Value = open;
        Props.OnOpenChange.Invoke(open);
    }

    void Field(ref KeyEvent e)
    {
        if (e.Modifiers != ModifierKeys.None)
            return;
        var open = IsOpen;
        if (!open && e.Key == Key.Down)
            SetOpen(true);
        else if (
            !open
            || e.Key
                is not (Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)
        )
            return;
        e.PreventDefault();
        e.StopPropagation();
    }

    void Pick(in SelectionChange change)
    {
        switch (change.Action)
        {
            case SelectionAction.Replace or SelectionAction.Toggle or SelectionAction.Range:
                Tick(change.Key);
                break;
            case SelectionAction.All or SelectionAction.Clear:
                Props.OnValueChange.Invoke(change);
                break;
        }
    }

    void Tick(UiKey key) =>
        Props.OnValueChange.Invoke(new SelectionChange(SelectionAction.Toggle, key, key));

    internal void Listen(IItemsSource<TItem> items)
    {
        if (ReferenceEquals(items, _items))
            return;
        _items?.RemoveListener(this);
        items.AddListener(this);
        _items = items;
    }

    public void OnItemsChanged(IItemsSource source, in ItemsChange change) => Changes.Value++;

    public void Dispose()
    {
        _items?.RemoveListener(this);
        _items = null;
    }
}

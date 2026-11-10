using Tralys.Ui.Fonts;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct SuggestionInputProps
{
    /// <summary>The text it edits, which its owner keeps.</summary>
    public required ObservableText Text { get; init; }

    /// <summary>What answers the typed text with rows; none offers no rows, unless <see cref="Rows"/> does.</summary>
    public ISuggestionSource? Source { get; init; }

    /// <summary>The rows it shows, kept by its owner; without a <see cref="Source"/> the owner fills them as its text
    /// changes. Null keeps rows of its own, sized by <see cref="MaxSuggestions"/>.</summary>
    public SuggestionList? Rows { get; init; }

    /// <summary>Runs with the row taken by Enter, Tab or a click, in place of writing the row's text.</summary>
    public Callback<SuggestionSlot> OnAccept { get; init; }

    /// <summary>Runs as the rows close on Escape, the text blanking or the focus leaving, for an owner of
    /// <see cref="Rows"/> to drop an answer still on its way.</summary>
    public Callback OnDismiss { get; init; }

    /// <summary>The most rows it shows of its own: 5 by default.</summary>
    public int? MaxSuggestions { get; init; }

    /// <summary>Shown when the source finds nothing; left empty, the rows close instead.</summary>
    public TextValue EmptyText { get; init; }

    /// <summary>Shown under the rows, inside the popup.</summary>
    public Slot Footer { get; init; }

    /// <summary>What a row shows in place of its text and detail, such as a player's avatar and name from the row's
    /// <see cref="SuggestionSlot.Value"/>, in the row's padding, lighting and press.</summary>
    public Slot<SuggestionSlot> Item { get; init; }

    /// <summary>Inside the field's border after its text, its presses its own, as an <see cref="Input"/>'s.</summary>
    public Slot Trailing { get; init; }

    /// <summary>Lets the rows grow past the field, for a field too narrow to read them at its own width.</summary>
    public float MinPopupWidth { get; init; }

    /// <summary>Past this the rows scroll, inside the popup's frame, for a list that must stop short of what covers the
    /// screen below it, such as a soft keyboard.</summary>
    public Length MaxPopupHeight { get; init; }

    public TextValue Placeholder { get; init; }

    public int MaxLength { get; init; }

    public int MaxBytes { get; init; }

    /// <summary>A glyph before the text, as a search field's glass.</summary>
    public IconGlyph LeadingIcon { get; init; }

    public bool Clearable { get; init; }

    public bool Invalid { get; init; }

    public FieldVariant Variant { get; init; }

    public ComponentSize? Size { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    public bool Disabled { get; init; }

    public bool AutoFocus { get; init; }

    /// <summary>Bound to its text, the node that takes the focus.</summary>
    public NodeRef? NodeRef { get; init; }

    /// <summary>Runs on Enter, when no row takes the key.</summary>
    public Callback OnSubmit { get; init; }
}

/// <summary>An <see cref="Input"/> whose typed text is its value, offering the rows its
/// <see cref="SuggestionInputProps.Source"/> answers as it is typed, up to
/// <see cref="SuggestionInputProps.MaxSuggestions"/>, or the rows its owner keeps in <see cref="SuggestionInputProps.Rows"/>,
/// in a popup below it as wide as it while it holds the focus; an answer that waits for typing to pause waits on the
/// field's frame clock. Up and
/// Down light the next or previous row, wrapping round; Enter and Tab take the lit row, writing its text, unless it
/// is the text already, and Escape closes the rows, stopping an answer on its way from opening them, before the key
/// reaches anything around the field. Any of those but Escape with a modifier held, and every key while no rows show,
/// stays the field's. A pointer moving over a row lights it and a click takes it, leaving the focus in the field.
/// Blanking the text or the focus leaving closes the rows.</summary>
public sealed class SuggestionInput : Component<SuggestionInputProps>
{
    protected override void Render(Ui ui, in SuggestionInputProps p)
    {
        var model = ui.Remember(static () => new SuggestionInputModel());
        var own = ui.Ref();
        model.Host = ui.Self(p);
        model.Bind(p);
        model.Text = p.NodeRef ?? own;
        model.Field = ui.Ref();
        ui.Effect(model, p.Source, static (m, source, _) => m.SourceChanged(source));
        ui.Effect(model, Math.Max(1, p.MaxSuggestions ?? 5), static (m, max, _) => m.Resize(max));
        ui.Effect(
            model,
            ui.IsFocusWithin,
            static (m, focused, _) =>
            {
                if (!focused)
                    m.Dismiss();
            }
        );
        var rows = model.Rows;
        rows.OpenWhenEmpty = !p.EmptyText.Span.IsEmpty;
        using (
            ui.Box(
                direction: Direction.Column,
                width: p.Width,
                onKeyDown: model.Keys,
                nodeRef: model.Field
            )
        )
            ui.Input(
                text: p.Text,
                onEdit: model.Typed,
                onClear: model.Typed,
                placeholder: p.Placeholder,
                maxLength: p.MaxLength,
                maxBytes: p.MaxBytes,
                leadingIcon: p.LeadingIcon,
                clearable: p.Clearable,
                invalid: p.Invalid,
                variant: p.Variant,
                size: p.Size,
                height: p.Height,
                trailing: p.Trailing,
                autoFocus: p.AutoFocus,
                nodeRef: model.Text
            );
        var open = rows.IsOpen;
        using (
            ui.Positioner(
                anchor: model.Field,
                matchAnchorWidth: true,
                minWidth: open && p.MinPopupWidth > 0f ? p.MinPopupWidth : Length.Auto,
                direction: Direction.Column
            )
        )
            ui.SuggestionRows(
                rows: rows,
                emptyText: p.EmptyText,
                footer: p.Footer,
                item: p.Item,
                onAccept: model.Accept,
                maxHeight: p.MaxPopupHeight,
                collapsed: !open
            );
    }
}

public readonly record struct SuggestionRowsProps
{
    public SuggestionList? Rows { get; init; }

    /// <summary>Shown while an answer found nothing and the rows opened empty.</summary>
    public TextValue EmptyText { get; init; }

    /// <summary>Shown under the rows.</summary>
    public Slot Footer { get; init; }

    /// <summary>What a row shows in place of its text and detail.</summary>
    public Slot<SuggestionSlot> Item { get; init; }

    /// <summary>A left click on a row, which lights it first.</summary>
    public Callback<SuggestionSlot> OnAccept { get; init; }

    /// <summary>Past this the rows scroll, inside the frame.</summary>
    public Length MaxHeight { get; init; }

    public Length Width { get; init; }
}

/// <summary>A suggestion list's rows in a small floating frame, while the list is open: each row its text, cut with an
/// ellipsis, then its detail in the muted colour, under its group's caption, the lit row on the hover fill; then the
/// empty text while an answer found nothing, how many more matched, and the footer. A press anywhere on it leaves the
/// focus where it is. While it shows a list, an answer that waits for typing to pause waits on its view's frame
/// clock.</summary>
public sealed class SuggestionRows : Component<SuggestionRowsProps>
{
    static readonly Handler<PointerEvent> KeepFocus = Handler<PointerEvent>.Of(
        static (ref e) => e.PreventDefault()
    );

    protected override void Render(Ui ui, in SuggestionRowsProps p)
    {
        var shown = ui.Remember(static () => new SuggestionsShown());
        ui.Effect(shown, p.Rows, static (s, rows, cx) => s.Show(rows, cx.View.Context));
        var t = ui.Theme;
        var m = t.Metrics(ComponentSize.Sm);
        var rows = p.Rows;
        var count = rows is { IsOpen: true } ? rows.Count : 0;
        var weight = t.Weight(WeightToken.Content);
        using (ui.Box(direction: Direction.Column, width: p.Width, onPointerDown: KeepFocus))
        using (ui.OverlayFrame(size: ComponentSize.Sm))
        using (ui.ScrollArea(horizontal: ScrollbarVisibility.Disabled, maxHeight: p.MaxHeight))
        using (ui.Box(direction: Direction.Column))
        {
            for (var i = 0; i < count; i++)
            {
                var slot = rows!.Slots[i];
                ui.SuggestionRow(
                    slot: slot,
                    text: slot.Text,
                    header: slot.Header,
                    detail: slot.Detail,
                    item: p.Item,
                    index: i,
                    lit: rows.IsLit(slot),
                    rows: rows,
                    onAccept: p.OnAccept,
                    key: i
                );
            }
            if (rows is { IsEmpty: true } && !p.EmptyText.Span.IsEmpty)
                ui.Text(
                    p.EmptyText,
                    color: t.Color(ColorToken.FgMuted),
                    fontSize: m.FontSize,
                    fontWeight: weight,
                    margin: m.Padding
                );
            if (rows?.MoreCount is > 0 and var more)
                ui.Text(
                    ui.Format($"+{more} more · keep typing"),
                    color: t.Color(ColorToken.FgDisabled),
                    fontSize: m.FontSize,
                    fontWeight: weight,
                    margin: m.Padding
                );
            p.Footer.Render(ui);
        }
    }
}

[FramePath]
internal sealed class SuggestionsShown : IDisposable
{
    SuggestionList? _rows;

    public void Show(SuggestionList? rows, UiContext context)
    {
        if (ReferenceEquals(rows, _rows))
            return;
        _rows?.Hide();
        rows?.Show(context);
        _rows = rows;
    }

    public void Dispose()
    {
        _rows?.Hide();
        _rows = null;
    }
}

internal readonly record struct SuggestionRowProps
{
    public SuggestionSlot? Slot { get; init; }

    public string? Text { get; init; }

    public string? Header { get; init; }

    public string? Detail { get; init; }

    public Slot<SuggestionSlot> Item { get; init; }

    public int Index { get; init; }

    public bool Lit { get; init; }

    public SuggestionList? Rows { get; init; }

    public Callback<SuggestionSlot> OnAccept { get; init; }
}

/// <summary>One row of <see cref="SuggestionRows"/>: the pointer moving over it lights it, and a left click takes it
/// without taking the focus; its group's caption above it takes no input.</summary>
internal sealed class SuggestionRow : Component<SuggestionRowProps>
{
    protected override void Render(Ui ui, in SuggestionRowProps p)
    {
        var t = ui.Theme;
        var m = t.Metrics(ComponentSize.Sm);
        var self = ui.Self(p);
        using (ui.Box(direction: Direction.Column, fontSize: m.FontSize))
        {
            if (!string.IsNullOrEmpty(p.Header))
                ui.Text(
                    p.Header,
                    color: t.Color(ColorToken.FgMuted),
                    fontWeight: t.Weight(WeightToken.Interface),
                    variant: TextVariant.Caption,
                    pointerEvents: PointerEvents.None,
                    margin: m.Padding
                );
            using (
                ui.Box(
                    align: Align.Center,
                    padding: m.Padding,
                    radius: t.Radius(RadiusToken.Md),
                    background: ui.IsPressed ? t.Color(ColorToken.StatePressed)
                        : p.Lit ? t.Color(ColorToken.StateHover)
                        : default(Paint),
                    cursor: Cursors.Hand,
                    focusable: false,
                    onClick: Callback.Of(self, static h => Accept(h)),
                    onPointerDown: InputClear.KeepPress,
                    onPointerMove: Handler<PointerEvent>.Of(
                        self,
                        static (h, ref e) =>
                        {
                            if (e.Phase == PointerPhase.Move && h.Props.Slot is { } slot)
                                h.Props.Rows?.Select(slot);
                        }
                    )
                )
            )
            {
                if (!p.Item.IsEmpty && p.Slot is { } shown)
                {
                    using (ui.Box(align: Align.Center, grow: 1f, minWidth: 0f))
                        p.Item.Render(ui, shown, p.Index);
                    return;
                }
                ui.Text(
                    p.Text,
                    fontWeight: t.Weight(WeightToken.Interface),
                    trimming: TextTrimming.CharacterEllipsis,
                    grow: 1f,
                    shrink: 1f,
                    minWidth: 0f
                );
                if (!string.IsNullOrEmpty(p.Detail))
                    ui.Text(
                        p.Detail,
                        color: t.Color(ColorToken.FgMuted),
                        fontWeight: t.Weight(WeightToken.Content),
                        shrink: 0f,
                        margin: new Thickness(8f, 0f, 0f, 0f)
                    );
            }
        }
    }

    [FramePath]
    static void Accept(Host<SuggestionRowProps> h)
    {
        ref readonly var p = ref h.Props;
        if (p.Slot is not { } slot)
            return;
        p.Rows?.Select(slot);
        p.OnAccept.Invoke(slot);
    }
}

[FramePath]
internal sealed class SuggestionInputModel : IDisposable
{
    readonly SuggestionList _own = new(5);
    internal readonly Callback Typed;
    internal readonly Callback<SuggestionSlot> Accept;
    internal readonly Handler<KeyEvent> Keys;
    internal Host<SuggestionInputProps> Host;
    internal NodeRef Text = null!;
    internal NodeRef Field = null!;
    ISuggestionSource? _source;

    public SuggestionInputModel()
    {
        Rows = _own;
        Typed = Callback.Of(this, static m => m.Edited());
        Accept = Callback<SuggestionSlot>.Of(this, static (m, _) => m.AcceptLit());
        Keys = Handler<KeyEvent>.Of(this, static (m, ref e) => m.KeyDown(ref e));
    }

    Callback _onDismiss;

    // Kept from the last render, which is the last the field read them: unmounting disposes it with its host gone.
    internal SuggestionList Rows { get; private set; }

    internal void Bind(in SuggestionInputProps p)
    {
        Rows = p.Rows ?? _own;
        _onDismiss = p.OnDismiss;
    }

    bool Focused => Text.Node is { } node && (node.States & InputStates.Focused) != 0;

    void Edited()
    {
        var query = Host.Props.Text.Span.Trim();
        if (query.IsEmpty || !Focused)
            Dismiss();
        else if (_source is { } source)
            source.Suggest(query, Rows);
        else if (Host.Props.Rows is null)
            Dismiss();
    }

    void KeyDown(ref KeyEvent e)
    {
        if (e.Key == Key.Escape)
        {
            var shown = Rows.IsOpen;
            Dismiss();
            if (shown)
                Take(ref e);
            return;
        }
        if (e.Modifiers != ModifierKeys.None)
            return;
        if (Rows.IsOpen)
        {
            switch (e.Key)
            {
                case Key.Down or Key.Up when Rows.Count > 0:
                    Rows.Move(e.Key == Key.Down ? 1 : -1);
                    Take(ref e);
                    return;
                case Key.Enter or Key.Tab:
                    if (AcceptLit())
                    {
                        Take(ref e);
                        return;
                    }
                    break;
            }
        }
        if (e.Key == Key.Enter && !Host.Props.OnSubmit.IsEmpty)
        {
            Host.Props.OnSubmit.Invoke();
            Take(ref e);
        }
    }

    static void Take(ref KeyEvent e)
    {
        e.PreventDefault();
        e.StopPropagation();
    }

    bool AcceptLit()
    {
        var row = Rows.Selected;
        Dismiss();
        if (row is null)
            return false;
        if (!Host.Props.OnAccept.IsEmpty)
        {
            Host.Props.OnAccept.Invoke(row);
            return true;
        }
        if (Host.Props.Text.Span.SequenceEqual(row.Text))
            return false;
        Host.Props.Text.Set(row.Text, Index.End);
        return true;
    }

    internal void Dismiss()
    {
        _source?.Cancel();
        Rows.Close();
        _onDismiss.Invoke();
    }

    internal void SourceChanged(ISuggestionSource? source)
    {
        if (ReferenceEquals(source, _source))
            return;
        _source?.Cancel();
        _source = source;
        Rows.Close();
    }

    internal void Resize(int max)
    {
        _source?.Cancel();
        _own.Resize(max);
    }

    public void Dispose() => Dismiss();
}

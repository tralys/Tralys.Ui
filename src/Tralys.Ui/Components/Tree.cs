using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct TreeProps<TRow>
    where TRow : TreeRow
{
    /// <summary>The rows the tree shows: a <see cref="TreeRows{T}"/> flattens each row's children beneath it while it
    /// is expanded, filtered and sorted as it says.</summary>
    public TreeRows<TRow>? Items { get; init; }

    public KeyOf<TRow>? ItemKey { get; init; }

    /// <summary>The text a row shows when <see cref="Item"/> is unset, and that typing a row's first letters finds it
    /// by.</summary>
    public TextOf<TRow>? ItemText { get; init; }

    /// <summary>Renders a row, as a <c>ui.TreeItem</c> that takes its depth, children and expansion from the tree;
    /// a <c>ui.TreeItem</c> of <see cref="ItemText"/> by default.</summary>
    public Slot<TRow> Item { get; init; }

    /// <summary>The selected row's key; one naming no row selects none.</summary>
    public UiKey Value { get; init; }

    public Callback<UiKey> OnValueChange { get; init; }

    /// <summary><see cref="Tralys.Ui.SelectionMode.None"/> turns selection off; a tree selects one row at a time
    /// otherwise.</summary>
    public SelectionMode? SelectionMode { get; init; }

    /// <summary>A row the tree would open or shut: it shows so once the app has set the row's
    /// <see cref="TreeRow.IsExpanded"/>.</summary>
    public Callback<TreeExpandedChange<TRow>> OnExpandedChange { get; init; }

    /// <summary>Sizes every row; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    /// <summary>Scrolls the rows, as to bring a row the app selected into view; one of the tree's own by
    /// default.</summary>
    public ScrollHandle? Scroll { get; init; }

    /// <summary>When the bar down the tree's right edge shows; while it does, the rows stop short of it.</summary>
    public ScrollbarVisibility VerticalScrollbar { get; init; }

    /// <summary>When the bar along the tree's bottom edge shows, while rows wider than the tree scroll sideways; while it
    /// does, the rows stop short of it.</summary>
    public ScrollbarVisibility HorizontalScrollbar { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A row a tree would open (<see cref="Expanded"/>) or shut.</summary>
public readonly record struct TreeExpandedChange<TRow>(TRow Row, bool Expanded);

public readonly record struct TreeItemProps
{
    public TextValue Text { get; init; }

    /// <summary>What the row shows when it has no <see cref="Text"/>.</summary>
    public Slot Content { get; init; }

    /// <summary>How many levels the row sits below the top, each indenting it four spacing units; a tree's row takes
    /// its own.</summary>
    public int? Depth { get; init; }

    /// <summary>Whether the row shows the arrow that opens it; a tree's row takes its own.</summary>
    public bool? HasChildren { get; init; }

    /// <summary>Whether the row shows open; a tree's row takes its own.</summary>
    public bool? Expanded { get; init; }

    /// <summary>A press on the arrow or a double press, outside a tree; a tree reports its own.</summary>
    public Callback<bool> OnExpandedChange { get; init; }

    /// <summary>Sizes the row outside a tree; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    public bool Disabled { get; init; }
}

internal readonly record struct TreeContext(TreeModel? Model, ComponentSize Size);

internal readonly record struct TreeRowContext(TreeRow? Row, UiKey Key);

/// <summary>A <see cref="Tree{TRow}"/>'s parts and context keys.</summary>
public static partial class Tree
{
    internal static readonly ContextKey<TreeContext> Context = new(
        "Tree",
        new TreeContext(null, ComponentSize.Md)
    );

    internal static readonly ContextKey<TreeRowContext> RowContext = new("TreeRow");

    /// <summary>A row of a tree: indented by its depth, then the arrow that opens it while it has children, then its
    /// text. It lights under the pointer, fills with the selected-row look while selected, and outlines itself while
    /// it has the focus but isn't selected. A double press, or a press on its arrow, opens or shuts it; the arrow's
    /// press selects nothing. Outside a tree, as a row of a flat list of tree rows, it takes its depth, children and
    /// expansion from its props.</summary>
    public sealed class Item : Component<TreeItemProps>
    {
        protected override void Render(Ui ui, in TreeItemProps p)
        {
            var tree = ui.Use(Context);
            var at = ui.Use(RowContext);
            var t = ui.Theme;
            var m = t.Metrics(p.Size ?? tree.Size);
            var row = at.Row;
            var depth = p.Depth ?? row?.Depth ?? 0;
            var hasChildren = p.HasChildren ?? row?.HasChildren ?? false;
            var expanded = p.Expanded ?? row?.IsExpanded ?? false;
            var entry = ui.Remember(static () => TreeItemEntry.Rent());
            entry.Bind(tree.Model, row, at.Key, ui.Self(p), ui.Ref());
            ui.Effect(entry, at.Key, static (e, _, _) => e.Shown());
            var disabled = ui.IsDisabled;
            var selected = ui.IsSelected;
            var radius = t.Radius(RadiusToken.Md);
            using (
                ui.Box(
                    align: Align.Center,
                    minHeight: m.Height,
                    padding: m.Padding,
                    radius: radius,
                    background: selected ? t.Color(ComponentColor.RowBgSelected)
                        : ui.IsHovered && !disabled ? t.Color(ColorToken.StateHover)
                        : default(Paint),
                    color: disabled ? t.Color(ColorToken.FgDisabled)
                        : selected ? t.Color(ColorToken.FgAccent)
                        : default(Paint),
                    fontSize: m.FontSize,
                    fontWeight: t.Weight(WeightToken.Interface),
                    cursor: disabled ? null : Cursors.Hand,
                    onDoubleClick: entry.DoubleClick,
                    pressKeys: PressKeys.Space,
                    onKeyDown: entry.KeyDown,
                    onFocusChange: entry.FocusChange,
                    nodeRef: entry.Ref
                )
            )
            {
                using (ui.Box(width: depth * 4f * t.Spacing, shrink: 0f)) { }
                ui.TreeDisclosure(
                    expanded: expanded,
                    onToggle: entry.Toggle,
                    onPress: entry.ArrowPress,
                    shrink: 0f,
                    hidden: !hasChildren
                );
                using (ui.Box(align: Align.Center, margin: new Thickness(4f, 0f, 0f, 0f)))
                {
                    if (!p.Text.IsNull)
                        ui.Text(p.Text);
                    else
                        p.Content.Render(ui);
                }
                if (ui.IsFocused && !selected)
                    using (
                        ui.Box(
                            border: new Border(1f, t.Color(ColorToken.BorderFocus)),
                            radius: radius,
                            pointerEvents: PointerEvents.None,
                            position: Position.Absolute,
                            left: 0f,
                            top: 0f,
                            right: 0f,
                            bottom: 0f
                        )
                    ) { }
            }
        }
    }
}

/// <summary>A tree of rows that open beneath one another, controlled by <see cref="TreeProps{TRow}.Value"/>, the
/// selected row's key, which it reports through <see cref="TreeProps{TRow}.OnValueChange"/>, and by each row's
/// <see cref="TreeRow.IsExpanded"/>, which it reports through <see cref="TreeProps{TRow}.OnExpandedChange"/>. Its rows
/// are a <c>VirtualList</c> of <see cref="TreeProps{TRow}.Items"/>, so a tree of thousands draws only what shows.</summary>
/// <remarks>It is one Tab stop, landing on the selected row, and a row Tab lands on is selected. A press selects a row,
/// a touch or pen on its tap; Up and Down move to the row above or below that can take the focus, without wrapping,
/// and Home and End to the first and last, each selecting the row it reaches. Right opens a shut row, and on an open
/// one moves to its first child; Left shuts an open row, and on a shut one moves to its parent; numpad + and - open and
/// shut a row, and * opens it and every row below it. It scrolls both ways when its rows run past it, with a bar down its
/// right edge while they run past its foot.</remarks>
public sealed partial class Tree<TRow> : Component<TreeProps<TRow>>
    where TRow : TreeRow
{
    protected override void Render(Ui ui, in TreeProps<TRow> p)
    {
        var items = p.Items ?? throw Missing(nameof(p.Items));
        var itemKey = p.ItemKey ?? throw Missing(nameof(p.ItemKey));
        var t = ui.Theme;
        var size = p.Size ?? ComponentSize.Md;
        var model = ui.Remember(static () => new TreeModel<TRow>());
        model.Host = ui.Self(p);
        model.Scroll = p.Scroll ?? ui.ScrollHandle();
        var selects = p.SelectionMode != SelectionMode.None;
        model.Selects = selects;
        ui.Provide(Tree.Context, new TreeContext(model, size));
        var sideways = ui.ScrollHandle();
        var bar = ListBarParts.Space(model.Scroll, p.VerticalScrollbar);
        var under = ListBarParts.SidewaysSpace(sideways, p.HorizontalScrollbar);
        using (
            ui.Box(
                direction: Direction.Column,
                width: p.Width,
                height: p.Height,
                color: ui.IsDisabled ? t.Color(ColorToken.FgDisabled) : default(Paint),
                onKeyDown: ListEnds.UpDown
            )
        )
        {
            using (
                ui.Box(
                    grow: 1f,
                    basis: 0f,
                    overflow: Overflow.ScrollX,
                    scroll: sideways,
                    panning: Panning.HorizontalOnly
                )
            )
                ui.VirtualList(
                    items: items,
                    itemKey: itemKey,
                    row: model.Row,
                    rowHeight: t.Metrics(size).Height,
                    padding: new Thickness(0f, 0f, bar, 0f),
                    scroll: model.Scroll,
                    selectionMode: selects ? SelectionMode.Single : SelectionMode.None,
                    selection: model.Selection.Of(selects ? p.Value : default),
                    onSelectionChange: model.Picked,
                    typeahead: p.ItemText,
                    tabIndex: -1,
                    minWidth: Length.Full
                );
            ui.ListBar(scroll: model.Scroll, visibility: p.VerticalScrollbar, bottom: under);
            ui.SidewaysListBar(scroll: sideways, visibility: p.HorizontalScrollbar, right: bar);
        }
    }

    static ArgumentException Missing(string prop) => new($"A tree needs its {prop}.", prop);
}

internal readonly record struct TreeDisclosureProps
{
    public bool Expanded { get; init; }

    public Callback OnToggle { get; init; }

    public Handler<PointerEvent> OnPress { get; init; }
}

internal sealed class TreeDisclosure : Component<TreeDisclosureProps>
{
    protected override void Render(Ui ui, in TreeDisclosureProps p)
    {
        var t = ui.Theme;
        var turn = ui.Animate(
            p.Expanded ? 1f : 0f,
            t.Motion(DurationToken.Disclosure, EasingToken.Settle)
        );
        var color = t.Color(
            ui.IsDisabled ? ColorToken.IconDisabled
            : ui.IsHovered ? ColorToken.IconHover
            : ColorToken.Icon
        );
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                width: 16f,
                focusable: false,
                cursor: Cursors.Hand,
                onClick: p.OnToggle,
                onPointerDown: p.OnPress
            )
        )
        using (ui.Box(transform: Transform.Rotate(turn * 90f)))
            ui.Icon(glyph: IconGlyph.Of(IconRole.ChevronRight), size: 14f, color: color);
    }
}

[FramePath]
internal abstract class TreeModel
{
    readonly Dictionary<UiKey, TreeItemEntry> _shown = [];
    internal readonly ShownKey Selection = new();
    internal ScrollHandle Scroll = null!;
    internal bool Selects;
    UiKey _pendingFocus;

    internal abstract int Count { get; }

    internal abstract TreeRow At(int index);

    internal abstract int IndexOf(TreeRow row);

    internal abstract UiKey KeyOf(TreeRow row);

    internal abstract UiKey Value { get; }

    internal abstract void ReportValue(UiKey key);

    internal abstract void ReportExpanded(TreeRow row, bool expanded);

    internal void Show(TreeItemEntry entry)
    {
        if (entry.Key.IsKeyed)
            _shown[entry.Key] = entry;
    }

    internal void Forget(TreeItemEntry entry)
    {
        if (
            entry.Key.IsKeyed
            && _shown.TryGetValue(entry.Key, out var shown)
            && ReferenceEquals(shown, entry)
        )
            _shown.Remove(entry.Key);
    }

    internal void Select(UiKey key)
    {
        if (Selects && key.IsKeyed && key != Value)
            ReportValue(key);
    }

    internal void Shown(TreeItemEntry entry)
    {
        if (!_pendingFocus.IsKeyed || entry.Key != _pendingFocus)
            return;
        _pendingFocus = default;
        entry.Ref.Focus(visible: true);
    }

    internal void Pressed(TreeItemEntry entry, ref KeyEvent e)
    {
        if (
            (e.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Gui)) != 0
            || entry.Row is not { } row
        )
            return;
        switch (e.Key)
        {
            case Key.Right or Key.Add when row.HasChildren && !row.IsExpanded:
                ReportExpanded(row, true);
                break;
            case Key.Right when row.HasChildren:
                MoveTo(FirstChild(row));
                break;
            case Key.Left or Key.Subtract when row.HasChildren && row.IsExpanded:
                ReportExpanded(row, false);
                break;
            case Key.Left:
                MoveTo(Parent(row));
                break;
            case Key.Multiply:
                OpenBelow(row);
                break;
            default:
                return;
        }
        e.PreventDefault();
        e.StopPropagation();
    }

    internal void Toggle(TreeItemEntry entry)
    {
        if (entry.Row is { HasChildren: true } row)
            ReportExpanded(row, !row.IsExpanded);
    }

    void OpenBelow(TreeRow row)
    {
        if (row.HasChildren && !row.IsExpanded)
            ReportExpanded(row, true);
        var depth = row.Depth;
        for (var i = IndexOf(row) + 1; i > 0 && i < Count; i++)
        {
            var below = At(i);
            if (below.Depth <= depth)
                break;
            if (below.HasChildren && !below.IsExpanded)
                ReportExpanded(below, true);
        }
    }

    int FirstChild(TreeRow row)
    {
        var at = IndexOf(row);
        if (at < 0)
            return -1;
        var depth = row.Depth;
        for (var i = at + 1; i < Count; i++)
        {
            var below = At(i);
            if (below.Depth <= depth)
                break;
            if (below.Depth == depth + 1 && Reachable(below))
                return i;
        }
        return -1;
    }

    int Parent(TreeRow row)
    {
        var depth = row.Depth;
        if (depth == 0)
            return -1;
        for (var i = IndexOf(row) - 1; i >= 0; i--)
        {
            if (At(i).Depth < depth)
                return i;
        }
        return -1;
    }

    bool Reachable(TreeRow row) =>
        !_shown.TryGetValue(KeyOf(row), out var entry)
        || entry.Ref.Node is not { } node
        || (node.States & InputStates.Disabled) == 0;

    void MoveTo(int index)
    {
        if (index < 0)
            return;
        var key = KeyOf(At(index));
        Select(key);
        Scroll.ScrollToIndex(index);
        if (_shown.TryGetValue(key, out var entry) && entry.Ref.Node is { IsMounted: true })
            entry.Ref.Focus(visible: true);
        else
            _pendingFocus = key;
    }
}

[FramePath]
internal sealed class TreeModel<TRow> : TreeModel
    where TRow : TreeRow
{
    internal readonly Slot<TRow> Row;
    internal readonly Callback<SelectionChange> Picked;
    internal Host<TreeProps<TRow>> Host;

    public TreeModel()
    {
        Row = Slot<TRow>.Of(this, static (ui, m, row, index) => m.RenderRow(ui, row, index));
        Picked = Callback<SelectionChange>.Of(this, static (m, change) => m.Select(change.Key));
    }

    ref readonly TreeProps<TRow> Props => ref Host.Props;

    internal override int Count => Props.Items?.Count ?? 0;

    internal override TreeRow At(int index) => Props.Items![index];

    internal override int IndexOf(TreeRow row) => Props.Items?.IndexOf(row) ?? -1;

    internal override UiKey KeyOf(TreeRow row) => Props.ItemKey!((TRow)row);

    internal override UiKey Value => Props.Value;

    internal override void ReportValue(UiKey key) => Props.OnValueChange.Invoke(key);

    internal override void ReportExpanded(TreeRow row, bool expanded) =>
        Props.OnExpandedChange.Invoke(new TreeExpandedChange<TRow>((TRow)row, expanded));

    void RenderRow(
        Ui ui,
        TRow row,
        int index,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    )
    {
        ref readonly var p = ref Props;
        ui.Provide(Tree.RowContext, new TreeRowContext(row, p.ItemKey!(row)));
        if (!p.Item.IsEmpty)
            p.Item.Render(ui, row, index);
        else
            ui.TreeItem(text: p.ItemText is { } text ? text(row) : default, file: file, line: line);
    }
}

[FramePath]
internal sealed class TreeItemEntry : IResettable, IDisposable
{
    internal readonly Handler<KeyEvent> KeyDown;
    internal readonly Callback<FocusChange> FocusChange;
    internal readonly Callback DoubleClick;
    internal readonly Callback Toggle;
    internal readonly Handler<PointerEvent> ArrowPress;
    internal TreeModel? Model;
    internal TreeRow? Row;
    internal UiKey Key;
    internal Host<TreeItemProps> Host;
    internal NodeRef Ref = null!;

    TreeItemEntry()
    {
        KeyDown = Handler<KeyEvent>.Of(this, static (t, ref e) => t.Model?.Pressed(t, ref e));
        FocusChange = Callback<FocusChange>.Of(
            this,
            static (t, change) =>
            {
                if (change.Focused && change.Cause == FocusCause.Tab)
                    t.Model?.Select(t.Key);
            }
        );
        DoubleClick = Callback.Of(this, static t => t.Flip());
        Toggle = Callback.Of(this, static t => t.Flip());
        ArrowPress = Handler<PointerEvent>.Of(this, static (t, ref e) => t.ArrowPressed(ref e));
    }

    internal static TreeItemEntry Rent() => Pool<TreeItemEntry>.TryRent() ?? new TreeItemEntry();

    internal void Bind(
        TreeModel? model,
        TreeRow? row,
        UiKey key,
        Host<TreeItemProps> host,
        NodeRef node
    )
    {
        Host = host;
        Ref = node;
        Row = row;
        if (ReferenceEquals(model, Model) && key == Key)
            return;
        Model?.Forget(this);
        Model = model;
        Key = key;
        model?.Show(this);
    }

    internal void Shown() => Model?.Shown(this);

    void Flip()
    {
        if (Model is not null)
        {
            Model.Toggle(this);
            return;
        }
        ref readonly var p = ref Host.Props;
        if (p.HasChildren == true)
            p.OnExpandedChange.Invoke(!(p.Expanded ?? false));
    }

    void ArrowPressed(ref PointerEvent e)
    {
        if (e.Pointer.Kind == PointerKind.Mouse && e.Button != MouseButton.Left)
            return;
        e.StopPropagation();
        Ref.Focus();
    }

    public void Reset()
    {
        Model?.Forget(this);
        Model = null;
        Row = null;
        Key = default;
    }

    public void Dispose()
    {
        Reset();
        Host = default;
        Ref = null!;
        Pool<TreeItemEntry>.Return(this);
    }
}

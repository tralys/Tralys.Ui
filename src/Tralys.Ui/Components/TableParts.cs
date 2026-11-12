using System.Numerics;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

internal readonly record struct TableHeaderCellProps
{
    public int Index { get; init; }

    public TextValue Header { get; init; }

    public Slot HeaderContent { get; init; }

    public TextValue Info { get; init; }

    public UiKey SortKey { get; init; }

    public bool Sorts { get; init; }

    public SortDirection Sorted { get; init; }

    public Length Width { get; init; }

    public Length MinWidth { get; init; }

    /// <summary>Whether the column fits its widest header or cell, which the header then measures.</summary>
    public bool Measures { get; init; }
}

/// <summary>One column's header: its text, its sort direction while the table sorts by it, its info icon, and the
/// gripper that resizes it. Lit under the pointer while a click on it sorts.</summary>
internal sealed class TableHeaderCell : Component<TableHeaderCellProps>
{
    static readonly Thickness Padding = new(6f, 8f, 6f, 7f);
    const float SortIconSize = 10f;

    protected override void Render(Ui ui, in TableHeaderCellProps p)
    {
        var layout = ui.Use(Table.Context).Layout;
        var t = ui.Theme;
        var cell = ui.Ref();
        layout?.Register(p.Index, cell);
        var content = layout is not null && p.Measures ? ui.Ref() : null;
        var lit =
            !p.Sorts || ui.IsDisabled ? default
            : ui.IsPressed ? t.Color(ColorToken.StatePressed)
            : ui.IsHovered ? t.Color(ColorToken.StateHover)
            : default;
        using (
            ui.Box(
                width: p.Width,
                minWidth: p.MinWidth,
                background: ui.Animate(lit),
                cursor: p.Sorts ? Cursors.Hand : null,
                onClick: p.Sorts
                    ? Callback.Of(
                        ui.Self(p),
                        static h => h.Use(Table.Context).Layout?.OnSort.Invoke(h.Props.SortKey)
                    )
                    : default,
                focusable: false,
                nodeRef: cell
            )
        )
        {
            using (ui.Box(grow: 1f, overflow: Overflow.Clip))
            using (
                ui.Box(align: Align.Center, gap: 4f, padding: Padding, shrink: 0f, nodeRef: content)
            )
            {
                if (!p.Header.IsNull)
                    ui.Text(p.Header);
                else
                    p.HeaderContent.Render(ui);
                ui.Icon(
                    glyph: IconGlyph.Of(
                        p.Sorted == SortDirection.Ascending ? IconRole.ArrowUp : IconRole.ArrowDown
                    ),
                    size: SortIconSize,
                    color: t.Color(ColorToken.FgAccent),
                    shrink: 0f,
                    collapsed: p.Sorted == SortDirection.None
                );
                if (!p.Info.Span.IsEmpty)
                    using (ui.Tooltip(text: p.Info, shrink: 0f))
                        ui.Icon(variant: IconVariant.Info);
            }
            if (layout is { Resizable: true })
                ui.TableGripper(
                    index: p.Index,
                    position: Position.Absolute,
                    top: 0f,
                    right: -TableGripper.Width / 2f,
                    bottom: 0f
                );
        }
        if (content is not null)
            ui.Effect(
                layout!,
                new Grown(p.Index, content.Size.Width),
                static (l, g, _) => l.Grow(g.Column, g.Width)
            );
    }
}

internal readonly record struct TableGripperProps
{
    public int Index { get; init; }
}

/// <summary>The grip at a header's right edge, reaching half its width over the next header: dragged sideways it sets
/// its column's width, and its line shows while it's hovered or dragged.</summary>
internal sealed class TableGripper : Component<TableGripperProps>
{
    internal const float Width = 8f;
    const float LineWidth = 2f;

    // Pressable, so a click on the grip is its own and never sorts the header under it.
    static readonly Callback Swallow = Callback.Of(static () => { });

    protected override void Render(Ui ui, in TableGripperProps p)
    {
        var t = ui.Theme;
        using (
            ui.Box(
                justify: Justify.Center,
                width: Width,
                cursor: ui.IsDisabled ? null : Cursors.SizeWe,
                onClick: Swallow,
                focusable: false,
                onDrag: Handler<DragEvent>.Of(
                    ui.Self(p),
                    static (h, ref e) => h.Use(Table.Context).Layout?.Resize(h.Props.Index, ref e)
                )
            )
        )
        using (
            ui.Box(
                width: LineWidth,
                background: t.Color(ColorToken.BorderHover),
                hidden: !(ui.IsHovered || ui.IsCapturing)
            )
        ) { }
    }
}

internal readonly record struct CellSize(
    Length Width,
    Length MinWidth,
    Length Basis,
    float Grow,
    bool Measures
);

/// <summary>A table's column layout, shared by its header row and its rows: the order the columns show in, the widths
/// their cells were measured at and the widths their grippers dragged, and the drags on the header row.</summary>
[FramePath]
internal sealed class TableLayout
{
    const float Notch = 48f;

    internal readonly Handler<DragEvent> HeaderDrag;
    internal readonly Handler<WheelEvent> HeaderWheel;
    internal State<int> Version = null!;
    internal State<DropLine>? Drop;
    internal ScrollHandle Own = null!;
    internal ScrollHandle Sideways = null!;
    internal ScrollHandle Rows = null!;
    internal NodeRef HeaderRow = null!;
    internal Callback<UiKey> OnSort;
    internal bool Sorts;
    internal bool Reorderable;
    internal bool Resizable;
    internal bool ScrollsSideways;
    object? _columns;
    float?[] _widths = [];
    bool[] _fills = [];
    float[] _measured = [];
    float[] _dragged = [];
    int[] _order = [];
    NodeRef?[] _headers = [];
    HeaderDragMode _mode;
    int _moving = -1;
    int _drop = -1;
    int _resizing = -1;
    float _resizeFrom;
    float _resizeBefore;

    enum HeaderDragMode : byte
    {
        None,
        Move,
        Pan,
    }

    public TableLayout()
    {
        HeaderDrag = Handler<DragEvent>.Of(this, static (l, ref e) => l.DragHeader(ref e));
        HeaderWheel = Handler<WheelEvent>.Of(this, static (l, ref e) => l.Wheel(ref e));
    }

    /// <summary>The column shown at <paramref name="shown"/>.</summary>
    internal int ColumnAt(int shown) => (uint)shown < (uint)_order.Length ? _order[shown] : shown;

    internal static CellSize SizeOf(TableLayout? layout, int column, float? width, bool fill)
    {
        if (layout is not null && (uint)column < (uint)layout._dragged.Length)
        {
            var dragged = layout._dragged[column];
            if (!float.IsNaN(dragged))
                return new CellSize(dragged, default, default, 0f, false);
        }
        if (width is { } own)
            return fill
                ? new CellSize(default, default, own, 1f, false)
                : new CellSize(own, default, default, 0f, false);
        var measured =
            layout is not null && (uint)column < (uint)layout._measured.Length
                ? layout._measured[column]
                : 0f;
        return fill
            ? new CellSize(default, default, measured, 1f, true)
            : new CellSize(default, measured, default, 0f, true);
    }

    internal void Sync<TItem>(TableColumn<TItem>[] columns)
    {
        if (ReferenceEquals(columns, _columns))
            return;
        _columns = columns;
        var count = columns.Length;
        if (count != _order.Length)
        {
            EndHeaderDrag();
            _resizing = -1;
            _order = new int[count];
            _widths = new float?[count];
            _fills = new bool[count];
            _measured = new float[count];
            _dragged = new float[count];
            _headers = new NodeRef?[count];
            for (var i = 0; i < count; i++)
            {
                _order[i] = i;
                _widths[i] = columns[i].Width;
                _fills[i] = columns[i].Fill;
                _dragged[i] = float.NaN;
            }
            return;
        }
        for (var i = 0; i < count; i++)
        {
            var column = columns[i];
            if (column.Width == _widths[i] && column.Fill == _fills[i])
                continue;
            _widths[i] = column.Width;
            _fills[i] = column.Fill;
            _dragged[i] = float.NaN;
            _measured[i] = 0f;
        }
    }

    internal void Register(int column, NodeRef header)
    {
        if ((uint)column < (uint)_headers.Length)
            _headers[column] = header;
    }

    internal void Grow(int column, float width)
    {
        if ((uint)column >= (uint)_measured.Length || !(width > _measured[column]))
            return;
        _measured[column] = width;
        Bump();
    }

    void Bump() => Version.Value = Version.Peek + 1;

    internal void Resize(int column, ref DragEvent e)
    {
        switch (e.Phase)
        {
            case DragPhase.Start:
                if (
                    !Resizable
                    || (uint)column >= (uint)_headers.Length
                    || _headers[column] is not { } header
                )
                {
                    e.PreventDefault();
                    return;
                }
                _resizing = column;
                _resizeFrom = header.Size.Width;
                _resizeBefore = _dragged[column];
                break;
            case DragPhase.Move when _resizing == column:
                var width = MathF.Max(0f, _resizeFrom + e.Offset.X);
                if (width == _dragged[column])
                    return;
                _dragged[column] = width;
                Bump();
                break;
            case DragPhase.End:
                _resizing = -1;
                break;
            case DragPhase.Cancel when _resizing == column:
                _resizing = -1;
                _dragged[column] = _resizeBefore;
                Bump();
                break;
        }
    }

    void DragHeader(ref DragEvent e)
    {
        switch (e.Phase)
        {
            case DragPhase.Start:
                StartHeaderDrag(ref e);
                break;
            case DragPhase.Move when _mode == HeaderDragMode.Pan:
                Rows.ScrollBy(new Vector2(0f, -e.Delta.Y));
                break;
            case DragPhase.Move when _mode == HeaderDragMode.Move:
                Aim(e.Position.X);
                break;
            case DragPhase.End:
                if (_mode == HeaderDragMode.Move)
                    Place();
                EndHeaderDrag();
                break;
            case DragPhase.Cancel:
                EndHeaderDrag();
                break;
        }
    }

    void StartHeaderDrag(ref DragEvent e)
    {
        var offset = e.Offset;
        var vertical = MathF.Abs(offset.Y) > MathF.Abs(offset.X);
        if (e.Pointer.Kind != PointerKind.Mouse)
        {
            if (vertical)
            {
                _mode = HeaderDragMode.Pan;
                return;
            }
            if (CanMove(Sideways, ScrollsSideways, -offset.X, horizontal: true))
            {
                e.PreventDefault();
                return;
            }
        }
        var shown = vertical || !Reorderable ? -1 : ShownAt(e.Position.X - offset.X);
        if (shown < 0)
        {
            e.PreventDefault();
            return;
        }
        _mode = HeaderDragMode.Move;
        _moving = shown;
        Aim(e.Position.X);
    }

    void EndHeaderDrag()
    {
        _mode = HeaderDragMode.None;
        _moving = -1;
        _drop = -1;
        if (Drop is { } drop)
            drop.Value = default;
    }

    int ShownAt(float x)
    {
        for (var shown = 0; shown < _order.Length; shown++)
        {
            var bounds = HeaderBounds(shown);
            if (x >= bounds.X && x < bounds.Right)
                return shown;
        }
        return -1;
    }

    Rect HeaderBounds(int shown)
    {
        if (_headers[_order[shown]] is not { } header)
            return default;
        var bounds = header.Bounds;
        return new Rect(bounds.X - HeaderRow.Bounds.X, 0f, bounds.Width, bounds.Height);
    }

    void Aim(float x)
    {
        var count = _order.Length;
        var drop = count;
        var edge = count > 0 ? HeaderBounds(count - 1).Right : 0f;
        for (var shown = 0; shown < count; shown++)
        {
            var bounds = HeaderBounds(shown);
            if (x < bounds.X + bounds.Width / 2f)
            {
                drop = shown;
                edge = bounds.X;
                break;
            }
        }
        _drop = drop;
        if (Drop is { } line)
            line.Value = new DropLine(true, edge);
    }

    void Place()
    {
        var from = _moving;
        var to = _drop > from ? _drop - 1 : _drop;
        if (from < 0 || to < 0 || to == from)
            return;
        var column = _order[from];
        if (to > from)
            Array.Copy(_order, from + 1, _order, from, to - from);
        else
            Array.Copy(_order, to, _order, to + 1, from - to);
        _order[to] = column;
        Bump();
    }

    void Wheel(ref WheelEvent e)
    {
        var dy = -e.Notches.Y * Notch;
        var dx = e.Notches.X * Notch;
        var down = CanMove(Rows, true, dy, horizontal: false);
        var across = CanMove(Sideways, ScrollsSideways, dx, horizontal: true);
        if (!down && !across)
            return;
        if (down)
            Rows.ScrollBy(new Vector2(0f, dy), animate: true);
        if (across)
            Sideways.ScrollBy(new Vector2(dx, 0f), animate: true);
        e.StopPropagation();
        e.PreventDefault();
    }

    static bool CanMove(ScrollHandle handle, bool scrolls, float delta, bool horizontal)
    {
        if (!scrolls || delta == 0f)
            return false;
        var offset = horizontal ? handle.Offset.X : handle.Offset.Y;
        var max = horizontal
            ? handle.Extent.Width - handle.Viewport.Width
            : handle.Extent.Height - handle.Viewport.Height;
        return delta > 0f ? offset < max : offset > 0f;
    }
}

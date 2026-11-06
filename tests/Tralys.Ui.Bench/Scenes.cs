using Tralys.Ui;
using Tralys.Ui.Rendering;

namespace Bench;

/// <summary>A view in a context of its own, drawn into a list no renderer submits.</summary>
abstract class ViewScene : Scene
{
    static readonly BenchFonts Fonts = new();

    readonly UiContext _context = new(Fonts.Services);
    readonly GradientRampAtlas _ramps = new();
    readonly DrawList _list;
    readonly Size _size;
    UiView? _view;
    TimeSpan _now;

    protected ViewScene(int width, int height)
    {
        _size = new Size(width, height);
        _list = new DrawList(_ramps);
        _context.Advance(_now);
    }

    protected UiView View => _view!;

    public override void Prepare() =>
        _view = new UiView(_context, new BenchHost()) { Size = _size };

    public override void Mount()
    {
        MountRoot();
        Frame();
    }

    public override void Unmount()
    {
        View.Dispose();
        _view = null;
    }

    public override int Count => CountNodes(View.Root) - 1;

    protected abstract void MountRoot();

    public override void Frame()
    {
        _now += FrameTime;
        _context.Advance(_now);
        View.Update();
        _ramps.BeginFrame();
        _list.ClearPatchedRanges();
        View.Render(_list);
    }

    static int CountNodes(Node node)
    {
        var count = node.Kind is NodeKind.Component or NodeKind.Fragment ? 0 : 1;
        foreach (var child in node.Children)
            count += CountNodes(child);
        return count;
    }
}

sealed class DenseScene(Cell[] cells) : ViewScene(1280, 2100)
{
    readonly Ticker _ticker = new();

    public override string Name => "dense";

    protected override void MountRoot() =>
        View.Mount<DenseView, DenseProps>(new DenseProps { Cells = cells, Ticker = _ticker });

    public override void Rerender() => _ticker.Tick++;
}

sealed class RowsScene(Line[] lines) : ViewScene(800, 11000)
{
    public override string Name => "rows";

    protected override void MountRoot() =>
        View.Mount<RowsView, RowsProps>(new RowsProps { Lines = lines });
}

sealed class ListScene(Item[] items) : ViewScene(800, 600)
{
    readonly ScrollState _scroll = new();

    public override string Name => "list";

    protected override void MountRoot() =>
        View.Mount<ListView, ListProps>(
            new ListProps { Items = ItemsSource.From(items), Scroll = _scroll }
        );

    public override void ScrollTo(float offset) => _scroll.Handle!.ScrollTo(null, offset);

    public override float ScrollOffset => _scroll.Handle?.Offset.Y ?? 0f;

    public override float ScrollMax =>
        _scroll.Handle is { } handle
            ? MathF.Max(0f, handle.Extent.Height - handle.Viewport.Height)
            : 0f;
}

static class DenseShape
{
    public const int Rows = 40;
    public const int Columns = 10;
}

static class Paints
{
    public static readonly Color Card = Color.FromRgb(40, 44, 52);
    public static readonly Color Muted = Color.FromRgb(60, 66, 78);
    public static readonly Color Accent = Color.FromRgb(90, 140, 230);
    public static readonly Color Odd = Color.FromRgb(48, 40, 52);
}

readonly record struct DenseProps
{
    public Cell[]? Cells { get; init; }

    public Ticker? Ticker { get; init; }
}

/// <summary>Read by a root that shows nothing of it, so a tick renders the root again with unchanged output.</summary>
sealed partial class Ticker : Tralys.Ui.Mvvm.ObservableObject
{
    [Tralys.Ui.Mvvm.ObservableProperty]
    public partial int Tick { get; set; }
}

/// <summary>40 rows of 10 cards, each a label, a count and a bar: 2,000 nodes in one render.</summary>
sealed class DenseView : Component<DenseProps>
{
    protected override void Render(Ui ui, in DenseProps p)
    {
        var cells = p.Cells!;
        _ = p.Ticker!.Tick;
        using (ui.Box(direction: Direction.Column, gap: 4, align: Align.Start))
        {
            for (var r = 0; r < DenseShape.Rows; r++)
            {
                using (ui.Box(gap: 4, key: r))
                {
                    for (var c = 0; c < DenseShape.Columns; c++)
                    {
                        var cell = cells[r * DenseShape.Columns + c];
                        using (
                            ui.Box(
                                direction: Direction.Column,
                                gap: 2,
                                width: 120,
                                padding: 4,
                                radius: 4,
                                background: Paints.Card,
                                key: c
                            )
                        )
                        {
                            ui.Text(cell.Label, fontSize: 12);
                            ui.Text(ui.Format($"{cell.Count}"), fontSize: 12);
                            using (ui.Box(height: 4, background: Paints.Muted))
                            using (ui.Box(width: cell.Fill, background: Paints.Accent)) { }
                        }
                    }
                }
            }
        }
    }
}

readonly record struct RowsProps
{
    public Line[]? Lines { get; init; }
}

sealed class RowsView : Component<RowsProps>
{
    protected override void Render(Ui ui, in RowsProps p)
    {
        using (ui.Box(direction: Direction.Column, align: Align.Start))
        {
            var lines = p.Lines!;
            for (var i = 0; i < lines.Length; i++)
                ui.LineRow(item: lines[i], key: i);
        }
    }
}

readonly record struct LineRowProps
{
    public Line? Item { get; init; }
}

/// <summary>A row of its own line: it alone renders again when the line changes.</summary>
sealed class LineRow : Component<LineRowProps>
{
    protected override void Render(Ui ui, in LineRowProps p)
    {
        var line = p.Item!;
        using (
            ui.Box(
                gap: 8,
                padding: new Thickness(8, 2, 8, 2),
                background: line.Shade == Shade.Odd ? Paints.Odd : Paints.Card,
                opacity: line.Opacity
            )
        )
        {
            ui.Text(line.Label, fontSize: 12);
            ui.Text(ui.Format($"{line.Count}"), fontSize: 12);
        }
    }
}

sealed class ScrollState
{
    public ScrollHandle? Handle;
}

readonly record struct ListProps
{
    public IItemsSource<Item>? Items { get; init; }

    public ScrollState? Scroll { get; init; }
}

sealed class ListView : Component<ListProps>
{
    static readonly Slot<Item> Row = Slot<Item>.Of(static (ui, item, _) => ui.ItemRow(item: item));

    protected override void Render(Ui ui, in ListProps p)
    {
        var scroll = ui.ScrollHandle();
        p.Scroll!.Handle = scroll;
        ui.VirtualList(items: p.Items!, itemKey: Item.ById, row: Row, scroll: scroll);
    }
}

readonly record struct ItemRowProps
{
    public Item? Item { get; init; }
}

sealed class ItemRow : Component<ItemRowProps>
{
    protected override void Render(Ui ui, in ItemRowProps p)
    {
        using (ui.Box(padding: new Thickness(12, 4, 12, 4)))
            ui.Text(p.Item!.Label, fontSize: 12);
    }
}

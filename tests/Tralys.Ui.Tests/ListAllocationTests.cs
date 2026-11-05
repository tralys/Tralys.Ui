using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;
using TUnit.Assertions.Enums;

namespace Tralys.Ui.Tests;

public sealed class ListAllocationTests
{
    [Test]
    public async Task Scrolling_a_ten_thousand_row_list_allocates_nothing()
    {
        var lines = Line.Many(10_000);
        using var driver = UiDriver.Render(
            ui =>
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: LineSlots.Rows,
                    height: 400,
                    id: "list"
                ),
            new Size(300, 400)
        );
        driver.View.Context.SmoothScrolling = false;
        var list = driver.Get("list");

        var bytes = driver.WorstFrameBytes(
            list,
            static (d, l) =>
            {
                d.Wheel(l, new Vector2(0, -1), settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Recycling_rows_whose_buttons_bind_one_shared_command_allocates_nothing()
    {
        var lines = Line.Many(10_000);
        var command = new Command(static () => { });
        using var driver = UiDriver.Render(
            ui =>
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: Slot<Line>.Of(
                        command,
                        static (ui, command, line, _) =>
                            ui.Button(text: line.Name, onClick: command)
                    ),
                    rowHeight: 32,
                    fixedRowHeight: true,
                    height: 400,
                    id: "list"
                ),
            new Size(300, 400)
        );
        driver.View.Context.SmoothScrolling = false;
        var list = driver.Get("list");

        var bytes = driver.WorstFrameBytes(
            list,
            static (d, l) =>
            {
                d.Wheel(l, new Vector2(0, -1), settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Arrows_moving_a_lists_selection_and_focus_allocate_nothing()
    {
        var lines = Line.Many(10_000);
        var state = new ListState { Items = ItemsSource.From(lines) };
        using var driver = UiDriver.Render(
            ui =>
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: LineSlots.Rows,
                    selectionMode: SelectionMode.Single,
                    selection: state.Selection,
                    onSelectionChange: Callback<SelectionChange>.Of(
                        state,
                        static (s, change) => s.Apply(change)
                    ),
                    height: 400
                ),
            new Size(300, 400)
        );
        driver.Focus(driver.Get("line 0"));

        var bytes = driver.WorstFrameBytes(static d =>
        {
            d.Press(Key.Down, settle: false);
            d.Frame();
        });

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task End_and_home_in_turn_among_unseen_rows_taller_than_those_shown_focus_the_end_row_in_the_same_frame_and_allocate_nothing()
    {
        var lines = Line.Many(1000);
        for (var i = 20; i < lines.Count; i++)
            lines[i].Height = 60;
        var state = new ListState { Items = ItemsSource.From(lines) };
        using var driver = UiDriver.Render(
            ui =>
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: LineSlots.Rows,
                    selectionMode: SelectionMode.Single,
                    selection: state.Selection,
                    onSelectionChange: Callback<SelectionChange>.Of(
                        state,
                        static (s, change) => s.Apply(change)
                    ),
                    height: 200
                ),
            new Size(300, 200)
        );
        driver.Focus(driver.Get("line 0"));
        var ends = new Ends();

        var bytes = driver.WorstFrameBytes(
            ends,
            static (d, e) =>
            {
                e.ToEnd = !e.ToEnd;
                d.Press(e.ToEnd ? Key.End : Key.Home, settle: false);
                d.Frame();
                if (d.Focused?.Id != (e.ToEnd ? "line 999" : "line 0"))
                    e.Missed++;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(ends.Missed).IsEqualTo(0);
    }

    sealed class Ends
    {
        public bool ToEnd;
        public int Missed;
    }

    [Test]
    public async Task Mounting_a_list_again_allocates_nothing()
    {
        var lines = Line.Many(20);
        var model = new Model();
        using var driver = UiDriver.Render(
            ui =>
            {
                if ((model.Count & 1) == 0)
                    ui.VirtualList(
                        items: lines,
                        itemKey: Line.ById,
                        row: LineSlots.Rows,
                        height: 200
                    );
                else
                    ui.Text("No results");
            },
            new Size(300, 200)
        );

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Wheeling_and_panning_a_scroll_box_allocate_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(
            ui =>
            {
                using (
                    ui.Box(
                        direction: Direction.Column,
                        overflow: Overflow.ScrollY,
                        height: 200,
                        id: "pane"
                    )
                )
                {
                    for (var i = 0; i < 50; i++)
                    {
                        using (ui.Box(height: 40, shrink: 0, key: i))
                            ui.Label(model: model);
                    }
                }
            },
            new Size(300, 200)
        );
        var pane = driver.Get("pane");

        var bytes = driver.WorstFrameBytes(
            (pane, new Counter()),
            static (d, s) =>
            {
                var down = (s.Item2.Value++ & 1) == 0;
                d.Wheel(s.pane, new Vector2(0, down ? -1 : 1), settle: false);
                d.Drag(s.pane, new Vector2(0, down ? -20 : 20), PointerKind.Touch, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Appending_dropping_the_first_and_replacing_the_fourth_item_every_frame_allocates_nothing_and_each_row_shows_the_item_at_its_index()
    {
        var lines = Line.Many(20);
        var pool = new LinePool(64, 100);
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(direction: Direction.Row))
                {
                    ui.VirtualList(
                        items: lines,
                        itemKey: Line.ById,
                        row: LineSlots.Rows,
                        width: 200,
                        height: 300,
                        id: "virtual"
                    );
                    ui.VirtualList(
                        items: lines,
                        itemKey: Line.ById,
                        row: LineSlots.Rows,
                        realizeAll: true,
                        width: 200,
                        height: 300,
                        id: "all"
                    );
                }
            },
            new Size(400, 300)
        );

        var bytes = driver.WorstFrameBytes(
            (lines, pool),
            static (d, s) =>
            {
                s.lines.Add(s.pool.Next());
                s.lines.RemoveAt(0);
                s.lines[3] = s.pool.Next();
                d.Frame();
            }
        );

        var names = new List<string?>();
        foreach (var line in lines)
            names.Add(line.Name);
        await Assert.That(bytes).IsEqualTo(0L);
        await Assert
            .That(Shown(driver.Get("virtual")))
            .IsEquivalentTo(names, CollectionOrdering.Matching);
        await Assert
            .That(Shown(driver.Get("all")))
            .IsEquivalentTo(names, CollectionOrdering.Matching);
    }

    [Test]
    public async Task A_focused_rows_item_replaced_then_removed_every_frame_hands_the_focus_to_the_new_row_then_the_list_and_allocates_nothing()
    {
        var lines = Line.Many(20);
        using var driver = Render(lines, new ListState(), new Size(200, 300));
        var fourth = driver.Get("line 3").ViewBounds;
        var hops = new Handoffs(
            lines,
            new LinePool(64, 100),
            driver.Get("list"),
            new Vector2(fourth.X + fourth.Width / 2f, fourth.Y + fourth.Height / 2f)
        );

        var bytes = driver.WorstFrameBytes(
            hops,
            static (d, h) =>
            {
                d.Click(h.Fourth, settle: false);
                d.Frame();
                h.Lines[3] = h.Pool.Next();
                d.Frame();
                if (d.Focused?.Id != h.Lines[3].Name)
                    h.Missed++;
                h.Lines.RemoveAt(3);
                h.Lines.Add(h.Pool.Next());
                d.Frame();
                if (d.Focused != h.List)
                    h.Missed++;
                h.Steps++;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That((hops.Steps > 0, hops.Missed)).IsEqualTo((true, 0));
    }

    [Test]
    public async Task A_focused_row_scrolled_away_whose_item_is_replaced_every_frame_hands_the_focus_on_and_allocates_nothing()
    {
        var lines = Line.Many(1000);
        var state = new ListState();
        using var driver = Render(lines, state, new Size(200, 300));
        driver.Focus(driver.Get("line 1"));
        state.Scroll!.ScrollTo(null, 4000);
        driver.Settle();
        var kept = new Replacements(lines, new LinePool(64, 5000));

        var bytes = driver.WorstFrameBytes(
            kept,
            static (d, k) =>
            {
                k.Lines[1] = k.Pool.Next();
                d.Frame();
                if (d.Focused?.Id != k.Lines[1].Name || d.Focused.ViewBounds.Bottom > 0f)
                    k.Missed++;
                k.Steps++;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That((kept.Steps > 0, kept.Missed)).IsEqualTo((true, 0));
    }

    [Test]
    public async Task A_row_above_the_view_growing_and_shrinking_every_frame_holds_the_rows_in_view_still_and_allocates_nothing()
    {
        var lines = Line.Many(1000);
        var state = new ListState();
        using var driver = Render(lines, state, new Size(300, 200));
        state.Scroll!.ScrollTo(null, 2000);
        driver.Settle();
        var above = driver.Get("line 98").ViewBounds.Bottom;
        var toggle = new Toggle(lines[98], driver.Get("line 100"));

        var bytes = driver.WorstFrameBytes(
            toggle,
            static (d, t) =>
            {
                t.Step();
                d.Frame();
                if (t.Row.Id != t.Id || t.Row.ViewBounds.Y != t.Y)
                    t.Moved++;
            }
        );
        var sampled = new List<(bool, float)>();
        for (var i = 0; i < 3; i++)
        {
            toggle.Step();
            driver.Frame();
            var row = driver.Get("line 100");
            sampled.Add((ReferenceEquals(row, toggle.Row), row.ViewBounds.Y));
        }

        await Assert.That(above).IsLessThanOrEqualTo(0f);
        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(toggle.Moved).IsEqualTo(0);
        await Assert
            .That(sampled)
            .IsEquivalentTo([(true, 0f), (true, 0f), (true, 0f)], CollectionOrdering.Matching);
    }

    [Test]
    public async Task A_ring_kept_at_its_end_appending_a_line_and_dropping_its_oldest_every_frame_stays_at_the_bottom_and_allocates_nothing()
    {
        var pool = new LinePool(128, 0, 16, 30, 44);
        var lines = new ObservableList<Line>(65);
        for (var i = 0; i < 64; i++)
            lines.Add(pool.Next());
        var state = new ListState();
        using var driver = Render(lines, state, new Size(300, 244));
        state.Scroll!.ScrollToEnd();
        driver.Settle();
        var ring = new Ring(lines, pool, driver.Get("list"), state.Scroll);

        var bytes = driver.WorstFrameBytes(
            ring,
            static (d, r) =>
            {
                r.Lines.RemoveAt(0);
                r.Lines.Add(r.Pool.Next());
                d.Frame();
                r.Check();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That((ring.Checks > 0, ring.Off)).IsEqualTo((true, 0));
        await Assert.That(ring.Census.Built).IsLessThan(32);
    }

    [Test]
    public async Task Wheeling_a_notch_every_frame_end_to_end_through_lines_and_dividers_recycles_rows_and_allocates_nothing()
    {
        var items = new ObservableList<object>(1000);
        for (var i = 0; i < 1000; i++)
            items.Add(
                i % 5 == 4 ? new Model { Count = 20 } : new Line { Id = i, Name = "line " + i }
            );
        var scroll = new ScrollHandle();
        using var driver = UiDriver.Render(
            ui =>
                ui.VirtualList(
                    items: items,
                    itemKey: ByReference,
                    row: LinesAndDividers,
                    scroll: scroll,
                    height: 300,
                    id: "list"
                ),
            new Size(200, 300)
        );
        driver.View.Context.SmoothScrolling = false;
        var sweep = new Sweep(driver.Get("list"), scroll);
        for (var i = 0; i < 2000 && sweep.Turns < 2; i++)
            Sweep.Step(driver, sweep);

        var bytes = driver.WorstFrameBytes(sweep, static (d, s) => Sweep.Step(d, s));

        await Assert.That(sweep.Turns).IsGreaterThanOrEqualTo(2);
        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(sweep.Census.MostRealized).IsLessThanOrEqualTo(32);
        await Assert.That(sweep.Census.Built).IsLessThanOrEqualTo(sweep.Census.MostRealized + 4);
    }

    static readonly KeyOf<object> ByReference = static item => UiKey.Of(item);

    static readonly Slot<object> LinesAndDividers = Slot<object>.Of(
        static (ui, item, _) =>
        {
            if (item is Line line)
                ui.LineRow(item: line, id: line.Name);
            else
                ui.CountBox(model: (Model)item);
        }
    );

    static UiDriver Render(ObservableList<Line> lines, ListState state, Size size) =>
        UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                state.Scroll = scroll;
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: LineSlots.Rows,
                    scroll: scroll,
                    height: size.Height,
                    id: "list"
                );
            },
            size
        );

    internal static List<string?> Shown(Node list)
    {
        var ids = new List<string?>();
        foreach (var row in list.Children)
        {
            if (!row.SpareRow)
                ids.Add(row.Host?.Root?.Id);
        }
        return ids;
    }

    sealed class LinePool
    {
        readonly Line[] _lines;
        int _next;

        public LinePool(int count, int firstId, params float[] heights)
        {
            _lines = new Line[count];
            for (var i = 0; i < count; i++)
            {
                var id = firstId + i;
                _lines[i] = new Line
                {
                    Id = id,
                    Name = "line " + id,
                    Height = heights.Length == 0 ? 20 : heights[id % heights.Length],
                };
            }
        }

        public Line Next()
        {
            var line = _lines[_next];
            _next = (_next + 1) % _lines.Length;
            return line;
        }
    }

    sealed class Census
    {
        readonly HashSet<Node> _built = new(64);

        public int MostRealized;

        public int Built => _built.Count;

        public Node? Take(Node list)
        {
            var realized = 0;
            Node? last = null;
            foreach (var row in list.Children)
            {
                _built.Add(row);
                if (row.SpareRow)
                    continue;
                realized++;
                last = row;
            }
            MostRealized = Math.Max(MostRealized, realized);
            return last;
        }
    }

    sealed class Handoffs(ObservableList<Line> lines, LinePool pool, Node list, Vector2 fourth)
    {
        public readonly ObservableList<Line> Lines = lines;
        public readonly LinePool Pool = pool;
        public readonly Node List = list;
        public readonly Vector2 Fourth = fourth;
        public int Steps;
        public int Missed;
    }

    sealed class Replacements(ObservableList<Line> lines, LinePool pool)
    {
        public readonly ObservableList<Line> Lines = lines;
        public readonly LinePool Pool = pool;
        public int Steps;
        public int Missed;
    }

    sealed class Toggle(Line grows, Node row)
    {
        public readonly Node Row = row;
        public readonly string? Id = row.Id;
        public readonly float Y = row.ViewBounds.Y;
        public int Moved;

        public void Step() => grows.Height = grows.Height == 20 ? 40 : 20;
    }

    sealed class Ring(ObservableList<Line> lines, LinePool pool, Node list, ScrollHandle scroll)
    {
        public readonly ObservableList<Line> Lines = lines;
        public readonly LinePool Pool = pool;
        public readonly Census Census = new();
        public int Checks;
        public int Off;

        public void Check()
        {
            Checks++;
            var end = scroll.Extent.Height - scroll.Viewport.Height;
            var newest = Census.Take(list)?.Host?.Root;
            if (
                end <= 0f
                || MathF.Abs(scroll.Offset.Y - end) > 0.01f
                || newest?.Id != Lines[Lines.Count - 1].Name
                || MathF.Abs(newest.ViewBounds.Bottom - list.ViewBounds.Bottom) > 0.01f
            )
                Off++;
        }
    }

    sealed class Sweep(Node list, ScrollHandle scroll)
    {
        public readonly Node List = list;
        public readonly ScrollHandle Scroll = scroll;
        public readonly Census Census = new();
        public bool Up;
        public int Turns;

        public static void Step(UiDriver driver, Sweep sweep)
        {
            var from = sweep.Scroll.Offset;
            driver.Wheel(sweep.List, new Vector2(0, sweep.Up ? 1 : -1), settle: false);
            if (sweep.Scroll.Offset == from)
            {
                sweep.Up = !sweep.Up;
                sweep.Turns++;
            }
            driver.Frame();
            sweep.Census.Take(sweep.List);
        }
    }
}

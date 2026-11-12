using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;
using TUnit.Assertions.Enums;
using static Tralys.Ui.Tests.Rows;

namespace Tralys.Ui.Tests;

public sealed class ListBoxReorderTests
{
    static float HoldSeconds => (float)UiView.HoldTime.TotalSeconds + 0.1f;

    [Test]
    public async Task A_row_lands_past_a_rows_middle_counting_its_own_place_and_moves_nothing_on_its_own_place_where_refused_or_turned_off()
    {
        var moves = new Moves { Can = static m => m.To != 3 && m.Value != "c" };
        using var driver = Render(moves, Words.Of("a", "b", "c", "d"));

        DragRow(driver, "a", "c", 0.75f);
        DragRow(driver, "a", "b", 0.25f);
        DragRow(driver, "a", "d", 0.75f);
        var locked = driver.Hold(Of(driver, "c"));
        locked.MoveBy(new Vector2(0, 20));
        var pickedUp = driver.View.DragDropActive;
        locked.Release();
        moves.On = false;
        driver.Rerender();
        DragRow(driver, "a", "c", 0.75f);

        await Assert.That(moves.Log).IsEquivalentTo(["a 0>2"]);
        await Assert.That(pickedUp).IsFalse();
    }

    [Test]
    public async Task A_row_carried_while_a_row_comes_above_it_stays_carried_and_moves_from_where_it_is_now()
    {
        var moves = new Moves();
        var words = Words.List("a", "b", "c", "d");
        using var driver = Render(moves, ItemsSource.From(words));

        var mouse = driver.Hold(Of(driver, "b"));
        mouse.MoveBy(new Vector2(0, 10));
        words.Insert(0, "z");
        driver.Settle();
        var carried = driver.View.DragDropActive;
        MoveOnto(driver, mouse, "d", 0.75f);
        mouse.Release();

        await Assert.That(carried).IsTrue();
        await Assert.That(moves.Log).IsEquivalentTo(["b 2>4"]);
    }

    [Test]
    public async Task One_insert_line_shows_above_the_row_under_a_carried_row_or_below_past_its_middle_and_none_off_the_rows_or_for_other_data()
    {
        var moves = new Moves();
        var items = Words.Of("a", "b", "c", "d");
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 20))
                {
                    Reordering(ui, moves, items, height: 200);
                    using (ui.Box(width: 40, height: 40, dragData: "other", id: "other")) { }
                }
            },
            new Size(400, 400)
        );
        var frame = List(driver).Parent!.ViewBounds;
        var c = Of(driver, "c").ViewBounds;

        var mouse = driver.Hold(Of(driver, "a"));
        mouse.MoveBy(new Vector2(0, 10));
        MoveOnto(driver, mouse, "c", 0.25f);
        var above = Lines(driver);
        MoveOnto(driver, mouse, "c", 0.75f);
        var below = Lines(driver);
        var last = Of(driver, "d").ViewBounds;
        mouse.MoveBy(new Vector2(0, last.Bottom + 20 - mouse.Position.Y));
        var off = Lines(driver);
        mouse.Release();
        var other = driver.Hold(driver.Get("other"));
        other.MoveBy(new Vector2(0, 10));
        MoveOnto(driver, other, "c", 0.75f);
        var foreign = Lines(driver);
        other.Release();

        await Assert.That(above).IsEquivalentTo([Line(c.X - frame.X, c.Y - frame.Y, c.Width)]);
        await Assert.That(below).IsEquivalentTo([Line(c.X - frame.X, c.Bottom - frame.Y, c.Width)]);
        await Assert.That(off).IsEmpty();
        await Assert.That(foreign).IsEmpty();
        await Assert.That(moves.Log).IsEmpty();
    }

    [Test]
    public async Task A_press_leaving_the_list_short_of_the_threshold_is_held_until_it_crosses_and_one_released_short_leaves_no_drag()
    {
        using var driver = Render(new Moves(), Words.Of("a", "b", "c", "d"));
        var row = Of(driver, "a").ViewBounds;
        var list = List(driver).Parent!.ViewBounds;
        var inside = new Vector2(row.Right - 1, row.Y + row.Height / 2);
        var justOut = new Vector2(list.Right + 1, inside.Y);
        var farOut = justOut + new Vector2(20, 0);
        var view = driver.View;
        bool Run(Vector2 from, Vector2 to, Vector2? on)
        {
            view.PointerMove(Pointer.Mouse, from, MouseButtons.None, false, default, default);
            view.PointerDown(Pointer.Mouse, from, MouseButton.Left, default, default);
            view.PointerMove(Pointer.Mouse, to, MouseButtons.Left, true, default, default);
            driver.Settle();
            var dragging = view.DragDropActive;
            if (on is { } further)
            {
                view.PointerMove(Pointer.Mouse, further, MouseButtons.Left, true, default, default);
                driver.Settle();
                dragging = view.DragDropActive;
            }
            view.PointerUp(Pointer.Mouse, on ?? to, MouseButton.Left, default, default);
            driver.Settle();
            return dragging;
        }

        var leapt = Run(inside, farOut, null);
        var shortOfIt = Run(inside, justOut, null);
        var crept = Run(inside, justOut, farOut);
        var stale = Run(farOut, inside, null);

        await Assert.That((leapt, shortOfIt, crept, stale)).IsEqualTo((true, false, true, false));
    }

    [Test]
    public async Task A_finger_swiping_a_row_scrolls_the_list_and_carries_nothing()
    {
        var moves = new Moves();
        using var driver = Render(moves, Words.Of("a", "b", "c", "d"), height: 40);
        var scroll = moves.Scroll!;
        var overflow = scroll.Extent.Height - scroll.Viewport.Height;

        driver.Drag(Of(driver, "b"), new Vector2(0, -overflow), PointerKind.Touch);

        await Assert.That(overflow).IsGreaterThan(0f);
        await Assert.That(scroll.Offset.Y).IsEqualTo(overflow);
        await Assert.That(driver.View.DragDropActive).IsFalse();
    }

    [Test]
    public async Task A_held_finger_carries_a_row_a_second_finger_leaves_it_the_drop_and_the_drop_reports_the_move_then_the_item()
    {
        var moves = new Moves();
        using var driver = Render(moves, Words.Of("a", "b", "c", "d"), selects: true);

        var first = driver.Hold(Of(driver, "a"), PointerKind.Touch);
        driver.Frame(HoldSeconds);
        driver.Tap(Of(driver, "b"));
        first.MoveBy(new Vector2(0, 20));
        var carried = driver.View.DragDropActive;
        var late = driver.Hold(Of(driver, "c"), PointerKind.Touch);
        driver.Frame(HoldSeconds);
        late.MoveBy(new Vector2(0, 20));
        late.Release();
        MoveOnto(driver, first, "d", 0.75f);
        first.Release();

        await Assert.That(carried).IsTrue();
        await Assert
            .That(moves.Log)
            .IsEquivalentTo(["value b", "a 0>3", "value a"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Selecting_text_across_a_field_in_a_row_keeps_the_drag_and_picks_up_no_row()
    {
        var words = new ObservableText("some words here");
        using var driver = UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: Words.Of("a", "b"),
                    itemKey: Words.ByText,
                    item: Slot<string>.Of(
                        words,
                        static (ui, w, text, _) => ui.Input(text: w, width: 160)
                    ),
                    reorderable: true,
                    width: 200
                ),
            new Size(400, 300)
        );
        var field = Nodes.Find(driver.Root, NodeKind.EditableText);

        var mouse = driver.Hold(field);
        mouse.MoveBy(new Vector2(40, 0));
        var picked = driver.View.DragDropActive;
        mouse.Release();

        await Assert.That(picked).IsFalse();
        await Assert.That(driver.Focused).IsSameReferenceAs(field);
    }

    [Test]
    public async Task A_sideways_list_lays_its_items_in_a_row_and_drags_one_past_another_items_middle_along_it()
    {
        var moves = new Moves();
        using var driver = UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: Words.Of("a", "b", "c", "d"),
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    layout: ListLayout.Horizontal,
                    itemWidth: 60,
                    reorderable: true,
                    onReorder: moves.OnReorder,
                    width: 300,
                    height: 40
                ),
            new Size(400, 300)
        );
        var a = Of(driver, "a").ViewBounds;
        var b = Of(driver, "b").ViewBounds;

        var mouse = driver.Hold(Of(driver, "a"));
        mouse.MoveBy(new Vector2(10, 0));
        var c = Of(driver, "c").ViewBounds;
        mouse.MoveBy(new Vector2(c.X + c.Width * 0.75f, c.Y + 10) - mouse.Position);
        mouse.Release();

        await Assert.That((b.X, b.Y)).IsEqualTo((a.Right, a.Y));
        await Assert.That(moves.Log).IsEquivalentTo(["a 0>2"]);
    }

    internal sealed class Moves
    {
        readonly ValueLog<(ListReorder? Move, UiKey Value)> _log = new(Spelled);
        public bool On = true;
        public Func<ListReorder, bool>? Can;
        public ScrollHandle? Scroll;

        public List<string> Log => _log.Lines;

        public Callback<ListReorder> OnReorder =>
            Callback<ListReorder>.Of(this, static (m, move) => m._log.Add((move, default)));

        public Callback<UiKey> OnValueChange =>
            Callback<UiKey>.Of(this, static (m, key) => m._log.Add((null, key)));

        static string Spelled((ListReorder? Move, UiKey Value) seen) =>
            seen.Move is { } move
                ? string.Create(CultureInfo.InvariantCulture, $"{move.Value} {move.From}>{move.To}")
                : $"value {seen.Value}";
    }

    static UiDriver Render(
        Moves moves,
        IItemsSource<string> items,
        float height = 0,
        bool selects = false
    ) =>
        UiDriver.Render(
            ui =>
            {
                moves.Scroll = ui.ScrollHandle();
                Reordering(ui, moves, items, height, selects);
            },
            new Size(400, 300)
        );

    [FramePath]
    static void Reordering(
        Ui ui,
        Moves moves,
        IItemsSource<string> items,
        float height = 0,
        bool selects = false,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.ListBox(
            items: items,
            itemKey: Words.ByText,
            itemText: Words.Text,
            onValueChange: selects ? moves.OnValueChange : default,
            reorderable: moves.On,
            onReorder: moves.OnReorder,
            canReorder: moves.Can,
            scroll: moves.Scroll,
            width: 200,
            height: height > 0 ? height : Length.Auto,
            key: key,
            file: file,
            line: line
        );

    static void DragRow(UiDriver driver, string row, string onto, float down)
    {
        var mouse = driver.Hold(Of(driver, row));
        mouse.MoveBy(new Vector2(0, 10));
        MoveOnto(driver, mouse, onto, down);
        mouse.Release();
    }

    static void MoveOnto(UiDriver driver, HeldPointer pointer, string row, float down)
    {
        var to = Of(driver, row).ViewBounds;
        pointer.MoveBy(new Vector2(to.X + 10, to.Y + to.Height * down) - pointer.Position);
    }

    static string Line(float x, float y, float length) =>
        string.Create(CultureInfo.InvariantCulture, $"{x},{y} {x + length},{y}");

    static List<string> Lines(UiDriver driver)
    {
        var lines = new List<string>();
        foreach (var line in driver.Draw().Lines)
        {
            var at = line.IndexOf(": line ", StringComparison.Ordinal);
            if (at >= 0 && line.Contains("/Canvas", StringComparison.Ordinal))
                lines.Add(
                    line[(at + ": line ".Length)..line.IndexOf(" width", StringComparison.Ordinal)]
                );
        }
        return lines;
    }
}

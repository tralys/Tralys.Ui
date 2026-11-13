using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class TabDragTests
{
    static float HoldSeconds => (float)UiView.HoldTime.TotalSeconds + 0.1f;

    [Test]
    public async Task A_tab_dragged_past_its_neighbours_midpoint_reorders_on_that_move_and_not_again_until_laid_out()
    {
        var board = new TabBoard();
        board.Add("a", "One", "Two", "Three");
        using var driver = UiDriver.Render(ui => board.Render(ui));

        var press = driver.Hold(driver.Get("One"));
        press.MoveBy(new Vector2(78, 0));
        var shy = (board.Log.ToString(), board.Order);
        press.MoveBy(new Vector2(4, 0), settle: false);
        press.MoveBy(new Vector2(80, 0), settle: false);
        var stale = board.Log.ToString();
        driver.Settle();
        press.MoveBy(new Vector2(2, 0));
        press.Release();

        await Assert.That(shy).IsEqualTo(("", "One,Two,Three"));
        await Assert.That(stale).IsEqualTo("reorder One 0 1");
        await Assert
            .That(board.Log.ToString())
            .IsEqualTo("reorder One 0 1 reorder One 1 2 end One");
        await Assert.That(board.Order).IsEqualTo("Two,Three,One");
    }

    [Test]
    public async Task A_tab_released_on_the_move_that_carried_it_over_another_strip_joins_it_at_the_gap_past_a_hidden_tab()
    {
        var board = new TabBoard();
        board.Add("a", "One", "Two");
        board.Add("b", "Three", "Four").HiddenTab = "Three";
        using var driver = UiDriver.Render(ui => board.Render(ui, grouped: true));

        var press = driver.Hold(driver.Get("One"));
        press.MoveBy(new Vector2(20, 72), settle: false);
        press.Release();

        await Assert.That(board.Log.ToString()).IsEqualTo("move One b 1 end One");
        await Assert.That(board.Order).IsEqualTo("Two | Three,One,Four");
    }

    [Test]
    public async Task Only_the_band_of_a_strips_tab_row_takes_a_tab_and_one_dropped_over_no_strip_tears_off_where_it_lands()
    {
        var board = new TabBoard();
        board.Add("a", "One", "Two");
        board.Add("b", "Three");
        using var driver = UiDriver.Render(ui => board.Render(ui, grouped: true));

        var press = driver.Hold(driver.Get("One"));
        press.MoveBy(new Vector2(20, 108));
        var seams = TabBoard.Seams(driver);
        press.Release();

        await Assert.That(seams).IsEqualTo("");
        await Assert.That(board.Log.ToString()).IsEqualTo("tear One 60,124 end One");
        await Assert.That(board.Order).IsEqualTo("One,Two | Three");
    }

    [Test]
    public async Task A_strip_with_no_tabs_takes_a_tab_anywhere_over_it_showing_one_marker_at_its_start_as_tall_as_it()
    {
        var board = new TabBoard();
        board.Add("a", "One", "Two");
        board.Add("b").Height = 100;
        using var driver = UiDriver.Render(ui => board.Render(ui, grouped: true));
        var seams = new List<string>();

        var press = driver.Hold(driver.Get("One"));
        foreach (var by in (Vector2[])[new(0, 86), new(0, 20), new(160, 10)])
        {
            press.MoveBy(by);
            seams.Add(TabBoard.Seams(driver));
        }
        press.Release();

        await Assert
            .That(string.Join(" ; ", seams))
            .IsEqualTo("b 0,0 0,100 ; b 0,0 0,100 ; b 0,0 0,100");
        await Assert.That(board.Log.ToString()).IsEqualTo("move One b 0 end One");
        await Assert.That(board.Order).IsEqualTo("Two | One");
    }

    [Test]
    public async Task Only_the_strip_under_a_dragged_tab_marks_the_gap_before_or_after_the_tab_by_its_midpoint()
    {
        var board = new TabBoard();
        board.Add("a", "One", "Two");
        board.Add("b", "Three", "Four");
        board.Add("c", "Five");
        using var driver = UiDriver.Render(ui => board.Render(ui, grouped: true));
        var seams = new List<string>();

        var press = driver.Hold(driver.Get("One"));
        foreach (
            var to in (Vector2[])
                [new(20, 88), new(60, 88), new(140, 88), new(20, 160), new(100, 16), new(400, 16)]
        )
        {
            press.MoveBy(to - press.Position);
            seams.Add(TabBoard.Seams(driver));
        }

        await Assert
            .That(string.Join(" ; ", seams))
            .IsEqualTo("b 0,0 0,32 ; b 80,0 80,32 ; b 160,0 160,32 ; c 0,0 0,32 ;  ; ");
    }

    [Test]
    public async Task A_tab_dragged_along_a_grouped_strip_that_cant_reorder_moves_nothing_and_still_reports_its_end()
    {
        var board = new TabBoard();
        board.Add("a", "One", "Two");
        board.Add("b", "Three");
        using var driver = UiDriver.Render(ui =>
            board.Render(ui, grouped: true, reorderable: false)
        );

        var press = driver.Hold(driver.Get("One"));
        press.MoveBy(new Vector2(82, 0));
        press.Release();

        await Assert.That(board.Log.ToString()).IsEqualTo("end One");
        await Assert.That(board.Order).IsEqualTo("One,Two | Three");
    }

    [Test]
    public async Task A_right_click_during_a_left_tab_drag_leaves_the_drag_to_the_left_button()
    {
        var board = new TabBoard();
        board.Add("a", "One", "Two");
        using var driver = UiDriver.Render(ui => board.Render(ui));

        var left = driver.Hold(driver.Get("One"));
        left.MoveBy(new Vector2(0, 200));
        driver.Hold(driver.Get("One"), button: MouseButton.Right).Release();
        var whileHeld = board.Log.ToString();
        left.Release();

        await Assert.That(whileHeld).IsEqualTo("");
        await Assert.That(board.Log.ToString()).IsEqualTo("tear One 40,216 end One");
    }

    [Test]
    public async Task A_tab_drag_losing_its_pointer_ends_as_it_is_lost()
    {
        var board = new TabBoard();
        board.Add("a", "One", "Two", "Three");
        using var driver = UiDriver.Render(ui => board.Render(ui));

        var press = driver.Hold(driver.Get("One"));
        press.MoveBy(new Vector2(20, 0));
        var midDrag = board.Log.ToString();
        press.LoseRelease();

        await Assert.That(midDrag).IsEqualTo("");
        await Assert.That(board.Log.ToString()).IsEqualTo("end One");
    }

    [Test]
    public async Task A_dragged_tab_removed_or_its_strip_unmounted_mid_drag_reports_its_end_once()
    {
        var removing = new TabBoard();
        var strip = removing.Add("a", "One", "Two");
        using (var driver = UiDriver.Render(ui => removing.Render(ui)))
        {
            var press = driver.Hold(driver.Get("One"));
            press.MoveBy(new Vector2(20, 0));
            strip.Tabs.Remove("One");
            driver.Settle();
            press.Release();
        }
        var unmounting = new TabBoard();
        var gone = unmounting.Add("a", "One", "Two");
        using (var driver = UiDriver.Render(ui => unmounting.Render(ui)))
        {
            var press = driver.Hold(driver.Get("One"));
            press.MoveBy(new Vector2(20, 0));
            gone.Mounted = false;
            driver.Rerender();
            press.Release();
        }

        await Assert.That(removing.Log.ToString()).IsEqualTo("end One");
        await Assert.That(unmounting.Log.ToString()).IsEqualTo("end One");
    }

    [Test]
    public async Task Each_drag_move_reports_the_tab_the_point_and_the_strip_under_it_after_the_strips_show_the_landing()
    {
        var board = new TabBoard();
        board.Add("a", "One", "Two").Drag = board.Dragged;
        board.Add("b", "Three");
        using var driver = UiDriver.Render(ui => board.Render(ui, grouped: true));

        var press = driver.Hold(driver.Get("One"));
        press.MoveBy(new Vector2(20, 72));
        var seams = TabBoard.Seams(driver);
        press.MoveBy(new Vector2(360, -72));
        press.Release();

        await Assert.That(seams).IsEqualTo("");
        await Assert
            .That(board.Log.ToString())
            .IsEqualTo("drag One 60,88 b drag One 420,16 - tear One 420,16 end One");
    }

    [Test]
    public async Task A_finger_swipes_an_overflowing_reorderable_list_and_drags_a_tab_only_after_holding_it()
    {
        var board = new TabBoard();
        board.Add("a", "One", "Two", "Three", "Four").Width = 160;
        using var driver = UiDriver.Render(ui => board.Render(ui));

        driver.Drag(driver.Get("Two"), new Vector2(-40, 0), PointerKind.Touch);
        var swiped = (driver.Get("One").ViewBounds.X, board.Log.ToString(), board.Order);
        var finger = driver.Hold(driver.Get("Three"), PointerKind.Touch);
        driver.Frame(HoldSeconds);
        finger.MoveBy(new Vector2(-100, 0));
        finger.Release();

        await Assert.That(swiped).IsEqualTo((-40f, "", "One,Two,Three,Four"));
        await Assert.That(board.Log.ToString()).IsEqualTo("reorder Three 2 1 end Three");
        await Assert.That(board.Order).IsEqualTo("One,Three,Two,Four");
        await Assert.That(driver.Get("One").ViewBounds.X).IsEqualTo(-40f);
    }

    [Test]
    public async Task A_finger_drag_without_a_hold_picks_up_no_tab_and_a_second_finger_leaves_a_held_drag_with_the_first()
    {
        var board = new TabBoard();
        board.Add("a", "One", "Two", "Three");
        using var driver = UiDriver.Render(ui => board.Render(ui));

        driver.Tap(driver.Get("Two"));
        driver.Drag(driver.Get("Three"), new Vector2(40, 0), PointerKind.Touch);
        var swept = (TabLog.SelectedOf(driver), board.Log.ToString());
        var first = driver.Hold(driver.Get("One"), PointerKind.Touch);
        driver.Frame(HoldSeconds);
        first.MoveBy(new Vector2(20, 0));
        driver.Tap(driver.Get("Three"));
        var midDrag = board.Log.ToString();
        first.Release();

        await Assert.That(swept).IsEqualTo(("Two", ""));
        await Assert.That(midDrag).IsEqualTo("");
        await Assert.That(board.Log.ToString()).IsEqualTo("end One");
    }

    [Test]
    public async Task A_finger_swiping_a_page_from_a_reorderable_tab_scrolls_the_page_and_leaves_the_tab_be()
    {
        var board = new TabBoard();
        board.Add("a", "One", "Two");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, height: 200, overflow: Overflow.ScrollY))
            {
                board.Render(ui);
                using (ui.Box(height: 2000)) { }
            }
        });

        driver.Drag(driver.Get("One"), new Vector2(0, -100), PointerKind.Touch);

        await Assert.That(driver.Get("One").ViewBounds.Y).IsEqualTo(-100f);
        await Assert.That((board.Log.ToString(), board.Order)).IsEqualTo(("", "One,Two"));
    }

    [Test]
    public async Task A_strip_carried_over_overlapping_strips_is_passed_over_and_the_one_in_front_takes_the_point()
    {
        var board = new TabBoard();
        board.Add("behind", "One");
        board.Add("front", "Two");
        board.Add("carried", "Three");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 320, height: 100))
                foreach (var strip in board.Strips)
                    TabStrip.Render(
                        ui,
                        strip,
                        board.Group,
                        reorderable: true,
                        stacked: true,
                        key: strip.Key
                    );
        });
        var three = driver.Get("Three").ViewBounds;

        var hit = board.Group.HitTest(
            driver.View,
            new Vector2(three.X + 20, three.Y + three.Height / 2),
            driver.Get("carried"),
            out _
        );

        await Assert.That(hit).IsEqualTo((UiKey?)"front");
    }

    [Test]
    public async Task A_strip_of_the_group_in_another_view_or_not_shown_is_never_a_drop_target()
    {
        var board = new TabBoard();
        board.Add("gone", "Five").Hidden = true;
        board.Add("a", "One", "Two");
        var elsewhere = new TabBoard();
        elsewhere.Add("b", "Three");
        using var other = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
                TabStrip.Render(ui, elsewhere.Strips[0], board.Group, reorderable: true);
        });
        using var driver = UiDriver.Render(ui => board.Render(ui, grouped: true));

        var press = driver.Hold(driver.Get("One"));
        press.MoveBy(new Vector2(0, -68));
        press.Release();

        await Assert.That(board.Log.ToString()).IsEqualTo("tear One 40,20 end One");
        await Assert.That(elsewhere.Order).IsEqualTo("Three");
    }

    [Test]
    public async Task A_grouped_strip_leaves_its_group_as_it_leaves_the_view_and_rejoins_it_as_it_is_put_back()
    {
        var board = new TabBoard();
        board.Add("a", "One", "Two");
        var back = board.Add("b", "Three");
        using var driver = UiDriver.Render(ui => board.Render(ui, grouped: true));

        back.Mounted = false;
        driver.Rerender();
        var left = board.Group.Count;
        back.Mounted = true;
        driver.Rerender();
        var press = driver.Hold(driver.Get("One"));
        press.MoveBy(new Vector2(20, 72));
        press.Release();

        await Assert.That((left, board.Group.Count)).IsEqualTo((1, 2));
        await Assert.That(board.Order).IsEqualTo("Two | Three,One");
    }
}

internal sealed class TabBoard
{
    public readonly TabDragGroup Group = new();
    public readonly List<TabStrip> Strips = [];
    public readonly ValueLog<TabEvent> Log = new(static e => e.Spelled());
    public readonly Callback<TabDrag> Dragged;
    public bool Quiet;
    public int Reorders;

    public TabBoard() => Dragged = Callback<TabDrag>.Of(this, static (b, d) => b.Moved(d));

    public TabStrip Add(string key, params string[] tabs)
    {
        var strip = new TabStrip(this, key);
        foreach (var tab in tabs)
            strip.Tabs.Add(tab);
        Strips.Add(strip);
        return strip;
    }

    public string Order =>
        string.Join(
            " | ",
            Strips.Where(static s => s.Mounted).Select(static s => string.Join(",", s.Tabs))
        );

    void Moved(TabDrag drag)
    {
        Log.Add(new TabEvent("drag", drag.Value, drag.Over, drag.Position));
        Group.Hint(null, 0);
    }

    public static string Seams(UiDriver driver)
    {
        var seams = new List<string>();
        foreach (var line in driver.Draw().Lines)
        {
            var at = line.IndexOf("/Canvas", StringComparison.Ordinal);
            var draw = line.IndexOf(": line ", StringComparison.Ordinal);
            if (at < 0 || draw < 0)
                continue;
            var strip = line[..at];
            strip = strip[(strip.LastIndexOf('#') + 1)..];
            var ends = line[(draw + ": line ".Length)..];
            seams.Add($"{strip} {ends[..ends.IndexOf(" width", StringComparison.Ordinal)]}");
        }
        return string.Join(" ; ", seams);
    }

    public TabStrip Named(UiKey key)
    {
        foreach (var strip in Strips)
        {
            if ((UiKey)strip.Key == key)
                return strip;
        }
        throw new InvalidOperationException("No strip has the key.");
    }

    [FramePath]
    public void Render(
        Ui ui,
        bool grouped = false,
        bool reorderable = true,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Box(direction: Direction.Column, align: Align.Start, file: file, line: line))
            foreach (var strip in Strips)
                if (strip.Mounted)
                    TabStrip.Render(ui, strip, grouped ? Group : null, reorderable, key: strip.Key);
    }
}

internal sealed class TabStrip
{
    public readonly TabBoard Board;
    public readonly string Key;
    public readonly ObservableList<string> Tabs = new();
    public readonly Callback<TabReorder> Reorder;
    public readonly Callback<TabMove> Move;
    public readonly Callback<TabTearOff> TearOff;
    public readonly Callback<UiKey> End;
    public Callback<TabDrag> Drag { get; set; }
    public string? HiddenTab { get; set; }
    public float? Height { get; set; }
    public float? Width { get; set; }
    public bool Hidden { get; set; }
    public bool Mounted { get; set; } = true;

    public TabStrip(TabBoard board, string key)
    {
        Board = board;
        Key = key;
        Reorder = Callback<TabReorder>.Of(this, static (s, r) => s.Reordered(r));
        Move = Callback<TabMove>.Of(this, static (s, m) => s.Moved(m));
        TearOff = Callback<TabTearOff>.Of(this, static (s, t) => s.Torn(t));
        End = Callback<UiKey>.Of(this, static (s, v) => s.Ended(v));
    }

    void Torn(TabTearOff t) => Board.Log.Add(new TabEvent("tear", t.Value, null, t.Position));

    void Ended(UiKey value)
    {
        if (!Board.Quiet)
            Board.Log.Add(new TabEvent("end", value, null, default));
    }

    void Reordered(TabReorder r)
    {
        Board.Reorders++;
        if (!Board.Quiet)
            Board.Log.Add(new TabEvent("reorder", r.Value, null, default, r.From, r.To));
        Tabs.Move(r.From, r.To);
    }

    void Moved(TabMove m)
    {
        var tab = TabLog.Name(m.Value);
        Board.Log.Add(new TabEvent("move", m.Value, m.Target, default, To: m.Gap));
        Tabs.Remove(tab);
        var target = Board.Named(m.Target).Tabs;
        target.Insert(Math.Min(m.Gap, target.Count), tab);
    }

    [FramePath]
    public static void Render(
        Ui ui,
        TabStrip strip,
        TabDragGroup? group,
        bool reorderable,
        bool stacked = false,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Tabs(
                width: strip.Width ?? 320,
                height: strip.Height is { } height ? height : Length.Auto,
                reorderable: reorderable,
                dragGroup: group,
                groupKey: strip.Key,
                onReorder: strip.Reorder,
                onTabMove: strip.Move,
                onTabTearOff: strip.TearOff,
                onTabDrag: strip.Drag,
                onTabDragEnd: strip.End,
                hidden: strip.Hidden,
                position: stacked ? Position.Absolute : default,
                id: strip.Key,
                key: key,
                file: file,
                line: line
            )
        )
        {
            using (ui.TabsList())
                foreach (var tab in strip.Tabs)
                    ui.TabsTab(
                        value: tab,
                        text: tab,
                        width: 80,
                        hidden: tab == strip.HiddenTab,
                        key: tab,
                        id: tab
                    );
            foreach (var tab in strip.Tabs)
                using (ui.TabsPanel(value: tab, key: tab))
                using (ui.Box(height: 40)) { }
        }
    }
}

internal readonly record struct TabEvent(
    string Verb,
    UiKey Tab,
    UiKey? Other,
    Vector2 At,
    int From = 0,
    int To = 0
)
{
    public string Spelled()
    {
        var tab = TabLog.Name(Tab);
        var other = Other is { } key ? TabLog.Name(key) : "-";
        return Verb switch
        {
            "drag" => string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"drag {tab} {At.X},{At.Y} {other}"
            ),
            "tear" => string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"tear {tab} {At.X},{At.Y}"
            ),
            "reorder" => $"reorder {tab} {From} {To}",
            "move" => $"move {tab} {other} {To}",
            _ => $"end {tab}",
        };
    }
}

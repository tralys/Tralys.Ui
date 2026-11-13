using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class TabsAllocationTests
{
    static readonly string[] Names = ["Tab 1", "Tab 2", "Tab 3", "Tab 4", "Tab 5", "Tab 6"];

    static readonly string[] Contents =
    [
        "Content 1",
        "Content 2",
        "Content 3",
        "Content 4",
        "Content 5",
        "Content 6",
    ];

    [Test]
    public async Task Tabs_switched_by_the_arrows_and_ctrl_tab_every_other_frame_show_each_panel_and_allocate_nothing()
    {
        using var driver = UiDriver.Render(static ui =>
        {
            using (ui.Tabs())
            {
                using (ui.TabsList())
                    for (var i = 0; i < Names.Length; i++)
                        ui.TabsTab(value: i, text: Names[i], key: i, id: Names[i]);
                for (var i = 0; i < Names.Length; i++)
                    using (ui.TabsPanel(value: i, key: i))
                        ui.Text(Contents[i]);
            }
        });
        var walk = new Walk(driver, Names);
        driver.Focus(walk.Tabs[0]);

        var bytes = driver.WorstFrameBytes(
            walk,
            static (d, w) =>
            {
                if (w.Tick++ % 2 == 0)
                {
                    if (w.Tick % 3 == 0)
                        d.Press(Key.Tab, ModifierKeys.Control, settle: false);
                    else
                        d.Press(Key.Right, settle: false);
                    w.At = (w.At + 1) % w.Tabs.Length;
                    w.Switches++;
                }
                d.Frame();
                var tab = w.Tabs[w.At];
                if (
                    d.Focused == tab
                    && (tab.States & InputStates.Selected) != 0
                    && Shows(d.Root, Contents[w.At])
                )
                    w.Shown++;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(walk.Shown).IsEqualTo(walk.Tick);
        await Assert.That(walk.Switches).IsGreaterThan(10);
    }

    [Test]
    public async Task A_finger_swiping_an_overflowing_reorderable_list_every_frame_scrolls_it_and_allocates_nothing()
    {
        using var driver = UiDriver.Render(static ui =>
        {
            using (ui.Tabs(width: 240, reorderable: true))
            using (ui.TabsList())
                for (var i = 0; i < Names.Length; i++)
                    ui.TabsTab(value: i, text: Names[i], key: i, id: Names[i]);
        });
        var walk = new Walk(driver, Names);

        var bytes = driver.WorstFrameBytes(
            walk,
            static (d, w) =>
            {
                var before = w.Tabs[0].ViewBounds.X;
                d.Drag(
                    w.Tabs[1],
                    new Vector2(w.Tick++ % 2 == 0 ? -30f : 30f, 0f),
                    PointerKind.Touch,
                    settle: false
                );
                d.Frame();
                if (w.Tabs[0].ViewBounds.X != before)
                    w.Shown++;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(walk.Shown).IsEqualTo(walk.Tick);
    }

    [Test]
    public async Task A_tab_dragged_over_a_strip_with_no_tabs_every_frame_marks_where_it_lands_and_allocates_nothing()
    {
        var board = new TabBoard { Quiet = true };
        board.Add("a", "One", "Two");
        board.Add("b").Height = 100;
        using var driver = UiDriver.Render(ui => board.Render(ui, grouped: true));
        var press = driver.Hold(driver.Get("One"));
        press.MoveBy(new Vector2(0, 86));
        var moves = new Moves(press, new Vector2(160, 30));

        var bytes = driver.WorstFrameBytes(
            moves,
            static (d, m) =>
            {
                m.Press.MoveBy(m.Tick++ % 2 == 0 ? m.By : -m.By, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(TabBoard.Seams(driver)).IsEqualTo("b 0,0 0,100");
    }

    [Test]
    public async Task A_tab_dragged_across_another_strips_tab_every_frame_moves_its_marker_and_allocates_nothing()
    {
        var board = new TabBoard { Quiet = true };
        board.Add("a", "One", "Two");
        board.Add("b", "Three", "Four");
        using var driver = UiDriver.Render(ui => board.Render(ui, grouped: true));
        var press = driver.Hold(driver.Get("One"));
        press.MoveBy(new Vector2(-20, 72));
        var moves = new Moves(press, new Vector2(40, 0));
        var seams = new HashSet<string>();

        var bytes = driver.WorstFrameBytes(
            moves,
            static (d, m) =>
            {
                m.Press.MoveBy(m.Tick++ % 2 == 0 ? m.By : -m.By, settle: false);
                d.Frame();
            }
        );
        seams.Add(TabBoard.Seams(driver));
        press.MoveBy(moves.Tick % 2 == 0 ? moves.By : -moves.By);
        seams.Add(TabBoard.Seams(driver));

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(seams).IsEquivalentTo(["b 0,0 0,32", "b 80,0 80,32"]);
    }

    [Test]
    public async Task A_tab_dragged_back_and_forth_past_its_neighbour_every_frame_reorders_each_time_and_allocates_nothing()
    {
        var board = new TabBoard { Quiet = true };
        board.Add("a", "One", "Two", "Three");
        using var driver = UiDriver.Render(ui => board.Render(ui));
        var press = driver.Hold(driver.Get("One"));
        press.MoveBy(new Vector2(-2, 0));
        var moves = new Moves(press, new Vector2(84, 0));

        var bytes = driver.WorstFrameBytes(
            moves,
            static (d, m) =>
            {
                m.Press.MoveBy(m.Tick++ % 2 == 0 ? m.By : -m.By, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(board.Reorders).IsEqualTo(moves.Tick);
    }

    static bool Shows(Node node, string text)
    {
        foreach (var child in node.Children)
        {
            if (child.Kind == NodeKind.Text && child.Text.SequenceEqual(text))
                return true;
            if (Shows(child, text))
                return true;
        }
        return false;
    }

    sealed class Walk
    {
        public readonly Node[] Tabs;
        public int Tick;
        public int At;
        public int Switches;
        public int Shown;

        public Walk(UiDriver driver, string[] ids)
        {
            Tabs = new Node[ids.Length];
            for (var i = 0; i < ids.Length; i++)
                Tabs[i] = driver.Get(ids[i]);
        }
    }

    sealed class Moves(HeldPointer press, Vector2 by)
    {
        public readonly HeldPointer Press = press;
        public readonly Vector2 By = by;
        public int Tick;
    }
}

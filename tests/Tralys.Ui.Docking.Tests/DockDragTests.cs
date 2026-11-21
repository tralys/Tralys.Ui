using System.Numerics;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Docking.Tests;

public sealed class DockDragTests
{
    static void MoveTo(HeldPointer pointer, Vector2 point) =>
        pointer.MoveBy(point - pointer.Position);

    [Test]
    public async Task The_move_that_starts_a_tab_drag_already_previews_the_group_it_carries_the_tab_over()
    {
        var dock = new DockView(
            ("a", DockPosition.Left),
            ("b", DockPosition.Left),
            ("c", DockPosition.Center)
        );
        var overTheCentreGroup = DockView.CentreOf(dock.Strip(dock["c"]));
        var before = dock.OverlayShown;

        var mouse = dock.Ui.Hold(dock.Tab("a"));
        MoveTo(mouse, overTheCentreGroup);
        var previews = dock.Previews.Count;
        mouse.Release();

        await Assert.That(before).IsFalse();
        await Assert.That(previews).IsEqualTo(1);
        await Assert.That(dock.OverlayShown).IsFalse();
    }

    [Test]
    public async Task A_tab_press_moved_short_of_the_drag_threshold_stays_a_press_and_one_dropped_past_it_on_its_strip_stays()
    {
        var dock = new DockView(
            ("a", DockPosition.Left),
            ("b", DockPosition.Left),
            ("c", DockPosition.Center)
        );
        var group = dock["a"].Parent;

        var mouse = dock.Ui.Hold(dock.Tab("a"));
        mouse.MoveBy(new Vector2(3f, 0f));
        var shortOfIt = dock.OverlayShown;
        mouse.MoveBy(new Vector2(2f, 0f));
        var pastIt = dock.OverlayShown;
        mouse.Release();

        await Assert.That(shortOfIt).IsFalse();
        await Assert.That(pastIt).IsTrue();
        await Assert.That(dock["a"].Parent).IsSameReferenceAs(group);
    }

    [Test]
    public async Task A_tab_dragged_over_a_float_offers_only_the_centre_arrow()
    {
        var dock = new DockView(
            ("a", DockPosition.Left),
            ("c", DockPosition.Center),
            ("d", DockPosition.Center)
        );
        dock.Manager.Float(dock["d"], 300f, 200f, 400f, 300f);
        dock.Ui.Settle();

        var mouse = dock.Ui.Hold(dock.Tab("a"));
        mouse.MoveBy(new Vector2(10f, 0f));
        MoveTo(mouse, DockView.CentreOf(dock.Strip(dock["d"])));
        var compass = dock.Compass.ConvertAll(DockView.CentreOf);
        var floatCentre = DockView.CentreOf(dock.Strip(dock["d"]));
        mouse.Release();

        await Assert.That(compass).IsEquivalentTo(new[] { floatCentre });
    }

    [Test]
    public async Task A_tab_dropped_where_floats_overlap_joins_the_top_one()
    {
        var dock = new DockView(
            ("a", DockPosition.Left),
            ("c", DockPosition.Center),
            ("d", DockPosition.Center),
            ("e", DockPosition.Center)
        );
        dock.Manager.Float(dock["d"], 200f, 100f, 400f, 300f);
        var top = dock.Manager.Float(dock["e"], 300f, 150f, 400f, 300f);
        dock.Ui.Settle();

        var mouse = dock.Ui.Hold(dock.Tab("a"));
        mouse.MoveBy(new Vector2(10f, 0f));
        MoveTo(mouse, new Vector2(450f, 300f));
        MoveTo(mouse, DockView.CentreOf(dock.Compass[0]));
        mouse.Release();

        await Assert.That(dock["a"].Parent).IsSameReferenceAs(top.Group);
    }

    [Test]
    public async Task A_tab_dropped_on_a_groups_left_compass_arrow_splits_that_group_with_the_tab_on_its_left()
    {
        var dock = new DockView(("a", DockPosition.Left), ("c", DockPosition.Center));

        var mouse = dock.Ui.Hold(dock.Tab("a"));
        mouse.MoveBy(new Vector2(10f, 0f));
        MoveTo(mouse, DockView.CentreOf(dock.Strip(dock["c"])));
        MoveTo(mouse, DockView.CentreOf(dock.Compass[0]));
        var preview = dock.Previews[0].ViewBounds;
        var group = dock.Strip(dock["c"]).ViewBounds;
        mouse.Release();
        var a = dock.Tab("a").ViewBounds;
        var c = dock.Tab("c").ViewBounds;

        await Assert.That(preview).IsEqualTo(group with { Width = group.Width / 2f });
        await Assert.That(dock.Compass.Count).IsEqualTo(0);
        await Assert.That(a.X).IsLessThan(c.X);
        await Assert.That(a.Y).IsEqualTo(c.Y);
        await Assert.That(dock["a"].Parent?.Parent).IsSameReferenceAs(dock.Manager.CenterGroup);
    }

    [Test]
    public async Task A_tab_dropped_on_the_right_indicator_docks_along_that_edge_and_previews_a_quarter_of_the_dock()
    {
        var dock = new DockView(("a", DockPosition.Left), ("c", DockPosition.Center));

        var mouse = dock.Ui.Hold(dock.Tab("a"));
        mouse.MoveBy(new Vector2(10f, 0f));
        MoveTo(mouse, new Vector2(960f, 350f));
        var preview = dock.Previews[0].ViewBounds;
        var fills = dock
            .Ui.Draw()
            .Lines.Where(line =>
                line.Contains("/DockOverlay[", StringComparison.Ordinal)
                && line.Contains(" 40x40 ", StringComparison.Ordinal)
            )
            .Select(line => line.Split(" fill ")[1])
            .ToList();
        var label = DockView.All(
            dock.Overlay,
            node => node.Kind == NodeKind.Text && node.Text.SequenceEqual("a")
        )[0];
        var labelAt = (await Assert.That(label.Parent).IsNotNull()).ViewBounds;
        mouse.Release();

        await Assert.That(preview).IsEqualTo(new Rect(750f, 0f, 250f, 700f));
        await Assert.That(fills.Count(fill => fill == fills[1])).IsEqualTo(1);
        await Assert.That(fills.Distinct().Count()).IsEqualTo(2);
        await Assert.That(new Vector2(labelAt.X, labelAt.Y)).IsEqualTo(new Vector2(975f, 365f));
        await Assert.That(dock.Manager.RightGroup.Elements).Contains(dock["a"]);
        await Assert.That(dock.OverlayShown).IsFalse();
    }

    [Test]
    public async Task A_tab_dropped_over_nothing_floats_where_it_was_dropped()
    {
        var dock = new DockView(
            ("a", DockPosition.Left),
            ("b", DockPosition.Left),
            ("c", DockPosition.Center)
        );

        var mouse = dock.Ui.Hold(dock.Tab("a"));
        mouse.MoveBy(new Vector2(10f, 0f));
        MoveTo(mouse, new Vector2(500f, 690f));
        mouse.Release();

        await Assert.That(dock["a"].State).IsEqualTo(DockState.Floating);
        await Assert.That(dock["a"].Parent?.Window?.X).IsEqualTo(500f);
        await Assert.That(dock.Manager.ActiveElement).IsSameReferenceAs(dock["a"]);
    }

    [Test]
    public async Task A_click_on_the_selected_tab_of_another_group_activates_it()
    {
        var dock = new DockView(("a", DockPosition.Left), ("c", DockPosition.Center));
        dock["c"].Activate();

        dock.Ui.Click(dock.Tab("a"));

        await Assert.That(dock.Manager.ActiveElement).IsSameReferenceAs(dock["a"]);
    }

    [Test]
    public async Task A_press_on_a_tab_of_a_dock_inside_a_tab_activates_the_outer_tab()
    {
        var inner = DockView.Docked(("i", DockPosition.Center));
        var manager = DockView.Docked(("a", DockPosition.Left), ("c", DockPosition.Center));
        manager.Element("a").Content = inner;
        var dock = new DockView(
            manager,
            content: Slot<DockElement>.Of(
                static (ui, element, _) =>
                {
                    if (element.Content is DockManager nested)
                        ui.Dock(manager: nested);
                }
            )
        );
        manager.Element("c").Activate();

        dock.Ui.Click(dock.Tab("i"));

        await Assert.That(manager.ActiveElement).IsSameReferenceAs(manager.GetElement("a"));
        await Assert.That(inner.ActiveElement).IsSameReferenceAs(inner.GetElement("i"));
    }

    [Test]
    public async Task A_two_tab_float_docked_by_its_title_moves_both_tabs_in_order_and_keeps_the_active_one()
    {
        var dock = new DockView(
            ("a", DockPosition.Left),
            ("b", DockPosition.Left),
            ("c", DockPosition.Center)
        );
        var window = dock.Manager.Float(dock["a"], 200f, 200f, 300f, 200f);
        dock.Manager.DockToGroup(dock["b"], window.Group);
        dock["a"].Activate();
        dock.Ui.Settle();

        var mouse = dock.Ui.Hold(dock.Title());
        mouse.MoveBy(new Vector2(10f, 0f));
        MoveTo(mouse, new Vector2(960f, 350f));
        mouse.Release();

        await Assert.That(dock.Manager.RightGroup.Elements[0]).IsSameReferenceAs(dock["a"]);
        await Assert.That(dock.Manager.RightGroup.Elements[1]).IsSameReferenceAs(dock["b"]);
        await Assert.That(dock.Manager.ActiveElement).IsSameReferenceAs(dock["a"]);
        await Assert.That(dock.Manager.RightGroup.ActiveElement).IsSameReferenceAs(dock["a"]);
        await Assert.That(dock.Manager.FloatingWindows.Count).IsEqualTo(0);
        await Assert.That(dock.OverlayShown).IsFalse();
    }

    [Test]
    public async Task A_float_dragged_by_its_title_onto_a_strip_joins_it_at_the_gap()
    {
        var dock = new DockView(
            ("a", DockPosition.Left),
            ("c", DockPosition.Center),
            ("d", DockPosition.Center)
        );
        dock.Manager.Float(dock["a"], 600f, 400f, 300f, 200f);
        dock.Ui.Settle();
        var c = dock.Tab("c").ViewBounds;

        var mouse = dock.Ui.Hold(dock.Title());
        mouse.MoveBy(new Vector2(10f, 0f));
        MoveTo(mouse, new Vector2(c.X + 2f, c.Y + c.Height / 2f));
        mouse.Release();

        await Assert.That(dock.Manager.CenterGroup.Elements[0]).IsSameReferenceAs(dock["a"]);
        await Assert.That(dock.Manager.FloatingWindows.Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_float_dragged_past_the_docks_edge_stays_inside_and_the_dock_shrinking_brings_it_along()
    {
        var dock = new DockView(("a", DockPosition.Left), ("c", DockPosition.Center));
        var window = dock.Manager.Float(dock["a"], 200f, 200f, 300f, 200f);
        dock.Ui.Settle();

        var mouse = dock.Ui.Hold(dock.Title());
        mouse.MoveBy(new Vector2(10f, 0f));
        MoveTo(mouse, new Vector2(5000f, 5000f));
        mouse.Release();
        var dragged = new Vector2(window.X, window.Y);
        dock.Ui.View.Size = new Size(600f, 400f);
        dock.Ui.Settle();

        await Assert.That(dragged).IsEqualTo(new Vector2(700f, 500f));
        await Assert.That(new Vector2(window.X, window.Y)).IsEqualTo(new Vector2(300f, 200f));
    }

    [Test]
    public async Task A_float_that_leaves_mid_title_drag_takes_the_drag_overlay_away()
    {
        var dock = new DockView(("a", DockPosition.Left), ("c", DockPosition.Center));
        dock.Manager.Float(dock["a"], 200f, 200f, 300f, 200f);
        dock.Ui.Settle();

        var mouse = dock.Ui.Hold(dock.Title());
        mouse.MoveBy(new Vector2(10f, 0f));
        var overlay = dock.OverlayShown;
        dock.Manager.Dock(dock["a"], DockPosition.Right);
        dock.Ui.Settle();

        await Assert.That(overlay).IsTrue();
        await Assert.That(dock.OverlayShown).IsFalse();
    }

    [Test]
    public async Task A_window_deactivating_mid_title_drag_leaves_the_panel_floating_and_takes_the_overlay_away()
    {
        var dock = new DockView(("a", DockPosition.Left), ("c", DockPosition.Center));
        dock.Manager.Float(dock["a"], 200f, 200f, 300f, 200f);
        dock.Ui.Settle();

        var mouse = dock.Ui.Hold(dock.Title());
        mouse.MoveBy(new Vector2(10f, 0f));
        MoveTo(mouse, new Vector2(960f, 350f));
        dock.Ui.WindowFocus(false);

        await Assert.That(dock.Manager.RightGroup.Elements).DoesNotContain(dock["a"]);
        await Assert.That(dock["a"].State).IsEqualTo(DockState.Floating);
        await Assert.That(dock.OverlayShown).IsFalse();
    }

    [Test]
    public async Task A_tab_docked_out_of_a_split_side_takes_the_drag_overlay_away()
    {
        var dock = new DockView(
            ("a", DockPosition.Left),
            ("c", DockPosition.Center),
            ("d", DockPosition.Center)
        );
        var centre = await Assert.That(dock["d"].Parent).IsNotNull();
        dock.Manager.DockBySplit(dock["d"], centre, DockDirection.Right);
        dock.Ui.Settle();

        var mouse = dock.Ui.Hold(dock.Tab("d"));
        mouse.MoveBy(new Vector2(10f, 0f));
        MoveTo(mouse, DockView.CentreOf(dock.Tab("a")));
        mouse.Release();

        await Assert.That(dock.Manager.LeftGroup.Elements).Contains(dock["d"]);
        await Assert.That(dock.OverlayShown).IsFalse();
    }

    [Test]
    public async Task A_tab_held_by_a_finger_drags_if_the_finger_moves_and_opens_its_menu_if_it_lifts()
    {
        var dock = new DockView(("a", DockPosition.Left), ("c", DockPosition.Center));

        var moving = dock.Ui.Hold(dock.Tab("a"), kind: PointerKind.Touch);
        dock.Ui.Frame(0.6f);
        moving.MoveBy(new Vector2(0f, 60f));
        var overlay = dock.OverlayShown;
        var menuWhileDragging = dock.Ui.View.Layers.Length;
        moving.Cancel();
        var lifting = dock.Ui.Hold(dock.Tab("a"), kind: PointerKind.Touch);
        dock.Ui.Frame(0.6f);
        lifting.Release();

        await Assert.That(overlay).IsTrue();
        await Assert.That(menuWhileDragging).IsEqualTo(0);
        await Assert.That(dock.OverlayShown).IsFalse();
        await Assert.That(dock.Ui.GetText("Close All")).IsNotNull();
    }

    [Test]
    public async Task Close_others_on_a_tabs_menu_closes_the_rest_of_its_group_only()
    {
        var dock = new DockView(
            ("a", DockPosition.Left),
            ("c", DockPosition.Center),
            ("d", DockPosition.Center),
            ("e", DockPosition.Center)
        );

        dock.Ui.Click(dock.Tab("d"), MouseButton.Right);
        dock.Ui.Click(dock.Ui.GetText("Close Others"));

        await Assert.That(dock["d"].Parent?.Elements).IsEquivalentTo(new[] { dock["d"] });
        await Assert.That(dock.Manager.LeftGroup.Elements).Contains(dock["a"]);
    }

    [Test]
    public async Task A_tabs_menu_opened_again_on_another_tab_closes_that_tab()
    {
        var dock = new DockView(
            ("c", DockPosition.Center),
            ("d", DockPosition.Center),
            ("e", DockPosition.Center)
        );
        var group = await Assert.That(dock["d"].Parent).IsNotNull();

        dock.Ui.Click(dock.Tab("d"), MouseButton.Right);
        dock.Ui.Press(Key.Escape);
        dock.Ui.Click(dock.Tab("e"), MouseButton.Right);
        dock.Ui.Click(dock.Ui.GetText("Close"));

        await Assert.That(group.Elements).Contains(dock["d"]);
        await Assert.That(group.Elements).DoesNotContain(dock["e"]);
    }

    [Test]
    public async Task A_tab_dragged_along_its_strip_past_a_neighbour_moves_there()
    {
        var dock = new DockView(
            ("c", DockPosition.Center),
            ("d", DockPosition.Center),
            ("e", DockPosition.Center)
        );
        var d = dock.Tab("d").ViewBounds;

        var mouse = dock.Ui.Hold(dock.Tab("c"));
        mouse.MoveBy(new Vector2(10f, 0f));
        MoveTo(mouse, new Vector2(d.X + d.Width * 0.75f, d.Y + d.Height / 2f));
        mouse.Release();

        await Assert.That(dock.Manager.CenterGroup.Elements[1]).IsSameReferenceAs(dock["c"]);
    }

    [Test]
    public async Task The_centres_root_indicator_shows_only_while_the_centre_is_empty_and_takes_drops()
    {
        var manager = DockView.Docked(("a", DockPosition.Left), ("b", DockPosition.Left));
        var dockable = new DockView(manager);
        var refusing = new DockView(manager, centerDockable: false);
        static int Shown(DockView dock)
        {
            var mouse = dock.Ui.Hold(dock.Tab("a"));
            mouse.MoveBy(new Vector2(10f, 60f));
            var count = dock.Indicators.Count;
            mouse.Cancel();
            return count;
        }

        var empty = Shown(dockable);
        var refused = Shown(refusing);
        manager.Dock(manager.Element("b"), DockPosition.Center);
        dockable.Ui.Settle();
        var filled = Shown(dockable);

        await Assert.That(empty).IsEqualTo(5);
        await Assert.That(refused).IsEqualTo(4);
        await Assert.That(filled).IsEqualTo(4);
    }
}

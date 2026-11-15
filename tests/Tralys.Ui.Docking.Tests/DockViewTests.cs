using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Docking.Tests;

public sealed class DockViewTests
{
    static string LayoutWithLeftWidth(float width)
    {
        var manager = DockView.Docked(("a", DockPosition.Left), ("b", DockPosition.Center));
        manager.PanelSizes = new DockPanelSizes { LeftWidth = width };
        return manager.SaveLayout();
    }

    sealed class Source(string id, string title) : DockSource(id, title)
    {
        public override DockPosition DefaultPosition => DockPosition.Center;

        public void Edit(string title)
        {
            Title = title;
            IsDirty = true;
        }
    }

    [Test]
    public async Task A_loaded_layout_sizes_the_edge_regions()
    {
        var reference = new DockView(("a", DockPosition.Left), ("b", DockPosition.Center));
        var manager = DockView.Docked(("a", DockPosition.Left), ("b", DockPosition.Center));
        manager.LoadLayout(LayoutWithLeftWidth(412f));

        var loaded = new DockView(manager);

        await Assert.That(loaded.WidthOf(loaded["a"])).IsEqualTo(412f);
        await Assert.That(reference.WidthOf(reference["a"])).IsEqualTo(300f);
    }

    [Test]
    public async Task A_layout_without_an_edge_size_puts_a_dragged_edge_back_at_its_default()
    {
        var dock = new DockView(("a", DockPosition.Left), ("b", DockPosition.Center));
        var layout = dock.Manager.SaveLayout();
        var mouse = dock.Ui.Hold(dock.Handles(nested: false)[0]);
        mouse.MoveBy(new Vector2(88f, 0f));
        mouse.Release();
        var dragged = dock.WidthOf(dock["a"]);

        dock.Manager.LoadLayout(layout);
        dock.Ui.Settle();

        await Assert.That(dragged).IsEqualTo(388f);
        await Assert.That(dock.WidthOf(dock["a"])).IsEqualTo(300f);
    }

    [Test]
    public async Task A_dock_change_mid_drag_keeps_the_edge_its_handle_is_resizing()
    {
        var manager = DockView.Docked(("a", DockPosition.Left), ("b", DockPosition.Center));
        manager.LoadLayout(LayoutWithLeftWidth(412f));
        var dock = new DockView(manager);
        manager.Register(new DockElement("c", "c"));
        var mouse = dock.Ui.Hold(dock.Handles(nested: false)[0]);
        mouse.MoveBy(new Vector2(88f, 0f));

        manager.Dock(manager.GetElement("c")!, DockPosition.Center);
        dock.Ui.Settle();

        await Assert.That(dock.WidthOf(dock["a"])).IsEqualTo(500f);
    }

    [Test]
    public async Task A_click_then_an_arrow_key_on_an_edge_handle_reports_one_resize_and_records_it()
    {
        var dock = new DockView(("a", DockPosition.Left), ("b", DockPosition.Center));

        dock.Ui.Click(dock.Handles(nested: false)[0]);
        dock.Ui.Press(Key.Right);

        await Assert.That(dock.Resizes).IsEqualTo(1);
        await Assert.That(dock.Manager.PanelSizes.LeftWidth).IsEqualTo(310f);
        await Assert.That(dock.WidthOf(dock["a"])).IsEqualTo(310f);
    }

    [Test]
    public async Task Tab_walks_the_regions_and_their_handles_in_order_and_an_empty_edge_takes_no_stop()
    {
        var dock = new DockView(
            ("t", DockPosition.Top),
            ("l", DockPosition.Left),
            ("c", DockPosition.Center)
        );
        var stops = new List<Node?>();
        for (var i = 0; i < 5; i++)
        {
            dock.Ui.Tab();
            stops.Add(dock.Ui.Focused);
        }
        var handles = dock.Handles(nested: false);

        await Assert
            .That(stops)
            .IsEquivalentTo(
                new Node?[] { dock.Tab("t"), handles[0], dock.Tab("l"), handles[1], dock.Tab("c") }
            );
    }

    [Test]
    public async Task Clearing_the_manager_empties_the_regions_and_collapses_the_edges()
    {
        var dock = new DockView(("a", DockPosition.Left), ("c", DockPosition.Center));

        dock.Shown = null;
        dock.Ui.Rerender();

        await Assert
            .That(DockView.Any(dock.Ui.Root, node => node.Component == typeof(Tabs.Tab)))
            .IsFalse();
        await Assert.That(dock.Handles(nested: false).Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_host_adding_to_and_removing_from_a_group_keeps_the_dock_in_step()
    {
        var dock = new DockView(("c", DockPosition.Center));
        var x = new DockElement("x", "x");
        dock.Manager.Register(x);

        dock.Manager.LeftGroup.Add(x);
        dock.Ui.Settle();
        var shown = dock.WidthOf(x);
        x.Activate();
        dock.Manager.LeftGroup.Remove(x);
        dock.Ui.Settle();

        await Assert.That(shown).IsEqualTo(300f);
        await Assert.That(dock.Manager.ActiveElement).IsNull();
        await Assert.That(dock.WidthOf(x)).IsEqualTo(0f);
        await Assert.That(dock.Handles(nested: false).Count).IsEqualTo(0);
    }

    [Test]
    public async Task An_emptied_edge_opens_again_at_the_size_it_last_had()
    {
        var dock = new DockView(("a", DockPosition.Left), ("c", DockPosition.Center));
        dock.Ui.Click(dock.Handles(nested: false)[0]);
        dock.Ui.Press(Key.Right);

        dock["a"].Hide();
        dock.Ui.Settle();
        dock["a"].Show();
        dock.Ui.Settle();

        await Assert.That(dock.WidthOf(dock["a"])).IsEqualTo(310f);
    }

    [Test]
    public async Task The_empty_centre_shows_its_content_until_a_panel_docks_there()
    {
        var manager = DockView.Docked(("a", DockPosition.Left));
        manager.Register(new DockElement("c", "c"));
        using var ui = UiDriver.Render(
            ui =>
                ui.Dock(
                    manager: manager,
                    emptyCenter: Slot.Of(static ui => ui.Text("Nothing open"))
                ),
            size: new Size(1000f, 700f)
        );
        var empty = DockView.Any(
            ui.Root,
            node => node.Kind == NodeKind.Text && node.Text.SequenceEqual("Nothing open")
        );

        manager.Dock(manager.GetElement("c")!, DockPosition.Center);
        ui.Settle();

        await Assert.That(empty).IsTrue();
        await Assert
            .That(
                DockView.Any(
                    ui.Root,
                    node => node.Kind == NodeKind.Text && node.Text.SequenceEqual("Nothing open")
                )
            )
            .IsFalse();
    }

    [Test]
    public async Task A_dock_mounted_again_shows_what_changed_while_it_was_out()
    {
        var dock = new DockView(
            ("a", DockPosition.Left),
            ("b", DockPosition.Left),
            ("c", DockPosition.Center)
        );
        dock.Mounted = false;
        dock.Ui.Rerender();

        dock.Manager.Dock(dock["b"], DockPosition.Right);
        dock.Manager.Float(dock["a"]);
        dock.Mounted = true;
        dock.Ui.Rerender();

        await Assert.That(dock.WidthOf(dock["b"])).IsEqualTo(300f);
        await Assert.That(dock.Title("a")).IsNotNull();
    }

    [Test]
    public async Task An_unmounted_dock_leaves_nothing_of_it_on_its_manager()
    {
        var manager = DockView.Docked(("a", DockPosition.Left), ("c", DockPosition.Center));
        var window = manager.Float(manager.GetElement("a")!);
        var dock = new DockView(manager);

        dock.Mounted = false;
        dock.Ui.Rerender();
        IObservable[] sources =
        [
            manager,
            window,
            window.Group,
            manager.CenterGroup,
            manager.GetElement("a")!,
            manager.GetElement("c")!,
        ];

        await Assert.That(sources.Sum(source => source.Observers.Count)).IsEqualTo(0);
        await Assert
            .That(
                typeof(DockManager)
                    .GetField(
                        nameof(DockManager.FloatingWindowRemoved),
                        BindingFlags.NonPublic | BindingFlags.Instance
                    )!
                    .GetValue(manager)
            )
            .IsNull();
    }

    sealed class Reader : IChangeObserver
    {
        Dependencies _dependencies = new(null);

        public ref Dependencies Dependencies => ref _dependencies;

        public void OnChanged(object source, int property) { }
    }

    [Test]
    public async Task An_element_holds_none_of_a_reader_that_stopped_reading_it()
    {
        var element = new DockElement("a", "a");

        var reader = StoppedReading(element);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Assert.That(reader.IsAlive).IsFalse();
        GC.KeepAlive(element);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference StoppedReading(DockElement element)
    {
        var reader = new Reader();
        using (Observation.Track(reader))
            _ = element.Title;
        using (Observation.Track(reader)) { }
        return new WeakReference(reader);
    }

    [Test]
    public async Task A_float_made_before_the_docks_first_layout_shows()
    {
        var manager = DockView.Docked(("a", DockPosition.Left), ("c", DockPosition.Center));
        manager.Float(manager.GetElement("a")!, 100f, 100f, 300f, 200f);

        var dock = new DockView(manager);

        await Assert.That(dock.Title("a").ViewBounds).IsEqualTo(new Rect(101f, 101f, 298f, 24f));
    }

    [Test]
    public async Task Setting_a_splits_proportions_resizes_its_halves_in_the_dock()
    {
        var dock = new DockView(("c", DockPosition.Center), ("d", DockPosition.Center));
        dock.Manager.DockBySplit(dock["d"], dock["c"].Parent!, DockDirection.Right);
        dock.Ui.Settle();

        dock["c"].Parent!.SizeProportion = 0.8f;
        dock["d"].Parent!.SizeProportion = 0.2f;
        dock.Ui.Settle();

        await Assert.That(dock.WidthOf(dock["c"])).IsGreaterThan(3f * dock.WidthOf(dock["d"]));
    }

    [Test]
    public async Task A_group_split_to_the_bottom_stacks_its_halves_and_its_handle_trades_their_heights()
    {
        var dock = new DockView(("c", DockPosition.Center), ("d", DockPosition.Center));
        dock.Manager.DockBySplit(dock["d"], dock["c"].Parent!, DockDirection.Bottom);
        dock.Ui.Settle();
        var c = dock.Tab("c").ViewBounds;
        var d = dock.Tab("d").ViewBounds;
        var changes = 0;
        dock.Manager.LayoutChanged += (_, _) => changes++;

        var mouse = dock.Ui.Hold(dock.Handles(nested: true)[0]);
        mouse.MoveBy(new Vector2(0f, 60f));
        var mid = dock["c"].Parent!.SizeProportion;
        mouse.Release();

        await Assert.That(d.X).IsEqualTo(c.X);
        await Assert.That(d.Y).IsGreaterThan(c.Y);
        await Assert.That(mid).IsEqualTo(0.5f);
        await Assert
            .That(dock["c"].Parent!.SizeProportion)
            .IsGreaterThan(dock["d"].Parent!.SizeProportion);
        await Assert.That(changes).IsEqualTo(1);
        await Assert.That(dock.Resizes).IsEqualTo(1);
    }

    [Test]
    public async Task A_hovered_splits_handle_lights_muted_where_an_edges_lights_in_the_border_colour()
    {
        var dock = new DockView(
            ("a", DockPosition.Left),
            ("c", DockPosition.Center),
            ("d", DockPosition.Center)
        );
        dock.Manager.DockBySplit(dock["d"], dock["c"].Parent!, DockDirection.Right);
        dock.Ui.Settle();
        static string Fill(DockView dock, bool nested) =>
            dock
                .Ui.Draw()
                .Lines.Single(line =>
                    line.Contains("/ResizeHandle[", StringComparison.Ordinal)
                    && line.Contains(": box", StringComparison.Ordinal)
                    && line.Contains("DockSplit", StringComparison.Ordinal) == nested
                    && !line.Contains("fill #00", StringComparison.Ordinal)
                    && !line.Contains("fill none", StringComparison.Ordinal)
                )
                .Split(" fill ")[1];

        dock.Ui.Hover(dock.Handles(nested: true)[0]);
        var nested = Fill(dock, nested: true);
        dock.Ui.Hover(dock.Handles(nested: false)[0]);
        var edge = Fill(dock, nested: false);

        await Assert.That(nested).IsNotEqualTo(edge);
    }

    [Test]
    public async Task An_arrow_key_on_a_splits_handle_trades_its_groups_proportions()
    {
        var dock = new DockView(("c", DockPosition.Center), ("d", DockPosition.Center));
        dock.Manager.DockBySplit(dock["d"], dock["c"].Parent!, DockDirection.Right);
        dock.Ui.Settle();

        dock.Ui.Click(dock.Handles(nested: true)[0]);
        dock.Ui.Press(Key.Right);

        await Assert
            .That(dock["c"].Parent!.SizeProportion)
            .IsGreaterThan(dock["d"].Parent!.SizeProportion);
    }

    [Test]
    public async Task A_tab_leaving_or_joining_another_group_leaves_the_active_element_alone()
    {
        var dock = new DockView(
            ("r", DockPosition.Right),
            ("s", DockPosition.Right),
            ("c", DockPosition.Center)
        );
        dock["c"].Activate();
        dock.Ui.Settle();

        dock["s"].Hide();
        dock["r"].Hide();
        dock.Manager.Dock(dock["r"], DockPosition.Right);
        dock.Ui.Settle();

        await Assert.That(dock.Manager.ActiveElement).IsSameReferenceAs(dock["c"]);
    }

    [Test]
    public async Task Changing_a_docked_elements_content_shows_the_new_content()
    {
        var dock = new DockView(("a", DockPosition.Center));

        dock["a"].Content = "Shown";
        dock.Ui.Settle();

        await Assert.That(dock.Ui.GetText("Shown")).IsNotNull();
    }

    [Test]
    public async Task A_tools_panel_is_inset_8_and_a_documents_is_not()
    {
        var manager = new DockManager();
        var tool = new DockElement("t", "t") { Content = "Tool" };
        var document = new DockElement("d", "d", DockType.Document) { Content = "Document" };
        manager.Register(tool);
        manager.Register(document);
        manager.Dock(tool, DockPosition.Left);
        manager.Dock(document, DockPosition.Center);

        var dock = new DockView(manager);

        await Assert
            .That(dock.Ui.GetText("Tool").ViewBounds.X - dock.Strip(tool).ViewBounds.X)
            .IsEqualTo(8f);
        await Assert
            .That(dock.Ui.GetText("Document").ViewBounds.X - dock.Strip(document).ViewBounds.X)
            .IsEqualTo(0f);
    }

    [Test]
    public async Task A_source_renamed_and_edited_after_registering_shows_both_on_its_tab()
    {
        var manager = new DockManager();
        var source = new Source("s", "Before");
        manager.Dock(manager.Register(source), DockPosition.Center);
        var dock = new DockView(manager);

        source.Edit("Renamed");
        dock.Ui.Settle();
        var tab = dock.Tab("Renamed");

        await Assert
            .That(
                DockView.Any(
                    tab,
                    node => node.ViewBounds.Width == 6f && node.ViewBounds.Height == 6f
                )
            )
            .IsTrue();
    }

    [Test]
    public async Task A_hovered_closeable_tab_shows_its_close_button_which_closes_it_until_it_can_no_longer_close()
    {
        var dock = new DockView(("a", DockPosition.Center), ("b", DockPosition.Center));
        static Node? Close(DockView dock, string tab) =>
            DockView.All(dock.Tab(tab), node => node.Component == typeof(IconButton))
                is [var button]
                ? DockView.Laid(button)
                : null;
        var unhovered = Close(dock, "a");

        dock.Ui.Hover(dock.Tab("a"));
        var hovered = Close(dock, "a");
        dock["a"].CanClose = false;
        dock.Ui.Settle();
        var refused = Close(dock, "a");
        dock.Ui.Hover(dock.Tab("b"));
        dock.Ui.Click(Close(dock, "b")!);

        await Assert.That(unhovered).IsNull();
        await Assert.That(hovered).IsNotNull();
        await Assert.That(refused).IsNull();
        await Assert.That(dock["b"].State).IsEqualTo(DockState.Hidden);
    }

    [Test]
    public async Task A_floats_title_follows_its_active_tab_and_its_close_button_closes_the_window()
    {
        var dock = new DockView(
            ("a", DockPosition.Left),
            ("b", DockPosition.Left),
            ("c", DockPosition.Center)
        );
        var window = dock.Manager.Float(dock["a"], 200f, 200f, 300f, 200f);
        dock.Manager.DockToGroup(dock["b"], window.Group);
        dock.Ui.Settle();
        var titled = DockView.Any(
            dock.Title(),
            node => node.Kind == NodeKind.Text && node.Text.SequenceEqual("b")
        );

        dock["a"].Activate();
        dock["a"].Title = "Renamed";
        dock.Ui.Settle();
        var renamed = DockView.Any(
            dock.Title(),
            node => node.Kind == NodeKind.Text && node.Text.SequenceEqual("Renamed")
        );
        dock.Ui.Click(
            DockView.Laid(
                DockView.All(dock.Title(), node => node.Component == typeof(IconButton))[0]
            )
        );

        await Assert.That(titled).IsTrue();
        await Assert.That(renamed).IsTrue();
        await Assert.That(dock.Manager.FloatingWindows.Count).IsEqualTo(0);
        await Assert.That(dock["a"].State).IsEqualTo(DockState.Hidden);
    }

    [Test]
    public async Task A_floats_bottom_right_corner_resizes_it_and_reports_the_resize()
    {
        var dock = new DockView(("a", DockPosition.Left), ("c", DockPosition.Center));
        var window = dock.Manager.Float(dock["a"], 200f, 200f, 300f, 200f);
        dock.Ui.Settle();
        var corner = DockView
            .All(
                dock.Ui.Root,
                node =>
                    node.ViewBounds is { Right: 500f, Bottom: 400f, Width: > 0f and < 30f }
                    && node.ViewBounds.Height < 30f
            )
            .MinBy(node => node.ViewBounds.Width)!;

        var mouse = dock.Ui.Hold(corner);
        mouse.MoveBy(new Vector2(40f, 30f));
        mouse.Release();

        await Assert
            .That(new Vector2(window.Width, window.Height))
            .IsEqualTo(new Vector2(340f, 230f));
        await Assert.That(dock.Resizes).IsGreaterThan(0);
    }
}

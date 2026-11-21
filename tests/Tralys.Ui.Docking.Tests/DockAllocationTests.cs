using System.Numerics;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Docking.Tests;

public sealed class DockAllocationTests
{
    sealed class Wiggle(HeldPointer pointer, float by, UiDriver? other = null)
    {
        public HeldPointer Pointer { get; } = pointer;

        public float By { get; set; } = by;

        public UiDriver? Other { get; } = other;
    }

    static readonly Action<UiDriver, Wiggle> Move = static (d, w) =>
    {
        w.By = -w.By;
        w.Pointer.MoveBy(new Vector2(w.By, 0f), settle: false);
        w.Other?.Frame();
        d.Frame();
    };

    static DockView Docked() =>
        new(
            ("a", DockPosition.Left),
            ("b", DockPosition.Left),
            ("c", DockPosition.Center),
            ("d", DockPosition.Center),
            ("e", DockPosition.Right)
        );

    static HeldPointer DragTab(DockView dock, string tab, Vector2 to)
    {
        var mouse = dock.Ui.Hold(dock.Tab(tab));
        mouse.MoveBy(new Vector2(10f, 0f));
        mouse.MoveBy(to - mouse.Position);
        return mouse;
    }

    [Test]
    public async Task A_tab_dragged_over_a_root_indicator_allocates_nothing_per_move_and_docks_along_that_edge()
    {
        var dock = Docked();
        var mouse = DragTab(dock, "a", new Vector2(960f, 350f));

        var bytes = dock.Ui.WorstFrameBytes(new Wiggle(mouse, 2f), Move);
        mouse.Release();

        await Assert.That(bytes).IsEqualTo(0);
        await Assert.That(dock.Manager.RightGroup.Elements).Contains(dock["a"]);
    }

    [Test]
    public async Task A_tab_dragged_over_a_floats_centre_arrow_allocates_nothing_per_move_and_joins_the_float()
    {
        var dock = Docked();
        var window = dock.Manager.Float(dock["e"], 300f, 200f, 400f, 300f);
        dock.Ui.Settle();
        var mouse = DragTab(dock, "a", DockView.CentreOf(dock.Strip(dock["e"])));

        var bytes = dock.Ui.WorstFrameBytes(new Wiggle(mouse, 2f), Move);
        mouse.Release();

        await Assert.That(bytes).IsEqualTo(0);
        await Assert.That(dock["a"].Parent).IsSameReferenceAs(window.Group);
    }

    [Test]
    public async Task A_tab_dragged_over_another_strip_allocates_nothing_per_move_and_joins_that_strip()
    {
        var dock = Docked();
        var mouse = DragTab(dock, "a", DockView.CentreOf(dock.Tab("c")));

        var bytes = dock.Ui.WorstFrameBytes(new Wiggle(mouse, 2f), Move);
        mouse.Release();

        await Assert.That(bytes).IsEqualTo(0);
        await Assert.That(dock["a"].Parent).IsSameReferenceAs(dock["c"].Parent);
    }

    [Test]
    public async Task A_tab_of_a_torn_out_window_dragged_over_a_root_indicator_allocates_nothing_per_move()
    {
        var (dock, desktop) = DockTearOutTests.TornOut();
        var torn = await Assert.That(dock["a"].Parent).IsNotNull();
        dock.Manager.DockToGroup(dock["c"], torn);
        var window = desktop.Opened[0];
        window.Ui.Settle();
        desktop.CursorPosition = new Vector2(1060f, 400f);
        var mouse = window.Ui.Hold(DockView.Tab(window.Ui.Root, "a"));
        mouse.MoveBy(new Vector2(10f, 0f));
        mouse.MoveBy(new Vector2(0f, 60f));

        var bytes = window.Ui.WorstFrameBytes(new Wiggle(mouse, 1f, dock.Ui), Move);
        mouse.Release();

        await Assert.That(bytes).IsEqualTo(0);
        await Assert.That(dock.Manager.RightGroup.Elements).Contains(dock["a"]);
    }

    [Test]
    public async Task A_dock_resized_every_frame_allocates_nothing_keeping_its_floats_inside()
    {
        var dock = Docked();
        var window = dock.Manager.Float(dock["e"], 800f, 500f, 400f, 300f);
        dock.Ui.Settle();

        var bytes = dock.Ui.WorstFrameBytes(static d =>
        {
            d.View.Size = new Size(d.View.Size.Width == 900f ? 1000f : 900f, 700f);
            d.Frame();
        });

        await Assert.That(bytes).IsEqualTo(0);
        await Assert.That(window.X).IsLessThanOrEqualTo(600f);
    }
}

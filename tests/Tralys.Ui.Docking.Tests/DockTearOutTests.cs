using System.Numerics;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Docking.Tests;

public sealed class DockTearOutTests
{
    internal sealed class Desktop : IDockWindowHost
    {
        public Rect MainWindowBounds => new(100f, 50f, 1000f, 700f);

        public float MainWindowDensity => 1f;

        public Vector2 CursorPosition { get; set; }

        public List<Window> Opened { get; } = [];

        public IDockWindow Open(FloatingWindow window, Vector2 position, Slot content)
        {
            var opened = new Window(content) { Position = position };
            Opened.Add(opened);
            return opened;
        }
    }

    internal sealed class Window(Slot content) : IDockWindow
    {
        public UiDriver Ui { get; } =
            UiDriver.Render(ui => content.Render(ui), size: new Size(300f, 200f));

        public Vector2 Position { get; set; }

        public Vector2 Size => new(300f, 200f);

        public bool IsClosed { get; private set; }

        public void Close()
        {
            if (IsClosed)
                return;
            IsClosed = true;
            Ui.Dispose();
        }

        public void CloseFromTheDesktop() => Closed?.Invoke();

        public event Action? Closed;
    }

    sealed class KeepingHandler : IDockCloseHandler
    {
        public List<DockElement> Asked { get; } = [];

        public bool Handles(DockElement element) => true;

        public void RequestClose(IReadOnlyList<DockElement> elements) => Asked.AddRange(elements);
    }

    static void MoveTo(HeldPointer pointer, Vector2 point) =>
        pointer.MoveBy(point - pointer.Position);

    internal static (DockView Dock, Desktop Desktop) TornOut()
    {
        var desktop = new Desktop { CursorPosition = new Vector2(1700f, 300f) };
        var dock = new DockView(
            DockView.Docked(("a", DockPosition.Left), ("c", DockPosition.Center)),
            desktop
        );
        var mouse = dock.Ui.Hold(dock.Tab("a"));
        mouse.MoveBy(new Vector2(10f, 0f));
        MoveTo(mouse, new Vector2(500f, 690f));
        mouse.Release();
        return (dock, desktop);
    }

    [Test]
    public async Task A_tab_dropped_with_the_pointer_off_the_docks_window_tears_out_into_a_window_under_the_pointer()
    {
        var (dock, desktop) = TornOut();
        var window = desktop.Opened[0];

        await Assert.That(desktop.Opened.Count).IsEqualTo(1);
        await Assert.That(window.Position).IsEqualTo(new Vector2(1670f, 288f));
        await Assert.That(DockView.Title(window.Ui.Root, "a")).IsNotNull();
        await Assert
            .That(DockView.Any(dock.Ui.Root, node => node.Component == typeof(DockFloat)))
            .IsFalse();
        await Assert
            .That(new Vector2(dock["a"].Parent!.Window!.Width, dock["a"].Parent!.Window!.Height))
            .IsEqualTo(window.Size);
        await Assert.That(dock["a"].Parent!.Position).IsEqualTo(DockPosition.Floating);
    }

    [Test]
    public async Task A_torn_out_window_dragged_onto_the_docks_right_indicator_docks_its_panel_there_and_closes()
    {
        var (dock, desktop) = TornOut();
        var window = desktop.Opened[0];
        desktop.CursorPosition = new Vector2(1060f, 400f);

        var mouse = window.Ui.Hold(DockView.Title(window.Ui.Root));
        mouse.MoveBy(new Vector2(10f, 0f));
        mouse.MoveBy(new Vector2(10f, 0f));
        dock.Ui.Settle();
        var lit = dock.Previews.Count;
        mouse.Release(settle: false);
        dock.Ui.Settle();

        await Assert.That(lit).IsEqualTo(1);
        await Assert.That(dock.Manager.RightGroup.Elements).Contains(dock["a"]);
        await Assert.That(window.IsClosed).IsTrue();
        await Assert.That(dock.OverlayShown).IsFalse();
    }

    [Test]
    public async Task A_torn_out_window_dragged_by_its_title_follows_the_cursor_less_where_its_title_was_pressed()
    {
        var (_, desktop) = TornOut();
        var window = desktop.Opened[0];
        var title = DockView.Title(window.Ui.Root);
        desktop.CursorPosition = new Vector2(1800f, 600f);

        var mouse = window.Ui.Hold(title);
        var pressed = mouse.Position;
        mouse.MoveBy(new Vector2(10f, 0f));
        mouse.Release();

        await Assert.That(window.Position).IsEqualTo(new Vector2(1800f, 600f) - pressed);
    }

    [Test]
    public async Task One_tab_of_a_two_tab_float_torn_out_leaves_the_other_in_the_float()
    {
        var desktop = new Desktop { CursorPosition = new Vector2(1700f, 300f) };
        var dock = new DockView(
            DockView.Docked(
                ("a", DockPosition.Left),
                ("b", DockPosition.Left),
                ("c", DockPosition.Center)
            ),
            desktop
        );
        var window = dock.Manager.Float(dock["a"], 200f, 200f, 300f, 200f);
        dock.Manager.DockToGroup(dock["b"], window.Group);
        dock.Ui.Settle();

        var mouse = dock.Ui.Hold(dock.Tab("a"));
        mouse.MoveBy(new Vector2(10f, 0f));
        MoveTo(mouse, new Vector2(500f, 690f));
        mouse.Release();

        await Assert.That(desktop.Opened.Count).IsEqualTo(1);
        await Assert.That(window.Group.Elements).IsEquivalentTo(new[] { dock["b"] });
        await Assert.That(dock["a"].State).IsEqualTo(DockState.Floating);
        await Assert.That(dock.Tab("b")).IsNotNull();
    }

    [Test]
    public async Task A_tab_dragged_out_of_a_two_tab_torn_out_window_leaves_the_window_where_it_is_and_tears_out_alone()
    {
        var (dock, desktop) = TornOut();
        dock.Manager.DockToGroup(dock["c"], dock["a"].Parent!);
        var window = desktop.Opened[0];
        var at = window.Position;
        window.Ui.Settle();
        desktop.CursorPosition = new Vector2(1800f, 500f);

        var mouse = window.Ui.Hold(DockView.Tab(window.Ui.Root, "c"));
        mouse.MoveBy(new Vector2(10f, 0f));
        MoveTo(mouse, new Vector2(150f, 150f));
        var whileDragging = window.Position;
        mouse.Release();

        await Assert.That(whileDragging).IsEqualTo(at);
        await Assert.That(window.Position).IsEqualTo(at);
        await Assert.That(desktop.Opened.Count).IsEqualTo(2);
        await Assert.That(dock["a"].Parent!.Elements).IsEquivalentTo(new[] { dock["a"] });
        await Assert.That(dock["c"].Parent!.Elements).IsEquivalentTo(new[] { dock["c"] });
    }

    [Test]
    public async Task A_torn_out_window_closed_by_the_desktop_asks_the_handler_and_a_tab_it_keeps_returns_to_the_dock()
    {
        var (dock, desktop) = TornOut();
        var handler = new KeepingHandler();
        dock.Manager.CloseHandler = handler;

        desktop.Opened[0].CloseFromTheDesktop();
        dock.Ui.Settle();

        await Assert.That(handler.Asked).IsEquivalentTo(new[] { dock["a"] });
        await Assert.That(dock["a"].State).IsEqualTo(DockState.Floating);
        await Assert.That(dock.Title("a")).IsNotNull();
    }

    [Test]
    public async Task Unmounting_the_dock_closes_its_torn_out_windows_and_mounting_it_again_shows_them_as_floats()
    {
        var (dock, desktop) = TornOut();

        dock.Mounted = false;
        dock.Ui.Rerender();
        var closed = desktop.Opened[0].IsClosed;
        dock.Mounted = true;
        dock.Ui.Rerender();

        await Assert.That(closed).IsTrue();
        await Assert.That(dock["a"].State).IsEqualTo(DockState.Floating);
        await Assert.That(dock.Title("a")).IsNotNull();
    }

    [Test]
    public async Task A_layout_saved_while_a_panel_is_torn_out_loads_it_as_a_float_at_its_bounds()
    {
        var (dock, _) = DockTearOutTests.TornOut();
        dock["a"].Parent!.Window!.X = 40f;
        var layout = dock.Manager.SaveLayout();

        dock.Manager.LoadLayout(layout);
        dock.Ui.Settle();

        await Assert.That(dock.Title("a").ViewBounds.X).IsEqualTo(41f);
        await Assert.That(dock["a"].Parent!.Window!.Width).IsEqualTo(300f);
    }
}

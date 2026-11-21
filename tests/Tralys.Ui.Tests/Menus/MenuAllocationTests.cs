using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class MenuAllocationTests
{
    static readonly Slot Rows = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "New tab", shortcut: "Ctrl+T", id: "tab");
        ui.MenuItem(text: "Open recent", items: Recent, id: "recent");
        ui.MenuSeparator();
        ui.MenuItem(text: "Snap to grid", checkable: true, id: "snap");
    });

    static readonly Slot Recent = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Edinburgh.map", id: "edinburgh");
        ui.MenuItem(text: "Cairo.map", id: "cairo");
    });

    internal static Node? Find(Node within, string id)
    {
        foreach (var child in within.Children)
        {
            if (child.Id == id)
                return child;
            if (Find(child, id) is { } found)
                return found;
        }
        return null;
    }

    static Node? FindIn(UiDriver driver, string id)
    {
        foreach (var layer in driver.View.Layers)
        {
            if (Find(layer, id) is { } found)
                return found;
        }
        return Find(driver.Root, id);
    }

    [Test]
    public async Task A_submenu_opened_by_a_click_on_its_row_and_shut_by_escape_every_few_frames_allocates_nothing()
    {
        using var driver = UiDriver.Render(ui => ui.Menu(open: true, content: Rows)).WithIcons();
        var cycle = new Cycle(driver);

        var bytes = driver.WorstFrameBytes(
            cycle,
            static (d, c) =>
            {
                switch (c.Step++ % 8)
                {
                    case 0 when FindIn(d, "recent") is { } recent:
                        d.Click(recent, settle: false);
                        break;
                    case 4:
                        d.Press(Key.Escape, settle: false);
                        break;
                }
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(cycle.Driver.View.Layers.Length).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task A_menu_button_opened_by_down_walked_into_its_submenu_and_shut_by_escape_every_few_frames_allocates_nothing()
    {
        using var driver = UiDriver
            .Render(ui => ui.MenuButton(text: "Map", menu: Rows, id: "button"))
            .WithIcons();
        driver.Focus(driver.Get("button"));

        var bytes = driver.WorstFrameBytes(
            new Cycle(driver),
            static (d, c) =>
            {
                var step = c.Step++ % 24;
                if (step % 4 == 0)
                    d.Press(ButtonWalk[step / 4], settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    static readonly Key[] ButtonWalk =
    [
        Key.Down,
        Key.Down,
        Key.Down,
        Key.Right,
        Key.Escape,
        Key.Escape,
    ];

    [Test]
    public async Task A_menu_bars_dropdowns_walked_by_the_arrows_every_few_frames_allocate_nothing()
    {
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Menubar())
                {
                    ui.MenuItem(text: "File", items: Rows, id: "file");
                    ui.MenuItem(text: "View", items: Recent, id: "view");
                    ui.MenuItem(text: "Help", id: "help");
                }
            })
            .WithIcons();
        var cycle = new Cycle(driver) { Target = driver.Get("file") };

        var bytes = driver.WorstFrameBytes(
            cycle,
            static (d, c) =>
            {
                var step = c.Step++ % 28;
                if (step == 0)
                {
                    if (c.Target is not { } bar)
                        throw new InvalidOperationException(
                            "A bar walk needs the bar's first item."
                        );
                    d.Focus(bar, settle: false);
                }
                else if (step % 4 == 0)
                    d.Press(BarWalk[step / 4 - 1], settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    static readonly Key[] BarWalk =
    [
        Key.Down,
        Key.Right,
        Key.Right,
        Key.Left,
        Key.Left,
        Key.Escape,
    ];

    [Test]
    public async Task A_context_menus_submenu_opened_by_resting_on_its_row_and_its_row_clicked_every_few_frames_allocates_nothing()
    {
        var layout = Slot.Of(static ui =>
        {
            ui.MenuItem(text: "Arrange", items: Recent, id: "arrange");
            ui.MenuItem(text: "Put away", id: "away");
        });
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 300))
                {
                    using (ui.ContextMenu(menu: layout))
                    using (ui.Box(width: 200, height: 100, id: "area")) { }
                    using (ui.Box(width: 20, height: 20, id: "off")) { }
                }
            })
            .WithIcons();
        var cycle = new Cycle(driver) { Target = driver.Get("area"), Off = driver.Get("off") };

        var bytes = driver.WorstFrameBytes(cycle, Rest);

        await Assert.That(bytes).IsEqualTo(0L);
    }

    static void Rest(UiDriver d, Cycle c)
    {
        switch (c.Step++ % 60)
        {
            case 0:
                if (c.Target is not { } area)
                    throw new InvalidOperationException(
                        "A rest walk needs the area its menu opens on."
                    );
                d.Click(area, MouseButton.Right, settle: false);
                break;
            case 2 when FindIn(d, "arrange") is { } arrange:
                d.Hover(arrange, settle: false);
                break;
            case 40 when FindIn(d, "cairo") is { } cairo:
                d.Click(cairo, settle: false);
                break;
            case 44:
                if (c.Off is not { } off)
                    throw new InvalidOperationException("A rest walk needs a node off the menu.");
                d.Hover(off, settle: false);
                break;
        }
        d.Frame();
    }

    sealed class Cycle(UiDriver driver)
    {
        public readonly UiDriver Driver = driver;
        public int Step;
        public Node? Target;
        public Node? Off;
    }
}

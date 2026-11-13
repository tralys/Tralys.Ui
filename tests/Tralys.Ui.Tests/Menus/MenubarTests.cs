using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class MenubarTests
{
    static readonly Slot FileRows = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "New map", id: "new");
        ui.MenuItem(text: "Open recent", items: Recent, id: "recent");
        ui.MenuItem(text: "Quit", id: "quit");
    });

    static readonly Slot Recent = Slot.Of(static ui => ui.MenuItem(text: "Cairo.map", id: "cairo"));

    static readonly Slot ViewRows = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Show grid", id: "grid");
        ui.MenuItem(text: "Pinned", staysOpen: true, id: "pinned");
    });

    static UiDriver Bar(Log log) =>
        UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
                {
                    using (ui.Menubar())
                    {
                        ui.MenuItem(text: "File", items: FileRows, id: "file");
                        ui.MenuItem(text: "View", items: ViewRows, id: "view");
                        ui.MenuItem(text: "Help", onClick: Menus.Logs(log, "help"), id: "help");
                    }
                    ui.Button(text: "Below", id: "below");
                }
            })
            .WithIcons();

    [Test]
    public async Task A_press_toggles_a_headers_dropdown_and_while_one_is_open_the_pointer_switches_to_another_at_once()
    {
        var log = new Log();
        using var driver = Bar(log);
        var file = driver.Get("file");
        var view = driver.Get("view");
        var help = driver.Get("help");

        driver.Hover(file);
        var hovered = (driver.View.Layers.Length, Menus.Background(file) == Menus.Hover);
        driver.Click(file, MouseButton.Right);
        var right = driver.View.Layers.Length;
        driver.Click(file);
        var opened = (
            driver.View.Layers.Length,
            driver.Focused == Menus.Panel(driver),
            Menus.Background(file) == (Paint)Themes.Dark.Color(ColorToken.StatePressed)
        );
        driver.Hover(view, settle: false);
        driver.Frame();
        var switched = (
            driver.View.Layers.Length,
            driver.Focused == Menus.Panel(driver),
            driver.GetText("Show grid") is not null
        );
        driver.Hover(help);
        var none = (driver.View.Layers.Length, driver.Focused == help);
        driver.Click(view);
        driver.Click(view);
        var shut = driver.View.Layers.Length;
        driver.Click(help);

        await Assert.That(hovered).IsEqualTo((0, true));
        await Assert.That(right).IsEqualTo(0);
        await Assert.That(opened).IsEqualTo((1, true, true));
        await Assert.That(switched).IsEqualTo((1, true, true));
        await Assert.That(none).IsEqualTo((0, true));
        await Assert.That(shut).IsEqualTo(0);
        await Assert.That(log.ToString()).IsEqualTo("help");
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    [Test]
    public async Task A_tap_on_another_header_while_one_is_open_opens_it_and_a_tap_on_the_open_one_shuts_it()
    {
        using var driver = Bar(new Log());

        driver.Tap(driver.Get("file"));
        var file = driver.GetText("New map") is not null;
        driver.Tap(driver.Get("view"));
        var view = (driver.View.Layers.Length, driver.All<Components.Menu.Item>().Count);
        driver.Tap(driver.Get("view"));

        await Assert.That(file).IsTrue();
        await Assert.That(view).IsEqualTo((1, 5));
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    [Test]
    public async Task A_press_on_a_header_dragged_onto_a_row_clicks_it_on_release_and_a_leaf_or_a_submenus_leaf_shuts_the_dropdown()
    {
        var log = new Log();
        var rows = Slot.Of(
            log,
            static (ui, log) =>
            {
                ui.MenuItem(text: "Show grid", onClick: Menus.Logs(log, "grid"), id: "grid");
                ui.MenuItem(
                    text: "Pinned",
                    staysOpen: true,
                    onClick: Menus.Logs(log, "pinned"),
                    id: "pinned"
                );
                ui.MenuItem(text: "Open recent", items: Recent, id: "recent");
            }
        );
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Menubar())
                    ui.MenuItem(text: "View", items: rows, id: "view");
            })
            .WithIcons();

        var press = driver.Hold(driver.Get("view"));
        press.MoveTo(driver.Get("grid"));
        press.Release();
        var dragged = (log.ToString(), driver.View.Layers.Length);
        driver.Click(driver.Get("view"));
        driver.Click(driver.Get("pinned"));
        var pinned = driver.View.Layers.Length;
        driver.Click(driver.Get("recent"));
        driver.Click(driver.Get("cairo"));

        await Assert.That(dragged).IsEqualTo(("grid", 0));
        await Assert.That(pinned).IsEqualTo(1);
        await Assert.That(log.ToString()).IsEqualTo("grid pinned");
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    [Test]
    public async Task The_arrows_walk_the_bar_and_its_dropdowns_down_or_enter_opens_one_on_its_first_row_and_escape_goes_back_to_its_header()
    {
        var log = new Log();
        using var driver = Bar(log);
        var file = driver.Get("file");
        var view = driver.Get("view");
        var help = driver.Get("help");
        driver.Focus(file);

        driver.Press(Key.Right);
        var right = driver.Focused == view;
        driver.Press(Key.Right);
        driver.Press(Key.Right);
        var wrapped = driver.Focused == file;
        driver.Press(Key.Left);
        var back = driver.Focused == help;
        driver.Press(Key.Down);
        var helpDown = (driver.Focused == help, driver.View.Layers.Length, log.ToString());
        driver.Press(Key.Enter);
        var helpEnter = log.ToString();
        driver.Press(Key.Right);
        driver.Press(Key.Down);
        var opened = (driver.View.Layers.Length, driver.Focused?.Id);
        driver.Press(Key.Right);
        var next = (driver.View.Layers.Length, driver.Focused?.Id);
        driver.Press(Key.Left);
        var previous = driver.Focused?.Id;
        driver.Press(Key.Left);
        var onHelp = (driver.View.Layers.Length, driver.Focused == help);
        driver.Focus(view);
        driver.Press(Key.Enter);
        var entered = driver.Focused?.Id;
        var escaped = driver.KeyDown(Key.Escape);
        driver.KeyUp(Key.Escape);

        await Assert.That((right, wrapped, back)).IsEqualTo((true, true, true));
        await Assert.That(helpDown).IsEqualTo((true, 0, ""));
        await Assert.That(helpEnter).IsEqualTo("help");
        await Assert.That(opened).IsEqualTo((1, "new"));
        await Assert.That(next).IsEqualTo((1, "grid"));
        await Assert.That(previous).IsEqualTo("new");
        await Assert.That(onHelp).IsEqualTo((0, true));
        await Assert.That(entered).IsEqualTo("grid");
        await Assert.That(escaped).IsTrue();
        await Assert.That((driver.View.Layers.Length, driver.Focused == view)).IsEqualTo((0, true));
    }

    [Test]
    public async Task Undo_in_a_dropdown_reaches_the_field_that_had_the_focus_before_the_bar_even_after_it_switched_and_the_focus_goes_back_there()
    {
        var undone = new Counter();
        var board = new MenuBoard();
        var edit = Slot.Of(static ui =>
            ui.MenuItem(text: "Undo", onClick: ui.Command(EditCommand.Undo), id: "undo")
        );
        var file = Slot.Of(static ui => ui.MenuItem(text: "New map", id: "new"));
        using var driver = UiDriver
            .Render(ui =>
            {
                ui.Shortcut(
                    Chord.Of(Key.Z, ModifierKeys.Control),
                    Callback.Of(undone, static u => u.Value++),
                    scope: ShortcutScope.View
                );
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
                {
                    using (ui.Menubar())
                    {
                        ui.MenuItem(text: "File", items: file, id: "file");
                        ui.MenuItem(text: "Edit", items: edit, id: "edit");
                    }
                    ui.Input(text: board.Field, id: "field");
                }
            })
            .WithIcons();
        var field = driver.Get("field");
        driver.Click(field);
        driver.Type("map");

        driver.Click(driver.Get("edit"));
        var enabled = !Menus.Has(driver.Get("undo"), InputStates.Disabled);
        driver.Click(driver.Get("undo"));
        var undoneOnce = (board.Field.ToString(), Menus.Has(field, InputStates.FocusWithin));
        driver.Type("s");
        driver.Click(driver.Get("file"));
        driver.Hover(driver.Get("edit"), settle: false);
        driver.Frame();
        driver.Settle();
        driver.Click(driver.Get("undo"));

        await Assert.That(enabled).IsTrue();
        await Assert.That(undoneOnce).IsEqualTo(("", true));
        await Assert.That(board.Field.ToString()).IsEqualTo("");
        await Assert.That(undone.Value).IsEqualTo(0);
        await Assert.That(Menus.Has(field, InputStates.FocusWithin)).IsTrue();
    }
}

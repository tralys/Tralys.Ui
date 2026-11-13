using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class MenuRowTests
{
    [Test]
    public async Task A_rows_edit_command_answers_to_what_had_the_focus_before_its_menu_in_a_submenu_too_and_one_that_cant_run_is_passed_over()
    {
        var board = new MenuBoard();
        board.Command = new Command(static () => { }, static () => false);
        var rows = Slot.Of(
            board,
            static (ui, board) =>
            {
                ui.MenuItem(text: "Copy", id: "copy");
                ui.MenuItem(text: "Paste", onClick: board.Command, id: "paste");
                ui.MenuItem(text: "Undo", onClick: ui.Command(EditCommand.Undo), id: "undo");
                ui.MenuItem(text: "More", items: More, id: "more");
            }
        );
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start))
                {
                    ui.Input(text: board.Field, id: "field");
                    ui.Menu(open: board.Open, onOpenChange: board.OnOpenChange, content: rows);
                }
            })
            .WithIcons();
        driver.Click(driver.Get("field"));
        driver.Type("maps");

        board.Open = true;
        driver.Settle();
        var enabled = !Menus.Has(driver.Get("undo"), InputStates.Disabled);
        driver.Press(Key.Down);
        driver.Press(Key.Down);
        var passed = driver.Focused?.Id;
        driver.Press(Key.Up);
        var back = driver.Focused?.Id;
        driver.Press(Key.End);
        driver.Press(Key.Right);
        var inSubmenu = !Menus.Has(driver.Get("redo"), InputStates.Disabled);
        driver.Press(Key.Left);
        driver.Press(Key.Right);
        var again = !Menus.Has(driver.Get("redo"), InputStates.Disabled);
        driver.Press(Key.Left);
        driver.Press(Key.Up);
        driver.Press(Key.Enter);

        await Assert.That(enabled).IsTrue();
        await Assert.That((passed, back)).IsEqualTo(("undo", "copy"));
        await Assert.That((inSubmenu, again)).IsEqualTo((true, true));
        await Assert.That(board.Field.ToString()).IsEqualTo("");
        await Assert.That(board.Opens).IsEquivalentTo([false]);
    }

    static readonly Slot More = Slot.Of(static ui =>
        ui.MenuItem(text: "Undo again", onClick: ui.Command(EditCommand.Undo), id: "redo")
    );

    [Test]
    public async Task A_rows_edit_command_answers_to_the_field_its_menu_opened_from_inside_a_dialog()
    {
        var board = new MenuBoard();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Dialog(open: true))
                using (ui.Box(direction: Direction.Column, align: Align.Start))
                {
                    ui.Input(text: board.Field, id: "field");
                    ui.Menu(open: board.Open, onOpenChange: board.OnOpenChange, content: More);
                }
            })
            .WithIcons();
        driver.Click(driver.Get("field"));
        driver.Type("maps");

        board.Open = true;
        driver.Settle();
        driver.Press(Key.Down);

        await Assert.That(driver.Focused?.Id).IsEqualTo("redo");
        await Assert.That(Menus.Has(driver.Get("redo"), InputStates.Disabled)).IsFalse();
    }

    [Test]
    public async Task A_row_shows_its_commands_gesture_unless_it_has_a_shortcut_of_its_own_which_binds_no_key()
    {
        var clicks = new Counter();
        var rows = Slot.Of(
            clicks,
            static (ui, clicks) =>
            {
                ui.MenuItem(text: "Undo", onClick: ui.Command(EditCommand.Undo));
                ui.MenuItem(
                    text: "Redo",
                    onClick: ui.Command(EditCommand.Redo),
                    shortcut: "Ctrl+Shift+Z"
                );
                ui.MenuItem(
                    text: "Count",
                    shortcut: "Ctrl+K",
                    onClick: Callback.Of(clicks, static c => c.Value++)
                );
            }
        );
        using var driver = UiDriver.Render(ui => ui.Menu(open: true, content: rows)).WithIcons();

        driver.Press(Key.K, ModifierKeys.Control);

        await Assert.That(driver.GetText("Ctrl+Z")).IsNotNull();
        await Assert.That(driver.GetText("Ctrl+Shift+Z")).IsNotNull();
        await Assert.That(clicks.Value).IsEqualTo(0);
    }

    [Test]
    public async Task A_rows_look_mutes_its_shortcut_marks_a_check_a_danger_and_a_disabled_row_and_points_a_hand()
    {
        var rows = Slot.Of(static ui =>
        {
            ui.MenuItem(text: "Plain", shortcut: "Ctrl+P", items: More, id: "plain");
            ui.MenuItem(text: "Checked", @checked: true, id: "checked");
            ui.MenuItem(text: "Danger", destructive: true, id: "danger");
            ui.MenuItem(text: "Off", shortcut: "Ctrl+O", items: More, disabled: true, id: "off");
        });
        using var driver = UiDriver.Render(ui => ui.Menu(open: true, content: rows)).WithIcons();
        var t = Themes.Dark;
        var md = t.Dimensions(Density.Pointer).Size(ComponentSize.Md);
        Paint Color(ColorToken token) => t.Color(token);
        Paint Ink(string text) => driver.GetText(text).Inherit.Color;
        var chevron = Nodes.Glyph(driver.Get("plain"));
        var offChevron = Nodes.Glyph(driver.Get("off"));

        await Assert.That(Ink("Ctrl+P")).IsEqualTo(Color(ColorToken.FgMuted));
        await Assert.That(chevron.Bounds.Width).IsEqualTo(md.IconSize - 4f);
        await Assert.That(Menus.RootOf(chevron).Inherit.Color).IsEqualTo(Color(ColorToken.Icon));
        await Assert.That(driver.Get("plain").Interaction!.Props.Cursor).IsEqualTo(Cursors.Hand);
        await Assert.That(Menus.Background(driver.Get("checked"))).IsEqualTo(Menus.Selected);
        await Assert
            .That(driver.Get("checked").Inherit.Color)
            .IsEqualTo(Color(ColorToken.FgAccent));
        await Assert.That(Nodes.Glyph(driver.Get("checked"))).IsNotNull();
        await Assert.That(driver.Get("danger").Inherit.Color).IsEqualTo(Color(ColorToken.FgDanger));
        await Assert.That(driver.Get("off").Inherit.Color).IsEqualTo(Color(ColorToken.FgDisabled));
        await Assert.That(Ink("Ctrl+O")).IsEqualTo(Color(ColorToken.FgDisabled));
        await Assert
            .That(Menus.RootOf(offChevron).Inherit.Color)
            .IsEqualTo(Color(ColorToken.IconDisabled));
    }

    [Test]
    public async Task A_row_taken_out_or_emptied_while_its_submenu_is_open_shuts_it_and_loses_its_arrow()
    {
        var board = new MenuBoard { Picked = 0 };
        var rows = Slot.Of(
            board,
            static (ui, board) =>
            {
                ui.MenuItem(text: "First", id: "first");
                if (board.Picked < 2)
                    ui.MenuItem(
                        text: "Recent",
                        items: board.Picked == 0 ? More : default,
                        id: "recent"
                    );
            }
        );
        using var driver = UiDriver.Render(ui => ui.Menu(open: true, content: rows)).WithIcons();

        driver.Click(driver.Get("recent"));
        var opened = Menus.Panels(driver);
        board.Picked = 1;
        driver.Settle();
        var emptied = (Menus.Panels(driver), driver.View.Layers.Length);
        var arrows = driver.All<Icon>().Count;
        board.Picked = 0;
        driver.Settle();
        driver.Click(driver.Get("recent"));
        board.Picked = 2;
        driver.Settle();

        await Assert.That(opened).IsEqualTo(2);
        await Assert.That(emptied).IsEqualTo((1, 1));
        await Assert.That(arrows).IsEqualTo(0);
        await Assert.That((Menus.Panels(driver), driver.View.Layers.Length)).IsEqualTo((1, 1));
    }

    [Test]
    public async Task A_press_on_the_menus_frame_stops_at_it_and_its_header_follows_the_state_it_reads()
    {
        var presses = new Counter();
        var board = new MenuBoard { Text = "first" };
        var header = Slot.Of(board, static (ui, board) => ui.Text(board.Text, id: "header"));
        using var driver = UiDriver
            .Render(ui =>
            {
                using (
                    ui.Box(
                        width: 200,
                        height: 40,
                        onPointerDown: Handler<PointerEvent>.Of(
                            presses,
                            static (p, ref _) => p.Value++
                        )
                    )
                )
                    ui.Menu(open: true, header: header, content: More);
            })
            .WithIcons();

        driver.Click(driver.Get("header"));
        board.Text = "second";
        driver.Settle();

        await Assert.That(presses.Value).IsEqualTo(0);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
        await Assert.That(driver.GetText("second")).IsNotNull();
    }

    [Test]
    public async Task A_menu_button_given_another_menu_while_open_keeps_it_open_with_the_focus_in_it()
    {
        var first = Slot.Of(static ui => ui.MenuItem(text: "Rename", id: "rename"));
        var second = Slot.Of(static ui => ui.MenuItem(text: "Archive", id: "archive"));
        var shown = first;
        using var driver = UiDriver
            .Render(ui => ui.MenuButton(text: "Map", menu: shown, id: "button"))
            .WithIcons();

        driver.Click(driver.Get("button"));
        driver.Press(Key.Down);
        shown = second;
        driver.Rerender();
        var swapped = (driver.View.Layers.Length, driver.Focused == Menus.Panel(driver));
        driver.Press(Key.Down);

        await Assert.That(swapped).IsEqualTo((1, true));
        await Assert.That(driver.Focused?.Id).IsEqualTo("archive");
    }
}

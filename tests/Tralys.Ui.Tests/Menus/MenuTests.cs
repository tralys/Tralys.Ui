using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class MenuTests
{
    static readonly Slot Flat = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Profile", id: "profile");
        ui.MenuItem(text: "Snap to grid", checkable: true, id: "snap");
        ui.MenuSeparator();
        ui.MenuItem(text: "Export", disabled: true, id: "export");
        ui.MenuItem(text: "Delete map", id: "delete");
    });

    static readonly Slot Nested = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "New tab", id: "tab");
        ui.MenuItem(text: "Open recent", items: Recent, id: "recent");
        ui.MenuItem(text: "Share", items: Share, id: "share");
        ui.MenuItem(text: "Locked", items: Share, disabled: true, id: "locked");
        ui.MenuItem(text: "Delete map", id: "delete");
    });

    static readonly Slot Recent = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Edinburgh.map", id: "edinburgh");
        ui.MenuItem(text: "Cairo.map", id: "cairo");
        ui.MenuItem(text: "Archive", items: Archive, id: "archive");
    });

    static readonly Slot Archive = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "2025", id: "2025");
        ui.MenuItem(text: "2024", id: "2024");
        ui.MenuItem(text: "2023", disabled: true, id: "2023");
    });

    static readonly Slot Share = Slot.Of(static ui => ui.MenuItem(text: "By link", id: "link"));

    static UiDriver Button(Slot rows, Opens? opens = null) =>
        UiDriver
            .Render(ui =>
                ui.MenuButton(
                    text: "Map",
                    menu: rows,
                    onOpenChange: opens?.OnOpenChange ?? default,
                    id: "button"
                )
            )
            .WithIcons();

    [Test]
    public async Task The_arrows_step_through_the_rows_that_can_take_focus_wrapping_round_and_escape_gives_the_focus_back()
    {
        var opens = new Opens();
        using var driver = Button(Flat, opens);
        var button = driver.Get("button");
        var walked = new List<string>();

        driver.Click(button);
        var opened = driver.Focused == Menus.Panel(driver);
        foreach (var (key, modifiers) in Walk)
        {
            driver.Press(key, modifiers);
            walked.Add(driver.Focused?.Id ?? "");
        }
        var stayed = driver.View.Layers.Length;
        driver.Press(Key.Escape);
        var escaped = (driver.View.Layers.Length, driver.Focused == button);
        driver.Click(button);
        driver.Press(Key.Up);

        await Assert.That(opened).IsTrue();
        await Assert
            .That(walked)
            .IsEquivalentTo([
                "profile",
                "delete",
                "snap",
                "delete",
                "profile",
                "delete",
                "profile",
                "profile",
                "profile",
            ]);
        await Assert.That(stayed).IsEqualTo(1);
        await Assert.That(escaped).IsEqualTo((0, true));
        await Assert.That(driver.Focused?.Id).IsEqualTo("delete");
        await Assert.That(opens.Reports).IsEquivalentTo([true, false, true]);
    }

    static readonly (Key, ModifierKeys)[] Walk =
    [
        (Key.Down, ModifierKeys.None),
        (Key.Up, ModifierKeys.None),
        (Key.Up, ModifierKeys.None),
        (Key.Down, ModifierKeys.None),
        (Key.Down, ModifierKeys.None),
        (Key.End, ModifierKeys.None),
        (Key.Home, ModifierKeys.None),
        (Key.Down, ModifierKeys.Control),
        (Key.Left, ModifierKeys.None),
    ];

    [Test]
    public async Task Keys_open_a_submenu_on_its_first_row_and_left_or_escape_shut_only_that_level()
    {
        using var driver = Button(Nested);
        driver.Click(driver.Get("button"));

        driver.Press(Key.Down);
        driver.Press(Key.Down);
        driver.Press(Key.Right);
        var right = (Menus.Panels(driver), driver.Focused?.Id);
        driver.Press(Key.Down);
        driver.Press(Key.Down);
        driver.Press(Key.Enter);
        var enter = (Menus.Panels(driver), driver.Focused?.Id);
        driver.Press(Key.Escape);
        var escaped = (Menus.Panels(driver), driver.Focused?.Id);
        driver.Press(Key.Up);
        driver.Press(Key.Right);
        var leaf = (Menus.Panels(driver), driver.Focused?.Id);
        driver.Press(Key.Left);
        var left = (Menus.Panels(driver), driver.Focused?.Id);
        driver.Press(Key.Space);
        var space = (Menus.Panels(driver), driver.Focused?.Id);
        driver.Press(Key.Left);
        driver.Click(driver.Get("recent"));
        var clicked = (Menus.Panels(driver), driver.Focused?.Id);
        driver.Press(Key.Right);

        await Assert.That(right).IsEqualTo((2, "edinburgh"));
        await Assert.That(enter).IsEqualTo((3, "2025"));
        await Assert.That(escaped).IsEqualTo((2, "archive"));
        await Assert.That(leaf).IsEqualTo((2, "cairo"));
        await Assert.That(left).IsEqualTo((1, "recent"));
        await Assert.That(space).IsEqualTo((2, "edinburgh"));
        await Assert.That(clicked).IsEqualTo((2, "recent"));
        await Assert.That((Menus.Panels(driver), driver.Focused?.Id)).IsEqualTo((2, "edinburgh"));
    }

    [Test]
    public async Task A_menu_pooled_while_a_row_waited_on_its_delay_opens_submenus_again_in_the_next_view()
    {
        using (var first = Button(Nested))
        {
            first.Click(first.Get("button"));
            first.Hover(first.Get("recent"), settle: false);
            first.Frame();
        }
        using var driver = Button(Nested);
        driver.Click(driver.Get("button"));

        driver.Hover(driver.Get("recent"), settle: false);
        driver.Frame();
        driver.Frame(0.5f);

        await Assert.That(Menus.Panels(driver)).IsEqualTo(2);
    }

    [Test]
    public async Task Resting_the_pointer_focuses_a_row_at_once_and_opens_its_submenu_only_after_the_delay()
    {
        using var driver = Button(Nested);
        driver.Click(driver.Get("button"));
        var recent = driver.Get("recent");

        driver.Hover(recent, settle: false);
        driver.Frame();
        var lit = (driver.Focused == recent, Menus.Background(recent) == Menus.Hover);
        driver.Frame(0.3f);
        var early = Menus.Panels(driver);
        driver.Frame(0.2f);
        var late = (Menus.Panels(driver), driver.Focused == recent);
        driver.Hover(driver.Get("cairo"), settle: false);
        driver.Frame();
        var inside = (
            driver.Focused == driver.Get("cairo"),
            Menus.Background(recent) == Menus.Hover
        );
        driver.Hover(driver.Get("archive"), settle: false);
        driver.Frame();
        driver.Frame(0.45f);
        var nested = Menus.Panels(driver);
        driver.Hover(driver.Get("tab"), settle: false);
        driver.Frame();
        driver.Frame(0.45f);
        var other = Menus.Panels(driver);

        await Assert.That(lit).IsEqualTo((true, true));
        await Assert.That(early).IsEqualTo(1);
        await Assert.That(late).IsEqualTo((2, true));
        await Assert.That(inside).IsEqualTo((true, true));
        await Assert.That(nested).IsEqualTo(3);
        await Assert.That(other).IsEqualTo(1);
    }

    [Test]
    public async Task A_submenu_opened_by_its_key_then_shut_opens_on_a_rest_leaving_the_focus_on_its_row()
    {
        using var driver = Button(Nested);
        driver.Click(driver.Get("button"));
        var recent = driver.Get("recent");
        driver.Focus(recent);
        driver.Press(Key.Right);
        var keyed = (Menus.Panels(driver), driver.Focused == recent);
        driver.Press(Key.Left);

        driver.Hover(recent, settle: false);
        driver.Frame();
        driver.Frame(0.5f);

        await Assert.That(keyed).IsEqualTo((2, false));
        await Assert.That((Menus.Panels(driver), driver.Focused == recent)).IsEqualTo((2, true));
    }

    [Test]
    public async Task Crossing_a_row_on_the_way_or_leaving_early_opens_nothing_and_a_disabled_row_or_a_finger_takes_no_rest()
    {
        using var driver = Button(Nested);
        driver.Click(driver.Get("button"));
        var recent = driver.Get("recent");
        driver.Click(recent);

        driver.Hover(driver.Get("share"), settle: false);
        driver.Frame(0.1f);
        driver.Hover(driver.Get("edinburgh"), settle: false);
        driver.Frame(0.5f);
        driver.Settle();
        var crossed = (Menus.Panels(driver), driver.Focused?.Id);
        driver.Press(Key.Escape);
        driver.Press(Key.Escape);
        driver.Click(driver.Get("button"));
        driver.Hover(driver.Get("share"), settle: false);
        driver.Frame(0.1f);
        driver.Hover(driver.Get("button"), settle: false);
        driver.Frame(0.5f);
        driver.Settle();
        var left = Menus.Panels(driver);
        driver.Hover(driver.Get("tab"));
        driver.Hover(driver.Get("locked"), settle: false);
        driver.Frame(0.5f);
        driver.Settle();
        var locked = (
            Menus.Panels(driver),
            driver.Focused?.Id,
            Menus.Background(driver.Get("locked")) == default
        );
        var finger = driver.Hold(driver.Get("delete"), PointerKind.Touch);
        finger.MoveTo(driver.Get("share"), settle: false);
        driver.Frame(0.5f);
        driver.Settle();
        var slid = (Menus.Panels(driver), driver.Focused?.Id);

        await Assert.That(crossed).IsEqualTo((2, "edinburgh"));
        await Assert.That(left).IsEqualTo(1);
        await Assert.That(locked).IsEqualTo((1, "tab", true));
        await Assert.That(slid).IsEqualTo((1, "delete"));
    }

    [Test]
    public async Task A_click_opens_a_submenu_keeping_the_focus_on_its_row_whose_escape_shuts_it_for_good_before_the_menu()
    {
        var opens = new Opens();
        using var driver = Button(Nested, opens);
        driver.Click(driver.Get("button"));
        var recent = driver.Get("recent");

        driver.Click(recent);
        var clicked = (Menus.Panels(driver), driver.Focused == recent);
        driver.Press(Key.Escape);
        var escaped = (Menus.Panels(driver), driver.Focused == recent, driver.View.Layers.Length);
        driver.Frame(1f);
        driver.Settle();
        var rested = Menus.Panels(driver);
        driver.Press(Key.Escape);

        await Assert.That(clicked).IsEqualTo((2, true));
        await Assert.That(escaped).IsEqualTo((1, true, 1));
        await Assert.That(rested).IsEqualTo(1);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
        await Assert.That(opens.Reports).IsEquivalentTo([true, false]);
    }

    [Test]
    public async Task A_click_or_space_on_a_row_runs_it_reports_its_check_and_closes_the_menu_unless_it_stays_open()
    {
        var board = new MenuBoard();
        board.Command = new Command<string>(board.Log.Add).With("snap");
        var rows = Slot.Of(
            board,
            static (ui, board) =>
            {
                ui.MenuItem(
                    text: "Snap",
                    checkable: true,
                    @checked: board.Checked,
                    onCheckedChange: board.OnCheckedChange,
                    onClick: board.Command,
                    id: "snap"
                );
                ui.MenuItem(
                    text: "Pinned",
                    @checked: true,
                    onClick: Menus.Logs(board.Log, "pinned"),
                    id: "pinned"
                );
                ui.MenuItem(
                    text: "Keep",
                    checkable: true,
                    @checked: true,
                    staysOpen: true,
                    onCheckedChange: board.OnCheckedChange,
                    onClick: Menus.Logs(board.Log, "keep"),
                    id: "keep"
                );
                ui.MenuItem(text: "Open recent", items: Recent, id: "recent");
                ui.MenuItem(text: "Delete", onClick: Menus.Logs(board.Log, "delete"), id: "delete");
            }
        );
        using var driver = UiDriver
            .Render(ui =>
                ui.Menu(open: board.Open, onOpenChange: board.OnOpenChange, content: rows)
            )
            .WithIcons();
        board.Open = true;
        driver.Settle();

        driver.Click(driver.Get("snap"));
        var snapped = (board.Log.ToString(), board.Opens.Count, driver.View.Layers.Length);
        board.Open = true;
        driver.Settle();
        driver.Click(driver.Get("keep"));
        var kept = driver.View.Layers.Length;
        driver.Click(driver.Get("pinned"));
        board.Open = true;
        driver.Settle();
        driver.Click(driver.Get("recent"));
        driver.Click(driver.Get("delete"));
        var deleted = (driver.View.Layers.Length, Menus.Panels(driver));
        board.Open = true;
        driver.Settle();
        driver.Click(driver.Get("recent"));
        driver.Click(driver.Get("cairo"));
        board.Open = true;
        driver.Settle();
        driver.Press(Key.Home);
        driver.KeyDown(Key.Space);
        driver.KeyUp(Key.Space);

        await Assert.That(snapped).IsEqualTo(("snap", 1, 0));
        await Assert.That(kept).IsEqualTo(1);
        await Assert.That(deleted).IsEqualTo((0, 0));
        await Assert.That(board.Checks).IsEquivalentTo([true, false, true]);
        await Assert.That(board.Log.ToString()).IsEqualTo("snap keep pinned delete snap");
        await Assert.That(board.Opens).IsEquivalentTo([false, false, false, false, false]);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Closing_the_menu_any_way_shuts_every_submenu_and_it_reopens_on_its_top_level()
    {
        var board = new MenuBoard();
        var draft = new ObservableText("draft");
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
                {
                    ui.Input(text: draft, id: "field");
                    using (ui.Box(width: 120, height: 40, id: "area"))
                        ui.Menu(
                            open: board.Open,
                            onOpenChange: board.OnOpenChange,
                            content: Nested
                        );
                }
            })
            .WithIcons();
        driver.Click(driver.Get("field"));

        board.Open = true;
        driver.Settle();
        var took = driver.Focused == Menus.Panel(driver);
        driver.Click(driver.Get("recent"));
        driver.Click(driver.Get("archive"));
        var deep = driver.View.Layers.Length;
        board.Open = false;
        driver.Settle();
        var shut = driver.View.Layers.Length;
        board.Open = true;
        driver.Settle();
        var reopened = driver.View.Layers.Length;
        driver.Click(driver.Get("recent"));
        driver.Click(new Vector2(700f, 500f));
        var outside = (driver.View.Layers.Length, board.Opens.Count, board.Opens[0]);
        board.Open = true;
        driver.Settle();
        driver.Click(driver.Get("tab"));

        await Assert.That(took).IsTrue();
        await Assert.That(deep).IsEqualTo(3);
        await Assert.That((shut, reopened)).IsEqualTo((0, 1));
        await Assert.That(outside).IsEqualTo((0, 1, false));
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
        await Assert.That(Menus.Has(driver.Get("field"), InputStates.FocusWithin)).IsTrue();
    }

    [Test]
    public async Task Every_label_stays_in_line_once_any_row_has_an_icon_a_check_or_could_be_checked()
    {
        var board = new MenuBoard { Picked = -1 };
        var rows = Slot.Of(
            board,
            static (ui, board) =>
            {
                ui.MenuItem(text: "First", @checked: board.Picked == 0);
                ui.MenuItem(text: "Second", @checked: board.Picked == 1);
            }
        );
        var checkable = Slot.Of(static ui =>
        {
            ui.MenuItem(text: "Plain");
            ui.MenuItem(text: "Could be", checkable: true);
        });
        var shown = rows;
        using var driver = UiDriver.Render(ui => ui.Menu(open: true, content: shown)).WithIcons();
        float Label(string text) => driver.GetText(text).ViewBounds.X;

        var none = (Label("First"), Label("Second"));
        board.Picked = 0;
        driver.Settle();
        var first = (Label("First"), Label("Second"));
        board.Picked = 1;
        driver.Settle();
        var second = (Label("First"), Label("Second"));
        shown = checkable;
        driver.Rerender();
        var could = (Label("Plain"), Label("Could be"));

        await Assert.That(none.Item1).IsEqualTo(none.Item2);
        await Assert.That(first.Item1).IsEqualTo(first.Item2);
        await Assert.That(first.Item1).IsGreaterThan(none.Item1);
        await Assert.That(second).IsEqualTo(first);
        await Assert.That(could).IsEqualTo((first.Item1, first.Item1));
    }

    [Test]
    public async Task A_menu_names_its_layer_by_its_callers_id_and_follows_a_new_one()
    {
        var rows = Slot.Of(static ui => ui.MenuItem(text: "Only"));
        var name = "context";
        using var driver = UiDriver
            .Render(ui => ui.Menu(open: true, content: rows, id: name))
            .WithIcons();
        var named = driver.Get("context").Layer;
        name = "renamed";
        driver.Rerender();

        await Assert.That(named).IsNotNull();
        await Assert.That(driver.Get("renamed").Layer).IsNotNull();
    }

    [Test]
    public async Task A_row_outside_any_menu_shows_its_icon_column_only_for_an_icon_or_a_check()
    {
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start))
                {
                    ui.MenuItem(text: "Plain");
                    ui.MenuItem(text: "Checked", @checked: true);
                    ui.MenuItem(text: "Icon", icon: IconGlyph.Of(IconRole.Plus));
                }
            })
            .WithIcons();

        var plain = driver.GetText("Plain").ViewBounds.X;
        var checkedRow = driver.GetText("Checked").ViewBounds.X;

        await Assert.That(checkedRow).IsGreaterThan(plain);
        await Assert.That(driver.GetText("Icon").ViewBounds.X).IsEqualTo(checkedRow);
    }

    [Test]
    public async Task A_menu_opens_at_a_point_inside_its_anchor_against_its_bounds_or_at_a_view_point_and_a_submenu_beside_its_row()
    {
        var anchor = (Anchor?)null;
        var area = new NodeRef();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(padding: 40, align: Align.Start))
                using (ui.Box(width: 120, height: 40, nodeRef: area, id: "area"))
                    ui.Menu(open: true, anchor: anchor ?? default, content: Nested);
            })
            .WithIcons();
        var bounds = driver.Get("area").ViewBounds;
        Rect Placed() => Menus.Panel(driver).ViewBounds;

        var against = Placed();
        var focused = driver.Focused == Menus.Panel(driver);
        anchor = Anchor.Within(area, new Rect(30f, 20f, 0f, 0f));
        driver.Rerender();
        var within = Placed();
        anchor = Anchor.Point(new Vector2(300f, 200f));
        driver.Rerender();
        var point = Placed();
        driver.Click(driver.Get("recent"));
        var row = driver.Get("recent").ViewBounds;
        var submenu = Menus.Panel(driver, 1).ViewBounds;

        await Assert.That(focused).IsTrue();
        await Assert
            .That((against.X, against.Y))
            .IsEqualTo((bounds.X, bounds.Bottom + MenuLook.Gap));
        await Assert.That(within.X).IsEqualTo(bounds.X + 30f).Within(1f);
        await Assert.That(within.Y).IsEqualTo(bounds.Y + 20f + MenuLook.Gap).Within(1f);
        await Assert.That((point.X, point.Y)).IsEqualTo((300f, 200f + MenuLook.Gap));
        await Assert.That((submenu.X, submenu.Y)).IsEqualTo((row.Right, row.Y));
    }

    [Test]
    public async Task A_menu_is_at_least_160_wide_scrolls_its_rows_past_400_and_sizes_every_level_by_its_size()
    {
        var size = (Theming.ComponentSize?)null;
        var rows = Slot.Of(static ui =>
        {
            for (var i = 0; i < 30; i++)
                ui.MenuItem(text: "R", items: i == 0 ? Recent : default, key: i);
        });
        using var driver = UiDriver
            .Render(ui => ui.Menu(open: true, size: size, content: rows))
            .WithIcons();
        var t = Themes.Dark.Dimensions(Theming.Density.Pointer);

        var panel = Menus.Panel(driver).ViewBounds;
        var md = Menus.RootOf(driver.All<Components.Menu.Item>()[0]).ViewBounds.Height;
        size = Theming.ComponentSize.Sm;
        driver.Rerender();
        driver.Click(Menus.RootOf(driver.All<Components.Menu.Item>()[0]));
        var sm = Menus.RootOf(driver.All<Components.Menu.Item>()[0]).ViewBounds.Height;
        var inner = driver.Get("edinburgh").ViewBounds.Height;
        var scrolled = driver.Wheel(Menus.Panel(driver), new Vector2(0f, -1f));

        await Assert.That(panel.Width).IsEqualTo(MenuLook.MinWidth);
        await Assert.That(panel.Height).IsEqualTo(MenuLook.MaxRowsHeight + 10f);
        await Assert.That(md).IsEqualTo(t.Size(Theming.ComponentSize.Md).Height);
        await Assert.That((sm, inner)).IsEqualTo((t.Size(Theming.ComponentSize.Sm).Height, sm));
        await Assert.That(scrolled).IsTrue();
    }

    static readonly Slot ManyRows = Slot.Of(static ui =>
    {
        for (var i = 0; i < 30; i++)
            ui.MenuItem(text: "S", key: i);
    });

    [Test]
    public async Task A_menu_and_its_submenus_scroll_their_rows_past_the_height_its_caller_caps_them_at_short_of_a_bar_beside_them()
    {
        var rows = Slot.Of(static ui =>
        {
            for (var i = 0; i < 30; i++)
                ui.MenuItem(text: "R", items: i == 0 ? ManyRows : default, key: i);
        });
        using var driver = UiDriver
            .Render(ui => ui.Menu(open: true, maxHeight: 120, content: rows))
            .WithIcons();

        driver.Click(Menus.RootOf(driver.All<Components.Menu.Item>()[0]));
        var row = Menus.RootOf(driver.All<Components.Menu.Item>()[0]).ViewBounds;
        var bar = driver.All<Scrollbar>().Min(b => Bars.Root(b).ViewBounds.X);

        await Assert
            .That((Menus.Panel(driver).ViewBounds.Height, Menus.Panel(driver, 1).ViewBounds.Height))
            .IsEqualTo((130f, 130f));
        await Assert.That(Bars.Shown(driver)).IsEqualTo(2);
        await Assert.That(row.Right).IsEqualTo(bar);
    }
}

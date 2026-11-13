using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class Opens
{
    public readonly List<bool> Reports = [];

    public Callback<bool> OnOpenChange =>
        Callback<bool>.Of(this, static (o, open) => o.Reports.Add(open));
}

public sealed class MenuButtonTests
{
    internal static readonly Slot Rows = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Rename", id: "rename");
        ui.MenuItem(text: "Delete", id: "delete");
    });

    [Test]
    public async Task A_menu_button_is_one_stop_that_opens_on_a_click_or_down_focusing_its_menu_and_closes_for_good_on_a_press_on_it()
    {
        var opens = new Opens();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
                {
                    ui.Button(text: "Before", id: "before");
                    ui.MenuButton(
                        text: "Actions",
                        chevron: true,
                        menu: Rows,
                        onOpenChange: opens.OnOpenChange,
                        id: "menu"
                    );
                    ui.Button(text: "After", id: "after");
                }
            })
            .WithIcons();
        var menu = driver.Get("menu");
        driver.Tab();
        driver.Tab();
        var tabbed = driver.Focused == menu;
        driver.Tab();
        var tabbedPast = driver.Focused == driver.Get("after");

        driver.Click(menu);
        var clicked = (
            driver.View.Layers.Length,
            driver.Focused == Menus.Panel(driver),
            Chevron(menu),
            driver.View.Layers[0].ViewBounds.X == menu.ViewBounds.X,
            driver.View.Layers[0].ViewBounds.Y >= menu.ViewBounds.Bottom
        );
        driver.Click(menu);
        var pressedShut = (driver.View.Layers.Length, driver.Focused == menu, Chevron(menu));
        driver.Press(Key.Down);
        var downed = (driver.View.Layers.Length, driver.Focused == Menus.Panel(driver));
        driver.Press(Key.Escape);
        var escaped = (driver.View.Layers.Length, driver.Focused == menu);
        driver.Press(Key.Down, ModifierKeys.Control);
        var controlDown = driver.View.Layers.Length;
        driver.Click(menu);
        driver.Click(new Vector2(700f, 500f));

        await Assert.That((tabbed, tabbedPast)).IsEqualTo((true, true));
        await Assert.That(clicked).IsEqualTo((1, true, Transform.Scale(1f, -1f), true, true));
        await Assert.That(pressedShut).IsEqualTo((0, true, default(Transform)));
        await Assert.That(downed).IsEqualTo((1, true));
        await Assert.That(escaped).IsEqualTo((0, true));
        await Assert.That(controlDown).IsEqualTo(0);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
        await Assert.That(opens.Reports).IsEquivalentTo([true, false, true, false, true, false]);
    }

    [Test]
    public async Task A_controlled_menu_button_reports_a_click_and_shows_its_menu_only_while_open_is_true()
    {
        var opens = new Opens();
        var open = false;
        using var driver = UiDriver.Render(ui =>
            ui.MenuButton(
                text: "Actions",
                open: open,
                menu: Rows,
                onOpenChange: opens.OnOpenChange,
                id: "menu"
            )
        );

        driver.Click(driver.Get("menu"));
        var refused = driver.View.Layers.Length;
        open = true;
        driver.Rerender();

        await Assert.That(opens.Reports).IsEquivalentTo([true]);
        await Assert.That((refused, driver.View.Layers.Length)).IsEqualTo((0, 1));
    }

    [Test]
    public async Task Keys_and_presses_inside_the_menu_never_click_or_close_its_menu_button()
    {
        var opens = new Opens();
        var pins = new Counter();
        var menu = Slot.Of(
            pins,
            static (ui, pins) =>
            {
                ui.Button(
                    text: "Pin",
                    onClick: Callback.Of(pins, static p => p.Value++),
                    id: "pin"
                );
                using (ui.Box(width: 80, height: 24, focusable: true, id: "row")) { }
            }
        );
        using var driver = UiDriver.Render(ui =>
            ui.MenuButton(
                text: "Person",
                menu: menu,
                onOpenChange: opens.OnOpenChange,
                id: "person"
            )
        );

        driver.Click(driver.Get("person"));
        driver.Click(driver.Get("row"));
        driver.Press(Key.Enter);
        driver.Press(Key.Down);
        driver.Click(driver.Get("pin"));

        await Assert.That(pins.Value).IsEqualTo(1);
        await Assert.That(opens.Reports).IsEquivalentTo([true]);
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
    }

    internal static Transform Chevron(Node root) => Nodes.Glyph(root).Visual.Transform;
}

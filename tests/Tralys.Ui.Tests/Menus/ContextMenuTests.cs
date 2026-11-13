using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class ContextMenuTests
{
    static readonly Slot Rows = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Copy", id: "copy");
        ui.MenuItem(text: "Paste", id: "paste");
    });

    static Callback<bool> Reports(Log log, string what) =>
        Callback<bool>.Of(log, what, static (l, w, open) => l.Add(open ? w : "shut"));

    [Test]
    public async Task A_right_click_opens_the_menu_at_the_pointer_reporting_with_the_state_it_was_given()
    {
        var log = new Log();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(padding: 40))
                using (ui.ContextMenu(menu: Rows, onOpenChange: Reports(log, "report")))
                using (ui.Box(width: 200, height: 100, id: "area")) { }
            })
            .WithIcons();
        var area = driver.Get("area").ViewBounds;

        driver.Click(driver.Get("area"), MouseButton.Right);
        var panel = Menus.Panel(driver).ViewBounds;

        await Assert.That(log.ToString()).IsEqualTo("report");
        await Assert
            .That((panel.X, panel.Y))
            .IsEqualTo((area.X + area.Width / 2f, area.Y + area.Height / 2f));
        await Assert.That(driver.Focused).IsEqualTo(Menus.Panel(driver));
    }

    [Test]
    public async Task A_held_finger_opens_the_menu_without_clicking_what_it_held_and_one_slid_off_opens_nothing()
    {
        var log = new Log();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 40))
                {
                    using (ui.ContextMenu(menu: Rows, onOpenChange: Reports(log, "button")))
                        ui.Button(text: "Hold me", onClick: Menus.Logs(log, "click"), id: "button");
                    using (ui.ContextMenu(menu: Rows, onOpenChange: Reports(log, "area")))
                    using (ui.Box(width: 200, height: 100, id: "area")) { }
                }
            })
            .WithIcons();

        driver.LongPress(driver.Get("button"));
        var held = (log.ToString(), driver.View.Layers.Length);
        driver.Press(Key.Escape);
        var finger = driver.Hold(driver.Get("area"), PointerKind.Touch);
        driver.Frame(0.6f);
        finger.MoveBy(new Vector2(0f, 300f));
        finger.Release();

        await Assert.That(held).IsEqualTo(("button", 1));
        await Assert.That(log.ToString()).IsEqualTo("button shut");
        await Assert.That(driver.View.Layers.Length).IsEqualTo(0);
    }

    [Test]
    public async Task A_context_menu_with_no_rows_leaves_the_request_to_the_one_around_it()
    {
        var log = new Log();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.ContextMenu(menu: Rows, onOpenChange: Reports(log, "outer")))
                using (ui.Box(padding: 20))
                using (ui.ContextMenu(onOpenChange: Reports(log, "inner")))
                using (ui.Box(width: 100, height: 60, id: "inner")) { }
            })
            .WithIcons();

        driver.Click(driver.Get("inner"), MouseButton.Right);

        await Assert.That(log.ToString()).IsEqualTo("outer");
        await Assert.That(driver.View.Layers.Length).IsEqualTo(1);
    }
}

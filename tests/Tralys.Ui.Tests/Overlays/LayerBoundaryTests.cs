using System.Numerics;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class LayerBoundaryTests
{
    [Test]
    public async Task A_layer_inside_a_box_that_contains_layers_fills_it_and_a_positioner_slides_back_into_it()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    width: 200,
                    height: 100,
                    margin: new Thickness(50, 40, 0, 0),
                    alignSelf: AlignSelf.Start,
                    containsLayers: true,
                    id: "stage"
                )
            )
            {
                using (ui.Layer()) { }
                var anchor = ui.Ref();
                using (
                    ui.Box(
                        width: 20,
                        height: 20,
                        margin: new Thickness(0, 80, 0, 0),
                        nodeRef: anchor
                    )
                ) { }
                using (ui.Positioner(anchor: anchor, placement: PlacementMode.Bottom))
                using (ui.Box(width: 60, height: 50, id: "popup")) { }
            }
        });
        var stage = driver.Get("stage").ViewBounds;

        await Assert.That(driver.View.Layers[0].ViewBounds).IsEqualTo(stage);
        await Assert.That(driver.Get("popup").ViewBounds.Bottom).IsLessThanOrEqualTo(stage.Bottom);
    }

    [Test]
    public async Task A_press_outside_the_box_reaches_what_is_there_and_leaves_a_dismissable_layer_inside_it_open()
    {
        var dismissed = new Counter();
        var outside = new Counter();
        using var driver = Render(dismissed, outside, Dismiss.OutsidePress);

        var stage = driver.Get("stage").ViewBounds;

        driver.Click(driver.Get("outside"));
        var after = (outside.Value, dismissed.Value);
        driver.Click(new Vector2(stage.X + 150, stage.Y + 80));

        await Assert.That(after).IsEqualTo((1, 0));
        await Assert.That(dismissed.Value).IsEqualTo(1);
    }

    [Test]
    public async Task Escape_with_the_focus_outside_the_box_leaves_a_layer_inside_it_open()
    {
        var dismissed = new Counter();
        using var driver = Render(dismissed, new Counter(), Dismiss.Escape);

        driver.Focus(driver.Get("outside"));
        driver.Press(Key.Escape);
        var outside = dismissed.Value;
        driver.Focus(driver.Get("inside"));
        driver.Press(Key.Escape);

        await Assert.That((outside, dismissed.Value)).IsEqualTo((0, 1));
    }

    [Test]
    public async Task Tab_from_outside_the_box_stays_outside_while_a_modal_inside_it_is_active()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (ui.Box(width: 40, height: 20, focusable: true, id: "first")) { }
                using (ui.Box(width: 40, height: 20, focusable: true, id: "second")) { }
                using (ui.Box(width: 200, height: 100, containsLayers: true))
                using (ui.Layer(modal: true))
                using (ui.Box(width: 40, height: 20, focusable: true, id: "inside")) { }
            }
        });

        driver.Click(driver.Get("first"));
        driver.Tab();

        await Assert.That(driver.Focused).IsEqualTo(driver.Get("second"));
    }

    [Test]
    public async Task A_layer_inside_a_box_draws_and_hits_with_that_box_so_a_window_stacked_over_the_box_covers_it()
    {
        var fill = Color.FromRgb(90, 90, 90);
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 400, height: 300))
            {
                using (
                    ui.Box(
                        width: 200,
                        height: 100,
                        position: Position.Absolute,
                        containsLayers: true,
                        id: "panel"
                    )
                )
                using (ui.Layer(align: Align.Start))
                using (ui.Box(width: 150, height: 80, background: fill, id: "dialog")) { }
                using (
                    ui.Box(
                        width: 100,
                        height: 100,
                        position: Position.Absolute,
                        left: 100,
                        background: fill,
                        id: "window"
                    )
                ) { }
            }
        });

        var lines = driver.Draw().Lines.ToList();
        var dialog = lines.FindIndex(l => l.Contains("#dialog"));
        var window = lines.FindIndex(l => l.Contains("#window"));

        await Assert.That(dialog).IsGreaterThanOrEqualTo(0).And.IsLessThan(window);
        await Assert
            .That(driver.View.HitTest(new Vector2(120, 40)))
            .IsEqualTo(driver.Get("window"));
        await Assert.That(driver.View.HitTest(new Vector2(40, 40))).IsEqualTo(driver.Get("dialog"));
    }

    static UiDriver Render(Counter dismissed, Counter outside, Dismiss dismissOn) =>
        UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 10))
            {
                using (
                    ui.Box(
                        width: 80,
                        height: 30,
                        onClick: Callback.Of(outside, static c => c.Value++),
                        id: "outside"
                    )
                ) { }
                using (ui.Box(width: 200, height: 100, containsLayers: true, id: "stage"))
                using (
                    ui.Layer(
                        dismissOn: dismissOn,
                        onDismiss: Callback.Of(dismissed, static c => c.Value++),
                        align: Align.Start
                    )
                )
                using (ui.Box(width: 60, height: 30, focusable: true, id: "inside")) { }
            }
        });
}

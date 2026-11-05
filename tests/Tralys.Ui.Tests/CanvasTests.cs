using System.Numerics;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class CanvasTests
{
    [Test]
    public async Task A_canvas_repaints_only_on_its_state_or_a_read_while_drawing()
    {
        var model = new Model { Name = "n" };
        var tint = new Model();
        var draws = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box())
            {
                ui.Canvas(
                    state: new Gauge(model, draws, tint.Count),
                    draw: Gauge.Draw,
                    width: 20,
                    height: 20
                );
                ui.Label(model: model);
            }
        });
        var before = draws.Value;

        model.Name = "unrelated";
        driver.Frame();
        await Assert.That(draws.Value).IsEqualTo(before);

        model.Count = 3;
        driver.Frame();
        await Assert.That(draws.Value).IsEqualTo(before + 1);
        await Assert.That(driver.Stats.Renders).IsEqualTo(0);

        tint.Count = 1;
        driver.Frame();
        await Assert.That(draws.Value).IsEqualTo(before + 2);
    }

    [Test]
    public async Task A_canvas_is_hit_on_what_it_drew_and_not_across_its_bounds()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.Canvas(state: 0, draw: RingAndDisc, width: 100, height: 40, id: "canvas");
        });

        await Assert.That(HitAt(driver, 20, 20)).IsEqualTo("canvas");
        await Assert.That(HitAt(driver, 2, 2)).IsNull();
        await Assert.That(HitAt(driver, 52, 20)).IsEqualTo("canvas");
        await Assert.That(HitAt(driver, 70, 20)).IsNull();
        await Assert.That(HitAt(driver, 51, 1)).IsNull();
    }

    [Test]
    public async Task A_canvas_hit_test_that_throws_reports_the_fault_and_takes_no_hit()
    {
        var faults = new List<Exception>();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.Canvas(
                    state: 0,
                    draw: RingAndDisc,
                    hitTest: static (_, _, _) => throw new InvalidOperationException("hit"),
                    width: 100,
                    height: 40,
                    id: "canvas"
                );
        });
        driver.View.Context.OnFault = faults.Add;

        var hit = HitAt(driver, 20, 20);

        await Assert.That(hit).IsNull();
        await Assert.That(faults.Select(static f => f.Message).Distinct()).IsEquivalentTo(["hit"]);
    }

    static readonly CanvasDraw<int> RingAndDisc = static (dc, _, _) =>
    {
        var ink = Color.FromRgb(9, 9, 9);
        dc.DrawEllipse(new Rect(0, 0, 40, 40), ink, 0, default);
        dc.DrawBox(new Rect(50, 0, 40, 40), new CornerRadius(20), new Thickness(10), default, ink);
    };

    [Test]
    public async Task A_drawn_box_rounds_its_hole_by_its_radius_less_its_thicker_border_and_caps_a_radius_at_half_its_narrower_side()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.Canvas(state: 0, draw: FrameAndBar, width: 140, height: 100, id: "canvas");
        });

        await Assert.That(HitAt(driver, 3, 9)).IsNull();
        await Assert.That(HitAt(driver, 105, 9)).IsEqualTo("canvas");
    }

    static readonly CanvasDraw<int> FrameAndBar = static (dc, _, _) =>
    {
        var ink = Color.FromRgb(9, 9, 9);
        dc.DrawBox(
            new Rect(0, 0, 100, 100),
            new CornerRadius(10),
            new Thickness(2, 8, 2, 8),
            default,
            ink
        );
        dc.DrawBox(new Rect(100, 0, 40, 100), new CornerRadius(30), default, ink, default);
    };

    static string? HitAt(UiDriver driver, float x, float y) =>
        driver.View.HitTest(new Vector2(x, y))?.Id;
}

internal sealed class Counter
{
    public int Value;
}

/// <summary>Draws a bar as long as its model's count, which it reads while drawing.</summary>
internal readonly record struct Gauge(Model Model, Counter Draws, int Tint)
{
    public static readonly CanvasDraw<Gauge> Draw = static (dc, gauge, info) =>
    {
        gauge.Draws.Value++;
        dc.DrawRectangle(
            Color.FromRgb((byte)gauge.Tint, 0, 0),
            new Rect(0, 0, gauge.Model.Count, info.Size.Height)
        );
        dc.DrawLine(Vector2.Zero, new Vector2(info.Size.Width, 0), 1, Color.FromRgb(1, 1, 1));
    };
}

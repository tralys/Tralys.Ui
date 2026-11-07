using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class RepeatButtonTests
{
    static UiDriver Render(Counter clicks) =>
        UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.RepeatButton(
                    text: "More",
                    delay: 0.5f,
                    interval: 0.1f,
                    onClick: Callback.Of(clicks, static c => c.Value++),
                    id: "more"
                );
                using (ui.Box(width: 40, height: 40, focusable: true, id: "away")) { }
            }
        });

    [Test]
    public async Task A_held_press_clicks_as_it_lands_then_after_the_delay_every_interval_while_over_it()
    {
        var clicks = new Counter();
        using var driver = Render(clicks);
        var more = driver.Get("more");

        driver.Click(more);
        driver.Click(more, MouseButton.Right);
        driver.Click(more, new Vector2(1f, 1f));
        var quick = clicks.Value;
        clicks.Value = 0;
        var held = driver.Hold(more, settle: false);
        var landed = clicks.Value;
        driver.Frame(0.45f);
        var waiting = clicks.Value;
        driver.Frame(0.1f);
        driver.Frame(0.15f);
        var repeating = clicks.Value;
        held.MoveTo(driver.Get("away"), settle: false);
        driver.Frame(0.1f);
        driver.Frame(0.1f);
        var away = clicks.Value;
        held.MoveTo(more, settle: false);
        driver.Frame(0.1f);
        var back = clicks.Value;
        held.Release(settle: false);
        driver.Frame(0.1f);
        driver.Frame(0.1f);

        await Assert.That(quick).IsEqualTo(2);
        await Assert.That((landed, waiting, repeating, away, back)).IsEqualTo((1, 1, 3, 3, 4));
        await Assert.That(clicks.Value).IsEqualTo(4);
    }

    [Test]
    public async Task Space_held_clicks_on_the_buttons_own_timing_until_it_comes_up_another_key_goes_down_or_focus_leaves()
    {
        var clicks = new Counter();
        using var driver = Render(clicks);
        driver.Focus(driver.Get("more"));

        var taken = driver.KeyDown(Key.Space, settle: false);
        for (var i = 0; i < 10; i++)
        {
            driver.Frame(0.03f);
            driver.KeyDown(Key.Space, repeat: true, settle: false);
        }
        var early = clicks.Value;
        driver.Frame(0.25f);
        driver.Frame(0.15f);
        var repeating = clicks.Value;
        driver.KeyUp(Key.Space, settle: false);
        driver.Frame(0.2f);
        var released = clicks.Value;

        driver.KeyDown(Key.Space, settle: false);
        driver.KeyDown(Key.Enter, settle: false);
        driver.Frame(0.7f);
        driver.Frame(0.1f);
        driver.KeyUp(Key.Enter, settle: false);
        driver.KeyUp(Key.Space, settle: false);
        driver.Frame(0.1f);
        var entered = clicks.Value;

        driver.KeyDown(Key.Space, settle: false);
        driver.Focus(driver.Get("away"), settle: false);
        driver.Frame(0.7f);
        driver.Frame(0.1f);
        driver.KeyUp(Key.Space);

        await Assert.That(taken).IsTrue();
        await Assert.That((early, repeating, released)).IsEqualTo((1, 3, 3));
        await Assert.That(entered).IsEqualTo(5);
        await Assert.That(clicks.Value).IsEqualTo(6);
    }

    [Test]
    public async Task A_negative_delay_or_an_interval_of_zero_is_refused_where_a_delay_of_zero_is_not()
    {
        await Assert
            .That(() => UiDriver.Render(static ui => ui.RepeatButton(delay: -0.1f)))
            .Throws<ArgumentOutOfRangeException>();
        await Assert
            .That(() => UiDriver.Render(static ui => ui.RepeatButton(interval: 0f)))
            .Throws<ArgumentOutOfRangeException>();
        using var driver = UiDriver.Render(static ui =>
            ui.RepeatButton(text: "Now", delay: 0f, interval: 0.001f, id: "now")
        );
        await Assert.That(driver.Get("now")).IsNotNull();
    }
}

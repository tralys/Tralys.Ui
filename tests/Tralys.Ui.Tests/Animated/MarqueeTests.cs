using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using static Tralys.Ui.Tests.AnimatedLooks;

namespace Tralys.Ui.Tests;

public sealed class MarqueeTests
{
    [Test]
    public async Task An_overflowing_marquee_holds_then_scrolls_its_child_to_the_far_end_and_back_at_30_a_second_moving_only_the_drawing()
    {
        using var driver = Start(new Model { Count = 300 }, scale: 1f);
        var offsets = new List<float>();
        var laidOut = 0;
        for (var i = 0; i < 80; i++)
        {
            driver.Frame(0.25f);
            offsets.Add(Offset(driver));
            laidOut += driver.Stats.Layouts;
        }

        await Assert.That(offsets.Min()).IsEqualTo(0f);
        await Assert.That(offsets.Max()).IsEqualTo(200f);
        await Assert
            .That((offsets[4], offsets[9], offsets[32], offsets[37], offsets[51], offsets[65]))
            .IsEqualTo((0f, 30f, 200f, 200f, 100f, 0f));
        await Assert.That(laidOut).IsEqualTo(0);
    }

    [Test]
    public async Task A_marquee_scrolls_on_whole_device_pixels_at_the_contexts_animation_speed_and_holds_at_speed_0()
    {
        using var driver = Start(new Model { Count = 300 }, scale: 1.5f);
        var context = driver.View.Context;
        context.AnimationSpeed = 0.5f;

        driver.Frame(2.9f);
        var holding = Offset(driver);
        driver.Frame(2.1f);
        var travelled = Offset(driver);
        driver.Frame(0.07f);
        var between = Offset(driver);
        context.AnimationSpeed = 0f;
        driver.Frame(5f);

        await Assert.That(holding).IsEqualTo(0f);
        await Assert.That(travelled).IsEqualTo(30f).Within(1e-4f);
        await Assert.That(between).IsEqualTo(47f / 1.5f).Within(1e-4f);
        await Assert.That(Offset(driver)).IsEqualTo(between);
    }

    [Test]
    public async Task A_marquee_whose_overflow_changes_starts_over_with_its_hold_and_rests_at_its_start_once_it_fits()
    {
        var model = new Model { Count = 300 };
        using var driver = Start(model, scale: 1f);
        driver.Frame(4f);
        var before = Offset(driver);

        model.Count = 250;
        driver.Frame();
        driver.Frame();
        driver.Frame(1.4f);
        var held = Offset(driver);
        driver.Frame(1.1f);
        var travelled = Offset(driver);
        model.Count = 80;
        driver.Frame();
        driver.Frame();
        var fits = (Offset(driver), driver.View.NeedsFrame, driver.Get("marquee").Visual.Mask);

        await Assert.That(before).IsEqualTo(75f);
        await Assert.That(held).IsEqualTo(0f);
        await Assert.That(travelled).IsEqualTo(30f);
        await Assert.That(fits).IsEqualTo((0f, false, default(Paint)));
    }

    static UiDriver Start(Model model, float scale)
    {
        var driver = UiDriver.Render(
            ui =>
            {
                if (model.Remember)
                    using (ui.Marquee(width: 100, id: "marquee"))
                    using (ui.Box(width: model.Count, height: 10, id: "child")) { }
            },
            scale: scale
        );
        model.Remember = true;
        driver.Frame(0f);
        driver.Frame(0f);
        return driver;
    }

    static float Offset(UiDriver driver)
    {
        if (driver.Get("child").Parent is not { } track)
            throw new InvalidOperationException("The marquee's child sits in no track.");
        return -Shift(track);
    }
}

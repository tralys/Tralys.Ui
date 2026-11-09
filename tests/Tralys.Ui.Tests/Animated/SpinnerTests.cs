using Tralys.Ui.Components;
using Tralys.Ui.Testing;
using static Tralys.Ui.Tests.AnimatedLooks;

namespace Tralys.Ui.Tests;

public sealed class SpinnerTests
{
    [Test]
    public async Task A_spinner_turns_every_frame_once_a_second_without_end()
    {
        using var driver = UiDriver.Render(ui => ui.Spinner(id: "spinner")).WithIcons();
        var spinner = driver.Get("spinner");

        var start = Turn(spinner);
        driver.Frame();
        var stepped = Turned(start, spinner);
        driver.Frame(0.25f - 1f / 60f);
        var quarter = Turned(start, spinner);
        driver.Frame(10f);
        var later = Turned(start, spinner);

        await Assert.That(stepped).IsEqualTo(6f).Within(0.01f);
        await Assert.That(quarter).IsEqualTo(90f).Within(0.01f);
        await Assert.That(later).IsEqualTo(90f).Within(0.01f);
        await Assert.That(driver.View.NeedsFrame).IsTrue();
    }

    [Test]
    public async Task A_hidden_spinner_progress_sweep_and_marquee_rest_asking_for_no_frames_and_move_again_once_shown()
    {
        var model = new Model();
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, hidden: model.Remember))
                {
                    ui.Spinner(id: "spinner");
                    ui.Progress(indeterminate: true, width: 100);
                    using (ui.Marquee(width: 100))
                    using (ui.Box(width: 300, height: 10)) { }
                }
            })
            .WithIcons();
        driver.Frame(2f);
        var moving = driver.View.NeedsFrame;

        model.Remember = true;
        driver.Frame();
        driver.Frame();
        var rested = (driver.View.NeedsFrame, driver.Stats.Renders);
        model.Remember = false;
        driver.Frame();
        var shown = (driver.View.NeedsFrame, Turn(driver.Get("spinner")));

        await Assert.That(moving).IsTrue();
        await Assert.That(rested).IsEqualTo((false, 0));
        await Assert.That(shown).IsEqualTo((true, 0f));
    }

    static float Turned(float from, Node spinner) => ((Turn(spinner) - from) % 360f + 360f) % 360f;
}

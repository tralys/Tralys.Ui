using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class ClockTests
{
    static readonly TimeSpan IndiaOffset = new(5, 30, 0);

    [Test]
    public async Task A_reader_renders_again_as_the_local_hour_turns_asking_for_no_frames_before_or_once_unmounted()
    {
        var india = TimeZoneInfo.CreateCustomTimeZone("Test India", IndiaOffset, "India", "India");
        var clock = new ManualClock(new DateTimeOffset(2026, 3, 1, 10, 20, 0, IndiaOffset), india);
        var model = new Model { Remember = true };
        var probe = new ClockProbe();
        using var driver = UiDriver.Render(ui =>
        {
            if (model.Remember)
                ui.ClockFace(granularity: TimeSpan.FromHours(1), probe: probe);
        });
        driver.View.Context.Time = clock;
        driver.Settle();
        var renders = probe.Renders;

        clock.Advance(new TimeSpan(0, 0, 39, 59, 999));
        driver.Frame();
        var before = (driver.View.NeedsFrame, probe.Renders - renders);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var asks = driver.View.NeedsFrame;
        driver.Frame();
        var turned = (probe.Renders - renders, probe.Shown.Hour, driver.View.NeedsFrame);
        model.Remember = false;
        driver.Settle();
        clock.Advance(TimeSpan.FromHours(1));

        await Assert.That(before).IsEqualTo((false, 0));
        await Assert.That(asks).IsTrue();
        await Assert.That(turned).IsEqualTo((1, 11, false));
        await Assert.That(driver.View.NeedsFrame).IsFalse();
    }

    [Test]
    public async Task A_day_turns_at_local_midnight_after_the_clocks_go_forward()
    {
        var clock = new ManualClock(
            new DateTimeOffset(2026, 3, 8, 1, 0, 0, TimeSpan.FromHours(-5)),
            ManualClock.Eastern
        );
        var probe = new ClockProbe();
        using var driver = UiDriver.Render(ui =>
            ui.ClockFace(granularity: TimeSpan.FromDays(1), probe: probe)
        );
        driver.View.Context.Time = clock;
        driver.Settle();
        var renders = probe.Renders;

        clock.Advance(TimeSpan.FromHours(22) - TimeSpan.FromMilliseconds(1));
        driver.Frame();
        var before = probe.Renders - renders;
        clock.Advance(TimeSpan.FromMilliseconds(1));
        driver.Frame();

        await Assert.That(before).IsEqualTo(0);
        await Assert.That(probe.Renders - renders).IsEqualTo(1);
        await Assert
            .That(probe.Shown)
            .IsEqualTo(new DateTimeOffset(2026, 3, 9, 0, 0, 0, TimeSpan.FromHours(-4)));
    }

    [Test]
    public async Task A_minute_turns_as_the_clocks_go_back_into_the_repeated_hour()
    {
        var clock = new ManualClock(
            new DateTimeOffset(2026, 11, 1, 1, 59, 0, TimeSpan.FromHours(-4)),
            ManualClock.Eastern
        );
        var probe = new ClockProbe();
        using var driver = UiDriver.Render(ui =>
            ui.ClockFace(granularity: TimeSpan.FromMinutes(1), probe: probe)
        );
        driver.View.Context.Time = clock;
        driver.Settle();
        var renders = probe.Renders;

        clock.Advance(TimeSpan.FromMinutes(1));
        driver.Frame();

        await Assert.That(probe.Renders - renders).IsEqualTo(1);
        await Assert
            .That(probe.Shown)
            .IsEqualTo(new DateTimeOffset(2026, 11, 1, 1, 0, 0, TimeSpan.FromHours(-5)));
    }
}

public sealed class ClockProbe
{
    public DateTimeOffset Shown;

    public int Renders;
}

public readonly record struct ClockFaceProps
{
    public TimeSpan Granularity { get; init; }

    public ClockProbe? Probe { get; init; }
}

public sealed class ClockFace : Component<ClockFaceProps>
{
    protected override void Render(Ui ui, in ClockFaceProps p)
    {
        var probe = p.Probe!;
        probe.Shown = ui.Now(p.Granularity);
        probe.Renders++;
        using (ui.Box(width: 20, height: 20)) { }
    }
}

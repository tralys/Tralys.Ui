namespace Tralys.Ui.Tests;

/// <summary>A wall clock a test moves by hand, before the driver's frame that should see the time; each one-shot timer
/// fires as the clock reaches it.</summary>
internal sealed class ManualClock(DateTimeOffset start, TimeZoneInfo? zone = null) : TimeProvider
{
    readonly List<Timer> _timers = [];
    DateTimeOffset _now = start.ToUniversalTime();

    /// <summary>Five hours behind UTC, an hour less from 02:00 on March's second Sunday to 02:00 on November's
    /// first.</summary>
    public static readonly TimeZoneInfo Eastern = TimeZoneInfo.CreateCustomTimeZone(
        "Test Eastern",
        TimeSpan.FromHours(-5),
        "Test Eastern",
        "Test Standard",
        "Test Daylight",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                DateTime.MinValue.Date,
                DateTime.MaxValue.Date,
                TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
                    new DateTime(1, 1, 1, 2, 0, 0),
                    3,
                    2,
                    DayOfWeek.Sunday
                ),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
                    new DateTime(1, 1, 1, 2, 0, 0),
                    11,
                    1,
                    DayOfWeek.Sunday
                )
            ),
        ]
    );

    public override TimeZoneInfo LocalTimeZone { get; } = zone ?? TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by)
    {
        _now += by;
        for (var i = 0; i < _timers.Count; i++)
            _timers[i].FireIfDue();
    }

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period
    )
    {
        var timer = new Timer(this, callback, state);
        _timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    sealed class Timer(ManualClock clock, TimerCallback callback, object? state) : ITimer
    {
        DateTimeOffset _due = DateTimeOffset.MaxValue;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _due =
                dueTime == Timeout.InfiniteTimeSpan
                    ? DateTimeOffset.MaxValue
                    : clock._now + dueTime;
            FireIfDue();
            return true;
        }

        public void FireIfDue()
        {
            if (_due > clock._now)
                return;
            _due = DateTimeOffset.MaxValue;
            callback(state);
        }

        public void Dispose()
        {
            _due = DateTimeOffset.MaxValue;
            clock._timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
    }
}

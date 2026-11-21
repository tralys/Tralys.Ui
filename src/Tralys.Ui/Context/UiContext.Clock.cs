namespace Tralys.Ui;

public sealed partial class UiContext
{
    static readonly TimeSpan MaxWait = TimeSpan.FromDays(1);
    static readonly TimerCallback Ring = static state => (state as UiContext)?.Rang();

    TimeProvider _time = TimeProvider.System;
    ValueList<ComponentHost> _clockReaders;
    ITimer? _timer;
    long _scheduledAt = long.MaxValue;
    bool _wakeDirty;
    int _clockRang;

    /// <summary>The wall clock <see cref="Ui.Now"/> reads, in its local time zone; <see cref="TimeProvider.System"/>
    /// by default. A test swaps in one it moves by hand. A change renders every component that read it again.</summary>
    /// <exception cref="InvalidOperationException">Set from another thread.</exception>
    public TimeProvider Time
    {
        get => _time;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Dispatcher.VerifyAccess();
            if (ReferenceEquals(_time, value))
                return;
            _time = value;
            _timer?.Dispose();
            _timer = null;
            _scheduledAt = long.MaxValue;
            for (var i = _clockReaders.Count - 1; i >= 0; i--)
                Wake(_clockReaders[i]);
        }
    }

    internal bool ClockRang => Volatile.Read(ref _clockRang) != 0;

    void Rang() => Volatile.Write(ref _clockRang, 1);

    [FramePath]
    internal DateTimeOffset ReadClock(ComponentHost? host, TimeSpan granularity)
    {
        if (granularity <= TimeSpan.Zero)
            throw Faults.OutOfRange(
                nameof(granularity),
                granularity,
                "A wall-clock granularity is positive."
            );
        var now = _time.GetLocalNow();
        if (host is not null)
            host.WakeAt = Math.Min(host.WakeAt, NextCrossing(now, granularity.Ticks));
        return now;
    }

    // The UTC instant the local clock reaches the next multiple, or a change of offset jumps it to another.
    [FramePath]
    long NextCrossing(DateTimeOffset now, long granularity)
    {
        var steps = now.Ticks / granularity + 1;
        var latest = DateTimeOffset.MaxValue.Ticks - TimeSpan.TicksPerDay;
        if (steps > latest / granularity)
            return long.MaxValue;
        var next = steps * granularity;
        var due = next - now.Offset.Ticks;
        var zone = _time.LocalTimeZone;
        var then = zone.GetUtcOffset(new DateTimeOffset(due, TimeSpan.Zero));
        if (then == now.Offset)
            return due;
        var shift = OffsetShift(zone, now.UtcTicks, due, now.Offset);
        if ((shift + then.Ticks) / granularity != now.Ticks / granularity)
            return shift;
        return next - then.Ticks > now.UtcTicks ? next - then.Ticks : due;
    }

    [FramePath]
    static long OffsetShift(TimeZoneInfo zone, long from, long to, TimeSpan offset)
    {
        while (to - from > 1)
        {
            var mid = from + (to - from) / 2;
            if (zone.GetUtcOffset(new DateTimeOffset(mid, TimeSpan.Zero)) == offset)
                from = mid;
            else
                to = mid;
        }
        return to;
    }

    [FramePath]
    internal void ClockMoved(ComponentHost host)
    {
        _wakeDirty = true;
        if (host.WakeAt == long.MaxValue)
            Unwatch(host);
        else if (host.ClockSlot < 0)
        {
            host.ClockSlot = _clockReaders.Count;
            _clockReaders.Add(host);
        }
    }

    [FramePath]
    internal void Unwatch(ComponentHost host)
    {
        var slot = host.ClockSlot;
        if (slot < 0)
            return;
        var last = _clockReaders.Count - 1;
        var moved = _clockReaders[last];
        _clockReaders[slot] = moved;
        moved.ClockSlot = slot;
        _clockReaders.Truncate(last);
        host.ClockSlot = -1;
        _wakeDirty = true;
    }

    [FramePath]
    void Wake(ComponentHost host)
    {
        host.WakeAt = long.MaxValue;
        Unwatch(host);
        host.Invalidate();
    }

    // Backwards, so a reader leaving swaps in one already looked at.
    [FramePath]
    void WakeClockReaders()
    {
        _scheduledAt = long.MaxValue;
        _wakeDirty = true;
        var now = _time.GetUtcNow().UtcTicks;
        for (var i = _clockReaders.Count - 1; i >= 0; i--)
        {
            if (_clockReaders[i].WakeAt <= now)
                Wake(_clockReaders[i]);
        }
    }

    [FramePath]
    internal void ScheduleWake()
    {
        if (!_wakeDirty)
            return;
        _wakeDirty = false;
        var due = long.MaxValue;
        for (var i = 0; i < _clockReaders.Count; i++)
            due = Math.Min(due, _clockReaders[i].WakeAt);
        if (due == _scheduledAt)
            return;
        _scheduledAt = due;
        if (due == long.MaxValue)
        {
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return;
        }
        var wait = TimeSpan.FromTicks(
            Math.Clamp(due - _time.GetUtcNow().UtcTicks, 0L, MaxWait.Ticks)
        );
        if (_timer is null)
            _timer = _time.CreateTimer(Ring, this, wait, Timeout.InfiniteTimeSpan);
        else
            _timer.Change(wait, Timeout.InfiniteTimeSpan);
    }
}

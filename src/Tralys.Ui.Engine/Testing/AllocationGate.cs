using System.Runtime;

namespace Tralys.Ui.Testing;

/// <summary>Measures what a frame allocates on the calling thread once warm.</summary>
public static class AllocationGate
{
    /// <summary>The worst single-frame allocation of <paramref name="frames"/> consecutive runs, taken as the least
    /// across <paramref name="windows"/> windows: a cost every frame pays shows in every window, while a one-off
    /// runtime disturbance such as a tier-up lands in only one.</summary>
    /// <exception cref="InvalidOperationException">The process collects garbage in the background, which counts the
    /// unused rest of the calling thread's allocation context as allocated; the project running the gate must set
    /// <c>ConcurrentGarbageCollection</c> to false.</exception>
    public static long WorstFrameBytes(
        Action frame,
        int frames = 120,
        int windows = 4,
        int warmup = 4
    )
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frames);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windows);
        if (
            GCSettings.LatencyMode is GCLatencyMode.Interactive or GCLatencyMode.SustainedLowLatency
        )
            throw new InvalidOperationException(
                "This process collects garbage in the background, and a background collection counts the unused rest "
                    + "of the thread's allocation context as allocated, so a frame that allocates nothing can read as "
                    + "kilobytes. Set ConcurrentGarbageCollection to false in the project that runs the gate."
            );

        for (var i = 0; i < warmup; i++)
            frame();

        var least = long.MaxValue;
        for (var window = 0; window < windows && least > 0; window++)
        {
            var worst = 0L;
            for (var i = 0; i < frames; i++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                frame();
                worst = Math.Max(worst, GC.GetAllocatedBytesForCurrentThread() - before);
            }
            least = Math.Min(least, worst);
        }
        return least;
    }
}

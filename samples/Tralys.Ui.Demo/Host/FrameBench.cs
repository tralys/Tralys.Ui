using System.Diagnostics;

namespace Tralys.Ui.Demo;

sealed class FrameBench(int capacity)
{
    readonly FrameTiming[] _frames = new FrameTiming[capacity];
    readonly long[] _allocated = new long[capacity];
    int _count;

    [FramePath]
    public void Add(in FrameTiming frame, long allocatedBytes)
    {
        _frames[_count] = frame;
        _allocated[_count] = allocatedBytes;
        _count++;
    }

    public void Report(TextWriter output, string description, int warmup)
    {
        output.WriteLine($"{description}: {_count} frames after {warmup} warm-up");
        output.WriteLine($"{"phase", -18}{"mean", 9}{"p50", 9}{"p99", 9}{"max", 9}  (µs)");
        Row(output, "event pump", static f => f.Pump);
        Row(output, "update + layout", static f => f.Update);
        Row(output, "render + flatten", static f => f.Render);
        Row(output, "submit", static f => f.Submit);
        Row(output, "UI total", static f => f.Ui);
        Row(output, "bgfx frame", static f => f.Present);

        long draws = 0;
        uint maxDraws = 0;
        var rebuilt = 0;
        long worst = 0;
        long allocated = 0;
        for (var i = 0; i < _count; i++)
        {
            draws += _frames[i].DrawCalls;
            maxDraws = Math.Max(maxDraws, _frames[i].DrawCalls);
            rebuilt += _frames[i].Rebuilt ? 1 : 0;
            worst = Math.Max(worst, _allocated[i]);
            allocated += _allocated[i];
        }

        var last = _count > 0 ? _frames[_count - 1] : default;
        output.WriteLine(
            $"draw calls {(double)draws / Math.Max(_count, 1):0.#} a frame, {maxDraws} at most; {last.Quads} quads; the list rebuilt in {rebuilt} of {_count} frames"
        );
        if (maxDraws == 0)
            output.WriteLine(
                "no draw calls to count on the no-op renderer: name one with --renderer"
            );
        output.WriteLine($"allocated {worst} B in the worst frame, {allocated} B over the run");
    }

    void Row(TextWriter output, string phase, Func<FrameTiming, long> select)
    {
        var ticks = new long[_count];
        long sum = 0;
        for (var i = 0; i < _count; i++)
        {
            ticks[i] = select(_frames[i]);
            sum += ticks[i];
        }
        Array.Sort(ticks);

        output.WriteLine(
            $"{phase, -18}{Micros(_count > 0 ? (double)sum / _count : 0), 9:0.0}{Micros(Percentile(ticks, 50)), 9:0.0}{Micros(Percentile(ticks, 99)), 9:0.0}{Micros(_count > 0 ? ticks[^1] : 0), 9:0.0}"
        );
    }

    static long Percentile(long[] sorted, int percent) =>
        sorted.Length == 0 ? 0 : sorted[Math.Max(0, (percent * sorted.Length + 99) / 100 - 1)];

    static double Micros(double ticks) => ticks * 1_000_000.0 / Stopwatch.Frequency;
}

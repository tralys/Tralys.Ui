using System.Diagnostics;
using System.Globalization;

namespace Bench;

/// <summary>Runs the scale workloads on the core and prints each workload's median frame: <c>--runs</c>,
/// <c>--frames</c>, <c>--warmup</c> and <c>--mounts</c> set the effort.</summary>
static class Program
{
    const int RowCount = 500;
    const int ListCount = 10_000;
    const float ScrollStep = 97f;

    static int Main(string[] args)
    {
        var options = Options.Parse(args);
        Console.WriteLine(
            $"{options.Runs} runs of {options.Frames} frames after {options.Warmup} warm-up; {options.Mounts} mounts a run; .NET {Environment.Version}, {Environment.ProcessorCount} cores"
        );

        var cells = Cell.Many(DenseShape.Rows * DenseShape.Columns);
        var lines = Line.Many(RowCount);
        var items = Item.Many(ListCount);

        var dense = new DenseScene(cells);
        var rows = new RowsScene(lines);
        var list = new ListScene(items);

        if (options.Profile is { } profile)
        {
            Profile(profile, dense, rows, list, cells, lines);
            return 0;
        }

        var results = new Results();
        for (var run = 0; run < options.Runs; run++)
        {
            results.Add($"{dense.Name}: mount", Mounts(dense, options.Mounts));
            dense.Prepare();
            dense.Mount();
            results.Add(
                $"{dense.Name}: every value changes",
                Frames(dense, options, f => ChangeCells(cells, f))
            );
            results.Add(
                $"{dense.Name}: re-render, output unchanged",
                Frames(dense, options, _ => dense.Rerender())
            );
            results.Add($"{dense.Name}: idle frame", Frames(dense, options, null));
            results.Count(dense);
            dense.Unmount();

            results.Add($"{rows.Name}: mount", Mounts(rows, options.Mounts));
            rows.Prepare();
            rows.Mount();
            results.Add(
                $"{rows.Name}: every count changes",
                Frames(rows, options, _ => ChangeCounts(lines))
            );
            results.Add(
                $"{rows.Name}: every background changes",
                Frames(rows, options, f => ChangeShades(lines, f))
            );
            results.Add(
                $"{rows.Name}: every opacity changes",
                Frames(rows, options, f => ChangeOpacities(lines, f))
            );
            for (var i = 0; i < lines.Length; i++)
                lines[i].Opacity = 1f;
            results.Add($"{rows.Name}: idle frame", Frames(rows, options, null));
            results.Count(rows);
            rows.Unmount();

            results.Add($"{list.Name}: mount", Mounts(list, options.Mounts));
            list.Prepare();
            list.Mount();
            var down = true;
            results.Add(
                $"{list.Name}: scroll {ScrollStep} a frame",
                Frames(
                    list,
                    options,
                    _ =>
                    {
                        var at = list.ScrollOffset;
                        if (at >= list.ScrollMax - 0.5f)
                            down = false;
                        else if (at <= 0.5f)
                            down = true;
                        list.ScrollTo(at + (down ? ScrollStep : -ScrollStep));
                    }
                )
            );
            results.Add($"{list.Name}: idle frame", Frames(list, options, null));
            results.Count(list);
            list.Unmount();
        }

        results.Print(RowCount);
        return 0;
    }

    static void Profile(
        string profile,
        Scene dense,
        Scene rows,
        Scene list,
        Cell[] cells,
        Line[] lines
    )
    {
        var (scene, change) = profile switch
        {
            "dense" => (dense, (Action<Scene, int>)((_, f) => ChangeCells(cells, f))),
            "dense-rerender" => (dense, (s, _) => s.Rerender()),
            "rows-count" => (rows, (_, _) => ChangeCounts(lines)),
            "rows-background" => (rows, (_, f) => ChangeShades(lines, f)),
            "rows-opacity" => (rows, (_, f) => ChangeOpacities(lines, f)),
            "list" => (
                list,
                (Action<Scene, int>)(
                    (s, _) =>
                        s.ScrollTo(
                            s.ScrollOffset >= s.ScrollMax - 0.5f ? 0f : s.ScrollOffset + ScrollStep
                        )
                )
            ),
            "dense-mount" => (dense, null),
            "rows-mount" => (rows, null),
            "list-mount" => (list, null),
            _ => throw new ArgumentException($"Unknown profile {profile}."),
        };
        var until = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 10;
        if (change is null)
        {
            while (Stopwatch.GetTimestamp() < until)
            {
                scene.Prepare();
                scene.Mount();
                scene.Unmount();
            }
            return;
        }
        scene.Prepare();
        scene.Mount();
        for (var f = 0; Stopwatch.GetTimestamp() < until; f++)
        {
            change(scene, f);
            scene.Frame();
        }
    }

    static void ChangeCells(Cell[] cells, int frame)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            cell.Count++;
            cell.Fill = (i + frame) % 100;
        }
    }

    static void ChangeCounts(Line[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
            lines[i].Count++;
    }

    static void ChangeShades(Line[] lines, int frame)
    {
        var shade = frame % 2 == 0 ? Shade.Odd : Shade.Even;
        for (var i = 0; i < lines.Length; i++)
            lines[i].Shade = shade;
    }

    static void ChangeOpacities(Line[] lines, int frame)
    {
        var opacity = frame % 2 == 0 ? 0.6f : 0.9f;
        for (var i = 0; i < lines.Length; i++)
            lines[i].Opacity = opacity;
    }

    static Sample Mounts(Scene scene, int mounts)
    {
        var ticks = new long[mounts];
        long worst = 0;
        for (var i = -2; i < mounts; i++)
        {
            scene.Prepare();
            Collect();
            var bytes = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            scene.Mount();
            var took = Stopwatch.GetTimestamp() - start;
            bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            scene.Unmount();
            if (i < 0)
                continue;
            ticks[i] = took;
            worst = Math.Max(worst, bytes);
        }
        return new Sample(Median(ticks), worst);
    }

    static Sample Frames(Scene scene, Options options, Action<int>? change)
    {
        for (var i = 0; i < options.Warmup; i++)
        {
            change?.Invoke(i);
            scene.Frame();
        }
        Collect();
        var ticks = new long[options.Frames];
        long worst = 0;
        for (var i = 0; i < options.Frames; i++)
        {
            var bytes = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            change?.Invoke(options.Warmup + i);
            scene.Frame();
            ticks[i] = Stopwatch.GetTimestamp() - start;
            worst = Math.Max(worst, GC.GetAllocatedBytesForCurrentThread() - bytes);
        }
        return new Sample(Median(ticks), worst);
    }

    static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    internal static double Median(long[] ticks)
    {
        var sorted = (long[])ticks.Clone();
        Array.Sort(sorted);
        var mid = sorted.Length / 2;
        var median = sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
        return median * 1_000_000.0 / Stopwatch.Frequency;
    }

    internal static double Median(List<double> values)
    {
        var sorted = values.ToArray();
        Array.Sort(sorted);
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    readonly record struct Options(int Runs, int Frames, int Warmup, int Mounts, string? Profile)
    {
        public static Options Parse(string[] args)
        {
            var options = new Options(7, 200, 120, 12, null);
            for (var i = 0; i + 1 < args.Length; i += 2)
            {
                if (args[i] == "--profile")
                {
                    options = options with { Profile = args[i + 1] };
                    continue;
                }
                var value = int.Parse(args[i + 1], CultureInfo.InvariantCulture);
                options = args[i] switch
                {
                    "--runs" => options with { Runs = value },
                    "--frames" => options with { Frames = value },
                    "--warmup" => options with { Warmup = value },
                    "--mounts" => options with { Mounts = value },
                    _ => throw new ArgumentException($"Unknown option {args[i]}."),
                };
            }
            return options;
        }
    }
}

readonly record struct Sample(double Micros, long WorstBytes);

sealed class Results
{
    readonly Dictionary<string, List<Sample>> _samples = [];
    readonly List<string> _order = [];
    readonly Dictionary<string, int> _counts = [];

    public void Add(string row, Sample sample)
    {
        if (!_samples.TryGetValue(row, out var list))
        {
            _order.Add(row);
            _samples[row] = list = [];
        }
        list.Add(sample);
    }

    public void Count(Scene scene) => _counts[scene.Name] = scene.Count;

    (double Median, double Low, double High, long Bytes) Of(string row)
    {
        if (!_samples.TryGetValue(row, out var list))
            return (double.NaN, double.NaN, double.NaN, -1);
        var micros = list.Select(s => s.Micros).ToList();
        return (Program.Median(micros), micros.Min(), micros.Max(), list.Max(s => s.WorstBytes));
    }

    public void Print(int rowCount)
    {
        Console.WriteLine();
        foreach (var (workload, count) in _counts.OrderBy(c => c.Key))
            Console.WriteLine($"{workload}: {count} nodes laid out");
        Console.WriteLine();
        Console.WriteLine("| Workload | µs (min–max of runs) | Worst frame bytes |");
        Console.WriteLine("|---|---|---|");
        foreach (var row in _order)
        {
            var now = Of(row);
            Console.WriteLine($"| {row} | {Cell(now)} | {Bytes(now.Bytes)} |");
        }

        Console.WriteLine();
        PerRow("rows: every count changes", rowCount);
        PerRow("rows: every background changes", rowCount);
        PerRow("rows: every opacity changes", rowCount);
    }

    void PerRow(string row, int rowCount)
    {
        const string Idle = "rows: idle frame";
        var now = (Of(row).Median - Of(Idle).Median) / rowCount;
        Console.WriteLine($"{row}, per row over an idle frame: {now:0.000} µs");
    }

    static string Cell((double Median, double Low, double High, long Bytes) s) =>
        double.IsNaN(s.Median) ? "—" : $"{s.Median:0.0} ({s.Low:0.0}–{s.High:0.0})";

    static string Bytes(long bytes) =>
        bytes < 0 ? "—" : bytes.ToString(CultureInfo.InvariantCulture);
}

using System.Diagnostics;

namespace Tralys.Ui.Demo;

static class Program
{
    // Long enough for each of the workload's recycled feed rows to have laid out every sample line.
    const int BenchWarmup = 600;

    // Without vsync the frames above take a fraction of a second, before a screen's delayed animations begin.
    static readonly TimeSpan BenchWarmupTime = TimeSpan.FromSeconds(2);

    static int Main(string[] args)
    {
        if (!DemoOptions.TryParse(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(DemoOptions.Usage);
            return 2;
        }

        if (options.Help)
        {
            Console.WriteLine(DemoOptions.Usage);
            return 0;
        }

        try
        {
            if (options.CaptureStories is { } directory)
                return options is { Pixels: true, Renderer: { } renderer }
                    ? StoryPixels.Run(
                        directory,
                        options.Story,
                        renderer,
                        options.Theme,
                        options.Density
                    )
                    : StoryCapture.Run(directory, options.Story, options.Theme, options.Density);

            using var host = DemoHost.Start(options);
            Console.WriteLine(host.Description);
            if (options.Bench)
                Bench(host, options.Frames);
            else
                Run(host, options.Frames);
            if (options.Screenshot is { } screenshot)
                host.Screenshot(screenshot);
            return 0;
        }
        catch (DemoException e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }

    static void Run(DemoHost host, int frames)
    {
        for (var i = 0; frames == 0 || i < frames; i++)
        {
            if (!host.Frame())
                return;
        }
    }

    static void Bench(DemoHost host, int frames)
    {
        var warmup = 0;
        var started = Stopwatch.GetTimestamp();
        for (
            ;
            warmup < BenchWarmup || Stopwatch.GetElapsedTime(started) < BenchWarmupTime;
            warmup++
        )
        {
            if (!host.Frame())
                return;
        }

        var bench = new FrameBench(frames);
        for (var i = 0; i < frames; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            if (!host.Frame())
                break;
            bench.Add(host.LastFrame, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        bench.Report(Console.Out, host.Description, warmup);
    }
}

sealed class DemoException(string message) : Exception(message);

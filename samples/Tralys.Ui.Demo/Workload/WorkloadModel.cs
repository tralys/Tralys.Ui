using System.Globalization;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Demo.Workload;

/// <summary>A labelled reading between 0 and 1, drawn as a bar.</summary>
sealed partial class Meter : ObservableObject
{
    public Meter(string label) => Label = label;

    public string Label { get; }

    [ObservableProperty]
    public partial float Value { get; set; }
}

/// <summary>One line of the activity feed. A full feed refills the row that leaves its top as the one that
/// arrives.</summary>
sealed partial class FeedRow : ObservableObject
{
    static int _made;

    public static readonly KeyOf<FeedRow> ById = static row => row.Id;

    public FeedRow() => Text = "";

    public int Id { get; } = Interlocked.Increment(ref _made);

    [ObservableProperty]
    public partial string Text { get; private set; }

    [FramePath]
    public void Fill(string text) => Text = text;
}

/// <summary>The feed's rows, at most <see cref="Capacity"/>, appended one at a time as one Add, the oldest leaving as
/// one Remove and refilled as the newest.</summary>
sealed class ActivityFeed
{
    public const int Capacity = 60;

    static readonly string[] Samples =
    [
        "Build 1432 passed in 4m 12s",
        "Deploy to staging started",
        "Ada Lovelace commented on Layout rounding",
        "Nightly backup finished: 18.4 GB",
        "Cache hit rate fell below 80%",
        "Grace Hopper merged Text shaping fixes",
        "Deploy to staging finished",
        "Alan Turing opened Focus ring on touch",
        "Disk usage on db-2 reached 71%",
        "Release 0.4.0 tagged",
        "Katherine Johnson reviewed Grid star sizing",
        "Scheduled maintenance in 2 hours",
    ];

    int _next;

    public ActivityFeed()
    {
        while (Rows.Count < Capacity)
            Append();
    }

    public ObservableList<FeedRow> Rows { get; } = new(Capacity);

    /// <summary>Appends the next sample line.</summary>
    [FramePath]
    public void Append()
    {
        var text = Samples[_next++ % Samples.Length];
        if (Rows.Count < Capacity)
        {
            var added = new FeedRow();
            added.Fill(text);
            Rows.Add(added);
            return;
        }

        var row = Rows[0];
        Rows.RemoveAt(0);
        row.Fill(text);
        Rows.Add(row);
    }
}

/// <summary>A numbered button that counts the clicks it took.</summary>
sealed class WorkloadAction
{
    public WorkloadAction(int number)
    {
        Number = number;
        Label = number.ToString(CultureInfo.InvariantCulture);
        Run = Callback.Of(this, static a => a.Clicks++);
    }

    public int Number { get; }

    public string Label { get; }

    public int Clicks { get; private set; }

    public Callback Run { get; }
}

sealed partial class FrameStats : ObservableObject
{
    // A fixed width: a slow frame must not lengthen the line, which would grow its text buffers.
    const int MillisecondsWidth = 7;
    const double LongestMilliseconds = 9999.99;

    public FrameStats() => Renderer = "";

    [ObservableProperty]
    public partial string Renderer { get; set; }

    [ObservableProperty]
    public partial int PixelWidth { get; set; }

    [ObservableProperty]
    public partial int PixelHeight { get; set; }

    [ObservableProperty]
    public partial float Scale { get; set; }

    [ObservableProperty]
    public partial double UiMilliseconds { get; set; }

    [ObservableProperty]
    public partial uint DrawCalls { get; set; }

    [FramePath]
    public static ReadOnlySpan<char> Milliseconds(double milliseconds, Span<char> into)
    {
        Math.Min(milliseconds, LongestMilliseconds)
            .TryFormat(into, out var written, "0.00", CultureInfo.InvariantCulture);
        var pad = MillisecondsWidth - written;
        if (pad > 0)
        {
            into[..written].CopyTo(into[pad..]);
            into[..pad].Fill(' ');
        }
        return into[..Math.Max(written, MillisecondsWidth)];
    }
}

sealed class WorkloadModel
{
    public const int ActionCount = 6;

    const int FeedEvery = 2;

    int _frame;

    public WorkloadModel()
    {
        var actions = new WorkloadAction[ActionCount];
        for (var i = 0; i < actions.Length; i++)
            actions[i] = new WorkloadAction(i + 1);
        Actions = actions;
    }

    public Meter[] Meters { get; } = [new("CPU"), new("Memory"), new("Network"), new("Disk")];

    public ActivityFeed Feed { get; } = new();

    public WorkloadAction[] Actions { get; }

    public FrameStats Stats { get; } = new();

    [FramePath]
    public void Advance(double seconds)
    {
        var meters = Meters;
        for (var i = 0; i < meters.Length; i++)
            meters[i].Value = (float)(0.5 + 0.45 * Math.Sin(seconds * (0.3 + 0.2 * i) + i));

        if (_frame % FeedEvery == 0)
            Feed.Append();
        _frame++;
    }
}

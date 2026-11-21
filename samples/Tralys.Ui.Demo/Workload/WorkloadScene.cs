using System.Diagnostics;
using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Demo.Workload;

sealed class WorkloadScene : IScene
{
    public WorkloadModel Model { get; } = new();

    public string Title(UiContext context) => "Tralys.Ui workload";

    public void Mount(UiView view) =>
        view.Mount(
            Slot.Of(Model, static (ui, model) => ui.WorkloadView(model: model, grow: 1, basis: 0))
        );

    [FramePath]
    public void Frame(UiView view, string renderer, in FrameTiming last)
    {
        Model.Advance(view.Context.Now.TotalSeconds);
        var stats = Model.Stats;
        stats.Renderer = renderer;
        stats.PixelWidth = view.PixelWidth;
        stats.PixelHeight = view.PixelHeight;
        stats.Scale = view.Scale;
        stats.UiMilliseconds = last.Ui * 1000.0 / Stopwatch.Frequency;
        stats.DrawCalls = last.DrawCalls;
    }

    public bool Shortcut(UiView view, Key key) => false;

    public void Dispose() { }
}

readonly record struct WorkloadViewProps
{
    public required WorkloadModel Model { get; init; }
}

sealed class WorkloadView : Component<WorkloadViewProps>
{
    const float FeedWidth = 420f;

    protected override void Render(Ui ui, in WorkloadViewProps p)
    {
        var model = p.Model;
        var weight = ui.Theme.Weight(WeightToken.Interface);
        using (ui.Box(direction: Direction.Column, padding: 24))
        {
            using (ui.Box(grow: 1, basis: 0, minHeight: 0))
            {
                using (ui.Box(direction: Direction.Column, width: FeedWidth))
                {
                    ui.Text("Activity", variant: TextVariant.Heading, fontWeight: weight);
                    using (
                        ui.Box(
                            grow: 1,
                            basis: 0,
                            minHeight: 0,
                            margin: new Thickness(0f, 8f, 0f, 16f)
                        )
                    )
                        ui.WorkloadFeed(feed: model.Feed, grow: 1);
                }
                using (
                    ui.Box(
                        direction: Direction.Column,
                        grow: 1,
                        margin: new Thickness(24f, 0f, 0f, 0f)
                    )
                )
                {
                    ui.Text("Usage", variant: TextVariant.Heading, fontWeight: weight);
                    using (
                        ui.Box(direction: Direction.Column, margin: new Thickness(0f, 8f, 0f, 16f))
                    )
                    {
                        var meters = model.Meters;
                        for (var i = 0; i < meters.Length; i++)
                            ui.WorkloadMeter(
                                meter: meters[i],
                                margin: new Thickness(0f, 0f, 0f, 16f),
                                key: i
                            );
                    }
                }
            }
            using (
                ui.Box(gap: 16, alignSelf: AlignSelf.Start, margin: new Thickness(192f, 0f, 0f, 0f))
            )
            {
                var actions = model.Actions;
                for (var i = 0; i < actions.Length; i++)
                    ui.Button(text: actions[i].Label, width: 48, onClick: actions[i].Run, key: i);
            }
            ui.WorkloadStatsLine(
                stats: model.Stats,
                position: Position.Absolute,
                top: 16,
                right: 16
            );
        }
    }
}

readonly record struct WorkloadFeedProps
{
    public required ActivityFeed Feed { get; init; }
}

sealed class WorkloadFeed : Component<WorkloadFeedProps>
{
    static readonly Slot<FeedRow> Line = Slot<FeedRow>.Of(
        static (ui, row, _) => ui.WorkloadFeedLine(entry: row)
    );

    protected override void Render(Ui ui, in WorkloadFeedProps p)
    {
        var t = ui.Theme;
        var scroll = ui.ScrollHandle();
        var bar = ui.Remember(scroll, static s => new FeedBar(s));
        var extent = scroll.Extent.Height;
        var viewport = scroll.Viewport.Height;
        using (
            ui.Box(
                background: t.Color(ColorToken.BgSunken),
                radius: t.Radius(RadiusToken.Lg),
                overflow: Overflow.Clip
            )
        )
        {
            ui.VirtualList(
                items: p.Feed.Rows,
                itemKey: FeedRow.ById,
                row: Line,
                scroll: scroll,
                grow: 1
            );
            ui.Scrollbar(
                value: scroll.Offset.Y,
                onValueChange: bar.Moved,
                max: MathF.Max(0f, extent - viewport),
                viewportSize: viewport,
                lineStep: FeedBar.LineStep,
                pageStep: viewport,
                orientation: Orientation.Vertical
            );
        }
    }
}

[FramePath]
sealed class FeedBar
{
    public const float LineStep = 16f;

    public readonly Callback<ScrollbarChange> Moved;
    public readonly ScrollHandle Handle;

    public FeedBar(ScrollHandle handle)
    {
        Handle = handle;
        Moved = Callback<ScrollbarChange>.Of(this, static (b, change) => b.Move(change));
    }

    void Move(ScrollbarChange change)
    {
        var by = change.Step switch
        {
            ScrollbarStep.LineDecrease => -LineStep,
            ScrollbarStep.LineIncrease => LineStep,
            ScrollbarStep.PageDecrease => -Handle.Viewport.Height,
            ScrollbarStep.PageIncrease => Handle.Viewport.Height,
            _ => 0f,
        };
        if (by == 0f)
            Handle.ScrollTo(null, change.Value);
        else
            Handle.ScrollBy(new System.Numerics.Vector2(0f, by));
    }
}

readonly record struct WorkloadFeedLineProps
{
    public required FeedRow Entry { get; init; }
}

sealed class WorkloadFeedLine : Component<WorkloadFeedLineProps>
{
    protected override void Render(Ui ui, in WorkloadFeedLineProps p)
    {
        using (ui.Box(padding: new Thickness(4f, 0f, 4f, 0f)))
            ui.Text(p.Entry.Text, fontWeight: ui.Theme.Weight(WeightToken.Interface), wrap: true);
    }
}

readonly record struct WorkloadMeterProps
{
    public required Meter Meter { get; init; }
}

sealed class WorkloadMeter : Component<WorkloadMeterProps>
{
    protected override void Render(Ui ui, in WorkloadMeterProps p)
    {
        var t = ui.Theme;
        using (ui.Box(direction: Direction.Column, gap: 16))
        {
            ui.Text(
                p.Meter.Label,
                variant: TextVariant.Caption,
                color: t.Color(ColorToken.FgMuted),
                fontWeight: t.Weight(WeightToken.Interface)
            );
            ui.Progress(value: p.Meter.Value, max: 1f);
        }
    }
}

readonly record struct WorkloadStatsLineProps
{
    public required FrameStats Stats { get; init; }
}

sealed class WorkloadStatsLine : Component<WorkloadStatsLineProps>
{
    protected override void Render(Ui ui, in WorkloadStatsLineProps p)
    {
        var s = p.Stats;
        var t = ui.Theme;
        Span<char> milliseconds = stackalloc char[24];
        ui.Text(
            ui.Format(
                $"{s.Renderer} · {s.PixelWidth}×{s.PixelHeight} at {s.Scale:0.##}× · UI {FrameStats.Milliseconds(s.UiMilliseconds, milliseconds)} ms · {s.DrawCalls} draws"
            ),
            variant: TextVariant.Caption,
            color: t.Color(ColorToken.FgMuted),
            fontWeight: t.Weight(WeightToken.Interface)
        );
    }
}

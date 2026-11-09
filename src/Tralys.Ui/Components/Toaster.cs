namespace Tralys.Ui.Components;

public readonly record struct ToasterProps
{
    /// <summary>Where the toasts stack in the view, 16 in from its edges: the side they keep to and where along it;
    /// <see cref="PlacementMode.Top"/>, the top centre, by default. <see cref="PlacementMode.Pointer"/> and
    /// <see cref="PlacementMode.Absolute"/> place nothing and fault its render.</summary>
    public PlacementMode? Placement { get; init; }

    /// <summary>Seconds a toast shows when it gives no duration of its own: 4 by default, and more than 0.</summary>
    public float? Duration { get; init; }
}

/// <summary>Shows the <see cref="Toast"/>s it holds, oldest first, 8 apart in a stack as wide as the widest, above the
/// view's other layers and never in the pointer's way, and times them: each reports its
/// <see cref="ToastProps.OnDismiss"/> once it has shown for its duration less its leaving fade, counted on the
/// context's clock from the update it first shows in. The caller removes the toast then, and it fades out as it
/// leaves.</summary>
[Children]
public sealed class Toaster : Component<ToasterProps>
{
    internal static readonly ContextKey<ToasterContext> Context = new("Toaster");

    const float DefaultDuration = 4f;
    const float Edge = 16f;
    const float Gap = 8f;

    protected override void Render(Ui ui, in ToasterProps p)
    {
        if (p.Duration is { } duration && !(duration > 0f && float.IsFinite(duration)))
            throw Refused(duration);
        var placement = p.Placement ?? PlacementMode.Top;
        var timer = ui.Remember(static () => new ToastTimer());
        ui.Provide(Context, new ToasterContext(timer, p.Duration ?? DefaultDuration));
        using (
            ui.Layer(
                hit: LayerHit.PassThrough,
                tier: LayerTier.Toast,
                direction: Direction.Column,
                align: Across(placement),
                justify: Along(placement),
                padding: Edge
            )
        )
        using (ui.Box(direction: Direction.Column, gap: Gap))
            ui.Children();
    }

    static Align Across(PlacementMode placement) =>
        placement switch
        {
            PlacementMode.Top or PlacementMode.Bottom => Align.Center,
            PlacementMode.TopStart
            or PlacementMode.BottomStart
            or PlacementMode.Left
            or PlacementMode.LeftStart
            or PlacementMode.LeftEnd => Align.Start,
            PlacementMode.TopEnd
            or PlacementMode.BottomEnd
            or PlacementMode.Right
            or PlacementMode.RightStart
            or PlacementMode.RightEnd => Align.End,
            _ => throw Unplaced(placement),
        };

    static Justify Along(PlacementMode placement) =>
        placement switch
        {
            PlacementMode.Top
            or PlacementMode.TopStart
            or PlacementMode.TopEnd
            or PlacementMode.LeftStart
            or PlacementMode.RightStart => Justify.Start,
            PlacementMode.Left or PlacementMode.Right => Justify.Center,
            _ => Justify.End,
        };

    static ArgumentOutOfRangeException Refused(float duration) =>
        new(
            nameof(ToasterProps.Duration),
            duration,
            "A toaster's duration is more than 0 seconds."
        );

    static ArgumentOutOfRangeException Unplaced(PlacementMode placement) =>
        new(nameof(ToasterProps.Placement), placement, "A toaster keeps to a side of the view.");
}

internal readonly record struct ToasterContext(ToastTimer? Timer, float Duration);

/// <summary>The toasts a <see cref="Toaster"/> times, each from the update it first shows in, on one dispatcher tick
/// an update while any is still due to report.</summary>
[FramePath]
internal sealed class ToastTimer
{
    readonly List<Entry> _entries = [];
    readonly Stack<Entry> _spare = new();
    UiView? _view;
    bool _ticking;

    public Entry Join()
    {
        var entry = _spare.Count > 0 ? _spare.Pop() : new Entry(this);
        _entries.Add(entry);
        return entry;
    }

    void Leave(Entry entry)
    {
        _entries.Remove(entry);
        entry.Clear();
        _spare.Push(entry);
    }

    void Tick(UiView view)
    {
        _view = view;
        if (_ticking)
            return;
        _ticking = true;
        view.Context.Dispatcher.Post(this, static t => t.OnTick());
    }

    void OnTick()
    {
        _ticking = false;
        if (_view is not { } view)
            return;
        var now = view.Context.Now;
        var due = false;
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry.Reported || entry.Leaving || entry.Shown is not { } shown)
                continue;
            if ((now - shown).TotalSeconds < entry.Life - Toast.LeaveSeconds)
            {
                due = true;
                continue;
            }
            entry.Reported = true;
            entry.Host.Props.OnDismiss.Invoke();
        }
        if (due)
            Tick(view);
    }

    [FramePath]
    internal sealed class Entry(ToastTimer timer) : IDisposable
    {
        internal Host<ToastProps> Host;
        internal float Life;
        internal bool Leaving;
        internal bool Reported;
        internal TimeSpan? Shown;

        public void Show(UiView view)
        {
            Shown ??= view.Context.Now;
            timer.Tick(view);
        }

        public void Clear()
        {
            Host = default;
            Life = 0f;
            Leaving = Reported = false;
            Shown = null;
        }

        public void Dispose() => timer.Leave(this);
    }
}

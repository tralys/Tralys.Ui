using Tralys.Ui.Components;

namespace Tralys.Ui.Docking;

internal readonly record struct DockSplitProps
{
    public DockGroup? Group { get; init; }
}

/// <summary>A group of a dock's layout: its tab strip, or its two halves beside or above each other with a handle
/// between them that trades their proportions.</summary>
internal sealed class DockSplit : Component<DockSplitProps>
{
    protected override void Render(Ui ui, in DockSplitProps p)
    {
        var boxRef = ui.Ref();
        var liveState = ui.State(float.NaN);
        var split = ui.Remember(
            (boxRef, liveState),
            static h => new SplitEntry(h.boxRef, h.liveState)
        );
        split.Host = ui.Self(p);
        split.Dock = ui.Use(Dock.Context).Dock;
        if (p.Group is not { } group)
            return;

        var children = group.Children;
        if (children.Count < 2)
        {
            ui.DockTabGroup(group: group);
            return;
        }

        var vertical = group.Orientation == SplitOrientation.Vertical;
        var (first, second) = DockSplitSizing.NormalizeProportions(
            children[0].SizeProportion,
            children[1].SizeProportion
        );
        if (split.Live.Value is var live && !float.IsNaN(live))
        {
            first = live;
            second = 1f - live;
        }
        var pair = split.Pair(vertical);
        var halves = new GridTracks(
            Track.MinMax(Track.Px(Dock.MinPanel), Track.Fr(first)),
            Track.Px(Dock.SplitterTrack),
            Track.MinMax(Track.Px(Dock.MinPanel), Track.Fr(second))
        );
        var across = new GridTracks(Track.Fr(1f));
        using (
            ui.Grid(
                columns: vertical ? across : halves,
                rows: vertical ? halves : across,
                minWidth: 0f,
                minHeight: 0f,
                nodeRef: split.Box
            )
        )
        {
            ui.DockSplit(group: children[0], key: UiKey.Of(children[0]));
            ui.ResizeHandle(
                edge: vertical ? ResizeEdge.Bottom : ResizeEdge.Right,
                value: first * pair,
                min: Dock.MinPanel,
                max: pair > 2f * Dock.MinPanel ? pair - Dock.MinPanel : null,
                onValueChange: split.Change,
                onValueCommit: split.Commit,
                variant: ResizeHandleVariant.GhostMuted,
                width: vertical ? Length.Auto : Dock.HandleSize,
                height: vertical ? Dock.HandleSize : Length.Auto,
                alignSelf: vertical ? AlignSelf.Center : default,
                justifySelf: vertical ? default : AlignSelf.Center
            );
            ui.DockSplit(group: children[1], key: UiKey.Of(children[1]));
        }
    }
}

[FramePath]
internal sealed class SplitEntry
{
    internal readonly Callback<float> Change;
    internal readonly Callback<float> Commit;
    internal readonly NodeRef Box;
    internal readonly State<float> Live;
    internal Host<DockSplitProps> Host;
    internal DockModel? Dock;

    public SplitEntry(NodeRef box, State<float> live)
    {
        Box = box;
        Live = live;
        Change = Callback<float>.Of(this, static (s, size) => s.Changed(size));
        Commit = Callback<float>.Of(this, static (s, size) => s.Committed(size));
    }

    internal float Pair(bool vertical)
    {
        var size = Box.Size;
        return MathF.Max(0f, (vertical ? size.Height : size.Width) - Docking.Dock.SplitterTrack);
    }

    bool Vertical => Host.Props.Group is { Orientation: SplitOrientation.Vertical };

    void Changed(float size)
    {
        var pair = Pair(Vertical);
        if (pair > 0f)
            Live.Value = Math.Clamp(size / pair, 0f, 1f);
    }

    void Committed(float size)
    {
        var pair = Pair(Vertical);
        Live.Value = float.NaN;
        if (pair <= 0f || Host.Props.Group is not { } group)
            return;
        var share = Math.Clamp(size / pair, 0f, 1f);
        group.ResizeSplit(share, 1f - share);
        Dock?.RaiseResize();
    }
}

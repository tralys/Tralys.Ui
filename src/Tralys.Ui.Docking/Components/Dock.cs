using System.Runtime.CompilerServices;
using Tralys.Ui.Components;

namespace Tralys.Ui.Docking;

public readonly record struct DockProps
{
    /// <summary>What the dock shows; none leaves it empty.</summary>
    public DockManager? Manager { get; init; }

    /// <summary>Draws an element's panel while its tab is selected, from the element's
    /// <see cref="DockElement.Content"/> or anything else the app keeps for it, with the element's index in its
    /// group.</summary>
    public Slot<DockElement> Content { get; init; }

    /// <summary>Shown in the centre region while it holds no panels.</summary>
    public Slot EmptyCenter { get; init; }

    /// <summary>Whether the empty centre takes drops: true by default; false leaves it to an app that shows its own
    /// content there.</summary>
    public bool? CenterDockable { get; init; }

    /// <summary>Tears panels out into windows of their own; none keeps floating panels inside the dock.</summary>
    public IDockWindowHost? WindowHost { get; init; }

    /// <summary>Reports each resize of an edge, a split or a floating window by the user. The manager's saved layout
    /// carries every size, so an app only marks its state dirty.</summary>
    public Callback OnResize { get; init; }

    /// <summary>All its parent's width by default.</summary>
    public Length Width { get; init; }

    /// <summary>All its parent's height by default.</summary>
    public Length Height { get; init; }
}

internal readonly record struct FloatArea(Size Area, int Floats);

internal readonly record struct DockContext(
    DockModel? Dock,
    IDockDragSink? Sink,
    Slot<DockElement> Content
);

/// <summary>Shows a <see cref="DockManager"/>: its five regions in a grid whose edges size apart and collapse while
/// empty, each region's groups as tab strips split beside or above each other, its floating windows over them and kept
/// inside the dock, and while a tab or a floating panel drags, the targets it can dock on.</summary>
/// <remarks>A tab dropped on a strip joins it; on a group's compass it joins the group or, unless the group floats,
/// splits it; on a root indicator it docks along that edge; anywhere else it floats, or with a
/// <see cref="DockProps.WindowHost"/>, tears out into a window of its own once the drag has left the dock's window. A
/// floating panel dragged by its title moves, and lands the same way with all its tabs in order; dragging a tab leaves
/// the panel or its window where it is. Unmounting closes any window it tore out.</remarks>
public sealed class Dock : Component<DockProps>
{
    internal const float Margin = 6f;
    internal const float SplitterTrack = 10f;
    internal const float HandleSize = 4f;
    internal const float MinPanel = 50f;

    internal static readonly ContextKey<DockContext> Context = new("Dock");

    protected override void Render(Ui ui, in DockProps p)
    {
        var model = ui.Remember(static () => new DockModel());
        model.Host = ui.Self(p);
        model.Root = ui.Ref();
        model.Regions = ui.Ref();
        model.Center = ui.Ref();
        model.Sizing = ui.State(0);
        model.Torn = ui.State(0);
        ui.Effect(model, 0, static (m, _, cx) => m.View = cx.View);
        var manager = p.Manager;
        ui.Effect(model, manager, static (m, manager, _) => m.Attach(manager));
        ui.Provide(Context, new DockContext(model, model, p.Content));
        var area = model.Root.Size;
        ui.Effect(
            model,
            new FloatArea(area, manager?.FloatingWindows.Count ?? 0),
            static (m, at, _) => m.KeepFloatsWithin(at.Area)
        );
        _ = model.Sizing.Value;
        _ = model.Torn.Value;

        using (
            ui.Box(
                direction: Direction.Column,
                width: p.Width.IsAuto ? Length.Full : p.Width,
                height: p.Height.IsAuto ? Length.Full : p.Height,
                nodeRef: model.Root
            )
        )
        {
            Regions(ui, model, manager, in p);
            if (manager is not null)
            {
                var floats = manager.FloatingWindows;
                for (var i = 0; i < floats.Count; i++)
                {
                    var window = floats[i];
                    if (!model.IsTornOut(window))
                        ui.DockFloat(
                            window: window,
                            key: UiKey.Of(window),
                            position: Position.Absolute,
                            left: 0f,
                            top: 0f,
                            right: 0f,
                            bottom: 0f
                        );
                }
            }
            ui.DockOverlay(position: Position.Absolute, left: 0f, top: 0f, right: 0f, bottom: 0f);
        }
    }

    [FramePath]
    static void Regions(
        Ui ui,
        DockModel model,
        DockManager? manager,
        in DockProps p,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var left = manager is not null && HasContent(manager.LeftGroup);
        var right = manager is not null && HasContent(manager.RightGroup);
        var top = manager is not null && HasContent(manager.TopGroup);
        var bottom = manager is not null && HasContent(manager.BottomGroup);
        var center = manager is not null && HasContent(manager.CenterGroup);
        var sizes = model.Shown(manager?.PanelSizes ?? default);
        var leftWidth = DockModel.SizeOf(sizes, DockModel.LeftEdge);
        var rightWidth = DockModel.SizeOf(sizes, DockModel.RightEdge);
        var topHeight = DockModel.SizeOf(sizes, DockModel.TopEdge);
        var bottomHeight = DockModel.SizeOf(sizes, DockModel.BottomEdge);
        var room = model.Regions.Size;
        var across = room.Width - 2f * Margin;
        var down = room.Height - 2f * Margin;

        using (
            ui.Grid(
                columns: new GridTracks(
                    Edge(left, leftWidth),
                    Gap(left),
                    Track.Fr(1f),
                    Gap(right),
                    Edge(right, rightWidth)
                ),
                rows: new GridTracks(
                    Edge(top, topHeight),
                    Gap(top),
                    Track.Fr(1f),
                    Gap(bottom),
                    Edge(bottom, bottomHeight)
                ),
                padding: Margin,
                grow: 1f,
                minHeight: 0f,
                nodeRef: model.Regions,
                key: key,
                file: file,
                line: line
            )
        )
        {
            if (manager is null)
                return;
            if (top)
            {
                ui.DockSplit(group: manager.TopGroup, row: 1, column: 1, columnSpan: 5);
                ui.ResizeHandle(
                    edge: ResizeEdge.Bottom,
                    value: topHeight,
                    min: MinPanel,
                    max: Room(down, bottom, bottomHeight),
                    onValueChange: model.EdgeChange[DockModel.TopEdge],
                    onValueCommit: model.EdgeCommit,
                    variant: ResizeHandleVariant.Ghost,
                    height: HandleSize,
                    row: 2,
                    column: 1,
                    columnSpan: 5,
                    alignSelf: AlignSelf.Center
                );
            }
            if (left)
            {
                ui.DockSplit(group: manager.LeftGroup, row: 3, column: 1);
                ui.ResizeHandle(
                    edge: ResizeEdge.Right,
                    value: leftWidth,
                    min: MinPanel,
                    max: Room(across, right, rightWidth),
                    onValueChange: model.EdgeChange[DockModel.LeftEdge],
                    onValueCommit: model.EdgeCommit,
                    variant: ResizeHandleVariant.Ghost,
                    width: HandleSize,
                    row: 3,
                    column: 2,
                    justifySelf: AlignSelf.Center
                );
            }
            using (
                ui.Box(
                    direction: Direction.Column,
                    row: 3,
                    column: 3,
                    minWidth: 0f,
                    minHeight: 0f,
                    nodeRef: model.Center
                )
            )
            {
                if (center)
                    ui.DockSplit(group: manager.CenterGroup, grow: 1f);
                else
                    p.EmptyCenter.Render(ui);
            }
            if (right)
            {
                ui.ResizeHandle(
                    edge: ResizeEdge.Left,
                    value: rightWidth,
                    min: MinPanel,
                    max: Room(across, left, leftWidth),
                    onValueChange: model.EdgeChange[DockModel.RightEdge],
                    onValueCommit: model.EdgeCommit,
                    variant: ResizeHandleVariant.Ghost,
                    width: HandleSize,
                    row: 3,
                    column: 4,
                    justifySelf: AlignSelf.Center
                );
                ui.DockSplit(group: manager.RightGroup, row: 3, column: 5);
            }
            if (bottom)
            {
                ui.ResizeHandle(
                    edge: ResizeEdge.Top,
                    value: bottomHeight,
                    min: MinPanel,
                    max: Room(down, top, topHeight),
                    onValueChange: model.EdgeChange[DockModel.BottomEdge],
                    onValueCommit: model.EdgeCommit,
                    variant: ResizeHandleVariant.Ghost,
                    height: HandleSize,
                    row: 4,
                    column: 1,
                    columnSpan: 5,
                    alignSelf: AlignSelf.Center
                );
                ui.DockSplit(group: manager.BottomGroup, row: 5, column: 1, columnSpan: 5);
            }
        }
    }

    internal static bool HasContent(DockGroup group) =>
        group.Elements.Count > 0 || group.Children.Count > 0;

    static Track Edge(bool shown, float size) => Track.Px(shown ? MathF.Max(size, MinPanel) : 0f);

    static Track Gap(bool shown) => Track.Px(shown ? SplitterTrack : 0f);

    static float? Room(float extent, bool otherShown, float other)
    {
        var room = extent - SplitterTrack - (otherShown ? other + SplitterTrack : 0f);
        return room > MinPanel ? room : null;
    }
}

using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Docking;

internal readonly record struct DockFloatProps
{
    public FloatingWindow? Window { get; init; }

    /// <summary>Fills a window of its own: its title drags the window, and the window's frame resizes it.</summary>
    public bool HostedAlone { get; init; }
}

/// <summary>A floating window's panel: a title bar, titled after its active tab, that moves it and closes it, over its
/// tab group, kept inside the dock and resized by its left, right and bottom edges.</summary>
internal sealed class DockFloat : Component<DockFloatProps>
{
    protected override void Render(Ui ui, in DockFloatProps p)
    {
        var c = ui.Use(Dock.Context);
        var t = ui.Theme;
        var panelRef = ui.Ref();
        var titleRef = ui.Ref();
        var f = ui.Remember(
            (panelRef, titleRef),
            static refs => new FloatEntry(refs.panelRef, refs.titleRef)
        );
        f.Host = ui.Self(p);
        f.Sink = c.Sink;
        f.Dock = c.Dock;
        if (p.Window is not { } window)
            return;

        var title = window.Group.ActiveElement?.Title ?? "Floating";
        var band = t.Grip(GripToken.Band);
        if (p.HostedAlone)
        {
            using (Panel(ui, f, p.HostedAlone))
            {
                ui.DockFloatTitle(
                    title: title,
                    onClose: f.Close,
                    onDrag: f.TitleDrag,
                    titleRef: f.Title
                );
                ui.DockTabGroup(group: window.Group, grow: 1f);
            }
            return;
        }

        using (
            ui.Floating(
                bounds: new Rect(
                    window.X,
                    window.Y,
                    window.ClampWidth(window.Width),
                    window.ClampHeight(window.Height)
                ),
                onBoundsChange: f.Bounds,
                resizeEdges: ResizeEdges.Left | ResizeEdges.Right | ResizeEdges.Bottom
            )
        )
        using (Panel(ui, f, p.HostedAlone))
        {
            using (ui.FloatingHandle(onDrag: f.HandleDrag))
                ui.DockFloatTitle(title: title, onClose: f.Close, titleRef: f.Title);
            ui.DockTabGroup(
                group: window.Group,
                grow: 1f,
                margin: new Thickness(band, 0f, band, band)
            );
        }
    }

    [FramePath]
    static Scope Panel(
        Ui ui,
        FloatEntry f,
        bool alone,
        UiKey key = default,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        return ui.Box(
            direction: Direction.Column,
            grow: alone ? 0f : 1f,
            background: t.Color(ColorToken.Bg),
            border: new Border(1f, t.Color(ColorToken.Border)),
            radius: t.Radius(RadiusToken.Md),
            overflow: Overflow.Clip,
            nodeRef: f.Panel,
            key: key,
            file: file,
            line: line
        );
    }
}

internal readonly record struct DockFloatTitleProps
{
    public TextValue Title { get; init; }

    public Callback OnClose { get; init; }

    public Handler<DragEvent> OnDrag { get; init; }

    public NodeRef? TitleRef { get; init; }
}

internal sealed class DockFloatTitle : Component<DockFloatTitleProps>
{
    protected override void Render(Ui ui, in DockFloatTitleProps p)
    {
        var t = ui.Theme;
        using (
            ui.Box(
                align: Align.Center,
                height: 24f,
                background: t.Color(ColorToken.Bg),
                onDrag: p.OnDrag,
                nodeRef: p.TitleRef
            )
        )
        {
            ui.Text(
                p.Title,
                fontWeight: t.Weight(WeightToken.Interface),
                grow: 1f,
                margin: new Thickness(8f, 0f, 0f, 0f)
            );
            ui.IconButton(
                icon: IconGlyph.Of(IconRole.X),
                size: ComponentSize.Xs,
                scheme: ColorScheme.Secondary,
                variant: ComponentVariant.Ghost,
                onClick: p.OnClose,
                margin: new Thickness(0f, 0f, 4f, 0f)
            );
        }
    }
}

[FramePath]
internal sealed class FloatEntry : IDisposable
{
    internal readonly Callback<Rect> Bounds;
    internal readonly Handler<DragEvent> HandleDrag;
    internal readonly Handler<DragEvent> TitleDrag;
    internal readonly Callback Close;
    internal readonly NodeRef Panel;
    internal readonly NodeRef Title;
    internal Host<DockFloatProps> Host;
    internal IDockDragSink? Sink;
    internal DockModel? Dock;
    bool _dragging;

    public FloatEntry(NodeRef panel, NodeRef title)
    {
        Panel = panel;
        Title = title;
        Bounds = Callback<Rect>.Of(this, static (f, bounds) => f.Moved(bounds));
        HandleDrag = Handler<DragEvent>.Of(this, static (f, ref e) => f.Dragged(ref e));
        TitleDrag = Handler<DragEvent>.Of(this, static (f, ref e) => f.Dragged(ref e));
        Close = Callback.Of(this, static f => f.Host.Props.Window?.RequestClose());
    }

    void Moved(Rect bounds)
    {
        if (Host.Props.Window is not { } window)
            return;
        window.X = bounds.X;
        window.Y = bounds.Y;
        window.Width = bounds.Width;
        window.Height = bounds.Height;
        Dock?.RaiseResize();
    }

    void Dragged(ref DragEvent e)
    {
        if (Host.Props.Window?.Group.ActiveElement is not { } active)
            return;
        var title = Title.Node?.ViewBounds ?? default;
        var point = new Vector2(title.X + e.Position.X, title.Y + e.Position.Y);
        switch (e.Phase)
        {
            case DragPhase.Start:
                var panel = Panel.Node?.ViewBounds ?? default;
                _dragging = true;
                Sink?.DragStarted(
                    true,
                    active,
                    point,
                    point - e.Offset - new Vector2(panel.X, panel.Y)
                );
                break;
            case DragPhase.Move when _dragging:
                Sink?.DragMoved(true, point, false);
                break;
            case DragPhase.End when _dragging:
                Sink?.Dropped(true, active, point);
                EndDrag();
                break;
            case DragPhase.Cancel:
                EndDrag();
                break;
        }
    }

    // A drop can close this window, and nothing reports the drag's end after this.
    public void Dispose() => EndDrag();

    void EndDrag()
    {
        if (!_dragging)
            return;

        _dragging = false;
        Sink?.DragEnded();
    }
}

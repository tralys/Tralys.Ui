using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>The edges of a <see cref="Floating"/> surface that resize it; a corner resizes it where both its edges
/// do.</summary>
[Flags]
public enum ResizeEdges : byte
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8,
    All = Left | Top | Right | Bottom,
}

public readonly record struct FloatingProps
{
    /// <summary>Where the surface sits in the floating area, and its size; a size of 0 on an axis leaves the surface
    /// to its content there. A set height holds: what the surface holds lies in a column as tall as it, which content
    /// taller than it shrinks to and scrolls in.</summary>
    public Rect Bounds { get; init; }

    /// <summary>Reports each place and size a drag on the handle or an edge moves the surface to.</summary>
    public Callback<Rect> OnBoundsChange { get; init; }

    /// <summary>Reports the surface's bounds once a drag that moved it ends, whether released or cut short.</summary>
    public Callback<Rect> OnBoundsCommit { get; init; }

    /// <summary>The edges a drag resizes the surface by, each a band <see cref="GripToken.Band"/> deep and each corner
    /// between two of them a <see cref="GripToken.Corner"/> square; none by default.</summary>
    public ResizeEdges ResizeEdges { get; init; }

    /// <summary>The narrowest a resize makes the surface: 120 by default.</summary>
    public float? MinWidth { get; init; }

    /// <summary>The shortest a resize makes the surface: 80 by default.</summary>
    public float? MinHeight { get; init; }

    public float? MaxWidth { get; init; }

    public float? MaxHeight { get; init; }

    /// <summary>The grid a moved surface's origin and a resized surface's size land on; none by default.</summary>
    public float Snap { get; init; }

    /// <summary>The grid a moved surface's origin lands on, in place of <see cref="Snap"/>; 0 leaves it where the drag
    /// puts it, for a caller that snaps it to something else as it takes <see cref="OnBoundsChange"/>.</summary>
    public float? MoveSnap { get; init; }

    /// <summary>The width the surface's content keeps to its height as an edge resizes it, or 0 to size the two
    /// apart; a corner follows the axis the drag changed more.</summary>
    public float AspectRatio { get; init; }

    /// <summary>What of the surface lies outside the content <see cref="AspectRatio"/> holds, such as a title bar's
    /// height or a side column's width.</summary>
    public Size AspectInset { get; init; }

    /// <summary>Keeps the surface where it is: its handle takes no drags.</summary>
    public bool Pinned { get; init; }

    /// <summary>The floating area's width: all its parent's by default.</summary>
    public Length Width { get; init; }

    /// <summary>The floating area's height: all its parent's by default.</summary>
    public Length Height { get; init; }
}

/// <summary>A surface, such as a floating window, that its caller places at <see cref="FloatingProps.Bounds"/> in the
/// area the floating fills, passing input through everywhere else. A drag on its <see cref="Handle"/> moves it, and
/// one on an edge <see cref="FloatingProps.ResizeEdges"/> names resizes it, a left or top edge moving its origin so the
/// opposite edge stays; either reports the new bounds through <see cref="FloatingProps.OnBoundsChange"/>, leaving them
/// to its caller. Each keeps the surface inside the area along an axis the area has a size on, and a moved surface
/// lands clamped, snapped, then clamped again.</summary>
[Children]
public sealed class Floating : Component<FloatingProps>
{
    internal static readonly ContextKey<FloatingContext> Context = new("Floating", default);

    protected override void Render(Ui ui, in FloatingProps p)
    {
        var t = ui.Theme;
        var model = ui.Remember(
            (Area: ui.Ref(), Surface: ui.Ref()),
            static refs => new Floater(refs.Area, refs.Surface)
        );
        model.Host = ui.Self(p);
        ui.Provide(Context, new FloatingContext(model));
        var b = p.Bounds;
        using (
            ui.Box(
                width: p.Width.IsAuto ? Length.Full : p.Width,
                height: p.Height.IsAuto ? Length.Full : p.Height,
                nodeRef: model.Area
            )
        )
        using (
            ui.Box(
                direction: Direction.Column,
                position: Position.Absolute,
                left: b.X,
                top: b.Y,
                width: b.Width > 0f ? b.Width : Length.Auto,
                height: b.Height > 0f ? b.Height : Length.Auto,
                nodeRef: model.Surface
            )
        )
        {
            using (ui.Box(direction: Direction.Column, grow: 1f, minHeight: 0f))
                ui.Children();
            if (p.ResizeEdges != ResizeEdges.None)
                ui.Grips(
                    edges: p.ResizeEdges,
                    band: t.Grip(GripToken.Band),
                    corner: t.Grip(GripToken.Corner),
                    model: model
                );
        }
    }

    /// <summary>The part of a <see cref="Floating"/> surface, such as its title bar, that a drag moves it by; what is
    /// pressable inside it still takes a click, and gives up its press once a drag starts from it.</summary>
    [Children]
    public sealed class Handle : Component<FloatingHandleProps>
    {
        static readonly Func<Floater, HandleLease> Lease = static m => new HandleLease(m);

        protected override void Render(Ui ui, in FloatingHandleProps p)
        {
            var model = ui.Use(Context).Model;
            if (model is not null)
                ui.Remember(model, Lease);
            using (
                ui.Box(
                    direction: Direction.Column,
                    onDrag: Handler<DragEvent>.Of(
                        ui.Self(p),
                        static (h, ref e) => Dragged(h, ref e)
                    )
                )
            )
                ui.Children();
        }

        static void Dragged(Host<FloatingHandleProps> host, ref DragEvent e)
        {
            if (host.Props.Pinned)
            {
                if (e.Phase == DragPhase.Start)
                    e.PreventDefault();
                return;
            }
            if (host.Use(Context).Model is { } model)
            {
                model.Move(ref e);
                if (e.Phase == DragPhase.Start && e.Prevented)
                    return;
            }
            host.Props.OnDrag.Invoke(ref e);
        }
    }

    /// <summary>A grip on a <see cref="Floating"/> surface that resizes it by <see cref="FloatingGripProps.Edge"/> as an
    /// edge <see cref="FloatingProps.ResizeEdges"/> names does, within its sizes, snap and aspect, kept inside the
    /// area, but placed and drawn as its caller says: a corner alone, or a grip with ridges.</summary>
    public sealed class Grip : Component<FloatingGripProps>
    {
        protected override void Render(Ui ui, in FloatingGripProps p)
        {
            var model = ui.Use(Context).Model;
            ui.ResizeHandle(
                edge: p.Edge,
                variant: p.Variant,
                band: p.Band,
                lightsInward: p.LightsInward,
                ridges: p.Ridges,
                onDrag: model is null ? default : model.Grips[(int)p.Edge],
                width: p.Width,
                height: p.Height
            );
        }
    }
}

public readonly record struct FloatingHandleProps
{
    /// <summary>Every drag on the handle, after the surface's own move; a drag the surface refuses, while
    /// <see cref="FloatingProps.Pinned"/> or <see cref="Pinned"/>, reports nothing.</summary>
    public Handler<DragEvent> OnDrag { get; init; }

    /// <summary>Refuses every drag on this handle alone, keeping what it holds mounted, so a handle nominated inside
    /// it moves the surface in its place.</summary>
    public bool Pinned { get; init; }
}

public readonly record struct FloatingGripProps
{
    /// <summary>The edge or corner of the surface it resizes by; <see cref="ResizeEdge.Right"/> by default.</summary>
    public ResizeEdge Edge { get; init; }

    public ResizeHandleVariant Variant { get; init; }

    /// <summary>As <see cref="ResizeHandleProps.Band"/>: how deep a corner reaches in from its two edges.</summary>
    public float Band { get; init; }

    public bool LightsInward { get; init; }

    public bool Ridges { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }
}

internal readonly record struct FloatingContext(Floater? Model);

[FramePath]
internal sealed class HandleLease(Floater model) : IDisposable
{
    public void Dispose() => model.HandleLeft();
}

[FramePath]
internal sealed class Floater
{
    const float DefaultMinWidth = 120f;
    const float DefaultMinHeight = 80f;

    internal readonly Handler<DragEvent>[] Grips = new Handler<DragEvent>[8];
    internal Host<FloatingProps> Host;
    internal readonly NodeRef Area;
    internal readonly NodeRef Surface;
    Vector2 _origin;
    Vector2 _extent;
    Rect _at;
    ResizeEdge _edge;
    bool _moving;
    bool _resizing;
    bool _changed;

    public Floater(NodeRef area, NodeRef surface)
    {
        Area = area;
        Surface = surface;
        for (var i = 0; i < Grips.Length; i++)
            Grips[i] = Handler<DragEvent>.Of(
                this,
                Edges.Of[i],
                static (m, edge, ref e) => m.Resize(edge.Edge, ref e)
            );
    }

    internal void Move(ref DragEvent e)
    {
        ref readonly var p = ref Host.Props;
        switch (e.Phase)
        {
            case DragPhase.Start:
                if (p.Pinned || _resizing)
                {
                    e.PreventDefault();
                    return;
                }
                Begin(in p);
                _moving = true;
                break;
            case DragPhase.Move when _moving:
                Place(in p, e.Offset);
                break;
            case DragPhase.End or DragPhase.Cancel when _moving:
                _moving = false;
                Commit(in p);
                break;
        }
    }

    internal void HandleLeft()
    {
        if (!_moving)
            return;
        _moving = false;
        Commit(in Host.Props);
    }

    void Resize(ResizeEdge edge, ref DragEvent e)
    {
        ref readonly var p = ref Host.Props;
        switch (e.Phase)
        {
            case DragPhase.Start:
                if (_moving)
                {
                    e.PreventDefault();
                    return;
                }
                Begin(in p);
                _edge = edge;
                _resizing = true;
                break;
            case DragPhase.Move when _resizing:
                SizeTo(in p, e.Offset);
                break;
            case DragPhase.End or DragPhase.Cancel when _resizing:
                _resizing = false;
                Commit(in p);
                break;
        }
    }

    void Begin(in FloatingProps p)
    {
        var b = p.Bounds;
        var measured = Surface.Size;
        _origin = new Vector2(b.X, b.Y);
        _extent = new Vector2(
            b.Width > 0f ? b.Width : measured.Width,
            b.Height > 0f ? b.Height : measured.Height
        );
        _at = b;
        _changed = false;
    }

    void Place(in FloatingProps p, Vector2 offset)
    {
        var bound = Area.Size;
        var at = Clamp(_origin + offset, bound);
        var snap = p.MoveSnap ?? p.Snap;
        if (snap > 0f)
            at = Clamp(new Vector2(Snapped(at.X, snap), Snapped(at.Y, snap)), bound);
        Report(in p, new Rect(at.X, at.Y, p.Bounds.Width, p.Bounds.Height));
    }

    void SizeTo(in FloatingProps p, Vector2 travel)
    {
        var bound = Area.Size;
        var horizontal = _edge is not (ResizeEdge.Top or ResizeEdge.Bottom);
        var vertical = _edge is not (ResizeEdge.Left or ResizeEdge.Right);
        var left = _edge is ResizeEdge.Left or ResizeEdge.TopLeft or ResizeEdge.BottomLeft;
        var top = _edge is ResizeEdge.Top or ResizeEdge.TopLeft or ResizeEdge.TopRight;

        var width = horizontal
            ? FitWidth(
                in p,
                Snapped(left ? _extent.X - travel.X : _extent.X + travel.X, p.Snap),
                left,
                bound
            )
            : _extent.X;
        var height = vertical
            ? FitHeight(
                in p,
                Snapped(top ? _extent.Y - travel.Y : _extent.Y + travel.Y, p.Snap),
                top,
                bound
            )
            : _extent.Y;

        var linked = p.AspectRatio > 0f;
        if (linked)
        {
            if (
                !vertical
                || (horizontal && MathF.Abs(width - _extent.X) >= MathF.Abs(height - _extent.Y))
            )
            {
                height = FitHeight(in p, HeightFor(in p, width), top, bound);
                width = FitWidth(in p, WidthFor(in p, height), left, bound);
                height = FitHeight(in p, HeightFor(in p, width), top, bound);
            }
            else
            {
                width = FitWidth(in p, WidthFor(in p, height), left, bound);
                height = FitHeight(in p, HeightFor(in p, width), top, bound);
                width = FitWidth(in p, WidthFor(in p, height), left, bound);
            }
        }

        var b = p.Bounds;
        var x = b.X;
        var y = b.Y;
        var w = b.Width;
        var h = b.Height;
        if (horizontal || linked)
        {
            w = width;
            x = left ? _origin.X + (_extent.X - width) : _origin.X;
        }
        if (vertical || linked)
        {
            h = height;
            y = top ? _origin.Y + (_extent.Y - height) : _origin.Y;
        }
        Report(in p, new Rect(x, y, w, h));
    }

    void Report(in FloatingProps p, Rect bounds)
    {
        if (bounds == _at)
            return;
        _at = bounds;
        _changed = true;
        p.OnBoundsChange.Invoke(bounds);
    }

    void Commit(in FloatingProps p)
    {
        if (_changed)
            p.OnBoundsCommit.Invoke(_at);
        _changed = false;
    }

    Vector2 Clamp(Vector2 origin, Size bound) =>
        new(Origin(origin.X, _extent.X, bound.Width), Origin(origin.Y, _extent.Y, bound.Height));

    float FitWidth(in FloatingProps p, float width, bool leading, Size bound) =>
        MathF.Min(
            Extent(width, p.MinWidth ?? DefaultMinWidth, p.MaxWidth),
            leading ? Leading(_origin.X, _extent.X, bound.Width) : Trailing(_origin.X, bound.Width)
        );

    float FitHeight(in FloatingProps p, float height, bool leading, Size bound) =>
        MathF.Min(
            Extent(height, p.MinHeight ?? DefaultMinHeight, p.MaxHeight),
            leading
                ? Leading(_origin.Y, _extent.Y, bound.Height)
                : Trailing(_origin.Y, bound.Height)
        );

    static float HeightFor(in FloatingProps p, float width) =>
        (width - p.AspectInset.Width) / p.AspectRatio + p.AspectInset.Height;

    static float WidthFor(in FloatingProps p, float height) =>
        (height - p.AspectInset.Height) * p.AspectRatio + p.AspectInset.Width;

    static float Extent(float extent, float min, float? max) =>
        MathF.Max(min, MathF.Min(extent, max ?? float.PositiveInfinity));

    static float Origin(float origin, float extent, float bound) =>
        bound <= 0f ? origin : Math.Clamp(origin, 0f, MathF.Max(0f, bound - extent));

    static float Trailing(float origin, float bound) =>
        bound <= 0f ? float.PositiveInfinity : MathF.Max(0f, bound - origin);

    static float Leading(float origin, float extent, float bound) =>
        bound <= 0f ? float.PositiveInfinity : MathF.Max(0f, origin + extent);

    static float Snapped(float value, float snap) =>
        snap > 0f ? MathF.Round(value / snap, MidpointRounding.AwayFromZero) * snap : value;

    sealed class Edges(ResizeEdge edge)
    {
        public static readonly Edges[] Of =
        [
            new(ResizeEdge.Right),
            new(ResizeEdge.Left),
            new(ResizeEdge.Bottom),
            new(ResizeEdge.Top),
            new(ResizeEdge.TopLeft),
            new(ResizeEdge.TopRight),
            new(ResizeEdge.BottomLeft),
            new(ResizeEdge.BottomRight),
        ];

        public ResizeEdge Edge => edge;
    }
}

[FramePath]
internal static class FloatingParts
{
    public static void Grips(
        this Ui ui,
        ResizeEdges edges,
        float band,
        float corner,
        Floater model,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var left = (edges & ResizeEdges.Left) != 0;
        var top = (edges & ResizeEdges.Top) != 0;
        var right = (edges & ResizeEdges.Right) != 0;
        var bottom = (edges & ResizeEdges.Bottom) != 0;
        using (ui.Fragment(key: key, file: file, line: line))
        {
            if (left)
                ui.ResizeHandle(
                    edge: ResizeEdge.Left,
                    variant: ResizeHandleVariant.Edge,
                    onDrag: model.Grips[(int)ResizeEdge.Left],
                    width: band,
                    position: Position.Absolute,
                    left: 0f,
                    top: 0f,
                    bottom: 0f
                );
            if (top)
                ui.ResizeHandle(
                    edge: ResizeEdge.Top,
                    variant: ResizeHandleVariant.Edge,
                    onDrag: model.Grips[(int)ResizeEdge.Top],
                    height: band,
                    position: Position.Absolute,
                    left: 0f,
                    top: 0f,
                    right: 0f
                );
            if (right)
                ui.ResizeHandle(
                    edge: ResizeEdge.Right,
                    variant: ResizeHandleVariant.Edge,
                    onDrag: model.Grips[(int)ResizeEdge.Right],
                    width: band,
                    position: Position.Absolute,
                    top: 0f,
                    right: 0f,
                    bottom: 0f
                );
            if (bottom)
                ui.ResizeHandle(
                    edge: ResizeEdge.Bottom,
                    variant: ResizeHandleVariant.Edge,
                    onDrag: model.Grips[(int)ResizeEdge.Bottom],
                    height: band,
                    position: Position.Absolute,
                    left: 0f,
                    right: 0f,
                    bottom: 0f
                );
            if (top && left)
                ui.ResizeHandle(
                    edge: ResizeEdge.TopLeft,
                    variant: ResizeHandleVariant.Edge,
                    band: band,
                    onDrag: model.Grips[(int)ResizeEdge.TopLeft],
                    width: corner,
                    height: corner,
                    position: Position.Absolute,
                    left: 0f,
                    top: 0f
                );
            if (top && right)
                ui.ResizeHandle(
                    edge: ResizeEdge.TopRight,
                    variant: ResizeHandleVariant.Edge,
                    band: band,
                    onDrag: model.Grips[(int)ResizeEdge.TopRight],
                    width: corner,
                    height: corner,
                    position: Position.Absolute,
                    top: 0f,
                    right: 0f
                );
            if (bottom && left)
                ui.ResizeHandle(
                    edge: ResizeEdge.BottomLeft,
                    variant: ResizeHandleVariant.Edge,
                    band: band,
                    onDrag: model.Grips[(int)ResizeEdge.BottomLeft],
                    width: corner,
                    height: corner,
                    position: Position.Absolute,
                    left: 0f,
                    bottom: 0f
                );
            if (bottom && right)
                ui.ResizeHandle(
                    edge: ResizeEdge.BottomRight,
                    variant: ResizeHandleVariant.Edge,
                    band: band,
                    onDrag: model.Grips[(int)ResizeEdge.BottomRight],
                    width: corner,
                    height: corner,
                    position: Position.Absolute,
                    right: 0f,
                    bottom: 0f
                );
        }
    }
}

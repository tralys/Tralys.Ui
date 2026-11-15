using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>The edge or corner of what a <see cref="ResizeHandle"/> resizes that the handle sits on.</summary>
public enum ResizeEdge : byte
{
    /// <summary>The right edge: a drag to the right widens what it resizes, as a handle after a column does.</summary>
    Right,

    /// <summary>The left edge: a drag to the left widens what it resizes, as a handle before a column does.</summary>
    Left,

    /// <summary>The bottom edge: a drag down heightens what it resizes, as a handle below a row does.</summary>
    Bottom,

    /// <summary>The top edge: a drag up heightens what it resizes, as a handle above a row does.</summary>
    Top,

    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>How a <see cref="ResizeHandle"/> looks.</summary>
public enum ResizeHandleVariant : byte
{
    /// <summary>A bar in <see cref="ColorToken.Border"/>, <see cref="ColorToken.BorderHover"/> under the pointer, and
    /// <see cref="ColorToken.BorderFocus"/> while dragged or focused by keys.</summary>
    Bar,

    /// <summary>A bar that shows only under the pointer, in <see cref="ColorToken.Border"/>, and while dragged or
    /// focused by keys, in <see cref="ColorToken.BorderFocus"/>, as between docked panels.</summary>
    Ghost,

    /// <summary>Nothing at rest, and a 2-unit <see cref="ColorToken.BorderAccent"/> line along its edge or edges while
    /// under the pointer or dragged, as along a window's edge.</summary>
    Edge,

    /// <summary>As <see cref="Ghost"/>, in <see cref="ColorToken.BorderMuted"/> under the pointer, as between the halves
    /// of a split inside a docked panel.</summary>
    GhostMuted,
}

public readonly record struct ResizeHandleProps
{
    /// <summary>The edge or corner it sits on, which picks its cursor and which way a drag or an arrow key moves
    /// <see cref="Value"/>; <see cref="ResizeEdge.Right"/> by default.</summary>
    public ResizeEdge Edge { get; init; }

    /// <summary>The size it sets, in layout units: the width of what it resizes on a left or right edge, the height on
    /// a top or bottom one. A corner sets none, and reports through <see cref="OnDrag"/> only.</summary>
    public float Value { get; init; }

    /// <summary>Reports each size a drag or an arrow key moves the handle to, and the size a drag started from as the
    /// drag is cancelled. Without it the handle sets no size, takes no keys and is no Tab stop.</summary>
    public Callback<float> OnValueChange { get; init; }

    /// <summary>Reports the size once a drag that moved it ends uncancelled, and after each arrow key that moved
    /// it.</summary>
    public Callback<float> OnValueCommit { get; init; }

    public float Min { get; init; }

    /// <summary>The largest size; none by default. <see cref="Min"/> wins over it.</summary>
    public float? Max { get; init; }

    /// <summary>How far an arrow key moves it: 10 by default.</summary>
    public float? Step { get; init; }

    /// <summary>The multiple a drag lands the size on; none by default.</summary>
    public float? DragStep { get; init; }

    /// <summary>Moves a ghost of the handle, in <see cref="ColorToken.BorderFocus"/> at half opacity, while a drag runs,
    /// as far as <see cref="Min"/> and <see cref="Max"/> allow, and reports the size only as the drag ends.</summary>
    public bool Preview { get; init; }

    /// <summary>Every drag on the handle, after the handle's own: for what a size can't say, such as a corner or a
    /// window's edge.</summary>
    public Handler<DragEvent> OnDrag { get; init; }

    public ResizeHandleVariant Variant { get; init; }

    /// <summary>How deep a corner reaches in from its two edges: only those strips take the pointer, so what it
    /// encloses stays clickable. 0 takes the whole corner.</summary>
    public float Band { get; init; }

    /// <summary>With <see cref="ResizeHandleVariant.Edge"/>, lights the inner side of <see cref="Band"/>, where a
    /// window without a frame shows its edge.</summary>
    public bool LightsInward { get; init; }

    /// <summary>Draws three ridges in a <see cref="ResizeEdge.BottomRight"/> corner, brighter under the pointer and
    /// while dragged.</summary>
    public bool Ridges { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A handle on the edge or corner of something resizable, such as the column or row before or after it in a
/// grid, or a window: it shows the resize cursor of its <see cref="ResizeHandleProps.Edge"/>, and a drag on it reports
/// the size it moves <see cref="ResizeHandleProps.Value"/> to, within <see cref="ResizeHandleProps.Min"/> and
/// <see cref="ResizeHandleProps.Max"/>, leaving the value to its caller. While it has the focus, the arrow keys along
/// its axis move it by <see cref="ResizeHandleProps.Step"/>. Escape mid-drag, the focus leaving, or the drag being
/// cancelled reports the size the drag started from.</summary>
public sealed class ResizeHandle : Component<ResizeHandleProps>
{
    const float DefaultStep = 10f;
    const float Line = 2f;
    const float RidgesSize = 12f;
    const float GhostOpacity = 0.5f;

    static readonly CanvasDraw<RidgesLook> DrawRidges = static (dc, look, info) =>
    {
        var path = info.Path();
        path.MoveTo(new Vector2(11f, 1f));
        path.LineTo(new Vector2(1f, 11f));
        path.MoveTo(new Vector2(11f, 5f));
        path.LineTo(new Vector2(5f, 11f));
        path.MoveTo(new Vector2(11f, 9f));
        path.LineTo(new Vector2(9f, 11f));
        dc.DrawPath(
            path,
            default,
            new Pen(look.Color, 1.5f, StartLineCap: PenLineCap.Round, EndLineCap: PenLineCap.Round)
        );
    };

    protected override void Render(Ui ui, in ResizeHandleProps p)
    {
        var t = ui.Theme;
        var model = ui.Remember(static () => new Resizer());
        model.Host = ui.Self(p);
        model.Shade = ui.State(default(PreviewShade));
        var root = ui.Ref();
        var sizes = !p.OnValueChange.IsEmpty;
        var dragged = ui.IsCapturing;
        var edge = p.Variant == ResizeHandleVariant.Edge;
        var strips = edge && IsCorner(p.Edge) && p.Band > 0f;
        var bar = ui.Animate(Fill(t, p.Variant, ui.IsHovered, dragged || ui.IsFocusVisible));
        using (
            ui.Box(
                width: p.Width,
                height: p.Height,
                background: edge ? default(Paint) : bar,
                cursor: CursorOf(p.Edge),
                focusable: sizes,
                onDrag: strips ? default : model.Drag,
                onKeyDown: sizes ? model.KeyDown : default,
                onFocusChange: sizes ? model.FocusChange : default,
                nodeRef: root
            )
        )
        {
            if (strips)
                ui.Strips(edge: p.Edge, band: p.Band, drag: model.Drag);
            if (edge)
            {
                var lit = ui.Animate(ui.IsHovered || dragged ? 1f : 0f);
                var inset = Sides(p.Edge, p.LightsInward ? MathF.Max(0f, p.Band - Line) : 0f);
                using (
                    ui.Box(
                        border: new Border(Sides(p.Edge, Line), t.Color(ColorToken.BorderAccent)),
                        opacity: lit,
                        pointerEvents: PointerEvents.None,
                        position: Position.Absolute,
                        left: inset.Left,
                        top: inset.Top,
                        right: inset.Right,
                        bottom: inset.Bottom
                    )
                ) { }
                if (p.Ridges && p.Edge == ResizeEdge.BottomRight)
                    ui.Canvas(
                        state: new RidgesLook(
                            t.Color(lit > 0f ? ColorToken.FgAccentHover : ColorToken.FgAccent)
                        ),
                        draw: DrawRidges,
                        opacity: 0.6f + 0.4f * lit,
                        pointerEvents: PointerEvents.None,
                        position: Position.Absolute,
                        right: 2f,
                        bottom: 2f,
                        width: RidgesSize,
                        height: RidgesSize
                    );
            }
        }
        if (model.Shade.Value is { Shown: true } shade)
        {
            var size = root.Bounds;
            var across = p.Edge is ResizeEdge.Left or ResizeEdge.Right;
            using (
                ui.Positioner(
                    anchor: root,
                    placement: PlacementMode.Absolute,
                    offset: across ? new Vector2(shade.Offset, 0f) : new Vector2(0f, shade.Offset),
                    hit: LayerHit.PassThrough
                )
            )
            using (
                ui.Box(
                    background: t.Color(ColorToken.BorderFocus).WithOpacity(GhostOpacity),
                    pointerEvents: PointerEvents.None,
                    width: size.Width,
                    height: size.Height
                )
            ) { }
        }
    }

    static Color Fill(UiTheme t, ResizeHandleVariant variant, bool hovered, bool lit)
    {
        if (lit)
            return t.Color(ColorToken.BorderFocus);
        if (variant == ResizeHandleVariant.Ghost)
            return hovered
                ? t.Color(ColorToken.Border)
                : t.Color(ColorToken.Border).WithOpacity(0f);
        if (variant == ResizeHandleVariant.GhostMuted)
            return hovered
                ? t.Color(ColorToken.BorderMuted)
                : t.Color(ColorToken.BorderMuted).WithOpacity(0f);
        return t.Color(hovered ? ColorToken.BorderHover : ColorToken.Border);
    }

    static Cursor CursorOf(ResizeEdge edge) =>
        edge switch
        {
            ResizeEdge.Left or ResizeEdge.Right => Cursors.SizeWe,
            ResizeEdge.Top or ResizeEdge.Bottom => Cursors.SizeNs,
            ResizeEdge.TopLeft or ResizeEdge.BottomRight => Cursors.SizeNwSe,
            _ => Cursors.SizeNeSw,
        };

    static bool IsCorner(ResizeEdge edge) => edge >= ResizeEdge.TopLeft;

    static Thickness Sides(ResizeEdge edge, float width) =>
        new(
            edge is ResizeEdge.Left or ResizeEdge.TopLeft or ResizeEdge.BottomLeft ? width : 0f,
            edge is ResizeEdge.Top or ResizeEdge.TopLeft or ResizeEdge.TopRight ? width : 0f,
            edge is ResizeEdge.Right or ResizeEdge.TopRight or ResizeEdge.BottomRight ? width : 0f,
            edge is ResizeEdge.Bottom or ResizeEdge.BottomLeft or ResizeEdge.BottomRight
                ? width
                : 0f
        );

    static Vector2 Growth(ResizeEdge edge) =>
        edge switch
        {
            ResizeEdge.Right => Vector2.UnitX,
            ResizeEdge.Left => -Vector2.UnitX,
            ResizeEdge.Bottom => Vector2.UnitY,
            ResizeEdge.Top => -Vector2.UnitY,
            _ => Vector2.Zero,
        };

    readonly record struct RidgesLook(Color Color);

    readonly record struct PreviewShade(bool Shown, float Offset);

    [FramePath]
    sealed class Resizer
    {
        internal readonly Handler<DragEvent> Drag;
        internal readonly Handler<KeyEvent> KeyDown;
        internal readonly Callback<FocusChange> FocusChange;
        internal Host<ResizeHandleProps> Host;
        internal State<PreviewShade> Shade = null!;
        bool _active;
        float _start;
        float _at;

        public Resizer()
        {
            Drag = Handler<DragEvent>.Of(this, static (m, ref e) => m.Dragged(ref e));
            KeyDown = Handler<KeyEvent>.Of(this, static (m, ref e) => m.Pressed(ref e));
            FocusChange = Callback<FocusChange>.Of(
                this,
                static (m, change) =>
                {
                    if (!change.Focused && m._active)
                        m.Cancel(in m.Host.Props);
                }
            );
        }

        void Dragged(ref DragEvent e)
        {
            ref readonly var p = ref Host.Props;
            if (!p.OnValueChange.IsEmpty)
            {
                switch (e.Phase)
                {
                    case DragPhase.Start:
                        _active = Growth(p.Edge) != Vector2.Zero;
                        _start = _at = p.Value;
                        break;
                    case DragPhase.Move when _active:
                        Follow(in p, e.Offset);
                        break;
                    case DragPhase.End when _active:
                        Finish(in p);
                        break;
                    case DragPhase.Cancel when _active:
                        Cancel(in p);
                        break;
                }
            }
            if (!p.OnDrag.IsEmpty)
                p.OnDrag.Invoke(ref e);
        }

        void Follow(in ResizeHandleProps p, Vector2 offset)
        {
            var raw = _start + Vector2.Dot(Growth(p.Edge), offset);
            var step = p.DragStep ?? 0f;
            var next = Clamp(
                in p,
                step > 0f ? MathF.Round(raw / step, MidpointRounding.AwayFromZero) * step : raw
            );
            if (next == _at)
                return;
            _at = next;
            if (p.Preview)
                Shade.Value = new PreviewShade(
                    true,
                    (next - _start) * (Growth(p.Edge).X + Growth(p.Edge).Y)
                );
            else
                p.OnValueChange.Invoke(next);
        }

        void Finish(in ResizeHandleProps p)
        {
            _active = false;
            if (p.Preview)
            {
                Shade.Value = default;
                if (_at != _start)
                    p.OnValueChange.Invoke(_at);
            }
            if (_at != _start)
                p.OnValueCommit.Invoke(_at);
        }

        void Cancel(in ResizeHandleProps p)
        {
            _active = false;
            if (p.Preview)
                Shade.Value = default;
            else if (_at != _start)
                p.OnValueChange.Invoke(_start);
        }

        void Pressed(ref KeyEvent e)
        {
            ref readonly var p = ref Host.Props;
            if (e.Modifiers != ModifierKeys.None)
                return;
            if (e.Key == Key.Escape)
            {
                if (!_active)
                    return;
                Cancel(in p);
                e.PreventDefault();
                return;
            }
            var growth = Growth(p.Edge);
            var toward = e.Key switch
            {
                Key.Right => growth.X,
                Key.Left => -growth.X,
                Key.Down => growth.Y,
                Key.Up => -growth.Y,
                _ => 0f,
            };
            if (toward == 0f)
                return;
            e.PreventDefault();
            if (_active)
                return;
            var next = Clamp(in p, p.Value + toward * (p.Step ?? DefaultStep));
            if (next == p.Value)
                return;
            p.OnValueChange.Invoke(next);
            p.OnValueCommit.Invoke(next);
        }

        static float Clamp(in ResizeHandleProps p, float value) =>
            MathF.Max(p.Min, MathF.Min(value, p.Max ?? float.PositiveInfinity));
    }
}

[FramePath]
internal static class ResizeHandleParts
{
    public static void Strips(
        this Ui ui,
        ResizeEdge edge,
        float band,
        Handler<DragEvent> drag,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var top = edge is ResizeEdge.TopLeft or ResizeEdge.TopRight;
        var left = edge is ResizeEdge.TopLeft or ResizeEdge.BottomLeft;
        using (ui.Fragment(key: key, file: file, line: line))
        {
            using (
                ui.Box(
                    onDrag: drag,
                    position: Position.Absolute,
                    left: 0f,
                    right: 0f,
                    top: top ? 0f : Length.Auto,
                    bottom: top ? Length.Auto : 0f,
                    height: band
                )
            ) { }
            using (
                ui.Box(
                    onDrag: drag,
                    position: Position.Absolute,
                    top: 0f,
                    bottom: 0f,
                    left: left ? 0f : Length.Auto,
                    right: left ? Length.Auto : 0f,
                    width: band
                )
            ) { }
        }
    }
}

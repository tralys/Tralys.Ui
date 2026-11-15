using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<ResizeHandle>("Layout")]
static class ResizeHandleStories
{
    const float Handle = 4f;

    [FramePath]
    public static void Default(Ui ui)
    {
        var left = ui.State((400f - Handle) / 2f);
        using (
            ui.Grid(
                columns: new GridTracks(Track.Px(left.Value), Track.Px(Handle), Track.Fr(1f)),
                width: 400,
                height: 160
            )
        )
        {
            Panel(ui, "Left panel");
            ui.ResizeHandle(value: left.Value, max: 400f - Handle, onValueChange: left.Setter);
            Panel(ui, "Right panel");
        }
    }

    [FramePath]
    public static void Preview(Ui ui)
    {
        var top = ui.State((160f - Handle) / 2f);
        using (
            ui.Grid(
                rows: new GridTracks(Track.Px(top.Value), Track.Px(Handle), Track.Fr(1f)),
                width: 400,
                height: 160
            )
        )
        {
            Panel(ui, "A drag moves the preview; the rows resize on release");
            ui.ResizeHandle(
                edge: ResizeEdge.Bottom,
                value: top.Value,
                max: 160f - Handle,
                preview: true,
                onValueChange: top.Setter
            );
            Panel(ui, "Bottom panel");
        }
    }

    [FramePath]
    public static void Edges(Ui ui)
    {
        var bounds = ui.State(new Rect(80f, 40f, 240f, 160f));
        var corners = ui.Remember(static () => new Corners());
        corners.Bounds = bounds;
        var b = bounds.Value;
        using (ui.Box(width: Sizable.Area.Width, height: Sizable.Area.Height))
        using (
            ui.Grid(
                position: Position.Absolute,
                left: b.X,
                top: b.Y,
                width: b.Width,
                height: b.Height
            )
        )
        {
            Panel(ui, "Drag an edge or corner");
            ui.ResizeHandle(
                edge: ResizeEdge.Top,
                variant: ResizeHandleVariant.Edge,
                value: b.Height,
                min: Sizable.Min,
                max: b.Bottom,
                onValueChange: Sizable.Top(bounds),
                height: Band,
                alignSelf: AlignSelf.Start
            );
            ui.ResizeHandle(
                edge: ResizeEdge.Bottom,
                variant: ResizeHandleVariant.Edge,
                value: b.Height,
                min: Sizable.Min,
                max: Sizable.Area.Height - b.Y,
                onValueChange: Sizable.Bottom(bounds),
                height: Band,
                alignSelf: AlignSelf.End
            );
            ui.ResizeHandle(
                edge: ResizeEdge.Left,
                variant: ResizeHandleVariant.Edge,
                value: b.Width,
                min: Sizable.Min,
                max: b.Right,
                onValueChange: Sizable.Left(bounds),
                width: Band,
                justifySelf: AlignSelf.Start
            );
            ui.ResizeHandle(
                edge: ResizeEdge.Right,
                variant: ResizeHandleVariant.Edge,
                value: b.Width,
                min: Sizable.Min,
                max: Sizable.Area.Width - b.X,
                onValueChange: Sizable.Right(bounds),
                width: Band,
                justifySelf: AlignSelf.End
            );
            ui.ResizeHandle(
                edge: ResizeEdge.TopLeft,
                variant: ResizeHandleVariant.Edge,
                band: Band,
                onDrag: corners.TopLeft,
                width: Corner,
                height: Corner,
                alignSelf: AlignSelf.Start,
                justifySelf: AlignSelf.Start
            );
            ui.ResizeHandle(
                edge: ResizeEdge.TopRight,
                variant: ResizeHandleVariant.Edge,
                band: Band,
                onDrag: corners.TopRight,
                width: Corner,
                height: Corner,
                alignSelf: AlignSelf.Start,
                justifySelf: AlignSelf.End
            );
            ui.ResizeHandle(
                edge: ResizeEdge.BottomLeft,
                variant: ResizeHandleVariant.Edge,
                band: Band,
                onDrag: corners.BottomLeft,
                width: Corner,
                height: Corner,
                alignSelf: AlignSelf.End,
                justifySelf: AlignSelf.Start
            );
            ui.ResizeHandle(
                edge: ResizeEdge.BottomRight,
                variant: ResizeHandleVariant.Edge,
                band: Band,
                ridges: true,
                onDrag: corners.BottomRight,
                width: Corner,
                height: Corner,
                alignSelf: AlignSelf.End,
                justifySelf: AlignSelf.End
            );
        }
    }

    const float Band = 8f;
    const float Corner = 16f;

    [FramePath]
    static void Panel(
        Ui ui,
        TextValue text,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                background: t.Color(ColorToken.BgCanvas),
                border: new Border(1f, t.Color(ColorToken.Border)),
                key: key,
                file: file,
                line: line
            )
        )
            ui.Text(text, fontWeight: t.Weight(WeightToken.Interface), wrap: true);
    }
}

[FramePath]
static class Sizable
{
    public const float Min = 80f;

    public static readonly Size Area = new(400f, 240f);

    public static Callback<float> Top(State<Rect> bounds) =>
        Callback<float>.Of(
            bounds,
            static (s, height) =>
            {
                var b = s.Peek;
                s.Value = new Rect(b.X, b.Bottom - height, b.Width, height);
            }
        );

    public static Callback<float> Bottom(State<Rect> bounds) =>
        Callback<float>.Of(bounds, static (s, height) => s.Value = s.Peek with { Height = height });

    public static Callback<float> Left(State<Rect> bounds) =>
        Callback<float>.Of(
            bounds,
            static (s, width) =>
            {
                var b = s.Peek;
                s.Value = new Rect(b.Right - width, b.Y, width, b.Height);
            }
        );

    public static Callback<float> Right(State<Rect> bounds) =>
        Callback<float>.Of(bounds, static (s, width) => s.Value = s.Peek with { Width = width });
}

[FramePath]
sealed class Corners
{
    public readonly Handler<DragEvent> TopLeft;
    public readonly Handler<DragEvent> TopRight;
    public readonly Handler<DragEvent> BottomLeft;
    public readonly Handler<DragEvent> BottomRight;
    public State<Rect> Bounds = null!;
    Rect _start;

    public Corners()
    {
        TopLeft = Handler<DragEvent>.Of(this, static (c, ref e) => c.Dragged(ref e, true, true));
        TopRight = Handler<DragEvent>.Of(this, static (c, ref e) => c.Dragged(ref e, false, true));
        BottomLeft = Handler<DragEvent>.Of(
            this,
            static (c, ref e) => c.Dragged(ref e, true, false)
        );
        BottomRight = Handler<DragEvent>.Of(
            this,
            static (c, ref e) => c.Dragged(ref e, false, false)
        );
    }

    void Dragged(ref DragEvent e, bool left, bool top)
    {
        switch (e.Phase)
        {
            case DragPhase.Start:
                _start = Bounds.Peek;
                break;
            case DragPhase.Move:
                Bounds.Value = Moved(e.Offset, left, top);
                break;
            case DragPhase.Cancel:
                Bounds.Value = _start;
                break;
        }
    }

    Rect Moved(System.Numerics.Vector2 by, bool left, bool top)
    {
        var area = Sizable.Area;
        var x0 = _start.X;
        var y0 = _start.Y;
        var x1 = _start.Right;
        var y1 = _start.Bottom;
        if (left)
            x0 = Math.Clamp(x0 + by.X, 0f, x1 - Sizable.Min);
        else
            x1 = Math.Clamp(x1 + by.X, x0 + Sizable.Min, area.Width);
        if (top)
            y0 = Math.Clamp(y0 + by.Y, 0f, y1 - Sizable.Min);
        else
            y1 = Math.Clamp(y1 + by.Y, y0 + Sizable.Min, area.Height);
        return Rect.FromEdges(x0, y0, x1, y1);
    }
}

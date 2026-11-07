using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.Canvas>("Media")]
static class CanvasStories
{
    readonly record struct Ink(Paint Fill, Paint Stroke, float Thickness = 1);

    readonly record struct Stroked(
        Paint Stroke,
        float Thickness,
        PenLineCap Start = PenLineCap.Flat,
        PenLineCap End = PenLineCap.Flat,
        PenLineJoin Join = PenLineJoin.Miter,
        float MiterLimit = 10,
        DashArray? Dashes = null,
        PenLineCap DashCap = PenLineCap.Flat,
        float DashOffset = 0
    )
    {
        public Pen Pen =>
            new(Stroke, Thickness, Join, Start, End, MiterLimit)
            {
                DashArray = Dashes,
                DashCap = DashCap,
                DashOffset = DashOffset,
            };
    }

    readonly record struct Line(Vector2 From, Vector2 To, Stroked Stroke);

    readonly record struct Figure(int Shape, Ink Ink, Stroked Stroke = default);

    static readonly DashArray OneTwo = new(1, 2);
    static readonly DashArray FourTwo = new(4, 2);
    static readonly DashArray ZeroTwo = new(0, 2);
    static readonly DashArray SixTwoOneTwo = new(6, 2, 1, 2);
    static readonly DashArray ThreeTwo = new(3, 2);

    static readonly Paint Sky = Paint.Radial(
        new GradientStops([new(0f, Color.Parse("#FFE0F2FE")), new(1f, Color.Parse("#FF0369A1"))]),
        new Vector2(0.35f, 0.35f),
        new Vector2(0.65f, 0.65f)
    );

    static readonly Paint[] Fills =
    [
        Color.Parse("#FF3B82F6"),
        Paint.Linear(
            new GradientStops([
                new(0f, Color.Parse("#FF0EA5E9")),
                new(1f, Color.Parse("#FF6366F1")),
            ]),
            new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f)
        ),
        Paint.Linear(
            new GradientStops([
                new(0f, Color.Parse("#FFF59E0B")),
                new(0.5f, Color.Parse("#FFEF4444")),
                new(1f, Color.Parse("#FF7C3AED")),
            ]),
            new Vector2(0f, 0f),
            new Vector2(1f, 1f)
        ),
        Paint.Radial(
            new GradientStops([
                new(0f, Color.Parse("#FFFDE68A")),
                new(1f, Color.Parse("#FFB45309")),
            ]),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f)
        ),
        Paint.Radial(
            new GradientStops([
                new(0f, Color.Parse("#FFFFFFFF")),
                new(0.4f, Color.Parse("#FF22C55E")),
                new(1f, Color.Parse("#00166534")),
            ]),
            new Vector2(0.25f, 0.3f),
            new Vector2(0.8f, 0.9f)
        ),
    ];

    static readonly CanvasDraw<Ink> Ellipse = static (dc, ink, info) =>
        dc.DrawEllipse(new Rect(info.Size), ink.Fill, ink.Thickness, ink.Stroke);

    static readonly CanvasDraw<Ink> UniformEllipse = static (dc, ink, info) =>
    {
        var side = MathF.Min(info.Size.Width, info.Size.Height);
        dc.DrawEllipse(new Rect(0, 0, side, side), ink.Fill, ink.Thickness, ink.Stroke);
    };

    static readonly CanvasDraw<Stroked> DashedEllipse = static (dc, stroke, info) =>
    {
        var half = stroke.Thickness / 2;
        var path = info.Path();
        path.AddEllipse(
            new Rect(
                half,
                half,
                info.Size.Width - stroke.Thickness,
                info.Size.Height - stroke.Thickness
            )
        );
        dc.DrawPath(path, default, stroke.Pen);
    };

    static readonly CanvasDraw<Ink> Box = static (dc, ink, info) =>
        dc.DrawBox(
            new Rect(info.Size),
            default,
            new Thickness(ink.Thickness),
            ink.Fill,
            ink.Stroke
        );

    static readonly CanvasDraw<Ink> RoundedBox = static (dc, ink, info) =>
        dc.DrawBox(
            new Rect(info.Size),
            new CornerRadius(16 + ink.Thickness / 2),
            new Thickness(ink.Thickness),
            ink.Fill,
            ink.Stroke
        );

    static readonly CanvasDraw<Paint> Pill = static (dc, fill, info) =>
    {
        var path = info.Path();
        path.AddRoundedRectangle(new Rect(info.Size), new Vector2(60, 40));
        dc.DrawPath(path, fill, default);
    };

    static readonly CanvasDraw<Stroked> DashedBox = static (dc, stroke, info) =>
    {
        var half = stroke.Thickness / 2;
        var path = info.Path();
        path.AddRectangle(
            new Rect(
                half,
                half,
                info.Size.Width - stroke.Thickness,
                info.Size.Height - stroke.Thickness
            )
        );
        dc.DrawPath(path, default, stroke.Pen);
    };

    static readonly CanvasDraw<Paint> Rounded = static (dc, fill, info) =>
        dc.DrawRoundedRectangle(fill, new Rect(info.Size), new CornerRadius(6));

    static readonly CanvasDraw<Line> Segment = static (dc, line, info) =>
    {
        var path = info.Path();
        path.MoveTo(line.From);
        path.LineTo(line.To);
        dc.DrawPath(path, default, line.Stroke.Pen);
    };

    static readonly CanvasDraw<Figure> Drawn = static (dc, figure, info) =>
    {
        var path = info.Path();
        switch (figure.Shape)
        {
            case Shapes.Triangle:
                path.MoveTo(new(0, 100));
                path.LineTo(new(50, 0));
                path.LineTo(new(100, 100));
                path.Close();
                break;
            case Shapes.Steps:
                path.MoveTo(new(0, 0));
                path.LineTo(new(100, 0));
                path.LineTo(new(100, 100));
                path.LineTo(new(50, 100));
                path.LineTo(new(50, 50));
                path.LineTo(new(0, 50));
                path.Close();
                break;
            case Shapes.Curves:
                path.MoveTo(new(0, 50));
                path.CubicTo(new(0, 0), new(50, 0), new(50, 50));
                path.CubicTo(new(50, 100), new(100, 100), new(100, 50));
                break;
            case Shapes.Quadratics:
                path.MoveTo(new(0, 100));
                path.QuadraticTo(new(25, 0), new(50, 50));
                path.QuadraticTo(new(75, 100), new(100, 0));
                break;
            case Shapes.Arcs:
                path.MoveTo(new(10, 50));
                path.ArcTo(new(90, 50), new(40, 40), 0, isLargeArc: true, clockwise: true);
                path.ArcTo(new(10, 50), new(40, 20), 0, isLargeArc: false, clockwise: true);
                path.Close();
                break;
            case Shapes.Squares:
                for (var i = 0; i < 3; i++)
                    path.AddRectangle(new Rect(i * 40, i * 40, 20, 20));
                break;
            case Shapes.StarEvenOdd or Shapes.StarNonzero:
                path.FillRule =
                    figure.Shape == Shapes.StarEvenOdd
                        ? Rendering.FillRule.EvenOdd
                        : Rendering.FillRule.Nonzero;
                path.MoveTo(new(50, 0));
                path.LineTo(new(79, 90));
                path.LineTo(new(2, 35));
                path.LineTo(new(98, 35));
                path.LineTo(new(21, 90));
                path.Close();
                break;
            case Shapes.Chevron:
                path.MoveTo(new(0, 90));
                path.LineTo(new(50, 10));
                path.LineTo(new(100, 90));
                break;
            case Shapes.FlatChevron:
                path.MoveTo(new(0, 90));
                path.LineTo(new(50, 60));
                path.LineTo(new(100, 90));
                break;
            case Shapes.RoundedRectangle:
                path.AddRoundedRectangle(new Rect(0, 0, 100, 70), new Vector2(10, 10));
                break;
            case Shapes.Oval:
                path.AddEllipse(new Rect(0, 20, 100, 60));
                break;
            case Shapes.Union or Shapes.Intersection or Shapes.Xor:
                path.AddEllipse(new Rect(0, 15, 70, 70));
                var right = info.Path();
                right.AddEllipse(new Rect(30, 15, 70, 70));
                path.Combine(
                    right,
                    figure.Shape switch
                    {
                        Shapes.Union => GeometryCombineMode.Union,
                        Shapes.Intersection => GeometryCombineMode.Intersect,
                        _ => GeometryCombineMode.Xor,
                    }
                );
                break;
            case Shapes.Exclusion:
                path.AddRectangle(new Rect(0, 15, 100, 70));
                var hole = info.Path();
                hole.AddEllipse(new Rect(25, 25, 50, 50));
                path.Combine(hole, GeometryCombineMode.Exclude);
                break;
        }
        var pen =
            figure.Stroke.Thickness > 0
                ? figure.Stroke.Pen
                : new Pen(figure.Ink.Stroke, figure.Ink.Thickness);
        dc.DrawPath(path, figure.Ink.Fill, pen);
    };

    static readonly CanvasDraw<(Paint Fill, Stretch Stretch)> Fitted = static (dc, s, info) =>
    {
        var path = info.Path();
        Star(path);
        var size = info.Size;
        var (scaleX, scaleY) = s.Stretch switch
        {
            Stretch.Fill => (size.Width / 20, size.Height / 20),
            Stretch.Uniform => Both(MathF.Min(size.Width, size.Height) / 20),
            Stretch.UniformToFill => Both(MathF.Max(size.Width, size.Height) / 20),
            _ => (1f, 1f),
        };
        if (s.Stretch != Stretch.None)
        {
            var left = s.Stretch == Stretch.Fill ? 0 : (size.Width - 20 * scaleX) / 2;
            var top = s.Stretch == Stretch.Uniform ? (size.Height - 20 * scaleY) / 2 : 0;
            dc.PushTransform(
                Matrix3x2.CreateTranslation(-2, -2)
                    * Matrix3x2.CreateScale(scaleX, scaleY)
                    * Matrix3x2.CreateTranslation(left, top)
            );
        }
        dc.DrawPath(path, s.Fill, default);
        if (s.Stretch != Stretch.None)
            dc.Pop();
    };

    static (float, float) Both(float scale) => (scale, scale);

    static void Star(PathBuilder path)
    {
        path.MoveTo(new(12, 2));
        path.LineTo(new(15, 9));
        path.LineTo(new(22, 9));
        path.LineTo(new(16, 14));
        path.LineTo(new(18, 22));
        path.LineTo(new(12, 17));
        path.LineTo(new(6, 22));
        path.LineTo(new(8, 14));
        path.LineTo(new(2, 9));
        path.LineTo(new(9, 9));
        path.Close();
    }

    static class Shapes
    {
        public const int Triangle = 0;
        public const int Steps = 1;
        public const int Curves = 2;
        public const int Quadratics = 3;
        public const int Arcs = 4;
        public const int Squares = 5;
        public const int StarEvenOdd = 6;
        public const int StarNonzero = 7;
        public const int Chevron = 8;
        public const int FlatChevron = 9;
        public const int RoundedRectangle = 10;
        public const int Oval = 11;
        public const int Union = 12;
        public const int Intersection = 13;
        public const int Xor = 14;
        public const int Exclusion = 15;
    }

    [FramePath]
    public static void Ellipses(Ui ui)
    {
        var t = ui.Theme;
        using (ui.Box(gap: 16, align: Align.Start))
        {
            ui.Canvas(
                state: new Ink(t.Color(ColorToken.BgAccent), default, 0),
                draw: Ellipse,
                width: 80,
                height: 80
            );
            ui.Canvas(
                state: new Ink(t.Color(ColorToken.BgRaised), t.Color(ColorToken.Border), 2),
                draw: Ellipse,
                width: 140,
                height: 80
            );
            ui.Canvas(
                state: new Ink(default, t.Color(ColorToken.BorderDanger), 6),
                draw: Ellipse,
                width: 80,
                height: 80
            );
            ui.Canvas(
                state: new Stroked(
                    t.Color(ColorToken.Fg),
                    3,
                    Dashes: OneTwo,
                    DashCap: PenLineCap.Round
                ),
                draw: DashedEllipse,
                width: 80,
                height: 80
            );
            ui.Canvas(state: new Ink(Sky, default, 0), draw: Ellipse, width: 80, height: 80);
        }
    }

    [FramePath]
    public static void EllipseSizes(Ui ui)
    {
        var t = ui.Theme;
        var outlined = new Ink(t.Color(ColorToken.BgAccentMuted), t.Color(ColorToken.BorderAccent));
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "An ellipse with no size fills its cell: stretched, centred and uniform in a 200×100 cell"
            );
            using (ui.Box(gap: 16))
            {
                using (Cell(ui, 200, 100))
                    ui.Canvas(state: outlined, draw: Ellipse, grow: 1);
                using (Cell(ui, 200, 100, centred: true))
                    ui.Canvas(
                        state: new Ink(t.Color(ColorToken.BgAccent), default, 0),
                        draw: Ellipse,
                        width: 40,
                        height: 40
                    );
                using (Cell(ui, 200, 100))
                    ui.Canvas(state: outlined, draw: UniformEllipse, grow: 1);
            }
        }
    }

    [FramePath]
    static Scope Cell(
        Ui ui,
        float width,
        float height,
        bool centred = false,
        bool clip = false,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Box(
            align: centred ? Align.Center : Align.Stretch,
            justify: centred ? Justify.Center : Justify.Start,
            width: width,
            height: height,
            overflow: clip ? Overflow.Clip : Overflow.Visible,
            background: ui.Theme.Color(ColorToken.BgSunken),
            key: key,
            file: file,
            line: line
        );

    [FramePath]
    public static void Rectangles(Ui ui)
    {
        var t = ui.Theme;
        using (ui.Box(gap: 16, align: Align.Start))
        {
            ui.Canvas(
                state: new Ink(t.Color(ColorToken.BgAccent), default, 0),
                draw: Box,
                width: 120,
                height: 80
            );
            ui.Canvas(
                state: new Ink(default, t.Color(ColorToken.BorderAccent), 3),
                draw: Box,
                width: 120,
                height: 80
            );
            ui.Canvas(
                state: new Ink(t.Color(ColorToken.BgRaised), t.Color(ColorToken.Border), 2),
                draw: RoundedBox,
                width: 120,
                height: 80
            );
            ui.Canvas(
                state: (Paint)t.Color(ColorToken.BgSuccess),
                draw: Pill,
                width: 120,
                height: 80
            );
            ui.Canvas(
                state: new Stroked(t.Color(ColorToken.Fg), 2, Dashes: FourTwo),
                draw: DashedBox,
                width: 120,
                height: 80
            );
        }
    }

    [FramePath]
    public static void Gradients(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "Solid, linear across, linear diagonal with three stops, radial, radial off-centre"
            );
            using (ui.Box(gap: 16))
            {
                for (var i = 0; i < Fills.Length; i++)
                    ui.Canvas(state: Fills[i], draw: Rounded, width: 120, height: 80, key: i);
            }
        }
    }

    [FramePath]
    public static void LineCaps(Ui ui)
    {
        var t = ui.Theme;
        var accent = t.Color(ColorToken.BgAccent);
        var from = new Vector2(12, 16);
        var to = new Vector2(260, 16);
        using (
            ui.Grid(
                columns: new(Track.Auto, Track.Auto),
                rows: new(32, 32, 32, 32, 48),
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            Labelled(ui, "Flat", 80, 1);
            ui.Canvas(
                state: new Line(from, to, new(accent, 12)),
                draw: Segment,
                width: 266,
                column: 2,
                row: 1
            );
            Labelled(ui, "Square", default, 2);
            ui.Canvas(
                state: new Line(from, to, new(accent, 12, PenLineCap.Square, PenLineCap.Square)),
                draw: Segment,
                width: 266,
                column: 2,
                row: 2
            );
            Labelled(ui, "Round", default, 3);
            ui.Canvas(
                state: new Line(from, to, new(accent, 12, PenLineCap.Round, PenLineCap.Round)),
                draw: Segment,
                width: 266,
                column: 2,
                row: 3
            );
            Labelled(ui, "Triangle", default, 4);
            ui.Canvas(
                state: new Line(from, to, new(accent, 12, PenLineCap.Flat, PenLineCap.Triangle)),
                draw: Segment,
                width: 266,
                column: 2,
                row: 4
            );
            Labelled(ui, "Diagonal", default, 5);
            ui.Canvas(
                state: new Line(new(12, 4), new(260, 44), new(t.Color(ColorToken.Fg), 2)),
                draw: Segment,
                width: 266,
                column: 2,
                row: 5
            );
        }
    }

    [FramePath]
    static void Labelled(
        Ui ui,
        string text,
        Length width,
        int row,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Text(
            text,
            color: ui.Theme.Color(ColorToken.FgMuted),
            wrap: true,
            width: width,
            alignSelf: AlignSelf.Center,
            column: 1,
            row: row,
            key: key,
            file: file,
            line: line
        );

    [FramePath]
    public static void Dashes(Ui ui)
    {
        var t = ui.Theme;
        var fg = t.Color(ColorToken.Fg);
        var march = ui.Loop(0.5f);
        var from = new Vector2(0, 14);
        var to = new Vector2(320, 14);
        using (
            ui.Grid(
                columns: new(Track.Auto, Track.Auto),
                rows: new(28, 28, 28, 28, 28),
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            Labelled(ui, "4 2", 160, 1);
            ui.Canvas(
                state: new Line(from, to, new(fg, 3, Dashes: FourTwo)),
                draw: Segment,
                width: 322,
                column: 2,
                row: 1
            );
            Labelled(ui, "0 2, round caps: dots", default, 2);
            ui.Canvas(
                state: new Line(
                    new(4, 14),
                    to,
                    new(fg, 4, Dashes: ZeroTwo, DashCap: PenLineCap.Round)
                ),
                draw: Segment,
                width: 322,
                column: 2,
                row: 2
            );
            Labelled(ui, "6 2 1 2", default, 3);
            ui.Canvas(
                state: new Line(from, to, new(fg, 3, Dashes: SixTwoOneTwo)),
                draw: Segment,
                width: 322,
                column: 2,
                row: 3
            );
            Labelled(ui, "4 2, offset 3", default, 4);
            ui.Canvas(
                state: new Line(from, to, new(fg, 3, Dashes: FourTwo, DashOffset: 3)),
                draw: Segment,
                width: 322,
                column: 2,
                row: 4
            );
            Labelled(ui, "Marching: offset animated", default, 5);
            ui.Canvas(
                state: new Line(
                    from,
                    to,
                    new(t.Color(ColorToken.BgAccent), 3, Dashes: FourTwo, DashOffset: -6 * march)
                ),
                draw: Segment,
                width: 322,
                column: 2,
                row: 5
            );
        }
    }

    [FramePath]
    static void Framed(
        Ui ui,
        Figure figure,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        using (
            ui.Box(
                direction: Direction.Column,
                width: 150,
                height: 150,
                padding: 16,
                background: t.Color(ColorToken.BgSunken),
                border: new(1, t.Color(ColorToken.Border)),
                key: key,
                file: file,
                line: line
            )
        )
            ui.Canvas(state: figure, draw: Drawn, grow: 1);
    }

    [FramePath]
    public static void Paths(Ui ui)
    {
        var t = ui.Theme;
        var outlined = new Ink(
            t.Color(ColorToken.BgAccentMuted),
            t.Color(ColorToken.BorderAccent),
            2
        );
        var line = new Ink(default, t.Color(ColorToken.Fg), 3);
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "Lines, horizontal and vertical runs, cubic and smooth curves, quadratics, arcs, and several figures"
            );
            using (ui.Box(gap: 12))
            {
                Framed(ui, new(Shapes.Triangle, outlined));
                Framed(ui, new(Shapes.Steps, outlined));
                Framed(ui, new(Shapes.Curves, line));
                Framed(ui, new(Shapes.Quadratics, line));
                Framed(
                    ui,
                    new(
                        Shapes.Arcs,
                        new(
                            t.Color(ColorToken.BgSuccessMuted),
                            t.Color(ColorToken.BorderSuccess),
                            2
                        )
                    )
                );
                Framed(ui, new(Shapes.Squares, new(t.Color(ColorToken.BgAccent), default, 0)));
            }
        }
    }

    [FramePath]
    public static void FillRule(Ui ui)
    {
        var t = ui.Theme;
        var ink = new Ink(t.Color(ColorToken.BgAccent), t.Color(ColorToken.Fg));
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "A star drawn in one stroke: EvenOdd leaves its centre open, Nonzero fills it"
            );
            using (ui.Box(gap: 12))
            {
                Framed(ui, new(Shapes.StarEvenOdd, ink));
                Framed(ui, new(Shapes.StarNonzero, ink));
            }
        }
    }

    [FramePath]
    public static void Joins(Ui ui)
    {
        var t = ui.Theme;
        var accent = t.Color(ColorToken.BgAccent);
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "Miter, Bevel and Round joins; a miter past its limit bevels; dashes follow the path"
            );
            using (ui.Box(gap: 12))
            {
                Framed(ui, new(Shapes.Chevron, default, new(accent, 14, Join: PenLineJoin.Miter)));
                Framed(ui, new(Shapes.Chevron, default, new(accent, 14, Join: PenLineJoin.Bevel)));
                Framed(ui, new(Shapes.Chevron, default, new(accent, 14, Join: PenLineJoin.Round)));
                Framed(ui, new(Shapes.FlatChevron, default, new(accent, 14, MiterLimit: 1)));
                Framed(
                    ui,
                    new(
                        Shapes.Curves,
                        default,
                        new(t.Color(ColorToken.Fg), 3, Dashes: ThreeTwo, DashCap: PenLineCap.Round)
                    )
                );
            }
        }
    }

    [FramePath]
    public static void Geometries(Ui ui)
    {
        var t = ui.Theme;
        var outlined = new Ink(t.Color(ColorToken.BgAccentMuted), t.Color(ColorToken.BorderAccent));
        var solid = new Ink(t.Color(ColorToken.BgAccent), default, 0);
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "Paths: a rectangle, an ellipse, and two shapes' union, intersection, xor and exclusion"
            );
            using (ui.Box(gap: 12))
            {
                Framed(ui, new(Shapes.RoundedRectangle, outlined));
                Framed(ui, new(Shapes.Oval, outlined));
                Framed(ui, new(Shapes.Union, solid));
                Framed(ui, new(Shapes.Intersection, solid));
                Framed(ui, new(Shapes.Xor, solid));
                Framed(ui, new(Shapes.Exclusion, solid));
            }
        }
    }

    [FramePath]
    public static void PathFit(Ui ui)
    {
        var t = ui.Theme;
        var warning = (Paint)t.Color(ColorToken.FgWarning);
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "One 24-unit glyph path in a 100×60 cell: None, Fill, Uniform, UniformToFill"
            );
            using (ui.Box(gap: 12))
            {
                using (Cell(ui, 100, 60, clip: true))
                    ui.Canvas(state: (warning, Stretch.None), draw: Fitted, grow: 1);
                using (Cell(ui, 100, 60, clip: true))
                    ui.Canvas(state: (warning, Stretch.Fill), draw: Fitted, grow: 1);
                using (Cell(ui, 100, 60, clip: true))
                    ui.Canvas(state: (warning, Stretch.Uniform), draw: Fitted, grow: 1);
                using (Cell(ui, 100, 60, clip: true))
                    ui.Canvas(state: (warning, Stretch.UniformToFill), draw: Fitted, grow: 1);
            }
        }
    }
}

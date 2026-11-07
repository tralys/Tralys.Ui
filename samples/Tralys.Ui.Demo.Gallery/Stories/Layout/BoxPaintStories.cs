using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Fonts;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.Box>("Layout")]
static class BoxPaintStories
{
    const int MaskSteps = 32;

    static readonly Color Black = Color.FromArgb(255, 0, 0, 0);
    static readonly Color Clear = Color.FromArgb(0, 0, 0, 0);

    static readonly Paint Falloff = Paint.Radial(
        new GradientStops([new(0f, Black), new(0.6f, Black), new(1f, Clear)]),
        new Vector2(0.5f, 0.5f),
        new Vector2(0.5f, 0.5f)
    );

    static readonly Paint EdgesAcross = Paint.Linear(
        new GradientStops([new(0f, Clear), new(0.2f, Black), new(0.8f, Black), new(1f, Clear)]),
        new Vector2(0f, 0f),
        new Vector2(1f, 0f)
    );

    static readonly Paint EdgesDown = Paint.Linear(
        new GradientStops([new(0f, Clear), new(0.25f, Black), new(0.75f, Black), new(1f, Clear)]),
        new Vector2(0f, 0f),
        new Vector2(0f, 1f)
    );

    static readonly Paint[] Wipes = WipeSteps();

    static readonly Paint Backdrop = Paint.FromImage(new PaintImage("backdrop.jpg"));

    static readonly Paint BackdropAtBottom = Paint.FromImage(
        new PaintImage("backdrop.jpg") { Stretch = Stretch.Uniform, AlignmentY = AlignmentY.Bottom }
    );

    static readonly Paint SpriteCell = Paint.FromImage(
        new PaintImage("atlas.png")
        {
            Viewbox = new Rect(64, 0, 64, 64),
            ViewboxUnits = GradientSpace.Absolute,
        }
    );

    static readonly Paint BackdropCovering = Paint.FromImage(
        new PaintImage("backdrop.jpg") { Stretch = Stretch.UniformToFill }
    );

    static readonly CanvasDraw<Paint> FilledEllipse = static (dc, fill, info) =>
        dc.DrawEllipse(new Rect(info.Size), fill, 0, default);

    static readonly CanvasDraw<Color> EllipseClip = static (dc, fill, info) =>
    {
        var path = info.Path();
        path.AddEllipse(new Rect(info.Size));
        dc.PushClip(path);
        dc.DrawRectangle(fill, new Rect(info.Size));
        dc.Pop();
    };

    static readonly CanvasDraw<Color> HoleClip = static (dc, fill, info) =>
    {
        var path = info.Path();
        path.AddRoundedRectangle(new Rect(info.Size), new Vector2(8, 8));
        var hole = info.Path();
        var centre = new Vector2(info.Size.Width / 2, info.Size.Height / 2);
        hole.AddEllipse(new Rect(centre.X - 20, centre.Y - 20, 40, 40));
        path.Combine(hole, GeometryCombineMode.Exclude);
        dc.PushClip(path);
        dc.DrawRectangle(fill, new Rect(info.Size));
        dc.Pop();
    };

    static readonly (
        Color Fill,
        float Width,
        float Height,
        float Radius,
        bool Edged,
        bool White
    )[] SwatchLooks =
    [
        (Color.Parse("#FF9900"), 24, 24, 0, false, false),
        (Color.Parse("#66CCFF"), 24, 24, -1, false, false),
        (Color.Parse("#66FF66"), 24, 24, -1, true, false),
        (Color.Parse("#C05028"), 16, 16, 999, false, true),
        (Color.Parse("#80FF55FF"), 36, 16, 999, false, false),
    ];

    static readonly Color White = Color.Parse("#FFFFFF");

    static readonly Color DropShadowColor = Color.Parse("#80000000");
    static readonly Color RingColor = Color.Parse("#FF4C8DF6");
    static readonly Color InsetColor = Color.Parse("#A0000000");

    static Paint[] WipeSteps()
    {
        var steps = new Paint[MaskSteps + 1];
        for (var i = 0; i <= MaskSteps; i++)
        {
            var middle = Color.FromArgb((byte)(255 - 255 * i / MaskSteps), 0, 0, 0);
            steps[i] = Paint.Linear(
                new GradientStops([new(0f, Black), new(0.5f, middle), new(1f, Black)]),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f)
            );
        }
        return steps;
    }

    [FramePath]
    public static void Border(Ui ui)
    {
        var t = ui.Theme;
        using (ui.Box(gap: 16, fontWeight: t.Weight(WeightToken.Interface)))
        {
            using (
                Framed(
                    ui,
                    t.Color(ColorToken.BgRaised),
                    new(1, t.Color(ColorToken.Border)),
                    default
                )
            )
                ui.Text("1 px, square", wrap: true);
            using (
                Framed(
                    ui,
                    t.Color(ColorToken.BgRaised),
                    new(2, t.Color(ColorToken.BorderAccent)),
                    t.Radius(RadiusToken.Lg)
                )
            )
                ui.Text("2 px, radius Lg", wrap: true);
            using (
                Framed(
                    ui,
                    t.Color(ColorToken.BgRaised),
                    new(new Thickness(1, 1, 1, 6), t.Color(ColorToken.BorderAccent)),
                    new CornerRadius(16, 4, 16, 4)
                )
            )
                ui.Text("Sides 1,1,1,6; corners 16,4,16,4", wrap: true);
            using (Framed(ui, default, new(3, t.Color(ColorToken.BorderDanger)), default))
                ui.Text("No background", wrap: true);
        }
    }

    [FramePath]
    static Scope Framed(
        Ui ui,
        Paint background,
        Border border,
        CornerRadius radius,
        BoxShadow shadow = default,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Box(
            direction: Direction.Column,
            width: 140,
            height: 90,
            padding: 12,
            background: background,
            border: border,
            radius: radius,
            shadow: shadow,
            key: key,
            file: file,
            line: line
        );

    [FramePath]
    public static void Shadows(Ui ui)
    {
        var t = ui.Theme;
        var radius = t.Radius(RadiusToken.Md);
        using (ui.Box(gap: 32, margin: 16, fontWeight: t.Weight(WeightToken.Interface)))
        {
            using (
                Framed(
                    ui,
                    t.Color(ColorToken.BgRaised),
                    default,
                    radius,
                    new BoxShadow(0, 4, 12, 0, DropShadowColor)
                )
            )
                ui.Text("Drop shadow", wrap: true);
            using (
                Framed(
                    ui,
                    t.Color(ColorToken.BgRaised),
                    default,
                    radius,
                    new BoxShadow(0, 0, 0, 3, RingColor)
                )
            )
                ui.Text("Spread ring", wrap: true);
            using (
                Framed(
                    ui,
                    t.Color(ColorToken.BgSunken),
                    default,
                    radius,
                    new BoxShadow(0, 2, 8, 2, InsetColor, IsInset: true)
                )
            )
                ui.Text("Inset shadow", wrap: true);
        }
    }

    [FramePath]
    public static void ThemeColours(Ui ui)
    {
        var t = ui.Theme;
        var onAccent = t.Color(ColorToken.FgOnAccent);
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 16,
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            using (Swatch(ui, t.Color(ColorToken.Bg)))
                ui.Text("Bg");
            using (Swatch(ui, t.Color(ColorToken.BgAccent)))
                ui.Text("BgAccent", color: onAccent);
            using (Swatch(ui, t.Color(ColorToken.BgDanger)))
                ui.Text("BgDanger", color: onAccent);
            using (Swatch(ui, t.Color(ColorToken.BgSuccess)))
                ui.Text("BgSuccess", color: onAccent);
        }
    }

    [FramePath]
    static Scope Swatch(
        Ui ui,
        Color background,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Box(
            direction: Direction.Column,
            width: 120,
            height: 40,
            background: background,
            key: key,
            file: file,
            line: line
        );

    [FramePath]
    public static void Clip(Ui ui)
    {
        var t = ui.Theme;
        using (ui.Box(gap: 16))
        {
            Overhung(
                ui,
                Overflow.Visible,
                new(1, t.Color(ColorToken.Border)),
                t.Radius(RadiusToken.Lg)
            );
            Overhung(
                ui,
                Overflow.Clip,
                new(1, t.Color(ColorToken.Border)),
                t.Radius(RadiusToken.Lg)
            );
        }
    }

    [FramePath]
    static void Overhung(
        Ui ui,
        Overflow overflow,
        Border border,
        CornerRadius radius,
        Color fill = default,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                direction: Direction.Column,
                width: 120,
                height: 80,
                overflow: overflow,
                border: border,
                radius: radius,
                key: key,
                file: file,
                line: line
            )
        )
        {
            using (
                ui.Box(
                    grow: 1,
                    margin: new Thickness(-20, -20, -20, -20),
                    background: fill == default ? ui.Theme.Color(ColorToken.BgAccent) : fill
                )
            ) { }
        }
    }

    [FramePath]
    public static void ClipCorners(Ui ui)
    {
        var t = ui.Theme;
        using (ui.Box(gap: 16, fontWeight: t.Weight(WeightToken.Interface)))
        {
            using (
                ui.Box(
                    direction: Direction.Column,
                    width: 120,
                    height: 80,
                    overflow: Overflow.Clip,
                    background: t.Color(ColorToken.BgRaised),
                    border: new(1, t.Color(ColorToken.Border)),
                    radius: 16
                )
            )
                ui.Text(
                    "Clipped to the inside of its corners",
                    wrap: true,
                    margin: new Thickness(-8, -4, 0, 0)
                );
            Overhung(
                ui,
                Overflow.Clip,
                new(2, t.Color(ColorToken.BorderAccent)),
                new CornerRadius(24, 4, 24, 4),
                t.Color(ColorToken.BgAccentMuted)
            );
        }
    }

    [FramePath]
    public static void ClipShapes(Ui ui)
    {
        var t = ui.Theme;
        var accent = t.Color(ColorToken.BgAccent);
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "A child clipped to its parent's bounds, and a canvas clipped to an ellipse and to a rectangle less a hole"
            );
            using (ui.Box(gap: 8))
            {
                using (Slot(ui, overflow: Overflow.Clip))
                {
                    using (
                        ui.Box(
                            width: 160,
                            height: 140,
                            margin: new Thickness(40, 40, 0, 0),
                            alignSelf: AlignSelf.Start,
                            shrink: 0,
                            background: accent
                        )
                    ) { }
                }
                using (Slot(ui))
                    ui.Canvas(state: accent, draw: EllipseClip, width: 100, height: 80);
                using (Slot(ui))
                    ui.Canvas(state: accent, draw: HoleClip, width: 100, height: 80);
            }
        }
    }

    [FramePath]
    static Scope Slot(
        Ui ui,
        Overflow overflow = default,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        return ui.Box(
            align: overflow == Overflow.Clip ? Align.Start : Align.Center,
            justify: overflow == Overflow.Clip ? Justify.Start : Justify.Center,
            width: 120,
            height: 100,
            overflow: overflow,
            background: t.Color(ColorToken.BgSunken),
            border: new(1, t.Color(ColorToken.Border)),
            key: key,
            file: file,
            line: line
        );
    }

    [FramePath]
    static Scope Card(
        Ui ui,
        Paint background = default,
        float? opacity = null,
        Transform transform = default,
        bool hidden = false,
        bool collapsed = false,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        return ui.Box(
            align: Align.Center,
            justify: Justify.Center,
            width: 64,
            height: 40,
            shrink: 0,
            background: background == default ? t.Color(ColorToken.BgAccentMuted) : background,
            border: new(1, t.Color(ColorToken.BorderAccent)),
            opacity: opacity,
            transform: transform,
            hidden: hidden,
            collapsed: collapsed,
            key: key,
            file: file,
            line: line
        );
    }

    [FramePath]
    public static void Transforms(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "Transforms about the centre: rotate, scale, skew, translate, one then another, and a matrix"
            );
            using (ui.Box(gap: 8))
            {
                using (Slot(ui))
                using (Card(ui, transform: Transform.Rotate(30)))
                    ui.Text("30°");
                using (Slot(ui))
                using (Card(ui, transform: Transform.Scale(1.5f, 0.75f)))
                    ui.Text("1.5×0.75");
                using (Slot(ui))
                using (Card(ui, transform: Transform.Skew(20, 0)))
                    ui.Text("Skew 20");
                using (Slot(ui))
                using (Card(ui, transform: Transform.Translate(20, -20)))
                    ui.Text("+20,-20");
                using (Slot(ui))
                using (Card(ui, transform: Transform.Scale(0.8f).Then(Transform.Rotate(-45))))
                    ui.Text("Group");
                using (Slot(ui))
                using (
                    Card(ui, transform: Transform.FromMatrix(new Matrix3x2(1, 0.3f, 0, 1, 0, 0)))
                )
                    ui.Text("Matrix");
            }
        }
    }

    [FramePath]
    public static void Opacity(Ui ui)
    {
        var t = ui.Theme;
        var accent = t.Color(ColorToken.BgAccent);
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption("Opacity multiplies down the tree: 1, 0.6, 0.3, and 0.5 inside 0.5");
            using (ui.Box(gap: 8))
            {
                using (Slot(ui))
                using (Card(ui, accent)) { }
                using (Slot(ui))
                using (Card(ui, accent, opacity: 0.6f)) { }
                using (Slot(ui))
                using (Card(ui, accent, opacity: 0.3f)) { }
                using (Slot(ui))
                {
                    using (
                        ui.Box(
                            align: Align.Center,
                            justify: Justify.Center,
                            width: 100,
                            height: 80,
                            opacity: 0.5f,
                            background: accent
                        )
                    )
                    using (Card(ui, t.Color(ColorToken.BgDanger), opacity: 0.5f)) { }
                }
            }
        }
    }

    [FramePath]
    public static void HiddenCollapsed(Ui ui)
    {
        var t = ui.Theme;
        var accent = t.Color(ColorToken.BgAccent);
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "The middle child shown, hidden (keeps its place) and collapsed (takes none)"
            );
            using (ui.Box(direction: Direction.Column, gap: 8))
            {
                using (ui.Box(gap: 4))
                {
                    using (Card(ui)) { }
                    using (Card(ui, accent)) { }
                    using (Card(ui)) { }
                }
                using (ui.Box(gap: 4))
                {
                    using (Card(ui)) { }
                    using (Card(ui, accent, hidden: true)) { }
                    using (Card(ui)) { }
                }
                using (ui.Box(gap: 4))
                {
                    using (Card(ui)) { }
                    using (Card(ui, accent, collapsed: true)) { }
                    using (Card(ui)) { }
                }
            }
        }
    }

    [FramePath]
    public static void Mask(Ui ui)
    {
        var t = ui.Theme;
        var wipe = ui.Loop(1, alternate: true);
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "A mask fades a box and its children by its alpha: art clear past a radial falloff, a scene fading in from its edges under one mask across it and another down its image, and a card whose mask's middle stop fades and returns"
            );
            using (ui.Box(gap: 8))
            {
                using (Scene(ui))
                {
                    using (ui.Box(grow: 1, mask: Falloff))
                        ui.Image("backdrop.jpg", width: Length.Full, height: Length.Full);
                }
                using (Scene(ui))
                {
                    using (ui.Box(grow: 1, mask: EdgesAcross))
                    using (ui.Box(grow: 1, mask: EdgesDown))
                        ui.Image("backdrop.jpg", width: Length.Full, height: Length.Full);
                }
                using (Scene(ui, centred: true))
                {
                    using (
                        ui.Box(
                            align: Align.Center,
                            justify: Justify.Center,
                            width: 160,
                            height: 64,
                            radius: t.Radius(RadiusToken.Md),
                            background: t.Color(ColorToken.BgAccent),
                            mask: Wipes[(int)MathF.Round(wipe * MaskSteps)]
                        )
                    )
                        ui.Text(
                            "Card and text, one mask",
                            color: t.Color(ColorToken.FgStrong),
                            wrap: true
                        );
                }
            }
        }
    }

    [FramePath]
    static Scope Scene(
        Ui ui,
        bool centred = false,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        return ui.Box(
            align: centred ? Align.Center : Align.Stretch,
            justify: centred ? Justify.Center : Justify.Start,
            width: 240,
            height: 136,
            background: t.Color(ColorToken.BgSunken),
            border: new(1, t.Color(ColorToken.Border)),
            key: key,
            file: file,
            line: line
        );
    }

    [FramePath]
    public static void ImagePaint(Ui ui)
    {
        var t = ui.Theme;
        var frame = new Border(1, t.Color(ColorToken.Border));
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "An image paint fills wherever a paint does: a background stretched to fill, a scene kept whole at the bottom, one frame of a sprite sheet, an ellipse, a border's band, and text"
            );
            using (ui.Box(gap: 8))
            {
                using (ui.Box(width: 160, height: 96, background: Backdrop, border: frame)) { }
                using (ui.Box(width: 96, height: 96, background: BackdropAtBottom, border: frame))
                { }
                using (ui.Box(width: 96, height: 96, background: SpriteCell, border: frame)) { }
                ui.Canvas(state: BackdropCovering, draw: FilledEllipse, width: 96, height: 96);
                using (ui.Box(width: 96, height: 96, border: new(12, BackdropCovering), radius: 16))
                { }
                ui.Text(
                    "Terrace",
                    color: BackdropCovering,
                    fontSize: 40,
                    fontWeight: FontWeight.Bold,
                    alignSelf: AlignSelf.Center
                );
            }
        }
    }

    [FramePath]
    public static void Swatches(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                width: 280,
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "A colour held as a value: square, rounded, outlined, a knob's circle and a translucent swatch"
            );
            using (ui.Box(gap: 8, align: Align.Start))
            {
                for (var i = 0; i < SwatchLooks.Length; i++)
                {
                    var (fill, width, height, radius, edged, white) = SwatchLooks[i];
                    using (
                        ui.Box(
                            width: width,
                            height: height,
                            radius: radius < 0
                                ? t.Radius(RadiusToken.Lg)
                                : new CornerRadius(radius),
                            background: fill,
                            border: edged ? new(1, t.Color(ColorToken.BorderMuted))
                                : white ? new(2, White)
                                : default,
                            key: i
                        )
                    ) { }
                }
            }
        }
    }
}

using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.Loop>("Features")]
static class LoopStories
{
    const int SheenSteps = 32;

    static readonly IEasingFunction Linear = new KeySpline();
    static readonly IEasingFunction Spline = new KeySpline(0.6f, 0f, 0.2f, 1f);
    static readonly IEasingFunction Settle = new KeySpline(0.25f, 0.1f, 0.25f, 1f);
    static readonly IEasingFunction Ratios = new AccelerateDecelerate(0.3f, 0.3f);

    static readonly Color Slate = Color.Parse("#FF1E293B");
    static readonly Color Amber = Color.Parse("#FFFBBF24");
    static readonly Color Green = Color.Parse("#FF22C55E");
    static readonly Color Yellow = Color.Parse("#FFEAB308");
    static readonly Color Red = Color.Parse("#FFEF4444");

    static readonly Paint[] Sheens = SheenSamples();

    static readonly (string Label, IEasingFunction Easing)[] Curves =
    [
        ("Linear", Linear),
        ("Cubic, ease in", Easings.CubicIn),
        ("Cubic, ease out", Easings.CubicOut),
        ("Cubic, ease in-out", Easings.CubicInOut),
        ("Back, ease out", Easings.Back(0.6f, EasingMode.Out)),
        ("Accel 0.3, decel 0.3", Ratios),
        ("KeySpline 0.6,0 0.2,1", Spline),
    ];

    static Paint[] SheenSamples()
    {
        var sheens = new Paint[SheenSteps + 1];
        var glow = Color.Parse("#FF7DD3FC");
        for (var i = 0; i <= SheenSteps; i++)
            sheens[i] = Paint.Linear(
                new GradientStops([
                    new(0f, Slate),
                    new(0.5f, Mix(Slate, glow, i / (float)SheenSteps)),
                    new(1f, Slate),
                ]),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f)
            );
        return sheens;
    }

    [FramePath]
    static Color Mix(Color from, Color to, float t) =>
        Color.FromArgb(
            Channel(from.A, to.A, t),
            Channel(from.R, to.R, t),
            Channel(from.G, to.G, t),
            Channel(from.B, to.B, t)
        );

    [FramePath]
    static byte Channel(byte from, byte to, float t) =>
        (byte)MathF.Round(from + (to - from) * Math.Clamp(t, 0f, 1f));

    [FramePath]
    public static void Repeating(Ui ui)
    {
        var t = ui.Theme;
        var md = t.Radius(RadiusToken.Md);
        var turn = ui.Loop(2);
        var fade = ui.Loop(0.8f, alternate: true);
        var squeeze = ui.Loop(0.6f, alternate: true);
        var round = ui.Loop(1.5f, alternate: true);
        var slide = ui.Loop(1.5f, alternate: true, easing: Easings.CubicInOut);
        var stretch = ui.Loop(2, alternate: true);
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 12,
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "Loops started as the story shows: a turn, a fade, a scale, a corner radius, a slide and a width, each repeating"
            );
            using (ui.Box(gap: 24))
            {
                using (
                    ui.Box(
                        width: 56,
                        height: 56,
                        radius: md,
                        background: t.Color(ColorToken.BgAccent),
                        transform: Transform.Rotate(360 * turn)
                    )
                ) { }
                using (
                    ui.Box(
                        width: 56,
                        height: 56,
                        radius: 28,
                        background: t.Color(ColorToken.BgSuccess),
                        opacity: 1 - 0.85f * fade
                    )
                ) { }
                using (
                    ui.Box(
                        width: 56,
                        height: 56,
                        radius: md,
                        background: t.Color(ColorToken.BgWarning),
                        transform: Transform.Scale(1 - 0.6f * squeeze, 1)
                    )
                ) { }
                using (
                    ui.Box(
                        width: 56,
                        height: 56,
                        radius: 4 + 24 * round,
                        background: t.Color(ColorToken.BgDanger)
                    )
                ) { }
            }
            using (Lane(ui))
                Dot(ui, 392 * slide);
            using (
                ui.Box(
                    width: 40 + 380 * stretch,
                    height: 12,
                    radius: 6,
                    background: t.Color(ColorToken.BgAccent)
                )
            ) { }
        }
    }

    [FramePath]
    static Scope Lane(
        Ui ui,
        int column = 0,
        int row = 0,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Box(
            width: 420,
            height: 28,
            radius: 14,
            background: ui.Theme.Color(ColorToken.BgSunken),
            column: column,
            row: row,
            key: key,
            file: file,
            line: line
        );

    [FramePath]
    static void Dot(
        Ui ui,
        float x,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                width: 20,
                height: 20,
                margin: 4,
                radius: 10,
                background: ui.Theme.Color(ColorToken.BgAccent),
                transform: Transform.Translate(x, 0),
                key: key,
                file: file,
                line: line
            )
        ) { }
    }

    [FramePath]
    public static void Easing(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Grid(
                columns: new(160, Track.Auto),
                rows: GridTracks.Repeat(Curves.Length, Track.Px(36)),
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            for (var i = 0; i < Curves.Length; i++)
                Eased(ui, i, key: i);
        }
    }

    [FramePath]
    static void Eased(
        Ui ui,
        int index,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var (label, easing) = Curves[index];
        using (ui.Fragment(key: key, file: file, line: line))
        {
            var x = ui.Loop(1.5f, alternate: true, easing: easing);
            ui.Text(
                label,
                color: ui.Theme.Color(ColorToken.FgMuted),
                alignSelf: AlignSelf.Center,
                column: 1,
                row: index + 1
            );
            using (Lane(ui, column: 2, row: index + 1))
                Dot(ui, 392 * x);
        }
    }

    [FramePath]
    public static void KeyFrames(Ui ui)
    {
        var t = ui.Theme;
        var hop = Hop(2.6f * ui.Loop(2.6f));
        var blink = ui.Loop(1);
        var frame = (int)(ui.Loop(0.8f) * 8);
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 12,
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "Key frames as code: linear, then a discrete jump, then a spline; a blink on a loop's second half; a sprite through its source rectangle"
            );
            using (Lane(ui))
                Dot(ui, hop);
            using (ui.Box(gap: 24, align: Align.Start))
            {
                ui.Icon(glyph: Icons.Bell, size: 32, hidden: blink >= 0.5f);
                ui.Image(
                    "atlas.png",
                    sourceRect: new Rect(Math.Min(frame, 7) * 64, 0, 64, 64),
                    slice: new NineSlice(default, TexelInset: 0.5f),
                    width: 64,
                    height: 64
                );
            }
        }
    }

    [FramePath]
    static float Hop(float seconds) =>
        seconds switch
        {
            < 0.8f => 130 * seconds / 0.8f,
            < 1.2f => 130,
            < 2.2f => 260 + 132 * (float)Settle.Ease(seconds - 1.2f),
            _ => 392,
        };

    [FramePath]
    public static void Colours(Ui ui)
    {
        var t = ui.Theme;
        var pulse = ui.Loop(0.5f, alternate: true);
        var sheen = ui.Loop(0.8f, alternate: true);
        var sweep = ui.Loop(3);
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 12,
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "Colours on a loop: an outline pulsing to amber, a gradient's middle stop glowing, and a bar sweeping through key colours"
            );
            using (ui.Box(gap: 24))
            {
                using (
                    ui.Box(
                        width: 120,
                        height: 40,
                        radius: t.Radius(RadiusToken.Md),
                        border: new(2, Mix(t.Color(ColorToken.Border), Amber, pulse))
                    )
                ) { }
                using (
                    ui.Box(
                        width: 120,
                        height: 40,
                        radius: t.Radius(RadiusToken.Md),
                        background: Sheens[(int)MathF.Round(sheen * SheenSteps)]
                    )
                ) { }
            }
            using (ui.Box(width: 420, height: 12, radius: 6, background: Swept(sweep))) { }
        }
    }

    [FramePath]
    static Color Swept(float phase) =>
        phase switch
        {
            < 0.3f => Mix(Green, Yellow, phase / 0.3f),
            < 0.8f => Mix(Yellow, Red, (phase - 0.3f) / 0.5f),
            _ => Red,
        };

    /// <summary>Speeds up over the first part of the time and slows down over the last, at a constant speed between,
    /// as WPF's acceleration and deceleration ratios do.</summary>
    sealed class AccelerateDecelerate(float accelerate, float decelerate) : IEasingFunction
    {
        public double Ease(double time)
        {
            var top = 2 / (2 - accelerate - decelerate);
            if (time < accelerate)
                return top * time * time / (2 * accelerate);
            if (time <= 1 - decelerate)
                return top * (time - accelerate / 2);
            var left = 1 - time;
            return 1 - top * left * left / (2 * decelerate);
        }
    }
}

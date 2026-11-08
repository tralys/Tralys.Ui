using System.Numerics;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct CooldownProps
{
    /// <summary>How long the cooldown lasts, in seconds.</summary>
    public float Duration { get; init; }

    /// <summary>How long is left of it, in seconds; 0 when it is ready.</summary>
    public float Remaining { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }
}

/// <summary>Shades what it holds, such as a skill's icon, while a cooldown runs: a <see cref="Shade"/> covers the share
/// of <see cref="CooldownProps.Duration"/> still <see cref="CooldownProps.Remaining"/>, under the seconds left in
/// heading digits of one width, whole seconds rounded up and tenths in the last second. Input passes through the shade
/// and the digits to what it holds.</summary>
[Children]
public sealed class Cooldown : Component<CooldownProps>
{
    protected override void Render(Ui ui, in CooldownProps p)
    {
        if (!(p.Duration >= 0f && float.IsFinite(p.Duration)))
            throw Refused(nameof(CooldownProps.Duration), p.Duration);
        if (!(p.Remaining >= 0f && float.IsFinite(p.Remaining)))
            throw Refused(nameof(CooldownProps.Remaining), p.Remaining);
        var t = ui.Theme;
        using (ui.Box(width: p.Width, height: p.Height))
        {
            using (ui.Box(grow: 1f))
                ui.Children();
            if (p.Remaining > 0f)
            {
                if (p.Duration > 0f)
                    ui.CooldownShade(
                        progress: Math.Clamp(p.Remaining / p.Duration, 0f, 1f),
                        color: t.Color(ColorToken.BgTranslucent),
                        position: Position.Absolute,
                        left: 0f,
                        top: 0f,
                        right: 0f,
                        bottom: 0f
                    );
                using (
                    ui.Box(
                        align: Align.Center,
                        justify: Justify.Center,
                        position: Position.Absolute,
                        left: 0f,
                        top: 0f,
                        right: 0f,
                        bottom: 0f,
                        pointerEvents: PointerEvents.None
                    )
                )
                    ui.Text(
                        p.Remaining >= 1f
                            ? ui.Format($"{(int)MathF.Ceiling(p.Remaining)}")
                            : ui.Format($"{p.Remaining:0.0}"),
                        color: t.Color(ColorToken.FgStrong),
                        fontWeight: t.Weight(WeightToken.Interface),
                        variant: TextVariant.Heading,
                        tabularDigits: true
                    );
            }
        }
    }

    static ArgumentOutOfRangeException Refused(string prop, float value) =>
        new(prop, value, "A cooldown's times are finite and not negative.");

    public readonly record struct ShadeProps
    {
        /// <summary>The share still to run, from 1 as the cooldown starts to 0 when it is done.</summary>
        public float Progress { get; init; }

        public Paint Color { get; init; }

        public Length Width { get; init; }

        public Length Height { get; init; }
    }

    /// <summary>What is left of a cooldown over its slot: a wedge of the inscribed ellipse ending at 12 o'clock,
    /// <see cref="ShadeProps.Progress"/> of the way round, so the clear part grows clockwise from 12 as progress falls.
    /// It takes no input.</summary>
    public sealed class Shade : Component<ShadeProps>
    {
        static readonly CanvasDraw<ShadeProps> Draw = static (dc, p, info) =>
            DrawWedge(dc, info, p.Progress, p.Color);

        /// <summary>Draws the shade's wedge for <paramref name="progress"/> over a canvas, so a canvas that reads a
        /// cooldown as it draws, redrawing as it ticks with no render, shades as this part does.</summary>
        [FramePath]
        public static void DrawWedge(
            DrawingContext dc,
            CanvasInfo info,
            float progress,
            Paint color
        )
        {
            progress = Math.Clamp(progress, 0f, 1f);
            var size = info.Size;
            if (!(progress > 0f) || !(size.Width > 0f && size.Height > 0f))
                return;
            var wedge = info.Path();
            if (progress >= 1f)
                wedge.AddEllipse(new Rect(0f, 0f, size.Width, size.Height));
            else
            {
                var radius = new Vector2(size.Width * 0.5f, size.Height * 0.5f);
                var angle = (1f - progress) * MathF.Tau;
                wedge.MoveTo(radius);
                wedge.LineTo(
                    new Vector2(
                        radius.X * (1f + MathF.Sin(angle)),
                        radius.Y * (1f - MathF.Cos(angle))
                    )
                );
                wedge.ArcTo(
                    new Vector2(radius.X, 0f),
                    radius,
                    0f,
                    progress > 0.5f,
                    clockwise: true
                );
                wedge.Close();
            }
            dc.DrawPath(wedge, color, default);
        }

        protected override void Render(Ui ui, in ShadeProps p) =>
            ui.Canvas(
                state: p,
                draw: Draw,
                width: p.Width,
                height: p.Height,
                pointerEvents: PointerEvents.None
            );
    }
}

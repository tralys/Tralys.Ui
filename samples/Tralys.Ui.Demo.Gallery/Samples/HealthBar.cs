using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery;

public readonly record struct HealthBarProps
{
    public Length Width { get; init; }
    public Length Height { get; init; }

    public float Value { get; init; }

    /// <summary>Zero means 100.</summary>
    public float Maximum { get; init; }

    public Orientation Orientation { get; init; }
}

/// <summary>A sample of a component an app builds on the core: a health meter drawn on a canvas. The fill falls at
/// once and flashes, while a trail holds where it was for half a second and then drains to it; at or below
/// <see cref="LowRatio"/> of the maximum the fill pulses.</summary>
public sealed class HealthBar : Component<HealthBarProps>
{
    public const float LowRatio = 0.3f;

    static readonly IEasingFunction Linear = new KeySpline();
    static readonly Transition Drain = new(0.45f, Linear, Delay: 0.5f);
    static readonly Transition Flash = new(0.09f, Linear);

    readonly record struct Look(
        Color Track,
        Color Trail,
        Color Fill,
        Color Flash,
        float Ratio,
        float TrailRatio,
        float FlashOpacity,
        float FillOpacity,
        bool Horizontal
    );

    static readonly CanvasDraw<Look> Draw = static (dc, look, info) =>
    {
        var size = info.Size;
        dc.DrawRectangle(look.Track, new Rect(size));
        dc.DrawRectangle(look.Trail, Share(look.TrailRatio, look.Horizontal, size));
        if (!(look.Ratio > 0f))
            return;
        var filled = Share(look.Ratio, look.Horizontal, size);
        dc.DrawRectangle(Paint.Solid(look.Fill, look.FillOpacity), filled);
        if (look.FlashOpacity > 0f)
            dc.DrawRectangle(Paint.Solid(look.Flash, look.FlashOpacity), filled);
    };

    sealed class Falls
    {
        public float Last = float.NaN;
        public int Count;
    }

    protected override void Render(Ui ui, in HealthBarProps p)
    {
        var t = ui.Theme;
        var maximum = p.Maximum > 0f ? p.Maximum : 100f;
        var ratio = Math.Clamp(p.Value / maximum, 0f, 1f);
        var falls = ui.Remember(static () => new Falls());
        var falling = ratio < falls.Last;
        if (falling)
            falls.Count++;
        falls.Last = ratio;
        var trail = MathF.Max(ui.Animate(ratio, falling ? Drain : Transition.Instant), ratio);
        var flash = falls.Count - ui.Animate(falls.Count, Flash);
        var low = ratio > 0f && ratio <= LowRatio;
        var pulse = ui.Loop(0.45f, alternate: true, paused: !low);
        ui.Canvas(
            state: new Look(
                t.Color(ColorToken.BgSunken),
                t.Color(ColorToken.BgWarning),
                t.Color(ColorToken.BgSuccess),
                t.Color(ColorToken.FgOnMedia),
                ratio,
                trail,
                flash,
                low ? 1f - 0.45f * pulse : 1f,
                p.Orientation == Orientation.Horizontal
            ),
            draw: Draw,
            width: p.Width,
            height: p.Height
        );
    }

    [FramePath]
    static Rect Share(float share, bool horizontal, Size size) =>
        horizontal
            ? new Rect(0f, 0f, size.Width * share, size.Height)
            : new Rect(0f, size.Height * (1f - share), size.Width, size.Height * share);
}

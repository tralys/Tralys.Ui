using System.Numerics;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>How a <see cref="CircularProgress"/> shows its share.</summary>
public enum CircularProgressVariant : byte
{
    /// <summary>An arc running clockwise from 12 o'clock over a sunken ring.</summary>
    Ring,

    /// <summary>A resource orb: a thin ring in the border colour, filled from the bottom up.</summary>
    Orb,
}

public readonly record struct CircularProgressProps
{
    public float Value { get; init; }

    public float Min { get; init; }

    /// <summary>The value that fills it; 100 by default.</summary>
    public float? Max { get; init; }

    public CircularProgressVariant Variant { get; init; }

    /// <summary>The ring's depth; 4 by default, and 0 draws no ring. The orb's ring is always 1 deep.</summary>
    public float? Thickness { get; init; }

    /// <summary>The fill; the theme's accent by default.</summary>
    public Paint Color { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }
}

/// <summary>How far <see cref="CircularProgressProps.Value"/> has come from <see cref="CircularProgressProps.Min"/> to
/// <see cref="CircularProgressProps.Max"/>, on a circle as big as its smaller side: an arc of
/// <see cref="CircularProgressProps.Color"/> clockwise from 12 o'clock over a sunken ring, or, as an orb, a fill rising
/// from the bottom of a thin bordered circle. A value outside the range shows as the nearer end. What it holds sits in
/// the middle, in digits of one width.</summary>
[Children]
public sealed class CircularProgress : Component<CircularProgressProps>
{
    const float DefaultMax = 100f;
    const float DefaultThickness = 4f;
    const float Top = -MathF.PI * 0.5f;

    static readonly CanvasDraw<Dial> Draw = static (dc, d, info) =>
    {
        var size = info.Size;
        if (d.Variant == CircularProgressVariant.Orb)
        {
            DrawOrb(dc, d, new Rect(size));
            return;
        }
        var radius = MathF.Min(size.Width, size.Height) * 0.5f;
        if (!(radius > 0f && d.Thickness > 0f))
            return;
        var center = new Vector2(size.Width * 0.5f, size.Height * 0.5f);
        dc.DrawArc(center, radius, d.Thickness, Top, MathF.Tau, d.Track);
        if (d.Share > 0f)
            dc.DrawArc(center, radius, d.Thickness, Top, d.Share * MathF.Tau, d.Fill);
    };

    protected override void Render(Ui ui, in CircularProgressProps p)
    {
        var t = ui.Theme;
        var orb = p.Variant == CircularProgressVariant.Orb;
        var dial = new Dial(
            p.Variant,
            AnimatedParts.Share(p.Value, p.Min, p.Max ?? DefaultMax),
            orb ? 1f : p.Thickness ?? DefaultThickness,
            t.Color(orb ? ColorToken.Border : ColorToken.BgSunken),
            p.Color.Kind != PaintKind.None ? p.Color : t.Color(ColorToken.BgAccent)
        );
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                width: p.Width,
                height: p.Height,
                tabularDigits: true
            )
        )
        {
            ui.Canvas(
                state: dial,
                draw: Draw,
                pointerEvents: PointerEvents.None,
                position: Position.Absolute,
                left: 0f,
                top: 0f,
                right: 0f,
                bottom: 0f
            );
            using (ui.Box())
                ui.Children();
        }
    }

    static void DrawOrb(DrawingContext dc, in Dial d, Rect bounds)
    {
        if (bounds.IsEmpty)
            return;
        dc.DrawEllipse(bounds, default, 1f, d.Track);
        if (!(d.Share > 0f))
            return;
        var height = bounds.Height * d.Share;
        dc.PushClip(new Rect(bounds.X, bounds.Bottom - height, bounds.Width, height));
        dc.DrawEllipse(bounds, d.Fill, 0f, default);
        dc.Pop();
    }

    readonly record struct Dial(
        CircularProgressVariant Variant,
        float Share,
        float Thickness,
        Paint Track,
        Paint Fill
    );
}

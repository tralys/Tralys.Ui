using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct ProgressProps
{
    public float Value { get; init; }

    public float Min { get; init; }

    /// <summary>The value that fills the track; 100 by default.</summary>
    public float? Max { get; init; }

    /// <summary>Stands for work of no known length: the value's fill gives way to a segment sweeping across the
    /// track.</summary>
    public bool Indeterminate { get; init; }

    public Length Width { get; init; }

    /// <summary>The height; 4 when unset.</summary>
    public Length Height { get; init; }

    /// <summary>The fill and the sweeping segment; the theme's progress fill by default.</summary>
    public Paint Color { get; init; }
}

/// <summary>A bar showing how far <see cref="ProgressProps.Value"/> has come from <see cref="ProgressProps.Min"/> to
/// <see cref="ProgressProps.Max"/>: its <see cref="ProgressProps.Color"/> covers that share of a sunken track from the left, a value
/// outside the range showing as the nearer end. While <see cref="ProgressProps.Indeterminate"/>, a segment 30% of the
/// track wide sweeps across it every 1.4 s instead, resting while hidden. A skin's progress-track art replaces the
/// plain track, with the fill inside the art's slice, and its fill-overlay art lies over the fill.</summary>
public sealed class Progress : Component<ProgressProps>
{
    const float DefaultMax = 100f;
    const float DefaultHeight = 4f;
    const float SweepSeconds = 1.4f;
    const float Segment = 0.3f;

    static readonly CanvasDraw<Sweep> DrawSweep = static (dc, s, info) =>
    {
        var width = info.Size.Width;
        dc.DrawRectangle(
            s.Color,
            new Rect(
                (s.Phase * (1f + Segment) - Segment) * width,
                0f,
                Segment * width,
                info.Size.Height
            )
        );
    };

    protected override void Render(Ui ui, in ProgressProps p)
    {
        var t = ui.Theme;
        var track = t.Skin(SkinPart.ProgressTrack);
        var inset = track is { } art ? AnimatedParts.Inset(art) : default;
        var fill = p.Color.Kind != PaintKind.None ? p.Color : t.Color(ColorToken.BgProgress);
        using (
            ui.Box(
                width: p.Width,
                height: p.Height.IsAuto ? DefaultHeight : p.Height,
                overflow: Overflow.Clip,
                background: track is null ? t.Color(ColorToken.BgSunken) : default(Paint)
            )
        )
        {
            if (track is { } shown)
                ui.Image(
                    shown.Source,
                    slice: shown.NineSlice,
                    position: Position.Absolute,
                    left: 0f,
                    top: 0f,
                    right: 0f,
                    bottom: 0f
                );
            using (
                ui.Box(
                    overflow: Overflow.Clip,
                    position: Position.Absolute,
                    left: inset.Left,
                    top: inset.Top,
                    right: inset.Right,
                    bottom: inset.Bottom
                )
            )
            {
                if (p.Indeterminate)
                    ui.Canvas(
                        state: new Sweep(ui.Loop(SweepSeconds), fill),
                        draw: DrawSweep,
                        grow: 1f
                    );
                else
                    using (
                        ui.Box(
                            width: Length.Percent(
                                AnimatedParts.Share(p.Value, p.Min, p.Max ?? DefaultMax) * 100f
                            ),
                            background: fill
                        )
                    )
                        if (t.Skin(SkinPart.ProgressFillOverlay) is { } overlay)
                            ui.Art(overlay);
            }
        }
    }

    readonly record struct Sweep(float Phase, Paint Color);
}

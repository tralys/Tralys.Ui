using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery;

public readonly record struct StatusLevelMeterProps
{
    public Length Width { get; init; }
    public Length Height { get; init; }

    /// <summary>The levels filled whole.</summary>
    public int Level { get; init; }

    public int MaxLevel { get; init; }

    /// <summary>How far the next level is filled, 0 to 100; at 100 a gate holds it, and it shows ready.</summary>
    public float Percent { get; init; }
}

/// <summary>A sample of a component an app builds on the core: a segmented level meter drawn on a canvas, one segment
/// per level 2 units apart and snapped to whole units, those below the level full and the next filled by the
/// percent, in the success colour once a gate holds it complete.</summary>
public sealed class StatusLevelMeter : Component<StatusLevelMeterProps>
{
    const float Gap = 2f;

    readonly record struct Look(
        Color Track,
        Color Fill,
        Color Ready,
        int Level,
        int MaxLevel,
        float Percent
    );

    static readonly CanvasDraw<Look> Draw = static (dc, look, info) =>
    {
        var levels = look.MaxLevel;
        if (levels <= 0)
            return;
        var height = info.Size.Height;
        var width = (info.Size.Width - Gap * (levels - 1)) / levels;
        if (!(width > 0f))
            return;
        var ready = look.Percent >= 100f;
        var part = Math.Clamp(look.Percent / 100f, 0f, 1f);
        for (var i = 0; i < levels; i++)
        {
            var left = Snap(i * (width + Gap));
            var right = Snap(i * (width + Gap) + width);
            dc.DrawRectangle(look.Track, new Rect(left, 0f, right - left, height));
            var fill =
                i < look.Level ? 1f
                : i == look.Level ? part
                : 0f;
            if (!(fill > 0f))
                continue;
            var fillRight = MathF.Ceiling(left + (right - left) * fill);
            dc.DrawRectangle(
                i == look.Level && ready ? look.Ready : look.Fill,
                new Rect(left, 0f, fillRight - left, height)
            );
        }
    };

    protected override void Render(Ui ui, in StatusLevelMeterProps p)
    {
        var t = ui.Theme;
        ui.Canvas(
            state: new Look(
                t.Color(ColorToken.BgSunken),
                t.Color(ColorToken.BgAccent),
                t.Color(ColorToken.BgSuccess),
                p.Level,
                p.MaxLevel,
                p.Percent
            ),
            draw: Draw,
            width: p.Width,
            height: p.Height
        );
    }

    [FramePath]
    static float Snap(float x) => MathF.Round(x, MidpointRounding.AwayFromZero);
}

using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

internal enum ToggleShape : byte
{
    Box,
    Round,
    Track,
}

[FramePath]
internal static class ToggleParts
{
    public const float LabelGap = 6f;

    public static readonly IEasingFunction Decelerate = new KeySpline(
        1f / 3f,
        2f / 3f,
        2f / 3f,
        1f
    );

    /// <summary>A box's or round mark's side, and a track's height.</summary>
    public static float Extent(ComponentMetrics m) => m.IconSize * 1.25f;

    public static Color LabelColor(this Ui ui, bool accent)
    {
        var t = ui.Theme;
        return t.Color(
            accent ? ColorToken.FgAccent
            : ui.IsHovered ? ColorToken.FgStrong
            : ColorToken.Fg
        );
    }

    /// <summary>The mark a checkbox, radio or switch draws beside its label, lit, pressed and on as its host is:
    /// a box with a tick (or, undecided, a square), a circle with a dot, or a track with a sliding knob.</summary>
    public static void Indicator(
        this Ui ui,
        ToggleShape shape,
        CheckState state,
        float extent,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        var on = state == CheckState.Checked;
        var pressed = ui.IsPressed;
        var hovered = ui.IsHovered;
        var track = shape == ToggleShape.Track;
        var bg = t.Color(ColorToken.Bg);
        var fill =
            on ? t.Color(ColorToken.BgSelected)
            : pressed ? t.Color(ColorToken.StatePressed).Over(bg)
            : bg;
        var edge = t.Color(
            on ? ColorToken.BorderSelected
            : pressed ? ColorToken.BorderPressed
            : hovered ? ColorToken.BorderHover
            : ColorToken.Border
        );
        var knob = t.Color(
            on ? ColorToken.IconOnAccent
            : pressed ? ColorToken.IconPressed
            : hovered ? ColorToken.IconHover
            : ColorToken.Icon
        );
        var offArt = track ? t.Skin(SkinPart.ToggleTrackOff) : null;
        var width = track ? extent * 2.2f : extent;
        var radius =
            shape == ToggleShape.Box ? t.Radius(RadiusToken.Sm) : new CornerRadius(extent / 2f);
        using (ui.Fragment(key: key, file: file, line: line))
        {
            var surface = ui.Animate(fill);
            var border = ui.Animate(edge);
            var shown = ui.Animate(
                on ? 1f : 0f,
                new Transition(on && !track ? 0.1f : 0.2f, Decelerate)
            );
            using (
                ui.Box(
                    align: Align.Center,
                    justify: Justify.Center,
                    width: width,
                    height: extent,
                    shrink: 0f
                )
            )
            {
                if (offArt is { } off)
                    ui.Art(art: off);
                else
                    using (
                        ui.Box(
                            position: Position.Absolute,
                            left: 0f,
                            top: 0f,
                            right: 0f,
                            bottom: 0f,
                            background: surface,
                            border: new Border(1f, border),
                            radius: radius
                        )
                    ) { }
                switch (shape)
                {
                    case ToggleShape.Box when state == CheckState.Indeterminate:
                        using (
                            ui.Box(
                                width: extent / 2f,
                                height: extent / 2f,
                                background: t.Color(ColorToken.Icon)
                            )
                        ) { }
                        break;
                    case ToggleShape.Box:
                        ui.Canvas(
                            state: t.Color(ColorToken.IconOnAccent),
                            draw: Tick,
                            width: extent * 0.7f,
                            height: extent * 0.7f,
                            transform: Transform.Scale(shown),
                            pointerEvents: PointerEvents.None
                        );
                        break;
                    case ToggleShape.Round:
                        using (
                            ui.Box(
                                width: extent / 2f,
                                height: extent / 2f,
                                radius: extent / 4f,
                                background: t.Color(ColorToken.IconOnAccent),
                                transform: Transform.Scale(shown)
                            )
                        ) { }
                        break;
                    default:
                        ui.Knob(
                            extent: extent,
                            width: width,
                            shown: shown,
                            color: ui.Animate(knob)
                        );
                        break;
                }
            }
        }
    }

    static void Knob(
        this Ui ui,
        float extent,
        float width,
        float shown,
        Color color,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        var size = extent / 2f;
        var inset = extent * 0.3f;
        var thumb = t.Skin(SkinPart.ToggleThumb);
        using (ui.Fragment(key: key, file: file, line: line))
        {
            if (t.Skin(SkinPart.ToggleTrackOn) is { } onArt)
                ui.Art(art: onArt, opacity: shown);
            using (
                ui.Box(
                    position: Position.Absolute,
                    left: inset + (width - 2f * inset - size) * shown,
                    top: (extent - size) / 2f,
                    width: size,
                    height: size,
                    radius: size / 2f,
                    background: thumb is null ? color : default(Paint)
                )
            )
            {
                if (thumb is { } art)
                    ui.Art(art: art);
            }
        }
    }

    static readonly CanvasDraw<Color> Tick = static (dc, color, info) =>
    {
        var k = info.Size.Width / 14f;
        var path = info.Path();
        path.MoveTo(new Vector2(0f, 7.5f * k));
        path.LineTo(new Vector2(5.5f * k, 12.5f * k));
        path.LineTo(new Vector2(14f * k, 2.75f * k));
        path.LineTo(new Vector2(11.75f * k, 1f * k));
        path.LineTo(new Vector2(5.5f * k, 9f * k));
        path.LineTo(new Vector2(1.5f * k, 5.5f * k));
        path.Close();
        dc.DrawPath(path, color, default);
    };
}

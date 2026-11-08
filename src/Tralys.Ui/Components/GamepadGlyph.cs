using System.Numerics;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct GamepadGlyphProps
{
    public GamepadButton Button { get; init; }

    /// <summary>Whose layout the button is printed in; the view's gamepad style when unset.</summary>
    public GamepadStyle? Style { get; init; }
}

/// <summary>A gamepad button drawn as the pad prints it: a face button is a disc with its letter, or with its shape on a
/// PlayStation pad, a D-pad direction a disc with a filled arrow, and any other button a pill with its short name, such
/// as LB, R1 or ZL; in the theme's sunken fill with a 1-unit border-coloured edge and strong-colour ink, at least 22 ×
/// 22. Input passes through it.</summary>
public sealed class GamepadGlyph : Component<GamepadGlyphProps>
{
    const float MarkThickness = 1.5f;

    static readonly CanvasDraw<Face> Draw = static (dc, face, info) =>
    {
        var size = info.Size;
        if (!(size.Width > 0f && size.Height > 0f))
            return;
        if (!IsRound(face.Button))
        {
            dc.DrawBox(
                new Rect(size),
                new CornerRadius(size.Height * 0.5f),
                new Thickness(1f),
                face.Fill,
                face.Edge
            );
            return;
        }
        var diameter = MathF.Min(size.Width, size.Height);
        var center = new Vector2(size.Width * 0.5f, size.Height * 0.5f);
        var radius = diameter * 0.5f;
        dc.DrawEllipse(
            new Rect(center.X - radius, center.Y - radius, diameter, diameter),
            face.Fill,
            1f,
            face.Edge
        );
        if (face.Marked)
            Mark(dc, info.Path(), face.Button, center, radius * 0.42f, face.Ink);
    };

    protected override void Render(Ui ui, in GamepadGlyphProps p)
    {
        var t = ui.Theme;
        var label = LabelOf(p.Button, p.Style ?? ui.GamepadStyle);
        var ink = t.Color(ColorToken.FgStrong);
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                minWidth: 22f,
                minHeight: 22f,
                padding: new Thickness(6f, 1f, 6f, 1f),
                pointerEvents: PointerEvents.None
            )
        )
        {
            ui.Canvas(
                state: new Face(
                    p.Button,
                    label is null,
                    t.Color(ColorToken.BgSunken),
                    t.Color(ColorToken.Border),
                    ink
                ),
                draw: Draw,
                position: Position.Absolute,
                left: 0f,
                top: 0f,
                right: 0f,
                bottom: 0f
            );
            if (label is not null)
                ui.Text(
                    label,
                    color: ink,
                    fontWeight: t.Weight(WeightToken.Interface),
                    variant: TextVariant.Caption
                );
        }
    }

    static void Mark(
        DrawingContext dc,
        PathBuilder mark,
        GamepadButton button,
        Vector2 center,
        float size,
        Color ink
    )
    {
        var pen = new Pen(
            ink,
            MarkThickness,
            PenLineJoin.Round,
            PenLineCap.Round,
            PenLineCap.Round
        );
        switch (button)
        {
            case GamepadButton.South:
                mark.MoveTo(center + new Vector2(-size, -size));
                mark.LineTo(center + new Vector2(size, size));
                mark.MoveTo(center + new Vector2(size, -size));
                mark.LineTo(center + new Vector2(-size, size));
                dc.DrawPath(mark, default, pen);
                return;
            case GamepadButton.East:
                dc.DrawEllipse(
                    new Rect(center.X - size, center.Y - size, size * 2f, size * 2f),
                    default,
                    MarkThickness,
                    ink
                );
                return;
            case GamepadButton.West:
                mark.AddRectangle(new Rect(center.X - size, center.Y - size, size * 2f, size * 2f));
                dc.DrawPath(mark, default, pen);
                return;
            case GamepadButton.North:
                Triangle(mark, center, size, -Vector2.UnitY);
                dc.DrawPath(mark, default, pen);
                return;
            default:
                Triangle(mark, center, size, Direction(button));
                dc.DrawPath(mark, ink, default);
                return;
        }
    }

    static void Triangle(PathBuilder mark, Vector2 center, float size, Vector2 toward)
    {
        var across = new Vector2(-toward.Y, toward.X);
        mark.MoveTo(center + toward * size);
        mark.LineTo(center - toward * size * 0.6f + across * size);
        mark.LineTo(center - toward * size * 0.6f - across * size);
        mark.Close();
    }

    static Vector2 Direction(GamepadButton button) =>
        button switch
        {
            GamepadButton.DPadUp => new Vector2(0f, -1f),
            GamepadButton.DPadDown => new Vector2(0f, 1f),
            GamepadButton.DPadLeft => new Vector2(-1f, 0f),
            _ => new Vector2(1f, 0f),
        };

    static bool IsRound(GamepadButton button) =>
        button is <= GamepadButton.North or >= GamepadButton.DPadUp;

    static string? LabelOf(GamepadButton button, GamepadStyle style) =>
        (style, button) switch
        {
            (_, >= GamepadButton.DPadUp) => null,
            (GamepadStyle.PlayStation, <= GamepadButton.North) => null,
            (GamepadStyle.PlayStation, GamepadButton.Back) => "Create",
            (GamepadStyle.PlayStation, GamepadButton.Start) => "Options",
            (GamepadStyle.PlayStation, GamepadButton.Guide) => "PS",
            (GamepadStyle.PlayStation, GamepadButton.LeftShoulder) => "L1",
            (GamepadStyle.PlayStation, GamepadButton.RightShoulder) => "R1",
            (GamepadStyle.PlayStation, GamepadButton.LeftTrigger) => "L2",
            (GamepadStyle.PlayStation, GamepadButton.RightTrigger) => "R2",
            (GamepadStyle.PlayStation, GamepadButton.LeftStick) => "L3",
            (GamepadStyle.PlayStation, GamepadButton.RightStick) => "R3",
            (GamepadStyle.Nintendo, GamepadButton.South) => "B",
            (GamepadStyle.Nintendo, GamepadButton.East) => "A",
            (GamepadStyle.Nintendo, GamepadButton.West) => "Y",
            (GamepadStyle.Nintendo, GamepadButton.North) => "X",
            (GamepadStyle.Nintendo, GamepadButton.Back) => "-",
            (GamepadStyle.Nintendo, GamepadButton.Start) => "+",
            (GamepadStyle.Nintendo, GamepadButton.LeftShoulder) => "L",
            (GamepadStyle.Nintendo, GamepadButton.RightShoulder) => "R",
            (GamepadStyle.Nintendo, GamepadButton.LeftTrigger) => "ZL",
            (GamepadStyle.Nintendo, GamepadButton.RightTrigger) => "ZR",
            (_, GamepadButton.South) => "A",
            (_, GamepadButton.East) => "B",
            (_, GamepadButton.West) => "X",
            (_, GamepadButton.North) => "Y",
            (_, GamepadButton.Back) => "View",
            (_, GamepadButton.Start) => "Menu",
            (_, GamepadButton.Guide) => "Home",
            (_, GamepadButton.LeftShoulder) => "LB",
            (_, GamepadButton.RightShoulder) => "RB",
            (_, GamepadButton.LeftTrigger) => "LT",
            (_, GamepadButton.RightTrigger) => "RT",
            (_, GamepadButton.LeftStick) => "LS",
            _ => "RS",
        };

    readonly record struct Face(
        GamepadButton Button,
        bool Marked,
        Color Fill,
        Color Edge,
        Color Ink
    );
}

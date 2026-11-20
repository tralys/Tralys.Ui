using System.Numerics;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery;

readonly record struct TralysMarkProps
{
    public float Size { get; init; }
}

sealed class TralysMark : Component<TralysMarkProps>
{
    const float Grid = 64f;

    static readonly Rect[] Warp =
    [
        new(16f, 4f, 12f, 28f),
        new(16f, 52f, 12f, 8f),
        new(36f, 4f, 12f, 8f),
        new(36f, 32f, 12f, 28f),
    ];

    static readonly Rect[] Weft =
    [
        new(4f, 16f, 8f, 12f),
        new(32f, 16f, 28f, 12f),
        new(4f, 36f, 28f, 12f),
        new(52f, 36f, 8f, 12f),
    ];

    readonly record struct Look(Color Warp, Color Weft);

    static readonly CanvasDraw<Look> Draw = static (dc, look, info) =>
    {
        var scale = MathF.Min(info.Size.Width, info.Size.Height) / Grid;
        dc.PushTransform(
            Matrix3x2.CreateRotation(MathF.PI / 4f, new Vector2(Grid / 2f))
                * Matrix3x2.CreateScale(scale)
        );
        foreach (var bar in Warp)
            dc.DrawRectangle(look.Warp, bar);
        foreach (var bar in Weft)
            dc.DrawRectangle(look.Weft, bar);
        dc.Pop();
    };

    protected override void Render(Ui ui, in TralysMarkProps p)
    {
        var t = ui.Theme;
        ui.Canvas(
            state: new Look(t.Color(ColorToken.Fg), t.Color(ColorToken.FgAccent)),
            draw: Draw,
            width: p.Size,
            height: p.Size
        );
    }
}

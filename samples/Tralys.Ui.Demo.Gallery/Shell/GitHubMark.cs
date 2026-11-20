using System.Numerics;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery;

readonly record struct GitHubMarkProps
{
    public float Size { get; init; }
}

sealed class GitHubMark : Component<GitHubMarkProps>
{
    static readonly Vector2 Frame = new(98f, 96f);

    static readonly Vector2[] Outline =
    [
        new(48.854f, 0f),
        new(21.839f, 0f),
        new(0f, 22f),
        new(0f, 49.217f),
        new(0f, 70.973f),
        new(13.993f, 89.389f),
        new(33.405f, 95.907f),
        new(35.832f, 96.397f),
        new(36.721f, 94.848f),
        new(36.721f, 93.545f),
        new(36.721f, 92.404f),
        new(36.641f, 88.493f),
        new(36.641f, 84.418f),
        new(23.051f, 87.352f),
        new(20.221f, 78.551f),
        new(20.221f, 78.551f),
        new(18.037f, 72.847f),
        new(14.801f, 71.381f),
        new(14.801f, 71.381f),
        new(10.353f, 68.366f),
        new(15.125f, 68.366f),
        new(15.125f, 68.366f),
        new(20.059f, 68.692f),
        new(22.648f, 73.418f),
        new(22.648f, 73.418f),
        new(27.015f, 80.914f),
        new(34.052f, 78.796f),
        new(36.883f, 77.492f),
        new(37.287f, 74.314f),
        new(38.582f, 72.114f),
        new(39.957f, 70.892f),
        new(29.118f, 69.751f),
        new(17.714f, 65.514f),
        new(17.714f, 46.609f),
        new(17.714f, 41.231f),
        new(19.654f, 36.831f),
        new(22.728f, 33.409f),
        new(22.243f, 32.187f),
        new(20.544f, 27.134f),
        new(23.214f, 20.371f),
        new(23.214f, 20.371f),
        new(27.339f, 19.067f),
        new(36.64f, 25.423f),
        new(40.622f, 24.346f),
        new(44.729f, 23.798f),
        new(48.854f, 23.793f),
        new(52.979f, 23.793f),
        new(57.184f, 24.364f),
        new(61.067f, 25.423f),
        new(70.369f, 19.067f),
        new(74.494f, 20.371f),
        new(74.494f, 20.371f),
        new(77.164f, 27.134f),
        new(75.464f, 32.187f),
        new(74.979f, 33.409f),
        new(78.134f, 36.831f),
        new(79.994f, 41.231f),
        new(79.994f, 46.609f),
        new(79.994f, 65.514f),
        new(68.59f, 69.669f),
        new(57.67f, 70.892f),
        new(59.45f, 72.44f),
        new(60.986f, 75.373f),
        new(60.986f, 80.018f),
        new(60.986f, 86.618f),
        new(60.906f, 91.915f),
        new(60.906f, 93.544f),
        new(60.906f, 94.848f),
        new(61.796f, 96.397f),
        new(64.222f, 95.908f),
        new(83.634f, 89.388f),
        new(97.627f, 70.973f),
        new(97.627f, 49.217f),
        new(97.707f, 22f),
        new(75.788f, 0f),
        new(48.854f, 0f),
    ];

    static readonly CanvasDraw<Color> Draw = static (dc, fill, info) =>
    {
        var scale = MathF.Min(info.Size.Width / Frame.X, info.Size.Height / Frame.Y);
        var offset = (new Vector2(info.Size.Width, info.Size.Height) - Frame * scale) / 2f;
        var path = info.Path();
        path.MoveTo(Outline[0] * scale + offset);
        for (var i = 1; i < Outline.Length; i += 3)
            path.CubicTo(
                Outline[i] * scale + offset,
                Outline[i + 1] * scale + offset,
                Outline[i + 2] * scale + offset
            );
        path.Close();
        dc.DrawPath(path, fill, default);
    };

    protected override void Render(Ui ui, in GitHubMarkProps p) =>
        ui.Canvas(state: ui.Theme.Color(ColorToken.Fg), draw: Draw, width: p.Size, height: p.Size);
}

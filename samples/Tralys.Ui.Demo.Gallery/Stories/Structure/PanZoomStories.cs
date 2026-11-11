using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<PanZoom>("Structure")]
static class PanZoomStories
{
    static readonly GridTracks Sides = new(Track.Fr(1f), Track.Auto);

    static readonly GridTracks Fill = new(Track.Fr(1f));

    static readonly string[] Nodes =
    [
        "Director",
        "Design",
        "Engineering",
        "Operations",
        "Research",
        "Interface",
        "Platform",
        "Support",
        "Logistics",
    ];

    static readonly Vector2[] At =
    [
        new(390, 44),
        new(150, 174),
        new(390, 174),
        new(630, 174),
        new(50, 324),
        new(250, 324),
        new(390, 324),
        new(530, 324),
        new(730, 324),
    ];

    static readonly ComponentVariant[] Looks =
    [
        ComponentVariant.Filled,
        ComponentVariant.Outlined,
        ComponentVariant.Outlined,
        ComponentVariant.Outlined,
        ComponentVariant.Subtle,
        ComponentVariant.Subtle,
        ComponentVariant.Subtle,
        ComponentVariant.Subtle,
        ComponentVariant.Subtle,
    ];

    static readonly Vector4[] Links =
    [
        new(450, 60, 210, 190),
        new(450, 60, 450, 190),
        new(450, 60, 690, 190),
        new(210, 190, 110, 340),
        new(210, 190, 310, 340),
        new(450, 190, 450, 340),
        new(690, 190, 590, 340),
        new(690, 190, 790, 340),
    ];

    static readonly CanvasDraw<Color> Lines = static (dc, color, _) =>
    {
        for (var i = 0; i < Links.Length; i++)
            dc.DrawLine(
                new Vector2(Links[i].X, Links[i].Y),
                new Vector2(Links[i].Z, Links[i].W),
                2f,
                color
            );
    };

    [FramePath]
    public static void OrgChart(Ui ui)
    {
        var t = ui.Theme;
        var zoom = ui.State(1f);
        using (ui.Grid(columns: Sides, rows: Fill, width: 520, height: 300))
        {
            using (
                ui.Box(
                    background: t.Color(ColorToken.BgSunken),
                    border: new Border(1f, t.Color(ColorToken.Border)),
                    minWidth: 0,
                    minHeight: 0,
                    column: 1
                )
            )
            using (
                ui.PanZoom(
                    zoom: zoom.Value,
                    onZoomChange: zoom.Setter,
                    boundaryMargin: 40,
                    grow: 1f,
                    id: "tree"
                )
            )
            using (ui.Box(width: 900, height: 420))
            {
                ui.Canvas(
                    state: t.Color(ColorToken.Border),
                    draw: Lines,
                    position: Position.Absolute,
                    left: 0f,
                    top: 0f,
                    width: 900,
                    height: 420
                );
                for (var i = 0; i < Nodes.Length; i++)
                    ui.Button(
                        text: Nodes[i],
                        scheme: i == 0 ? ColorScheme.Primary : ColorScheme.Secondary,
                        variant: Looks[i],
                        width: 120,
                        position: Position.Absolute,
                        left: At[i].X,
                        top: At[i].Y,
                        key: i
                    );
            }
            using (
                ui.Box(
                    direction: Direction.Column,
                    gap: 8,
                    margin: new Thickness(8f, 0f, 0f, 0f),
                    column: 2
                )
            )
            {
                ui.IconButton(
                    icon: Icons.Plus,
                    scheme: ColorScheme.Secondary,
                    variant: ComponentVariant.Outlined,
                    onClick: Callback.Of(
                        zoom,
                        static z => z.Value = MathF.Min(2f, z.Value * 1.25f)
                    ),
                    id: "in"
                );
                ui.IconButton(
                    icon: Icons.Minus,
                    scheme: ColorScheme.Secondary,
                    variant: ComponentVariant.Outlined,
                    onClick: Callback.Of(
                        zoom,
                        static z => z.Value = MathF.Max(0.5f, z.Value / 1.25f)
                    ),
                    id: "out"
                );
            }
        }
    }
}

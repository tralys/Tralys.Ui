using System.Runtime.CompilerServices;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.Image>("Text")]
static class ImageStories
{
    static readonly Rendering.NineSlice HalfTexel = new(default, TexelInset: 0.5f);

    [FramePath]
    public static void Fit(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "A 348×115 logo in a 180×110 frame: None, Fill, Contain, Cover; then Contain and ScaleDown"
            );
            using (ui.Box(gap: 8))
            {
                Framed(ui, "logo.png", ObjectFit.None);
                Framed(ui, "logo.png", ObjectFit.Fill);
                Framed(ui, "logo.png", ObjectFit.Contain);
                Framed(ui, "logo.png", ObjectFit.Cover);
            }
            using (ui.Box(gap: 8))
            {
                Framed(ui, "coin.png", ObjectFit.Contain);
                Framed(ui, "coin.png", ObjectFit.ScaleDown);
            }
        }
    }

    [FramePath]
    static void Framed(
        Ui ui,
        string source,
        ObjectFit fit,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        using (
            ui.Box(
                width: 180,
                height: 110,
                overflow: Overflow.Clip,
                background: t.Color(ColorToken.BgSunken),
                border: new(1, t.Color(ColorToken.Border)),
                key: key,
                file: file,
                line: line
            )
        )
            ui.Image(source, fit: fit, width: Length.Full, height: Length.Full);
    }

    [FramePath]
    public static void SourceRect(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "A 512×320 sheet of 64×64 frames, and single frames cut from it by sourceRect, inset half a texel"
            );
            using (ui.Box(gap: 16, align: Align.Start))
            {
                ui.Image("atlas.png", width: 256, height: 160);
                using (ui.Box(direction: Direction.Column, gap: 8))
                {
                    using (ui.Box(gap: 8))
                    {
                        Cut(ui, 0, 0, 64);
                        Cut(ui, 64, 64, 64);
                        Cut(ui, 128, 128, 64);
                    }
                    using (ui.Box(gap: 8, align: Align.Center))
                    {
                        Cut(ui, 192, 192, 64);
                        Cut(ui, 256, 256, 64);
                        Cut(ui, 448, 256, 32);
                    }
                }
            }
        }
    }

    [FramePath]
    static void Cut(
        Ui ui,
        float x,
        float y,
        float size,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Image(
            "atlas.png",
            sourceRect: new Rect(x, y, 64, 64),
            slice: HalfTexel,
            width: size,
            height: size,
            key: key,
            file: file,
            line: line
        );

    [FramePath]
    public static void NineSlice(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "A 144×144 panel cut 48 from each side: stretched small and wide, corners at half scale, and edges tiled"
            );
            using (ui.Box(gap: 12, align: Align.Center))
            {
                ui.Image("panel.png", slice: new(new Thickness(48)), width: 120, height: 120);
                ui.Image("panel.png", slice: new(new Thickness(48)), width: 320, height: 140);
                ui.Image(
                    "panel.png",
                    slice: new(new Thickness(48), Scale: 0.5f),
                    width: 200,
                    height: 100
                );
                ui.Image(
                    "panel.png",
                    slice: new(new Thickness(48), Fill: SliceFill.Tile),
                    width: 320,
                    height: 140
                );
            }
        }
    }

    [FramePath]
    public static void SliceStretch(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 16,
                align: Align.Start,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption("48px insets, square source, stretched");
            ui.Image("panel.png", slice: new(new Thickness(48)), width: 360, height: 220);
        }
    }

    [FramePath]
    public static void ThreeSlice(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 16,
                align: Align.Start,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption("Zero top and bottom insets collapse the rows into a horizontal 3-slice");
            ui.Image(
                "heading.png",
                slice: new(new Thickness(70, 0, 70, 0)),
                width: 420,
                height: 43
            );
        }
    }
}

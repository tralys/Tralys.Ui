using System.Runtime.CompilerServices;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.Sprite>("Text")]
static class SpriteStories
{
    static readonly Size Frame = new(64, 64);

    static readonly NineSlice Inset = new(default, TexelInset: 0.5f);

    [FramePath]
    static void Played(
        Ui ui,
        Sprite sprite,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Image(
            "atlas.png",
            slice: Inset,
            sprite: sprite,
            width: Frame.Width,
            height: Frame.Height,
            key: key,
            file: file,
            line: line
        );

    [FramePath]
    public static void Playing(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 12,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "One 8×5 sheet of 64×64 frames: a row at 10 fps, a row at 4 fps, all 40 frames from row 0, and one held"
            );
            using (ui.Box(gap: 16, align: Align.Start))
            {
                Played(ui, new(Frame, 8, 8, Row: 1));
                Played(ui, new(Frame, 8, 8, Row: 3, FramesPerSecond: 4));
                Played(ui, new(Frame, 8, 40));
                Played(ui, new(Frame, 8, 8, Row: 4, Paused: true));
            }
        }
    }
}

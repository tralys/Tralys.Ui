using System.Runtime.CompilerServices;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Reveal>("Custom")]
static class RevealStories
{
    [FramePath]
    public static void Progress(Ui ui)
    {
        using (ui.Box(gap: 16, width: 320, fontWeight: ui.Theme.Weight(WeightToken.Interface)))
        {
            Revealed(ui, "Closed", 0, RevealDirection.Down, false);
            Revealed(ui, "Half down", 0.5f, RevealDirection.Down, true);
            Revealed(ui, "Half up", 0.5f, RevealDirection.Up, true);
        }
    }

    [FramePath]
    static void Revealed(
        Ui ui,
        string caption,
        float progress,
        RevealDirection direction,
        bool edge,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 4,
                width: 100,
                key: key,
                file: file,
                line: line
            )
        )
        {
            ui.Caption(caption);
            using (ui.Reveal(progress: progress, direction: direction))
            using (
                ui.Box(
                    direction: Direction.Column,
                    justify: Justify.End,
                    height: 60,
                    background: ui.Theme.Color(ColorToken.BgSubtle)
                )
            )
            {
                if (edge)
                    ui.Text("Bottom edge");
            }
            ui.Text("Below");
        }
    }
}

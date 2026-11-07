using System.Runtime.CompilerServices;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<FittedGrid>("Custom")]
static class FittedGridStories
{
    static readonly string[] Cities = ["Lisbon", "Nairobi", "Kyoto", "Lima", "Oslo"];

    [FramePath]
    public static void MaxColumns(Ui ui)
    {
        using (ui.Box(fontWeight: ui.Theme.Weight(WeightToken.Interface)))
        using (ui.FittedGrid(maxColumns: 4, width: 240))
        {
            for (var i = 0; i < Cities.Length; i++)
                Cell(ui, Cities[i], key: i);
        }
    }

    [FramePath]
    static void Cell(
        Ui ui,
        string text,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                padding: new Thickness(8, 4, 8, 4),
                background: ui.Theme.Color(ColorToken.BgRaised),
                key: key,
                file: file,
                line: line
            )
        )
            ui.Text(text);
    }
}

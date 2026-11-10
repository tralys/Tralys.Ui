using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Gallery.Stories;

[Stories<SearchInput>("Inputs")]
static class SearchInputStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var typed = ui.Remember(static () => new ObservableText());
        var report = ui.Remember(static () => new ObservableText("report"));
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 16, padding: 16))
        {
            ui.SearchInput(text: typed, placeholder: "Search...", width: 240);
            ui.SearchInput(text: report, width: 240);
        }
    }
}

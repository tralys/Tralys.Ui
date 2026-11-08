using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Card>("Displays")]
static class CardStories
{
    [FramePath]
    public static void States(Ui ui)
    {
        using (ui.Box(gap: 16, fontWeight: ui.Theme.Weight(WeightToken.Interface)))
        {
            using (ui.Card(width: 120, height: 80, padding: 8))
                ui.Text("Rest");
            using (ui.Card(highlighted: true, width: 120, height: 80, padding: 8))
                ui.Text("Highlighted");
            using (ui.Card(selected: true, width: 120, height: 80, padding: 8))
                ui.Text("Selected");
        }
    }
}

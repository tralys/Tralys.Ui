using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.Link>("Text")]
static class LinkStories
{
    static readonly Color Gold = Color.Parse("#E0A040");

    [FramePath]
    public static void Default(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 16,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Link("Visit website");
            using (ui.Text(wrap: true))
            {
                ui.Span("See the ");
                ui.Link("documentation");
                ui.Span(" for more details.");
            }
        }
    }

    [FramePath]
    public static void States(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 16,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            using (ui.Text(wrap: true))
            {
                ui.Span("An ");
                ui.Link("unavailable link", disabled: true);
                ui.Span(" shows disabled.");
            }
            using (ui.Text(wrap: true))
            {
                ui.Span("A link coloured by its text: ");
                ui.Link("Gold", color: Gold);
            }
            using (ui.Text(wrap: true, width: 160))
            {
                ui.Span("A long link wraps ");
                ui.Link("with its underline on every line it takes");
            }
        }
    }
}

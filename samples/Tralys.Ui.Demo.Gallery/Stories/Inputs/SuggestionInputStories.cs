using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Gallery.Stories;

[Stories<SuggestionInput>("Inputs")]
static class SuggestionInputStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var names = ui.Remember(static () => new ProjectNames());
        var local = ui.Remember(static () => new ObservableText());
        var server = ui.Remember(static () => new ObservableText());
        using (ui.Box(direction: Direction.Column, gap: 16))
        {
            ui.SuggestionInput(
                text: local,
                source: names.Local,
                maxBytes: 12,
                placeholder: "Project name",
                width: 268
            );
            ui.SuggestionInput(
                text: server,
                source: names.Server,
                emptyText: "No project has that name.",
                maxBytes: 12,
                maxSuggestions: 3,
                placeholder: "Project name, asked of the server",
                width: 268
            );
        }
    }
}

[Stories<SuggestionRows>("Inputs")]
static class SuggestionRowsStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var rows = ui.Remember(static () => Picks.Projects());
        ui.SuggestionRows(rows: rows, width: 240);
    }

    [FramePath]
    public static void States(Ui ui)
    {
        var rows = ui.Remember(static () => Picks.Towns(more: 0));
        ui.SuggestionRows(rows: rows, width: 240);
    }

    [FramePath]
    public static void Open(Ui ui)
    {
        var rows = ui.Remember(static () => Picks.Towns(more: 9));
        var town = ui.Remember(static () => new ObservableText("a"));
        var field = ui.Ref();
        using (ui.Box(direction: Direction.Column, width: 268, nodeRef: field))
            ui.Input(text: town, placeholder: "Town");
        using (ui.Positioner(anchor: field, matchAnchorWidth: true, direction: Direction.Column))
            ui.SuggestionRows(rows: rows);
    }

    [FramePath]
    public static void Empty(Ui ui)
    {
        var rows = ui.Remember(static () => Picks.Nothing());
        var town = ui.Remember(static () => new ObservableText("Zz"));
        var field = ui.Ref();
        using (ui.Box(direction: Direction.Column, width: 268, nodeRef: field))
            ui.Input(text: town, placeholder: "Town");
        using (ui.Positioner(anchor: field, matchAnchorWidth: true, direction: Direction.Column))
            ui.SuggestionRows(rows: rows, emptyText: "No town has that name.");
    }
}

/// <summary>The project names the suggestion stories offer, as the client holds them and as a server answers for
/// them.</summary>
sealed class ProjectNames
{
    static readonly string[] Projects =
    [
        "Dreamline",
        "Dreamland",
        "Nimbus",
        "Sunrise",
        "Driftwood",
        "Aurora",
        "Ørn Systems",
    ];

    public readonly ISuggestionSource Local = new LocalSuggestions(static names =>
        names.AddRange(Projects)
    );

    public readonly ISuggestionSource Server = new RemoteSuggestions<string>(
        static query => new(query),
        static (query, rows) =>
        {
            foreach (var project in Projects)
            {
                if (project.Contains(query, StringComparison.OrdinalIgnoreCase))
                    rows.AddRow(project);
            }
            return true;
        }
    );
}

/// <summary>Rows already answered, as a field's popup shows them.</summary>
static class Picks
{
    public static SuggestionList Projects()
    {
        var rows = new SuggestionList(5);
        rows.BeginRows();
        rows.AddRow("Dreamline");
        rows.AddRow("Dreamland");
        rows.AddRow("Driftwood");
        rows.AddRow("Nimbus");
        rows.EndRows(final: true);
        return rows;
    }

    public static SuggestionList Towns(int more)
    {
        var rows = new SuggestionList(3);
        rows.BeginRows();
        rows.AddRow("Ashford", "Towns", detail: "12 online");
        rows.AddRow("Brightwater", detail: "4 online");
        rows.AddRow("Coldharbour");
        rows.CountMore(more);
        rows.EndRows(final: true);
        rows.Move(1);
        return rows;
    }

    public static SuggestionList Nothing()
    {
        var rows = new SuggestionList(3) { OpenWhenEmpty = true };
        rows.BeginRows();
        rows.EndRows(final: true);
        return rows;
    }
}

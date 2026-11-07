using System.Runtime.CompilerServices;
using Tralys.Ui.Fonts;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.Format>("Features")]
static class FormatStories
{
    [FramePath]
    public static void EveryFrame(Ui ui)
    {
        var t = ui.Theme;
        var bar = 400 * ui.Loop(3);
        var remaining = 5 * (1 - ui.Loop(5));
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 10,
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "Values that change every frame, formatted straight into each text: a format string, two values, a function, and spans"
            );
            using (
                ui.Box(width: 400, height: 12, radius: 6, background: t.Color(ColorToken.BgSunken))
            )
            using (ui.Box(width: bar, radius: 6, background: t.Color(ColorToken.BgAccent))) { }
            using (ui.Grid(columns: new(220, Track.Auto), rows: GridTracks.Repeat(5, Track.Px(24))))
            {
                Label(ui, "A format string", 1);
                ui.Text($"{bar:0.0} px", alignSelf: AlignSelf.Center, column: 2, row: 1);
                Label(ui, "Two values", 2);
                ui.Text($"{bar:0} of {400:0}", alignSelf: AlignSelf.Center, column: 2, row: 2);
                Label(ui, "A function, then a format", 3);
                ui.Text($"{Percent(bar, 400):0}%", alignSelf: AlignSelf.Center, column: 2, row: 3);
                Label(ui, "A function writing text", 4);
                Countdown(ui, remaining, column: 2, row: 4);
                Label(ui, "Spans", 5);
                using (ui.Text(alignSelf: AlignSelf.Center, column: 2, row: 5))
                {
                    ui.Span("Loading ");
                    ui.Span(ui.Format($"{bar:0}"), fontWeight: FontWeight.Bold);
                    ui.Span(" / 400");
                }
            }
        }
    }

    [FramePath]
    static float Percent(float value, float of) => of > 0 ? value / of * 100 : 0;

    [FramePath]
    static void Countdown(
        Ui ui,
        float seconds,
        int column,
        int row,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Fragment(key: key, file: file, line: line))
        {
            if (seconds <= 0)
                ui.Text("ready", alignSelf: AlignSelf.Center, column: column, row: row);
            else
                ui.Text($"{seconds:0.0}s", alignSelf: AlignSelf.Center, column: column, row: row);
        }
    }

    [FramePath]
    static void Label(
        Ui ui,
        string text,
        int row,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Text(
            text,
            color: ui.Theme.Color(ColorToken.FgMuted),
            alignSelf: AlignSelf.Center,
            column: 1,
            row: row,
            key: key,
            file: file,
            line: line
        );
}

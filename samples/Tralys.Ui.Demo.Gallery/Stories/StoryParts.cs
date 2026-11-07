using System.Runtime.CompilerServices;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

/// <summary>The looks the stories share: a muted caption over a story's content, and the raised, outlined cell
/// that stands for a child in the layout stories.</summary>
static class StoryParts
{
    [FramePath]
    public static void Caption(
        this Ui ui,
        TextValue text,
        Thickness margin = default,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Text(
            text,
            color: ui.Theme.Color(ColorToken.FgMuted),
            wrap: true,
            margin: margin,
            key: key,
            file: file,
            line: line
        );

    [FramePath]
    public static Scope Cell(
        this Ui ui,
        Length width = default,
        Length height = default,
        Thickness margin = default,
        int column = 0,
        int row = 0,
        int columnSpan = 0,
        int rowSpan = 0,
        AlignSelf alignSelf = default,
        AlignSelf justifySelf = default,
        bool collapsed = false,
        bool hidden = false,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        return ui.Box(
            direction: Direction.Column,
            padding: new Thickness(8, 6, 8, 6),
            background: t.Color(ColorToken.BgRaised),
            border: new Border(1, t.Color(ColorToken.Border)),
            width: width,
            height: height,
            margin: margin,
            column: column,
            row: row,
            columnSpan: columnSpan,
            rowSpan: rowSpan,
            alignSelf: alignSelf,
            justifySelf: justifySelf,
            collapsed: collapsed,
            hidden: hidden,
            key: key,
            file: file,
            line: line
        );
    }
}

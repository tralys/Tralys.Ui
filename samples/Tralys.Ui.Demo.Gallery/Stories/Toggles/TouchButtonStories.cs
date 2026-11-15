using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<TouchButton>("Toggles")]
static class TouchButtonStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var t = ui.Theme;
        using (ui.Box(align: Align.Center, gap: 16))
        {
            using (ui.TouchButton(width: 48, height: 48))
                Round(ui, Icons.MessageCircle, 20, t.Color(ColorToken.FgMuted));
            using (ui.TouchButton(width: 48, height: 48))
                Round(ui, Icons.Zap, 20, t.Color(ColorToken.FgMuted));
            using (ui.TouchButton(width: 76, height: 76))
                Round(ui, Icons.Crosshair, 28, t.Color(ColorToken.FgAccent));
        }
    }

    [FramePath]
    internal static void Round(
        Ui ui,
        IconGlyph glyph,
        float size,
        Color color,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                background: t.Color(ColorToken.BgTranslucent),
                border: new Border(1f, t.Color(ColorToken.Border)),
                radius: 999,
                key: key,
                file: file,
                line: line
            )
        )
            ui.Icon(glyph: glyph, size: size, color: color);
    }
}

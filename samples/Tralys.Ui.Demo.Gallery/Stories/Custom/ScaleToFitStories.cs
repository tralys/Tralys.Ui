using System.Runtime.CompilerServices;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<ScaleToFit>("Custom")]
static class ScaleToFitStories
{
    [FramePath]
    public static void Modes(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption("One badge scaled into a 140×90 frame: None, Fill, Contain, Cover");
            using (ui.Box(gap: 12))
            {
                Framed(ui, ObjectFit.None, default, "No. 120", false);
                Framed(ui, ObjectFit.Fill, default, "No. 120", false);
                Framed(ui, ObjectFit.Contain, default, "No. 120", false);
                Framed(ui, ObjectFit.Cover, default, "No. 120", false);
            }
        }
    }

    [FramePath]
    public static void Directions(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "Direction: DownOnly keeps a small child at its size; UpOnly keeps a large one"
            );
            using (ui.Box(gap: 12))
            {
                Framed(ui, ObjectFit.Contain, FitDirection.DownOnly, "small", false);
                Framed(ui, ObjectFit.Contain, FitDirection.DownOnly, "400×120 shrinks", true);
                Framed(ui, ObjectFit.Contain, FitDirection.UpOnly, "small grows", false);
                Framed(ui, ObjectFit.Contain, FitDirection.UpOnly, "400×120 stays", true);
            }
        }
    }

    [FramePath]
    static void Framed(
        Ui ui,
        ObjectFit fit,
        FitDirection direction,
        string label,
        bool large,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        using (
            ui.Box(
                direction: Direction.Column,
                width: 140,
                height: 90,
                overflow: Overflow.Clip,
                background: t.Color(ColorToken.BgSunken),
                border: new(1, t.Color(ColorToken.Border)),
                key: key,
                file: file,
                line: line
            )
        )
        using (ui.ScaleToFit(fit: fit, direction: direction, grow: 1))
        using (
            ui.Box(
                padding: new Thickness(6, 2, 6, 2),
                width: large ? 400 : Length.Auto,
                height: large ? 120 : Length.Auto,
                background: t.Color(ColorToken.BgAccentMuted),
                border: new(1, t.Color(ColorToken.BorderAccent))
            )
        )
            ui.Text(label);
    }
}

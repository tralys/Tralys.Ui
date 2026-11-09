using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Progress>("Progress")]
static class ProgressStories
{
    [FramePath]
    public static void Variants(Ui ui)
    {
        var t = ui.Theme;
        var muted = t.Color(ColorToken.FgMuted);
        var weight = t.Weight(WeightToken.Interface);
        using (ui.Box(direction: Direction.Column, gap: 32, width: 300))
        {
            using (ui.Box(direction: Direction.Column, gap: 8))
            {
                ui.Text(
                    "Default",
                    color: muted,
                    fontWeight: weight,
                    variant: TextVariant.Caption,
                    wrap: true
                );
                ui.Progress(value: 60);
            }
            using (ui.Box(direction: Direction.Column, gap: 8))
            {
                ui.Text(
                    "Indeterminate",
                    color: muted,
                    fontWeight: weight,
                    variant: TextVariant.Caption,
                    wrap: true
                );
                ui.Progress(indeterminate: true);
            }
        }
    }

    [FramePath]
    public static void Fill(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 32, width: 300))
        {
            ui.Progress(value: 0, height: 10);
            ui.Progress(value: 35, height: 10);
            ui.Progress(value: 100, height: 10);
        }
    }
}

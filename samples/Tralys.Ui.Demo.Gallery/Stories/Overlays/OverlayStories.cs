using Tralys.Ui.Components;
using Tralys.Ui.Fonts;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Overlay>("Overlays")]
static class OverlayStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var t = ui.Theme;
        var open = ui.State(true);
        using (ui.Box(direction: Direction.Column, gap: 16))
        {
            ui.Text(
                "An overlay scrims the area it fills and centres its content",
                color: t.Color(ColorToken.FgMuted),
                fontWeight: t.Weight(WeightToken.Interface),
                wrap: true
            );
            using (ui.Box(width: 480, height: 240, align: Align.Center, justify: Justify.Center))
            {
                ui.Button(text: "Open overlay", onClick: StoryOpen.Opener(open));
                using (
                    ui.Overlay(
                        open: open.Value,
                        onOpenChange: open.Setter,
                        position: Position.Absolute,
                        left: 0,
                        top: 0,
                        right: 0,
                        bottom: 0
                    )
                )
                using (ui.Box(direction: Direction.Column, gap: 8, margin: 16))
                {
                    ui.Text("Modal Overlay", fontWeight: FontWeight.SemiBold, wrap: true);
                    ui.Text(
                        "Content displayed on top",
                        color: t.Color(ColorToken.FgMuted),
                        fontWeight: t.Weight(WeightToken.Interface),
                        wrap: true
                    );
                }
            }
        }
    }

    [FramePath]
    public static void Screen(Ui ui)
    {
        var t = ui.Theme;
        var open = ui.State(true);
        var beneath = ui.Remember(static () => new ObservableText("Focus me, then open"));
        var name = ui.Remember(static () => new ObservableText("Aria"));
        var second = ui.Remember(static () => new ObservableText("A second Tab stop"));
        using (ui.Box(width: 640, height: 360))
        {
            using (
                ui.Box(
                    direction: Direction.Column,
                    gap: 8,
                    grow: 1,
                    padding: 16,
                    background: t.Color(ColorToken.BgSunken)
                )
            )
            {
                ui.Text(
                    "The screen beneath",
                    variant: TextVariant.Heading,
                    fontWeight: t.Weight(WeightToken.Interface),
                    wrap: true
                );
                ui.Text(
                    "While the surface is active, presses land on it, Tab keeps to the dialog, and closing gives the focus back.",
                    color: t.Color(ColorToken.FgMuted),
                    fontWeight: t.Weight(WeightToken.Interface),
                    wrap: true
                );
                ui.Input(text: beneath, readOnly: true, width: 240, alignSelf: AlignSelf.Start);
                ui.Button(
                    text: "Open the dialog",
                    onClick: StoryOpen.Opener(open),
                    alignSelf: AlignSelf.Start
                );
            }
            using (
                ui.Overlay(
                    open: open.Value,
                    onOpenChange: open.Setter,
                    position: Position.Absolute,
                    left: 0,
                    top: 0,
                    right: 0,
                    bottom: 0
                )
            )
            using (ui.Box(direction: Direction.Column, gap: 10, width: 318, padding: 12))
            {
                ui.Text(
                    "Rename profile",
                    variant: TextVariant.Heading,
                    fontWeight: t.Weight(WeightToken.Interface),
                    wrap: true
                );
                ui.Input(text: name, readOnly: true);
                ui.Input(text: second, readOnly: true);
                ui.Button(
                    text: "Close (Esc)",
                    onClick: StoryOpen.Closer(open),
                    alignSelf: AlignSelf.End
                );
            }
        }
    }
}

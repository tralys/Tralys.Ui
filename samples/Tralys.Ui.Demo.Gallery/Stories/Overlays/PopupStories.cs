using Tralys.Ui.Components;
using Tralys.Ui.Fonts;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Popover>("Overlays")]
static class PopupStories
{
    static readonly Slot Panel = Slot.Of(static ui =>
    {
        using (ui.Box(direction: Direction.Column, gap: 4, width: 200))
        {
            ui.Text("Popup content", fontWeight: FontWeight.SemiBold);
            ui.Text("Click outside to close", color: ui.Theme.Color(ColorToken.FgMuted));
            ui.Button(text: "Action", size: ComponentSize.Sm);
        }
    });

    [FramePath]
    public static void Default(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption("The trigger opens it below; a click outside closes it");
            using (ui.Popover(content: Panel, alignSelf: AlignSelf.Start))
            using (ui.PopoverTrigger())
            using (
                ui.Box(
                    padding: new Thickness(8, 4, 8, 4),
                    background: t.Color(ColorToken.Bg),
                    border: new(1, t.Color(ColorToken.Border)),
                    radius: t.Radius(RadiusToken.Md)
                )
            )
                ui.Text("Toggle popup");
        }
    }
}

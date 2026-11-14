using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<PickerField>("Pickers")]
static class PickerPopupStories
{
    static readonly Slot AnyContent = Slot.Of(static ui =>
        ui.Text("Any content: a grid of tiles, a short form", wrap: true)
    );

    static readonly Slot NothingPicked = Slot.Of(static ui => ui.Text("Nothing picked yet"));

    [FramePath]
    public static void Popups(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                width: 240,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.PickerField(text: "Soft", sublabel: "Quality", content: AnyContent);
            ui.PickerField(placeholder: "Pick a tile", content: NothingPicked);
            ui.PickerField(text: "Soft", sublabel: "Quality", disabled: true);
            ui.PickerField(text: "Small field", size: ComponentSize.Sm);
        }
    }
}

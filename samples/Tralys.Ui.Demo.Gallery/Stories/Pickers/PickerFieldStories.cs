using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<PickerField>("Pickers")]
static class PickerFieldStories
{
    static readonly (ComponentSize Size, string Text)[] Rungs =
    [
        (ComponentSize.Xs, "Extra small"),
        (ComponentSize.Sm, "Small"),
        (ComponentSize.Md, "Medium"),
        (ComponentSize.Lg, "Large"),
    ];

    static readonly string[] Hairs = ["Short", "Long", "Braid", "Bun"];

    static readonly Slot HairStyles = Slot.Of(static ui =>
    {
        using (ui.Grid(columns: GridTracks.Repeat(4, Track.Fr(1))))
            for (var i = 0; i < Hairs.Length; i++)
                ui.Button(text: Hairs[i], margin: new Thickness(2f), key: i);
    });

    [FramePath]
    public static void States(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 8, width: 280))
        {
            HoldingListBoxStories.Caption(
                ui,
                "The field a picker opens from: a press opens it, turning the chevron and lighting the border"
            );
            ui.PickerField(text: "Europe West", sublabel: "Server");
            ui.PickerField(text: "Europe West", sublabel: "Server", open: true);
            ui.PickerField(placeholder: "Choose an account");
            ui.PickerField(text: "Weekend", hasArrow: false);
            ui.PickerField(text: "Europe West", variant: FieldVariant.Flush);
            ui.PickerField(text: "Europe West", sublabel: "Server", disabled: true);
        }
    }

    [FramePath]
    public static void Sizes(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 8, width: 280))
        {
            foreach (var (size, text) in Rungs)
                ui.PickerField(text: text, size: size, key: (int)size);
        }
    }

    [FramePath]
    public static void Open(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, width: 320, height: 220))
            ui.PickerField(
                text: "Short",
                sublabel: "Hair",
                content: HairStyles,
                defaultOpen: true,
                width: 260
            );
    }
}

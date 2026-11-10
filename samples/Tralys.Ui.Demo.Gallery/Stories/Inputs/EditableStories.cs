using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Editable>("Inputs")]
static class EditableStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var name = ui.State("Click to rename");
        var note = ui.State("");
        using (ui.Box(direction: Direction.Column, gap: 8, padding: 16, width: 272))
        {
            ui.Editable(value: name.Value, onValueChange: name.Setter);
            ui.Editable(value: note.Value, onValueChange: note.Setter, placeholder: "Add a note…");
            ui.Editable(value: "Disabled", disabled: true);
        }
    }

    [FramePath]
    public static void Sizes(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 8, padding: 16, width: 272))
        {
            ui.Editable(value: "Extra small", size: ComponentSize.Xs);
            ui.Editable(value: "Small", size: ComponentSize.Sm);
            ui.Editable(value: "Medium");
        }
    }

    [FramePath]
    public static void Hex(Ui ui)
    {
        var color = ui.State(default(Color));
        using (ui.Box(direction: Direction.Column, gap: 8, padding: 16, width: 252))
        {
            ui.ColorPicker(
                value: color.Value,
                onValueChange: color.Setter,
                showHex: false,
                showSwatches: false,
                size: ComponentSize.Xs,
                height: 160
            );
            var c = color.Value;
            ui.Editable(
                value: ui.Format($"#{c.R:X2}{c.G:X2}{c.B:X2}"),
                onValueChange: Callback<string>.Of(
                    color,
                    static (color, typed) => Typed(color, typed)
                ),
                size: ComponentSize.Sm,
                width: 96,
                alignSelf: AlignSelf.Start
            );
        }
    }

    static void Typed(State<Color> color, string typed)
    {
        var digits = typed.AsSpan().Trim().TrimStart('#');
        if (
            digits.Length == 6
            && uint.TryParse(
                digits,
                System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture,
                out var hex
            )
        )
            color.Value = Color.FromRgb((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex);
    }
}

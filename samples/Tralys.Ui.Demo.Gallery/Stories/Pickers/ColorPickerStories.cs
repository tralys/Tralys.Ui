using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<ColorPicker>("Pickers")]
static class ColorPickerStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var color = ui.State(default(Color));
        ui.ColorPicker(value: color.Value, onValueChange: color.Setter);
    }

    [FramePath]
    public static void Full(Ui ui)
    {
        var color = ui.State(Color.FromArgb(0x80, 0xC0, 0x50, 0x28));
        ui.ColorPicker(
            value: color.Value,
            onValueChange: color.Setter,
            showAlpha: true,
            showPrevious: true,
            swatchColumns: 3
        );
    }
}

[Stories<ColorPicker.Select>("Pickers")]
static class ColorPickerSelectStories
{
    static readonly ComponentSize[] Rungs =
    [
        ComponentSize.Xxs,
        ComponentSize.Xs,
        ComponentSize.Sm,
        ComponentSize.Md,
        ComponentSize.Lg,
    ];

    [FramePath]
    public static void Default(Ui ui)
    {
        var orange = ui.State(Color.FromRgb(0xFF, 0x55, 0x00));
        var blue = ui.State(Color.FromArgb(0x80, 0x33, 0x99, 0xFF));
        using (ui.Box(gap: 16))
        {
            ui.ColorPickerSelect(value: orange.Value, onValueChange: orange.Setter);
            ui.ColorPickerSelect(value: blue.Value, onValueChange: blue.Setter);
            ui.ColorPickerSelect(value: Color.FromRgb(0x66, 0xCC, 0x66), disabled: true);
        }
    }

    [FramePath]
    public static void Sizes(Ui ui)
    {
        using (ui.Box(gap: 16))
        {
            foreach (var size in Rungs)
                ui.ColorPickerSelect(
                    value: Color.FromRgb(0xFF, 0x55, 0x00),
                    size: size,
                    key: (int)size
                );
        }
    }
}

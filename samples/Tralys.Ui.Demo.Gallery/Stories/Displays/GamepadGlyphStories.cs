using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<GamepadGlyph>("Displays")]
static class GamepadGlyphStories
{
    static readonly GamepadStyle[] Pads =
    [
        GamepadStyle.Xbox,
        GamepadStyle.PlayStation,
        GamepadStyle.Nintendo,
    ];

    static readonly GamepadButton[] Buttons =
    [
        GamepadButton.South,
        GamepadButton.East,
        GamepadButton.West,
        GamepadButton.North,
        GamepadButton.LeftShoulder,
        GamepadButton.RightTrigger,
        GamepadButton.Start,
    ];

    static readonly GamepadButton[] DPad =
    [
        GamepadButton.DPadUp,
        GamepadButton.DPadDown,
        GamepadButton.DPadLeft,
        GamepadButton.DPadRight,
    ];

    [FramePath]
    public static void Styles(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 24))
        {
            foreach (var style in Pads)
                using (ui.Box(gap: 16, key: (long)style))
                    foreach (var button in Buttons)
                        ui.GamepadGlyph(button: button, style: style, key: (long)button);
            using (ui.Box(gap: 16))
                foreach (var button in DPad)
                    ui.GamepadGlyph(button: button, key: (long)button);
        }
    }
}

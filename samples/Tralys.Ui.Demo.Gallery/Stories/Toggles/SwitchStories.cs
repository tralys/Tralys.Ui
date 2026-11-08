using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Switch>("Toggles")]
static class SwitchStories
{
    [FramePath]
    public static void States(Ui ui)
    {
        var on = ui.State(true);
        using (ui.Box(direction: Direction.Column, gap: 12, padding: 16))
        {
            ui.Switch(value: on.Value, onValueChange: on.Setter, label: "Notifications");
            ui.Switch(value: false, label: "Off");
            ui.Switch(value: true, label: "Disabled", disabled: true);
        }
    }

    [FramePath]
    public static void Sizes(Ui ui)
    {
        using (ui.Box(align: Align.Center, gap: 12, padding: 16))
        {
            ui.Switch(value: true, size: ComponentSize.Sm);
            ui.Switch(value: true, size: ComponentSize.Md);
            ui.Switch(value: true, size: ComponentSize.Lg);
        }
    }

    [FramePath]
    public static void Default(Ui ui)
    {
        var off = ui.State(false);
        var on = ui.State(true);
        using (ui.Box(direction: Direction.Column, gap: 16, padding: 16))
        {
            ui.Switch(value: off.Value, onValueChange: off.Setter, label: "Off");
            ui.Switch(value: on.Value, onValueChange: on.Setter, label: "On");
        }
    }

    [FramePath]
    public static void Disabled(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 16, padding: 16))
        {
            ui.Switch(value: false, label: "Disabled Off", disabled: true);
            ui.Switch(value: true, label: "Disabled On", disabled: true);
        }
    }

    [FramePath]
    public static void Sides(Ui ui)
    {
        var left = ui.State(false);
        var right = ui.State(true);
        using (ui.Box(direction: Direction.Column, gap: 16, padding: 16, width: 232))
        {
            ui.Switch(value: left.Value, onValueChange: left.Setter, label: "Switch on the left");
            ui.Switch(
                value: right.Value,
                onValueChange: right.Setter,
                label: "Switch on the right",
                side: SwitchSide.Right
            );
        }
    }
}

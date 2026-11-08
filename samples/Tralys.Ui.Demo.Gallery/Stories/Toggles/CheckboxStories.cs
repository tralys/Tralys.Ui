using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Checkbox<bool>>("Toggles")]
static class CheckboxStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var off = ui.State(false);
        var on = ui.State(true);
        using (ui.Box(direction: Direction.Column, gap: 16, padding: 16))
        {
            ui.Checkbox(value: off.Value, onValueChange: off.Setter, label: "Unchecked");
            ui.Checkbox(value: on.Value, onValueChange: on.Setter, label: "Checked");
            ui.Checkbox(value: false, label: "Disabled", disabled: true);
            ui.Checkbox(value: true, label: "Disabled checked", disabled: true);
        }
    }
}

[Stories<Checkbox<CheckState>>("Toggles")]
static class TriStateCheckboxStories
{
    [FramePath]
    public static void Indeterminate(Ui ui)
    {
        var undecided = ui.State(CheckState.Indeterminate);
        using (ui.Box(direction: Direction.Column, gap: 16, padding: 16))
        {
            ui.Checkbox(
                value: undecided.Value,
                onValueChange: undecided.Setter,
                label: "Indeterminate"
            );
            ui.Checkbox(
                value: CheckState.Indeterminate,
                label: "Indeterminate disabled",
                disabled: true
            );
        }
    }

    [FramePath]
    public static void ThreeState(Ui ui)
    {
        var three = ui.State(CheckState.Unchecked);
        var tipped = ui.State(false);
        using (ui.Box(direction: Direction.Column, gap: 16, padding: 16))
        {
            ui.Checkbox(
                value: three.Value,
                onValueChange: three.Setter,
                label: "Click through checked, undecided and unchecked"
            );
            ui.Checkbox(
                value: tipped.Value,
                onValueChange: tipped.Setter,
                label: "With an info tip",
                infoTip: "Explains the setting"
            );
        }
    }
}

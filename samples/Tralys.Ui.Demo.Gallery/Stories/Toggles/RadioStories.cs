using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Radio>("Toggles")]
static class RadioStories
{
    [FramePath]
    public static void Checked(Ui ui)
    {
        var picked = ui.State<UiKey>("B");
        using (ui.Box(padding: 16))
        using (ui.RadioGroup(value: picked.Value, onValueChange: picked.Setter))
        {
            ui.Radio(value: "A", label: "Option A");
            ui.Radio(value: "B", label: "Option B");
            ui.Radio(value: "C", label: "Option C");
        }
    }

    [FramePath]
    public static void Disabled(Ui ui)
    {
        using (ui.Box(padding: 16))
        using (ui.RadioGroup(value: "B"))
        {
            ui.Radio(value: "A", label: "Option A", disabled: true);
            ui.Radio(value: "B", label: "Option B (selected)", disabled: true);
        }
    }

    [FramePath]
    public static void ByParent(Ui ui)
    {
        var size = ui.State<UiKey>("Small");
        var colour = ui.State<UiKey>("Green");
        using (ui.Box(gap: 32, padding: 16))
        {
            using (ui.RadioGroup(value: size.Value, onValueChange: size.Setter))
            {
                ui.Radio(value: "Small", label: "Small");
                ui.Radio(value: "Medium", label: "Medium");
                ui.Radio(value: "Large", label: "Large");
            }
            using (ui.RadioGroup(value: colour.Value, onValueChange: colour.Setter))
            {
                ui.Radio(value: "Red", label: "Red");
                ui.Radio(value: "Green", label: "Green");
                ui.Radio(value: "Blue", label: "Blue");
            }
        }
    }
}

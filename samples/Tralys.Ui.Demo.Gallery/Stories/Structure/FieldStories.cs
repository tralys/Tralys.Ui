using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Field>("Structure")]
static class FieldStories
{
    static readonly string[] Qualities = ["High", "Low"];

    static readonly IItemsSource<string> QualityItems = ItemsSource.From(Qualities);

    static readonly KeyOf<string> ByName = static name => name;

    static readonly TextOf<string> Named = static name => name;

    [FramePath]
    public static void Default(Ui ui)
    {
        const float label = 140f;
        var name = ui.Remember(static () => new ObservableText());
        using (ui.Box(direction: Direction.Column, gap: 8, padding: 16, width: 352))
        {
            using (ui.Field(label: "Level", value: "112", labelWidth: label)) { }
            using (
                ui.Field(
                    label: "Weekly commits",
                    value: "1,204",
                    infoTip: "Counts every commit this week",
                    labelWidth: label
                )
            ) { }
            using (ui.Field(label: "Team", placeholder: "None", labelWidth: label)) { }
            using (ui.Field(label: "Name", labelWidth: label))
                ui.Input(text: name, placeholder: "Type a name");
        }
    }

    [FramePath]
    public static void Formats(Ui ui)
    {
        const float label = 85f;
        using (ui.Box(direction: Direction.Column, gap: 8, padding: 16, width: 352))
        {
            using (ui.Field(label: "Members", value: ui.Format($"{1845:N0}"), labelWidth: label))
            { }
            using (ui.Field(label: "Bonus", value: ui.Format($"{15:+0;-0;0}%"), labelWidth: label))
            { }
            using (
                ui.Field(label: "Win rate", value: ui.Format($"{66.66:0.0}%"), labelWidth: label)
            ) { }
            using (
                ui.Field(
                    label: "Experience",
                    value: ui.Format($"{1200:N0} / {40000:N0}"),
                    labelWidth: label
                )
            ) { }
            using (ui.Field(label: "Joined", placeholder: "Not yet", labelWidth: label)) { }
        }
    }

    [FramePath]
    public static void Vertical(Ui ui)
    {
        var motto = ui.Remember(static () => new ObservableText());
        var project = ui.Remember(static () => new ObservableText("Dreamland"));
        using (ui.Box(direction: Direction.Column, gap: 16, padding: 16, width: 312))
        {
            using (
                ui.Field(
                    label: "Motto",
                    description: "Shown to everyone who opens your profile",
                    orientation: Orientation.Vertical
                )
            )
                ui.Input(text: motto, placeholder: "Say something");
            using (
                ui.Field(
                    label: "Project name",
                    error: "That name is taken",
                    orientation: Orientation.Vertical
                )
            )
                ui.Input(text: project, invalid: true);
        }
    }

    [FramePath]
    public static void WithSliders(Ui ui)
    {
        const float label = 110f;
        var quality = ui.State<UiKey>("High");
        var music = ui.State(40.0);
        var scale = ui.State(100.0);
        using (ui.Box(direction: Direction.Column, gap: 8, padding: 16, width: 392))
        {
            ui.SliderField(
                label: "Music",
                value: music.Value,
                onValueChange: music.Setter,
                unit: "%",
                labelWidth: label
            );
            ui.SliderField(
                label: "Interface scale",
                value: scale.Value,
                onValueChange: scale.Setter,
                min: 50,
                max: 200,
                unit: "%",
                labelWidth: label
            );
            using (ui.Field(label: "Quality", labelWidth: label))
                ui.Combobox(
                    items: QualityItems,
                    itemKey: ByName,
                    itemText: Named,
                    value: quality.Value,
                    onValueChange: quality.Setter
                );
        }
    }
}

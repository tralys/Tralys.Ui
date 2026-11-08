using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<RadioGroup>("Toggles")]
static class RadioGroupStories
{
    static readonly string[] Roles = ["Engineer", "Designer", "Writer"];
    static readonly string[] Tiers = ["Gold", "Silver", "Bronze"];
    static readonly string[] Letters = ["A", "B", "C", "D", "E", "F"];
    static readonly string[] Numbers = ["1", "2", "3", "4", "5"];

    static readonly Action<Ui, string> Numbered = static (ui, text) => ui.Text(text, margin: 8);

    static readonly Action<Ui, string> Pictured = static (ui, text) =>
    {
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                width: 64,
                height: 48,
                background: ui.Theme.Color(ColorToken.BgRaised)
            )
        )
            ui.Text(text);
    };

    [FramePath]
    public static void Default(Ui ui)
    {
        var role = ui.State<UiKey>("Engineer");
        var tier = ui.State<UiKey>("Gold");
        var muted = ui.Theme.Color(ColorToken.FgMuted);
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 32,
                padding: 16,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Text("Vertical", color: muted, wrap: true);
            using (ui.RadioGroup(value: role.Value, onValueChange: role.Setter))
                foreach (var name in Roles)
                    ui.Radio(value: name, label: name, key: name);
            ui.Text("Horizontal", color: muted, wrap: true);
            using (
                ui.RadioGroup(
                    value: tier.Value,
                    onValueChange: tier.Setter,
                    direction: Direction.Row
                )
            )
                foreach (var name in Tiers)
                    ui.Radio(value: name, label: name, key: name);
        }
    }

    [FramePath]
    public static void Segmented(Ui ui)
    {
        var number = ui.State<UiKey>(2);
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 32,
                padding: 16,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Text(
                "Segmented, with unavailable options",
                color: ui.Theme.Color(ColorToken.FgMuted),
                wrap: true
            );
            using (
                ui.RadioGroup(
                    value: number.Value,
                    onValueChange: number.Setter,
                    variant: RadioGroupVariant.Segmented,
                    direction: Direction.Row
                )
            )
                for (var i = 0; i < Numbers.Length; i++)
                    ui.Radio(value: i + 1, label: Numbers[i], disabled: i >= 3, key: i);
        }
    }

    [FramePath]
    public static void Card(Ui ui)
    {
        var number = ui.State<UiKey>(2);
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 32,
                padding: 16,
                width: 272,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Text(
                "Cards in three columns, with unavailable options",
                color: ui.Theme.Color(ColorToken.FgMuted),
                wrap: true
            );
            using (
                ui.RadioGroup(
                    value: number.Value,
                    onValueChange: number.Setter,
                    variant: RadioGroupVariant.Card,
                    columns: 3
                )
            )
                for (var i = 0; i < Numbers.Length; i++)
                    ui.Radio(
                        value: i + 1,
                        content: Slot.Of(Numbers[i], Numbered),
                        disabled: i >= 3,
                        key: i
                    );
        }
    }

    [FramePath]
    public static void Rows(Ui ui)
    {
        var letter = ui.State<UiKey>("B");
        var tier = ui.State<UiKey>("Gold");
        var muted = ui.Theme.Color(ColorToken.FgMuted);
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 32,
                padding: 16,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Text(
                "Segmented over two rows; the arrows move across the grid",
                color: muted,
                wrap: true
            );
            using (
                ui.RadioGroup(
                    value: letter.Value,
                    onValueChange: letter.Setter,
                    variant: RadioGroupVariant.Segmented,
                    direction: Direction.Row,
                    rows: 2
                )
            )
                foreach (var name in Letters)
                    ui.Radio(value: name, label: name, key: name);
            ui.Text("Segmented, stacked", color: muted, wrap: true);
            using (
                ui.RadioGroup(
                    value: tier.Value,
                    onValueChange: tier.Setter,
                    variant: RadioGroupVariant.Segmented,
                    size: ComponentSize.Sm
                )
            )
                foreach (var name in Tiers)
                    ui.Radio(value: name, label: name, key: name);
        }
    }

    [FramePath]
    public static void Tile(Ui ui)
    {
        var tier = ui.State<UiKey>("Gold");
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 32,
                padding: 16,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Text(
                "Tiles, dimmed until picked",
                color: ui.Theme.Color(ColorToken.FgMuted),
                wrap: true
            );
            using (
                ui.RadioGroup(
                    value: tier.Value,
                    onValueChange: tier.Setter,
                    variant: RadioGroupVariant.Tile,
                    direction: Direction.Row
                )
            )
                foreach (var name in Tiers)
                    ui.Radio(value: name, content: Slot.Of(name, Pictured), key: name);
        }
    }
}

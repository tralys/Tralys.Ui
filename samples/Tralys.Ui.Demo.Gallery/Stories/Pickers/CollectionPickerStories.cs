using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<CollectionPicker<Garment>>("Pickers")]
static class CollectionPickerStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var worn = ui.State<UiKey>(Garment.Worn);
        var other = ui.State<UiKey>(default);
        using (ui.Box(direction: Direction.Column, gap: 8, width: 240))
        {
            ui.CollectionPicker(
                items: Garment.All,
                itemKey: Garment.ByName,
                itemText: Garment.Named,
                value: worn.Value,
                onValueChange: worn.Setter,
                sublabel: "Garment"
            );
            ui.CollectionPicker(
                items: Garment.All,
                itemKey: Garment.ByName,
                itemText: Garment.Named,
                value: other.Value,
                onValueChange: other.Setter
            );
        }
    }

    [FramePath]
    public static void Open(Ui ui)
    {
        var worn = ui.State<UiKey>(Garment.Worn);
        using (ui.Box(direction: Direction.Column, align: Align.Start, width: 420, height: 440))
            ui.CollectionPicker(
                items: Garment.All,
                itemKey: Garment.ByName,
                itemText: Garment.Named,
                value: worn.Value,
                onValueChange: worn.Setter,
                defaultOpen: true,
                defaultLayout: ListLayout.Rows,
                sublabel: "Garment",
                width: 240
            );
    }
}

/// <summary>A garment a stylist's picker searches by name.</summary>
sealed class Garment(string name)
{
    static readonly string[] Cuts = ["Silk", "Linen", "Leather", "Velvet", "Wool", "Cotton"];
    static readonly string[] Kinds = ["Robe", "Tunic", "Cloak", "Vest", "Sash"];
    static readonly Garment[] Garments = Make();

    public static readonly IItemsSource<Garment> All = ItemsSource.From(Garments);

    public static readonly UiKey Worn = Garments[7].Name;

    public static readonly KeyOf<Garment> ByName = static g => g.Name;

    public static readonly TextOf<Garment> Named = static g => g.Name;

    public string Name { get; } = name;

    static Garment[] Make()
    {
        var garments = new Garment[Cuts.Length * Kinds.Length];
        for (var i = 0; i < garments.Length; i++)
            garments[i] = new($"{Cuts[i % Cuts.Length]} {Kinds[i / Cuts.Length]}");
        return garments;
    }
}

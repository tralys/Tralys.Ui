using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<CheckList<AssetKind>>("Toggles")]
static class CheckListStories
{
    [FramePath]
    public static void Grouped(Ui ui)
    {
        var filter = ui.Remember(static () => new CheckListFilter(["Images", "Video", "Sheets"]));
        ui.CheckList(
            items: AssetKind.All,
            itemKey: AssetKind.ByLabel,
            itemText: AssetKind.Labelled,
            itemGroup: AssetKind.Grouped,
            value: filter.Checked,
            onValueChange: filter.Change,
            width: 220,
            maxHeight: 240
        );
    }
}

/// <summary>A kind of asset an asset browser can show, in its group.</summary>
sealed record AssetKind(string Label, string Group)
{
    public static readonly IItemsSource<AssetKind> All = ItemsSource.From(
        new AssetKind[]
        {
            new("Images", "Media"),
            new("Audio", "Media"),
            new("Video", "Media"),
            new("Text", "Documents"),
            new("Sheets", "Documents"),
            new("Slides", "Documents"),
            new("Scripts", "Code"),
            new("Shaders", "Code"),
        }
    );

    public static readonly KeyOf<AssetKind> ByLabel = static kind => kind.Label;

    public static readonly TextOf<AssetKind> Labelled = static kind => kind.Label;

    public static readonly TextOf<AssetKind> Grouped = static kind => kind.Group;
}

using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Tree.Item>("Data")]
static class TreeItemStories
{
    static readonly Action<Ui, Branch> Badged = static (ui, branch) =>
    {
        using (ui.Box(align: Align.Center, gap: 8))
        {
            ui.Text(branch.Name);
            ui.Badge(count: branch.Badge);
        }
    };

    static readonly Slot<Branch> StateRows = Slot<Branch>.Of(
        static (ui, branch, _) =>
        {
            if (branch.Badge > 0)
                ui.TreeItem(content: Slot.Of(branch, Badged));
            else
                ui.TreeItem(text: branch.Name, disabled: branch.Locked);
        }
    );

    static readonly Slot<Branch> FlatRow = Slot<Branch>.Of(
        static (ui, branch, _) =>
            ui.TreeItem(
                text: branch.Name,
                depth: branch.Depth,
                hasChildren: branch.HasChildren,
                expanded: branch.IsExpanded,
                onExpandedChange: Callback<bool>.Of(branch, static (b, open) => b.IsExpanded = open)
            )
    );

    [FramePath]
    public static void States(Ui ui)
    {
        var forest = ui.Remember(static () => Forest.States());
        var value = ui.State(forest.Selected);
        using (ui.Box(padding: 16))
            ui.Tree(
                items: forest.Rows,
                itemKey: Branch.Key,
                itemText: Branch.Named,
                item: StateRows,
                value: value.Value,
                onValueChange: value.Setter,
                onExpandedChange: Branch.Expand,
                width: 240,
                height: 200
            );
    }

    [FramePath]
    public static void Levels(Ui ui)
    {
        var forest = ui.Remember(static () => Forest.Levels());
        var value = ui.State(forest.Selected);
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 4, padding: 16))
        {
            Caption(ui, "A row's indent: four spacing units for every level above it");
            ui.Tree(
                items: forest.Rows,
                itemKey: Branch.Key,
                itemText: Branch.Named,
                value: value.Value,
                onValueChange: value.Setter,
                onExpandedChange: Branch.Expand,
                width: 240,
                height: 120
            );
        }
    }

    [FramePath]
    public static void FlatRows(Ui ui)
    {
        var forest = ui.Remember(static () => Forest.Thousand(opened: true));
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 4, padding: 16))
        {
            Caption(
                ui,
                "A thousand places as one virtualized list, each row indented by its depth"
            );
            ui.VirtualList(
                items: forest.Rows,
                itemKey: Branch.Key,
                row: FlatRow,
                width: 240,
                height: 240
            );
        }
    }

    [FramePath]
    static void Caption(
        Ui ui,
        string text,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    ) =>
        ui.Text(
            text,
            color: ui.Theme.Color(ColorToken.FgMuted),
            fontWeight: ui.Theme.Weight(WeightToken.Interface),
            wrap: true,
            file: file,
            line: line
        );
}

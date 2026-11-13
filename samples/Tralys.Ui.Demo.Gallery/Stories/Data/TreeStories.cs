using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Tree<Branch>>("Data")]
static class TreeStories
{
    static readonly Slot<Branch> Locking = Slot<Branch>.Of(
        static (ui, branch, _) => ui.TreeItem(text: branch.Name, disabled: branch.Locked)
    );

    static readonly Action<Ui, Branch> Count = static (ui, branch) =>
    {
        using (ui.Box(align: Align.Center, gap: 8))
        {
            ui.Text(branch.Name);
            ui.Text(
                ui.Format($"{branch.Children.Length}"),
                variant: TextVariant.Caption,
                color: ui.Theme.Color(ColorToken.FgMuted)
            );
        }
    };

    static readonly Slot<Branch> Counted = Slot<Branch>.Of(
        static (ui, branch, _) => ui.TreeItem(content: Slot.Of(branch, Count))
    );

    [FramePath]
    public static void Default(Ui ui) =>
        Show(ui, ui.Remember(static () => Forest.Staff()), 220, 200);

    [FramePath]
    public static void DeepSelection(Ui ui) =>
        Show(ui, ui.Remember(static () => Forest.Lisbon()), 240, 200);

    [FramePath]
    public static void Disabled(Ui ui) =>
        Show(ui, ui.Remember(static () => Forest.Locks()), 220, 160, Locking);

    [FramePath]
    public static void LongLabels(Ui ui) =>
        Show(ui, ui.Remember(static () => Forest.LongLabels()), 200, 160);

    [FramePath]
    public static void Bound(Ui ui) =>
        Show(ui, ui.Remember(static () => Forest.Atlas()), 240, 220, Counted);

    [FramePath]
    public static void Thousand(Ui ui) =>
        Show(ui, ui.Remember(static () => Forest.Thousand(opened: false)), 240, 320);

    [FramePath]
    static void Show(
        Ui ui,
        Forest forest,
        float width,
        float height,
        Slot<Branch> item = default,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    )
    {
        using (ui.Fragment(file: file, line: line))
        {
            var value = ui.State(forest.Selected);
            using (ui.Box(padding: 16))
                ui.Tree(
                    items: forest.Rows,
                    itemKey: Branch.Key,
                    itemText: Branch.Named,
                    item: item,
                    value: value.Value,
                    onValueChange: value.Setter,
                    onExpandedChange: Branch.Expand,
                    width: width,
                    height: height
                );
        }
    }
}

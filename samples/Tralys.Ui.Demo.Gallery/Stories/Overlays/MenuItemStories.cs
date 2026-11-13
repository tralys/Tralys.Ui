using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Menu.Item>("Overlays")]
static class MenuItemStories
{
    static readonly Slot Rows = Slot.Of(static ui =>
    {
        var checkable = ui.State(false);
        var checkedRow = ui.State(true);
        ui.MenuItem(text: "With an icon", icon: Icons.Star, shortcut: "Ctrl+I");
        ui.MenuItem(
            text: "Checked",
            checkable: true,
            @checked: checkedRow.Value,
            onCheckedChange: checkedRow.Setter
        );
        ui.MenuItem(
            text: "Checkable",
            checkable: true,
            @checked: checkable.Value,
            onCheckedChange: checkable.Setter
        );
        ui.MenuItem(text: "Disabled", shortcut: "Ctrl+D", disabled: true);
        ui.MenuItem(text: "Opens a submenu", items: Inside);
        ui.MenuSeparator();
        ui.MenuItem(text: "Destructive", destructive: true);
    });

    static readonly Slot Inside = Slot.Of(static ui => ui.MenuItem(text: "Inside"));

    [FramePath]
    public static void States(Ui ui)
    {
        var area = ui.Ref();
        using (ui.Box(width: 320, height: 300, padding: 16, align: Align.Start))
        using (ui.Box(width: Length.Full, height: Length.Full, nodeRef: area))
            ui.Menu(open: true, anchor: Anchor.Within(area, default), content: Rows);
    }
}

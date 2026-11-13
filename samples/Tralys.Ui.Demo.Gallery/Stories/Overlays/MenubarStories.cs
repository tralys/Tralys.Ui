using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Menubar>("Overlays")]
static class MenubarStories
{
    static readonly Slot File = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "New map", shortcut: "Ctrl+N");
        ui.MenuItem(text: "Open recent", items: Recent);
        ui.MenuSeparator();
        ui.MenuItem(text: "Quit", destructive: true);
    });

    static readonly Slot Recent = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Edinburgh.map");
        ui.MenuItem(text: "Cairo.map");
    });

    static readonly Slot View = Slot.Of(static ui =>
    {
        var grid = ui.State(true);
        var snap = ui.State(false);
        ui.MenuItem(
            text: "Show grid",
            checkable: true,
            @checked: grid.Value,
            onCheckedChange: grid.Setter
        );
        ui.MenuItem(
            text: "Snap to grid",
            checkable: true,
            @checked: snap.Value,
            onCheckedChange: snap.Setter
        );
    });

    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Box(width: 360, height: 200, padding: 16, align: Align.Start))
        using (ui.Menubar())
        {
            ui.MenuItem(text: "File", items: File);
            ui.MenuItem(text: "View", items: View);
            ui.MenuItem(text: "Help");
        }
    }
}

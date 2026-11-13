using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<ContextMenu>("Overlays")]
static class ContextMenuStories
{
    static readonly Slot Edits = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Copy");
        ui.MenuItem(text: "Paste");
        ui.MenuSeparator();
        ui.MenuItem(text: "Delete", destructive: true);
    });

    static readonly Slot Layout = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Arrange", items: Arrange);
        ui.MenuItem(text: "Align", items: AlignRows);
        ui.MenuSeparator();
        ui.MenuItem(text: "Put away", destructive: true);
    });

    static readonly Slot Arrange = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Bring to front");
        ui.MenuItem(text: "Send to back");
    });

    static readonly Slot AlignRows = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Left");
        ui.MenuItem(text: "Centre");
        ui.MenuItem(text: "Right");
    });

    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 8, padding: 16))
        {
            ui.Text(
                "Right-click the area, or hold it: its view model opens the anchor's menu at the pointer",
                color: ui.Theme.Color(Theming.ColorToken.FgMuted),
                fontWeight: ui.Theme.Weight(Theming.WeightToken.Interface),
                wrap: true
            );
            using (ui.ContextMenu(menu: Edits))
                Area(ui);
        }
    }

    [FramePath]
    public static void Nested(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 8, padding: 16))
        {
            ui.Text(
                "Right-click the area, then hover a row with an arrow to open its submenu",
                color: ui.Theme.Color(Theming.ColorToken.FgMuted),
                fontWeight: ui.Theme.Weight(Theming.WeightToken.Interface),
                wrap: true
            );
            using (ui.ContextMenu(menu: Layout))
                Area(ui);
        }
    }

    [FramePath]
    static void Area(
        Ui ui,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        using (
            ui.Box(
                width: 320,
                height: 160,
                background: t.Color(Theming.ColorToken.BgSunken),
                border: new Border(1f, t.Color(Theming.ColorToken.Border)),
                radius: t.Radius(Theming.RadiusToken.Md),
                file: file,
                line: line
            )
        ) { }
    }
}

using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Menu>("Overlays")]
static class MenuStories
{
    static readonly Slot NestedRows = Slot.Of(static ui =>
    {
        var grid = ui.State(true);
        var snap = ui.State(false);
        ui.MenuItem(text: "New tab", icon: Icons.Plus, shortcut: "Ctrl+T");
        ui.MenuItem(text: "Open recent", items: RecentRows);
        ui.MenuSeparator();
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
        ui.MenuItem(text: "Export", shortcut: "Ctrl+E", disabled: true);
        ui.MenuSeparator();
        ui.MenuItem(text: "Delete map", shortcut: "Del", destructive: true);
    });

    static readonly Slot RecentRows = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Edinburgh.map");
        ui.MenuItem(text: "Cairo.map");
        ui.MenuItem(text: "Archive", items: ArchiveRows);
    });

    static readonly Slot ArchiveRows = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "2025");
        ui.MenuItem(text: "2024");
        ui.MenuItem(text: "2023", disabled: true);
    });

    static readonly string[] RoomNames = Rooms();

    static readonly Slot RoomRows = Slot.Of(static ui =>
    {
        for (var i = 0; i < RoomNames.Length; i++)
            ui.MenuItem(text: RoomNames[i], key: i);
    });

    static readonly Slot EditRows = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Rename");
        ui.MenuItem(text: "Duplicate");
        ui.MenuItem(text: "Delete", destructive: true);
    });

    static readonly Slot ContactRows = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Message");
        ui.MenuItem(text: "Call");
        ui.MenuSeparator();
        ui.MenuItem(text: "Block", destructive: true);
    });

    static readonly Slot Person = Slot.Of(static ui =>
        ui.Text(
            "Ada Moreau · Admin",
            variant: TextVariant.Caption,
            color: ui.Theme.Color(ColorToken.FgMuted),
            fontWeight: ui.Theme.Weight(WeightToken.Interface),
            margin: new Thickness(8f, 6f, 8f, 6f)
        )
    );

    static readonly Slot ActionRows = Slot.Of(static ui =>
    {
        var grid = ui.State(true);
        ui.MenuItem(
            text: "Show grid",
            checkable: true,
            @checked: grid.Value,
            onCheckedChange: grid.Setter
        );
        ui.MenuItem(text: "Export", shortcut: "Ctrl+E");
        ui.MenuItem(text: "Open recent", items: OneRecent);
        ui.MenuSeparator();
        ui.MenuItem(text: "Delete map", destructive: true);
    });

    static readonly Slot OneRecent = Slot.Of(static ui => ui.MenuItem(text: "Edinburgh.map"));

    static readonly Slot BoundsRows = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Opens below the anchor");
        ui.MenuItem(text: "Right edges lined up");
    });

    [FramePath]
    public static void Nested(Ui ui)
    {
        using (ui.Box(padding: 16, height: 240, align: Align.Start))
            ui.MenuButton(
                iconLeft: Icons.MoreHorizontal,
                scheme: ColorScheme.Secondary,
                size: ComponentSize.Sm,
                menu: NestedRows
            );
    }

    [FramePath]
    public static void Scrolling(Ui ui)
    {
        using (ui.Box(padding: 16, height: 240, align: Align.Start))
            ui.MenuButton(
                text: "Rooms",
                chevron: true,
                scheme: ColorScheme.Secondary,
                variant: ComponentVariant.Outlined,
                size: ComponentSize.Sm,
                menu: RoomRows
            );
    }

    [FramePath]
    public static void Sizes(Ui ui)
    {
        using (ui.Box(gap: 8, padding: 16, height: 200, align: Align.Start))
        {
            ui.MenuButton(
                text: "Small",
                scheme: ColorScheme.Secondary,
                variant: ComponentVariant.Outlined,
                size: ComponentSize.Sm,
                menu: EditRows,
                menuSize: ComponentSize.Sm
            );
            ui.MenuButton(
                text: "Medium",
                scheme: ColorScheme.Secondary,
                variant: ComponentVariant.Outlined,
                size: ComponentSize.Sm,
                menu: EditRows
            );
        }
    }

    [FramePath]
    public static void Header(Ui ui)
    {
        using (ui.Box(padding: 16, height: 200, align: Align.Start))
            ui.MenuButton(
                text: "Contact",
                scheme: ColorScheme.Secondary,
                variant: ComponentVariant.Outlined,
                size: ComponentSize.Sm,
                menu: ContactRows,
                menuHeader: Person
            );
    }

    [FramePath]
    public static void Open(Ui ui)
    {
        using (ui.Box(width: 320, height: 240, padding: 16, align: Align.Start))
            ui.MenuButton(
                text: "Actions",
                chevron: true,
                scheme: ColorScheme.Secondary,
                variant: ComponentVariant.Outlined,
                size: ComponentSize.Sm,
                defaultOpen: true,
                menu: ActionRows
            );
    }

    [FramePath]
    public static void Anchored(Ui ui)
    {
        using (ui.Box(width: 320, height: 220, padding: 16, align: Align.Start))
        using (ui.Box(width: 120, height: 40, background: ui.Theme.Color(ColorToken.BgSunken)))
            ui.Menu(open: true, placement: PlacementMode.BottomEnd, content: BoundsRows);
    }

    static string[] Rooms()
    {
        var rooms = new string[30];
        for (var i = 0; i < rooms.Length; i++)
            rooms[i] = "Room " + (i + 1);
        return rooms;
    }
}

using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

static class StoryMenus
{
    public static readonly Slot Visibility = Slot.Of(static ui =>
    {
        var picked = ui.State(0);
        ui.MenuItem(
            text: "Public",
            checkable: true,
            @checked: picked.Value == 0,
            onCheckedChange: Callback<bool>.Of(picked, static (p, _) => p.Value = 0)
        );
        ui.MenuItem(
            text: "Team",
            checkable: true,
            @checked: picked.Value == 1,
            onCheckedChange: Callback<bool>.Of(picked, static (p, _) => p.Value = 1)
        );
        ui.MenuItem(
            text: "Private",
            checkable: true,
            @checked: picked.Value == 2,
            onCheckedChange: Callback<bool>.Of(picked, static (p, _) => p.Value = 2)
        );
    });

    public static readonly Slot Account = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Profile");
        ui.MenuItem(text: "Settings");
        ui.MenuItem(text: "Billing");
        ui.MenuItem(text: "Support");
    });

    public static readonly Slot Sell = Slot.Of(static ui =>
    {
        ui.MenuItem(text: "Sell all junk", shortcut: "Ctrl+J");
        ui.MenuItem(text: "Sell duplicates", shortcut: "Ctrl+D");
        ui.MenuItem(text: "Sell and repair", shortcut: "Ctrl+R");
    });
}

[Stories<MenuButton>("Buttons")]
static class MenuButtonStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Box(padding: 16, height: 200, align: Align.Start))
            ui.MenuButton(
                text: "Actions",
                chevron: true,
                scheme: ColorScheme.Secondary,
                variant: ComponentVariant.Outlined,
                size: ComponentSize.Sm,
                menu: StoryMenus.Account
            );
    }

    [FramePath]
    public static void Above(Ui ui)
    {
        using (ui.Box(width: 320, height: 200, padding: 16, align: Align.End))
            ui.MenuButton(
                text: "Mode",
                chevron: true,
                scheme: ColorScheme.Secondary,
                variant: ComponentVariant.Ghost,
                defaultOpen: true,
                placement: PlacementMode.TopStart,
                menu: StoryMenus.Visibility,
                menuSize: ComponentSize.Sm
            );
    }
}

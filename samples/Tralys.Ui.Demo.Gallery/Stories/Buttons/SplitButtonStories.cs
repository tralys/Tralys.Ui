using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<SplitButton>("Buttons")]
static class SplitButtonStories
{
    static readonly ColorScheme[] Schemes =
    [
        ColorScheme.Primary,
        ColorScheme.Secondary,
        ColorScheme.Danger,
    ];

    static readonly ComponentVariant[] Looks =
    [
        ComponentVariant.Filled,
        ComponentVariant.Outlined,
        ComponentVariant.Subtle,
    ];

    static readonly string[] Names = Enum.GetNames<ColorScheme>();

    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Box(padding: 16, height: 200, align: Align.Start))
            ui.SplitButton(
                text: "Sell selected",
                menu: StoryMenus.Sell,
                menuSize: ComponentSize.Sm
            );
    }

    [FramePath]
    public static void Variants(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 16, padding: 16))
            foreach (var variant in Looks)
                using (ui.Box(gap: 16, key: (long)variant))
                    foreach (var scheme in Schemes)
                        ui.SplitButton(
                            text: Names[(int)scheme],
                            scheme: scheme,
                            variant: variant,
                            menu: StoryMenus.Sell,
                            key: (long)scheme
                        );
    }

    [FramePath]
    public static void Open(Ui ui)
    {
        using (ui.Box(width: 320, height: 200, padding: 16, align: Align.Start))
            ui.SplitButton(
                text: "Sell selected",
                defaultOpen: true,
                menu: StoryMenus.Sell,
                menuSize: ComponentSize.Sm
            );
    }
}

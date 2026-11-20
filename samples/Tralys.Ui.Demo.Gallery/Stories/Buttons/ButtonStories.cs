using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Button>("Buttons")]
static class ButtonStories
{
    static readonly ComponentVariant[] Looks =
    [
        ComponentVariant.Filled,
        ComponentVariant.Outlined,
        ComponentVariant.Text,
        ComponentVariant.Subtle,
        ComponentVariant.Ghost,
        ComponentVariant.GhostOutlined,
    ];

    static readonly ColorScheme[] Schemes = Enum.GetValues<ColorScheme>();

    static readonly string[] SchemeNames = Enum.GetNames<ColorScheme>();

    static readonly string[] SizeNames = ["Xxs", "Xs", "Sm", "Default", "Lg"];

    static readonly ComponentSize[] AllSizes = Enum.GetValues<ComponentSize>();

    /// <summary>Each look in a row, Filled, Outlined, Text, Subtle, Ghost and GhostOutlined, across every colour
    /// scheme.</summary>
    [FramePath]
    public static void Variants(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 16, padding: 16))
            foreach (var variant in Looks)
                using (ui.Box(gap: 16, key: (long)variant))
                    foreach (var scheme in Schemes)
                        ui.Button(
                            text: SchemeNames[(int)scheme],
                            scheme: scheme,
                            variant: variant,
                            key: (long)scheme
                        );
    }

    /// <summary>Disabled buttons in the Primary, Success and Danger schemes.</summary>
    [FramePath]
    public static void Disabled(Ui ui)
    {
        using (ui.Box(align: Align.Start, gap: 16, padding: 16))
        {
            ui.Button(text: "Primary", disabled: true);
            ui.Button(text: "Success", scheme: ColorScheme.Success, disabled: true);
            ui.Button(text: "Danger", scheme: ColorScheme.Danger, disabled: true);
        }
    }

    /// <summary>Ghost buttons marking the active page; pressing the other makes it the active one.</summary>
    [FramePath]
    public static void Active(Ui ui)
    {
        var page = ui.State(0);
        using (ui.Box(align: Align.Start, gap: 16, padding: 16))
        {
            ui.Button(
                text: "Active",
                scheme: ColorScheme.Secondary,
                variant: ComponentVariant.Ghost,
                active: page.Value == 0,
                onClick: Callback.Of(page, static p => p.Value = 0)
            );
            ui.Button(
                text: "Inactive",
                scheme: ColorScheme.Secondary,
                variant: ComponentVariant.Ghost,
                active: page.Value == 1,
                onClick: Callback.Of(page, static p => p.Value = 1)
            );
        }
    }

    /// <summary>Every size from Xxs to Lg: text alone, with an icon before it, and with an icon after it.</summary>
    [FramePath]
    public static void Sizes(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 16, padding: 16))
        {
            using (ui.Box(align: Align.Start, gap: 16))
                foreach (var size in AllSizes)
                    ui.Button(text: SizeNames[(int)size], size: size, key: (long)size);
            using (ui.Box(align: Align.Start, gap: 16))
                foreach (var size in AllSizes)
                    ui.Button(
                        text: SizeNames[(int)size],
                        size: size,
                        iconLeft: Icons.Star,
                        key: (long)size
                    );
            using (ui.Box(align: Align.Start, gap: 16))
                foreach (var size in AllSizes)
                    ui.Button(
                        text: SizeNames[(int)size],
                        size: size,
                        iconRight: Icons.ArrowRight,
                        key: (long)size
                    );
        }
    }

    /// <summary>An outlined button flashing to draw the eye; pressing it stops and starts the flash.</summary>
    [FramePath]
    public static void Flashing(Ui ui)
    {
        var flashing = ui.State(true);
        using (ui.Box(padding: 16))
            ui.Button(
                text: "Review changes",
                scheme: ColorScheme.Secondary,
                variant: ComponentVariant.Outlined,
                flashing: flashing.Value,
                onClick: flashing.Toggle()
            );
    }

    /// <summary>Link buttons: plain, with a trailing icon, in the Primary scheme, and disabled.</summary>
    [FramePath]
    public static void Link(Ui ui)
    {
        using (ui.Box(align: Align.Center, gap: 16, padding: 16))
        {
            ui.Button(text: "Text Button", variant: ComponentVariant.Link);
            ui.Button(
                text: "Read more",
                variant: ComponentVariant.Link,
                iconRight: Icons.ArrowUpRight
            );
            ui.Button(
                text: "Status page",
                variant: ComponentVariant.Link,
                scheme: ColorScheme.Primary
            );
            ui.Button(text: "Disabled", variant: ComponentVariant.Link, disabled: true);
        }
    }

    /// <summary>Link buttons with a leading icon at the small, medium and large sizes.</summary>
    [FramePath]
    public static void LinkSizes(Ui ui)
    {
        using (ui.Box(gap: 16, padding: 16))
        {
            ui.Button(
                text: "Small",
                variant: ComponentVariant.Link,
                iconLeft: Icons.Link,
                size: ComponentSize.Sm
            );
            ui.Button(text: "Medium", variant: ComponentVariant.Link, iconLeft: Icons.Link);
            ui.Button(
                text: "Large",
                variant: ComponentVariant.Link,
                iconLeft: Icons.Link,
                size: ComponentSize.Lg
            );
        }
    }
}

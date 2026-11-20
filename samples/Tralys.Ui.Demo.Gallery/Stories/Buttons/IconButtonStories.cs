using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<IconButton>("Buttons")]
static class IconButtonStories
{
    static readonly ColorScheme[] Schemes =
    [
        ColorScheme.Primary,
        ColorScheme.Secondary,
        ColorScheme.Success,
        ColorScheme.Danger,
    ];

    static readonly ComponentVariant[] Looks =
    [
        ComponentVariant.Filled,
        ComponentVariant.Outlined,
        ComponentVariant.Ghost,
        ComponentVariant.GhostOutlined,
    ];

    static readonly ComponentSize[] AllSizes = Enum.GetValues<ComponentSize>();

    /// <summary>An icon button in the Primary, Secondary, Success and Danger schemes, then in the Filled, Outlined,
    /// Ghost and GhostOutlined looks.</summary>
    [FramePath]
    public static void Variants(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 16, padding: 16))
        {
            using (ui.Box(gap: 16))
                foreach (var scheme in Schemes)
                    ui.IconButton(icon: Icons.Star, scheme: scheme, key: (long)scheme);
            using (ui.Box(gap: 16))
                foreach (var variant in Looks)
                    ui.IconButton(icon: Icons.Star, variant: variant, key: (long)variant);
        }
    }

    /// <summary>An icon button at every size.</summary>
    [FramePath]
    public static void Sizes(Ui ui)
    {
        using (ui.Box(align: Align.Center, gap: 16, padding: 16))
            foreach (var size in AllSizes)
                ui.IconButton(icon: Icons.Star, size: size, key: (long)size);
    }
}

using Tralys.Ui.Components;
using Tralys.Ui.Fonts;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Icon>("Displays")]
static class IconStories
{
    static readonly float[] Sizes = [12, 16, 20, 24, 32];

    static readonly IconGlyph[] Common =
    [
        Icons.Bell,
        Icons.Check,
        Icons.Trash,
        Icons.Settings,
        Icons.Search,
        Icons.User,
        Icons.Heart,
        Icons.Home,
    ];

    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 32))
        {
            using (ui.Box(gap: 32))
                for (var i = 0; i < Sizes.Length; i++)
                    ui.Icon(glyph: Icons.Star, size: Sizes[i], key: i);
            using (ui.Box(gap: 32))
                for (var i = 0; i < Common.Length; i++)
                    ui.Icon(glyph: Common[i], size: 20, key: i);
        }
    }

    [FramePath]
    public static void AllIcons(Ui ui)
    {
        var t = ui.Theme;
        var all = Icons.All;
        using (
            ui.Box(
                wrap: FlexWrap.Wrap,
                overflow: Overflow.ScrollY,
                grow: 1,
                width: Length.Full,
                maxHeight: 400,
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
            for (var i = 0; i < all.Count; i++)
                using (
                    ui.Box(
                        direction: Direction.Column,
                        align: Align.Center,
                        justify: Justify.Center,
                        gap: 8,
                        width: 70,
                        height: 64,
                        margin: 2,
                        background: t.Color(ColorToken.BgSunken),
                        border: new Border(1f, t.Color(ColorToken.Border)),
                        key: i
                    )
                )
                {
                    ui.Icon(glyph: all[i].Value, size: 20);
                    ui.Text(
                        all[i].Key,
                        color: t.Color(ColorToken.FgMuted),
                        variant: TextVariant.Badge,
                        wrap: true,
                        trimming: TextTrimming.CharacterEllipsis
                    );
                }
    }

    [FramePath]
    public static void Info(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 32,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            using (ui.Box(gap: 16))
            {
                ui.Text("Hover icons for tooltip");
                ui.Icon(variant: IconVariant.Info);
            }
            using (ui.Box(gap: 16))
            {
                ui.Text("Another field");
                ui.Icon(variant: IconVariant.Info);
            }
        }
    }
}

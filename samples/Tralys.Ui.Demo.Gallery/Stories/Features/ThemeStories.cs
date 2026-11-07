using System.Runtime.CompilerServices;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.Theme>("Features")]
static class ThemeStories
{
    static readonly (string Name, ColorToken Fill, ColorToken Text)[] Fills =
    [
        ("Bg", ColorToken.Bg, ColorToken.Fg),
        ("BgSunken", ColorToken.BgSunken, ColorToken.Fg),
        ("BgRaised", ColorToken.BgRaised, ColorToken.Fg),
        ("BgOverlay", ColorToken.BgOverlay, ColorToken.Fg),
        ("BgSubtle", ColorToken.BgSubtle, ColorToken.Fg),
        ("BgSelected", ColorToken.BgSelected, ColorToken.FgOnAccent),
        ("BgAccent", ColorToken.BgAccent, ColorToken.FgOnAccent),
        ("BgAccentMuted", ColorToken.BgAccentMuted, ColorToken.Fg),
        ("BgDanger", ColorToken.BgDanger, ColorToken.FgFilledDanger),
        ("BgDangerMuted", ColorToken.BgDangerMuted, ColorToken.Fg),
        ("BgSuccess", ColorToken.BgSuccess, ColorToken.FgFilledSuccess),
        ("BgWarning", ColorToken.BgWarning, ColorToken.FgFilledWarning),
        ("Fg", ColorToken.BgSunken, ColorToken.Fg),
        ("FgStrong", ColorToken.BgSunken, ColorToken.FgStrong),
        ("FgMuted", ColorToken.BgSunken, ColorToken.FgMuted),
        ("FgAccent", ColorToken.BgSunken, ColorToken.FgAccent),
        ("FgDanger", ColorToken.BgSunken, ColorToken.FgDanger),
        ("FgSuccess", ColorToken.BgSunken, ColorToken.FgSuccess),
    ];

    static readonly (string Name, ColorToken Border)[] Borders =
    [
        ("Border", ColorToken.Border),
        ("BorderHover", ColorToken.BorderHover),
        ("BorderFocus", ColorToken.BorderFocus),
        ("BorderAccent", ColorToken.BorderAccent),
        ("BorderDanger", ColorToken.BorderDanger),
        ("BorderSuccess", ColorToken.BorderSuccess),
    ];

    static readonly (string Name, ComponentSize Size, RadiusToken Radius)[] Sizes =
    [
        ("Xxs", ComponentSize.Xxs, RadiusToken.Sm),
        ("Xs", ComponentSize.Xs, RadiusToken.Sm),
        ("Sm", ComponentSize.Sm, RadiusToken.Md),
        ("Md", ComponentSize.Md, RadiusToken.Md),
        ("Lg", ComponentSize.Lg, RadiusToken.Lg),
    ];

    static readonly (string Name, TextVariant Variant)[] Ramp =
    [
        ("Display", TextVariant.Display),
        ("Headline", TextVariant.Headline),
        ("Title", TextVariant.Title),
        ("Heading", TextVariant.Heading),
    ];

    static readonly (string Name, RadiusToken Radius)[] Radii =
    [
        ("Radius Sm", RadiusToken.Sm),
        ("Radius Md", RadiusToken.Md),
        ("Radius Lg", RadiusToken.Lg),
    ];

    [FramePath]
    public static void Colors(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 6, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "Colour tokens drawn from the theme; the theme menu above switches it and every swatch repaints"
            );
            using (ui.Grid(columns: GridTracks.Repeat(6, Track.Fr(1)), width: 880))
            {
                for (var i = 0; i < Fills.Length; i++)
                {
                    var (name, fill, text) = Fills[i];
                    Swatch(ui, name, t.Color(fill), default, 1, t.Color(text), key: i);
                }
                for (var i = 0; i < Borders.Length; i++)
                {
                    var (name, border) = Borders[i];
                    Swatch(ui, name, default, t.Color(border), 3, default, key: Fills.Length + i);
                }
            }
        }
    }

    [FramePath]
    static void Swatch(
        Ui ui,
        string name,
        Paint fill,
        Color border,
        float width,
        Paint text,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        using (
            ui.Box(
                direction: Direction.Column,
                justify: Justify.End,
                height: 44,
                margin: 2,
                padding: new Thickness(6, 0, 6, 0),
                radius: t.Radius(RadiusToken.Sm),
                background: fill,
                border: new(width, border == default ? t.Color(ColorToken.BorderMuted) : border),
                key: key,
                file: file,
                line: line
            )
        )
            ui.Text(
                name,
                variant: TextVariant.Caption,
                color: text,
                margin: new Thickness(0, 0, 0, 4)
            );
    }

    [FramePath]
    public static void Density(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 6, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "The size ramp under the context's density: the density menu above switches Pointer and Touch, and every height, padding and gap here follows"
            );
            using (ui.Box(gap: 8, align: Align.Start))
            {
                for (var i = 0; i < Sizes.Length; i++)
                {
                    var (name, size, radius) = Sizes[i];
                    var m = t.Metrics(size);
                    using (
                        ui.Box(
                            align: Align.Center,
                            height: m.Height,
                            padding: new Thickness(m.PadX, m.PadY, m.PadX, m.PadY),
                            radius: t.Radius(radius),
                            background: t.Color(ColorToken.BgRaised),
                            key: i
                        )
                    )
                        ui.Text($"{name} {m.Height}", fontSize: m.FontSize);
                }
            }
            ui.Caption(
                "A box's gap stays in layout units, which density leaves alone",
                margin: new Thickness(0, 8, 0, 0)
            );
            using (ui.Box(gap: 8))
            {
                for (var i = 0; i < 4; i++)
                {
                    using (
                        ui.Box(
                            width: 48,
                            height: 24,
                            background: t.Color(ColorToken.BgAccentMuted),
                            key: i
                        )
                    ) { }
                }
            }
        }
    }

    [FramePath]
    public static void Type(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 4, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "The text ramp, the body and code fonts, and the radius ramp, all read from the theme"
            );
            for (var i = 0; i < Ramp.Length; i++)
                ui.Text(Ramp[i].Name, variant: Ramp[i].Variant, key: i);
            ui.Text("Body: the quick brown fox jumps over the lazy dog");
            ui.Text("Caption", variant: TextVariant.Caption);
            ui.Text("Badge", variant: TextVariant.Badge);
            ui.Text(
                "Code font: var max = 42;",
                fontFamily: t.FontFamily(FontToken.Code),
                fontSize: t.FontSize(TextVariant.Body)
            );
            using (ui.Box(gap: 12, margin: new Thickness(0, 8, 0, 0)))
            {
                for (var i = 0; i < Radii.Length; i++)
                {
                    using (
                        ui.Box(
                            align: Align.Center,
                            justify: Justify.Center,
                            width: 80,
                            height: 48,
                            radius: t.Radius(Radii[i].Radius),
                            background: t.Color(ColorToken.BgRaised),
                            border: new(1, t.Color(ColorToken.Border)),
                            key: i
                        )
                    )
                        ui.Text(Radii[i].Name, variant: TextVariant.Caption);
                }
            }
        }
    }
}

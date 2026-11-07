using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Fonts;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.Text>("Text")]
static class TextStories
{
    const string Ferry =
        "The ferry leaves at dawn. Bring water, a warm jacket, and the map from the front desk.";

    const string Guide = "Guide to the Northern Wind Farms, v12";

    static readonly Color Gold = Color.Parse("#F0C83C");
    static readonly Color Green = Color.Parse("#FF4ADE80");
    static readonly Color Blue = Color.Parse("#FF60A5FA");
    static readonly Color Coin = Color.Parse("#FFF0C83C");
    static readonly Color Ink = Color.Parse("#FF000000");
    static readonly Color Paper = Color.Parse("#FFFFFFFF");
    static readonly Color Ember = Color.Parse("#FFB91C1C");

    static readonly Paint Busy = Paint.Linear(
        new GradientStops([
            new(0f, Color.Parse("#FFF8FAFC")),
            new(0.5f, Color.Parse("#FF38BDF8")),
            new(1f, Color.Parse("#FF0F172A")),
        ]),
        new Vector2(0f, 0f),
        new Vector2(1f, 1f)
    );

    static readonly float[] FontSizes = [12, 14, 18, 24, 32];
    static readonly string[] FontSizeNames = ["12", "14", "18", "24", "32"];

    [FramePath]
    public static void Default(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 16,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Text("Display — hero numbers", variant: TextVariant.Display);
            ui.Text("Headline — detail titles", variant: TextVariant.Headline);
            ui.Text("Title — window and dialog titles", variant: TextVariant.Title);
            ui.Text("Heading — section headings", variant: TextVariant.Heading);
            ui.Text("Body — the default");
            ui.Text("Caption — secondary text and hints", variant: TextVariant.Caption);
            ui.Text("Badge — counts drawn over art", variant: TextVariant.Badge);
        }
    }

    [FramePath]
    public static void Variants(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 16,
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Text("Fg — default foreground", color: t.Color(ColorToken.Fg));
            ui.Text("FgMuted — secondary text", color: t.Color(ColorToken.FgMuted));
            ui.Text("FgStrong — label text", color: t.Color(ColorToken.FgStrong));
            ui.Text("FgAccent — accent text", color: t.Color(ColorToken.FgAccent));
            ui.Text("FgSuccess — success text", color: t.Color(ColorToken.FgSuccess));
            ui.Text("FgDanger — danger text", color: t.Color(ColorToken.FgDanger));
            ui.Text("FgWarning — warning text", color: t.Color(ColorToken.FgWarning));
        }
    }

    [FramePath]
    public static void Formats(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Text($"{1845:N0}");
            ui.Text($"{-1200:N0}");
            ui.Text($"{7}%");
            ui.Text($"{66.66:F1}%");
            ui.Text($"{15:+0;-0}%");
            ui.Text($"{12500 / 1000f:0.#}K");
            ui.Text($"{7500 / 3600}h {7500 % 3600 / 60:00}m");
            ui.Text($"{725 / 60}m");
            ui.Text($"{1200:N0} / {40000:N0}");
            ui.Text("No date set", color: t.Color(ColorToken.FgPlaceholder));
            ui.Text(
                "Clamped to two lines, the last ending in an ellipsis however much more text follows it",
                wrap: true,
                maxLines: 2,
                trimming: TextTrimming.CharacterEllipsis,
                width: 220
            );
        }
    }

    [FramePath]
    public static void Runs(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption("Spans set their own colour, weight and size; a line break starts a line");
            using (Sample(ui, 480))
            {
                using (ui.Text(fontSize: 15, wrap: true))
                {
                    ui.Span("Upgrade:\n");
                    ui.Span("Storage", color: Gold, fontWeight: FontWeight.Bold);
                    ui.Span(" increased by ");
                    ui.Span("50", fontSize: 20, color: Green);
                    ui.Span(" gigabytes, and ");
                    ui.Span("Backups", fontWeight: FontWeight.Medium, color: Blue);
                    ui.Span(" kept for the rest of the ");
                    ui.Span("year.", fontStyle: FontStyle.Italic);
                }
            }
        }
    }

    [FramePath]
    static Scope Sample(
        Ui ui,
        Length width,
        Overflow overflow = default,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        return ui.Box(
            direction: Direction.Column,
            width: width,
            padding: 8,
            overflow: overflow,
            background: t.Color(ColorToken.BgSunken),
            border: new(1, t.Color(ColorToken.Border)),
            key: key,
            file: file,
            line: line
        );
    }

    [FramePath]
    public static void Wrapping(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "Wrapped at 260, aligned left, centre and right; the last with line height 26"
            );
            using (ui.Box(gap: 8))
            {
                using (Sample(ui, 260))
                    ui.Text(Ferry, wrap: true);
                using (Sample(ui, 260))
                    ui.Text(Ferry, wrap: true, textAlign: TextAlign.Center);
                using (Sample(ui, 260))
                    ui.Text(Ferry, wrap: true, textAlign: TextAlign.Right, lineHeight: 26);
            }
        }
    }

    [FramePath]
    public static void Trimming(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption("One line in 220: cut off, CharacterEllipsis, WordEllipsis");
            using (Sample(ui, 220, Overflow.Clip))
                ui.Text(Guide, fontSize: 15);
            using (Sample(ui, 220))
                ui.Text(Guide, fontSize: 15, trimming: TextTrimming.CharacterEllipsis);
            using (Sample(ui, 220))
                ui.Text(Guide, fontSize: 15, trimming: TextTrimming.WordEllipsis);
        }
    }

    [FramePath]
    public static void Stroke(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption("Outlined text over a busy background: no stroke, then 1, 2 and 3 units");
            using (ui.Box(gap: 24, width: 560, padding: 12, background: Busy))
            {
                ui.Text("9,999", fontSize: 32, fontWeight: FontWeight.Bold, color: Coin);
                ui.Text(
                    "9,999",
                    fontSize: 32,
                    fontWeight: FontWeight.Bold,
                    color: Coin,
                    stroke: Ink,
                    strokeWidth: 1
                );
                ui.Text(
                    "9,999",
                    fontSize: 32,
                    fontWeight: FontWeight.Bold,
                    color: Coin,
                    stroke: Ink,
                    strokeWidth: 2
                );
                ui.Text(
                    "9,999",
                    fontSize: 32,
                    fontWeight: FontWeight.Bold,
                    color: Paper,
                    stroke: Ember,
                    strokeWidth: 3
                );
            }
        }
    }

    [FramePath]
    public static void Fonts(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 6, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            using (ui.Box(gap: 16, align: Align.End))
            {
                for (var i = 0; i < FontSizes.Length; i++)
                    ui.Text(FontSizeNames[i], fontSize: FontSizes[i], key: i);
            }
            using (ui.Box(gap: 16))
            {
                ui.Text("Regular", fontSize: 18, fontWeight: FontWeight.Normal);
                ui.Text("Medium", fontSize: 18, fontWeight: FontWeight.Medium);
                ui.Text("SemiBold", fontSize: 18, fontWeight: FontWeight.SemiBold);
                ui.Text("Bold", fontSize: 18, fontWeight: FontWeight.Bold);
            }
            using (ui.Box(gap: 16))
            {
                ui.Text(
                    "Body font",
                    fontSize: 18,
                    fontWeight: FontWeight.Normal,
                    fontFamily: t.FontFamily(FontToken.Body)
                );
                ui.Text(
                    "Code font 0O1lI",
                    fontSize: 18,
                    fontWeight: FontWeight.Normal,
                    fontFamily: t.FontFamily(FontToken.Code)
                );
                ui.Text(
                    "Inter, the fallback",
                    fontSize: 18,
                    fontWeight: FontWeight.Normal,
                    fontFamily: "Inter"
                );
            }
            using (ui.Box(padding: new Thickness(8, 4, 8, 4)))
                ui.Text(
                    "Foreground, padding and the theme's strong colour",
                    color: t.Color(ColorToken.FgStrong)
                );
        }
    }
}

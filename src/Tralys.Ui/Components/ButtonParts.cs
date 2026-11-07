using System.Runtime.CompilerServices;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

[FramePath]
internal static class ButtonParts
{
    public static ComponentPalette Animated(
        this Ui ui,
        ComponentPalette look,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Fragment(key: key, file: file, line: line))
            return new ComponentPalette(
                ui.Animate(look.Background),
                ui.Animate(look.Foreground),
                ui.Animate(look.Border)
            );
    }

    public static void Label(
        this Ui ui,
        TextValue text,
        Slot content,
        IconGlyph iconLeft,
        IconGlyph iconRight,
        float iconSize,
        float gap,
        Paint color,
        Justify justify = default,
        float grow = 0f,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                align: Align.Center,
                justify: justify,
                gap: gap,
                grow: grow,
                key: key,
                file: file,
                line: line
            )
        )
            ui.LabelGlyphs(
                text: text,
                content: content,
                iconLeft: iconLeft,
                iconRight: iconRight,
                iconSize: iconSize,
                color: color
            );
    }

    public static void LabelGlyphs(
        this Ui ui,
        TextValue text,
        Slot content,
        IconGlyph iconLeft,
        IconGlyph iconRight,
        float iconSize,
        Paint color,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Fragment(key: key, file: file, line: line))
        {
            if (!iconLeft.IsEmpty)
                ui.Icon(glyph: iconLeft, size: iconSize, color: color, shrink: 0f);
            if (!text.IsNull)
                ui.Text(text, color: color);
            else
                content.Render(ui);
            if (!iconRight.IsEmpty)
                ui.Icon(glyph: iconRight, size: iconSize, color: color, shrink: 0f);
        }
    }

    public static void Chevron(
        this Ui ui,
        Paint color,
        float size,
        bool open,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Icon(
            glyph: IconGlyph.Of(IconRole.ChevronDown),
            size: size,
            color: color,
            flipped: open,
            shrink: 0f,
            key: key,
            file: file,
            line: line
        );

    // The theme's padding runs from the outer edge, so a border inside the box takes its share of it.
    public static Thickness Inside(Thickness padding, Thickness border) =>
        new(
            MathF.Max(0f, padding.Left - border.Left),
            MathF.Max(0f, padding.Top - border.Top),
            MathF.Max(0f, padding.Right - border.Right),
            MathF.Max(0f, padding.Bottom - border.Bottom)
        );

    public static Color Mix(Color from, Color to, float amount) =>
        Color.FromArgb(
            Step(from.A, to.A, amount),
            Step(from.R, to.R, amount),
            Step(from.G, to.G, amount),
            Step(from.B, to.B, amount)
        );

    static byte Step(byte from, byte to, float amount) =>
        (byte)MathF.Round(from + (to - from) * Math.Clamp(amount, 0f, 1f));
}

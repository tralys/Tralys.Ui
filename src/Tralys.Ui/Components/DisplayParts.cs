using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

[FramePath]
internal static class DisplayParts
{
    public static void Pill(
        this Ui ui,
        TextValue text,
        IconGlyph icon,
        ComponentPalette look,
        float height,
        float minWidth,
        float padX,
        CornerRadius radius,
        float border,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                gap: 4f,
                height: height,
                minWidth: minWidth,
                padding: new Thickness(padX, 0f, padX, 0f),
                background: look.Background,
                border: border > 0f ? new Border(border, look.Border) : default,
                radius: radius,
                pointerEvents: PointerEvents.None,
                key: key,
                file: file,
                line: line
            )
        )
        {
            if (!icon.IsEmpty)
                ui.Icon(glyph: icon, size: 10f, color: look.Foreground);
            ui.Text(
                text,
                color: look.Foreground,
                fontWeight: ui.Theme.Weight(WeightToken.Interface),
                variant: TextVariant.Caption
            );
        }
    }

    public static ComponentState LabelState(this Ui ui) =>
        ui.IsDisabled ? ComponentState.Disabled : ComponentState.Normal;
}

/// <summary>The gradients the displays fill with, made once per theme.</summary>
internal sealed class DisplayFills
{
    static readonly ConditionalWeakTable<Theme, DisplayFills> ByTheme = [];

    DisplayFills(Theme theme)
    {
        var top = theme.Color(ComponentColor.CardBgTop);
        var bottom = theme.Color(ComponentColor.CardBgBottom);
        var hover = theme.Color(ColorToken.StateHover);
        CardRest = Vertical(top, bottom);
        CardLit = Vertical(hover.Over(top), hover.Over(bottom));
        var bg = theme.Color(ColorToken.Bg);
        EmptyBackdrop = Paint.Radial(
            new GradientStops([
                new ColorStop(0f, bg.WithOpacity(0.95f)),
                new ColorStop(0.65f, bg.WithOpacity(0.85f)),
                new ColorStop(1f, bg.WithOpacity(0f)),
            ]),
            new Vector2(0.5f),
            new Vector2(0.5f)
        );
    }

    /// <summary>The card gradient, top to bottom.</summary>
    public Paint CardRest { get; }

    /// <summary>The card gradient under the neutral hover wash.</summary>
    public Paint CardLit { get; }

    /// <summary>The theme's background fading out from the middle.</summary>
    public Paint EmptyBackdrop { get; }

    [FramePath]
    public static DisplayFills Of(Theme theme) =>
        ByTheme.GetValue(theme, static theme => new DisplayFills(theme));

    static Paint Vertical(Color top, Color bottom) =>
        Paint.Linear(
            new GradientStops([new ColorStop(0f, top), new ColorStop(1f, bottom)]),
            Vector2.Zero,
            Vector2.UnitY
        );
}

/// <summary>Keys and chords as a key cap shows them, such as <c>Ctrl+PgUp</c> or <c>Esc</c>.</summary>
internal static class KeyLabels
{
    static readonly string[] Names = Build();

    [FramePath]
    public static TextValue Of(Ui ui, Chord chord)
    {
        if (chord.Key == Key.None)
            return default;
        var m = chord.Modifiers;
        return ui.Format(
            $"{((m & ModifierKeys.Control) != 0 ? "Ctrl+" : "")}{((m & ModifierKeys.Shift) != 0 ? "Shift+" : "")}{((m & ModifierKeys.Alt) != 0 ? "Alt+" : "")}{((m & ModifierKeys.Gui) != 0 ? "Win+" : "")}{Name(chord.Key)}"
        );
    }

    [FramePath]
    static string Name(Key key) => (uint)key < (uint)Names.Length ? Names[(int)key] : "";

    static string[] Build()
    {
        var keys = Enum.GetValues<Key>();
        var names = new string[(int)keys[^1] + 1];
        foreach (var key in keys)
            names[(int)key] = key switch
            {
                >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
                >= Key.NumPad0 and <= Key.NumPad9 => "Num " + (char)('0' + (key - Key.NumPad0)),
                Key.Escape => "Esc",
                Key.BackSpace => "Backspace",
                Key.PageUp => "PgUp",
                Key.PageDown => "PgDn",
                Key.Delete => "Del",
                Key.Insert => "Ins",
                Key.PrintScreen => "PrtSc",
                Key.Add => "Num +",
                Key.Subtract => "Num -",
                Key.Multiply => "Num *",
                Key.Divide => "Num /",
                Key.Decimal => "Num .",
                Key.OemPlus => "=",
                Key.OemMinus => "-",
                Key.OemComma => ",",
                Key.OemPeriod => ".",
                Key.OemQuestion => "/",
                Key.OemSemicolon => ";",
                Key.OemTilde => "`",
                Key.OemBracketLeft => "[",
                Key.OemBracketRight => "]",
                Key.OemPipe => "\\",
                Key.OemQuotes => "'",
                _ => key.ToString(),
            };
        return names;
    }
}

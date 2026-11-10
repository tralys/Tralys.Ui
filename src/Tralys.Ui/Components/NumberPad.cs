using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>What a <see cref="NumberPad"/> key does; a digit's value is the digit it types.</summary>
public enum NumberPadKeyKind : byte
{
    Digit0,
    Digit1,
    Digit2,
    Digit3,
    Digit4,
    Digit5,
    Digit6,
    Digit7,
    Digit8,
    Digit9,

    /// <summary>Makes the value 0.</summary>
    Clear,

    /// <summary>Drops the last digit.</summary>
    Back,

    /// <summary>Steps down by the field's step.</summary>
    Down,

    /// <summary>Steps up by the field's step.</summary>
    Up,

    /// <summary>Takes the field's bound.</summary>
    Max,

    /// <summary>Multiplies an amount by a thousand.</summary>
    K,

    /// <summary>Multiplies an amount by a million.</summary>
    M,

    /// <summary>Multiplies an amount by a billion.</summary>
    B,

    /// <summary>Lets the focus go.</summary>
    Done,
}

public readonly record struct NumberPadProps
{
    /// <summary>Shows − and +, for a field with a step.</summary>
    public bool HasStep { get; init; }

    /// <summary>Shows K, M and B, for an amount.</summary>
    public bool HasScales { get; init; }

    /// <summary>Shows Max, for a field with a bound of its own.</summary>
    public bool HasMax { get; init; }

    public Callback<NumberPadKeyKind> OnKey { get; init; }
}

public readonly record struct NumberPadKeyProps
{
    public NumberPadKeyKind Kind { get; init; }
}

/// <summary>What a <see cref="NumberPad"/> tells its keys: where a key reports its press.</summary>
public readonly record struct NumberPadContext(Callback<NumberPadKeyKind> OnKey);

/// <summary>Keys for a number field under touch density, in place of a soft keyboard that covers half a phone's
/// screen: a row of − and +, K, M and B, and Max, as the field has them, over the digits, C and the back key, with the
/// tick that lets the focus go beside them. Its keys never take the focus, so the field keeps it, and each reports
/// its press through <see cref="NumberPadProps.OnKey"/>; the field does the arithmetic.</summary>
public sealed class NumberPad : Component<NumberPadProps>
{
    public static readonly ContextKey<NumberPadContext> Context = new("NumberPad");

    const float KeyWidth = 52f;

    static readonly GridTracks Four = GridTracks.Repeat(4, KeyWidth);
    static readonly GridTracks Three = GridTracks.Repeat(3, KeyWidth);

    protected override void Render(Ui ui, in NumberPadProps p)
    {
        var t = ui.Theme;
        ui.Provide(Context, new NumberPadContext(p.OnKey));
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 4f,
                padding: 4f,
                background: t.Color(ColorToken.BgRaised),
                border: new Border(1f, t.Color(ColorToken.BorderOverlay)),
                radius: t.Radius(RadiusToken.Md)
            )
        )
        {
            using (ui.Grid(columns: Four, gap: t.Spacing))
            {
                if (p.HasStep)
                {
                    ui.NumberPadKey(kind: NumberPadKeyKind.Down);
                    ui.NumberPadKey(kind: NumberPadKeyKind.Up);
                }
                if (p.HasScales)
                {
                    ui.NumberPadKey(kind: NumberPadKeyKind.K);
                    ui.NumberPadKey(kind: NumberPadKeyKind.M);
                    ui.NumberPadKey(kind: NumberPadKeyKind.B);
                }
                if (p.HasMax)
                    ui.NumberPadKey(kind: NumberPadKeyKind.Max);
            }
            using (ui.Box())
            {
                using (ui.Grid(columns: Three, gap: t.Spacing))
                {
                    for (var digit = 1; digit <= 9; digit++)
                        ui.NumberPadKey(kind: (NumberPadKeyKind)digit, key: digit);
                    ui.NumberPadKey(kind: NumberPadKeyKind.Clear);
                    ui.NumberPadKey(kind: NumberPadKeyKind.Digit0);
                    ui.NumberPadKey(kind: NumberPadKeyKind.Back);
                }
                ui.NumberPadKey(kind: NumberPadKeyKind.Done, margin: new Thickness(4f, 0f, 0f, 0f));
            }
        }
    }

    /// <summary>One of a <see cref="NumberPad"/>'s keys: a small outlined button 52 wide, or the filled primary tick for
    /// <see cref="NumberPadKeyKind.Done"/>, that never takes the focus and reports its press to the pad around it.</summary>
    public sealed class Key : Component<NumberPadKeyProps>
    {
        static readonly string[] Digits = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];

        protected override void Render(Ui ui, in NumberPadKeyProps p)
        {
            var t = ui.Theme;
            var m = t.Metrics(ComponentSize.Sm);
            var done = p.Kind == NumberPadKeyKind.Done;
            var scheme = done ? ColorScheme.Primary : ColorScheme.Secondary;
            var variant = done ? ComponentVariant.Filled : ComponentVariant.Outlined;
            var look = ui.Animated(t.Palette(scheme, variant, ui.ComponentState));
            var art = done ? t.Skin(SkinPart.ButtonPrimary) : null;
            var radius = t.Radius(RadiusToken.Md);
            using (
                ui.Box(
                    align: Align.Center,
                    justify: Justify.Center,
                    gap: m.Gap,
                    width: KeyWidth,
                    minHeight: m.Height,
                    padding: art is null ? ButtonParts.Inside(m.PaddingX, 1f) : m.PaddingX,
                    background: art is null ? look.Background : default(Paint),
                    border: art is null ? new Border(1f, look.Border) : default,
                    radius: radius,
                    fontSize: m.FontSize,
                    fontWeight: t.Weight(WeightToken.Interface),
                    opacity: ui.IsDisabled ? 0.5f : null,
                    cursor: Cursors.Hand,
                    focusable: false,
                    onClick: Callback.Of(
                        ui.Self(p),
                        static h => h.Use(Context).OnKey.Invoke(h.Props.Kind)
                    )
                )
            )
            {
                if (art is { } rest)
                {
                    ui.Art(art: rest);
                    if (ui.IsHovered && t.Skin(SkinPart.ButtonPrimaryHover) is { } hover)
                        ui.Art(art: hover);
                }
                ui.Label(
                    text: TextOf(p.Kind),
                    content: default,
                    iconLeft: IconOf(p.Kind),
                    iconRight: default,
                    iconSize: m.FontSize,
                    gap: m.Gap,
                    color: look.Foreground
                );
            }
        }

        static TextValue TextOf(NumberPadKeyKind key) =>
            key switch
            {
                <= NumberPadKeyKind.Digit9 => Digits[(int)key],
                NumberPadKeyKind.Clear => "C",
                NumberPadKeyKind.Max => "Max",
                NumberPadKeyKind.K => "K",
                NumberPadKeyKind.M => "M",
                NumberPadKeyKind.B => "B",
                _ => default,
            };

        static IconGlyph IconOf(NumberPadKeyKind key) =>
            key switch
            {
                NumberPadKeyKind.Back => IconGlyph.Of(IconRole.Delete),
                NumberPadKeyKind.Down => IconGlyph.Of(IconRole.Minus),
                NumberPadKeyKind.Up => IconGlyph.Of(IconRole.Plus),
                NumberPadKeyKind.Done => IconGlyph.Of(IconRole.Check),
                _ => default,
            };
    }

    /// <summary><paramref name="value"/> after <paramref name="key"/>, within 0 and <paramref name="max"/>, saturating
    /// rather than overflowing.</summary>
    [FramePath]
    internal static long Apply(NumberPadKeyKind key, long value, long max, long step)
    {
        var next = key switch
        {
            <= NumberPadKeyKind.Digit9 => value > (long.MaxValue - (int)key) / 10
                ? long.MaxValue
                : value * 10 + (int)key,
            NumberPadKeyKind.Clear => 0,
            NumberPadKeyKind.Back => value / 10,
            NumberPadKeyKind.Down => value < long.MinValue + step ? long.MinValue : value - step,
            NumberPadKeyKind.Up => value > long.MaxValue - step ? long.MaxValue : value + step,
            NumberPadKeyKind.Max => max,
            NumberPadKeyKind.K => Scaled(value, 1_000),
            NumberPadKeyKind.M => Scaled(value, 1_000_000),
            NumberPadKeyKind.B => Scaled(value, 1_000_000_000),
            _ => value,
        };
        return Math.Max(0, Math.Min(next, max));
    }

    static long Scaled(long value, long factor) =>
        value > long.MaxValue / factor ? long.MaxValue : value * factor;
}

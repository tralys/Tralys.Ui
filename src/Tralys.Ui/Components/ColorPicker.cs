using System.Globalization;
using System.Numerics;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct ColorPickerProps
{
    public Color Value { get; init; }

    public Callback<Color> OnValueChange { get; init; }

    /// <summary>The colours offered beside the square; null offers the built-in seven.</summary>
    public IReadOnlyList<Color>? Swatches { get; init; }

    /// <summary>Whether the swatches show; true when null.</summary>
    public bool? ShowSwatches { get; init; }

    /// <summary>The columns the swatches lay out in; one stacks them, as 0 does.</summary>
    public int SwatchColumns { get; init; }

    /// <summary>Shows an opacity bar; without it every edit yields an opaque colour.</summary>
    public bool ShowAlpha { get; init; }

    /// <summary>Shows the colour the picker mounted with beside the current one; a click on it brings it back.</summary>
    public bool ShowPrevious { get; init; }

    /// <summary>Whether the hex box shows; true when null.</summary>
    public bool? ShowHex { get; init; }

    /// <summary>The rung the knobs, bars and hex box take; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    public bool Disabled { get; init; }
}

public readonly record struct ColorPickerSelectProps
{
    public Color Value { get; init; }

    public Callback<Color> OnValueChange { get; init; }

    /// <summary>Whether the picker shows; null leaves it to the swatch, starting at <see cref="DefaultOpen"/>.</summary>
    public bool? Open { get; init; }

    public bool DefaultOpen { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    /// <summary>The colours the picker offers beside its square; null offers its built-in seven.</summary>
    public IReadOnlyList<Color>? Swatches { get; init; }

    public int SwatchColumns { get; init; }

    /// <summary>Whether picking a swatch leaves the picker open.</summary>
    public bool StaysOpen { get; init; }

    public bool ShowAlpha { get; init; }

    public bool ShowPrevious { get; init; }

    /// <summary>The swatch's rung, which it is as wide and tall as; <see cref="ComponentSize.Xxs"/> by
    /// default.</summary>
    public ComponentSize? Size { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>Picks a colour from swatches, a saturation and value square, a hue bar, an optional opacity bar and a hex
/// box. <see cref="ColorPickerProps.Value"/> is the colour, which every edit reports through
/// <see cref="ColorPickerProps.OnValueChange"/> as it goes, a drag on each move.</summary>
/// <remarks>It holds where its knobs stand, so a drag lands where the pointer let go even through grey and black, which
/// a colour alone cannot place; a value from outside moves them. A left press on the square or a bar takes the pointer
/// until its release, and a second pointer meanwhile changes nothing. A knob's centre stops half the knob short of
/// each end, and its knobs and bars follow the size's icon size. The hex box shows the colour as <c>#RRGGBB</c>, or
/// <c>#RRGGBBAA</c> when translucent with the opacity bar shown, and a valid code typed there is the colour at once,
/// while what was typed stays until Enter or the focus leaves. Over a clear colour a swatch brings its own
/// alpha.</remarks>
public sealed partial class ColorPicker : Component<ColorPickerProps>
{
    internal const float MinWidth = 200f;
    internal const float MinSquareHeight = 120f;
    internal const float HexWidth = 96f;
    internal const float SwatchSize = 24f;

    internal static readonly Color[] DefaultSwatches =
    [
        Color.FromRgb(0xFF, 0x99, 0x00),
        Color.FromRgb(0xFF, 0x55, 0x55),
        Color.FromRgb(0xFF, 0x99, 0xCC),
        Color.FromRgb(0x66, 0xFF, 0x66),
        Color.FromRgb(0xFF, 0xFF, 0x66),
        Color.FromRgb(0x66, 0xCC, 0xFF),
        Color.FromRgb(0xFF, 0xFF, 0xFF),
    ];

    static readonly GradientStops Hues = new([
        new ColorStop(0f, Color.FromRgb(0xFF, 0x00, 0x00)),
        new ColorStop(0.1667f, Color.FromRgb(0xFF, 0xFF, 0x00)),
        new ColorStop(0.3333f, Color.FromRgb(0x00, 0xFF, 0x00)),
        new ColorStop(0.5f, Color.FromRgb(0x00, 0xFF, 0xFF)),
        new ColorStop(0.6667f, Color.FromRgb(0x00, 0x00, 0xFF)),
        new ColorStop(0.8333f, Color.FromRgb(0xFF, 0x00, 0xFF)),
        new ColorStop(1f, Color.FromRgb(0xFF, 0x00, 0x00)),
    ]);

    static readonly GradientStops Whites = new([
        new ColorStop(0f, Color.FromArgb(255, 255, 255, 255)),
        new ColorStop(1f, Color.FromArgb(0, 255, 255, 255)),
    ]);

    static readonly GradientStops Shade = new([
        new ColorStop(0f, Color.FromArgb(0, 255, 255, 255)),
        new ColorStop(1f, Color.FromArgb(255, 0, 0, 0)),
    ]);

    static readonly GradientStops Opaque = new([
        new ColorStop(0f, Color.FromArgb(0, 0, 0, 0)),
        new ColorStop(1f, Color.FromArgb(255, 0, 0, 0)),
    ]);

    internal static readonly GradientStops Checker = new([
        new ColorStop(0f, Color.FromRgb(0xB4, 0xB4, 0xB4)),
        new ColorStop(0.5f, Color.FromRgb(0xB4, 0xB4, 0xB4)),
        new ColorStop(0.5f, Color.FromRgb(0x5A, 0x5A, 0x5A)),
        new ColorStop(1f, Color.FromRgb(0x5A, 0x5A, 0x5A)),
    ]);

    /// <summary>What a picker inside a <see cref="Select"/> runs after a swatch picks its colour.</summary>
    internal static readonly ContextKey<Callback> Swatched = new("ColorPicker.Swatched", default);

    protected override void Render(Ui ui, in ColorPickerProps p)
    {
        var t = ui.Theme;
        var m = t.Metrics(p.Size ?? ComponentSize.Md);
        var model = ui.Remember(static () => ColorPickerModel.Rent());
        model.Host = ui.Self(p);
        model.Knob = m.IconSize;
        model.Swatched = ui.Use(Swatched);
        var changes = ui.State(0);
        model.Changes = changes;
        _ = changes.Value;
        model.Follow(p.Value);
        var hsv = model.Hsv;
        var knob = m.IconSize;
        var bar = MathF.Max(0f, knob - 4f);
        var radius = t.Radius(RadiusToken.Md);
        var value = p.Value;
        var hand = ui.IsDisabled ? Cursors.Arrow : Cursors.Hand;
        using (ui.Box(minWidth: MinWidth, width: p.Width, height: p.Height))
        {
            if (p.ShowSwatches ?? true)
                Swatches(ui, model, p.Swatches ?? DefaultSwatches, p.SwatchColumns, value);
            using (ui.Box(direction: Direction.Column, grow: 1f, minWidth: 0f))
            {
                using (
                    ui.Box(
                        grow: 1f,
                        minHeight: MinSquareHeight,
                        background: new HsvColor(hsv.H, 1f, 1f, 255).ToColor(),
                        cursor: hand,
                        onPointerDown: model.SquareDown,
                        onPointerMove: model.Move,
                        onPointerUp: model.Up,
                        onPointerCancel: model.Cancel
                    )
                )
                {
                    Fill(ui, Paint.Linear(Whites, Vector2.Zero, Vector2.UnitX));
                    Fill(ui, Paint.Linear(Shade, Vector2.Zero, Vector2.UnitY));
                    using (Travel(ui, knob, top: knob / 2f, bottom: knob / 2f))
                        Knob(ui, knob, value, hsv.S, 1f - hsv.V);
                }
                using (
                    ui.Box(
                        height: bar,
                        margin: new Thickness(0f, 8f, 0f, 8f),
                        background: Paint.Linear(Hues, Vector2.Zero, Vector2.UnitX),
                        radius: radius,
                        cursor: hand,
                        onPointerDown: model.HueDown,
                        onPointerMove: model.Move,
                        onPointerUp: model.Up,
                        onPointerCancel: model.Cancel
                    )
                )
                using (Travel(ui, knob, top: (bar - knob) / 2f, bottom: null))
                    Knob(ui, knob, value, hsv.H / 360f, null);
                if (p.ShowAlpha)
                    using (
                        ui.Box(
                            height: bar,
                            margin: new Thickness(0f, 0f, 0f, 8f),
                            background: Paint.Linear(Checker, Vector2.Zero, Vector2.UnitY),
                            radius: radius,
                            cursor: hand,
                            onPointerDown: model.AlphaDown,
                            onPointerMove: model.Move,
                            onPointerUp: model.Up,
                            onPointerCancel: model.Cancel
                        )
                    )
                    {
                        using (
                            ui.Box(
                                background: value with
                                {
                                    A = 255,
                                },
                                mask: Paint.Linear(Opaque, Vector2.Zero, Vector2.UnitX),
                                radius: radius,
                                pointerEvents: PointerEvents.None,
                                position: Position.Absolute,
                                left: 0f,
                                top: 0f,
                                right: 0f,
                                bottom: 0f
                            )
                        ) { }
                        using (Travel(ui, knob, top: (bar - knob) / 2f, bottom: null))
                            Knob(ui, knob, value, hsv.A / 255f, null);
                    }
                if (p.ShowPrevious || (p.ShowHex ?? true))
                    using (ui.Box(align: Align.Center))
                    {
                        if (p.ShowPrevious)
                            using (ui.Box(margin: new Thickness(0f, 0f, 8f, 0f), shrink: 0f))
                            {
                                using (ui.Tooltip(text: "Back to the previous color"))
                                using (
                                    ui.Box(
                                        width: 18f,
                                        height: SwatchSize,
                                        background: model.Previous,
                                        cursor: hand,
                                        focusable: false,
                                        onClick: model.Restore
                                    )
                                ) { }
                                using (ui.Box(width: 18f, height: SwatchSize, background: value))
                                { }
                            }
                        if (p.ShowHex ?? true)
                            ui.ColorPickerHex(
                                model: model,
                                value: value,
                                alpha: p.ShowAlpha,
                                size: p.Size,
                                changes: changes.Value,
                                grow: 1f
                            );
                    }
            }
        }
    }

    [FramePath]
    static void Swatches(
        Ui ui,
        ColorPickerModel model,
        IReadOnlyList<Color> colors,
        int columns,
        Color value,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    )
    {
        var grid = columns > 1;
        using (
            ui.Box(
                direction: grid ? Direction.Row : Direction.Column,
                wrap: grid ? FlexWrap.Wrap : default,
                align: Align.Start,
                alignContent: AlignContent.Start,
                gap: grid ? 0f : 4f,
                width: grid ? columns * (SwatchSize + 4f) : Length.Auto,
                margin: new Thickness(0f, 0f, 16f, 0f),
                shrink: 0f,
                file: file,
                line: line
            )
        )
        {
            for (var i = 0; i < colors.Count; i++)
                ui.ColorPickerSwatch(
                    color: colors[i],
                    selected: colors[i] == (value with { A = 255 }),
                    onPick: model.Pick,
                    margin: grid ? new Thickness(2f) : default,
                    key: i
                );
        }
    }

    [FramePath]
    static void Fill(
        Ui ui,
        Paint paint,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                background: paint,
                pointerEvents: PointerEvents.None,
                position: Position.Absolute,
                left: 0f,
                top: 0f,
                right: 0f,
                bottom: 0f,
                file: file,
                line: line
            )
        ) { }
    }

    /// <summary>The area a knob's centre runs over: the part less half a knob at each end.</summary>
    [FramePath]
    static Scope Travel(
        Ui ui,
        float knob,
        float top,
        float? bottom,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    ) =>
        ui.Box(
            pointerEvents: PointerEvents.None,
            position: Position.Absolute,
            left: knob / 2f,
            right: knob / 2f,
            top: top,
            bottom: bottom is { } b ? b : Length.Auto,
            height: bottom is null ? knob : Length.Auto,
            file: file,
            line: line
        );

    [FramePath]
    static void Knob(
        Ui ui,
        float size,
        Color color,
        float x,
        float? y,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                width: size,
                height: size,
                background: color,
                border: new Border(2f, Color.FromRgb(0xFF, 0xFF, 0xFF)),
                radius: size / 2f,
                pointerEvents: PointerEvents.None,
                position: Position.Absolute,
                left: Length.Percent(x * 100f),
                top: y is { } down ? Length.Percent(down * 100f) : 0f,
                margin: new Thickness(-size / 2f, y is null ? 0f : -size / 2f, 0f, 0f),
                file: file,
                line: line
            )
        ) { }
    }

    [FramePath]
    internal static TextValue Hex(Ui ui, Color c, bool alpha) =>
        alpha && c.A < 255
            ? ui.Format($"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}")
            : ui.Format($"#{c.R:X2}{c.G:X2}{c.B:X2}");

    [FramePath]
    internal static bool TryParseHex(
        ReadOnlySpan<char> text,
        bool alpha,
        byte edited,
        out Color color
    )
    {
        color = default;
        var digits = text.Trim().TrimStart('#');
        if (
            (digits.Length != 6 && !(digits.Length == 8 && alpha))
            || !uint.TryParse(
                digits,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var hex
            )
        )
            return false;
        color =
            digits.Length == 8
                ? Color.FromArgb((byte)hex, (byte)(hex >> 24), (byte)(hex >> 16), (byte)(hex >> 8))
                : Color.FromArgb(edited, (byte)(hex >> 16), (byte)(hex >> 8), (byte)hex);
        return true;
    }

    /// <summary>A swatch of <see cref="ColorPickerSelectProps.Value"/>, as wide and tall as the theme's height for its
    /// size, over a checker while the colour is translucent, that opens a <see cref="ColorPicker"/> below it on a
    /// click; the picker reports through <see cref="ColorPickerSelectProps.OnValueChange"/>, and a swatch picked there
    /// closes it unless <see cref="ColorPickerSelectProps.StaysOpen"/>. It dims while disabled.</summary>
    public sealed class Select : Component<ColorPickerSelectProps>
    {
        protected override void Render(Ui ui, in ColorPickerSelectProps p)
        {
            var t = ui.Theme;
            var side = t.Metrics(p.Size ?? ComponentSize.Xxs).Height;
            var radius = t.Radius(RadiusToken.Lg);
            var disclosure = ui.Remember(
                ui.State(p.DefaultOpen),
                static own => new Disclosure(own)
            );
            disclosure.Open = p.Open;
            disclosure.OnOpenChange = p.OnOpenChange;
            var disabled = ui.IsDisabled;
            ui.Provide(SelectContext, new ColorPickerSelectContext(p, disclosure.Close));
            using (
                ui.Popover(
                    open: (p.Open ?? disclosure.Own.Value) && !disabled,
                    onOpenChange: disclosure.SetOpen,
                    content: Body,
                    placement: PlacementMode.Bottom
                )
            )
            using (
                ui.Box(
                    width: side,
                    height: side,
                    opacity: disabled ? 0.5f : null,
                    cursor: disabled ? null : Cursors.Hand,
                    onClick: disclosure.Toggle
                )
            )
            {
                if (p.Value.A < 255)
                    using (
                        ui.Box(
                            background: Paint.Linear(Checker, Vector2.Zero, Vector2.UnitY),
                            radius: radius,
                            position: Position.Absolute,
                            left: 0f,
                            top: 0f,
                            right: 0f,
                            bottom: 0f
                        )
                    ) { }
                using (
                    ui.Box(
                        grow: 1f,
                        background: p.Value,
                        border: new Border(1f, t.Color(ColorToken.BorderMuted)),
                        radius: radius
                    )
                ) { }
                if (ui.IsFocusVisible)
                    ui.FocusRing(radius: radius);
            }
        }

        internal static readonly ContextKey<ColorPickerSelectContext> SelectContext = new(
            "ColorPicker.Select",
            default
        );

        static readonly Slot Body = Slot.Of(static ui => ui.ColorPickerSelectBody());
    }
}

internal readonly record struct ColorPickerSelectContext(
    ColorPickerSelectProps Props,
    Callback Close
);

internal readonly record struct ColorPickerSelectBodyProps;

internal sealed class ColorPickerSelectBody : Component<ColorPickerSelectBodyProps>
{
    protected override void Render(Ui ui, in ColorPickerSelectBodyProps p)
    {
        var c = ui.Use(ColorPicker.Select.SelectContext);
        var s = c.Props;
        ui.Provide(ColorPicker.Swatched, s.StaysOpen ? default : c.Close);
        ui.ColorPicker(
            value: s.Value,
            onValueChange: s.OnValueChange,
            swatches: s.Swatches,
            swatchColumns: s.SwatchColumns,
            showAlpha: s.ShowAlpha,
            showPrevious: s.ShowPrevious
        );
    }
}

internal readonly record struct ColorPickerHexProps
{
    public required ColorPickerModel Model { get; init; }

    public Color Value { get; init; }

    public bool Alpha { get; init; }

    public ComponentSize? Size { get; init; }

    public int Changes { get; init; }
}

/// <summary>The hex box: the colour's code, or what was typed while the box keeps the focus; Enter or the focus
/// leaving puts the code back.</summary>
internal sealed class ColorPickerHex : Component<ColorPickerHexProps>
{
    protected override void Render(Ui ui, in ColorPickerHexProps p)
    {
        var model = p.Model;
        if (!ui.IsFocusWithin)
            model.Drafting = false;
        if (!model.Drafting)
            model.HexDraft.Set(ColorPicker.Hex(ui, p.Value, p.Alpha).Span);
        using (ui.Box(align: Align.Center, onKeyDown: model.HexKey))
            ui.Input(text: model.HexDraft, onEdit: model.HexTyped, size: p.Size, grow: 1f);
    }
}

internal readonly record struct ColorPickerSwatchProps
{
    public Color Color { get; init; }

    public bool Selected { get; init; }

    public Callback<Color> OnPick { get; init; }
}

/// <summary>A swatch a click picks, pressed anywhere in its box however clear its colour, ringed while selected.</summary>
internal sealed class ColorPickerSwatch : Component<ColorPickerSwatchProps>
{
    protected override void Render(Ui ui, in ColorPickerSwatchProps p)
    {
        var t = ui.Theme;
        using (
            ui.Box(
                width: ColorPicker.SwatchSize,
                height: ColorPicker.SwatchSize,
                background: p.Color,
                cursor: Cursors.Hand,
                focusable: false,
                onClick: Callback.Of(ui.Self(p), static h => h.Props.OnPick.Invoke(h.Props.Color))
            )
        )
        {
            if (p.Selected)
                using (
                    ui.Box(
                        border: new Border(2f, t.Color(ColorToken.BorderSelected)),
                        pointerEvents: PointerEvents.None,
                        position: Position.Absolute,
                        left: -3f,
                        top: -3f,
                        right: -3f,
                        bottom: -3f
                    )
                ) { }
        }
    }
}

/// <summary>Held, not re-derived from the colour: grey has no hue and black no saturation to round-trip.</summary>
internal readonly record struct HsvColor(float H, float S, float V, byte A)
{
    [FramePath]
    public static HsvColor FromColor(Color color, HsvColor previous)
    {
        var r = color.R / 255f;
        var g = color.G / 255f;
        var b = color.B / 255f;
        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        var delta = max - min;
        var hue = previous.H;
        if (delta > 0f)
        {
            hue =
                max == r ? 60f * ((g - b) / delta % 6f)
                : max == g ? 60f * ((b - r) / delta + 2f)
                : 60f * ((r - g) / delta + 4f);
            if (hue < 0f)
                hue += 360f;
        }
        var saturation = max > 0f ? delta / max : previous.S;
        return new(hue, saturation, max, color.A);
    }

    [FramePath]
    public Color ToColor()
    {
        var h = (H % 360f + 360f) % 360f;
        var c = V * S;
        double chroma = c;
        double x = c * (1f - MathF.Abs(h / 60f % 2f - 1f));
        var (r, g, b) = h switch
        {
            < 60f => (chroma, x, 0d),
            < 120f => (x, chroma, 0d),
            < 180f => (0d, chroma, x),
            < 240f => (0d, x, chroma),
            < 300f => (x, 0d, chroma),
            _ => (chroma, 0d, x),
        };
        var m = V - c;
        return Color.FromArgb(A, Channel(r + m), Channel(g + m), Channel(b + m));
    }

    static byte Channel(double unit) => (byte)Math.Round(Math.Clamp(unit, 0d, 1d) * 255d);
}

[FramePath]
internal sealed class ColorPickerModel : IDisposable
{
    enum Part : byte
    {
        None,
        Square,
        Hue,
        Alpha,
    }

    internal readonly Handler<PointerEvent> SquareDown;
    internal readonly Handler<PointerEvent> HueDown;
    internal readonly Handler<PointerEvent> AlphaDown;
    internal readonly Handler<PointerEvent> Move;
    internal readonly Handler<PointerEvent> Up;
    internal readonly Handler<PointerEvent> Cancel;
    internal readonly Callback<Color> Pick;
    internal readonly Callback Restore;
    internal readonly Callback HexTyped;
    internal readonly Handler<KeyEvent> HexKey;
    internal Host<ColorPickerProps> Host;
    internal Callback Swatched;
    internal State<int>? Changes;
    internal float Knob;
    internal HsvColor Hsv = new(60f, 0f, 0f, 255);
    internal Color Previous;
    internal readonly ObservableText HexDraft = new();
    internal bool Drafting;
    Color _shown;
    bool _followed;
    Part _drag;
    Pointer _pointer;

    public ColorPickerModel()
    {
        SquareDown = Handler<PointerEvent>.Of(
            this,
            static (m, ref e) => m.Down(ref e, Part.Square)
        );
        HueDown = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Down(ref e, Part.Hue));
        AlphaDown = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Down(ref e, Part.Alpha));
        Move = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Moved(ref e));
        Up = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Released(ref e));
        Cancel = Handler<PointerEvent>.Of(
            this,
            static (m, ref e) =>
            {
                if (m._drag != Part.None && e.Pointer == m._pointer)
                    m._drag = Part.None;
            }
        );
        Pick = Callback<Color>.Of(this, static (m, swatch) => m.Swatch(swatch));
        Restore = Callback.Of(this, static m => m.Report(m.Previous, rederive: true));
        HexTyped = Callback.Of(this, static m => m.Typed());
        HexKey = Handler<KeyEvent>.Of(
            this,
            static (m, ref e) =>
            {
                if (e.Key == Key.Enter)
                    m.Committed();
            }
        );
    }

    ref readonly ColorPickerProps Props => ref Host.Props;

    internal static ColorPickerModel Rent() =>
        Pool<ColorPickerModel>.TryRent() ?? new ColorPickerModel();

    public void Dispose()
    {
        Host = default;
        Swatched = default;
        Changes = null;
        Hsv = new(60f, 0f, 0f, 255);
        Previous = _shown = default;
        Drafting = false;
        _followed = false;
        _drag = Part.None;
        Pool<ColorPickerModel>.Return(this);
    }

    byte EditedAlpha => Props.ShowAlpha ? Hsv.A : (byte)255;

    /// <summary>Changes the knobs to a value from outside, leaving them where they stand for the value they made.</summary>
    internal void Follow(Color value)
    {
        if (!_followed)
        {
            _followed = true;
            Previous = value;
        }
        else if (value == _shown)
            return;
        _shown = value;
        Hsv = HsvColor.FromColor(value, Hsv);
    }

    void Down(ref PointerEvent e, Part part)
    {
        if (
            (_drag != Part.None && e.Pointer != _pointer)
            || (e.Pointer.Kind == PointerKind.Mouse && e.Button != MouseButton.Left)
        )
            return;
        e.Capture();
        e.PreventDefault();
        e.StopPropagation();
        _drag = part;
        _pointer = e.Pointer;
        DragTo(e.Position, e.Size);
    }

    void Moved(ref PointerEvent e)
    {
        if (_drag == Part.None || e.Pointer != _pointer || e.Phase != PointerPhase.Move)
            return;
        if (
            e.Pointer.Kind == PointerKind.Mouse
                ? (e.Buttons & MouseButtons.Left) == 0
                : !e.IsInContact
        )
        {
            _drag = Part.None;
            return;
        }
        e.StopPropagation();
        DragTo(e.Position, e.Size);
    }

    void Released(ref PointerEvent e)
    {
        if (_drag == Part.None || e.Pointer != _pointer)
            return;
        e.StopPropagation();
        _drag = Part.None;
    }

    void DragTo(Vector2 at, Size size)
    {
        var knob = Knob;
        var x = Math.Clamp((at.X - knob / 2f) / MathF.Max(1f, size.Width - knob), 0f, 1f);
        var y = Math.Clamp((at.Y - knob / 2f) / MathF.Max(1f, size.Height - knob), 0f, 1f);
        var hsv = _drag switch
        {
            Part.Square => Hsv with { S = x, V = 1f - y },
            Part.Hue => Hsv with { H = x * 360f },
            _ => Hsv with { A = (byte)MathF.Round(x * 255f) },
        };
        if (hsv != Hsv)
        {
            Hsv = hsv;
            Changed();
        }
        Report((Hsv with { A = EditedAlpha }).ToColor(), rederive: false);
    }

    void Swatch(Color swatch)
    {
        var alpha = EditedAlpha;
        Report(alpha == 0 ? swatch : swatch with { A = alpha }, rederive: true);
        Swatched.Invoke();
    }

    void Typed()
    {
        Drafting = true;
        Changed();
        if (ColorPicker.TryParseHex(HexDraft.Span, Props.ShowAlpha, EditedAlpha, out var color))
            Report(color, rederive: true);
    }

    void Committed()
    {
        if (!Drafting)
            return;
        Drafting = false;
        Changed();
    }

    void Changed()
    {
        if (Changes is { } changes)
            changes.Value++;
    }

    void Report(Color color, bool rederive)
    {
        if (rederive)
            Hsv = HsvColor.FromColor(color, Hsv);
        _shown = color;
        if (color != Props.Value)
            Props.OnValueChange.Invoke(color);
    }
}

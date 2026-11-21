using System.Globalization;
using System.Numerics;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>How a <see cref="NumberInput"/> reads and shows its value.</summary>
public enum NumberMode : byte
{
    /// <summary>Whole numbers, grouped by thousands.</summary>
    Integer,

    /// <summary>Numbers with a fraction, shown to <see cref="NumberInputProps.DecimalPlaces"/>.</summary>
    Decimal,
}

/// <summary>What a number field tells its steppers: whether each can step.</summary>
internal readonly record struct NumberSteppers(bool CanDown, bool CanUp);

/// <summary>The text, keys, pad and settling a <see cref="NumberInput"/> and a <see cref="MoneyInput"/> share. Each
/// edit is read from the field's text as it stands and the number it reads reported, the text standing as typed or
/// regrouped with the caret counted from its end, and the value echoing it leaves the text alone; the field settles as
/// the focus leaves, on Enter or on a step: then the text is read once more, the value it settles to is reported, and
/// the field shows that value again. Nothing is built as a string.</summary>
[FramePath]
internal abstract class NumberFieldModel
{
    internal const int MaxChars = 128;

    internal static readonly ContextKey<NumberSteppers> Steppers = new(
        "NumberSteppers",
        new NumberSteppers(true, true)
    );

    internal readonly Handler<KeyEvent> Keys;
    internal readonly Callback<NumberPadKeyKind> PadKey;
    internal readonly Callback Edit;
    internal NodeRef Text;
    internal readonly NodeRef Field;
    internal readonly State<int> Rewrites;
    internal readonly ObservableText Draft = new();
    char[] _buffer = new char[MaxChars];
    int _shown;
    char[] _synced = new char[MaxChars];
    int _syncedLength = -1;
    bool _take;
    Index? _takeCaret;
    bool _echoing;
    bool _focused;
    long _typed;
    bool _hasTyped;

    protected NumberFieldModel(NodeRef text, NodeRef field, State<int> rewrites)
    {
        Text = text;
        Field = field;
        Rewrites = rewrites;
        Keys = Handler<KeyEvent>.Of(this, static (m, ref e) => m.KeyDown(ref e));
        PadKey = Callback<NumberPadKeyKind>.Of(this, static (m, key) => m.Pad(key));
        Edit = Callback.Of(this, static m => m.Typed());
    }

    protected static CultureInfo Culture => CultureInfo.CurrentCulture;

    protected abstract bool ReadOnly { get; }

    /// <summary>The value the pad types, read saturated to a whole number.</summary>
    protected abstract long PadValue { get; }

    /// <summary><see cref="long.MaxValue"/> when the field has no bound of its own.</summary>
    internal abstract long PadMax { get; }

    /// <summary>What − and + move the value by; zero shows neither.</summary>
    internal abstract long PadStep { get; }

    /// <summary>Reports the pad's value, written without the field's minimum, so a value typed digit by digit can pass
    /// below it.</summary>
    protected abstract void ReportPad(long value);

    protected abstract void Read(ReadOnlySpan<char> text, int trailing);

    /// <summary>Reads <paramref name="text"/> when <paramref name="edited"/>, else the value, settles it into the
    /// field's bounds and kind of number, and reports it if it differs from the value; what doesn't read as a number
    /// settles the value as it is.</summary>
    protected abstract void Settle(ReadOnlySpan<char> text, bool edited);

    /// <summary>Runs the field's submit handler with its settled value; false when it has none, so Enter goes
    /// on.</summary>
    protected abstract bool Submit();

    /// <summary>Steps the value from <paramref name="text"/> when <paramref name="edited"/> and it reads, else from the
    /// value; false when the field doesn't step.</summary>
    protected virtual bool Step(
        ReadOnlySpan<char> text,
        bool edited,
        int direction,
        bool tenfold
    ) => false;

    /// <summary>Called by an effect as the focus comes into the field or leaves it.</summary>
    internal void FocusMoved(bool focused)
    {
        if (_focused == focused)
            return;
        _focused = focused;
        if (focused)
        {
            _hasTyped = false;
            FocusArrived();
            return;
        }
        if (!ReadOnly)
            SettleText();
        FocusLeft();
    }

    protected virtual void FocusArrived() { }

    protected virtual void FocusLeft() { }

    internal void SettleText(bool caretToEnd = false)
    {
        StopEcho();
        var text = Draft.Span;
        Settle(text, Edited(text));
        Take(caretToEnd ? Index.End : null);
    }

    void Typed()
    {
        var text = Draft.Span;
        Read(text, text.Length - Text.EditedSelection().Caret);
    }

    internal ObservableText Sync(Ui ui)
    {
        var shown = Shown(ui).Span;
        var changed = _syncedLength < 0 || !shown.SequenceEqual(_synced.AsSpan(0, _syncedLength));
        if (changed)
        {
            ValueList.EnsureCapacity(ref _synced, shown.Length);
            shown.CopyTo(_synced);
            _syncedLength = shown.Length;
        }
        if (_take)
        {
            _take = false;
            if (_takeCaret is { } caret)
                Draft.Set(shown, caret);
            else
                Draft.Set(shown);
            _takeCaret = null;
        }
        else if (changed)
            Draft.Set(shown);
        return Draft;
    }

    protected void Take(Index? caret)
    {
        _take = true;
        _takeCaret = caret;
        Rewrites.Value++;
    }

    protected bool Edited(ReadOnlySpan<char> text) =>
        !text.SequenceEqual(_buffer.AsSpan(0, _shown));

    TextValue Shown(Ui ui)
    {
        // Tracked, so a rewrite or a settle renders the field again.
        _ = Rewrites.Value;
        if (_echoing && Echoed)
            return ui.Format($"{_buffer.AsSpan(0, _shown)}");
        _echoing = false;
        return Show(ui);
    }

    protected abstract bool Echoed { get; }

    protected abstract TextValue Show(Ui ui);

    protected void Echo(ReadOnlySpan<char> typed)
    {
        _echoing = true;
        if (_buffer.Length < typed.Length)
            _buffer = new char[Math.Max(typed.Length, _buffer.Length * 2)];
        typed.CopyTo(_buffer);
        _shown = typed.Length;
    }

    protected void StopEcho()
    {
        if (!_echoing)
            return;
        _echoing = false;
        Rewrites.Value++;
    }

    protected void EchoGrouped<T>(
        T value,
        ReadOnlySpan<char> typed,
        int trailing,
        bool blank = false
    )
        where T : ISpanFormattable
    {
        _echoing = true;
        _shown = blank ? 0 : Format(value, NumberText.Grouped);
        if (typed.SequenceEqual(_buffer.AsSpan(0, _shown)))
            return;
        // The take reads what the field shows, which holds the regrouped text only once it renders again.
        Take(^trailing);
    }

    protected TextValue Empty()
    {
        _shown = 0;
        return "";
    }

    /// <summary>The field's text for <paramref name="value"/> in <paramref name="format"/>, through a buffer that grows
    /// to hold it.</summary>
    protected TextValue Write<T>(Ui ui, T value, scoped ReadOnlySpan<char> format)
        where T : ISpanFormattable
    {
        _shown = Format(value, format);
        return ui.Format($"{_buffer.AsSpan(0, _shown)}");
    }

    int Format<T>(T value, scoped ReadOnlySpan<char> format)
        where T : ISpanFormattable
    {
        int written;
        while (!value.TryFormat(_buffer, out written, format, Culture))
            _buffer = new char[_buffer.Length * 2];
        return written;
    }

    void KeyDown(ref KeyEvent e)
    {
        if (
            (e.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Gui)) != 0
            || ReadOnly
        )
            return;
        if (e.Key == Key.Enter)
        {
            SettleText(caretToEnd: true);
            if (Submit())
                Take(ref e);
            return;
        }
        if (e.Key is not (Key.Up or Key.Down))
            return;
        StopEcho();
        var tenfold = (e.Modifiers & ModifierKeys.Shift) != 0;
        var text = Draft.Span;
        if (Step(text, Edited(text), e.Key == Key.Up ? 1 : -1, tenfold))
        {
            Take(Index.End);
            Take(ref e);
        }
    }

    static void Take(ref KeyEvent e)
    {
        e.PreventDefault();
        e.StopPropagation();
    }

    void Pad(NumberPadKeyKind key)
    {
        if (key == NumberPadKeyKind.Done)
        {
            Text.Node?.View?.ClearFocus();
            return;
        }
        if (ReadOnly)
            return;
        StopEcho();
        var from =
            key is NumberPadKeyKind.Down or NumberPadKeyKind.Up || !_hasTyped ? PadValue : _typed;
        _typed = NumberPad.Apply(key, from, PadMax, Math.Max(1, PadStep));
        _hasTyped = true;
        ReportPad(_typed);
        Take(Index.End);
    }

    [FramePath]
    internal static T Clamp<T>(T value, T min, T max)
        where T : INumber<T> => T.Max(min, T.Min(value, max));
}

internal readonly record struct NumberStepperProps
{
    public bool Up { get; init; }

    public bool Disabled { get; init; }

    public Callback OnStep { get; init; }

    public Handler<PointerEvent> OnPress { get; init; }
}

/// <summary>A number field's − or + button: a ghost secondary icon button at the extra-small rung that never takes
/// the focus, disabled once the value reaches its bound.</summary>
internal sealed class NumberStepper : Component<NumberStepperProps>
{
    protected override void Render(Ui ui, in NumberStepperProps p)
    {
        var t = ui.Theme;
        var m = t.Metrics(ComponentSize.Xs);
        var off = ui.IsDisabled;
        var look = ui.Animated(
            t.Palette(ColorScheme.Secondary, ComponentVariant.Ghost, ui.ComponentState)
        );
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                width: m.Height,
                height: m.Height,
                background: look.Background,
                border: new Border(1f, look.Border),
                radius: t.Radius(RadiusToken.Md),
                opacity: off ? 0.5f : null,
                cursor: Cursors.Hand,
                focusable: false,
                onClick: p.OnStep,
                onPointerDown: p.OnPress
            )
        )
            ui.Icon(
                glyph: IconGlyph.Of(p.Up ? IconRole.Plus : IconRole.Minus),
                size: m.IconSize,
                color: look.Foreground
            );
    }
}

[FramePath]
internal static class NumberText
{
    internal const string Grouped = "N0";

    internal static bool TryParse(
        ReadOnlySpan<char> text,
        bool integer,
        string? format,
        out double result
    )
    {
        var culture = CultureInfo.CurrentCulture;
        if (format is null)
        {
            var style = integer
                ? NumberStyles.Integer | NumberStyles.AllowThousands
                : NumberStyles.Number;
            return double.TryParse(text, style, culture, out result) && double.IsFinite(result);
        }
        Span<char> sample = stackalloc char[NumberFieldModel.MaxChars];
        if (1.0.TryFormat(sample, out var written, format, culture))
            text = WithoutAffixes(text, sample[..written]);
        if (
            !double.TryParse(
                text,
                integer
                    ? NumberStyles.Integer | NumberStyles.AllowThousands
                    : NumberStyles.Any & ~NumberStyles.AllowExponent,
                culture,
                out result
            ) || !double.IsFinite(result)
        )
            return false;
        result /= Scale(format);
        return true;
    }

    static ReadOnlySpan<char> WithoutAffixes(
        ReadOnlySpan<char> text,
        scoped ReadOnlySpan<char> sample
    )
    {
        var first = 0;
        while (first < sample.Length && !char.IsAsciiDigit(sample[first]))
            first++;
        if (first == sample.Length)
            return text;
        var last = sample.Length - 1;
        while (!char.IsAsciiDigit(sample[last]))
            last--;
        var prefix = sample[..first].Trim();
        var suffix = sample[(last + 1)..].Trim();
        text = text.Trim();
        if (!prefix.IsEmpty && text.StartsWith(prefix))
            text = text[prefix.Length..];
        if (!suffix.IsEmpty && text.EndsWith(suffix))
            text = text[..^suffix.Length];
        return text;
    }

    static double Scale(ReadOnlySpan<char> format)
    {
        if (format.Length > 0 && char.IsAsciiLetter(format[0]) && AllDigits(format[1..]))
            return format[0] is 'P' or 'p' ? 100.0 : 1.0;
        var scale = 1.0;
        var quote = '\0';
        for (var i = 0; i < format.Length; i++)
        {
            var c = format[i];
            if (quote != '\0')
            {
                if (c == quote)
                    quote = '\0';
                continue;
            }
            switch (c)
            {
                case '\\':
                    i++;
                    break;
                case '\'' or '"':
                    quote = c;
                    break;
                case ';':
                    return scale;
                case '%':
                    scale *= 100.0;
                    break;
                case '‰':
                    scale *= 1000.0;
                    break;
            }
        }
        return scale;
    }

    static bool AllDigits(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (!char.IsAsciiDigit(c))
                return false;
        }
        return true;
    }
}

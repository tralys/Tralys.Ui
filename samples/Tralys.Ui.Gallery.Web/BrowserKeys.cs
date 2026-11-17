namespace Tralys.Ui.Gallery.Web;

static class BrowserKeys
{
    const string LetterCode = "Key";
    const string DigitCode = "Digit";
    const string NumPadCode = "Numpad";
    const string FunctionCode = "F";

    // A letter goes by what the layout types, so a shortcut follows it, and any other key by where it sits.
    public static Key ToKey(string code, string name)
    {
        if (name.Length == 1 && char.IsAsciiLetter(name[0]))
            return Key.A + (char.ToUpperInvariant(name[0]) - 'A');
        if (
            code.Length == LetterCode.Length + 1
            && code.StartsWith(LetterCode, StringComparison.Ordinal)
        )
            return Key.A + (code[^1] - 'A');
        if (
            code.Length == DigitCode.Length + 1
            && code.StartsWith(DigitCode, StringComparison.Ordinal)
        )
            return Key.D0 + (code[^1] - '0');
        if (
            code.Length == NumPadCode.Length + 1
            && code.StartsWith(NumPadCode, StringComparison.Ordinal)
        )
            return Key.NumPad0 + (code[^1] - '0');
        if (
            code.StartsWith(FunctionCode, StringComparison.Ordinal)
            && int.TryParse(code.AsSpan(FunctionCode.Length), out var function)
            && function is >= 1 and <= 24
        )
            return Key.F1 + (function - 1);

        return code switch
        {
            "Backspace" => Key.BackSpace,
            "Tab" => Key.Tab,
            "Enter" or "NumpadEnter" => Key.Enter,
            "Pause" => Key.Pause,
            "CapsLock" => Key.CapsLock,
            "Escape" => Key.Escape,
            "Space" => Key.Space,
            "PageUp" => Key.PageUp,
            "PageDown" => Key.PageDown,
            "End" => Key.End,
            "Home" => Key.Home,
            "ArrowLeft" => Key.Left,
            "ArrowUp" => Key.Up,
            "ArrowRight" => Key.Right,
            "ArrowDown" => Key.Down,
            "PrintScreen" => Key.PrintScreen,
            "Insert" => Key.Insert,
            "Delete" => Key.Delete,
            "MetaLeft" => Key.WindowsLeft,
            "MetaRight" => Key.WindowsRight,
            "ContextMenu" => Key.Menu,
            "NumpadMultiply" => Key.Multiply,
            "NumpadAdd" => Key.Add,
            "NumpadSubtract" => Key.Subtract,
            "NumpadDecimal" => Key.Decimal,
            "NumpadDivide" => Key.Divide,
            "NumLock" => Key.NumLock,
            "ScrollLock" => Key.ScrollLock,
            "ShiftLeft" => Key.ShiftLeft,
            "ShiftRight" => Key.ShiftRight,
            "ControlLeft" => Key.ControlLeft,
            "ControlRight" => Key.ControlRight,
            "AltLeft" => Key.AltLeft,
            "AltRight" => Key.AltRight,
            "Semicolon" => Key.OemSemicolon,
            "Equal" => Key.OemPlus,
            "Comma" => Key.OemComma,
            "Minus" => Key.OemMinus,
            "Period" => Key.OemPeriod,
            "Slash" => Key.OemQuestion,
            "Backquote" => Key.OemTilde,
            "BracketLeft" => Key.OemBracketLeft,
            "Backslash" => Key.OemPipe,
            "BracketRight" => Key.OemBracketRight,
            "Quote" => Key.OemQuotes,
            _ => Key.None,
        };
    }
}

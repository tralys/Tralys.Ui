using SDL;
using static SDL.SDL_Keycode;

namespace Tralys.Ui.Demo;

static partial class SdlInputMap
{
    [FramePath]
    public static ModifierKeys ToModifiers(SDL_Keymod mod)
    {
        var modifiers = ModifierKeys.None;
        if ((mod & SDL_Keymod.SDL_KMOD_SHIFT) != 0)
            modifiers |= ModifierKeys.Shift;
        if ((mod & SDL_Keymod.SDL_KMOD_CTRL) != 0)
            modifiers |= ModifierKeys.Control;
        if ((mod & SDL_Keymod.SDL_KMOD_ALT) != 0)
            modifiers |= ModifierKeys.Alt;
        if ((mod & SDL_Keymod.SDL_KMOD_GUI) != 0)
            modifiers |= ModifierKeys.Gui;
        return modifiers;
    }

    [FramePath]
    public static GamepadButton? ToGamepadButton(SDL_GamepadButton button) =>
        button switch
        {
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH => GamepadButton.South,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST => GamepadButton.East,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST => GamepadButton.West,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH => GamepadButton.North,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK => GamepadButton.Back,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE => GamepadButton.Guide,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START => GamepadButton.Start,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK => GamepadButton.LeftStick,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK => GamepadButton.RightStick,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER => GamepadButton.LeftShoulder,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER => GamepadButton.RightShoulder,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP => GamepadButton.DPadUp,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN => GamepadButton.DPadDown,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT => GamepadButton.DPadLeft,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT => GamepadButton.DPadRight,
            _ => null,
        };

    [FramePath]
    public static MouseButton? ToButton(byte button) =>
        button switch
        {
            1 => MouseButton.Left,
            2 => MouseButton.Middle,
            3 => MouseButton.Right,
            4 => MouseButton.Button1,
            5 => MouseButton.Button2,
            _ => null,
        };

    [FramePath]
    public static Key ToKey(SDL_Keycode key) =>
        key switch
        {
            SDLK_BACKSPACE => Tralys.Ui.Key.BackSpace,
            SDLK_TAB => Tralys.Ui.Key.Tab,
            SDLK_RETURN or SDLK_KP_ENTER => Tralys.Ui.Key.Enter,
            SDLK_PAUSE => Tralys.Ui.Key.Pause,
            SDLK_CAPSLOCK => Tralys.Ui.Key.CapsLock,
            SDLK_ESCAPE => Tralys.Ui.Key.Escape,
            SDLK_SPACE => Tralys.Ui.Key.Space,
            SDLK_PAGEUP => Tralys.Ui.Key.PageUp,
            SDLK_PAGEDOWN => Tralys.Ui.Key.PageDown,
            SDLK_END => Tralys.Ui.Key.End,
            SDLK_HOME => Tralys.Ui.Key.Home,
            SDLK_LEFT => Tralys.Ui.Key.Left,
            SDLK_UP => Tralys.Ui.Key.Up,
            SDLK_RIGHT => Tralys.Ui.Key.Right,
            SDLK_DOWN => Tralys.Ui.Key.Down,
            SDLK_PRINTSCREEN => Tralys.Ui.Key.PrintScreen,
            SDLK_INSERT => Tralys.Ui.Key.Insert,
            SDLK_DELETE => Tralys.Ui.Key.Delete,
            SDLK_0 => Tralys.Ui.Key.D0,
            SDLK_1 => Tralys.Ui.Key.D1,
            SDLK_2 => Tralys.Ui.Key.D2,
            SDLK_3 => Tralys.Ui.Key.D3,
            SDLK_4 => Tralys.Ui.Key.D4,
            SDLK_5 => Tralys.Ui.Key.D5,
            SDLK_6 => Tralys.Ui.Key.D6,
            SDLK_7 => Tralys.Ui.Key.D7,
            SDLK_8 => Tralys.Ui.Key.D8,
            SDLK_9 => Tralys.Ui.Key.D9,
            SDLK_A => Tralys.Ui.Key.A,
            SDLK_B => Tralys.Ui.Key.B,
            SDLK_C => Tralys.Ui.Key.C,
            SDLK_D => Tralys.Ui.Key.D,
            SDLK_E => Tralys.Ui.Key.E,
            SDLK_F => Tralys.Ui.Key.F,
            SDLK_G => Tralys.Ui.Key.G,
            SDLK_H => Tralys.Ui.Key.H,
            SDLK_I => Tralys.Ui.Key.I,
            SDLK_J => Tralys.Ui.Key.J,
            SDLK_K => Tralys.Ui.Key.K,
            SDLK_L => Tralys.Ui.Key.L,
            SDLK_M => Tralys.Ui.Key.M,
            SDLK_N => Tralys.Ui.Key.N,
            SDLK_O => Tralys.Ui.Key.O,
            SDLK_P => Tralys.Ui.Key.P,
            SDLK_Q => Tralys.Ui.Key.Q,
            SDLK_R => Tralys.Ui.Key.R,
            SDLK_S => Tralys.Ui.Key.S,
            SDLK_T => Tralys.Ui.Key.T,
            SDLK_U => Tralys.Ui.Key.U,
            SDLK_V => Tralys.Ui.Key.V,
            SDLK_W => Tralys.Ui.Key.W,
            SDLK_X => Tralys.Ui.Key.X,
            SDLK_Y => Tralys.Ui.Key.Y,
            SDLK_Z => Tralys.Ui.Key.Z,
            SDLK_LGUI => Tralys.Ui.Key.WindowsLeft,
            SDLK_RGUI => Tralys.Ui.Key.WindowsRight,
            SDLK_APPLICATION or SDLK_MENU => Tralys.Ui.Key.Menu,
            SDLK_KP_0 => Tralys.Ui.Key.NumPad0,
            SDLK_KP_1 => Tralys.Ui.Key.NumPad1,
            SDLK_KP_2 => Tralys.Ui.Key.NumPad2,
            SDLK_KP_3 => Tralys.Ui.Key.NumPad3,
            SDLK_KP_4 => Tralys.Ui.Key.NumPad4,
            SDLK_KP_5 => Tralys.Ui.Key.NumPad5,
            SDLK_KP_6 => Tralys.Ui.Key.NumPad6,
            SDLK_KP_7 => Tralys.Ui.Key.NumPad7,
            SDLK_KP_8 => Tralys.Ui.Key.NumPad8,
            SDLK_KP_9 => Tralys.Ui.Key.NumPad9,
            SDLK_KP_MULTIPLY => Tralys.Ui.Key.Multiply,
            SDLK_KP_PLUS => Tralys.Ui.Key.Add,
            SDLK_KP_MINUS => Tralys.Ui.Key.Subtract,
            SDLK_KP_PERIOD => Tralys.Ui.Key.Decimal,
            SDLK_KP_DIVIDE => Tralys.Ui.Key.Divide,
            SDLK_F1 => Tralys.Ui.Key.F1,
            SDLK_F2 => Tralys.Ui.Key.F2,
            SDLK_F3 => Tralys.Ui.Key.F3,
            SDLK_F4 => Tralys.Ui.Key.F4,
            SDLK_F5 => Tralys.Ui.Key.F5,
            SDLK_F6 => Tralys.Ui.Key.F6,
            SDLK_F7 => Tralys.Ui.Key.F7,
            SDLK_F8 => Tralys.Ui.Key.F8,
            SDLK_F9 => Tralys.Ui.Key.F9,
            SDLK_F10 => Tralys.Ui.Key.F10,
            SDLK_F11 => Tralys.Ui.Key.F11,
            SDLK_F12 => Tralys.Ui.Key.F12,
            SDLK_F13 => Tralys.Ui.Key.F13,
            SDLK_F14 => Tralys.Ui.Key.F14,
            SDLK_F15 => Tralys.Ui.Key.F15,
            SDLK_F16 => Tralys.Ui.Key.F16,
            SDLK_F17 => Tralys.Ui.Key.F17,
            SDLK_F18 => Tralys.Ui.Key.F18,
            SDLK_F19 => Tralys.Ui.Key.F19,
            SDLK_F20 => Tralys.Ui.Key.F20,
            SDLK_F21 => Tralys.Ui.Key.F21,
            SDLK_F22 => Tralys.Ui.Key.F22,
            SDLK_F23 => Tralys.Ui.Key.F23,
            SDLK_F24 => Tralys.Ui.Key.F24,
            SDLK_NUMLOCKCLEAR => Tralys.Ui.Key.NumLock,
            SDLK_SCROLLLOCK => Tralys.Ui.Key.ScrollLock,
            SDLK_LSHIFT => Tralys.Ui.Key.ShiftLeft,
            SDLK_RSHIFT => Tralys.Ui.Key.ShiftRight,
            SDLK_LCTRL => Tralys.Ui.Key.ControlLeft,
            SDLK_RCTRL => Tralys.Ui.Key.ControlRight,
            SDLK_LALT => Tralys.Ui.Key.AltLeft,
            SDLK_RALT => Tralys.Ui.Key.AltRight,
            SDLK_SEMICOLON => Tralys.Ui.Key.OemSemicolon,
            SDLK_EQUALS => Tralys.Ui.Key.OemPlus,
            SDLK_COMMA => Tralys.Ui.Key.OemComma,
            SDLK_MINUS => Tralys.Ui.Key.OemMinus,
            SDLK_PERIOD => Tralys.Ui.Key.OemPeriod,
            SDLK_SLASH => Tralys.Ui.Key.OemQuestion,
            SDLK_GRAVE => Tralys.Ui.Key.OemTilde,
            SDLK_LEFTBRACKET => Tralys.Ui.Key.OemBracketLeft,
            SDLK_BACKSLASH => Tralys.Ui.Key.OemPipe,
            SDLK_RIGHTBRACKET => Tralys.Ui.Key.OemBracketRight,
            SDLK_APOSTROPHE => Tralys.Ui.Key.OemQuotes,
            _ => Tralys.Ui.Key.None,
        };
}

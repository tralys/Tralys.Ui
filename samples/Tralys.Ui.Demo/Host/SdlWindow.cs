using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Bgfx.Net;
using SDL;
using static SDL.SDL_EventType;
using static SDL.SDL3;

namespace Tralys.Ui.Demo;

sealed unsafe class SdlWindow : IDisposable
{
    static readonly List<SdlWindow> Opened = [];

    readonly SDL_Cursor*[] _cursors = new SDL_Cursor*[(int)CursorType.Custom];
    SDL_Window* _window;
    SDL_WindowID _id;
    char[] _text = new char[32];
    float _pixelDensity = 1f;
    int _typingViews;
    bool _resized;
    bool _ownsVideo = true;
    bool _disposed;

    SdlWindow() { }

    public int PixelWidth { get; private set; }

    public int PixelHeight { get; private set; }

    public float DisplayScale { get; private set; } = 1f;

    public float PixelDensity => _pixelDensity;

    public bool CloseRequested { get; private set; }

    public static SdlWindow Open(string title, int width, int height)
    {
        Start(null);
        var window = SDL_CreateWindow(
            title,
            width,
            height,
            SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY
        );
        if (window is null)
        {
            var error = SDL_GetError();
            SDL_Quit();
            throw new DemoException($"SDL could not open a window: {error}");
        }

        var host = new SdlWindow { _window = window, _id = SDL_GetWindowID(window) };
        host.Refresh();
        Opened.Add(host);
        return host;
    }

    /// <summary>Another window beside the first, borderless, with its top-left at (<paramref name="x"/>,
    /// <paramref name="y"/>) on the desktop, in points.</summary>
    public static SdlWindow OpenChild(string title, int x, int y, int width, int height)
    {
        var window = SDL_CreateWindow(
            title,
            Math.Max(1, width),
            Math.Max(1, height),
            SDL_WindowFlags.SDL_WINDOW_RESIZABLE
                | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY
                | SDL_WindowFlags.SDL_WINDOW_BORDERLESS
        );
        if (window is null)
            throw new DemoException($"SDL could not open a window: {SDL_GetError()}");

        SDL_SetWindowPosition(window, x, y);
        var host = new SdlWindow
        {
            _window = window,
            _id = SDL_GetWindowID(window),
            _ownsVideo = false,
        };
        host.Refresh();
        Opened.Add(host);
        return host;
    }

    /// <summary>The window on the desktop, in points.</summary>
    public Rect Bounds
    {
        get
        {
            int x = 0,
                y = 0,
                width = 0,
                height = 0;
            if (_window is not null)
            {
                SDL_GetWindowPosition(_window, &x, &y);
                SDL_GetWindowSize(_window, &width, &height);
            }
            return new Rect(x, y, width, height);
        }
    }

    public void MoveTo(Vector2 position)
    {
        if (_window is not null)
            SDL_SetWindowPosition(_window, (int)position.X, (int)position.Y);
    }

    /// <summary>The pointer on the desktop, in points, wherever it is.</summary>
    public static Vector2 GlobalCursor
    {
        get
        {
            float x,
                y;
            SDL_GetGlobalMouseState(&x, &y);
            return new Vector2(x, y);
        }
    }

    public static SdlWindow OpenHeadless(int pixelWidth, int pixelHeight)
    {
        Start("dummy");
        return new SdlWindow { PixelWidth = pixelWidth, PixelHeight = pixelHeight };
    }

    static void Start(string? videoDriver)
    {
        if (videoDriver is not null)
            SDL_SetHint(SDL_HINT_VIDEO_DRIVER, videoDriver);
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO | SDL_InitFlags.SDL_INIT_GAMEPAD))
            throw new DemoException($"SDL could not start its video subsystem: {SDL_GetError()}");
    }

    // A headless window has no points, so it takes the size as pixels.
    public void SetSize(int width, int height)
    {
        if (_window is null)
        {
            PixelWidth = width;
            PixelHeight = height;
            return;
        }

        SDL_SetWindowSize(_window, width, height);
        Refresh();
    }

    public bool IsMaximizedOrFullscreen =>
        _window is not null
        && (
            SDL_GetWindowFlags(_window)
            & (SDL_WindowFlags.SDL_WINDOW_MAXIMIZED | SDL_WindowFlags.SDL_WINDOW_FULLSCREEN)
        ) != 0;

    public string? VideoDriver => SDL_GetCurrentVideoDriver();

    public void SetTitle(string title)
    {
        if (_window is not null)
            SDL_SetWindowTitle(_window, title);
    }

    public (nint Window, nint Display, NativeWindowHandleType Type) Surface()
    {
        var props = SDL_GetWindowProperties(_window);
        nint window;
        nint display = 0;
        var type = NativeWindowHandleType.Default;
        if (OperatingSystem.IsWindows())
            window = SDL_GetPointerProperty(props, SDL_PROP_WINDOW_WIN32_HWND_POINTER, 0);
        else if (OperatingSystem.IsMacOS())
            window = SDL_GetPointerProperty(props, SDL_PROP_WINDOW_COCOA_WINDOW_POINTER, 0);
        else if (
            SDL_GetPointerProperty(props, SDL_PROP_WINDOW_WAYLAND_SURFACE_POINTER, 0)
            is var surface
                and not 0
        )
        {
            window = surface;
            display = SDL_GetPointerProperty(props, SDL_PROP_WINDOW_WAYLAND_DISPLAY_POINTER, 0);
            type = NativeWindowHandleType.Wayland;
        }
        else
        {
            window = (nint)SDL_GetNumberProperty(props, SDL_PROP_WINDOW_X11_WINDOW_NUMBER, 0);
            display = SDL_GetPointerProperty(props, SDL_PROP_WINDOW_X11_DISPLAY_POINTER, 0);
        }

        if (window == 0)
            throw NoNativeWindow(VideoDriver);
        return (window, display, type);
    }

    static DemoException NoNativeWindow(string? driver) =>
        new($"SDL's {driver} video driver gives bgfx no native window to draw into.");

    [FramePath]
    public void SetCursor(Cursor? cursor)
    {
        if (_window is null)
            return;

        var type = cursor is { Type: not CursorType.Custom and var shown }
            ? shown
            : CursorType.Arrow;
        ref var made = ref _cursors[(int)type];
        if (made is null)
            made = SDL_CreateSystemCursor(SystemCursor(type));
        SDL_SetCursor(made);
    }

    static SDL_SystemCursor SystemCursor(CursorType type) =>
        type switch
        {
            CursorType.IBeam => SDL_SystemCursor.SDL_SYSTEM_CURSOR_TEXT,
            CursorType.Wait => SDL_SystemCursor.SDL_SYSTEM_CURSOR_WAIT,
            CursorType.Crosshair => SDL_SystemCursor.SDL_SYSTEM_CURSOR_CROSSHAIR,
            CursorType.WaitArrow => SDL_SystemCursor.SDL_SYSTEM_CURSOR_PROGRESS,
            CursorType.SizeNwSe => SDL_SystemCursor.SDL_SYSTEM_CURSOR_NWSE_RESIZE,
            CursorType.SizeNeSw => SDL_SystemCursor.SDL_SYSTEM_CURSOR_NESW_RESIZE,
            CursorType.SizeWe => SDL_SystemCursor.SDL_SYSTEM_CURSOR_EW_RESIZE,
            CursorType.SizeNs => SDL_SystemCursor.SDL_SYSTEM_CURSOR_NS_RESIZE,
            CursorType.SizeAll or CursorType.ScrollAll => SDL_SystemCursor.SDL_SYSTEM_CURSOR_MOVE,
            CursorType.No => SDL_SystemCursor.SDL_SYSTEM_CURSOR_NOT_ALLOWED,
            CursorType.Hand => SDL_SystemCursor.SDL_SYSTEM_CURSOR_POINTER,
            _ => SDL_SystemCursor.SDL_SYSTEM_CURSOR_DEFAULT,
        };

    public string? GetClipboardText() => _window is null ? null : SDL_GetClipboardText();

    public void SetClipboardText(ReadOnlySpan<char> text)
    {
        if (_window is not null)
            SDL_SetClipboardText(text.ToString());
    }

    // Each view's host counts once, so one view's field losing focus leaves another's typing.
    public void StartTextInput(TextInputKind kind, Rect area, bool viewWasTyping)
    {
        if (_window is null)
            return;

        if (!viewWasTyping)
            _typingViews++;
        MoveTextInput(area);
        var props = SDL_CreateProperties();
        SDL_SetNumberProperty(props, SDL_PROP_TEXTINPUT_TYPE_NUMBER, (long)TextInputType(kind));
        SDL_SetBooleanProperty(
            props,
            SDL_PROP_TEXTINPUT_AUTOCORRECT_BOOLEAN,
            kind == TextInputKind.Text
        );
        SDL_StartTextInputWithProperties(_window, props);
        SDL_DestroyProperties(props);
    }

    public void MoveTextInput(Rect area)
    {
        if (_window is null)
            return;

        var rect = new SDL_Rect
        {
            x = (int)(area.X / _pixelDensity),
            y = (int)(area.Y / _pixelDensity),
            w = (int)MathF.Ceiling(area.Width / _pixelDensity),
            h = (int)MathF.Ceiling(area.Height / _pixelDensity),
        };
        SDL_SetTextInputArea(_window, &rect, 0);
    }

    public void StopTextInput()
    {
        if (_window is not null && --_typingViews == 0)
            SDL_StopTextInput(_window);
    }

    static SDL_TextInputType TextInputType(TextInputKind kind) =>
        kind switch
        {
            TextInputKind.Name => SDL_TextInputType.SDL_TEXTINPUT_TYPE_TEXT_NAME,
            TextInputKind.Number => SDL_TextInputType.SDL_TEXTINPUT_TYPE_NUMBER,
            TextInputKind.Password => SDL_TextInputType.SDL_TEXTINPUT_TYPE_TEXT_PASSWORD_HIDDEN,
            _ => SDL_TextInputType.SDL_TEXTINPUT_TYPE_TEXT,
        };

    [FramePath]
    public bool TakeResize()
    {
        var resized = _resized;
        _resized = false;
        return resized;
    }

    /// <summary>Takes the next event of any open window, translated by the window it came to, which
    /// <paramref name="from"/> names; an event of no window comes to this one, and one of a closed window is
    /// dropped.</summary>
    [FramePath]
    public bool Poll(out InputEvent input, out SdlWindow from)
    {
        SDL_Event e;
        while (SDL_PollEvent(&e))
        {
            var id = WindowOf(&e);
            if ((id == default ? this : Of(id)) is not { } target)
                continue;

            from = target;
            if (from.Translate(&e, out input))
                return true;
        }

        input = default;
        from = this;
        return false;
    }

    [FramePath]
    static SdlWindow? Of(SDL_WindowID id)
    {
        foreach (var window in Opened)
        {
            if (window._id == id)
                return window;
        }
        return null;
    }

    [FramePath]
    static SDL_WindowID WindowOf(SDL_Event* e) =>
        e->Type switch
        {
            >= SDL_EVENT_WINDOW_FIRST and <= SDL_EVENT_WINDOW_LAST => e->window.windowID,
            SDL_EVENT_MOUSE_MOTION => e->motion.windowID,
            SDL_EVENT_MOUSE_BUTTON_DOWN or SDL_EVENT_MOUSE_BUTTON_UP => e->button.windowID,
            SDL_EVENT_MOUSE_WHEEL => e->wheel.windowID,
            SDL_EVENT_FINGER_DOWN
            or SDL_EVENT_FINGER_MOTION
            or SDL_EVENT_FINGER_UP
            or SDL_EVENT_FINGER_CANCELED => e->tfinger.windowID,
            SDL_EVENT_KEY_DOWN or SDL_EVENT_KEY_UP => e->key.windowID,
            SDL_EVENT_TEXT_INPUT => e->text.windowID,
            _ => default,
        };

    [FramePath]
    bool Translate(SDL_Event* e, out InputEvent input)
    {
        input = default;
        switch (e->Type)
        {
            case SDL_EVENT_QUIT or SDL_EVENT_WINDOW_CLOSE_REQUESTED:
                CloseRequested = true;
                return false;
            case SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED or SDL_EVENT_WINDOW_DISPLAY_SCALE_CHANGED:
                Refresh();
                _resized = true;
                return false;
            case SDL_EVENT_WINDOW_MOUSE_LEAVE:
                input = new InputEvent { Kind = InputKind.PointerLeave };
                return true;
            case SDL_EVENT_WINDOW_FOCUS_LOST:
                input = new InputEvent { Kind = InputKind.FocusLost };
                return true;
            case SDL_EVENT_WINDOW_FOCUS_GAINED:
                input = new InputEvent { Kind = InputKind.FocusGained };
                return true;
            // SDL mirrors each touch as mouse events too, which the finger events already deliver.
            case SDL_EVENT_MOUSE_MOTION when e->motion.which != SDL_TOUCH_MOUSEID:
                input = new InputEvent
                {
                    Kind = InputKind.PointerMove,
                    Position = Pixels(e->motion.x, e->motion.y),
                    Modifiers = HeldModifiers(),
                };
                return true;
            case SDL_EVENT_MOUSE_BUTTON_DOWN
            or SDL_EVENT_MOUSE_BUTTON_UP
                when e->button.which != SDL_TOUCH_MOUSEID
                    && SdlInputMap.ToButton(e->button.button) is { } pressed:
                input = new InputEvent
                {
                    Kind = e->button.down ? InputKind.PointerDown : InputKind.PointerUp,
                    Position = Pixels(e->button.x, e->button.y),
                    Button = pressed,
                    Clicks = e->button.clicks,
                    Modifiers = HeldModifiers(),
                };
                return true;
            case SDL_EVENT_MOUSE_WHEEL:
                var flip =
                    e->wheel.direction == SDL_MouseWheelDirection.SDL_MOUSEWHEEL_FLIPPED ? -1f : 1f;
                input = new InputEvent
                {
                    Kind = InputKind.Wheel,
                    Position = Pixels(e->wheel.mouse_x, e->wheel.mouse_y),
                    Wheel = new Vector2(e->wheel.x, e->wheel.y) * flip,
                    Modifiers = HeldModifiers(),
                };
                return true;
            // SDL mirrors a pen as both mouse and touch events; the mouse events already deliver it.
            case SDL_EVENT_FINGER_DOWN
            or SDL_EVENT_FINGER_MOTION
            or SDL_EVENT_FINGER_UP
            or SDL_EVENT_FINGER_CANCELED when e->tfinger.touchID != SDL_PEN_TOUCHID:
                input = new InputEvent
                {
                    Kind = e->Type switch
                    {
                        SDL_EVENT_FINGER_DOWN => InputKind.TouchDown,
                        SDL_EVENT_FINGER_MOTION => InputKind.TouchMove,
                        SDL_EVENT_FINGER_UP => InputKind.TouchUp,
                        _ => InputKind.TouchCancel,
                    },
                    Position = new Vector2(
                        FingerPixel(e->tfinger.x, PixelWidth),
                        FingerPixel(e->tfinger.y, PixelHeight)
                    ),
                    Finger = (ulong)e->tfinger.fingerID,
                    Modifiers = HeldModifiers(),
                };
                return true;
            case SDL_EVENT_KEY_DOWN
            or SDL_EVENT_KEY_UP when SdlInputMap.ToKey(e->key.key) is not Key.None and var key:
                input = new InputEvent
                {
                    Kind = e->key.down ? InputKind.KeyDown : InputKind.KeyUp,
                    Key = key,
                    Modifiers = SdlInputMap.ToModifiers(e->key.mod),
                    Repeat = e->key.repeat,
                };
                return true;
            case SDL_EVENT_TEXT_INPUT:
                input = new InputEvent { Kind = InputKind.Text, Text = Decode(e->text.text) };
                return true;
            case SDL_EVENT_GAMEPAD_ADDED:
                SDL_OpenGamepad(e->gdevice.which);
                return false;
            case SDL_EVENT_GAMEPAD_REMOVED:
                SDL_CloseGamepad(SDL_GetGamepadFromID(e->gdevice.which));
                Pad.Forget(e->gdevice.which);
                return false;
            case SDL_EVENT_GAMEPAD_BUTTON_DOWN
            or SDL_EVENT_GAMEPAD_BUTTON_UP
                when SdlInputMap.ToGamepadButton((SDL_GamepadButton)e->gbutton.button)
                    is { } button:
                input = new InputEvent
                {
                    Kind = e->gbutton.down
                        ? InputKind.GamepadButtonDown
                        : InputKind.GamepadButtonUp,
                    PadButton = button,
                    PadStyle = StyleOf(e->gbutton.which),
                };
                return true;
            case SDL_EVENT_GAMEPAD_AXIS_MOTION:
                return Pad.Axis(
                    e->gaxis.which,
                    (SDL_GamepadAxis)e->gaxis.axis,
                    e->gaxis.value,
                    out input
                );
            default:
                return false;
        }
    }

    [FramePath]
    static GamepadStyle StyleOf(SDL_JoystickID id)
    {
        var gamepad = SDL_GetGamepadFromID(id);
        if (gamepad is null)
            return GamepadStyle.Generic;

        return SDL_GetGamepadButtonLabel(gamepad, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH) switch
        {
            SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_CROSS => GamepadStyle.PlayStation,
            SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_B => GamepadStyle.Nintendo,
            _ => SDL_GetGamepadType(gamepad)
                is SDL_GamepadType.SDL_GAMEPAD_TYPE_XBOX360
                    or SDL_GamepadType.SDL_GAMEPAD_TYPE_XBOXONE
                ? GamepadStyle.Xbox
                : GamepadStyle.Generic,
        };
    }

    static class Pad
    {
        const float TriggerDown = 0.5f;
        const float TriggerUp = 0.25f;

        static readonly Dictionary<SDL_JoystickID, State> States = [];

        public static void Forget(SDL_JoystickID id) => States.Remove(id);

        [FramePath]
        public static bool Axis(
            SDL_JoystickID id,
            SDL_GamepadAxis axis,
            short raw,
            out InputEvent input
        )
        {
            input = default;
            var value = Math.Clamp(raw / 32767f, -1f, 1f);
            ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(States, id, out _);
            switch (axis)
            {
                case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX:
                    state.Left.X = value;
                    return Stick(id, GamepadStick.Left, state.Left, out input);
                case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY:
                    state.Left.Y = value;
                    return Stick(id, GamepadStick.Left, state.Left, out input);
                case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTX:
                    state.Right.X = value;
                    return Stick(id, GamepadStick.Right, state.Right, out input);
                case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTY:
                    state.Right.Y = value;
                    return Stick(id, GamepadStick.Right, state.Right, out input);
                case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFT_TRIGGER:
                    return Trigger(
                        id,
                        GamepadButton.LeftTrigger,
                        value,
                        ref state.LeftTrigger,
                        out input
                    );
                case SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHT_TRIGGER:
                    return Trigger(
                        id,
                        GamepadButton.RightTrigger,
                        value,
                        ref state.RightTrigger,
                        out input
                    );
                default:
                    return false;
            }
        }

        static bool Stick(
            SDL_JoystickID id,
            GamepadStick stick,
            Vector2 value,
            out InputEvent input
        )
        {
            input = new InputEvent
            {
                Kind = InputKind.GamepadStick,
                Stick = stick,
                StickValue = value,
                PadStyle = StyleOf(id),
            };
            return true;
        }

        static bool Trigger(
            SDL_JoystickID id,
            GamepadButton button,
            float value,
            ref bool down,
            out InputEvent input
        )
        {
            input = default;
            var pressed = down ? value > TriggerUp : value > TriggerDown;
            if (pressed == down)
                return false;

            down = pressed;
            input = new InputEvent
            {
                Kind = pressed ? InputKind.GamepadButtonDown : InputKind.GamepadButtonUp,
                PadButton = button,
                PadStyle = StyleOf(id),
            };
            return true;
        }

        struct State
        {
            public Vector2 Left;
            public Vector2 Right;
            public bool LeftTrigger;
            public bool RightTrigger;
        }
    }

    [FramePath]
    static ModifierKeys HeldModifiers() => SdlInputMap.ToModifiers(SDL_GetModState());

    [FramePath]
    Vector2 Pixels(float x, float y) => new Vector2(x, y) * _pixelDensity;

    // A finger on the last pixel can report 1, which lands past the half-open view.
    [FramePath]
    static float FingerPixel(float normalized, int pixels) =>
        Math.Clamp(normalized * pixels, 0f, Math.Max(0, pixels - 1));

    [FramePath]
    ReadOnlySpan<char> Decode(byte* text)
    {
        var utf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(text);
        var count = Encoding.UTF8.GetCharCount(utf8);
        if (count > _text.Length)
            _text = new char[Math.Max(count, _text.Length * 2)];
        return _text.AsSpan(0, Encoding.UTF8.GetChars(utf8, _text));
    }

    [FramePath]
    void Refresh()
    {
        int width,
            height;
        if (SDL_GetWindowSizeInPixels(_window, &width, &height))
        {
            PixelWidth = width;
            PixelHeight = height;
        }

        var density = SDL_GetWindowPixelDensity(_window);
        if (density > 0f)
            _pixelDensity = density;
        var scale = SDL_GetWindowDisplayScale(_window);
        if (scale > 0f)
            DisplayScale = scale;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Opened.Remove(this);
        if (_window is not null)
        {
            SDL_StopTextInput(_window);
            SDL_DestroyWindow(_window);
            _window = null;
        }
        foreach (var cursor in _cursors)
        {
            if (cursor is not null)
                SDL_DestroyCursor(cursor);
        }
        if (_ownsVideo)
            SDL_Quit();
    }
}

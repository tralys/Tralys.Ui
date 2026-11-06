using System.Numerics;
using SDL;
using static SDL.SDL3;

namespace Tralys.Ui.Demo.Tests;

static unsafe class SdlEvents
{
    public static void Key(SDL_Keycode key, bool down)
    {
        SDL_Event e = default;
        e.type = (uint)(down ? SDL_EventType.SDL_EVENT_KEY_DOWN : SDL_EventType.SDL_EVENT_KEY_UP);
        e.key.key = key;
        e.key.down = down;
        SDL_PushEvent(&e);
    }

    public static void Text(nint utf8)
    {
        SDL_Event e = default;
        e.type = (uint)SDL_EventType.SDL_EVENT_TEXT_INPUT;
        e.text.text = (byte*)utf8;
        SDL_PushEvent(&e);
    }

    public static void Wheel(Vector2 at)
    {
        SDL_Event e = default;
        e.type = (uint)SDL_EventType.SDL_EVENT_MOUSE_WHEEL;
        e.wheel.y = 1f;
        e.wheel.mouse_x = at.X;
        e.wheel.mouse_y = at.Y;
        SDL_PushEvent(&e);
    }

    public static void Motion(Vector2 at)
    {
        SDL_Event e = default;
        e.type = (uint)SDL_EventType.SDL_EVENT_MOUSE_MOTION;
        e.motion.x = at.X;
        e.motion.y = at.Y;
        SDL_PushEvent(&e);
    }

    public static void FingerDown(Vector2 normalized, SDL_TouchID touch)
    {
        SDL_Event e = default;
        e.type = (uint)SDL_EventType.SDL_EVENT_FINGER_DOWN;
        e.tfinger.touchID = touch;
        e.tfinger.fingerID = (SDL_FingerID)1;
        e.tfinger.x = normalized.X;
        e.tfinger.y = normalized.Y;
        SDL_PushEvent(&e);
    }

    public static void CloseRequested(SDL_WindowID window)
    {
        SDL_Event e = default;
        e.type = (uint)SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED;
        e.window.windowID = window;
        SDL_PushEvent(&e);
    }

    public static void Button(Vector2 at, bool pressed)
    {
        SDL_Event e = default;
        e.type = (uint)(
            pressed
                ? SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN
                : SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP
        );
        e.button.button = 1;
        e.button.down = pressed;
        e.button.x = at.X;
        e.button.y = at.Y;
        SDL_PushEvent(&e);
    }
}

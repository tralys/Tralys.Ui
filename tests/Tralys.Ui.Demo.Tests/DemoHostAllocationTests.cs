using System.Numerics;
using System.Runtime.InteropServices;
using SDL;
using Tralys.Ui.Demo.Workload;
using Tralys.Ui.Testing;
using static SDL.SDL3;

namespace Tralys.Ui.Demo.Tests;

[NotInParallel(nameof(DemoHost))]
public sealed class DemoHostAllocationTests
{
    const float Scale = 1.25f;
    static readonly nint Typed = Marshal.StringToCoTaskMemUTF8("é");

    // Not a span literal: unoptimized, one of a wider-than-byte enum allocates in RuntimeHelpers.CreateSpan.
    static readonly SDL_EventType[] WindowEvents =
    [
        SDL_EventType.SDL_EVENT_WINDOW_MOUSE_LEAVE,
        SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST,
        SDL_EventType.SDL_EVENT_WINDOW_FOCUS_GAINED,
    ];
    static readonly SDL_GamepadButton[] PadButtons =
    [
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH,
    ];

    [Test]
    public async Task The_workload_host_pumping_input_every_frame_allocates_nothing_once_warm_and_its_clicks_reach_every_action()
    {
        using var host = DemoHost.Start(
            new DemoOptions
            {
                Workload = true,
                Headless = true,
                Frames = 1,
                Scale = Scale,
            }
        );
        var frame = 0;

        void Frame()
        {
            frame++;
            PushInput(frame, new Vector2(250f + frame % 510, 660f), pressed: frame % 2 == 0);
            if (!host.Frame())
                throw new InvalidOperationException("The host stopped.");
        }

        // One unbroken window, so a late first-time spike is not forgiven; feed rows warm over many appends.
        var worst = AllocationGate.WorstFrameBytes(Frame, frames: 600, windows: 1, warmup: 900);
        var unclicked = new List<int>();
        foreach (var action in host.Workload!.Model.Actions)
        {
            if (action.Clicks == 0)
                unclicked.Add(action.Number);
        }

        await Assert.That(worst).IsEqualTo(0L);
        await Assert.That(unclicked).IsEmpty();
    }

    [Test]
    public async Task The_workload_host_with_the_inspector_on_a_still_element_and_input_pumped_below_it_every_frame_allocates_nothing_once_warm()
    {
        using var host = DemoHost.Start(
            new DemoOptions
            {
                Workload = true,
                Headless = true,
                Frames = 1,
                Scale = Scale,
                DevTools = true,
            }
        );
        host.Frame();
        var feed = Find(host.View.Root, typeof(WorkloadFeed))!.Parent!;
        host.Inspector!.Selected = feed;
        var frame = 0;

        void Frame()
        {
            frame++;
            PushInput(frame, new Vector2(250f + frame % 510, 660f), pressed: frame % 2 == 0);
            if (!host.Frame())
                throw new InvalidOperationException("The host stopped.");
        }

        var worst = AllocationGate.WorstFrameBytes(Frame, frames: 600, windows: 1, warmup: 900);
        var unclicked = new List<int>();
        foreach (var action in host.Workload!.Model.Actions)
        {
            if (action.Clicks == 0)
                unclicked.Add(action.Number);
        }

        await Assert.That(worst).IsEqualTo(0L);
        await Assert.That(host.Inspector.Selected).IsSameReferenceAs(feed);
        await Assert.That(unclicked).IsEmpty();
    }

    [Test]
    public async Task The_panel_host_forwarding_presses_and_cancels_every_frame_allocates_nothing_once_warm()
    {
        using var host = DemoHost.Start(
            new DemoOptions
            {
                Panel = true,
                Headless = true,
                Frames = 1,
            }
        );
        var view = host.View;
        var frame = 0;

        void Frame()
        {
            frame++;
            var point = new Vector2(640f + frame % 8, 360f);
            var time = view.Context.Now;
            var held = frame % 6 is 1 or 2 or 5 ? MouseButtons.Left : MouseButtons.None;
            view.PointerMove(Pointer.Mouse, point, held, held != 0, ModifierKeys.None, time);
            switch (frame % 6)
            {
                case 0:
                case 4:
                    view.PointerDown(
                        Pointer.Mouse,
                        point,
                        MouseButton.Left,
                        ModifierKeys.None,
                        time
                    );
                    break;
                case 1:
                    view.PointerDown(
                        Pointer.Mouse,
                        point,
                        MouseButton.Right,
                        ModifierKeys.None,
                        time
                    );
                    break;
                case 2:
                    view.PointerUp(
                        Pointer.Mouse,
                        point,
                        MouseButton.Right,
                        ModifierKeys.None,
                        time
                    );
                    break;
                case 3:
                    view.PointerUp(Pointer.Mouse, point, MouseButton.Left, ModifierKeys.None, time);
                    break;
                default:
                    view.PointerCancel(Pointer.Mouse);
                    break;
            }
            if (!host.Frame())
                throw new InvalidOperationException("The host stopped.");
        }

        var worst = AllocationGate.WorstFrameBytes(Frame);

        await Assert.That(worst).IsEqualTo(0L);
    }

    static Node? Find(Node node, Type component)
    {
        foreach (var child in node.Children)
        {
            if (child.Component == component)
                return child;
            if (Find(child, component) is { } found)
                return found;
        }
        return null;
    }

    static unsafe void PushInput(int frame, Vector2 pointer, bool pressed)
    {
        SDL_Event e = default;
        if (pressed)
        {
            foreach (var type in WindowEvents)
            {
                e = default;
                e.type = (uint)type;
                SDL_PushEvent(&e);
            }
        }

        SdlEvents.Key(SDL_Keycode.SDLK_A, down: true);
        SdlEvents.Key(SDL_Keycode.SDLK_A, down: false);
        SdlEvents.Text(Typed);

        PushTouch(frame % 4);

        var button = PadButtons[frame % PadButtons.Length];
        foreach (var down in (ReadOnlySpan<bool>)[true, false])
        {
            e = default;
            e.type = (uint)(
                down
                    ? SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN
                    : SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_UP
            );
            e.gbutton.button = (byte)button;
            e.gbutton.down = down;
            SDL_PushEvent(&e);
        }

        e = default;
        e.type = (uint)SDL_EventType.SDL_EVENT_GAMEPAD_AXIS_MOTION;
        e.gaxis.axis = (byte)SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX;
        e.gaxis.value = (short)(pressed ? 24000 : 0);
        SDL_PushEvent(&e);

        SdlEvents.Wheel(pointer);
        SdlEvents.Motion(pointer);
        SdlEvents.Button(pointer, pressed);
    }

    static unsafe void PushTouch(int step)
    {
        // Clear of the feed: swiped, it hands rows to containers unevenly, which warms far later.
        const float y = 0.92f;
        SDL_Event e = default;
        e.type = (uint)(
            step switch
            {
                0 or 3 => SDL_EventType.SDL_EVENT_FINGER_DOWN,
                1 => SDL_EventType.SDL_EVENT_FINGER_MOTION,
                _ => SDL_EventType.SDL_EVENT_FINGER_UP,
            }
        );
        e.tfinger.fingerID = (SDL_FingerID)1;
        e.tfinger.x = 0.2f;
        e.tfinger.y = step == 0 || step == 3 ? y : y - 0.05f;
        SDL_PushEvent(&e);

        if (step == 3)
        {
            e.type = (uint)SDL_EventType.SDL_EVENT_FINGER_CANCELED;
            SDL_PushEvent(&e);
        }
    }
}

using System.Numerics;
using SDL;
using StbImageSharp;
using Tralys.Ui.DevTools;
using static SDL.SDL3;

namespace Tralys.Ui.Demo.Tests;

[NotInParallel([nameof(DemoHost), nameof(StoryPixels)])]
public sealed class DemoHostTests
{
    [Test]
    public async Task F12_opens_the_inspector_beside_the_view_narrowing_it_by_the_panel_and_again_gives_it_the_whole_window_back()
    {
        using var host = DemoHost.Start(
            new DemoOptions
            {
                Story = "Button.Variants",
                Headless = true,
                Frames = 1,
                Scale = 1f,
            }
        );
        host.Frame();
        var whole = host.View.Size.Width;

        PressF12();
        host.Frame();
        var opened = (host.Inspector is not null, host.View.Size.Width);
        PressF12();
        host.Frame();

        await Assert.That(opened).IsEqualTo((true, whole - Inspector.PanelWidth));
        await Assert.That(host.Inspector).IsNull();
        await Assert.That(host.View.Size.Width).IsEqualTo(whole);
    }

    [Test]
    public async Task A_close_request_for_a_window_not_open_reaches_none_while_one_naming_no_window_closes_the_main_one()
    {
        using var host = DemoHost.Start(
            new DemoOptions
            {
                Story = "Button.Variants",
                Headless = true,
                Frames = 1,
            }
        );
        host.Frame();

        SdlEvents.CloseRequested((SDL_WindowID)uint.MaxValue);
        var running = host.Frame();
        SdlEvents.CloseRequested(default);
        var closed = !host.Frame();

        await Assert.That((running, closed)).IsEqualTo((true, true));
    }

    [Test]
    public async Task A_finger_mirroring_a_pen_is_dropped_as_its_mouse_events_already_deliver_it()
    {
        using var host = DemoHost.Start(
            new DemoOptions
            {
                Story = "Button.Variants",
                Headless = true,
                Frames = 1,
            }
        );
        var middle = new Vector2(0.5f, 0.5f);

        SdlEvents.FingerDown(middle, SDL_PEN_TOUCHID);
        host.Frame();
        var fromPen = host.View.InputDevice;
        SdlEvents.FingerDown(middle, default);
        host.Frame();

        await Assert
            .That((fromPen, host.View.InputDevice))
            .IsEqualTo((InputDevice.KeyboardMouse, InputDevice.Touch));
    }

    [Test]
    public async Task A_headless_screenshot_holds_what_the_view_drew()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"tralys-screenshot-{Environment.ProcessId}",
            "shot"
        );
        try
        {
            using (
                var host = DemoHost.Start(
                    new DemoOptions
                    {
                        Story = "Button.Variants",
                        Headless = true,
                        Frames = 1,
                        Renderer = ReferenceDevice.OfThisOs.Renderer,
                        Vendor = ReferenceDevice.OfThisOs.AskVendor,
                        Device = ReferenceDevice.OfThisOs.Device,
                        Screenshot = path,
                    }
                )
            )
            {
                host.Frame();
                host.Screenshot(path);
            }

            var shot = ImageResult.FromMemory(
                File.ReadAllBytes(path + ".tga"),
                ColorComponents.RedGreenBlueAlpha
            );

            await Assert.That(StoryPixelsTests.Colours(shot.Data)).IsGreaterThan(1);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    static void PressF12()
    {
        SdlEvents.Key(SDL_Keycode.SDLK_F12, down: true);
        SdlEvents.Key(SDL_Keycode.SDLK_F12, down: false);
    }
}

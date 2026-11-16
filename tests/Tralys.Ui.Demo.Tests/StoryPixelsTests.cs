using Tralys.Ui.Theming;

namespace Tralys.Ui.Demo.Tests;

public sealed class StoryPixelsTests
{
    [Test]
    public async Task Pixels_on_a_headless_renderer_parse_without_frames()
    {
        var parsed = DemoOptions.TryParse(
            ["--capture-stories", "captured", "--pixels", "--headless", "--renderer", "vulkan"],
            out var options,
            out var error
        );

        await Assert.That((parsed, error, options.Pixels)).IsEqualTo((true, (string?)null, true));
    }

    [Test]
    [NotInParallel([nameof(DemoHost), nameof(StoryPixels)])]
    public async Task A_captured_frame_shows_a_storys_image_where_its_layout_places_it_beside_the_layout_a_capture_without_a_gpu_writes()
    {
        var story = DemoOptions.Find("Image.Fit")!.Value;
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"tralys-story-pixels-{Environment.ProcessId}"
        );
        try
        {
            var device = ReferenceDevice.OfThisOs;
            var exit = StoryPixels.Run(
                directory,
                story.Key,
                device.Renderer,
                Themes.Dark,
                Density.Pointer,
                device.AskVendor,
                device.Device
            );
            var path = Path.Combine(directory, StoryCapture.Folder(story), story.Key);
            var layout = StoryCapture.Capture(story);
            var tga = File.ReadAllBytes(path + ".tga");
            var frame = StbImageSharp.ImageResult.FromMemory(
                tga,
                StbImageSharp.ColorComponents.RedGreenBlueAlpha
            );
            var image = layout
                .Nodes.First(static n => n.Path.EndsWith("Image[0]", StringComparison.Ordinal))
                .Bounds;

            await Assert.That(exit).IsEqualTo(0);
            await Assert
                .That(File.ReadAllText(path + StoryCapture.LayoutExtension))
                .IsEqualTo(layout.ToJson());
            await Assert
                .That((tga[2], tga[16], tga[17] & 0x20))
                .IsEqualTo(((byte)2, (byte)24, 0x20));
            await Assert
                .That((frame.Width, frame.Height))
                .IsEqualTo((StoryStage.Width, StoryStage.Height));
            await Assert.That(Colours(frame.Data, frame.Width, image)).IsGreaterThan(1);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    internal static int Colours(byte[] rgba) =>
        Colours(rgba, 1, new Rect(0, 0, 1, rgba.Length / 4f));

    static int Colours(byte[] rgba, int width, Rect within)
    {
        var seen = new HashSet<uint>();
        for (var y = (int)within.Y; y < (int)within.Bottom; y++)
        {
            for (var x = (int)within.X; x < (int)within.Right; x++)
                seen.Add(BitConverter.ToUInt32(rgba, (y * width + x) * 4));
        }
        return seen.Count;
    }
}

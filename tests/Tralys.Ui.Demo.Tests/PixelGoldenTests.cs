using Bgfx.Net;
using Tralys.Ui.Gallery;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Demo.Tests;

public sealed class PixelGoldenTests
{
    static readonly ReferenceDevice Reference = ReferenceDevice.OfThisOs;

    /// <summary>The stories whose text, rounded shapes, clips, masks, gradients, paths, and shadows, a popup layer's
    /// among them, the draw goldens record but do not rasterise.</summary>
    static readonly string[] Rendered =
    [
        "Switch.States",
        "Input.States",
        "Combobox.Item.States",
        "Box.ClipCorners",
        "Box.ClipShapes",
        "Box.Mask",
        "Box.Shadows",
        "Canvas.Gradients",
        "Canvas.Paths",
    ];

    public static IEnumerable<string> Stories() => Rendered;

    [Test]
    [NotInParallel(nameof(StoryPixels))]
    [MethodDataSource(nameof(Stories))]
    public async Task The_story_renders_on_the_reference_device_as_its_goldens_say(string key)
    {
        var story =
            DemoOptions.Find(key)
            ?? throw new InvalidOperationException($"No story is keyed '{key}'.");
        using var pixels = StoryPixels.Start(
            Reference.Renderer,
            Reference.AskVendor,
            Reference.Device
        );
        await Assert
            .That((BgfxDevice.VendorId, Reference.Device == 0 ? 0 : BgfxDevice.DeviceId))
            .IsEqualTo((Reference.Vendor, Reference.Device));

        var failures = new List<string?>();
        foreach (var (theme, name) in Golden.EachTheme)
        {
            var image = new StoryImage(
                StoryStage.Width,
                StoryStage.Height,
                pixels.Draw(story, theme).ToArray()
            );
            failures.Add(Golden.Image(Golden.PixelPath(story, name), image));
        }

        await Assert.That(failures.OfType<string>()).IsEmpty();
    }

    [Test]
    public async Task Every_golden_belongs_to_a_story()
    {
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var story in StoryRegistry.All)
        {
            foreach (var density in Enum.GetValues<Density>())
                expected.Add(Golden.LayoutPath(story, density));
            foreach (var state in StoryStates.All)
                expected.Add(Golden.DrawPath(story, "dark", state));
            expected.Add(Golden.DrawPath(story, "light", StoryState.Normal));
        }
        foreach (var key in Rendered)
        {
            var story = await Assert.That(DemoOptions.Find(key)).IsNotNull();
            foreach (var (_, name) in Golden.EachTheme)
                expected.Add(Golden.PixelPath(story, name));
        }

        var orphans = Directory
            .EnumerateFiles(Golden.Root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Golden.Root, path))
            .Where(path => !expected.Contains(path))
            .ToList();
        if (Golden.Updating)
        {
            foreach (var orphan in orphans)
                File.Delete(Path.Combine(Golden.Root, orphan));
            orphans.Clear();
        }

        await Assert.That(orphans).IsEmpty();
    }
}

/// <summary>A renderer and the device to ask it for, with the vendor it reports and, unless zero, the device.</summary>
readonly record struct ReferenceDevice(
    RendererType Renderer,
    PciIdFlags AskVendor,
    ushort Device,
    ushort Vendor
)
{
    // Lavapipe's Vulkan vendor id, Mesa's 0x10005, cut to bgfx's 16 bits.
    const ushort Mesa = 0x0005;
    const ushort BasicRenderDriver = 0x008c;

    /// <summary>The device each OS draws on, so a machine's own GPU never decides what a test sees: lavapipe on
    /// Linux, WARP on Windows, and on macOS, which has no CPU device, the Apple GPU. A process sticks to one device,
    /// since Vulkan on another device first leaves bgfx unable to start on lavapipe.</summary>
    public static readonly ReferenceDevice OfThisOs =
        OperatingSystem.IsWindows()
            ? new(
                RendererType.Direct3D11,
                PciIdFlags.Microsoft,
                BasicRenderDriver,
                (ushort)PciIdFlags.Microsoft
            )
        : OperatingSystem.IsMacOS()
            ? new(RendererType.Metal, PciIdFlags.None, 0, (ushort)PciIdFlags.Apple)
        : new(RendererType.Vulkan, PciIdFlags.SoftwareRasterizer, 0, Mesa);
}

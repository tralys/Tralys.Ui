using System.Globalization;
using Bgfx.Net;
using Tralys.Ui.Gallery;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Demo;

enum DemoScene
{
    Gallery,
    Story,
    Workload,
    Panel,
}

sealed record DemoOptions
{
    public const string Usage = """
        Usage: Tralys.Ui.Demo [options]
                             with no scene named, the gallery: every story by category, searchable, beside the one
                             picked, with menus for the theme and density
          --story <key>      the story keyed Component.Name, such as Switch.States, alone across the view
          --gallery          the gallery, open at --story's story when one is named
          --states           the gallery, showing the story held normal, hovered, pressed, focused and disabled
          --workload         a scene that changes every frame and takes input, in place of the gallery
          --panel            a second view drawn into a texture, which a turned image shows and passes the pointer
                             on to, in place of the gallery
          --bench            time each phase of every frame and print a summary; needs --frames, and with
                             --headless, --renderer to count draw calls
          --theme <name>     dark or light; dark by default
          --density <name>   pointer or touch; pointer by default
          --capture-stories <dir>
                             write every story's layout to <dir>/<Component>/<Component>.<Name>.layout.json, each in
                             a fresh view after it settles; with --story, that one
          --pixels           with --capture-stories, also write the frame each layout was read from beside it, as
                             <Component>.<Name>.tga; draws, so it needs --headless and --renderer
          --renderer <name>  vulkan, d3d11, metal or webgpu; the platform's best by default
          --headless         no window: SDL's dummy video driver, and bgfx's no-op renderer unless --renderer
                             or --screenshot asks for one that draws, into a target of its own; needs --frames
          --frames <n>       run n frames, then exit; a window otherwise runs until closed
          --size <w>x<h>     the window's size in points, or the headless view's in pixels (1280x720)
          --scale <s>        the UI scale in device pixels per unit; the display's by default, 1 headless
          --no-vsync         present as fast as the device allows
          --devtools         open the inspector beside the scene from the start
          --screenshot <f>   after the last frame, write what it drew to <f>.tga; needs --frames; with
                             --headless, on the platform's best renderer unless --renderer names one
          --help             print this

        With --story in a window, Ctrl+PageDown and Ctrl+PageUp step through the stories, Ctrl+T switches the theme
        and Ctrl+D the density, whenever the story itself takes no such key. In every scene, F12 opens and shuts the
        inspector, where Ctrl+Shift+C picks.
        """;

    public bool Help { get; init; }

    public string? Story { get; init; }

    public bool Gallery { get; init; }

    public bool States { get; init; }

    public bool Workload { get; init; }

    public bool Panel { get; init; }

    public bool Bench { get; init; }

    public Theme Theme { get; init; } = Themes.Dark;

    public Density Density { get; init; }

    public string? CaptureStories { get; init; }

    public bool Pixels { get; init; }

    public RendererType? Renderer { get; init; }

    /// <summary>The GPU to ask the renderer for, by its vendor and, unless zero, its device; the OS's pick when
    /// none.</summary>
    public PciIdFlags Vendor { get; init; }

    public ushort Device { get; init; }

    public bool Headless { get; init; }

    /// <summary>Opens the inspector beside the story from the start; F12 opens and shuts it.</summary>
    public bool DevTools { get; init; }

    public int Frames { get; init; }

    public int Width { get; init; } = 1280;

    public int Height { get; init; } = 720;

    public float? Scale { get; init; }

    public bool VSync { get; init; } = true;

    public string? Screenshot { get; init; }

    public static bool TryParse(
        ReadOnlySpan<string> args,
        out DemoOptions options,
        out string? error
    )
    {
        options = new DemoOptions();
        error = null;
        foreach (var arg in args)
        {
            if (arg is "--help" or "-h")
            {
                options = options with { Help = true };
                return true;
            }
        }

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--headless":
                    options = options with { Headless = true };
                    continue;
                case "--devtools":
                    options = options with { DevTools = true };
                    continue;
                case "--no-vsync":
                    options = options with { VSync = false };
                    continue;
                case "--pixels":
                    options = options with { Pixels = true };
                    continue;
                case "--gallery":
                    options = options with { Gallery = true };
                    continue;
                case "--states":
                    options = options with { States = true };
                    continue;
                case "--workload":
                    options = options with { Workload = true };
                    continue;
                case "--panel":
                    options = options with { Panel = true };
                    continue;
                case "--bench":
                    options = options with { Bench = true };
                    continue;
            }

            if (i + 1 == args.Length)
            {
                error = $"{arg} is not an option, or is missing its value.";
                return false;
            }

            var value = args[++i];
            switch (arg)
            {
                case "--story" when Find(value) is { } story:
                    options = options with { Story = story.Key };
                    break;
                case "--story":
                    error =
                        $"--story {value} names no story; keys are Component.Name, such as Switch.States.";
                    return false;
                case "--theme" when ParseTheme(value) is { } theme:
                    options = options with { Theme = theme };
                    break;
                case "--density" when ParseDensity(value) is { } density:
                    options = options with { Density = density };
                    break;
                case "--capture-stories":
                    options = options with { CaptureStories = Path.GetFullPath(value) };
                    break;
                case "--renderer" when ParseRenderer(value) is { } renderer:
                    options = options with { Renderer = renderer };
                    break;
                case "--frames"
                    when int.TryParse(value, CultureInfo.InvariantCulture, out var frames)
                        && frames > 0:
                    options = options with { Frames = frames };
                    break;
                case "--size" when TryParseSize(value, out var width, out var height):
                    options = options with { Width = width, Height = height };
                    break;
                case "--scale"
                    when float.TryParse(value, CultureInfo.InvariantCulture, out var scale)
                        && scale > 0f
                        && float.IsFinite(scale):
                    options = options with { Scale = scale };
                    break;
                case "--screenshot":
                    options = options with { Screenshot = Path.GetFullPath(value) };
                    break;
                default:
                    error = $"{arg} {value} is not an option this demo takes.";
                    return false;
            }
        }

        if (options.CaptureStories is not null)
            error = CaptureError(options);
        else if (options.Pixels)
            error = "--pixels writes beside each story's layout, so it needs --capture-stories.";
        else if (SceneError(options) is { } scene)
            error = scene;
        else if (options.Headless && options.Frames == 0)
            error = "--headless needs --frames.";
        else if (options.Screenshot is not null && options.Frames == 0)
            error = "--screenshot reads back what a renderer drew, so it needs --frames.";
        else if (options.Bench && options.Frames == 0)
            error = "--bench sums up the frames it ran, so it needs --frames.";
        return error is null;
    }

    public DemoScene Scene =>
        Workload ? DemoScene.Workload
        : Panel ? DemoScene.Panel
        : Story is not null && !Gallery && !States ? DemoScene.Story
        : DemoScene.Gallery;

    static string? SceneError(DemoOptions options)
    {
        var named =
            (options.Workload ? 1 : 0)
            + (options.Panel ? 1 : 0)
            + (options.Gallery || options.States ? 1 : 0);
        if (named > 1)
            return "--workload, --panel and --gallery or --states each name the scene to show, so take one of them.";
        return (options.Workload || options.Panel) && options.Story is not null
            ? "--story names a story, which --workload and --panel don't show."
            : null;
    }

    /// <summary>The story keyed <paramref name="key"/>, ignoring case.</summary>
    public static Story? Find(string key)
    {
        foreach (var story in StoryRegistry.All)
        {
            if (string.Equals(story.Key, key, StringComparison.OrdinalIgnoreCase))
                return story;
        }
        return null;
    }

    static string? CaptureError(DemoOptions options)
    {
        var rest = options with
        {
            CaptureStories = null,
            Story = null,
            Theme = Themes.Dark,
            Density = default,
        };
        if (!options.Pixels)
            return rest == new DemoOptions()
                ? null
                : "--capture-stories runs the stories without a window or a renderer, so it takes no option but --story, --theme, --density and --pixels.";

        if (!options.Headless || options.Renderer is null)
            return "--capture-stories --pixels draws every story on a GPU without a window, so it needs --headless and --renderer, such as --headless --renderer vulkan.";

        return rest with { Pixels = false, Headless = false, Renderer = null } == new DemoOptions()
            ? null
            : "--capture-stories --pixels draws every story in the view the layout capture reads, so it takes no option but --story, --theme, --density, --headless and --renderer.";
    }

    static Theme? ParseTheme(string name) =>
        name switch
        {
            "dark" => Themes.Dark,
            "light" => Themes.Light,
            _ => null,
        };

    static Density? ParseDensity(string name) =>
        name switch
        {
            "pointer" => Density.Pointer,
            "touch" => Density.Touch,
            _ => null,
        };

    static RendererType? ParseRenderer(string name) =>
        name switch
        {
            "vulkan" => RendererType.Vulkan,
            "d3d11" => RendererType.Direct3D11,
            "metal" => RendererType.Metal,
            "webgpu" => RendererType.WebGPU,
            _ => null,
        };

    static bool TryParseSize(string value, out int width, out int height)
    {
        width = height = 0;
        var x = value.IndexOf('x');
        return x > 0
            && int.TryParse(value.AsSpan(0, x), CultureInfo.InvariantCulture, out width)
            && int.TryParse(value.AsSpan(x + 1), CultureInfo.InvariantCulture, out height)
            && width > 0
            && height > 0;
    }
}

using Tralys.Ui.Gallery;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Demo;

/// <summary>Writes stories' layouts: each story in a fresh <see cref="StoryStage"/>, read once it settles, saved as
/// <c>&lt;Component&gt;/&lt;Component&gt;.&lt;Name&gt;.layout.json</c>.</summary>
static class StoryCapture
{
    public const string LayoutExtension = ".layout.json";

    public static int Run(string directory, string? key, Theme theme, Density density) =>
        Run(
            directory,
            key,
            (story, path) => Capture(story, theme, density).Save(path + LayoutExtension)
        );

    /// <summary>Has <paramref name="capture"/> write each story, given the path of its files less their extension,
    /// and reports each as it goes.</summary>
    /// <returns>The process's exit code.</returns>
    public static int Run(string directory, string? key, Action<Story, string> capture)
    {
        IReadOnlyList<Story> stories = key is null
            ? StoryRegistry.All
            : [DemoOptions.Find(key)!.Value];
        var failed = 0;
        for (var i = 0; i < stories.Count; i++)
        {
            var story = stories[i];
            var path = Path.Combine(directory, Folder(story), story.Key);
            try
            {
                capture(story, path);
                Console.WriteLine($"[{i + 1}/{stories.Count}] {path}{LayoutExtension}");
            }
            catch (Exception e)
                when (e is InvalidOperationException or KeyNotFoundException or IOException)
            {
                Console.Error.WriteLine($"failed: {story.Key}: {e.Message}");
                failed++;
            }
        }
        return failed == 0 ? 0 : 3;
    }

    /// <summary>The layout of <paramref name="story"/> as the capture reads it.</summary>
    public static LayoutDump Capture(
        Story story,
        Theme? theme = null,
        Density density = Density.Pointer
    )
    {
        using var stage = new StoryStage();
        stage.Context.Theme = theme ?? Themes.Dark;
        stage.Context.Density = density;
        stage.Settle(story);
        return stage.Layout();
    }

    /// <summary>The folder a story's files go in: its component's name, as its key spells it.</summary>
    public static string Folder(Story story) => story.Key[..story.Key.LastIndexOf('.')];
}

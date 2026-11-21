using System.Reflection;
using Tralys.Ui.Gallery;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Demo.Tests;

/// <summary>The baselines committed under the project's <c>Goldens</c> folder, read from the source tree. With
/// <c>TRALYS_UPDATE_GOLDENS=1</c> a check writes its golden instead of comparing; a failed check writes what it got
/// under <c>artifacts/goldens</c>, at the golden's own path.</summary>
static class Golden
{
    public static readonly string Root = Metadata("Goldens");

    public static readonly (Theme Theme, string Name)[] EachTheme =
    [
        (Themes.Dark, "dark"),
        (Themes.Light, "light"),
    ];

    static readonly string Failures = Metadata("GoldenFailures");

    public static bool Updating =>
        Environment.GetEnvironmentVariable("TRALYS_UPDATE_GOLDENS") == "1";

    public static string LayoutPath(Story story, Density density) =>
        Path.Combine(
            "Layout",
            StoryCapture.Folder(story),
            $"{story.Key}.{Name(density)}{StoryCapture.LayoutExtension}"
        );

    public static string DrawPath(Story story, string theme, StoryState state) =>
        Path.Combine(
            "Draw",
            StoryCapture.Folder(story),
            $"{story.Key}.{theme}.{Name(state)}.draw.txt"
        );

    public static string PixelPath(Story story, string theme) =>
        Path.Combine("Pixels", StoryCapture.Folder(story), $"{story.Key}.{theme}.png");

    /// <returns>Null when <paramref name="actual"/> is the golden at <paramref name="relative"/>, or once it is
    /// written; otherwise how the two differ.</returns>
    public static string? Text(string relative, string actual)
    {
        var path = Path.Combine(Root, relative);
        if (Updating)
        {
            Write(path, actual);
            return null;
        }

        if (!File.Exists(path))
        {
            Write(Path.Combine(Failures, relative), actual);
            return $"{relative}: no golden";
        }

        var expected = File.ReadAllText(path);
        if (expected == actual)
            return null;

        Write(Path.Combine(Failures, relative), actual);
        return $"{relative}: {FirstDifference(expected, actual)}";
    }

    /// <summary>For a golden that must not exist, as a held state drawing what its story's normal look draws.</summary>
    /// <returns>Null when there is none, or once it is deleted; otherwise that it exists.</returns>
    public static string? Absent(string relative)
    {
        var path = Path.Combine(Root, relative);
        if (!File.Exists(path))
            return null;
        if (!Updating)
            return $"{relative}: draws what the normal look draws, so it needs no golden";

        File.Delete(path);
        return null;
    }

    /// <returns>Null when <paramref name="actual"/> is within the tolerance of the golden at
    /// <paramref name="relative"/>, or once it is written; otherwise how many pixels differ. A failure writes the
    /// expected, actual and diff images beside each other.</returns>
    public static string? Image(string relative, StoryImage actual)
    {
        var path = Path.Combine(Root, relative);
        if (Updating)
        {
            actual.Save(path);
            return null;
        }

        var failed = Path.Combine(Failures, Path.ChangeExtension(relative, null));
        if (!File.Exists(path))
        {
            actual.Save(failed + ".actual.png");
            return $"{relative}: no golden";
        }

        var expected = StoryImage.Load(path);
        var differing = expected.Differences(actual);
        if (differing == 0)
            return null;

        expected.Save(failed + ".expected.png");
        actual.Save(failed + ".actual.png");
        expected.Diff(actual).Save(failed + ".diff.png");
        return $"{relative}: {differing} pixels differ, {expected.Width}x{expected.Height} expected against {actual.Width}x{actual.Height}";
    }

    static string FirstDifference(string expected, string actual)
    {
        var want = expected.Split('\n');
        var got = actual.Split('\n');
        var line = 0;
        while (line < want.Length && line < got.Length && want[line] == got[line])
            line++;
        return $"line {line + 1}: expected `{At(want, line)}`, got `{At(got, line)}`";
    }

    static string At(string[] lines, int line) => line < lines.Length ? lines[line] : "(end)";

    static void Write(string path, string text)
    {
        if (Path.GetDirectoryName(path) is { } directory)
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, text);
    }

    static string Name<T>(T value)
        where T : struct, Enum => value.ToString().ToLowerInvariant();

    static string Metadata(string key) =>
        typeof(Golden)
            .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(a => a.Key == key)
            .Select(a => a.Value)
            .OfType<string>()
            .First();
}

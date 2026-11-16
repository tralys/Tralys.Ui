using Tralys.Ui.Gallery;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Demo.Tests;

public sealed class LayoutGoldenTests
{
    public static IEnumerable<(string Story, Density Density)> Cases()
    {
        foreach (var story in StoryRegistry.All)
        {
            foreach (var density in Enum.GetValues<Density>())
                yield return (story.Key, density);
        }
    }

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task The_story_lays_out_as_its_golden_says(string key, Density density)
    {
        var story = DemoOptions.Find(key)!.Value;
        var layout = StoryCapture.Capture(story, density: density).ToJson();

        await Assert.That(Golden.Text(Golden.LayoutPath(story, density), layout)).IsNull();
    }
}

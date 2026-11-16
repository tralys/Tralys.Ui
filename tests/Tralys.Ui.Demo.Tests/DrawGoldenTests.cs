using Tralys.Ui.Gallery;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Demo.Tests;

public sealed class DrawGoldenTests
{
    public static IEnumerable<string> Stories()
    {
        foreach (var story in StoryRegistry.All)
            yield return story.Key;
    }

    [Test]
    [MethodDataSource(nameof(Stories))]
    public async Task The_story_draws_as_its_goldens_say_in_each_theme_and_held_state(string key)
    {
        var story = DemoOptions.Find(key)!.Value;
        var failures = new List<string?>();
        var normal = Draw(story, Themes.Dark, StoryState.Normal);
        failures.Add(Golden.Text(Golden.DrawPath(story, "dark", StoryState.Normal), normal));
        failures.Add(
            Golden.Text(
                Golden.DrawPath(story, "light", StoryState.Normal),
                Draw(story, Themes.Light, StoryState.Normal)
            )
        );
        foreach (var state in StoryStates.All)
        {
            if (state == StoryState.Normal)
                continue;

            var path = Golden.DrawPath(story, "dark", state);
            var drawn = Draw(story, Themes.Dark, state);
            failures.Add(drawn == normal ? Golden.Absent(path) : Golden.Text(path, drawn));
        }

        await Assert.That(failures.OfType<string>()).IsEmpty();
    }

    static string Draw(Story story, Theme theme, StoryState state)
    {
        using var stage = new StoryStage();
        stage.Context.Theme = theme;
        stage.State = state;
        stage.Settle(story);
        return stage.Draw().ToString();
    }
}

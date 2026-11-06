using Tralys.Ui.Docking;
using Tralys.Ui.Gallery;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Demo;

sealed class StoryScene(string key, IDockWindowHost? windows) : IScene
{
    int _shown = IndexOf(key);

    public string Title(UiContext context) =>
        $"{StoryRegistry.All[_shown].Key} · {(context.Theme == Themes.Light ? "light" : "dark")} · {context.Density.ToString().ToLowerInvariant()}";

    public void Mount(UiView view) => view.Mount(StoryFrame.Of(StoryRegistry.All[_shown], windows));

    [FramePath]
    public void Frame(UiView view, string renderer, in FrameTiming last) { }

    public bool Shortcut(UiView view, Key key)
    {
        var context = view.Context;
        switch (key)
        {
            case Key.PageDown:
                _shown = (_shown + 1) % StoryRegistry.All.Count;
                Mount(view);
                return true;
            case Key.PageUp:
                _shown = (_shown + StoryRegistry.All.Count - 1) % StoryRegistry.All.Count;
                Mount(view);
                return true;
            case Key.T:
                context.Theme = context.Theme == Themes.Dark ? Themes.Light : Themes.Dark;
                return true;
            case Key.D:
                context.Density =
                    context.Density == Density.Touch ? Density.Pointer : Density.Touch;
                return true;
            default:
                return false;
        }
    }

    static int IndexOf(string key)
    {
        var stories = StoryRegistry.All;
        for (var i = 0; i < stories.Count; i++)
        {
            if (stories[i].Key == key)
                return i;
        }
        throw new ArgumentException($"No story is keyed '{key}'.", nameof(key));
    }

    public void Dispose() { }
}

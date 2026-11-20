using SDL;
using Tralys.Ui.Docking;
using Tralys.Ui.Gallery;

namespace Tralys.Ui.Demo;

sealed class GalleryScene : IScene
{
    public GalleryScene(string? story, bool states, IDockWindowHost? windows, SdlWindow? window)
    {
        Model = new GalleryModel(story, states, windows)
        {
            OpenLink = static link => SDL3.SDL_OpenURL(link),
        };
        if (window is not null)
            Model.Copy = text => window.SetClipboardText(text);
    }

    public GalleryModel Model { get; }

    public string Title(UiContext context) => "Tralys.Ui gallery";

    public void Mount(UiView view) => view.Mount(Slot.Of(Model, GalleryShell.Root));

    [FramePath]
    public void Frame(UiView view, string renderer, in FrameTiming last) { }

    public bool Shortcut(UiView view, Key key) => false;

    public void Dispose() { }
}

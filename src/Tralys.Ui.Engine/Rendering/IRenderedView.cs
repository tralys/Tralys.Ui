namespace Tralys.Ui.Rendering;

/// <summary>A view a renderer draws: it renders each frame into a <see cref="DrawList"/> at its size in pixels.</summary>
public interface IRenderedView
{
    int PixelWidth { get; }

    int PixelHeight { get; }

    /// <summary>Brings <paramref name="list"/> up to date as this frame of the view.</summary>
    /// <returns>Whether the list changed, rebuilt or patched.</returns>
    [FramePath]
    bool Render(DrawList list);
}

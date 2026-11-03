using Tralys.Ui.Rendering;

namespace Tralys.Ui;

[FramePath]
internal sealed class CanvasPaths
{
    ValueList<PathBuilder> _paths;
    int _given;

    internal void Reset() => _given = 0;

    internal PathBuilder Next()
    {
        if (_given == _paths.Count)
            _paths.Add(new PathBuilder());
        var path = _paths[_given++];
        path.Clear();
        path.FillRule = default;
        return path;
    }
}

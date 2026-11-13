using System.Numerics;

namespace Tralys.Ui.Components;

/// <summary>The tab strips a tab can be dragged between: every <see cref="Tabs"/> whose
/// <see cref="TabsProps.DragGroup"/> this is, while it is mounted, each named by its
/// <see cref="TabsProps.GroupKey"/>.</summary>
/// <remarks>A dragged tab lands on the strip of the group under the pointer, in the dragging strip's view, at the gap
/// the pointer falls in between the tabs' midpoints: over the band of a strip's tab row, across its whole width, or
/// anywhere over a strip with no tabs to show. Where strips overlap, the one hit testing reaches there takes it, else
/// the one later in tree order. One strip at a time shows where the tab would land.</remarks>
[FramePath]
public sealed class TabDragGroup
{
    readonly List<TabsModel> _strips = [];
    TabsModel? _hinted;

    internal int Count => _strips.Count;

    /// <summary>The name of the strip of the group in <paramref name="view"/> under <paramref name="point"/>, in view
    /// units, and the gap the point falls in there; null over none.</summary>
    public UiKey? HitTest(UiView view, Vector2 point, out int gap) =>
        HitTest(view, point, null, out gap);

    /// <summary>As <see cref="HitTest(UiView, Vector2, out int)"/>, passing over every strip within
    /// <paramref name="exclude"/>: a strip carried along under the pointer, as a window torn off with one tab carries
    /// its strip. Hit testing still meets what is carried, so where the strips beneath it overlap, the one later in
    /// tree order takes the point.</summary>
    public UiKey? HitTest(UiView view, Vector2 point, Node? exclude, out int gap) =>
        Find(view, point, exclude, out gap) is { } strip ? strip.Props.GroupKey : null;

    /// <summary>Shows where a tab would land on the strip named <paramref name="strip"/>, at <paramref name="gap"/>,
    /// hiding the landing any other strip shows; null hides them all.</summary>
    public void Hint(UiKey? strip, int gap) => Land(strip is { } key ? Named(key) : null, gap);

    internal void Join(TabsModel strip)
    {
        if (!_strips.Contains(strip))
            _strips.Add(strip);
    }

    internal void Leave(TabsModel strip)
    {
        _strips.Remove(strip);
        if (ReferenceEquals(_hinted, strip))
            _hinted = null;
    }

    internal TabsModel? Find(UiView view, Vector2 point, Node? exclude, out int gap)
    {
        ArgumentNullException.ThrowIfNull(view);
        gap = 0;
        TabsModel? found = null;
        var candidates = 0;
        foreach (var strip in _strips)
        {
            if (!Covers(strip, view, point, exclude))
                continue;
            candidates++;
            if (found is null || Over(strip, found))
                found = strip;
        }
        if (candidates > 1 && view.HitTest(point * view.Scale) is { } hit)
        {
            TabsModel? reached = null;
            foreach (var strip in _strips)
            {
                if (
                    Covers(strip, view, point, exclude)
                    && Within(hit, strip.Root.Node)
                    && (reached is null || Over(strip, reached))
                )
                    reached = strip;
            }
            found = reached ?? found;
        }
        if (found is not null)
            gap = found.GapAt(point.X);
        return found;
    }

    internal void Land(TabsModel? strip, int gap)
    {
        if (_hinted is { } shown && !ReferenceEquals(shown, strip))
            shown.HideSeam();
        _hinted = strip;
        strip?.ShowSeam(gap);
    }

    TabsModel? Named(UiKey key)
    {
        foreach (var strip in _strips)
        {
            if (strip.Props.GroupKey == key)
                return strip;
        }
        return null;
    }

    static bool Covers(TabsModel strip, UiView view, Vector2 point, Node? exclude) =>
        strip.IsShownIn(view)
        && (exclude is null || !Within(strip.Root.Node, exclude))
        && strip.Covers(point);

    static bool Over(TabsModel over, TabsModel under) =>
        over.Root.Node is { } a && under.Root.Node is { } b && TreeOrder.Before(b, a);

    static bool Within(Node? node, Node? ancestor)
    {
        for (var at = node; at is not null; at = at.Parent)
        {
            if (ReferenceEquals(at, ancestor))
                return true;
        }
        return false;
    }
}

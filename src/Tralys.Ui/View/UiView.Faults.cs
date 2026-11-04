#if DEBUG
using System.Numerics;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui;

public sealed partial class UiView
{
    RenderCommandBuffer? _faultBox;

    void DrawFaults(Node parent, DrawList list)
    {
        for (var child = parent._first; child is not null; child = child._next)
        {
            if (!child.IsTransparent)
                continue;
            if (child.FaultHidden)
                DrawFault(child, list);
            else
                DrawFaults(child, list);
        }
    }

    void DrawFault(Node host, DrawList list)
    {
        var space = Space(host, default);
        if (space.IsEmpty)
            return;
        var theme = ThemeAt(host);
        var danger = theme.Color(ColorToken.BorderDanger);
        var dc = (_faultBox ??= new RenderCommandBuffer()).Open();
        dc.DrawBox(space, default, new Thickness(2f), danger.WithOpacity(0.25f), danger);
        dc.DrawLine(
            new Vector2(space.X, space.Y),
            new Vector2(space.Right, space.Bottom),
            2f,
            danger
        );
        dc.DrawLine(
            new Vector2(space.Right, space.Y),
            new Vector2(space.X, space.Bottom),
            2f,
            danger
        );
        var outer = list.Trace?.Enter(host);
        list.Draw(_faultBox);
        list.Trace?.Leave(outer);
    }

    static Rect Space(Node node, Rect union)
    {
        for (var child = node._first; child is not null; child = child._next)
        {
            if (child.Place.Collapsed || child.Place.Hidden)
                continue;
            if (child.IsTransparent)
                union = Space(child, union);
            else
                union = union.IsEmpty ? child._bounds : union.Union(child._bounds);
        }
        return union;
    }
}
#endif

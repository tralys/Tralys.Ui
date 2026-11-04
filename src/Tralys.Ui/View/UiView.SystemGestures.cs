namespace Tralys.Ui;

public sealed partial class UiView
{
    ValueList<NodeEntry> _gestureNodes;
    ValueList<Rect> _gestureRects;
    ValueList<Rect> _sentGestureRects;

    void SendSystemGestures()
    {
        _gestureRects.Clear();
        for (var i = 0; i < _gestureNodes.Count; )
        {
            var entry = _gestureNodes[i];
            var node = entry.Node;
            if (
                !node.IsMounted
                || node.Generation != entry.Generation
                || node.Interaction?.Props.ExcludeSystemGestures != true
            )
            {
                _gestureNodes.RemoveAt(i);
                continue;
            }
            i++;
            if (!Drawn(node) || VisibleRect(node) is not { IsEmpty: false } rect)
                continue;
            _gestureRects.Add(
                new Rect(
                    rect.X * _scale,
                    rect.Y * _scale,
                    rect.Width * _scale,
                    rect.Height * _scale
                )
            );
        }
        if (_gestureRects.AsSpan().SequenceEqual(_sentGestureRects.AsSpan()))
            return;
        _sentGestureRects.Clear();
        for (var i = 0; i < _gestureRects.Count; i++)
            _sentGestureRects.Add(_gestureRects[i]);
        _host.SetSystemGestureExclusions(_gestureRects.AsSpan());
    }

    static Rect VisibleRect(Node node)
    {
        var visible = node.ViewBounds;
        for (var a = node._parent; a is not null; a = a._parent)
        {
            if (a.Kind == NodeKind.Layer)
            {
                var layer = PayloadOf(a);
                return layer.Bounded ? visible.Intersect(layer.Area) : visible;
            }
            if (a.IsTransparent || a.Layout.Overflow == Overflow.Visible)
                continue;
            visible = visible.Intersect(a.ViewBounds.Deflate(a.Layout.Border));
        }
        return visible;
    }
}

using System.Numerics;
using Tralys.Ui.Rendering;

namespace Tralys.Ui;

public sealed partial class UiView
{
    ValueList<Matrix3x2> _hitTransforms;

    /// <summary>The topmost node that takes pointer input at <paramref name="viewPixel"/>, disabled ones included.</summary>
    public Node? HitTest(Vector2 viewPixel)
    {
        VerifyAccess();
        return HitAt(viewPixel / _scale);
    }

    /// <summary>Whether a press at <paramref name="viewPixel"/> would be taken, by what lies there rather than by what
    /// its handlers would do: it would close a layer, or it hits a node hittable below the root. A hit on the mounted
    /// root component's root alone counts only while that root, enabled, presses or can take focus. A host gates its
    /// scene's hover with it.</summary>
    public bool TakesPress(Vector2 viewPixel)
    {
        VerifyAccess();
        var point = viewPixel / _scale;
        var hit = HitAt(point);
        if (PressDismissesAny(point, hit))
            return true;
        if (hit is null)
            return false;
        if (!IsViewRoot(hit))
            return true;
        return (hit._states & InputStates.Disabled) == 0
            && (hit.Interaction?.Props.IsPressable == true || CanFocus(hit));
    }

    Vector2 _hitAt;

    internal Node? HitAt(Vector2 point)
    {
        _hitAt = point;
        return HitLayers(point) ?? HitChildren(_root, point);
    }

    internal Node? PickAt(Vector2 point)
    {
        _hitAt = point;
        return HitLayers(point, pick: true) ?? HitChildren(_root, point, pick: true);
    }

    Node? HitChildren(Node parent, Vector2 point, bool pick = false)
    {
        var start = _ordered.Count;
        Ordered(parent);
        Node? hit = null;
        for (var i = _ordered.Count - 1; i >= start && hit is null; i--)
            hit = Hit(_ordered[i], point, pick);
        _ordered.Truncate(start);
        return hit;
    }

    Node? Hit(Node node, Vector2 point, bool pick = false)
    {
        if (ExitingAbove(node))
            return null;
        if (HoldsLayers(node) && HitLayers(_hitAt, pick, node) is { } held)
            return held;
        if (!Matrix3x2.Invert(LocalTransform(node), out var toLocal))
            return null;
        var local = Vector2.Transform(point, toLocal);
        var size = FrameSize(node);
        if (node.Layout.Overflow == Overflow.Visible || InClip(node, size, local))
        {
            if (HitChildren(node, local + node.ScrollApplied, pick) is { } inner)
                return inner;
        }
        // A layer that isn't positioned spans the view, so its root is picked only where it would take a hit.
        if (pick)
            return (node.Kind != NodeKind.Layer || Takes(node)) && new Rect(size).Contains(local)
                ? node
                : null;
        if (
            node.Payload is TextPayload { HasLinks: true } text
            && text.LinkAt(this, node, local) is { } link
        )
            return link;
        return Takes(node) && Contains(node, local, size) ? node : null;
    }

    static Vector2 LocalPosition(Node node, Vector2 view) =>
        Matrix3x2.Invert(ToView(node), out var toLocal)
            ? Vector2.Transform(view, toLocal)
            : default;

    static Matrix3x2 ToView(Node node)
    {
        var toView = LocalTransform(node);
        for (
            var n = node;
            n.Kind is not (NodeKind.Layer or NodeKind.Root) && n.LayoutParent is { } parent;
            n = parent
        )
            toView *= Matrix3x2.CreateTranslation(-parent.ScrollApplied) * LocalTransform(parent);
        return toView;
    }

    static bool Takes(Node node)
    {
        if (node.Env.PointerEvents == PointerEvents.None)
            return false;
        switch (node.Kind)
        {
            case NodeKind.Text or NodeKind.Canvas:
                return true;
            case NodeKind.EditableText:
                return true;
            case NodeKind.Image:
                return node.PayloadOf<ImagePayload>().Texture is not null;
            case NodeKind.Box
            or NodeKind.Grid
            or NodeKind.Layer
            or NodeKind.Layout
            or NodeKind.VirtualList:
                var props = node.Interaction?.Props ?? default;
                return props.HandlesPointer
                    || node.IsFocusable
                    || props.Modal
                    || node.Scroller is not null
                    || node.Visual.Background.IsVisible
                    || (node.Visual.BorderPaint.IsVisible && !node.Layout.Border.IsZero);
            default:
                return false;
        }
    }

    bool Contains(Node node, Vector2 point, Size size)
    {
        var rect = new Rect(size);
        if (!rect.Contains(point))
            return false;
        switch (node.Kind)
        {
            case NodeKind.Box or NodeKind.Grid or NodeKind.Layer or NodeKind.Layout:
                return InRounded(rect, node.Visual.Radius, point);
            case NodeKind.Canvas:
                var canvas = node.PayloadOf<CanvasPayload>();
                return canvas.HasHitTest
                    ? CanvasHits(canvas, point, size)
                    : node.Commands is { } commands && Drawn(commands, point);
            case NodeKind.Image:
                return node.PayloadOf<ImagePayload>().Hits(point, size);
            default:
                return true;
        }
    }

    bool CanvasHits(CanvasPayload canvas, Vector2 point, Size size)
    {
        try
        {
            return canvas.HitTest(point, size);
        }
        catch (Exception fault) when (Context.OnFault is not null)
        {
            Context.OnFault(fault);
            return false;
        }
    }

    static bool InClip(Node node, Size size, Vector2 point)
    {
        var border = node.Layout.Border;
        var padding = Rect.FromEdges(
            border.Left,
            border.Top,
            size.Width - border.Right,
            size.Height - border.Bottom
        );
        return padding.Contains(point)
            && InRounded(padding, node.Visual.Radius.Deflate(border), point);
    }

    static bool InRounded(Rect rect, CornerRadius radius, Vector2 point)
    {
        if (radius == default)
            return rect.Contains(point);
        var most = MathF.Min(rect.Width, rect.Height) / 2f;
        radius = new CornerRadius(
            MathF.Min(radius.TopLeft, most),
            MathF.Min(radius.TopRight, most),
            MathF.Min(radius.BottomRight, most),
            MathF.Min(radius.BottomLeft, most)
        );
        return InCorner(
                point,
                new Vector2(rect.X + radius.TopLeft, rect.Y + radius.TopLeft),
                radius.TopLeft,
                point.X < rect.X + radius.TopLeft && point.Y < rect.Y + radius.TopLeft
            )
            && InCorner(
                point,
                new Vector2(rect.Right - radius.TopRight, rect.Y + radius.TopRight),
                radius.TopRight,
                point.X > rect.Right - radius.TopRight && point.Y < rect.Y + radius.TopRight
            )
            && InCorner(
                point,
                new Vector2(rect.Right - radius.BottomRight, rect.Bottom - radius.BottomRight),
                radius.BottomRight,
                point.X > rect.Right - radius.BottomRight
                    && point.Y > rect.Bottom - radius.BottomRight
            )
            && InCorner(
                point,
                new Vector2(rect.X + radius.BottomLeft, rect.Bottom - radius.BottomLeft),
                radius.BottomLeft,
                point.X < rect.X + radius.BottomLeft && point.Y > rect.Bottom - radius.BottomLeft
            );
    }

    static bool InCorner(Vector2 point, Vector2 center, float radius, bool inCornerBox) =>
        !inCornerBox || radius <= 0f || Vector2.DistanceSquared(point, center) <= radius * radius;

    bool Drawn(RenderCommandBuffer buffer, Vector2 point)
    {
        var commands = buffer.Commands;
        _hitTransforms.Clear();
        var toLocal = Matrix3x2.Identity;
        foreach (ref readonly var command in commands)
        {
            var local = Vector2.Transform(point, toLocal);
            switch (command.Op)
            {
                case RenderOp.PushTransform:
                    _hitTransforms.Add(toLocal);
                    Matrix3x2.Invert(command.Transform, out var inverse);
                    toLocal *= inverse;
                    break;
                case RenderOp.PushClip
                or RenderOp.PushRoundedClip
                or RenderOp.PushPathClip
                or RenderOp.PushOpacity:
                    _hitTransforms.Add(toLocal);
                    break;
                case RenderOp.Pop when _hitTransforms.Count > 0:
                    toLocal = _hitTransforms.Pop();
                    break;
                case RenderOp.Box
                    when command.Rect.Contains(local)
                        && InRounded(command.Rect, command.Radius, local):
                    if (command.Fill.IsVisible)
                        return true;
                    var inner = command.Rect.Deflate(command.Border);
                    if (
                        command.Stroke.IsVisible
                        && !(
                            inner.Contains(local)
                            && InRounded(inner, command.Radius.Deflate(command.Border), local)
                        )
                    )
                        return true;
                    break;
                case RenderOp.Ellipse when InEllipse(command.Rect, local):
                    if (command.Fill.IsVisible)
                        return true;
                    if (
                        command.StrokeWidth > 0f
                        && !InEllipse(
                            command.Rect.Inflate(-command.StrokeWidth, -command.StrokeWidth),
                            local
                        )
                    )
                        return true;
                    break;
                case RenderOp.Line
                    when NearLine(
                        command.LineFrom,
                        command.LineTo,
                        command.StrokeWidth / 2f,
                        local
                    ):
                    return true;
                case RenderOp.Arc when OnArc(command, local):
                    return true;
                case RenderOp.Image
                or RenderOp.NineSlice
                or RenderOp.Glyphs when command.Rect.Contains(local):
                    return true;
                case RenderOp.Path:
                    var path = buffer.Paths[command.Start];
                    var reach = command.Stroke.IsVisible ? command.StrokeWidth / 2f : 0f;
                    if (path.Bounds.Inflate(reach, reach).Contains(local))
                        return true;
                    break;
            }
        }
        return false;
    }

    static bool InEllipse(Rect rect, Vector2 point)
    {
        if (rect.IsEmpty)
            return false;
        var center = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
        var d = (point - center) / new Vector2(rect.Width / 2f, rect.Height / 2f);
        return d.LengthSquared() <= 1f;
    }

    static bool NearLine(Vector2 from, Vector2 to, float half, Vector2 point)
    {
        var along = to - from;
        var length = along.LengthSquared();
        if (length <= 0f)
            return false;
        var t = Vector2.Dot(point - from, along) / length;
        if (t < 0f || t > 1f)
            return false;
        return Vector2.DistanceSquared(point, from + along * t) <= half * half;
    }

    static bool OnArc(in RenderCommand command, Vector2 point)
    {
        var radius = command.Rect.Width / 2f;
        var center = new Vector2(command.Rect.X + radius, command.Rect.Y + radius);
        var distance = Vector2.Distance(point, center);
        if (distance > radius || distance < radius - command.StrokeWidth)
            return false;
        var sweep = command.ArcSweep;
        if (sweep >= MathF.Tau)
            return true;
        var angle = MathF.Atan2(point.Y - center.Y, point.X - center.X) - command.ArcStart;
        angle %= MathF.Tau;
        if (angle < 0f)
            angle += MathF.Tau;
        return angle <= sweep;
    }
}

using System.Numerics;
using Tralys.Ui.Diagnostics;
using Tralys.Ui.Rendering;

namespace Tralys.Ui;

public sealed partial class UiView
{
    readonly CanvasPaths _canvasPaths = new();
    ValueList<Node> _paintDirty;
    ValueList<Node> _ordered;
    bool _compositeDirty = true;
    DrawList? _lastList;
    long _lastGeneration;
    DrawList? _verified;

    internal void MarkPaint(Node node)
    {
        node.Dirty |= Dirty.Paint;
        if (node.PaintQueued || !node.IsMounted)
            return;
        node.PaintQueued = true;
        _paintDirty.Add(node);
    }

    internal void MarkComposite() => _compositeDirty = true;

    void RemovePaintDirty(Node node)
    {
        node.PaintQueued = false;
        var index = _paintDirty.IndexOf(node);
        if (index >= 0)
            _paintDirty.RemoveAtSwap(index);
    }

    /// <summary>Brings <paramref name="list"/> up to date: as it was when nothing changed, patched in place when only
    /// what some nodes draw changed and the engine accepts the patch, else rebuilt.</summary>
    /// <returns>Whether the list changed.</returns>
    public bool Render(DrawList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        VerifyAccess();
        RepositionLayers();
        var rebuild =
            _compositeDirty
            || list != _lastList
            || list.Generation != _lastGeneration
            || list.IsStale;
        if (!rebuild)
        {
            if (!list.RetainRamps())
                rebuild = true;
            else if (_paintDirty.Count == 0 && _faded.Count == 0)
            {
                if (VerifiesFrames)
                    Verify(list);
                return false;
            }
            else
            {
                try
                {
                    rebuild = !Patch(list) || !PatchFades(list);
                }
                catch
                {
                    list.AbortPatch();
                    _compositeDirty = true;
                    throw;
                }
            }
        }

        if (rebuild)
            Rebuild(list);
        else if (VerifiesFrames)
            Verify(list);
        return true;
    }

    internal bool VerifiesFrames { get; set; }

    internal string? FrameDifference { get; private set; }

    void Verify(DrawList list)
    {
        var rebuilt = _verified ??= new DrawList(new GradientRampAtlas(list.Ramps.Rows))
        {
            RecordsSpans = false,
        };
        rebuilt.Ramps.BeginFrame();
        rebuilt.ShadowDownsample = list.ShadowDownsample;
        DrawFrame(rebuilt);
        FrameDifference ??= DrawListDifference.Of(list, rebuilt);
    }

    bool Patch(DrawList list)
    {
        for (var i = 0; i < _paintDirty.Count; i++)
        {
            var node = _paintDirty[i];
            if (!Drawn(node))
                continue;
            if (node.Payload is EditorPayload editor && !PatchCaret(node, editor, list))
                return false;
            if ((node.Dirty & Dirty.Paint) == 0)
                continue;
            if (node.Span < 0 || node.SpanList != list.Generation)
                return false;
            Record(node, FrameSize(node));
            if (!list.BeginPatch(node.Span))
                return false;
            list.Draw(node.Commands!);
            if (!list.EndPatch())
                return false;
        }
        for (var i = 0; i < _paintDirty.Count; i++)
            _paintDirty[i].PaintQueued = false;
        _paintDirty.Clear();
        return true;
    }

    static bool Drawn(Node node)
    {
        for (var n = node; n is not null; n = n._parent)
        {
            if (
                !n.IsMounted
                || n.Place.Hidden
                || n.Place.Collapsed
                || n.FaultHidden
                || n.LayoutHidden
            )
                return false;
        }
        return true;
    }

    void Rebuild(DrawList list)
    {
        Stats.Rebuilt = true;
        DrawFrame(list);
        for (var i = 0; i < _paintDirty.Count; i++)
            _paintDirty[i].PaintQueued = false;
        _paintDirty.Clear();
        ClearFades();
        _compositeDirty = false;
        _lastList = list;
        _lastGeneration = list.Generation;
    }

    void DrawFrame(DrawList list)
    {
        list.Begin(PixelWidth, PixelHeight);
        list.Textures = this;
        list.PushTransform(Matrix3x2.CreateScale(_scale));
        DrawChildren(_root, list);
        DrawLayers(list, boundary: null);
        list.Pop();
    }

    void DrawChildren(Node parent, DrawList list)
    {
        var start = _ordered.Count;
        Ordered(parent);
        var end = _ordered.Count;
        for (var i = start; i < end; i++)
            DrawNode(_ordered[i], list);
        _ordered.Truncate(start);
#if DEBUG
        DrawFaults(parent, list);
#endif
    }

    void Ordered(Node parent)
    {
        var start = _ordered.Count;
        Collect(parent);
        var items = _ordered.AsSpan(start, _ordered.Count - start);
        for (var i = 1; i < items.Length; i++)
        {
            var node = items[i];
            var j = i - 1;
            while (j >= 0 && ZOf(items[j]) > ZOf(node))
            {
                items[j + 1] = items[j];
                j--;
            }
            items[j + 1] = node;
        }
    }

    void Collect(Node parent)
    {
        for (var child = parent._first; child is not null; child = child._next)
        {
            if (
                child.Place.Collapsed
                || child.Place.Hidden
                || child.FaultHidden
                || child.LayoutHidden
                || child.Kind == NodeKind.Layer
            )
                continue;
            if (child.Kind == NodeKind.Span)
                continue;
            if (child.IsTransparent)
                Collect(child);
            else
                _ordered.Add(child);
        }
    }

    static Size FrameSize(Node node) =>
        node.QuarterTurned ? new Size(node._bounds.Height, node._bounds.Width) : node._bounds.Size;

    internal static Matrix3x2 LocalTransform(Node node)
    {
        var bounds = node._bounds;
        var matrix = Turn(node, bounds.Size);
        var visual = node.Visual;
        if (!visual.Transform.IsIdentity)
        {
            var origin = visual.TransformOrigin ?? new Vector2(0.5f, 0.5f);
            var pivot = new Vector2(origin.X * bounds.Width, origin.Y * bounds.Height);
            matrix *=
                Matrix3x2.CreateTranslation(-pivot)
                * visual.Transform.Matrix
                * Matrix3x2.CreateTranslation(pivot);
        }
        if (!node.Placed.IsIdentity)
            matrix *= node.Placed.Matrix;
        return matrix * Matrix3x2.CreateTranslation(bounds.X, bounds.Y);
    }

    void DrawNode(Node node, DrawList list)
    {
        var outer = list.Trace?.Enter(node);
        var size = FrameSize(node);
        if (NeedsRecord(node, size))
            Record(node, size);

        list.PushTransform(LocalTransform(node));
        var visual = node.Visual;
        var opacity = visual.Opacity ?? 1f;
        if (opacity < 1f)
            PushFade(node, opacity, list);
        if (visual.Mask.IsVisible)
            list.PushOpacityMask(visual.Mask, new Rect(size));

        var shadow = node.Payload is TextPayload { Props.Shadow: { } cast }
            ? cast
            : (DropShadow?)null;
        if (shadow is { } drop)
            list.PushDropShadow(drop);
        var span = list.BeginSpan();
        list.Draw(node.Commands!);
        list.EndSpan(span);
        if (shadow is not null)
            list.Pop();
        if (list.RecordsSpans)
        {
            node.Span = span;
            node.SpanList = list.Generation;
        }
        if (node.Payload is EditorPayload editor)
            DrawCaret(editor, list, size);

        var clips = node.Layout.Overflow != Overflow.Visible;
        if (clips)
        {
            var border = node.Layout.Border;
            list.PushClip(
                Rect.FromEdges(
                    border.Left,
                    border.Top,
                    size.Width - border.Right,
                    size.Height - border.Bottom
                ),
                visual.Radius.Deflate(border)
            );
        }

        var scrolled = node.ScrollApplied != default;
        if (scrolled)
            list.PushTransform(Matrix3x2.CreateTranslation(-node.ScrollApplied));
        DrawChildren(node, list);
        if (scrolled)
            list.Pop();

        if (clips)
            list.Pop();
        if (HoldsLayers(node))
            DrawHeldLayers(node, list);
        if (visual.Mask.IsVisible)
            list.Pop();
        if (opacity < 1f)
            list.Pop();
        list.Pop();
        list.Trace?.Leave(outer);
    }

    static bool NeedsRecord(Node node, Size size) =>
        (node.Dirty & Dirty.Paint) != 0
        || node.Commands is null
        || node.RecordedSize != size
        || node.Commands.IsStale;

    void Record(Node node, Size size)
    {
        var buffer = node.Commands ??=
            Pool<RenderCommandBuffer>.TryRent() ?? new RenderCommandBuffer();
        var dc = buffer.Open();
        var recorded = false;
        try
        {
            switch (node.Kind)
            {
                case NodeKind.Box or NodeKind.Grid or NodeKind.Layer or NodeKind.Layout:
                    RecordBox(dc, node, size);
                    break;
                case NodeKind.Text:
                    ((TextPayload)node.Payload!).Record(dc, this);
                    break;
                case NodeKind.EditableText:
                    ((EditorPayload)node.Payload!).Record(dc, this, size);
                    break;
                case NodeKind.Canvas:
                    DrawCanvas(buffer, dc, (CanvasPayload)node.Payload!, size);
                    break;
                case NodeKind.Image:
                    ((ImagePayload)node.Payload!).Record(dc, size);
                    break;
            }
            recorded = true;
        }
        finally
        {
            if (recorded)
            {
                node.RecordedSize = size;
                node.Dirty &= ~Dirty.Paint;
                Stats.Paints++;
                Recorded?.Add(node);
            }
            else
            {
                buffer.Clear();
                _compositeDirty = true;
            }
        }
    }

    void DrawCanvas(RenderCommandBuffer buffer, DrawingContext dc, CanvasPayload canvas, Size size)
    {
        try
        {
            canvas.Draw(dc, size, _scale, _canvasPaths);
        }
        catch (Exception fault) when (Context.OnFault is not null)
        {
            buffer.Clear();
            Context.OnFault(fault);
        }
    }

    void RecordBox(DrawingContext dc, Node node, Size size)
    {
        var visual = node.Visual;
        var rect = new Rect(size);
        var border = SnapBorder(node.Layout.Border);
        var shadow = visual.Shadow;
        if (!shadow.IsInset && shadow.Color.A > 0)
            dc.DrawBoxShadow(rect, visual.Radius, shadow);
        dc.DrawBox(rect, visual.Radius, border, visual.Background, visual.BorderPaint);
        if (shadow.IsInset && shadow.Color.A > 0)
            dc.DrawBoxShadow(rect.Deflate(border), visual.Radius.Deflate(border), shadow);
    }

    Thickness SnapBorder(Thickness border) =>
        border.IsZero
            ? border
            : new Thickness(
                SnapWidth(border.Left),
                SnapWidth(border.Top),
                SnapWidth(border.Right),
                SnapWidth(border.Bottom)
            );

    float SnapWidth(float width) =>
        width <= 0f
            ? 0f
            : MathF.Max(1f, MathF.Round(width * _scale, MidpointRounding.AwayFromZero)) / _scale;
}

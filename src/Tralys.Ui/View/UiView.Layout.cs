using System.Numerics;

namespace Tralys.Ui;

public sealed partial class UiView
{
    const float Epsilon = 1e-3f;

    ValueList<Node> _layoutChildren;
    ValueList<FlexItem> _items;
    ValueList<FlexLine> _lines;
    Node? _widthOf;
    float _laidOutWidth;

    internal void InvalidateMeasure(Node node)
    {
        if (Deferring)
        {
            ContentChanged(node);
            return;
        }
        var clearing = true;
        for (var n = node; n is not null; n = n._parent)
        {
            if (n == _rowBarrier)
                return;
            if (n.IsTransparent)
                continue;
            if (clearing)
            {
                n.ClearCache();
                n.Dirty |= Dirty.Layout;
                clearing = !IsLayoutRoot(n);
            }
            else
            {
                if ((n.Dirty & Dirty.Layout) != 0)
                    return;
                n.Dirty |= Dirty.Layout;
            }
            if (n.Kind == NodeKind.Layer)
                return;
        }
    }

    void InvalidateAll(Node node)
    {
        node.ClearCache();
        node.Dirty |= Dirty.Layout | Dirty.Paint | Dirty.Env;
        node.EnvBelow = true;
        if (node.Payload is TextPayload text)
            text.Invalidate();
        MarkPaint(node);
        for (var child = node._first; child is not null; child = child._next)
            InvalidateAll(child);
    }

    static bool IsLayoutRoot(Node node) =>
        node.Kind is NodeKind.Root or NodeKind.Layer
        || (node.Layout.Width.IsPx && node.Layout.Height.IsPx);

    internal void ChildrenChanged(Node parent)
    {
        TreeStamp++;
        var layoutParent = parent.IsTransparent ? parent.LayoutParent : parent;
        if (layoutParent is not null)
            InvalidateMeasure(layoutParent);
        _compositeDirty = true;
    }

    internal void MarkEnvBelow(Node node)
    {
        for (var n = node; n is not null; n = n._parent)
        {
            if (n.EnvBelow || n == _rowBarrier)
                return;
            n.EnvBelow = true;
        }
    }

    void Layout()
    {
        if (_root.EnvBelow || (_root.Dirty & Dirty.Env) != 0)
            ResolveEnv(
                _root,
                ResolvedStyle.Root(Context.Theme, Context.Density),
                force: (_root.Dirty & Dirty.Env) != 0
            );
        SettleMeasures();
        if ((_root.Dirty & Dirty.Layout) == 0 && _root.Arranged)
            return;
        _measuring++;
        try
        {
            _root.PctBasis = new Vector2(_size.Width, _size.Height);
            Arrange(_root, new Rect(_size), Matrix3x2.Identity, Matrix3x2.Identity);
        }
        finally
        {
            _measuring--;
        }
    }

    void ResolveEnv(Node node, in ResolvedStyle parent, bool force)
    {
        var own = (node.Dirty & Dirty.Env) != 0;
        if (!force && !own && !node.EnvBelow)
            return;
        var env = node.IsTransparent ? parent : parent.Under(node.Inherit);
        var changed = force || own || node.Env != env;
        node.Env = env;
        node.Dirty &= ~Dirty.Env;
        node.EnvBelow = false;
        if (changed && node.Payload is TextPayload text)
            text.Restyle(this, node);
        else if (changed && node.Payload is EditorPayload editor)
            editor.Restyle(this, node);
        else if (changed && node.Payload is SpanPayload span)
            span.Restyle(this, node);
        for (var child = node._first; child is not null; child = child._next)
            ResolveEnv(child, env, changed);
    }

    internal Size Measure(Node node, Avail width, Avail height)
    {
        if (!node.QuarterTurned)
            return MeasureFrame(node, width, height);
        var turned = MeasureFrame(node, height, width);
        return new Size(turned.Height, turned.Width);
    }

    Size MeasureFrame(Node node, Avail width, Avail height)
    {
        if (width.IsExact && height.IsExact)
            return new Size(width.Value, height.Value);
        ref readonly var style = ref node.Layout;
        var basis = node.QuarterTurned
            ? new Vector2(node.PctBasis.Y, node.PctBasis.X)
            : node.PctBasis;
        var minW = Definite(style.MinWidth.Resolve(basis.X), 0f);
        var maxW = Definite(style.MaxWidth.Resolve(basis.X), float.PositiveInfinity);
        var minH = Definite(style.MinHeight.Resolve(basis.Y), 0f);
        var maxH = Definite(style.MaxHeight.Resolve(basis.Y), float.PositiveInfinity);
        var w = width.IsExact ? width.Value : Clamp(style.Width.Resolve(basis.X), minW, maxW);
        var h = height.IsExact ? height.Value : Clamp(style.Height.Resolve(basis.Y), minH, maxH);
        if (style.AspectRatio > 0f)
        {
            if (!float.IsNaN(w) && float.IsNaN(h))
                h = Clamp(w / style.AspectRatio, minH, maxH);
            else if (float.IsNaN(w) && !float.IsNaN(h))
                w = Clamp(h * style.AspectRatio, minW, maxW);
        }
        if (!float.IsNaN(w) && !float.IsNaN(h))
            return new Size(w, h);

        var aw = float.IsNaN(w) ? Bounded(width, minW, maxW) : Avail.Exact(w);
        var ah = float.IsNaN(h) ? Bounded(height, minH, maxH) : Avail.Exact(h);
        var content = CachedContent(node, aw, ah);
        if (
            float.IsNaN(w)
            && float.IsNaN(h)
            && style.AspectRatio > 0f
            && node.Kind is NodeKind.Image or NodeKind.Canvas
        )
            return Proportional(content, minW, maxW, minH, maxH);
        return new Size(
            float.IsNaN(w) ? Clamp(content.Width, minW, maxW) : w,
            float.IsNaN(h) ? Clamp(content.Height, minH, maxH) : h
        );
    }

    static Size Proportional(Size natural, float minW, float maxW, float minH, float maxH)
    {
        if (!(natural.Width > 0f && natural.Height > 0f))
            return new Size(Clamp(natural.Width, minW, maxW), Clamp(natural.Height, minH, maxH));
        var scale = MathF.Min(1f, MathF.Min(maxW / natural.Width, maxH / natural.Height));
        if (scale >= 1f)
            scale = MathF.Max(1f, MathF.Max(minW / natural.Width, minH / natural.Height));
        return new Size(
            Clamp(natural.Width * scale, minW, maxW),
            Clamp(natural.Height * scale, minH, maxH)
        );
    }

    Size CachedContent(Node node, Avail width, Avail height)
    {
        if (node.TryCached(width, height, out var content))
            return content;
        Stats.Layouts++;
        content = Content(node, width, height);
        node.Cache(width, height, content);
        return content;
    }

    static Avail Bounded(Avail space, float min, float max) =>
        space.IsBounded ? Avail.AtMost(MathF.Max(min, MathF.Min(space.Value, max))) : space;

    Size Content(Node node, Avail width, Avail height) =>
        node.Kind switch
        {
            NodeKind.Box or NodeKind.Layer => Flex(node, width, height, perform: false),
            NodeKind.Grid => GridLayout(node, width, height, perform: false),
            NodeKind.Layout => CustomLayout(node, width, height, perform: false),
            NodeKind.Text => node.PayloadOf<TextPayload>().Measure(this, node, width),
            NodeKind.EditableText => node.PayloadOf<EditorPayload>().Measure(this, width),
            NodeKind.Canvas or NodeKind.Image => node.Layout.IntrinsicSize,
            NodeKind.VirtualList => ListMeasured(node, width, height),
            _ => default,
        };

    internal void Arrange(
        Node node,
        Rect frame,
        in Matrix3x2 parentToView,
        in Matrix3x2 parentSnappedToView
    )
    {
        if (
            (node.Dirty & Dirty.Layout) == 0
            && node.Arranged
            && node.Frame == frame
            && node.ArrangedParent == parentToView
        )
            return;
        Stats.Layouts++;

        var view = Snap(frame.TransformedBy(parentToView));
        Matrix3x2.Invert(parentSnappedToView, out var toParent);
        var bounds = Round(view.TransformedBy(toParent));
        if (bounds.Size != node._bounds.Size)
            MarkPaint(node);
        if (bounds != node._bounds || view != node._viewBounds)
            _compositeDirty = true;
        node._bounds = bounds;
        node._viewBounds = view;
        node.Frame = frame;
        node.ArrangedParent = parentToView;
        node.Arranged = true;
        node.FrameToView =
            Turn(node, frame.Size) * Matrix3x2.CreateTranslation(frame.X, frame.Y) * parentToView;
        node.SnappedToView =
            Turn(node, bounds.Size)
            * Matrix3x2.CreateTranslation(bounds.X, bounds.Y)
            * parentSnappedToView;
        node.Ref?.LaidOut(view);
        node.Dirty &= ~Dirty.Layout;

        var size = node.QuarterTurned ? new Size(frame.Height, frame.Width) : frame.Size;
        switch (node.Kind)
        {
            case NodeKind.Root:
                RootLayout(node, size);
                break;
            case NodeKind.Box or NodeKind.Layer:
                Flex(node, Avail.Exact(size.Width), Avail.Exact(size.Height), perform: true);
                break;
            case NodeKind.Grid:
                GridLayout(node, Avail.Exact(size.Width), Avail.Exact(size.Height), perform: true);
                break;
            case NodeKind.Layout:
                CustomLayout(
                    node,
                    Avail.Exact(size.Width),
                    Avail.Exact(size.Height),
                    perform: true
                );
                break;
            case NodeKind.Text:
                node.PayloadOf<TextPayload>().Arrange(this, node, size.Width);
                break;
            case NodeKind.EditableText:
                node.PayloadOf<EditorPayload>().Arrange(this, node, size);
                break;
            case NodeKind.VirtualList:
                ListArranged(node, size);
                break;
        }
        if (node.Scroller is { } scroller && node.Kind is NodeKind.Box or NodeKind.Grid)
            ScrollerArranged(node, scroller);
    }

    internal static Matrix3x2 Turn(Node node, Size bounds) =>
        (node.Layout.QuarterTurns & 3) switch
        {
            1 => Matrix3x2.CreateRotation(MathF.PI / 2f)
                * Matrix3x2.CreateTranslation(bounds.Width, 0f),
            2 => Matrix3x2.CreateRotation(MathF.PI)
                * Matrix3x2.CreateTranslation(bounds.Width, bounds.Height),
            3 => Matrix3x2.CreateRotation(-MathF.PI / 2f)
                * Matrix3x2.CreateTranslation(0f, bounds.Height),
            _ => Matrix3x2.Identity,
        };

    Rect Snap(Rect rect) =>
        Rect.FromEdges(
            SnapEdge(rect.X),
            SnapEdge(rect.Y),
            SnapEdge(rect.Right),
            SnapEdge(rect.Bottom)
        );

    float SnapEdge(float edge) => Pixels(edge) / _scale;

    // Halves round up, not away from zero, so an edge moved by whole pixels snaps the same on either side of 0.
    float Pixels(float edge)
    {
        var pixels = edge * _scale;
        var whole = MathF.Floor(pixels);
        return pixels - whole >= 0.5f ? whole + 1f : whole;
    }

    float Centred(in Matrix3x2 toView, bool horizontal, float start, float room, float size)
    {
        var axis = horizontal ? Vector2.UnitX : Vector2.UnitY;
        var from = Vector2.Transform(axis * start, toView);
        var to = Vector2.Transform(axis * (start + room), toView);
        var snapped =
            MathF.Abs(to.X - from.X) >= MathF.Abs(to.Y - from.Y)
                ? MathF.Abs(Pixels(to.X) - Pixels(from.X))
                : MathF.Abs(Pixels(to.Y) - Pixels(from.Y));
        return MathF.Round((snapped - size * _scale) / 2f, MidpointRounding.AwayFromZero) / _scale;
    }

    // Undoes the float error a transform back from view space leaves on snapped edges.
    Rect Round(Rect rect) =>
        Rect.FromEdges(Tidy(rect.X), Tidy(rect.Y), Tidy(rect.Right), Tidy(rect.Bottom));

    float Tidy(float edge)
    {
        var pixels = edge * _scale;
        var whole = MathF.Round(pixels);
        return MathF.Abs(pixels - whole) < Epsilon ? whole / _scale : edge;
    }

    void RootLayout(Node root, Size size)
    {
        var start = _layoutChildren.Count;
        CollectChildren(root, ref _layoutChildren);
        var end = _layoutChildren.Count;
        for (var i = start; i < end; i++)
        {
            var child = _layoutChildren[i];
            child.PctBasis = new Vector2(size.Width, size.Height);
            if (child.Place.Position == Position.Absolute)
            {
                PlaceAbsolute(root, child, new Rect(size), default);
                continue;
            }
            var margin = child.Place.Margin;
            var room = new Size(
                MathF.Max(0f, size.Width - margin.Left - margin.Right),
                MathF.Max(0f, size.Height - margin.Top - margin.Bottom)
            );
            var measured = Measure(
                child,
                Stretched(child, row: false, room.Width),
                Stretched(child, row: true, room.Height)
            );
            Arrange(
                child,
                new Rect(margin.Left, margin.Top, measured.Width, measured.Height),
                root.FrameToView,
                root.SnappedToView
            );
        }
        _layoutChildren.Truncate(start);
    }

    internal static void CollectChildren(Node parent, ref ValueList<Node> into)
    {
        for (var child = parent._first; child is not null; child = child._next)
        {
            if (child.IsTransparent)
                CollectChildren(child, ref into);
            else if (!child.Place.Collapsed && child.Kind != NodeKind.Layer)
                into.Add(child);
        }
    }

    static bool Scrolls(Overflow overflow, bool horizontal) =>
        overflow == Overflow.Scroll
        || overflow == (horizontal ? Overflow.ScrollX : Overflow.ScrollY);

    static bool IsScrollContainer(Node node) => node.Layout.Overflow >= Overflow.ScrollX;

    Size Flex(Node box, Avail width, Avail height, bool perform)
    {
        ref readonly var style = ref box.Layout;
        var row = style.Direction == Direction.Row;
        var pad = style.Padding;
        var border = style.Border;
        var pbW = pad.Left + pad.Right + border.Left + border.Right;
        var pbH = pad.Top + pad.Bottom + border.Top + border.Bottom;
        var mainSpace = row ? width : height;
        var crossSpace = row ? height : width;
        var pbMain = row ? pbW : pbH;
        var pbCross = row ? pbH : pbW;
        var scrollMain = Scrolls(style.Overflow, row);
        var scrollCross = Scrolls(style.Overflow, !row);
        var innerMain =
            mainSpace.IsBounded && !scrollMain
                ? MathF.Max(0f, mainSpace.Value - pbMain)
                : float.NaN;
        var innerCross =
            crossSpace.IsBounded && !scrollCross
                ? MathF.Max(0f, crossSpace.Value - pbCross)
                : float.NaN;
        var mainDefinite = mainSpace.IsExact && !scrollMain;
        var crossDefinite = crossSpace.IsExact && !scrollCross;
        var gapMain = row ? style.ColumnGap : style.RowGap;
        var gapCross = row ? style.RowGap : style.ColumnGap;
        var contentW = width.IsExact ? MathF.Max(0f, width.Value - pbW) : float.NaN;
        var contentH = height.IsExact ? MathF.Max(0f, height.Value - pbH) : float.NaN;
        var minSizing = mainSpace.Kind == AvailKind.MinContent;
        var wraps = style.Wrap != FlexWrap.NoWrap;

        var childStart = _layoutChildren.Count;
        CollectChildren(box, ref _layoutChildren);
        var childEnd = _layoutChildren.Count;
        var itemStart = _items.Count;
        for (var c = childStart; c < childEnd; c++)
        {
            var child = _layoutChildren[c];
            if (child.Place.Position == Position.Absolute)
                continue;
            child.PctBasis = new Vector2(contentW, contentH);
            var margin = child.Place.Margin;
            var marginCrossStart = row ? margin.Top : margin.Left;
            var marginCrossEnd = row ? margin.Bottom : margin.Right;
            var align = Effective(child.Place.AlignSelf, style.Align);
            var crossAvail = ItemCross(
                align,
                marginCrossStart,
                marginCrossEnd,
                child,
                row,
                innerCross,
                crossDefinite && !wraps,
                crossSpace.Kind
            );
            var basis = child.Place.Basis.Resolve(mainDefinite ? innerMain : float.NaN);
            var measured = false;
            float itemBase;
            if (!float.IsNaN(basis))
                itemBase = basis;
            else if (IsScrollContainer(child) && mainDefinite && MainIsAuto(child, row))
                itemBase = 0f;
            else
            {
                itemBase = MainOf(
                    Measure(
                        child,
                        minSizing ? Avail.MinContent : Avail.MaxContent,
                        crossAvail,
                        row
                    ),
                    row
                );
                measured = true;
            }
            var (minMain, maxMain) = MainLimits(child, row);
            var min = 0f;
            var pending = false;
            if (!float.IsNaN(minMain))
                min = minMain;
            else if (child.Layout.Overflow != Overflow.Visible)
                min = 0f;
            else if (measured && minSizing)
                min = MathF.Min(itemBase, maxMain);
            else if (measured)
                pending = true;
            else
                min = MathF.Min(
                    MainOf(Measure(child, Avail.MinContent, crossAvail, row), row),
                    maxMain
                );

            ref var item = ref _items.AddDefault();
            item.Node = child;
            item.MarginMainStart = row ? margin.Left : margin.Top;
            item.MarginMainEnd = row ? margin.Right : margin.Bottom;
            item.MarginCrossStart = marginCrossStart;
            item.MarginCrossEnd = marginCrossEnd;
            item.Grow = MathF.Max(0f, child.Place.Grow);
            item.Shrink = MathF.Max(0f, child.Place.Shrink ?? 1f);
            item.Align = align;
            item.CrossAvail = crossAvail;
            item.Base = itemBase;
            item.Min = min;
            item.Max = maxMain;
            item.MinPending = pending;
            item.Hypothetical = Clamp(itemBase, min, maxMain);
        }
        var count = _items.Count - itemStart;

        var lineStart = _lines.Count;
        var limit =
            !wraps ? float.PositiveInfinity
            : minSizing ? 0f
            : float.IsNaN(innerMain) ? float.PositiveInfinity
            : innerMain;
        var line = new FlexLine { Start = 0 };
        for (var i = 0; i < count; i++)
        {
            ref var item = ref _items[itemStart + i];
            var outer = item.Hypothetical + item.MarginMainStart + item.MarginMainEnd;
            if (line.Count > 0 && line.Main + gapMain + outer > limit + Epsilon)
            {
                _lines.Add(line);
                line = new FlexLine { Start = i };
            }
            line.Main += (line.Count > 0 ? gapMain : 0f) + outer;
            line.Count++;
        }
        if (line.Count > 0 || count == 0)
            _lines.Add(line);
        var lineCount = _lines.Count - lineStart;

        var longest = 0f;
        for (var l = 0; l < lineCount; l++)
            longest = MathF.Max(longest, _lines[lineStart + l].Main);
        var usedMain =
            mainDefinite ? innerMain
            : mainSpace.Kind == AvailKind.AtMost && !scrollMain ? MathF.Min(innerMain, longest)
            : longest;
        if (scrollMain && perform)
            usedMain = MathF.Max(MathF.Max(0f, mainSpace.Value - pbMain), longest);

        for (var l = 0; l < lineCount; l++)
            ResolveFlexible(itemStart, _lines[lineStart + l], usedMain, gapMain, row);

        var crossContent = 0f;
        for (var l = 0; l < lineCount; l++)
        {
            var first = _lines[lineStart + l].Start;
            var end = first + _lines[lineStart + l].Count;
            var ascent = 0f;
            var descent = 0f;
            var tallest = 0f;
            for (var i = first; i < end; i++)
            {
                var child = _items[itemStart + i].Node;
                var measured = Measure(
                    child,
                    Avail.Exact(_items[itemStart + i].Target),
                    _items[itemStart + i].CrossAvail,
                    row
                );
                if (child == _widthOf)
                    _laidOutWidth = measured.Width;
                ref var item = ref _items[itemStart + i];
                item.Cross = CrossOf(measured, row);
                var outer = item.Cross + item.MarginCrossStart + item.MarginCrossEnd;
                if (item.Align == Align.Baseline && row)
                {
                    item.Ascent = item.MarginCrossStart + Baseline(child, measured);
                    ascent = MathF.Max(ascent, item.Ascent);
                    descent = MathF.Max(descent, outer - item.Ascent);
                }
                tallest = MathF.Max(tallest, outer);
            }
            // Measuring a nested box can grow _lines, so a ref taken before it would write to the old array.
            ref var flexLine = ref _lines[lineStart + l];
            flexLine.Ascent = ascent;
            flexLine.Cross = MathF.Max(tallest, ascent + descent);
            crossContent += flexLine.Cross + (l > 0 ? gapCross : 0f);
        }

        if (!wraps && crossDefinite && lineCount == 1)
        {
            _lines[lineStart].Cross = innerCross;
            crossContent = innerCross;
        }

        var usedCross =
            crossDefinite ? innerCross
            : crossSpace.Kind == AvailKind.AtMost && !scrollCross
                ? MathF.Min(innerCross, crossContent)
            : crossContent;
        if (scrollCross && perform)
        {
            usedCross = MathF.Max(MathF.Max(0f, crossSpace.Value - pbCross), crossContent);
            if (!wraps && lineCount == 1)
                _lines[lineStart].Cross = crossContent = usedCross;
        }

        Size result;
        if (row)
            result = new Size(
                width.IsExact ? width.Value : usedMain + pbMain,
                height.IsExact ? height.Value : usedCross + pbCross
            );
        else
            result = new Size(
                width.IsExact ? width.Value : usedCross + pbCross,
                height.IsExact ? height.Value : usedMain + pbMain
            );

        if (perform)
            PlaceFlex(
                box,
                itemStart,
                lineStart,
                lineCount,
                row,
                usedMain,
                usedCross,
                crossContent,
                gapMain,
                gapCross,
                childStart,
                childEnd
            );

        _lines.Truncate(lineStart);
        _items.Truncate(itemStart);
        _layoutChildren.Truncate(childStart);
        return result;
    }

    void PlaceFlex(
        Node box,
        int itemStart,
        int lineStart,
        int lineCount,
        bool row,
        float usedMain,
        float usedCross,
        float crossContent,
        float gapMain,
        float gapCross,
        int childStart,
        int childEnd
    )
    {
        ref readonly var style = ref box.Layout;
        var pad = style.Padding;
        var border = style.Border;
        var mainOrigin = row ? border.Left + pad.Left : border.Top + pad.Top;
        var crossOrigin = row ? border.Top + pad.Top : border.Left + pad.Left;
        var wraps = style.Wrap != FlexWrap.NoWrap;
        var toView = ContentToView(box);
        var snappedToView = ContentSnappedToView(box);

        var freeCross = usedCross - crossContent;
        var lineOffset = 0f;
        var lineBetween = 0f;
        if (wraps && lineCount > 0)
        {
            switch (style.AlignContent)
            {
                case AlignContent.Stretch when freeCross > 0f:
                    var extra = freeCross / lineCount;
                    for (var l = 0; l < lineCount; l++)
                        _lines[lineStart + l].Cross += extra;
                    break;
                case AlignContent.Center:
                    lineOffset = Centred(toView, !row, crossOrigin, usedCross, crossContent);
                    break;
                case AlignContent.End:
                    lineOffset = freeCross;
                    break;
                case AlignContent.SpaceBetween when freeCross > 0f && lineCount > 1:
                    lineBetween = freeCross / (lineCount - 1);
                    break;
                case AlignContent.SpaceAround when freeCross > 0f:
                    lineBetween = freeCross / lineCount;
                    lineOffset = lineBetween / 2f;
                    break;
                case AlignContent.SpaceEvenly when freeCross > 0f:
                    lineBetween = freeCross / (lineCount + 1);
                    lineOffset = lineBetween;
                    break;
                case AlignContent.SpaceAround or AlignContent.SpaceEvenly:
                    lineOffset = Centred(toView, !row, crossOrigin, usedCross, crossContent);
                    break;
            }
        }

        var gaps = GapsOf(box);
        var reverse = style.Wrap == FlexWrap.WrapReverse;
        var crossPos = crossOrigin + (reverse ? usedCross - lineOffset : lineOffset);
        for (var l = 0; l < lineCount; l++)
        {
            var flexLine = _lines[lineStart + l];
            var lineCross = reverse ? crossPos - flexLine.Cross : crossPos;
            var used = 0f;
            for (var i = flexLine.Start; i < flexLine.Start + flexLine.Count; i++)
            {
                ref readonly var item = ref _items[itemStart + i];
                used += item.Target + item.MarginMainStart + item.MarginMainEnd;
            }
            used += gapMain * MathF.Max(0, flexLine.Count - 1);
            var free = usedMain - used;
            var offset = 0f;
            var between = 0f;
            switch (style.Justify)
            {
                case Justify.Center:
                    offset = Centred(toView, row, mainOrigin, usedMain, used);
                    break;
                case Justify.End:
                    offset = free;
                    break;
                case Justify.SpaceBetween when free > 0f && flexLine.Count > 1:
                    between = free / (flexLine.Count - 1);
                    break;
                case Justify.SpaceAround when free > 0f:
                    between = free / flexLine.Count;
                    offset = between / 2f;
                    break;
                case Justify.SpaceEvenly when free > 0f:
                    between = free / (flexLine.Count + 1);
                    offset = between;
                    break;
                case Justify.SpaceAround or Justify.SpaceEvenly:
                    offset = Centred(toView, row, mainOrigin, usedMain, used);
                    break;
            }

            var mainPos = mainOrigin + offset;
            for (var i = flexLine.Start; i < flexLine.Start + flexLine.Count; i++)
            {
                ref readonly var item = ref _items[itemStart + i];
                var child = item.Node;
                var cross = item.Cross;
                if (item.Align == Align.Stretch && CrossIsAuto(child, row))
                {
                    var (minCross, maxCross) = CrossLimits(child, row);
                    cross = Clamp(
                        flexLine.Cross - item.MarginCrossStart - item.MarginCrossEnd,
                        Definite(minCross, 0f),
                        maxCross
                    );
                }
                var outer = cross + item.MarginCrossStart + item.MarginCrossEnd;
                var crossAt =
                    item.Align switch
                    {
                        Align.Center => Centred(toView, !row, lineCross, flexLine.Cross, outer),
                        Align.End => flexLine.Cross - outer,
                        Align.Baseline when row => flexLine.Ascent - item.Ascent,
                        _ => 0f,
                    } + item.MarginCrossStart;
                var mainAt = mainPos + item.MarginMainStart;
                var rect = row
                    ? new Rect(mainAt, lineCross + crossAt, item.Target, cross)
                    : new Rect(lineCross + crossAt, mainAt, cross, item.Target);
                mainPos = mainAt + item.Target + item.MarginMainEnd + gapMain + between;
                Arrange(child, rect, toView, snappedToView);
                if (gaps is not null && gapMain > 0f && i + 1 < flexLine.Start + flexLine.Count)
                {
                    var gapAt = mainAt + item.Target + item.MarginMainEnd;
                    Gap(
                        gaps,
                        row
                            ? new Rect(gapAt, lineCross, gapMain, flexLine.Cross)
                            : new Rect(lineCross, gapAt, flexLine.Cross, gapMain),
                        toView
                    );
                }
            }

            if (gaps is not null && gapCross > 0f && l + 1 < lineCount)
            {
                var gapAt = reverse ? lineCross - gapCross : lineCross + flexLine.Cross;
                Gap(
                    gaps,
                    row
                        ? new Rect(mainOrigin, gapAt, usedMain, gapCross)
                        : new Rect(gapAt, mainOrigin, gapCross, usedMain),
                    toView
                );
            }
            crossPos = reverse
                ? lineCross - gapCross - lineBetween
                : crossPos + flexLine.Cross + gapCross + lineBetween;
        }

        var frame = box.QuarterTurned
            ? new Size(box.Frame.Height, box.Frame.Width)
            : box.Frame.Size;
        var paddingBox = Rect.FromEdges(
            border.Left,
            border.Top,
            frame.Width - border.Right,
            frame.Height - border.Bottom
        );
        for (var c = childStart; c < childEnd; c++)
        {
            var child = _layoutChildren[c];
            if (child.Place.Position == Position.Absolute)
                PlaceAbsolute(box, child, paddingBox, pad);
        }
        box.ContentExtent = Extent(box, childStart, childEnd, pad);
    }

    Rect Extent(Node box, int childStart, int childEnd, Thickness pad)
    {
        var extent = default(Rect);
        var any = false;
        for (var c = childStart; c < childEnd; c++)
        {
            var child = _layoutChildren[c];
            var m = child.Place.Margin;
            var outer = Rect.FromEdges(
                child.Frame.X - m.Left,
                child.Frame.Y - m.Top,
                child.Frame.Right + m.Right,
                child.Frame.Bottom + m.Bottom
            );
            extent = any ? extent.Union(outer) : outer;
            any = true;
        }
        if (!any)
            return default;
        var border = box.Layout.Border;
        return Rect.FromEdges(
            border.Left,
            border.Top,
            extent.Right + pad.Right,
            extent.Bottom + pad.Bottom
        );
    }

    void PlaceAbsolute(Node parent, Node child, Rect paddingBox, Thickness pad)
    {
        child.PctBasis = new Vector2(paddingBox.Width, paddingBox.Height);
        var place = child.Place;
        var m = place.Margin;
        var left = place.Left.Resolve(paddingBox.Width);
        var right = place.Right.Resolve(paddingBox.Width);
        var top = place.Top.Resolve(paddingBox.Height);
        var bottom = place.Bottom.Resolve(paddingBox.Height);

        var widthAvail =
            !float.IsNaN(left) && !float.IsNaN(right)
                ? Stretched(child, row: false, paddingBox.Width - left - right - m.Left - m.Right)
                : Avail.AtMost(
                    paddingBox.Width - Definite(left, 0f) - Definite(right, 0f) - m.Left - m.Right
                );
        var w = Measure(child, widthAvail, Avail.MaxContent).Width;
        var heightAvail =
            !float.IsNaN(top) && !float.IsNaN(bottom)
                ? Stretched(child, row: true, paddingBox.Height - top - bottom - m.Top - m.Bottom)
                : Avail.AtMost(
                    paddingBox.Height - Definite(top, 0f) - Definite(bottom, 0f) - m.Top - m.Bottom
                );
        var h = Measure(child, Avail.Exact(w), heightAvail).Height;

        var x =
            !float.IsNaN(left) ? paddingBox.X + left + m.Left
            : !float.IsNaN(right) ? paddingBox.Right - right - m.Right - w
            : paddingBox.X + pad.Left + m.Left;
        var y =
            !float.IsNaN(top) ? paddingBox.Y + top + m.Top
            : !float.IsNaN(bottom) ? paddingBox.Bottom - bottom - m.Bottom - h
            : paddingBox.Y + pad.Top + m.Top;
        Arrange(child, new Rect(x, y, w, h), ContentToView(parent), ContentSnappedToView(parent));
    }

    void ResolveFlexible(int itemStart, in FlexLine line, float usedMain, float gap, bool row)
    {
        var start = itemStart + line.Start;
        var end = start + line.Count;
        var margins = gap * MathF.Max(0, line.Count - 1);
        var hypothetical = margins;
        for (var i = start; i < end; i++)
        {
            ref var item = ref _items[i];
            margins += item.MarginMainStart + item.MarginMainEnd;
            hypothetical += item.Hypothetical + item.MarginMainStart + item.MarginMainEnd;
        }

        if (float.IsNaN(usedMain) || float.IsInfinity(usedMain))
        {
            for (var i = start; i < end; i++)
                _items[i].Target = _items[i].Hypothetical;
            return;
        }

        var growing = hypothetical < usedMain;
        var initialFree = usedMain - margins;
        for (var i = start; i < end; i++)
        {
            ref var item = ref _items[i];
            item.Target = item.Hypothetical;
            item.Frozen =
                (growing ? item.Grow == 0f : item.Shrink == 0f)
                || (growing && item.Base > item.Hypothetical)
                || (!growing && item.Base < item.Hypothetical);
            initialFree -= item.Frozen ? item.Target : item.Base;
        }

        // A min-content floor never exceeds a max-content base, so it binds only on a line that shrinks.
        if (!growing && initialFree < 0f)
        {
            for (var i = start; i < end; i++)
            {
                if (!_items[i].MinPending || _items[i].Frozen)
                    continue;
                var min = MainOf(
                    Measure(_items[i].Node, Avail.MinContent, _items[i].CrossAvail, row),
                    row
                );
                ref var item = ref _items[i];
                item.Min = MathF.Min(min, item.Max);
                item.MinPending = false;
            }
        }

        for (var round = 0; round <= line.Count; round++)
        {
            var free = usedMain - margins;
            var factors = 0f;
            var scaledShrink = 0f;
            var unfrozen = 0;
            for (var i = start; i < end; i++)
            {
                ref var item = ref _items[i];
                if (item.Frozen)
                {
                    free -= item.Target;
                    continue;
                }
                free -= item.Base;
                factors += growing ? item.Grow : item.Shrink;
                scaledShrink += item.Shrink * item.Base;
                unfrozen++;
            }
            if (unfrozen == 0)
                break;
            if (factors < 1f && MathF.Abs(initialFree * factors) < MathF.Abs(free))
                free = initialFree * factors;

            var violation = 0f;
            for (var i = start; i < end; i++)
            {
                ref var item = ref _items[i];
                if (item.Frozen)
                    continue;
                var target = item.Base;
                if (growing && free > 0f && factors > 0f)
                    target += free * item.Grow / factors;
                else if (!growing && free < 0f && scaledShrink > 0f)
                    target += free * item.Shrink * item.Base / scaledShrink;
                var clamped = Clamp(target, item.Min, item.Max);
                item.Violation = clamped - target;
                item.Target = clamped;
                violation += item.Violation;
            }

            for (var i = start; i < end; i++)
            {
                ref var item = ref _items[i];
                if (item.Frozen)
                    continue;
                if (
                    violation == 0f
                    || (violation > 0f && item.Violation > 0f)
                    || (violation < 0f && item.Violation < 0f)
                )
                    item.Frozen = true;
            }
        }
    }

    static Avail ItemCross(
        Align align,
        float marginCrossStart,
        float marginCrossEnd,
        Node child,
        bool row,
        float innerCross,
        bool stretches,
        AvailKind crossKind
    )
    {
        if (crossKind == AvailKind.MinContent)
            return Avail.MinContent;
        if (float.IsNaN(innerCross))
            return Avail.MaxContent;
        var room = innerCross - marginCrossStart - marginCrossEnd;
        return align == Align.Stretch && stretches
            ? Stretched(child, row, room)
            : Avail.AtMost(room);
    }

    internal static Avail Stretched(Node child, bool row, float room)
    {
        if (!CrossIsAuto(child, row))
            return Avail.AtMost(room);
        var (min, max) = CrossLimits(child, row);
        return Avail.Exact(Clamp(room, Definite(min, 0f), max));
    }

    Size Measure(Node child, Avail main, Avail cross, bool row) =>
        row ? Measure(child, main, cross) : Measure(child, cross, main);

    static float MainOf(Size size, bool row) => row ? size.Width : size.Height;

    static float CrossOf(Size size, bool row) => row ? size.Height : size.Width;

    static Align Effective(AlignSelf self, Align container) =>
        self == AlignSelf.Auto ? container : (Align)((int)self - 1);

    // A child's own sizes are in its frame, which a quarter turn swaps against its parent's.
    static bool CrossIsAuto(Node child, bool row) =>
        (row != child.QuarterTurned ? child.Layout.Height : child.Layout.Width).IsAuto;

    static bool MainIsAuto(Node child, bool row) =>
        (row != child.QuarterTurned ? child.Layout.Width : child.Layout.Height).IsAuto;

    static (float Min, float Max) MainLimits(Node child, bool row) =>
        Limits(child, row != child.QuarterTurned);

    static (float Min, float Max) CrossLimits(Node child, bool row) =>
        Limits(child, row == child.QuarterTurned);

    static (float Min, float Max) Limits(Node child, bool horizontal)
    {
        var basis = horizontal ? child.PctBasis.X : child.PctBasis.Y;
        ref readonly var style = ref child.Layout;
        return horizontal
            ? (
                style.MinWidth.Resolve(basis),
                Definite(style.MaxWidth.Resolve(basis), float.PositiveInfinity)
            )
            : (
                style.MinHeight.Resolve(basis),
                Definite(style.MaxHeight.Resolve(basis), float.PositiveInfinity)
            );
    }

    float Baseline(Node child, Size measured)
    {
        child.MarkBaselined();
        if (child.Payload is TextPayload text)
            return text.Baseline(this, measured.Width);
        if (child.Payload is EditorPayload editor)
            return editor.Baseline(this, measured.Width);
        if (
            child.Kind == NodeKind.Box
            && FirstLayoutChild(child) is { Payload: TextPayload first } inner
        )
        {
            inner.MarkBaselined();
            ref readonly var style = ref child.Layout;
            return style.Border.Top
                + style.Padding.Top
                + inner.Place.Margin.Top
                + first.Baseline(this, LaidOutWidth(child, inner, measured));
        }
        return measured.Height;
    }

    float LaidOutWidth(Node box, Node item, Size size)
    {
        var (outerItem, outerWidth) = (_widthOf, _laidOutWidth);
        _widthOf = item;
        _laidOutWidth = 0f;
        var frame = box.QuarterTurned ? new Size(size.Height, size.Width) : size;
        Flex(box, Avail.Exact(frame.Width), Avail.Exact(frame.Height), perform: false);
        var width = _laidOutWidth;
        (_widthOf, _laidOutWidth) = (outerItem, outerWidth);
        return width;
    }

    static Node? FirstLayoutChild(Node parent)
    {
        for (var child = parent._first; child is not null; child = child._next)
        {
            if (child.IsTransparent)
            {
                if (FirstLayoutChild(child) is { } inner)
                    return inner;
            }
            else if (
                !child.Place.Collapsed
                && child.Place.Position != Position.Absolute
                && child.Kind != NodeKind.Layer
            )
                return child;
        }
        return null;
    }

    static float Definite(float value, float fallback) => float.IsNaN(value) ? fallback : value;

    // Min beats max; NaN stays NaN.
    static float Clamp(float value, float min, float max) =>
        float.IsNaN(value) ? value : MathF.Max(min, MathF.Min(value, max));

    struct FlexItem
    {
        public Node Node;
        public float Base;
        public float Hypothetical;
        public float Min;
        public float Max;
        public float Target;
        public float Cross;
        public float Ascent;
        public float Violation;
        public float Grow;
        public float Shrink;
        public float MarginMainStart;
        public float MarginMainEnd;
        public float MarginCrossStart;
        public float MarginCrossEnd;
        public Avail CrossAvail;
        public Align Align;
        public bool Frozen;
        public bool MinPending;
    }

    struct FlexLine
    {
        public int Start;
        public int Count;
        public float Main;
        public float Cross;
        public float Ascent;
    }
}

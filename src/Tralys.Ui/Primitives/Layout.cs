using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Rendering;

namespace Tralys.Ui;

/// <summary>The space a node is measured in along one axis, as CSS's available space: a definite size it may take up
/// to, or none, asking for its content's narrowest or widest size. The default is a definite 0.</summary>
[FramePath]
public readonly struct AvailableSpace
{
    readonly Kind _kind;
    readonly float _value;

    AvailableSpace(Kind kind, float value)
    {
        _kind = kind;
        _value = value;
    }

    public static AvailableSpace Definite(float size) => new(Kind.Definite, size);

    public static AvailableSpace MinContent => new(Kind.MinContent, 0f);

    public static AvailableSpace MaxContent => new(Kind.MaxContent, float.PositiveInfinity);

    public bool IsDefinite => _kind == Kind.Definite;

    public bool IsMinContent => _kind == Kind.MinContent;

    public bool IsMaxContent => _kind == Kind.MaxContent;

    /// <summary>The definite size; 0 under <see cref="MinContent"/> and +∞ under <see cref="MaxContent"/>, so it
    /// bounds what fits either way.</summary>
    public float Value => _value;

    internal static AvailableSpace Of(Avail avail) =>
        avail.Kind switch
        {
            AvailKind.MinContent => MinContent,
            AvailKind.MaxContent => MaxContent,
            _ => Definite(avail.Value),
        };

    internal Avail ToAvail() =>
        _kind switch
        {
            Kind.MinContent => Avail.MinContent,
            Kind.MaxContent => Avail.MaxContent,
            _ => Avail.AtMost(_value),
        };

    enum Kind : byte
    {
        Definite,
        MinContent,
        MaxContent,
    }
}

/// <summary>A custom layout: it measures a <see cref="LayoutPrimitive.Layout{TState}"/> node and places its children
/// from the node's state, measuring the children it needs inside its own calls. One static readonly instance serves
/// every node; it keeps nothing between calls.</summary>
public abstract class LayoutAlgorithm<TState>
{
    /// <summary>The node's border-box size in the space it is offered, its <see cref="LayoutContext.Padding"/>
    /// included. An axis the node's own size fixes takes that size whatever this returns.</summary>
    [FramePath]
    public abstract Size Measure(
        ref LayoutContext cx,
        TState state,
        AvailableSpace width,
        AvailableSpace height
    );

    /// <summary>Places every child with <see cref="LayoutContext.PlaceChild"/> in a node whose border box is
    /// <paramref name="size"/>.</summary>
    [FramePath]
    public abstract void Arrange(ref LayoutContext cx, TState state, Size size);
}

/// <summary>A <see cref="LayoutAlgorithm{TState}"/>'s view of its node: the children it lays out, in order, by index,
/// leaving out collapsed ones and absolute ones, which the core places against the padding box. It applies no child's
/// margins or alignment: the algorithm places border boxes.</summary>
[FramePath]
public ref struct LayoutContext
{
    readonly UiView _view;
    readonly Node _node;
    readonly LayoutPayload _payload;
    readonly int _start;
    readonly bool _arranging;

    internal LayoutContext(
        UiView view,
        Node node,
        LayoutPayload payload,
        int start,
        int count,
        Thickness padding,
        bool arranging
    )
    {
        _view = view;
        _node = node;
        _payload = payload;
        _start = start;
        ChildCount = count;
        Padding = padding;
        _arranging = arranging;
    }

    public readonly int ChildCount { get; }

    /// <summary>From the border box in to the content box: the border's widths, then the padding.</summary>
    public readonly Thickness Padding { get; }

    /// <summary>The border-box size of child <paramref name="index"/> in the space given; a definite space bounds an
    /// auto-sized child, which takes no more.</summary>
    /// <exception cref="InvalidOperationException">A definite space is not finite.</exception>
    public readonly Size MeasureChild(int index, AvailableSpace width, AvailableSpace height)
    {
        var child = Child(index);
        if (!float.IsFinite(width.Value) && width.IsDefinite)
            throw Faults.CustomLayout(
                _payload.AlgorithmType,
                "measured a child in a non-finite width"
            );
        if (!float.IsFinite(height.Value) && height.IsDefinite)
            throw Faults.CustomLayout(
                _payload.AlgorithmType,
                "measured a child in a non-finite height"
            );
        return _view.Measure(child, width.ToAvail(), height.ToAvail());
    }

    /// <summary>Places child <paramref name="index"/> with its border box at <paramref name="borderBox"/>, in the
    /// node's border-box space, a negative extent taken as 0; <paramref name="transform"/> applies about that box's
    /// top-left corner, after the child's own transform, to paint, hit testing and event positions, as a scale-to-fit's
    /// scale does, while its bounds stay the box. Only inside <see cref="LayoutAlgorithm{TState}.Arrange"/>.</summary>
    /// <exception cref="InvalidOperationException">It is called while measuring, or the box or transform is not
    /// finite.</exception>
    public readonly void PlaceChild(int index, Rect borderBox, Transform transform = default)
    {
        var child = Child(index);
        if (!_arranging)
            throw Faults.CustomLayout(_payload.AlgorithmType, "placed a child while measuring");
        if (
            !float.IsFinite(borderBox.X)
            || !float.IsFinite(borderBox.Y)
            || !float.IsFinite(borderBox.Width)
            || !float.IsFinite(borderBox.Height)
            || !Node.Finite(transform.Matrix)
        )
            throw Faults.CustomLayout(_payload.AlgorithmType, "placed a child at a non-finite box");
        _view.PlaceCustom(_node, child, borderBox, transform);
    }

    /// <summary>Leaves child <paramref name="index"/> out of this arrangement: it draws, hits and takes focus as a
    /// hidden child does, keeping its last bounds, until an arrangement places it again. Only inside
    /// <see cref="LayoutAlgorithm{TState}.Arrange"/>, for a child the algorithm chose not to show, such as an
    /// alternative that doesn't fit.</summary>
    /// <exception cref="InvalidOperationException">It is called while measuring.</exception>
    public readonly void HideChild(int index)
    {
        var child = Child(index);
        if (!_arranging)
            throw Faults.CustomLayout(_payload.AlgorithmType, "hid a child while measuring");
        _view.LayOutHidden(child, true);
    }

    readonly Node Child(int index)
    {
        if ((uint)index >= (uint)ChildCount)
            throw Faults.OutOfRange(nameof(index), index, "There is no child at this index.");
        return _view.LayoutChild(_start + index);
    }
}

[FramePath]
internal abstract class LayoutPayload : NodePayload
{
    internal abstract Type AlgorithmType { get; }

    internal abstract Size Measure(
        ref LayoutContext cx,
        AvailableSpace width,
        AvailableSpace height
    );

    internal abstract void Arrange(ref LayoutContext cx, Size size);
}

[FramePath]
internal sealed class LayoutPayload<TState> : LayoutPayload
{
    LayoutAlgorithm<TState>? _algorithm;
    TState? _state;
    HeldText _held;

    [MemberNotNullWhen(true, nameof(_algorithm), nameof(_state))]
    bool Written => _algorithm is not null;

    internal bool Write(LayoutAlgorithm<TState> algorithm, TState state)
    {
        if (
            ReferenceEquals(_algorithm, algorithm)
            && EqualityComparer<TState>.Default.Equals(_state, state)
        )
            return false;
        _algorithm = algorithm;
        _state = _held.Hold(state);
        return true;
    }

    internal override Type AlgorithmType => Written ? _algorithm.GetType() : throw Unwritten();

    internal override Size Measure(
        ref LayoutContext cx,
        AvailableSpace width,
        AvailableSpace height
    ) => Written ? _algorithm.Measure(ref cx, _state, width, height) : throw Unwritten();

    internal override void Arrange(ref LayoutContext cx, Size size)
    {
        if (!Written)
            throw Unwritten();
        _algorithm.Arrange(ref cx, _state, size);
    }

    static InvalidOperationException Unwritten() =>
        new("A custom layout lays out only after its primitive wrote it.");

    public override void Release(UiView view)
    {
        _algorithm = null;
        _state = default;
        Pool<LayoutPayload<TState>>.Return(this);
    }
}

/// <summary>The custom layout primitive.</summary>
[FramePath]
public static class LayoutPrimitive
{
    /// <summary>A node <paramref name="algorithm"/> measures and whose children it places, from
    /// <paramref name="state"/>, compared with <see cref="EqualityComparer{T}.Default"/>: an unequal state lays it
    /// out again. It draws a box's decoration, and its block holds its children.</summary>
    public static Scope Layout<TState>(
        this Ui ui,
        LayoutAlgorithm<TState> algorithm,
        TState state,
        Thickness padding = default,
        Paint background = default,
        Border border = default,
        CornerRadius radius = default,
        BoxShadow shadow = default,
        Paint mask = default,
        float grow = 0,
        float? shrink = null,
        Length basis = default,
        AlignSelf alignSelf = default,
        AlignSelf justifySelf = default,
        Thickness margin = default,
        Position position = default,
        Length left = default,
        Length top = default,
        Length right = default,
        Length bottom = default,
        int zIndex = 0,
        int column = 0,
        int row = 0,
        int columnSpan = 0,
        int rowSpan = 0,
        bool hidden = false,
        bool collapsed = false,
        string? id = null,
        Length width = default,
        Length height = default,
        Length minWidth = default,
        Length minHeight = default,
        Length maxWidth = default,
        Length maxHeight = default,
        float aspectRatio = 0,
        float? opacity = null,
        Transform transform = default,
        Vector2? transformOrigin = null,
        Cursor? cursor = null,
        PointerEvents pointerEvents = default,
        NodeRef? nodeRef = null,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        ArgumentNullException.ThrowIfNull(algorithm);
        var b = ui.Builder;
        if (b.Discarding)
            return default;
        var depth = b.FrameDepth;
        var hooks = b.HookDepth;
        var node = b.Claim(NodeKind.Layout, null, key, file, line, out _);
        var view = b.View;
        if (node.Payload is not LayoutPayload<TState> payload)
        {
            node.Payload?.Release(view);
            payload = Pool<LayoutPayload<TState>>.TryRent() ?? new LayoutPayload<TState>();
            node.Payload = payload;
        }
        var changed = payload.Write(algorithm, state);
        if (changed)
            view.InvalidateMeasure(node);
        Primitives.Write(
            view,
            node,
            new LayoutStyle
            {
                Padding = padding,
                Border = border.Width,
                Width = width,
                Height = height,
                MinWidth = minWidth,
                MinHeight = minHeight,
                MaxWidth = maxWidth,
                MaxHeight = maxHeight,
                AspectRatio = aspectRatio,
            },
            new VisualStyle
            {
                Background = background,
                BorderPaint = border.Paint,
                Radius = radius,
                Shadow = shadow,
                Mask = mask,
                Opacity = opacity,
                Transform = transform,
                TransformOrigin = transformOrigin,
            },
            new InheritStyle { PointerEvents = pointerEvents },
            new Placement
            {
                Grow = grow,
                Shrink = shrink,
                Basis = basis,
                AlignSelf = alignSelf,
                JustifySelf = justifySelf,
                Margin = margin,
                Position = position,
                Left = left,
                Top = top,
                Right = right,
                Bottom = bottom,
                ZIndex = zIndex,
                Column = column,
                Row = row,
                ColumnSpan = columnSpan,
                RowSpan = rowSpan,
                Hidden = hidden,
                Collapsed = collapsed,
                Id = id,
            },
            false,
            nodeRef,
            changed | node.WriteClick(default, cursor)
        );
        b.Push(node);
        return new Scope(b, depth, hooks);
    }
}

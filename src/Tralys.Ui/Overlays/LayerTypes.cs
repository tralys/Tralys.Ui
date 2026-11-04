using System.Numerics;

namespace Tralys.Ui;

/// <summary>A layer's band above the root: tiers draw in this order, then by opening order within a tier.</summary>
public enum LayerTier : byte
{
    Default,
    Toast,
    Tooltip,
}

/// <summary>Whether a layer's content takes pointer input.</summary>
public enum LayerHit : byte
{
    Content,

    /// <summary>Nothing in the layer hits; input goes to what lies beneath.</summary>
    PassThrough,
}

/// <summary>What dismisses a layer: each reports through its <c>onDismiss</c>, and the owner closes it by no longer
/// rendering it.</summary>
[Flags]
public enum Dismiss : byte
{
    None = 0,

    /// <summary>An Escape (or gamepad East) whose default no handler prevented.</summary>
    Escape = 1,

    /// <summary>A press outside the layer's content.</summary>
    OutsidePress = 2,

    /// <summary>Focus leaving both the owner and the layer while the window is active.</summary>
    FocusOutside = 4,

    /// <summary>A positioner's anchor unmounted, hidden, collapsed or clipped out of view.</summary>
    AnchorHidden = 8,
}

/// <summary>What kind of layer a layer root is, as it last rendered: its band, whether its content takes pointer
/// input, what dismisses it, and whether its root is modal, so a host tells a tooltip or a pass-through overlay from a
/// dropdown or a dialog among <see cref="UiView.Layers"/>.</summary>
public readonly record struct LayerInfo(
    LayerTier Tier,
    LayerHit Hit,
    Dismiss DismissOn,
    bool Modal
);

/// <summary>What a positioner places its layer against; the default is the node the layer is written in.</summary>
[FramePath]
public readonly struct Anchor : IEquatable<Anchor>
{
    readonly AnchorKind _kind;
    readonly NodeRef? _node;
    readonly Rect _rect;

    Anchor(AnchorKind kind, NodeRef? node, Rect rect)
    {
        _kind = kind;
        _node = node;
        _rect = rect;
    }

    internal AnchorKind Kind => _kind;

    internal NodeRef? Node => _node;

    internal Rect Area => _rect;

    public static implicit operator Anchor(NodeRef node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new Anchor(AnchorKind.Node, node, default);
    }

    /// <summary>Part of <paramref name="node"/>, in its own border-box space; a zero size is a point in it.</summary>
    public static Anchor Within(NodeRef node, Rect local)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new Anchor(AnchorKind.Within, node, local);
    }

    /// <summary>A point in view space.</summary>
    public static Anchor Point(Vector2 viewPoint) =>
        new(AnchorKind.View, null, new Rect(viewPoint.X, viewPoint.Y, 0f, 0f));

    /// <summary>A rectangle in view space.</summary>
    public static Anchor Rect(Rect viewRect) => new(AnchorKind.View, null, viewRect);

    /// <summary>Where the pointer was when the layer mounted.</summary>
    public static Anchor Pointer => new(AnchorKind.Pointer, null, default);

    public bool Equals(Anchor other) =>
        _kind == other._kind && ReferenceEquals(_node, other._node) && _rect == other._rect;

    public override bool Equals(object? obj) => obj is Anchor other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_kind, _node, _rect);

    public static bool operator ==(Anchor left, Anchor right) => left.Equals(right);

    public static bool operator !=(Anchor left, Anchor right) => !left.Equals(right);
}

internal enum AnchorKind : byte
{
    Owner,
    Node,
    Within,
    View,
    Pointer,
}

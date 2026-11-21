using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Tralys.Ui;

[FramePath]
internal record struct LayerProps
{
    public LayerHit Hit;
    public Callback OnDismiss;
    public Dismiss DismissOn;
    public bool RestoreFocus;
    public bool AutoFocus;
    public LayerTier Tier;
    public NodeRef? DismissExempt;
    public NodeRef? Content;
    public bool Positioned;
    public Anchor Anchor;
    public PlacementMode? Placement;
    public float SideOffset;
    public Vector2 Offset;
    public bool MatchAnchorWidth;
    public Thickness CollisionPadding;
}

[FramePath]
internal sealed class LayerPayload : NodePayload
{
    public readonly FocusScope Scope = new();
    public LayerProps Props;
    public Vector2 PointerAt;
    public Rect AnchorRect;
    public Rect PlacedAgainst;
    public Rect PlacedWithin;
    public Rect Area;
    public bool Bounded;
    public Node? Boundary;
    public bool HasAnchor;
    public bool AnchorValid;
    public bool Placed;
    public bool AnchorLostPosted;
    public bool FocusWithinOwner;

    public bool Write(UiView view, in LayerProps props)
    {
        if (Props == props)
            return false;
        Props = props;
        view.MarkComposite();
        return true;
    }

    public override void Release(UiView view)
    {
        Scope.Clear();
        Props = default;
        PointerAt = default;
        AnchorRect = PlacedAgainst = PlacedWithin = Area = default;
        HasAnchor = AnchorValid = Placed = AnchorLostPosted = FocusWithinOwner = Bounded = false;
        Boundary = null;
        Pool<LayerPayload>.Return(this);
    }
}

[FramePath]
internal sealed class FocusScope
{
    public Node? Node;
    public int Generation;
    public UiView? View;
    public LayerPayload? Layer;
    public Node? Opener;
    public int OpenerGeneration;
    public ValueList<Node> OpenerAncestors;
    public ValueList<int> OpenerAncestorGenerations;
    public Node? Deferred;
    public int DeferredGeneration;
    public bool Shown;
    public bool Active;
    public bool EnterPending;

    [MemberNotNullWhen(true, nameof(Node))]
    public bool IsLive => Node is not null && Node.Generation == Generation && Node.IsMounted;

    public void Init(UiView view, Node node, LayerPayload? layer)
    {
        View = view;
        Node = node;
        Generation = node.Generation;
        Layer = layer;
    }

    // An opener that leaves the tree loses its parent link, so its ancestors are kept as they stand now.
    public void Opened(Node opener)
    {
        Opener = opener;
        OpenerGeneration = opener.Generation;
        OpenerAncestors.Clear();
        OpenerAncestorGenerations.Clear();
        for (var a = opener._parent; a is not null; a = a._parent)
        {
            if (!a.IsFocusable)
                continue;
            OpenerAncestors.Add(a);
            OpenerAncestorGenerations.Add(a.Generation);
        }
    }

    public void Clear()
    {
        Node = null;
        View = null;
        Layer = null;
        Generation = 0;
        OpenerAncestors.Clear();
        OpenerAncestorGenerations.Clear();
        Opener = Deferred = null;
        OpenerGeneration = DeferredGeneration = 0;
        Shown = Active = EnterPending = false;
    }
}

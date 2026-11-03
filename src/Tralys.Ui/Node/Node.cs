namespace Tralys.Ui;

/// <summary>A node of the one tree that identity, context, bubbling, layout and paint all walk. Primitives and
/// components write them; nothing else does.</summary>
[FramePath]
public sealed partial class Node
{
    internal Node? _parent;
    internal Node? _first;
    internal Node? _last;
    internal Node? _next;
    internal Node? _prev;
    internal int Depth;
    internal UiView? View;
    internal int Generation;
    internal bool IsMounted;

    internal string? File;
    internal int Line;
    internal UiKey Key;
    internal int Occurrence;

    internal LayoutStyle Layout;
    internal VisualStyle Visual;
    internal InheritStyle Inherit;
    internal Placement Place;
    internal Interaction? Interaction;
    internal NodePayload? Payload;
    internal ComponentHost? Host;
    internal HookList? Hooks;
    internal NodeRef? Ref;

    internal Dirty Dirty;
    internal bool EnvBelow;
    internal bool FaultHidden;
    internal bool LayoutHidden;
    internal bool Exiting;
    internal bool PaintQueued;
    internal InputStates _states;
    internal InputStates ForcedByView;
    internal byte HoverCount;
    internal byte PressCount;
    internal byte CommandRefusals;

    internal Node(NodeKind kind) => Kind = kind;

    public NodeKind Kind { get; }

    /// <summary>The logical parent.</summary>
    public Node? Parent => _parent;

    public ChildEnumerator Children => new(this);

    public string? Id => Place.Id;

    /// <summary>The component type a <see cref="NodeKind.Component"/> node hosts.</summary>
    public Type? Component => Host?.ComponentType;

    /// <summary>The border box, in the parent layout node's border-box space, snapped to device pixels.</summary>
    public Rect Bounds => _bounds;

    /// <summary>The border box in view space, transforms ignored.</summary>
    public Rect ViewBounds => _viewBounds;

    public InputStates States => _states;

    /// <summary>Where the node sits in its parent, as written; a component's root node holds its caller's.</summary>
    public ref readonly Placement Placement => ref Place;

    /// <summary>Whether nothing hides the node: no node here or above, a layer's owner included, is hidden or
    /// collapsed, faulted, or left out of an arrangement by the custom layout around it, as <c>ui.IsShown</c> reads
    /// for a component.</summary>
    public bool IsShown => UiView.Shown(this);

    /// <summary>Whether the custom layout around the node left it out of its last arrangement
    /// (<see cref="LayoutContext.HideChild"/>), so it draws, hits and focuses as a <c>hidden</c> one does, while its
    /// placement stays as written.</summary>
    public bool IsLayoutHidden => LayoutHidden;

    /// <summary>A text node's text, all its runs included; a span's or link's own text; an editable text's text as
    /// edited.</summary>
    public ReadOnlySpan<char> Text =>
        Payload switch
        {
            TextPayload text => text.Buffer.Text,
            SpanPayload span => span.Text,
            EditorPayload editor => editor.Text,
            _ => default,
        };

    /// <summary>What kind of layer a layer root is; null for any other node.</summary>
    public LayerInfo? Layer =>
        Payload is LayerPayload layer
            ? new LayerInfo(
                layer.Props.Tier,
                layer.Props.Hit,
                layer.Props.DismissOn,
                Interaction?.Props.Modal == true
            )
            : null;

    /// <summary>The props a component node last rendered with.</summary>
    public TProps Props<TProps>()
        where TProps : struct =>
        Host is IPropsHost<TProps> host
            ? host.Props
            : throw new InvalidOperationException("This node hosts no component with these props.");

    internal bool IsTransparent =>
        Kind is NodeKind.Component or NodeKind.Fragment or NodeKind.ActionScope;

    internal Node? LayoutParent
    {
        get
        {
            var parent = _parent;
            while (parent is not null && parent.IsTransparent)
                parent = parent._parent;
            return parent;
        }
    }

    internal bool IsAncestorOrSelfOf(Node? node)
    {
        for (var n = node; n is not null; n = n._parent)
        {
            if (n == this)
                return true;
        }
        return false;
    }

    internal void InsertAfter(Node? previous, Node child)
    {
        child._parent = this;
        child.Depth = Depth + 1;
        child._prev = previous;
        child._next = previous is null ? _first : previous._next;
        if (child._next is null)
            _last = child;
        else
            child._next._prev = child;
        if (previous is null)
            _first = child;
        else
            previous._next = child;
    }

    internal void Unlink(Node child)
    {
        if (child._prev is null)
            _first = child._next;
        else
            child._prev._next = child._next;
        if (child._next is null)
            _last = child._prev;
        else
            child._next._prev = child._prev;
        child._prev = null;
        child._next = null;
        child._parent = null;
    }

    internal void SetStates(InputStates states)
    {
        if (_states == states)
            return;
        var changed = _states ^ states;
        _states = states;
        View?.StatesChanged(this, changed);
    }

    internal void Reset()
    {
        _parent = _first = _last = _next = _prev = null;
        Depth = 0;
        View = null;
        Generation++;
        IsMounted = false;
        File = null;
        Line = 0;
        Key = default;
        Occurrence = 0;
        Layout = default;
        Visual = default;
        Inherit = default;
        Place = default;
        Interaction = null;
        Payload = null;
        Host = null;
        Hooks = null;
        Ref = null;
        Dirty = Dirty.None;
        EnvBelow = false;
        FaultHidden = false;
        LayoutHidden = false;
        Exiting = false;
        PaintQueued = false;
        _states = InputStates.None;
        ForcedByView = InputStates.None;
        HoverCount = 0;
        PressCount = 0;
        CommandRefusals = 0;
        ResetLayout();
        ResetScroll();
        ResetFade();
    }

    /// <summary>Walks a node's children in order.</summary>
    public struct ChildEnumerator
    {
        readonly Node _parent;
        Node? _current;
        bool _started;

        internal ChildEnumerator(Node parent)
        {
            _parent = parent;
            _current = null;
            _started = false;
        }

        public readonly Node Current => _current!;

        /// <summary>How many children there are, counted by walking them.</summary>
        public readonly int Count
        {
            get
            {
                var count = 0;
                for (var child = _parent._first; child is not null; child = child._next)
                    count++;
                return count;
            }
        }

        /// <summary>The child at <paramref name="index"/>, found by walking from the first.</summary>
        /// <exception cref="ArgumentOutOfRangeException">There are no more than <paramref name="index"/> children.</exception>
        public readonly Node this[int index]
        {
            get
            {
                ArgumentOutOfRangeException.ThrowIfNegative(index);
                var child = _parent._first;
                for (var i = 0; i < index && child is not null; i++)
                    child = child._next;
                return child ?? throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public bool MoveNext()
        {
            _current = _started ? _current?._next : _parent._first;
            _started = true;
            return _current is not null;
        }

        public readonly ChildEnumerator GetEnumerator() => this;
    }
}

[FramePath]
internal sealed class Interaction
{
    public InteractionProps Props;
}

[FramePath]
internal abstract class NodePayload
{
    public abstract void Release(UiView view);
}

internal interface IPropsHost<TProps>
    where TProps : struct
{
    TProps Props { get; }
}

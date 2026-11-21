using Tralys.Ui.Rendering;

namespace Tralys.Ui;

/// <summary>A tree of nodes on one <see cref="UiContext"/>, updated, laid out and drawn by its host each frame.</summary>
[FramePath]
public sealed partial class UiView : IRenderedView, IDisposable, ITextureResolver
{
    const int MaxEffectRounds = 64;
    const int MaxInputSettles = 4;
    static readonly int KindCount = Enum.GetValues<NodeKind>().Length;

    readonly IViewHost _host;
    readonly Node _root;
    internal readonly Builder Builder;
    internal readonly FrameArena Arena = new();

    // Per kind, so a recycled node's recording capacity already fits what it records next.
    readonly Stack<Node>?[] _nodes = new Stack<Node>?[KindCount];
    readonly Stack<HookList> _hookLists = new();
    readonly Stack<Interaction> _interactions = new();
    ValueList<ComponentHost> _dirtyHosts;
    ValueList<ComponentHost> _deferred;
    ValueList<EffectHook> _effects;
    ValueList<NodeRef> _layoutNotices;
    ValueList<Node> _unmounted;
    Size _size;
    float _scale;
    Thickness _safeInsets;
    float _keyboardInset;
    bool _disposed;
    internal int UpdateStamp;
    internal int TreeStamp;
    internal List<Node>? Recorded;
    internal ViewStats Stats;

    /// <param name="context">The context it runs on, whose thread it belongs to.</param>
    /// <param name="host">The window hosting it.</param>
    /// <param name="scale">Device pixels per layout unit.</param>
    public UiView(UiContext context, IViewHost host, float scale = 1f)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(host);
        context.Dispatcher.VerifyAccess();
        Context = context;
        _host = host;
        _scale = ValidScale(scale);
        Builder = new Builder(this);
        _root = new Node(NodeKind.Root) { View = this, IsMounted = true };
        _root.Dirty = Dirty.Layout | Dirty.Env;
        _root.EnvBelow = true;
        context.Attach(this);
    }

    public UiContext Context { get; }

    /// <summary>The view's size in layout units.</summary>
    public Size Size
    {
        get => _size;
        set
        {
            if (
                !(value.Width >= 0f && value.Height >= 0f)
                || !float.IsFinite(value.Width)
                || !float.IsFinite(value.Height)
            )
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "A view's size is finite and not negative."
                );
            if (_size == value)
                return;
            _size = value;
            InvalidateMeasure(_root);
            InvalidateEnv(_root, EnvReads.ViewSize);
            _compositeDirty = true;
        }
    }

    /// <summary>Device pixels per layout unit; a change lays out and records everything again.</summary>
    public float Scale
    {
        get => _scale;
        set
        {
            value = ValidScale(value);
            if (_scale == value)
                return;
            _scale = value;
            InvalidateAll(_root);
            _compositeDirty = true;
        }
    }

    /// <summary>The area system UI covers at the view's edges. Read through <see cref="Ui.SafeInsets"/>.</summary>
    public Thickness SafeInsets
    {
        get => _safeInsets;
        set
        {
            if (_safeInsets == value)
                return;
            _safeInsets = value;
            InvalidateEnv(_root, EnvReads.Insets);
            if (_layers.Count > 0)
                _root.Dirty |= Dirty.Layout;
        }
    }

    /// <summary>How far an on-screen keyboard covers the view's bottom. Read through <see cref="Ui.KeyboardInset"/>.</summary>
    public float KeyboardInset
    {
        get => _keyboardInset;
        set
        {
            if (_keyboardInset == value)
                return;
            _keyboardInset = value;
            InvalidateEnv(_root, EnvReads.Insets);
        }
    }

    /// <summary>The view's root: a view-sized box the mounted component fills.</summary>
    public Node Root => _root;

    public int PixelWidth => (int)MathF.Ceiling(_size.Width * _scale);

    public int PixelHeight => (int)MathF.Ceiling(_size.Height * _scale);

    /// <summary>Whether nothing is left to render, lay out or animate to an end; forever loops and sprites don't
    /// count.</summary>
    public bool IsIdle =>
        _dirtyHosts.Count == 0
        && _deferred.Count == 0
        && _reconciling.Count == 0
        && (_root.Dirty & Dirty.Layout) == 0
        && !_root.EnvBelow
        && MotionsIdle
        && ScrollSettled
        && !SizeClassDue;

    /// <summary>Whether a frame would change anything: something to render, lay out, record, run, loop or play, or a
    /// caret blinking, a hold or held D-pad coming due, or the wall clock crossing what a component waits on.</summary>
    public bool NeedsFrame =>
        HasWork
        || CaretBlinks
        || LoopsRunning
        || SpritesRunning
        || InputNeedsFrame
        || Context.ClockRang;

    internal bool HasWork =>
        !IsIdle || Context.Dispatcher.HasWork || _paintDirty.Count > 0 || _compositeDirty;

    /// <summary>Mounts <typeparamref name="TComponent"/> as the root, replacing what was mounted.</summary>
    public void Mount<TComponent, TProps>(in TProps props)
        where TComponent : Component<TProps>, new()
        where TProps : struct, IEquatable<TProps>
    {
        VerifyAccess();
        try
        {
            Builder.Push(_root);
            Builder.Component<TComponent, TProps>(props, default, default, "", 0);
            Builder.Component<DragPreview, DragPreviewProps>(default, default, default, "", 1);
            Builder.Close(0, 0);
        }
        finally
        {
            Builder.Reset();
        }
    }

    /// <summary>Mounts <paramref name="root"/> as the root's content, replacing what was mounted.</summary>
    public void Mount(Slot root) =>
        Mount<RootComponent, RootProps>(new RootProps { Content = root });

    internal void RerenderRoot()
    {
        for (var child = _root._first; child is not null; child = child._next)
        {
            if (child.Host is { } host && host.ComponentType != typeof(DragPreview))
                host.Invalidate();
        }
    }

    /// <summary>Runs posted work, renders what changed, lays out, runs effects, and settles input against the new
    /// layout, rendering again what that changes, so the frame drawn next shows the focus and hover it settled.</summary>
    public void Update()
    {
        VerifyAccess();
        UpdateStamp++;
        try
        {
            Context.Dispatcher.Drain(Context.OnFault);
            UpdateSizeClass();
            Arena.Reset();
            FinishExits();
            Settle();
            var cleaned = CleanUpUnmounted(0);
            SyncScopes();
            var waiting = _dirtyHosts.Count;
            InputStep();
            // A hover that moves its own node never settles, so the cap stops without throwing.
            for (var pass = 0; pass < MaxInputSettles && _dirtyHosts.Count > waiting; pass++)
            {
                Settle();
                cleaned = CleanUpUnmounted(cleaned);
                SyncScopes();
                waiting = _dirtyHosts.Count;
                InputSettle();
            }
            UpdateEditors();
            ReleaseUnmounted();
            for (var i = 0; i < _layoutNotices.Count; i++)
                _layoutNotices[i].NotifyLaidOut();
            _layoutNotices.Clear();
            DeliverScrollNotices();
            for (var i = 0; i < _deferred.Count; i++)
            {
                if (_deferred[i].IsMounted && _deferred[i].Dirty)
                    _dirtyHosts.Add(_deferred[i]);
            }
            _deferred.Clear();
            Context.ScheduleWake();
        }
        finally
        {
            Builder.Reset();
            _updatesDone++;
        }
    }

    void Settle()
    {
        for (var round = 0; ; round++)
        {
            RenderDirty();
            ReconcileEditors();
            Layout();
            LayoutLayers();
            SettleScroll();
            if (!RunEffects())
                return;
            if (round == MaxEffectRounds)
                throw new InvalidOperationException(
                    "Effects kept invalidating renders for 64 rounds in one update."
                );
        }
    }

    void RenderDirty()
    {
        while (_dirtyHosts.Count > 0)
        {
            SortDirty();
            var count = _dirtyHosts.Count;
            for (var i = 0; i < count; i++)
            {
                var host = _dirtyHosts[i];
                if (host.Dirty && host.IsMounted)
                    Builder.Render(host, fromCaller: false);
            }
            _dirtyHosts.RemoveFirst(count);
        }
    }

    // Ancestors first, so a host an ancestor renders inline renders once.
    void SortDirty()
    {
        var hosts = _dirtyHosts.AsSpan();
        for (var i = 1; i < hosts.Length; i++)
        {
            var host = hosts[i];
            var j = i - 1;
            while (j >= 0 && Before(host.Node, hosts[j].Node))
            {
                hosts[j + 1] = hosts[j];
                j--;
            }
            hosts[j + 1] = host;
        }
    }

    static bool Before(Node? a, Node? b)
    {
        if (a is null || b is null || a == b)
            return false;
        if (a.Depth != b.Depth)
            return a.Depth < b.Depth;
        while (a._parent != b._parent)
        {
            if (a._parent is null || b._parent is null)
                return false;
            a = a._parent;
            b = b._parent;
        }
        for (Node? next = a._next, prev = a._prev; next is not null || prev is not null; )
        {
            if (next == b)
                return true;
            if (prev == b)
                return false;
            next = next?._next;
            prev = prev?._prev;
        }
        return false;
    }

    internal void HostDirtied(ComponentHost host) => _dirtyHosts.Add(host);

    internal void Defer(ComponentHost host)
    {
        host.Dirty = true;
        _deferred.Add(host);
    }

    internal void CountRender(ComponentHost host)
    {
        host.RenderCount++;
        Stats.Renders++;
        Stats.CountType(host.TypeId);
    }

    internal void QueueEffect(EffectHook effect) => _effects.Add(effect);

    bool RunEffects()
    {
        if (_effects.Count == 0)
            return false;
        var count = _effects.Count;
        for (var i = 0; i < count; i++)
        {
            var effect = _effects[i];
            if (!effect.Pending)
                continue;
            try
            {
                effect.Run(this);
            }
            catch (Exception e) when (Context.OnFault is not null)
            {
                Context.OnFault(e);
            }
        }
        _effects.RemoveFirst(count);
        return _dirtyHosts.Count > 0 || _effects.Count > 0;
    }

    internal void QueueLayoutNotice(NodeRef nodeRef) => _layoutNotices.Add(nodeRef);

    internal Node RentNode(NodeKind kind)
    {
        if (_nodes[(int)kind] is not { Count: > 0 } spare)
        {
            PoolMisses.Add();
            return new Node(kind) { View = this };
        }
        var node = spare.Pop();
        node.View = this;
        return node;
    }

    internal HookList RentHookList()
    {
        if (_hookLists.TryPop(out var list))
            return list;
        PoolMisses.Add();
        return new HookList();
    }

    internal Interaction RentInteraction()
    {
        if (_interactions.TryPop(out var interaction))
            return interaction;
        PoolMisses.Add();
        return new Interaction();
    }

    internal void Mounted(Node node)
    {
        node.IsMounted = true;
        var parent = node._parent;
        if (parent is not null && (parent._states & InputStates.Disabled) != 0)
            node._states |= InputStates.Disabled;
        node.Dirty |= Dirty.Measure | Dirty.Layout | Dirty.Env | Dirty.Paint;
        MarkEnvBelow(node);
        if (parent is not null)
            ChildrenChanged(parent);
        Stats.Mounts++;
    }

    internal void Unmount(Node node)
    {
        var parent = node._parent;
        Departed(node);
        Detach(node);
        if (parent is not null)
        {
            parent.Unlink(node);
            ChildrenChanged(parent);
        }
        _unmounted.Add(node);
    }

    void Detach(Node node)
    {
        Stats.Unmounts++;
        node.IsMounted = false;
        if (node.Host is { } host)
            host.Unmounted();
        if (node.Payload is CanvasPayload canvas)
            canvas.Detach();
        if (node.Payload is EditorPayload editor)
            EditorDetached(editor);
        if (node.Kind == NodeKind.Layer)
            LayerClosed(node);
        if (node.Scroller is not null || node.ScrollProps.FollowScroll is not null)
            DetachScroll(node);
        if (node.PaintQueued)
            RemovePaintDirty(node);
        if (node == _gapsOf)
            _gapsOf = null;
        for (var child = node._first; child is not null; child = child._next)
            Detach(child);
    }

    int CleanUpUnmounted(int from)
    {
        for (var i = from; i < _unmounted.Count; i++)
            CleanUp(_unmounted[i]);
        return _unmounted.Count;
    }

    static void CleanUp(Node node)
    {
        for (var child = node._first; child is not null; child = child._next)
            CleanUp(child);
        node.Hooks?.DiscardFrom(0);
    }

    void ReleaseUnmounted()
    {
        for (var i = 0; i < _unmounted.Count; i++)
            Release(_unmounted[i]);
        _unmounted.Clear();
    }

    void Release(Node node)
    {
        var child = node._first;
        while (child is not null)
        {
            var next = child._next;
            Release(child);
            child = next;
        }
        node.Host?.Release();
        node.Payload?.Release(this);
        node.Ref?.Bind(null);
        if (node.Hooks is { } hooks)
        {
            hooks.Release();
            _hookLists.Push(hooks);
        }
        if (node.Interaction is { } interaction)
        {
            interaction.Props = default;
            _interactions.Push(interaction);
        }
        var kind = node.Kind;
        node.Reset();
        (_nodes[(int)kind] ??= new Stack<Node>()).Push(node);
    }

    internal void Faulted(ComponentHost host)
    {
        host.Faulted = true;
        if (host.Node is not { FaultHidden: false } node)
            return;
        node.FaultHidden = true;
        _compositeDirty = true;
        ShownBelowChanged(node);
    }

    internal void Unfaulted(ComponentHost host)
    {
        host.Faulted = false;
        if (host.Node is { } node)
        {
            node.FaultHidden = false;
            _compositeDirty = true;
            ShownBelowChanged(node);
        }
        WakeResting();
    }

    // The faulted host's own render is what threw, so only what it holds is told.
    static void ShownBelowChanged(Node node)
    {
        for (var child = node._first; child is not null; child = child._next)
            InvalidateEnv(child, EnvReads.Shown);
    }

    internal static void InvalidateEnv(Node node, EnvReads which)
    {
        if (node.Host is { } host && (host.EnvReads & which) != 0)
            host.Invalidate();
        for (var child = node._first; child is not null; child = child._next)
            InvalidateEnv(child, which);
    }

    internal void EnvironmentChanged(EnvReads which)
    {
        InvalidateEnv(_root, which);
        _root.Dirty |= Dirty.Env;
        MarkEnvBelow(_root);
    }

    static float ValidScale(float scale) =>
        scale > 0f && float.IsFinite(scale)
            ? scale
            : throw new ArgumentOutOfRangeException(
                nameof(scale),
                "A scale is positive and finite."
            );

    internal void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Context.Dispatcher.VerifyAccess();
    }

    RenderTexture? ITextureResolver.ResolveTexture(string key) => Context.ResolveTexture(key);

    /// <summary>Unmounts the root.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        Context.Dispatcher.VerifyAccess();
        var node = _root._first;
        while (node is not null)
        {
            var next = node._next;
            Unmount(node);
            node = next;
        }
        CleanUpUnmounted(0);
        DeliverNotices();
        ReleaseUnmounted();
        _disposed = true;
        Context.Detach(this);
    }
}

[FramePath]
internal struct ViewStats
{
    public int Renders;
    public int NodeWrites;
    public int Mounts;
    public int Unmounts;
    public int Layouts;
    public int Paints;
    public bool Rebuilt;
    int[]? _types;

    [FramePath]
    public void CountType(int id)
    {
        if (_types is null || _types.Length <= id)
            Array.Resize(ref _types, Math.Max(id + 1, 16));
        _types[id]++;
    }

    [FramePath]
    public readonly int RendersOf(int id) =>
        _types is not null && id < _types.Length ? _types[id] : 0;

    [FramePath]
    public void Clear()
    {
        Renders = NodeWrites = Mounts = Unmounts = Layouts = Paints = 0;
        Rebuilt = false;
        if (_types is not null)
            Array.Clear(_types);
    }
}

internal readonly record struct RootProps
{
    public Slot Content { get; init; }
}

internal sealed class RootComponent : Component<RootProps>
{
    protected override void Render(Ui ui, in RootProps p) => p.Content.Render(ui);
}

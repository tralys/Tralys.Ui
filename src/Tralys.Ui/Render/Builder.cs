using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

[FramePath]
internal sealed class Builder
{
    const int MaxRendersPerUpdate = 16;

    internal readonly UiView View;
    readonly Stack<Dictionary<Identity, Node>> _maps = new();
    readonly Stack<List<Node>> _orders = new();
    ValueList<Frame> _frames;
    ValueList<HookFrame> _hookFrames;
    int _faultDepth = int.MaxValue;
    int _hookFaultDepth = int.MaxValue;
    internal bool Aborting;
    internal ComponentHost? CurrentHost;

    internal Builder(UiView view) => View = view;

    internal bool Discarding =>
        _frames.Count > 0 && _frames[_frames.Count - 1].Mode == FrameMode.Discard;

    internal Node OpenNode => _frames[_frames.Count - 1].Parent;

    internal int FrameDepth => _frames.Count;

    internal int HookDepth => _hookFrames.Count;

    internal void Reset()
    {
        while (_frames.Count > 0)
        {
            ref var frame = ref _frames[_frames.Count - 1];
            ReturnMap(ref frame);
            _frames.Truncate(_frames.Count - 1);
        }
        _hookFrames.Clear();
        _faultDepth = _hookFaultDepth = int.MaxValue;
        Aborting = false;
        CurrentHost = null;
    }

    internal void Push(Node node) => _frames.Add(new Frame { Parent = node });

    internal void PushDiscard() =>
        _frames.Add(new Frame { Parent = OpenNode, Mode = FrameMode.Discard });

    internal void Close(int depth, int hookDepth)
    {
        while (_frames.Count > depth)
        {
            var index = _frames.Count - 1;
            ref var frame = ref _frames[index];
            if (frame.Mode == FrameMode.Normal && !Aborting && index < _faultDepth)
                UnmountAfter(ref frame);
            ReturnMap(ref frame);
            _frames.Truncate(index);
        }

        while (_hookFrames.Count > hookDepth)
        {
            var index = _hookFrames.Count - 1;
            var frame = _hookFrames[index];
            if (!Aborting && index < _hookFaultDepth && frame.Owner.Hooks is { } hooks)
                hooks.DiscardFrom(frame.Cursor);
            _hookFrames.Truncate(index);
        }
    }

    void UnmountAfter(ref Frame frame)
    {
        var parent = frame.Parent;
        var node = frame.Prev is null ? parent._first : frame.Prev._next;
        while (node is not null)
        {
            var next = node._next;
            if (!node.Exiting && !View.BeginExit(node))
                View.Unmount(node);
            node = next;
        }
        if (frame.Order is { } order)
            KeepExitingInPlace(parent, frame.SlowStart, order);
    }

    // Moves can carry claimed nodes past one that then exits; it goes back after its nearest earlier survivor.
    void KeepExitingInPlace(Node parent, Node? start, List<Node> order)
    {
        for (var i = 0; i < order.Count; i++)
        {
            var node = order[i];
            if (!node.Exiting || node._parent != parent)
                continue;
            var anchor = start;
            for (var j = i - 1; j >= 0; j--)
            {
                if (order[j]._parent == parent)
                {
                    anchor = order[j];
                    break;
                }
            }
            if (node._prev == anchor)
                continue;
            parent.Unlink(node);
            parent.InsertAfter(anchor, node);
            View.ChildrenChanged(parent);
        }
    }

    static Node? SkipExiting(Node? node)
    {
        while (node is { Exiting: true })
            node = node._next;
        return node;
    }

    static Node? LivePrevious(Node node)
    {
        var previous = node._prev;
        while (previous is { Exiting: true })
            previous = previous._prev;
        return previous;
    }

    void ReturnMap(ref Frame frame)
    {
        if (frame.Order is { } order)
        {
            order.Clear();
            _orders.Push(order);
            frame.Order = null;
        }
        if (frame.Map is null)
            return;
        frame.Map.Clear();
        _maps.Push(frame.Map);
        frame.Map = null;
    }

    internal Node Claim(
        NodeKind kind,
        Type? type,
        UiKey key,
        string file,
        int line,
        out bool mounted
    )
    {
        ref var frame = ref _frames[_frames.Count - 1];
#if DEBUG
        if (frame.Mode == FrameMode.Outlet && CurrentHost is { } host)
            throw Faults.InChildren(host.ComponentType, file, line);
#endif
        mounted = false;
        Node node;
        if (frame.Map is { } map)
            node = ClaimSlow(ref frame, map, kind, type, key, file, line, out mounted);
        else
        {
            var cursor = SkipExiting(frame.Prev is null ? frame.Parent._first : frame.Prev._next);
            if (cursor is null)
                node = Mount(
                    ref frame,
                    kind,
                    type,
                    key,
                    file,
                    line,
                    Occurrence(ref frame, key, file, line),
                    out mounted
                );
            else if (
                cursor.Line == line
                && cursor.Key == key
                && SameFile(cursor.File, file)
                && Matches(cursor, kind, type)
            )
                node = cursor;
            else
                node = ClaimSlow(
                    ref frame,
                    EnterSlow(ref frame, cursor),
                    kind,
                    type,
                    key,
                    file,
                    line,
                    out mounted
                );
        }

        frame.Prev = node;
        if (!node.IsTransparent)
            AssignRoot(node);
        return node;
    }

    Node ClaimSlow(
        ref Frame frame,
        Dictionary<Identity, Node> map,
        NodeKind kind,
        Type? type,
        UiKey key,
        string file,
        int line,
        out bool mounted
    )
    {
        var occurrence = Occurrence(ref frame, key, file, line);
#if DEBUG
        if (key.IsKeyed && occurrence == 0 && HasClaimed(ref frame, key, file, line))
            throw Faults.DuplicateKey(key, file, line);
#endif
        if (!map.Remove(new Identity(file, line, key, occurrence), out var hit))
            return Mount(ref frame, kind, type, key, file, line, occurrence, out mounted);
        if (!Matches(hit, kind, type))
        {
            View.Unmount(hit);
            return Mount(ref frame, kind, type, key, file, line, occurrence, out mounted);
        }
        mounted = false;
        if (LivePrevious(hit) != frame.Prev)
        {
            frame.Parent.Unlink(hit);
            frame.Parent.InsertAfter(frame.Prev, hit);
            View.ChildrenChanged(frame.Parent);
        }
        return hit;
    }

    static bool Matches(Node node, NodeKind kind, Type? type) =>
        node.Kind == kind && (kind != NodeKind.Component || node.Host?.ComponentType == type);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool SameFile(string? a, string? b) =>
        ReferenceEquals(a, b) || string.Equals(a, b, StringComparison.Ordinal);

    static int Occurrence(ref Frame frame, UiKey key, string file, int line)
    {
        if (key.IsKeyed)
            return 0;
        var count = 0;
        for (var n = frame.Prev; n is not null; n = n._prev)
        {
            if (n.Line == line && !n.Key.IsKeyed && !n.Exiting && SameFile(n.File, file))
                count++;
        }
        return count;
    }

#if DEBUG
    static bool HasClaimed(ref Frame frame, UiKey key, string file, int line)
    {
        for (var n = frame.Prev; n is not null; n = n._prev)
        {
            if (n.Line == line && n.Key == key && !n.Exiting && SameFile(n.File, file))
                return true;
        }
        return false;
    }
#endif

    Dictionary<Identity, Node> EnterSlow(ref Frame frame, Node cursor)
    {
        if (!_maps.TryPop(out var map))
        {
            PoolMisses.Add();
            map = [];
        }
        var start = frame.Prev is null ? frame.Parent._first : frame.Prev._next;
        List<Node>? order = null;
        for (var n = start; n is not null; n = n._next)
        {
            if (!n.Exiting)
                map.TryAdd(new Identity(n.File, n.Line, n.Key, n.Occurrence), n);
            if (order is null && (n.Exiting || UiView.PresenceOf(n) is not null))
            {
                if (!_orders.TryPop(out order))
                {
                    PoolMisses.Add();
                    order = [];
                }
                for (var m = start; m is not null && m != n; m = m._next)
                    order.Add(m);
            }
            order?.Add(n);
        }
        frame.Map = map;
        frame.Order = order;
        frame.SlowStart = frame.Prev;
        return map;
    }

    Node Mount(
        ref Frame frame,
        NodeKind kind,
        Type? type,
        UiKey key,
        string file,
        int line,
        int occurrence,
        out bool mounted
    )
    {
        if (
            kind is NodeKind.Component or NodeKind.Layer
            && View.Revive(frame.Parent, frame.Prev, kind, type, key, file, line, occurrence)
                is { } revived
        )
        {
            mounted = false;
            return revived;
        }
        var node = View.RentNode(kind);
        node.File = file;
        node.Line = line;
        node.Key = key;
        node.Occurrence = occurrence;
        frame.Parent.InsertAfter(frame.Prev, node);
        View.Mounted(node);
        mounted = true;
        return node;
    }

    void AssignRoot(Node node)
    {
        for (var p = node._parent; p is not null && p.IsTransparent; p = p._parent)
        {
            if (p.Host is not { } host)
                continue;
            if (host.RootAssigned)
            {
                if (host.Root == node)
                    continue;
                break;
            }
            host.RootAssigned = true;
            host.Root = node;
        }
    }

    internal static Placement EffectivePlacement(Node node, in Placement own)
    {
        ComponentHost? outermost = null;
        for (var p = node._parent; p is not null && p.IsTransparent; p = p._parent)
        {
            if (p.Host is not { } host)
                continue;
            if (host.Root != node || host.PassesPlacement)
                break;
            outermost = host;
        }
        if (outermost is null)
            return own;
        if (node.Kind == NodeKind.Layer)
        {
#if DEBUG
            if (outermost.Placement with { Id = null } != default)
                throw Faults.PlacementOnLayer(outermost.ComponentType);
            if (outermost.Placement.Id is not null && own.Id is not null)
                throw Faults.PlacementOnRoot(outermost.ComponentType);
#endif
            return own.Id is not null ? own : new Placement { Id = outermost.Placement.Id };
        }
#if DEBUG
        if (own != default)
            throw Faults.PlacementOnRoot(outermost.ComponentType);
#endif
        return outermost.Placement;
    }

    internal ComponentHost<TProps>? Component<TComponent, TProps>(
        in TProps props,
        in Placement place,
        UiKey key,
        string file,
        int line
    )
        where TComponent : Component<TProps>, new()
        where TProps : struct, IEquatable<TProps>
    {
        if (Discarding)
            return null;

        var node = Claim(NodeKind.Component, typeof(TComponent), key, file, line, out _);
        if (node.Host is not ComponentHost<TProps> host)
        {
            host = ComponentHost<TProps>.Rent(
                Singleton<TComponent>.Instance,
                typeof(TComponent),
                ComponentType<TComponent>.Id
            );
            host.Placement = place;
            host.PassesPlacement = typeof(TComponent) == typeof(RootComponent);
            node.Host = host;
            host.Mounted(View, node, CurrentHost);
            // After the singleton: making it runs the initializer or static constructor registering the props shape.
            host.Store(props);
            Render(host, fromCaller: true);
            return host;
        }

        if (!host.Placement.Equals(place))
        {
            host.Placement = place;
            if (host.Root is { } root)
                View.WritePlacement(root, default);
        }

        if (host.Dirty || !host.Value.Equals(props))
        {
            host.Store(props);
            Render(host, fromCaller: true);
        }
        else if (host.Root is { } root)
            AssignRoot(root);
        return host;
    }

    internal void Render(ComponentHost host, bool fromCaller)
    {
        if (!host.IsMounted)
            return;
        var view = View;
        if (host.RenderStamp != view.UpdateStamp)
        {
            host.RenderStamp = view.UpdateStamp;
            host.Renders = 0;
        }
        if (++host.Renders > MaxRendersPerUpdate)
        {
#if DEBUG
            throw Faults.TooManyRenders(host.ComponentType, MaxRendersPerUpdate);
#else
            view.Defer(host);
            return;
#endif
        }

        host.Dirty = false;
        view.CountRender(host);
        var frameDepth = _frames.Count;
        var hookDepth = _hookFrames.Count;
        _frames.Add(new Frame { Parent = host.Node });
        _hookFrames.Add(new HookFrame { Owner = host.Node });
        var previous = CurrentHost;
        CurrentHost = host;
        var oldRoot = host.Root;
        host.RootAssigned = false;
        host.Outlet = null;
        host.EnvReads = EnvReads.None;
        host.StateReads = InputStates.None;
        host.UsedKeys.Clear();
        for (var i = 0; i < host.Provided.Count; i++)
            host.Provided[i].Touched = false;
        var wake = host.WakeAt;
        host.WakeAt = long.MaxValue;

        var completed = false;
        var faulted = false;
        var tracking = Observation.Track(host);
        try
        {
            host.Invoke(new Ui(this));
            completed = true;
        }
        catch (Exception fault) when (ClaimFault(frameDepth, hookDepth))
        {
            faulted = true;
            view.Faulted(host);
            if (view.Context.OnFault is { } onFault)
                onFault(fault);
            else
            {
                Aborting = true;
                throw;
            }
        }
        finally
        {
            tracking.Complete(keepDropped: !completed);
            if (!completed)
                host.WakeAt = Math.Min(host.WakeAt, wake);
            if (host.WakeAt != wake)
                view.Context.ClockMoved(host);
            Close(frameDepth, hookDepth);
            if (_faultDepth == frameDepth)
                _faultDepth = _hookFaultDepth = int.MaxValue;
            CurrentHost = previous;
            if (!completed && !faulted && Aborting)
                host.Invalidate();
        }

        if (completed)
            Finish(host, oldRoot, fromCaller);
    }

    bool ClaimFault(int frameDepth, int hookDepth)
    {
        if (Aborting)
            return false;
        _faultDepth = frameDepth;
        _hookFaultDepth = hookDepth;
        return true;
    }

    void Finish(ComponentHost host, Node? oldRoot, bool fromCaller)
    {
        if (!host.RootAssigned)
            host.Root = null;

        for (var i = host.Provided.Count - 1; i >= 0; i--)
        {
            var entry = host.Provided[i];
            if (entry.Touched)
                continue;
            host.Provided.RemoveAt(i);
            entry.Withdraw();
        }

        if (host.Faulted)
            View.Unfaulted(host);

        if (host.Outlet != host.PreviousOutlet)
        {
            host.PreviousOutlet = host.Outlet;
            if (!fromCaller && host.Outlet is not null)
                host.Caller?.Invalidate();
        }

        if (!fromCaller && host.Root != oldRoot && host.Node is { } node)
            OwnerOf(node)?.Invalidate();

        for (var i = 0; i < host.PendingEffects.Count; i++)
            View.QueueEffect(host.PendingEffects[i]);
        host.PendingEffects.Clear();
    }

    static ComponentHost? OwnerOf(Node node)
    {
        for (var p = node._parent; p is not null && p.IsTransparent; p = p._parent)
        {
            if (p.Host is { } host)
                return host;
        }
        return null;
    }

    internal void Children()
    {
        if (Discarding || CurrentHost is not { } host)
            return;
        ref var frame = ref _frames[_frames.Count - 1];
#if DEBUG
        if (frame.Prev is { } own)
            throw Faults.InChildren(host.ComponentType, own.File, own.Line);
#endif
        frame.Mode = FrameMode.Outlet;
        host.Outlet = frame.Parent;
    }

    internal THook UseHook<THook>(UiKey key, string file, int line, out bool created)
        where THook : Hook, new()
    {
        ref var frame = ref _hookFrames[_hookFrames.Count - 1];
        var list = frame.Owner.Hooks ??= View.RentHookList();
        ref var items = ref list.Items;
        var at = frame.Cursor;
        created = false;
        if (at < items.Count)
        {
            var next = items[at];
            if (
                next.Line == line
                && next.Key == key
                && SameFile(next.File, file)
                && next is THook typed
            )
            {
                frame.Cursor++;
                return typed;
            }
        }

        var occurrence = 0;
        if (!key.IsKeyed)
        {
            for (var i = 0; i < at; i++)
            {
                var claimed = items[i];
                if (claimed.Line == line && !claimed.Key.IsKeyed && SameFile(claimed.File, file))
                    occurrence++;
            }
        }

        for (var i = at + 1; i < items.Count; i++)
        {
            var candidate = items[i];
            if (
                candidate.Line != line
                || candidate.Occurrence != occurrence
                || candidate.Key != key
                || !SameFile(candidate.File, file)
            )
                continue;
            if (candidate is THook typed)
            {
                items.Move(i, at);
                frame.Cursor++;
                return typed;
            }
            candidate.Discard();
            items.RemoveAt(i);
            break;
        }

        var hook = Pool<THook>.TryRent() ?? new THook();
        hook.File = file;
        hook.Line = line;
        hook.Key = key;
        hook.Occurrence = occurrence;
        items.Insert(at, hook);
        frame.Cursor++;
        created = true;
        return hook;
    }

    internal void PushHooks(Node owner) => _hookFrames.Add(new HookFrame { Owner = owner });

    internal enum FrameMode : byte
    {
        Normal,

        Outlet,

        Discard,
    }

    struct Frame
    {
        public Node Parent;
        public Node? Prev;
        public Dictionary<Identity, Node>? Map;
        public List<Node>? Order;
        public Node? SlowStart;
        public FrameMode Mode;
    }

    struct HookFrame
    {
        public Node Owner;
        public int Cursor;
    }

    readonly struct Identity(string? file, int line, UiKey key, int occurrence)
        : IEquatable<Identity>
    {
        readonly string? _file = file;
        readonly int _line = line;
        readonly UiKey _key = key;
        readonly int _occurrence = occurrence;

        public bool Equals(Identity other) =>
            _line == other._line
            && _occurrence == other._occurrence
            && _key == other._key
            && SameFile(_file, other._file);

        public override bool Equals(object? obj) => obj is Identity other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(_line, _key.GetHashCode(), _occurrence);
    }
}

static class Faults
{
    [ColdPath]
    public static InvalidOperationException UnboundedList(
        bool horizontal,
        string file,
        int line,
        ComponentHost? owner
    )
    {
        var (axis, track, scroll) = horizontal
            ? ("width", "column", "horizontal")
            : ("height", "row", "vertical");
        var within = owner is { Node: { File: { } at } node }
            ? $", inside the {NameOf(owner.ComponentType)} at {at}:{node.Line},"
            : "";
        return new(
            $"The virtual list at {file}:{line}{within} is measured with no {axis} limit, so it would realize every row on each layout. Give it a {axis} or a max {axis}, let it grow in a parent of bounded {axis} (a grid {track} of fixed or star size, not auto, and no {scroll} scroll area), or set realizeAll on a short list sized to its rows."
        );
    }

    static string NameOf(Type component) =>
        component.Name.IndexOf('`') is >= 0 and var tick ? component.Name[..tick] : component.Name;

    public static InvalidOperationException Missing(NodeKind kind, Type part) =>
        new($"A {kind} node carries no {NameOf(part)}.");

    public static InvalidOperationException DuplicateKey(UiKey key, string file, int line) =>
        new($"Two siblings share the key '{key}' at {file}:{line}.");

    public static InvalidOperationException InChildren(Type component, string? file, int line) =>
        new(
            $"{NameOf(component)} renders a node at {file}:{line} into the box that holds its caller's children, where the caller's next render removes it; render it outside that box."
        );

    public static InvalidOperationException PlacementOnRoot(Type component) =>
        new($"{component.Name} sets placement on its root; placement belongs to its caller.");

    public static InvalidOperationException PlacementOnLayer(Type component) =>
        new(
            $"{component.Name}'s root is a layer, which the view places; of its caller's placement only an id applies."
        );

    public static InvalidOperationException TooManyRenders(Type component, int limit) =>
        new($"{component.Name} rendered more than {limit} times in one update.");

    public static ArgumentOutOfRangeException OutOfRange<T>(string name, T value, string message) =>
        new(name, value, message);

    public static InvalidOperationException CustomLayout(Type algorithm, string what) =>
        new($"{algorithm.Name} {what}.");
}

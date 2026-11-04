using System.Numerics;

namespace Tralys.Ui;

public sealed partial class UiView
{
    const float CoastFrom = 40f;
    static readonly TimeSpan StillAfter = TimeSpan.FromSeconds(0.1);

    ValueList<PanRecord> _pans;

    bool StopCoasts(Node target)
    {
        var stopped = false;
        for (var node = target; node is not null; node = node._parent)
        {
            if (node.Scroller is { Motion: ScrollMotion.Coast } handle)
            {
                handle.StopMotion();
                stopped = true;
            }
        }
        return stopped;
    }

    void ArmPan(Pointer pointer, Node origin, Vector2 at, TimeSpan time)
    {
        DropPan(pointer);
        _pans.Add(
            new PanRecord
            {
                Pointer = pointer,
                Origin = origin,
                Start = at,
                Last = at,
                Sampled = at,
                SampledAt = time,
            }
        );
    }

    void PanCaptured(Pointer pointer)
    {
        var at = PanOf(pointer);
        if (at >= 0 && _pans[at].Claimed is null)
            _pans[at].Explicit = true;
    }

    bool PanMoved(PointerState state, TimeSpan time)
    {
        var at = PanOf(state.Pointer);
        if (at < 0)
            return false;
        ref var pan = ref _pans[at];
        var position = state.Position;
        var seconds = (float)(time - pan.SampledAt).TotalSeconds;
        if (seconds > 0f)
        {
            pan.Velocity = (position - pan.Sampled) / seconds;
            pan.Sampled = position;
            pan.SampledAt = time;
        }
        if (pan.Claimed is null)
        {
            if (pan.Explicit || !DragThreshold.IsCrossed(state.Pointer.Kind, position - pan.Start))
                return false;
            if (
                Claimant(pan.Origin, state.Pointer.Kind, position - pan.Start, out var axes)
                is not { } container
            )
                return false;
            pan.Claimed = container;
            pan.Axes = axes;
            pan.Last = pan.Start;
            Claim(state, container);
        }
        else if (!pan.Claimed.IsMounted)
        {
            _pans.RemoveAtSwap(at);
            return false;
        }

        var delta = position - pan.Last;
        pan.Last = position;
        if (
            PanBy(pan.Claimed, -delta, pan.Axes, chain: state.Pointer.Kind != PointerKind.Mouse) is
            { } moved
        )
            pan.Moved = moved;
        return true;
    }

    void Claim(PointerState state, Node container)
    {
        var captor = Live(state.Captor);
        SetCaptor(state, container);
        if (captor is null && state.Pressing && Live(state.Press.Target) is { } pressed)
            Cancel(state, pressed);
        EndPress(state);
    }

    bool EndPan(Pointer pointer, TimeSpan time, bool fling)
    {
        var at = PanOf(pointer);
        if (at < 0)
            return false;
        var pan = _pans[at];
        _pans.RemoveAtSwap(at);
        if (pan.Claimed is null)
            return false;
        var velocity = -pan.Velocity * pan.Axes;
        if (
            fling
            && pointer.Kind != PointerKind.Mouse
            && time - pan.SampledAt <= StillAfter
            && velocity.LengthSquared() >= CoastFrom * CoastFrom
            && pan.Moved is { IsMounted: true, Scroller: { } handle }
        )
            Coast(handle, velocity);
        return true;
    }

    internal void EndPans() => _pans.Clear();

    internal bool Pans(Pointer pointer, Node container)
    {
        var at = PanOf(pointer);
        return at >= 0 && _pans[at].Claimed == container;
    }

    internal bool TakePointer(Pointer pointer, Node node, out Vector2 position)
    {
        position = default;
        if (
            FindState(pointer) is not { InContact: true } state
            || (node._states & InputStates.Disabled) != 0
            || !Drawn(node)
            || DragDropDrives(state)
            || state.Gesture.Dragging is not null
        )
            return false;
        var captor = Live(state.Captor);
        var at = PanOf(pointer);
        var pressed = state.Pressing ? Live(state.Press.Target) : null;
        var held =
            at >= 0 && _pans[at].Claimed is { } claimed
                ? claimed != node
                : (
                    captor is not null
                    && captor != node
                    && (!node.IsAncestorOrSelfOf(captor) || (at >= 0 && _pans[at].Explicit))
                ) || (pressed is not null && !node.IsAncestorOrSelfOf(pressed));
        if (held)
            return false;
        if (at >= 0)
            _pans.RemoveAtSwap(at);
        position = state.Position;
        SetCaptor(state, node);
        if (captor is null && pressed is not null)
            Cancel(state, pressed);
        EndPress(state);
        return true;
    }

    void DropPan(Pointer pointer)
    {
        var at = PanOf(pointer);
        if (at >= 0)
            _pans.RemoveAtSwap(at);
    }

    bool MousePanning() => PanClaimed(Pointer.Mouse);

    bool PanClaimed(Pointer pointer)
    {
        var at = PanOf(pointer);
        return at >= 0 && _pans[at].Claimed is not null;
    }

    int PanOf(Pointer pointer)
    {
        for (var i = 0; i < _pans.Count; i++)
        {
            if (_pans[i].Pointer == pointer)
                return i;
        }
        return -1;
    }

    Node? Claimant(Node? origin, PointerKind kind, Vector2 travel, out Vector2 axes)
    {
        axes = Vector2.One;
        var horizontal = MathF.Abs(travel.X) > MathF.Abs(travel.Y);
        var pressable = origin is null ? null : PressableOf(origin);
        for (var node = origin; node is not null; node = node._parent)
        {
            if (node.Scroller is { } handle && (node.ScrollsX || node.ScrollsY))
            {
                var max = MaxOffset(node, handle);
                var alongX = CanMove(node.ScrollsX, -travel.X, handle.Current.X, max.X);
                var alongY = CanMove(node.ScrollsY, -travel.Y, handle.Current.Y, max.Y);
                if (kind == PointerKind.Mouse)
                {
                    if (
                        node.ScrollProps.DragToPan
                        && (alongX || alongY)
                        && (
                            pressable is null
                            || node.PansFromPressables
                            || !node.IsAncestorOrSelfOf(pressable)
                        )
                    )
                        return node;
                }
                else
                {
                    var claims = node.ScrollProps.Panning switch
                    {
                        Panning.Both => horizontal ? alongX : alongY,
                        Panning.HorizontalOnly or Panning.HorizontalFirst => horizontal && alongX,
                        Panning.VerticalOnly or Panning.VerticalFirst => !horizontal && alongY,
                        _ => false,
                    };
                    if (claims)
                    {
                        axes = Axes(node.ScrollProps.Panning);
                        return node;
                    }
                }
            }
            if (node.Kind == NodeKind.Layer)
                break;
        }
        return null;
    }

    static Vector2 Axes(Panning panning) =>
        panning switch
        {
            Panning.HorizontalOnly => Vector2.UnitX,
            Panning.VerticalOnly => Vector2.UnitY,
            Panning.None => Vector2.Zero,
            _ => Vector2.One,
        };

    Node? PanBy(Node container, Vector2 delta, Vector2 axes, bool chain)
    {
        delta *= axes;
        Node? moved = null;
        for (var node = container; node is not null && delta != default; node = node._parent)
        {
            if (node != container && !chain)
                break;
            if (node.Scroller is { } handle && (node.ScrollsX || node.ScrollsY))
            {
                var along = node == container ? Vector2.One : Axes(node.ScrollProps.Panning);
                var max = MaxOffset(node, handle);
                var from = handle.Current;
                var to = Clamp(from + delta * along, max);
                if (to != from)
                {
                    handle.StopMotion();
                    handle.ToEnd = false;
                    handle.Current = to;
                    Scrolled(node, handle);
                    moved = node;
                    delta -= to - from;
                }
            }
            if (node.Kind == NodeKind.Layer)
                break;
        }
        return moved;
    }

    struct PanRecord
    {
        public Pointer Pointer;
        public Node? Origin;
        public Node? Claimed;
        public Node? Moved;
        public Vector2 Axes;
        public Vector2 Start;
        public Vector2 Last;
        public Vector2 Sampled;
        public TimeSpan SampledAt;
        public Vector2 Velocity;
        public bool Explicit;
    }
}

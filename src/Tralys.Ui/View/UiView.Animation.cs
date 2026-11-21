using Tralys.Ui.Theming;

namespace Tralys.Ui;

public sealed partial class UiView
{
    ValueList<Motion> _motions;
    int _finiteMotions;
    ValueList<Node> _exiting;
    ValueList<Node> _exitsDone;
    ValueList<LoopHook> _resting;
    int _updatesDone;

    internal bool MotionsIdle => _finiteMotions == 0 && _exitsDone.Count == 0;

    internal bool LoopsRunning => _motions.Count > _finiteMotions;

    void Run(Motion motion, bool finite)
    {
        if (motion.IsRunning)
            return;
        motion.Slot = _motions.Count;
        motion.CountsTowardIdle = finite;
        _motions.Add(motion);
        if (finite)
            _finiteMotions++;
    }

    internal void StopMotion(Motion motion)
    {
        var slot = motion.Slot;
        if (slot < 0)
            return;
        var last = _motions.Count - 1;
        var moved = _motions[last];
        _motions[slot] = moved;
        moved.Slot = slot;
        _motions.Truncate(last);
        motion.Slot = -1;
        if (motion.CountsTowardIdle)
            _finiteMotions--;
        motion.CountsTowardIdle = false;
    }

    // Backwards, so a motion leaving swaps in one already moved.
    internal void AdvanceMotions()
    {
        var time = Context.AnimationTime;
        var reduced = Context.ReducedMotion;
        for (var i = _motions.Count - 1; i >= 0; i--)
        {
            var motion = _motions[i];
            if (motion is LoopHook { Host: { } host } loop && !Seen(host))
            {
                Rest(loop);
                continue;
            }
            var step = motion.Advance(time, reduced);
            if (step == Step.Unchanged)
                continue;
            motion.Host?.Invalidate();
            if (step != Step.Ended)
                continue;
            StopMotion(motion);
            motion.Ended();
        }
    }

    internal T Animate<T, TBlend>(
        Builder builder,
        T target,
        in Transition transition,
        UiKey key,
        string file,
        int line
    )
        where TBlend : struct, IBlend<T>
    {
        var hook = builder.UseHook<AnimateHook<T, TBlend>>(key, file, line, out var created);
        var time = Context.AnimationTime;
        if (created)
        {
            hook.Host = builder.CurrentHost;
            hook.Born = _updatesDone;
            hook.Tween.Rest(target);
            return target;
        }
        if (!TBlend.Same(hook.Tween.Target, target))
        {
            var shown = hook.Tween.Value(time, hook.IsRunning);
            var curve = Curve(transition, builder.OpenNode);
            if (
                hook.Born == _updatesDone
                || curve.IsInstant
                || Context.ReducedMotion
                || !Shown(builder.OpenNode)
            )
            {
                StopMotion(hook);
                hook.Tween.Rest(target);
            }
            else
            {
                hook.Tween.Begin(shown, target, time, curve);
                Run(hook, finite: true);
            }
        }
        return hook.Tween.Value(time, hook.IsRunning);
    }

    internal float Loop(
        Builder builder,
        float period,
        int count,
        bool alternate,
        IEasingFunction? easing,
        bool paused,
        UiKey key,
        string file,
        int line
    )
    {
        if (!(period > 0f && float.IsFinite(period)))
            throw new ArgumentOutOfRangeException(
                nameof(period),
                "A loop's period is positive and finite."
            );
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var hook = builder.UseHook<LoopHook>(key, file, line, out var created);
        var time = Context.AnimationTime;
        if (created)
        {
            hook.Host = builder.CurrentHost;
            hook.Period = period;
        }
        else if (hook.Period != period)
        {
            hook.Elapsed = hook.Elapsed / hook.Period * period;
            hook.Period = period;
        }
        if (hook.Count != count || hook.Alternate != alternate)
            StopMotion(hook);
        hook.Count = count;
        hook.Alternate = alternate;
        hook.Easing = easing;

        if (Context.ReducedMotion)
        {
            StopMotion(hook);
            return count > 0 ? 1f : 0f;
        }
        if (paused || hook.Done)
            StopMotion(hook);
        else if (!Seen(hook.Host!))
            Rest(hook);
        else if (!hook.IsRunning)
        {
            hook.Last = time;
            Run(hook, finite: count > 0);
        }
        return hook.Phase;
    }

    void Rest(LoopHook loop)
    {
        StopMotion(loop);
        loop.Elapsed = 0;
        if (loop.Resting)
            return;
        loop.Resting = true;
        _resting.Add(loop);
    }

    internal void Unrest(LoopHook loop)
    {
        var index = _resting.IndexOf(loop);
        if (index >= 0)
            _resting.RemoveAtSwap(index);
        loop.Resting = false;
    }

    internal void WakeResting()
    {
        for (var i = _resting.Count - 1; i >= 0; i--)
        {
            var loop = _resting[i];
            if (loop.Host is not { } host || !Seen(host))
                continue;
            _resting.RemoveAtSwap(i);
            loop.Resting = false;
            host.Invalidate();
        }
        WakeSprites();
    }

    static bool Seen(ComponentHost host) => Shown(host.Root ?? host.Node);

    internal float Presence(Builder builder, in Transition enter, in Transition exit)
    {
        var host = builder.CurrentHost!;
        var hook = builder.UseHook<PresenceHook>(default, PresenceHook.Site, 0, out var created);
        hook.Enter = enter;
        hook.Exit = exit;
        host.Presence = hook;
        var time = Context.AnimationTime;
        if (created)
        {
            hook.Host = host;
            var curve = Curve(enter, host.Node);
            if (curve.IsInstant || Context.ReducedMotion || !Shown(host.Node))
                hook.Tween.Rest(1f);
            else
            {
                hook.Tween.Begin(0f, 1f, time, curve);
                Run(hook, finite: true);
            }
        }
        return hook.Tween.Value(time, hook.IsRunning);
    }

    internal bool ReadShown(Builder builder)
    {
        if (builder.CurrentHost is not { } host)
            return Shown(builder.OpenNode);
        host.EnvReads |= EnvReads.Shown;
        if (host.Root is { } root)
            return Shown(root);
        // Before its first render ends, a host's caller placement sits on no node yet.
        for (var n = host.Node; n is not null; n = n._parent)
        {
            if (n.Host is { Placement: { Hidden: true } or { Collapsed: true } })
                return false;
            if (n.Place.Hidden || n.Place.Collapsed || n.FaultHidden || n.LayoutHidden)
                return false;
        }
        return true;
    }

    // The hosts whose root a node is sit on the transparent nodes above it, so a flip is told from the topmost.
    internal static void VisibilityFlipped(Node node)
    {
        var top = node;
        while (top._parent is { IsTransparent: true } parent)
            top = parent;
        InvalidateEnv(top, EnvReads.Shown);
    }

    internal bool ReadExiting(Builder builder)
    {
        if (builder.CurrentHost is not { } host)
            return false;
        host.EnvReads |= EnvReads.Exiting;
        for (var n = host.Node; n is not null; n = n._parent)
        {
            if (n.Exiting)
                return true;
        }
        return false;
    }

    internal bool BeginExit(Node node)
    {
        if (PresenceOf(node) is not { } presence)
            return false;
        var curve = Curve(presence.Exit, node);
        if (curve.IsInstant || Context.ReducedMotion || !Shown(node))
            return false;

        var time = Context.AnimationTime;
        presence.Tween.Begin(presence.Tween.Value(time, presence.IsRunning), 0f, time, curve);
        Run(presence, finite: true);
        node.Exiting = true;
        if (_exiting.IndexOf(node) < 0)
            _exiting.Add(node);
        Departed(node);
        Exited(node);
        return true;
    }

    internal static PresenceHook? PresenceOf(Node node)
    {
        if (node.Kind != NodeKind.Layer)
            return node.Host?.Presence;
        for (var child = node._first; child is not null; child = child._next)
        {
            if (child.Host?.Presence is { } presence)
                return presence;
        }
        return null;
    }

    internal static Node? ExitingWith(ComponentHost host) =>
        host.Node is not { } node ? null
        : node.Exiting ? node
        : node._parent is { Kind: NodeKind.Layer, Exiting: true } layer
        && PresenceOf(layer) == host.Presence
            ? layer
        : null;

    static bool ExitingLayer(Node layer) => layer.Exiting || ExitingAbove(layer);

    static bool InExiting(Node node)
    {
        for (var n = node; n is not null; n = n._parent)
        {
            if (n.Exiting)
                return true;
        }
        return false;
    }

    // Hit testing asks at every layout node, so the components just above it are as far as this looks.
    static bool ExitingAbove(Node node)
    {
        for (var p = node._parent; p is { IsTransparent: true }; p = p._parent)
        {
            if (p.Exiting)
                return true;
        }
        return false;
    }

    internal Node? Revive(
        Node parent,
        Node? previous,
        NodeKind kind,
        Type? type,
        UiKey key,
        string file,
        int line,
        int occurrence
    )
    {
        for (var i = 0; i < _exiting.Count; i++)
        {
            var node = _exiting[i];
            if (
                !node.Exiting
                || !node.IsMounted
                || node._parent != parent
                || node.Line != line
                || node.Occurrence != occurrence
                || node.Key != key
                || node.Kind != kind
                || node.Host?.ComponentType != type
                || !string.Equals(node.File, file, StringComparison.Ordinal)
            )
                continue;

            _exiting.RemoveAt(i);
            node.Exiting = false;
            if (node._prev != previous)
            {
                parent.Unlink(node);
                parent.InsertAfter(previous, node);
                ChildrenChanged(parent);
            }
            if (PresenceOf(node) is { } presence)
            {
                var time = Context.AnimationTime;
                var curve = Curve(presence.Enter, node);
                if (curve.IsInstant || Context.ReducedMotion)
                {
                    StopMotion(presence);
                    presence.Tween.Rest(1f);
                }
                else
                {
                    presence.Tween.Begin(
                        presence.Tween.Value(time, presence.IsRunning),
                        1f,
                        time,
                        curve
                    );
                    Run(presence, finite: true);
                }
            }
            Exited(node);
            return node;
        }
        return null;
    }

    void Exited(Node node)
    {
        node.Host?.Invalidate();
        InvalidateEnv(node, EnvReads.Exiting);
    }

    internal void ExitEnded(Node node)
    {
        if (node.IsMounted && _exitsDone.IndexOf(node) < 0)
            _exitsDone.Add(node);
    }

    void FinishExits()
    {
        for (var i = 0; i < _exitsDone.Count; i++)
        {
            var node = _exitsDone[i];
            if (node.IsMounted && node.Exiting)
                Unmount(node);
        }
        _exitsDone.Clear();
        for (var i = _exiting.Count - 1; i >= 0; i--)
        {
            if (!_exiting[i].IsMounted || !_exiting[i].Exiting)
                _exiting.RemoveAtSwap(i);
        }
    }

    MotionCurve Curve(in Transition transition, Node? at)
    {
        var duration = transition.Duration;
        var easing = transition.Easing;
        if (duration is null || easing is null)
        {
            var theme = ThemeAt(at);
            duration ??= theme.Duration(DurationToken.Disclosure);
            easing ??= theme.Easing(EasingToken.Settle);
        }
        return new MotionCurve(
            Finite(duration.Value),
            Finite(transition.Delay),
            easing ?? Linear.Instance
        );
    }

    static float Finite(float seconds) => float.IsFinite(seconds) ? MathF.Max(0f, seconds) : 0f;

    internal static bool Shown(Node? node)
    {
        for (var n = node; n is not null; n = n._parent)
        {
            if (n.Place.Hidden || n.Place.Collapsed || n.FaultHidden || n.LayoutHidden)
                return false;
        }
        return true;
    }

    sealed class Linear : IEasingFunction
    {
        public static readonly Linear Instance = new();

        [FramePath]
        public double Ease(double normalizedTime) => normalizedTime;
    }
}

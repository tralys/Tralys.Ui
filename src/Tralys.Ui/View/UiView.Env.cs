using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui;

public sealed partial class UiView
{
    internal UiTheme ReadTheme(Builder builder)
    {
        if (builder.CurrentHost is { } host)
            host.EnvReads |= EnvReads.Theme | EnvReads.Density;
        return new UiTheme(
            ThemeAt(builder.OpenNode),
            DensityAt(builder.OpenNode),
            Context.Skin,
            Context.Icons
        );
    }

    internal Density ReadDensity(Builder builder)
    {
        if (builder.CurrentHost is { } host)
            host.EnvReads |= EnvReads.Density;
        return DensityAt(builder.OpenNode);
    }

    internal Size ReadViewSize(Builder builder)
    {
        if (builder.CurrentHost is { } host)
            host.EnvReads |= EnvReads.ViewSize;
        return _size;
    }

    internal UiView ReadInsets(Builder builder)
    {
        if (builder.CurrentHost is { } host)
            host.EnvReads |= EnvReads.Insets;
        return this;
    }

    internal Theme ThemeAt(Node? node)
    {
        for (var n = node; n is not null; n = n._parent)
        {
            if (n.Inherit.Theme is { } theme)
                return theme;
        }
        return Context.Theme;
    }

    internal Density DensityAt(Node? node)
    {
        for (var n = node; n is not null; n = n._parent)
        {
            if (n.Inherit.Density is { } density)
                return density;
        }
        return Context.Density;
    }

    internal bool ReadStates(Builder builder, InputStates state) =>
        (ReadStateSet(builder, state) & state) != 0;

    internal static InputStates ReadStateSet(Builder builder, InputStates mask)
    {
        if (builder.CurrentHost is not { Node: { } node } host)
            return InputStates.None;
        host.StateReads |= mask | Implied(mask);
        host.EnvReads |= EnvReads.Held | EnvReads.Disabled;
        var root = host.Root;
        var states = (root?._states ?? InputStates.None) | (node._states & InputStates.Disabled);
        return states | Held(root ?? node);
    }

    static InputStates Implied(InputStates mask)
    {
        var implied = InputStates.None;
        if ((mask & InputStates.FocusVisible) != 0)
            implied |= InputStates.Focused;
        if ((mask & InputStates.Hovered) != 0)
            implied |= InputStates.Pressed;
        return implied;
    }

    internal static InputStates Held(Node node)
    {
        var held = InputStates.None;
        for (var n = node; n is not null; n = n._parent)
            held |= n.Inherit.ForcedInside | n.ForcedByView | n.ForcedByScope;
        if ((held & InputStates.Focused) != 0)
            held |= InputStates.FocusVisible;
        if ((held & InputStates.Pressed) != 0)
            held |= InputStates.Hovered;
        return held;
    }

    /// <summary>Holds <paramref name="states"/> on the components at and inside <paramref name="node"/>, on top of
    /// their real states, as the inspector does; <see cref="InputStates.None"/> releases them.</summary>
    public void ForceStates(Node node, InputStates states)
    {
        ArgumentNullException.ThrowIfNull(node);
        VerifyAccess();
        var before = node.ForcedByView;
        if (before == states)
            return;
        node.ForcedByView = states;
        Forced(node, before ^ states);
    }

    void Forced(Node node, InputStates changed)
    {
        HeldChanged(node);
        if ((changed & InputStates.Disabled) != 0)
            UpdateDisabled(node);
    }

    internal static void ComponentMounted(Node node, Type component)
    {
        var held = ScopeHeld(node._parent, component);
        node.ForcedByScope = held;
        if ((held & InputStates.Disabled) != 0)
            node._states |= InputStates.Disabled;
    }

    internal void ScopeChanged(Node scope)
    {
        for (var child = scope._first; child is not null; child = child._next)
            Rescope(child);
    }

    void Rescope(Node node)
    {
        if (node.Component is { } component)
        {
            var before = node.ForcedByScope;
            var held = ScopeHeld(node._parent, component);
            if (held != before)
            {
                node.ForcedByScope = held;
                Forced(node, before ^ held);
            }
        }
        for (var child = node._first; child is not null; child = child._next)
            Rescope(child);
    }

    static InputStates ScopeHeld(Node? from, Type component)
    {
        var held = InputStates.None;
        for (var n = from; n is not null; n = n._parent)
        {
            if (n.Inherit.ForceStatesOn == component)
                held |= n.Inherit.ForceStates;
        }
        return held;
    }

    internal void HeldChanged(Node node)
    {
        InvalidateEnv(node, EnvReads.Held);
        for (var p = node._parent; p is not null && p.IsTransparent; p = p._parent)
        {
            if (p.Host is not { } host)
                continue;
            if (host.Root != node)
                break;
            if ((host.EnvReads & EnvReads.Held) != 0)
                host.Invalidate();
        }
    }

    internal void StatesChanged(Node node, InputStates changed)
    {
        if (node.Payload is SpanPayload span)
            span.StatesChanged(this, node, changed);
        if (node.Host is { } own && (own.StateReads & changed) != 0)
            own.Invalidate();
        for (var p = node._parent; p is not null && p.IsTransparent; p = p._parent)
        {
            if (p.Host is not { } host)
                continue;
            if (host.Root != node)
                break;
            if ((host.StateReads & changed) != 0)
                host.Invalidate();
        }
    }

    internal void UpdateDisabled(Node node)
    {
        var parentDisabled =
            node._parent is { } parent && (parent._states & InputStates.Disabled) != 0;
        SetDisabled(node, parentDisabled);
    }

    static void SetDisabled(Node node, bool parentDisabled)
    {
        var disabled =
            parentDisabled
            || node.Inherit.Disabled
            || node.CommandRefusals > 0
            || (
                (node.Inherit.ForcedInside | node.ForcedByView | node.ForcedByScope)
                & InputStates.Disabled
            ) != 0;
        node.SetStates(
            disabled ? node._states | InputStates.Disabled : node._states & ~InputStates.Disabled
        );
        for (var child = node._first; child is not null; child = child._next)
            SetDisabled(child, disabled);
    }

    internal void WritePlacement(Node node, in Placement place)
    {
        if (node.WritePlacement(Builder.EffectivePlacement(node, place)))
            Stats.NodeWrites++;
    }

    internal void Provide<T>(Builder builder, ContextKey<T> key, T value)
        where T : IEquatable<T>?
    {
        ArgumentNullException.ThrowIfNull(key);
        if (builder.CurrentHost is not { } host)
            return;
        var provided = host.Provided.AsSpan();
        for (var i = 0; i < provided.Length; i++)
        {
            if (!ReferenceEquals(provided[i].Key, key))
                continue;
            var entry = (ProvidedEntry<T>)provided[i];
            entry.Touched = true;
            if (!EqualityComparer<T>.Default.Equals(entry.Value, value))
            {
                entry.Value = entry.Held.Hold(value);
                entry._observers.Notify(entry, 0);
            }
            return;
        }

        var added = Pool<ProvidedEntry<T>>.TryRent() ?? new ProvidedEntry<T>();
        added.Key = key;
        added.Value = added.Held.Hold(value);
        added.Touched = true;
        host.Provided.Add(added);
        if (host.Node is { } node)
            ContextLookup.Appeared(node, key);
    }

    internal T Use<T>(Builder builder, ContextKey<T> key)
        where T : IEquatable<T>?
    {
        ArgumentNullException.ThrowIfNull(key);
        var value = ContextLookup.Find(builder.OpenNode, key, out var entry);
        if (builder.CurrentHost is { } host && host.UsedKeys.IndexOf(key) < 0)
            host.UsedKeys.Add(key);
        if (entry is not null)
            Observation.Read(entry, 0);
        return value;
    }
}

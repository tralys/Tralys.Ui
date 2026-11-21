using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

/// <summary>A component: a function of its props and what it reads, run again when either changes. The core makes one
/// instance per type, so a component holds no fields; its state lives in hooks.</summary>
public abstract class Component<TProps>
    where TProps : struct, IEquatable<TProps>
{
    [FramePath]
    protected abstract void Render(Ui ui, in TProps p);

    [FramePath]
    internal void RenderBridge(Ui ui, in TProps p) => Render(ui, in p);
}

/// <summary>Marks a component whose render places its caller's children with <see cref="Ui.Children"/>.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ChildrenAttribute : Attribute;

/// <summary>A mounted component, for callbacks that need its latest props or its context: the pooled host it ran on
/// and the generation it was taken at. Once the component unmounts, or its row is recycled, the handle is stale: it
/// reads as unmounted with default props and finds every context key's fallback, and a callback or handler made from
/// it does nothing.</summary>
[FramePath]
public readonly struct Host<TProps> : IEquatable<Host<TProps>>
    where TProps : struct, IEquatable<TProps>
{
    static readonly TProps Unmounted = default;

    readonly ComponentHost<TProps>? _host;
    readonly int _generation;

    internal Host(ComponentHost<TProps>? host, int generation)
    {
        _host = host;
        _generation = generation;
    }

    internal ComponentHost<TProps>? Owner => _host;

    internal int Generation => _generation;

    /// <summary>The props as of the latest render; default once the handle is stale.</summary>
    public ref readonly TProps Props => ref Current ? ref _host.PropsRef : ref Unmounted;

    public bool IsMounted => Current && _host.IsMounted;

    [MemberNotNullWhen(true, nameof(_host))]
    bool Current => _host is not null && _host.Generation == _generation;

    /// <summary>The value of the nearest provider of <paramref name="key"/> above the component, as it is now; the
    /// read is not tracked.</summary>
    public T Use<T>(ContextKey<T> key)
        where T : IEquatable<T>? =>
        ContextLookup.Find(Current && _host.IsMounted ? _host.Node : null, key, out _);

    public bool Equals(Host<TProps> other) =>
        ReferenceEquals(_host, other._host) && _generation == other._generation;

    public override bool Equals(object? obj) => obj is Host<TProps> other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(RuntimeHelpers.GetHashCode(_host), _generation);

    public static bool operator ==(Host<TProps> left, Host<TProps> right) => left.Equals(right);

    public static bool operator !=(Host<TProps> left, Host<TProps> right) => !left.Equals(right);
}

static class Singleton<T>
    where T : new()
{
    public static readonly T Instance = new();
}

[FramePath]
internal abstract partial class ComponentHost : IChangeObserver
{
    internal Dependencies _dependencies;
    internal Node? Node;
    internal Node? Root;
    internal ComponentHost? Caller;
    internal Placement Placement;
    internal Node? Outlet;
    internal Node? PreviousOutlet;
    internal bool Dirty;
    internal bool Faulted;
    internal bool RootAssigned;
    bool _mounted;
    internal bool PassesPlacement;
    internal int Generation;
    internal long MountId;
    internal int RenderStamp;
    internal int Renders;
    internal EnvReads EnvReads;
    internal InputStates StateReads;
    internal ValueList<ProvidedEntry> Provided;
    internal ValueList<object> UsedKeys;
    internal ValueList<EffectHook> PendingEffects;
    internal PresenceHook? Presence;
    internal int RenderCount;
    internal long WakeAt = long.MaxValue;
    internal int ClockSlot = -1;
    internal UiView? View;

    static long s_mounts;

    public ref Dependencies Dependencies => ref _dependencies;

    [MemberNotNullWhen(true, nameof(Node), nameof(View))]
    internal bool IsMounted => _mounted;

    internal abstract Type ComponentType { get; }

    internal abstract int TypeId { get; }

    internal abstract void Invoke(Ui ui);

    public void OnChanged(object source, int property) => Invalidate();

    internal void Invalidate()
    {
        if (Dirty || !IsMounted)
            return;
        Dirty = true;
        View.HostDirtied(this);
    }

    internal void Mounted(UiView view, Node node, ComponentHost? caller)
    {
        View = view;
        Node = node;
        Caller = caller;
        _mounted = true;
        MountId = Interlocked.Increment(ref s_mounts);
        UiView.ComponentMounted(node, ComponentType);
        if (!ReferenceEquals(_dependencies.Dispatcher, view.Context.Dispatcher))
            _dependencies = new Dependencies(view.Context.Dispatcher);
    }

    internal virtual void Unmounted()
    {
        _mounted = false;
        Generation++;
        Dirty = false;
        _dependencies.Clear();
        View?.Context.Unwatch(this);
        WakeAt = long.MaxValue;
        for (var i = 0; i < Provided.Count; i++)
            Provided[i].Withdraw();
        Provided.Clear();
        UsedKeys.Clear();
        PendingEffects.Clear();
        Root = null;
        Caller = null;
        Outlet = PreviousOutlet = null;
        Placement = default;
        Faulted = false;
        RootAssigned = false;
        EnvReads = EnvReads.None;
        StateReads = InputStates.None;
        Renders = 0;
        RenderCount = 0;
    }

    internal virtual void Release()
    {
        Node = null;
        View = null;
    }
}

[FramePath]
internal sealed partial class ComponentHost<TProps> : ComponentHost, IPropsHost<TProps>
    where TProps : struct, IEquatable<TProps>
{
    internal TProps Value;
    internal Component<TProps> Component;
    internal Type Type;
    internal int Id;

    ComponentHost(Component<TProps> component, Type type, int id)
    {
        Component = component;
        Type = type;
        Id = id;
    }

    internal static ComponentHost<TProps> Rent(Component<TProps> component, Type type, int id)
    {
        if (Pool<ComponentHost<TProps>>.TryRent() is not { } host)
            return new ComponentHost<TProps>(component, type, id);
        host.Component = component;
        host.Type = type;
        host.Id = id;
        return host;
    }

    public TProps Props => Value;

    internal ref TProps PropsRef => ref Value;

    internal override Type ComponentType => Type;

    internal override int TypeId => Id;

    internal Host<TProps> Self => new(this, Generation);

    internal override void Invoke(Ui ui) => Component.RenderBridge(ui, in Value);

    internal override void Release()
    {
        base.Release();
        Value = default;
        Pool<ComponentHost<TProps>>.Return(this);
    }
}

internal static class ComponentTypes
{
    static int s_next;

    public static int Next() => Interlocked.Increment(ref s_next);
}

internal static class ComponentType<TComponent>
{
    public static readonly int Id = ComponentTypes.Next();
}

[Flags]
internal enum EnvReads : ushort
{
    None = 0,
    Theme = 1,
    Density = 2,
    Disabled = 4,
    Held = 8,
    Insets = 16,
    Exiting = 32,
    Device = 64,
    SizeClass = 128,
    Shown = 256,
    FocusedWithin = 512,
    ViewSize = 1024,
}

internal static class Pool<T>
    where T : class
{
    [ThreadStatic]
    static Stack<T>? t_items;

    [FramePath]
    public static T? TryRent()
    {
        if (t_items is { Count: > 0 } items)
            return items.Pop();
        PoolMisses.Add();
        return null;
    }

    [FramePath]
    public static void Return(T item) => (t_items ??= new Stack<T>()).Push(item);
}

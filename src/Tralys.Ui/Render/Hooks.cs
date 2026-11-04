using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

/// <summary>A cell of local state: setting a different value re-renders its owner and every host that read it.</summary>
[FramePath]
public sealed class State<T> : IObservable
{
    T _value = default!;
    HeldText _held;
    ObserverSet _observers;
    bool _byReference;
    internal ComponentHost? Owner;
    readonly Callback<T> _setter;

    internal State() =>
        _setter = Callback<T>.Of(this, static (state, value) => state.Value = value);

    ref ObserverSet IObservable.Observers => ref _observers;

    /// <summary>The value; reading it is tracked, and setting it writes only a different value.</summary>
    public T Value
    {
        get
        {
            Observation.Read(this, 0);
            return _value;
        }
        set
        {
            if (
                _byReference
                    ? ReferenceEquals(
                        Unsafe.As<T, object?>(ref _value),
                        Unsafe.As<T, object?>(ref value)
                    )
                    : EqualityComparer<T>.Default.Equals(_value, value)
            )
                return;
            _value = _held.Hold(value);
            Owner?.Invalidate();
            _observers.Notify(this, 0);
        }
    }

    /// <summary>The value, untracked.</summary>
    public T Peek => _value;

    /// <summary>A callback that sets the value.</summary>
    public Callback<T> Setter => _setter;

    internal void Init(ComponentHost owner, T value, bool byReference)
    {
        Owner = owner;
        _value = _held.Hold(value);
        _byReference = byReference;
    }

    internal void Release()
    {
        _observers.Clear();
        Owner = null;
        _value = default!;
        Pool<State<T>>.Return(this);
    }
}

public static class StateExtensions
{
    /// <summary>A callback that flips the value.</summary>
    [FramePath]
    public static Callback Toggle(this State<bool> state) =>
        Callback.Of(state, static s => s.Value = !s.Value);
}

/// <summary>A remembered value a recycled row keeps, reset instead of rebuilt.</summary>
public interface IResettable
{
    [FramePath]
    void Reset();
}

[FramePath]
public delegate void EffectFn<in TState, in TDep>(TState state, TDep dep, EffectContext cx);

/// <summary>What an effect may read after layout.</summary>
[FramePath]
public readonly ref struct EffectContext
{
    internal EffectContext(UiView view) => View = view;

    public UiView View { get; }

    /// <summary>Where <paramref name="node"/>'s node is in view space, as just laid out.</summary>
    public Rect BoundsOf(NodeRef node) => node.Node?.ViewBounds ?? default;
}

/// <summary>A handle on the node a primitive was given as its <c>nodeRef</c>.</summary>
[FramePath]
public sealed class NodeRef : IObservable
{
    ObserverSet _observers;
    internal Node? _node;
    internal Rect _bounds;
    internal UiView? _view;

    ref ObserverSet IObservable.Observers => ref _observers;

    public Node? Node => _node;

    /// <summary>The node's bounds in view space, as of the last layout. Tracked; a change renders in the next update.</summary>
    public Rect Bounds
    {
        get
        {
            Observation.Read(this, 0);
            return _bounds;
        }
    }

    /// <summary>The node's size, as of the last layout; a collapsed node keeps its last size. Tracked.</summary>
    public Size Size
    {
        get
        {
            Observation.Read(this, 0);
            return _bounds.Size;
        }
    }

    /// <summary>Focuses the node from code.</summary>
    /// <returns>False, leaving focus alone, when it can't take focus.</returns>
    public bool Focus(bool visible = false) =>
        _node is { View: { } view } node && view.Focus(node, FocusCause.Code, visible);

    /// <summary>Scrolls every scroll container around the node, innermost first, as little as
    /// <paramref name="align"/> allows to show it, at the next layout.</summary>
    public void ScrollIntoView(ScrollAlign align = default)
    {
        if (_node is { View: { } view } node)
            view.RequestReveal(node, align, animate: false);
    }

    internal void Bind(Node? node)
    {
        if (_node == node)
            return;
        if (_node is not null && _node.Ref == this)
            _node.Ref = null;
        _node = node;
        if (node is not null)
        {
            node.Ref = this;
            _view = node.View;
        }
    }

    internal void LaidOut(Rect bounds)
    {
        if (_bounds == bounds)
            return;
        _bounds = bounds;
        _view?.QueueLayoutNotice(this);
    }

    internal void NotifyLaidOut() => _observers.Notify(this, 0);

    internal void Release()
    {
        Bind(null);
        _observers.Clear();
        _bounds = default;
        _view = null;
        Pool<NodeRef>.Return(this);
    }
}

[FramePath]
internal abstract class Hook
{
    public string File = "";
    public int Line;
    public UiKey Key;
    public int Occurrence;

    public abstract void Discard();

    public virtual bool SurvivesRecycle => false;

    public virtual void Recycled() { }
}

[FramePath]
internal sealed class StateHook<T> : Hook
{
    public State<T> Cell = null!;

    public override void Discard()
    {
        Cell.Release();
        Cell = null!;
        Pool<StateHook<T>>.Return(this);
    }
}

[FramePath]
internal sealed class RememberHook : Hook
{
    public object? Value;

    public override bool SurvivesRecycle => Value is IResettable;

    public override void Recycled() => (Value as IResettable)?.Reset();

    public override void Discard()
    {
        (Value as IDisposable)?.Dispose();
        Value = null;
        Pool<RememberHook>.Return(this);
    }
}

[FramePath]
internal sealed class RememberHook<TDep> : Hook
{
    public TDep Dep = default!;
    public HeldText Held;
    public object? Value;

    public override bool SurvivesRecycle => Value is IResettable;

    public override void Recycled() => (Value as IResettable)?.Reset();

    public override void Discard()
    {
        (Value as IDisposable)?.Dispose();
        Value = null;
        Dep = default!;
        Pool<RememberHook<TDep>>.Return(this);
    }
}

[FramePath]
internal sealed class RefHook : Hook
{
    public NodeRef Ref = null!;

    public override void Discard()
    {
        Ref.Release();
        Ref = null!;
        Pool<RefHook>.Return(this);
    }
}

[FramePath]
internal abstract class EffectHook : Hook
{
    public bool Pending;
    public bool Live;
    public ComponentHost? Host;

    public abstract void Run(UiView view);
}

[FramePath]
internal sealed class EffectHook<TState, TDep> : EffectHook
    where TState : class
{
    public TState State = null!;
    public TDep Dep = default!;
    public HeldText Held;
    public EffectFn<TState, TDep> Fn = null!;

    [FramePath]
    public override void Run(UiView view)
    {
        Pending = false;
        if (Live && Host is { IsMounted: true })
            Fn(State, Dep, new EffectContext(view));
    }

    public override void Discard()
    {
        Live = false;
        Pending = false;
        Host = null;
        State = null!;
        Dep = default!;
        Fn = null!;
        Pool<EffectHook<TState, TDep>>.Return(this);
    }
}

[FramePath]
internal sealed class HookList
{
    public SharedList<Hook> Items;

    public void DiscardFrom(int index)
    {
        for (var i = Items.Count - 1; i >= index; i--)
            Items[i].Discard();
        Items.Truncate(index);
        Items.TrimExcess();
    }

    public void Release()
    {
        DiscardFrom(0);
        Items.Release();
    }
}

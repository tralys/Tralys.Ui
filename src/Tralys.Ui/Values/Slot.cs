using System.Runtime.CompilerServices;

namespace Tralys.Ui;

/// <summary>Content a component renders where and when it chooses: a static render delegate and the class state it
/// runs on, compared as a <see cref="Callback"/> is.</summary>
[FramePath]
public readonly struct Slot : IEquatable<Slot>
{
    readonly object? _fn;
    readonly object? _state;
    readonly Invoker? _invoker;

    Slot(object fn, object? state, Invoker invoker)
    {
        _fn = fn;
        _state = state;
        _invoker = invoker;
    }

    public static Slot Of([FramePath] Action<Ui> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        return new(render, null, Plain.Instance);
    }

    public static Slot Of<TState>(TState state, [FramePath] Action<Ui, TState> render)
        where TState : class
    {
        ArgumentNullException.ThrowIfNull(render);
        return new(render, state, One<TState>.Instance);
    }

    public bool IsEmpty => _fn is null;

    /// <summary>Renders the content at the open node.</summary>
    public void Render(Ui ui) => _invoker?.Invoke(ui, _fn!, _state);

    public bool Equals(Slot other) =>
        ReferenceEquals(_fn, other._fn) && ReferenceEquals(_state, other._state);

    public override bool Equals(object? obj) => obj is Slot other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(RuntimeHelpers.GetHashCode(_fn), RuntimeHelpers.GetHashCode(_state));

    public static bool operator ==(Slot a, Slot b) => a.Equals(b);

    public static bool operator !=(Slot a, Slot b) => !a.Equals(b);

    abstract class Invoker
    {
        public abstract void Invoke(Ui ui, object fn, object? state);
    }

    sealed class Plain : Invoker
    {
        public static readonly Plain Instance = new();

        public override void Invoke(Ui ui, object fn, object? state) =>
            Unsafe.As<Action<Ui>>(fn)(ui);
    }

    sealed class One<T> : Invoker
        where T : class
    {
        public static readonly One<T> Instance = new();

        public override void Invoke(Ui ui, object fn, object? state) =>
            Unsafe.As<Action<Ui, T>>(fn)(ui, Unsafe.As<T>(state)!);
    }
}

/// <summary>A <see cref="Slot"/> rendered once per item, with the item and its index.</summary>
[FramePath]
public readonly struct Slot<TItem> : IEquatable<Slot<TItem>>
{
    readonly object? _fn;
    readonly object? _state;
    readonly Invoker? _invoker;

    Slot(object fn, object? state, Invoker invoker)
    {
        _fn = fn;
        _state = state;
        _invoker = invoker;
    }

    public static Slot<TItem> Of([FramePath] Action<Ui, TItem, int> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        return new(render, null, Plain.Instance);
    }

    public static Slot<TItem> Of<TState>(
        TState state,
        [FramePath] Action<Ui, TState, TItem, int> render
    )
        where TState : class
    {
        ArgumentNullException.ThrowIfNull(render);
        return new(render, state, One<TState>.Instance);
    }

    public bool IsEmpty => _fn is null;

    public void Render(Ui ui, TItem item, int index) =>
        _invoker?.Invoke(ui, _fn!, _state, item, index);

    public bool Equals(Slot<TItem> other) =>
        ReferenceEquals(_fn, other._fn) && ReferenceEquals(_state, other._state);

    public override bool Equals(object? obj) => obj is Slot<TItem> other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(RuntimeHelpers.GetHashCode(_fn), RuntimeHelpers.GetHashCode(_state));

    public static bool operator ==(Slot<TItem> a, Slot<TItem> b) => a.Equals(b);

    public static bool operator !=(Slot<TItem> a, Slot<TItem> b) => !a.Equals(b);

    abstract class Invoker
    {
        public abstract void Invoke(Ui ui, object fn, object? state, TItem item, int index);
    }

    sealed class Plain : Invoker
    {
        public static readonly Plain Instance = new();

        public override void Invoke(Ui ui, object fn, object? state, TItem item, int index) =>
            Unsafe.As<Action<Ui, TItem, int>>(fn)(ui, item, index);
    }

    sealed class One<T> : Invoker
        where T : class
    {
        public static readonly One<T> Instance = new();

        public override void Invoke(Ui ui, object fn, object? state, TItem item, int index) =>
            Unsafe.As<Action<Ui, T, TItem, int>>(fn)(ui, Unsafe.As<T>(state)!, item, index);
    }
}

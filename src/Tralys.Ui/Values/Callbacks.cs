using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

/// <summary>A static delegate and the state it runs on. Two callbacks are equal when they hold the same delegate and
/// states, so props carrying one compare without re-rendering, and nothing boxes. One made from a
/// <see cref="Command"/> runs only while the command can run, and a component handed it greys while it
/// can't.</summary>
[FramePath]
public readonly struct Callback : IEquatable<Callback>
{
    readonly object? _fn;
    readonly object? _a;
    readonly object? _b;
    readonly long _value;
    readonly int _invoker;

    Callback(object fn, object? a, object? b, long value, int invoker)
    {
        _fn = fn;
        _a = a;
        _b = b;
        _value = value;
        _invoker = invoker;
    }

    public static Callback Of([FramePath] Action fn)
    {
        ArgumentNullException.ThrowIfNull(fn);
        return new(fn, null, null, 0, Plain.Id);
    }

    /// <summary>Runs <paramref name="fn"/> on <paramref name="state"/>: a class, or a value of at most 8 bytes that
    /// holds no references, kept inline.</summary>
    /// <exception cref="ArgumentException"><typeparamref name="TState"/> is a wider value, or one holding
    /// references.</exception>
    public static Callback Of<TState>(TState state, [FramePath] Action<TState> fn)
    {
        ArgumentNullException.ThrowIfNull(fn);
        CallbackState.Pack(state, out var a, out var value);
        return new(fn, a, null, value, One<TState>.Id);
    }

    public static Callback Of<T1, T2>(T1 a, T2 b, [FramePath] Action<T1, T2> fn)
        where T1 : class
        where T2 : class
    {
        ArgumentNullException.ThrowIfNull(fn);
        return new(fn, a, b, 0, Two<T1, T2>.Id);
    }

    /// <summary>Runs <paramref name="fn"/> on <paramref name="host"/> while the component it was taken from stays
    /// mounted, and does nothing once the handle is stale.</summary>
    public static Callback Of<TProps>(Host<TProps> host, [FramePath] Action<Host<TProps>> fn)
        where TProps : struct, IEquatable<TProps>
    {
        ArgumentNullException.ThrowIfNull(fn);
        return new(fn, host.Owner, null, host.Generation, OfHost<TProps>.Id);
    }

    /// <summary>Runs <paramref name="command"/> while it can run; null makes an empty callback.</summary>
    public static implicit operator Callback(Command? command) =>
        command is null ? default : new(command, null, null, 0, OfCommand.Id);

    internal static Callback Bind<T>(Command<T> command, T parameter)
    {
        CallbackState.Pack(parameter, out var a, out var value);
        return new(command, a, null, value, OfCommandWith<T>.Id);
    }

    public bool IsEmpty => _fn is null;

    /// <summary>Whether <see cref="Invoke"/> runs now: false only while the command it runs can't. Tracked.</summary>
    public bool CanRun => _invoker == 0 || Invokers<Invoker>.At(_invoker).CanRun(_fn!, _a, _value);

    internal bool IsGated => _invoker != 0 && Invokers<Invoker>.At(_invoker).IsGated;

    /// <summary>Runs it; one made from a command does nothing while the command can't run.</summary>
    public void Invoke()
    {
        if (_invoker != 0)
            Invokers<Invoker>.At(_invoker).Invoke(_fn!, _a, _b, _value);
    }

    public bool Equals(Callback other) =>
        ReferenceEquals(_fn, other._fn)
        && ReferenceEquals(_a, other._a)
        && ReferenceEquals(_b, other._b)
        && _value == other._value;

    public override bool Equals(object? obj) => obj is Callback other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(RuntimeHelpers.GetHashCode(_fn), RuntimeHelpers.GetHashCode(_a), _value);

    public static bool operator ==(Callback a, Callback b) => a.Equals(b);

    public static bool operator !=(Callback a, Callback b) => !a.Equals(b);

    abstract class Invoker
    {
        public virtual bool IsGated => false;

        public abstract void Invoke(object fn, object? a, object? b, long value);

        public virtual bool CanRun(object fn, object? a, long value) => true;
    }

    sealed class Plain : Invoker
    {
        public static readonly int Id = Invokers<Invoker>.Add(new Plain());

        public override void Invoke(object fn, object? a, object? b, long value) =>
            Unsafe.As<Action>(fn)();
    }

    sealed class One<T> : Invoker
    {
        public static readonly int Id = Invokers<Invoker>.Add(new One<T>());

        public override void Invoke(object fn, object? a, object? b, long value) =>
            Unsafe.As<Action<T>>(fn)(CallbackState.Unpack<T>(a, value));
    }

    sealed class Two<T1, T2> : Invoker
        where T1 : class
        where T2 : class
    {
        public static readonly int Id = Invokers<Invoker>.Add(new Two<T1, T2>());

        public override void Invoke(object fn, object? a, object? b, long value) =>
            Unsafe.As<Action<T1, T2>>(fn)(Unsafe.As<T1>(a)!, Unsafe.As<T2>(b)!);
    }

    sealed class OfHost<TProps> : Invoker
        where TProps : struct, IEquatable<TProps>
    {
        public static readonly int Id = Invokers<Invoker>.Add(new OfHost<TProps>());

        public override void Invoke(object fn, object? a, object? b, long value)
        {
            var host = new Host<TProps>(Unsafe.As<ComponentHost<TProps>>(a), (int)value);
            if (host.IsMounted)
                Unsafe.As<Action<Host<TProps>>>(fn)(host);
        }
    }

    sealed class OfCommand : Invoker
    {
        public static readonly int Id = Invokers<Invoker>.Add(new OfCommand());

        public override bool IsGated => true;

        public override void Invoke(object fn, object? a, object? b, long value) =>
            Unsafe.As<Command>(fn).Run();

        public override bool CanRun(object fn, object? a, long value) =>
            Unsafe.As<Command>(fn).CanRun;
    }

    sealed class OfCommandWith<T> : Invoker
    {
        public static readonly int Id = Invokers<Invoker>.Add(new OfCommandWith<T>());

        public override bool IsGated => true;

        public override void Invoke(object fn, object? a, object? b, long value) =>
            Unsafe.As<Command<T>>(fn).Run(CallbackState.Unpack<T>(a, value));

        public override bool CanRun(object fn, object? a, long value) =>
            Unsafe.As<Command<T>>(fn).CanRun(CallbackState.Unpack<T>(a, value));
    }
}

/// <summary>A <see cref="Callback"/> that takes an argument, such as the value a change reports. One made from a
/// <see cref="Command{T}"/> runs it with the argument only while it can run with it.</summary>
[FramePath]
public readonly struct Callback<TArg> : IEquatable<Callback<TArg>>
{
    readonly object? _fn;
    readonly object? _a;
    readonly object? _b;
    readonly long _value;
    readonly int _invoker;

    Callback(object fn, object? a, object? b, long value, int invoker)
    {
        _fn = fn;
        _a = a;
        _b = b;
        _value = value;
        _invoker = invoker;
    }

    public static Callback<TArg> Of([FramePath] Action<TArg> fn)
    {
        ArgumentNullException.ThrowIfNull(fn);
        return new(fn, null, null, 0, Plain.Id);
    }

    /// <summary>Runs <paramref name="fn"/> on <paramref name="state"/>: a class, or a value of at most 8 bytes that
    /// holds no references, kept inline.</summary>
    /// <exception cref="ArgumentException"><typeparamref name="TState"/> is a wider value, or one holding
    /// references.</exception>
    public static Callback<TArg> Of<TState>(TState state, [FramePath] Action<TState, TArg> fn)
    {
        ArgumentNullException.ThrowIfNull(fn);
        CallbackState.Pack(state, out var a, out var value);
        return new(fn, a, null, value, One<TState>.Id);
    }

    public static Callback<TArg> Of<T1, T2>(T1 a, T2 b, [FramePath] Action<T1, T2, TArg> fn)
        where T1 : class
        where T2 : class
    {
        ArgumentNullException.ThrowIfNull(fn);
        return new(fn, a, b, 0, Two<T1, T2>.Id);
    }

    /// <summary>Runs <paramref name="fn"/> on <paramref name="host"/> while the component it was taken from stays
    /// mounted, and does nothing once the handle is stale.</summary>
    public static Callback<TArg> Of<TProps>(
        Host<TProps> host,
        [FramePath] Action<Host<TProps>, TArg> fn
    )
        where TProps : struct, IEquatable<TProps>
    {
        ArgumentNullException.ThrowIfNull(fn);
        return new(fn, host.Owner, null, host.Generation, OfHost<TProps>.Id);
    }

    /// <summary>Runs <paramref name="command"/> with the argument while it can run with it; null makes an empty
    /// callback.</summary>
    public static implicit operator Callback<TArg>(Command<TArg>? command) =>
        command is null ? default : new(command, null, null, 0, OfCommand.Id);

    public bool IsEmpty => _fn is null;

    /// <summary>Runs it; one made from a command does nothing while the command can't run with
    /// <paramref name="arg"/>.</summary>
    public void Invoke(TArg arg)
    {
        if (_invoker != 0)
            Invokers<Invoker>.At(_invoker).Invoke(_fn!, _a, _b, _value, arg);
    }

    public bool Equals(Callback<TArg> other) =>
        ReferenceEquals(_fn, other._fn)
        && ReferenceEquals(_a, other._a)
        && ReferenceEquals(_b, other._b)
        && _value == other._value;

    public override bool Equals(object? obj) => obj is Callback<TArg> other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(RuntimeHelpers.GetHashCode(_fn), RuntimeHelpers.GetHashCode(_a), _value);

    public static bool operator ==(Callback<TArg> a, Callback<TArg> b) => a.Equals(b);

    public static bool operator !=(Callback<TArg> a, Callback<TArg> b) => !a.Equals(b);

    abstract class Invoker
    {
        public abstract void Invoke(object fn, object? a, object? b, long value, TArg arg);
    }

    sealed class Plain : Invoker
    {
        public static readonly int Id = Invokers<Invoker>.Add(new Plain());

        public override void Invoke(object fn, object? a, object? b, long value, TArg arg) =>
            Unsafe.As<Action<TArg>>(fn)(arg);
    }

    sealed class One<T> : Invoker
    {
        public static readonly int Id = Invokers<Invoker>.Add(new One<T>());

        public override void Invoke(object fn, object? a, object? b, long value, TArg arg) =>
            Unsafe.As<Action<T, TArg>>(fn)(CallbackState.Unpack<T>(a, value), arg);
    }

    sealed class Two<T1, T2> : Invoker
        where T1 : class
        where T2 : class
    {
        public static readonly int Id = Invokers<Invoker>.Add(new Two<T1, T2>());

        public override void Invoke(object fn, object? a, object? b, long value, TArg arg) =>
            Unsafe.As<Action<T1, T2, TArg>>(fn)(Unsafe.As<T1>(a)!, Unsafe.As<T2>(b)!, arg);
    }

    sealed class OfHost<TProps> : Invoker
        where TProps : struct, IEquatable<TProps>
    {
        public static readonly int Id = Invokers<Invoker>.Add(new OfHost<TProps>());

        public override void Invoke(object fn, object? a, object? b, long value, TArg arg)
        {
            var host = new Host<TProps>(Unsafe.As<ComponentHost<TProps>>(a), (int)value);
            if (host.IsMounted)
                Unsafe.As<Action<Host<TProps>, TArg>>(fn)(host, arg);
        }
    }

    sealed class OfCommand : Invoker
    {
        public static readonly int Id = Invokers<Invoker>.Add(new OfCommand());

        public override void Invoke(object fn, object? a, object? b, long value, TArg arg) =>
            Unsafe.As<Command<TArg>>(fn).Run(arg);
    }
}

public static class CommandExtensions
{
    /// <summary>A callback that runs <paramref name="command"/> with <paramref name="parameter"/> while it can run
    /// with it: a class, or a value of at most 8 bytes that holds no references, kept inline. A null command makes an
    /// empty callback.</summary>
    /// <exception cref="ArgumentException"><typeparamref name="T"/> is a wider value, or one holding
    /// references.</exception>
    [FramePath]
    public static Callback With<T>(this Command<T>? command, T parameter) =>
        command is null ? default : Callback.Bind(command, parameter);
}

[FramePath]
public delegate void RefAction<TEvent>(ref TEvent e)
    where TEvent : allows ref struct;

[FramePath]
public delegate void RefAction<TState, TEvent>(TState state, ref TEvent e)
    where TEvent : allows ref struct;

[FramePath]
public delegate void RefAction<T1, T2, TEvent>(T1 a, T2 b, ref TEvent e)
    where TEvent : allows ref struct;

/// <summary>A static handler of an event passed by reference, and the state it runs on; compared as a
/// <see cref="Callback"/> is.</summary>
[FramePath]
public readonly struct Handler<TEvent> : IEquatable<Handler<TEvent>>
    where TEvent : allows ref struct
{
    readonly object? _fn;
    readonly object? _a;
    readonly object? _b;
    readonly long _value;
    readonly int _invoker;

    Handler(object fn, object? a, object? b, long value, int invoker)
    {
        _fn = fn;
        _a = a;
        _b = b;
        _value = value;
        _invoker = invoker;
    }

    public static Handler<TEvent> Of(RefAction<TEvent> fn)
    {
        ArgumentNullException.ThrowIfNull(fn);
        return new(fn, null, null, 0, Plain.Id);
    }

    /// <summary>Runs <paramref name="fn"/> on <paramref name="state"/>: a class, or a value of at most 8 bytes that
    /// holds no references, kept inline.</summary>
    /// <exception cref="ArgumentException"><typeparamref name="TState"/> is a wider value, or one holding
    /// references.</exception>
    public static Handler<TEvent> Of<TState>(TState state, RefAction<TState, TEvent> fn)
    {
        ArgumentNullException.ThrowIfNull(fn);
        CallbackState.Pack(state, out var a, out var value);
        return new(fn, a, null, value, One<TState>.Id);
    }

    public static Handler<TEvent> Of<T1, T2>(T1 a, T2 b, RefAction<T1, T2, TEvent> fn)
        where T1 : class
        where T2 : class
    {
        ArgumentNullException.ThrowIfNull(fn);
        return new(fn, a, b, 0, Two<T1, T2>.Id);
    }

    /// <summary>Runs <paramref name="fn"/> on <paramref name="host"/> while the component it was taken from stays
    /// mounted, and does nothing once the handle is stale.</summary>
    public static Handler<TEvent> Of<TProps>(Host<TProps> host, RefAction<Host<TProps>, TEvent> fn)
        where TProps : struct, IEquatable<TProps>
    {
        ArgumentNullException.ThrowIfNull(fn);
        return new(fn, host.Owner, null, host.Generation, OfHost<TProps>.Id);
    }

    public bool IsEmpty => _fn is null;

    internal void Invoke(ref TEvent e)
    {
        if (_invoker != 0)
            Invokers<Invoker>.At(_invoker).Invoke(_fn!, _a, _b, _value, ref e);
    }

    public bool Equals(Handler<TEvent> other) =>
        ReferenceEquals(_fn, other._fn)
        && ReferenceEquals(_a, other._a)
        && ReferenceEquals(_b, other._b)
        && _value == other._value;

    public override bool Equals(object? obj) => obj is Handler<TEvent> other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(RuntimeHelpers.GetHashCode(_fn), RuntimeHelpers.GetHashCode(_a), _value);

    public static bool operator ==(Handler<TEvent> a, Handler<TEvent> b) => a.Equals(b);

    public static bool operator !=(Handler<TEvent> a, Handler<TEvent> b) => !a.Equals(b);

    abstract class Invoker
    {
        public abstract void Invoke(object fn, object? a, object? b, long value, ref TEvent e);
    }

    sealed class Plain : Invoker
    {
        public static readonly int Id = Invokers<Invoker>.Add(new Plain());

        public override void Invoke(object fn, object? a, object? b, long value, ref TEvent e) =>
            Unsafe.As<RefAction<TEvent>>(fn)(ref e);
    }

    sealed class One<T> : Invoker
    {
        public static readonly int Id = Invokers<Invoker>.Add(new One<T>());

        public override void Invoke(object fn, object? a, object? b, long value, ref TEvent e) =>
            Unsafe.As<RefAction<T, TEvent>>(fn)(CallbackState.Unpack<T>(a, value), ref e);
    }

    sealed class Two<T1, T2> : Invoker
        where T1 : class
        where T2 : class
    {
        public static readonly int Id = Invokers<Invoker>.Add(new Two<T1, T2>());

        public override void Invoke(object fn, object? a, object? b, long value, ref TEvent e) =>
            Unsafe.As<RefAction<T1, T2, TEvent>>(fn)(Unsafe.As<T1>(a)!, Unsafe.As<T2>(b)!, ref e);
    }

    sealed class OfHost<TProps> : Invoker
        where TProps : struct, IEquatable<TProps>
    {
        public static readonly int Id = Invokers<Invoker>.Add(new OfHost<TProps>());

        public override void Invoke(object fn, object? a, object? b, long value, ref TEvent e)
        {
            var host = new Host<TProps>(Unsafe.As<ComponentHost<TProps>>(a), (int)value);
            if (host.IsMounted)
                Unsafe.As<RefAction<Host<TProps>, TEvent>>(fn)(host, ref e);
        }
    }
}

[FramePath]
static class CallbackState
{
    public static void Pack<T>(T state, out object? reference, out long value)
    {
        value = 0;
        if (!typeof(T).IsValueType)
        {
            reference = Unsafe.As<T, object?>(ref state);
            return;
        }
        if (
            RuntimeHelpers.IsReferenceOrContainsReferences<T>()
            || Unsafe.SizeOf<T>() > sizeof(long)
        )
            throw new ArgumentException(
                "A callback's state is a class, or a value of at most 8 bytes that holds no references.",
                nameof(state)
            );
        reference = null;
        Unsafe.WriteUnaligned(ref Unsafe.As<long, byte>(ref value), state);
    }

    public static T Unpack<T>(object? reference, long value) =>
        typeof(T).IsValueType
            ? Unsafe.ReadUnaligned<T>(ref Unsafe.As<long, byte>(ref value))
            : Unsafe.As<object?, T>(ref reference);
}

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Tralys.Ui.Mvvm;

/// <summary>What re-runs when a source it read changes: a component's render, a canvas's drawing.</summary>
public interface IChangeObserver
{
    /// <summary>Runs on the observer's dispatcher thread after <paramref name="property"/> of
    /// <paramref name="source"/> changed.</summary>
    void OnChanged(object source, int property);

    /// <summary>The links to what the observer read, kept by the observer and written only by observation.</summary>
    ref Dependencies Dependencies { get; }
}

/// <summary>A source whose reads are tracked: an observable object, list or cell.</summary>
public interface IObservable
{
    /// <summary>The observers linked to it, kept by the source and written only by observation.</summary>
    ref ObserverSet Observers { get; }
}

/// <summary>Tracks reads, and links observers to what they read.</summary>
[FramePath]
public static class Observation
{
    [ThreadStatic]
    static IChangeObserver? t_current;

    /// <summary>Records that <paramref name="property"/> of <paramref name="source"/> was read, when a tracking scope
    /// is open on this thread. Indices past 63 share bit 63.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Read(IObservable source, int property)
    {
        var current = t_current;
        if (current is not null)
            current.Dependencies.Record(source, Bit(property), current);
    }

    /// <summary>Records reads for <paramref name="observer"/> until the scope completes, which links what was read and
    /// unlinks what no longer is. Scopes nest.</summary>
    public static ObservationScope Track(IChangeObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var previous = t_current;
        observer.Dependencies.Begin();
        t_current = observer;
        return new ObservationScope(observer, previous);
    }

    /// <summary>Runs <paramref name="read"/> with no scope tracking it.</summary>
    public static T Untracked<TState, T>(TState state, Func<TState, T> read)
    {
        var previous = t_current;
        t_current = null;
        try
        {
            return read(state);
        }
        finally
        {
            t_current = previous;
        }
    }

    internal static void Untracked<TState>(TState state, Action<TState> run)
    {
        var previous = t_current;
        t_current = null;
        try
        {
            run(state);
        }
        finally
        {
            t_current = previous;
        }
    }

    /// <summary>Runs <paramref name="select"/> tracked, and again whenever what it read changes; runs
    /// <paramref name="react"/> untracked with each result that differs from the last, and with the first unless
    /// <paramref name="now"/> is false. A change made off <paramref name="dispatcher"/>'s thread is posted there. A
    /// fault in either goes to <paramref name="onFault"/>, and the watch keeps watching; without one it unwinds the
    /// change's writer.</summary>
    /// <returns>The watch; disposing it unlinks it from everything it read.</returns>
    public static IDisposable Watch<T>(
        Func<T> select,
        Action<T> react,
        IUiDispatcher? dispatcher = null,
        bool now = true,
        Action<Exception>? onFault = null
    )
    {
        ArgumentNullException.ThrowIfNull(select);
        ArgumentNullException.ThrowIfNull(react);
        return new Watcher<T>(select, react, dispatcher, now, onFault);
    }

    /// <summary>For a setter: when <paramref name="owner"/> is observed, but only from other threads, posts the write
    /// of <paramref name="value"/> to each observing thread instead, where the first to run it writes.</summary>
    /// <returns>Whether the write was posted, and the setter must not write.</returns>
    public static bool Defers<TOwner, T>(TOwner owner, T value, Action<TOwner, T> write)
        where TOwner : class, IObservable
    {
        ref var observers = ref owner.Observers;
        if (!observers.IsOffThread())
            return false;

        observers.PostToEach(
            new PendingWrite<TOwner, T>(owner, value, write),
            PendingWrite<TOwner, T>.Run
        );
        return true;
    }

    internal static void Exit(IChangeObserver? previous) => t_current = previous;

    internal static ulong Bit(int property) => 1UL << Math.Clamp(property, 0, 63);

    sealed class PendingWrite<TOwner, T>(TOwner owner, T value, Action<TOwner, T> write)
        where TOwner : class
    {
        public static readonly Action<PendingWrite<TOwner, T>> Run = static pending =>
        {
            if (Interlocked.Exchange(ref pending._claimed, 1) == 0)
                pending._write(pending._owner, pending._value);
        };

        readonly TOwner _owner = owner;
        readonly T _value = value;
        readonly Action<TOwner, T> _write = write;
        int _claimed;
    }
}

/// <summary>An open tracking scope; completing it links what was read.</summary>
public readonly struct ObservationScope : IDisposable
{
    readonly IChangeObserver _observer;
    readonly IChangeObserver? _previous;

    internal ObservationScope(IChangeObserver observer, IChangeObserver? previous)
    {
        _observer = observer;
        _previous = previous;
    }

    /// <summary>Links what was read and unlinks what no longer was.</summary>
    public void Dispose() => Complete(keepDropped: false);

    /// <summary>Links what was read; with <paramref name="keepDropped"/>, what was read before stays linked too, as for a
    /// render that threw before reaching it or a pass that read only part of what its observer depends on.</summary>
    [FramePath]
    public void Complete(bool keepDropped)
    {
        Observation.Exit(_previous);
        _observer.Dependencies.Commit(_observer, keepDropped);
    }
}

/// <summary>An observer's links to its sources: what it read, the bits it read of each, and where its link sits in
/// each source's <see cref="ObserverSet"/>. A mutable struct kept in a field of its observer.</summary>
[FramePath]
public struct Dependencies
{
    Entry[]? _entries;
    int _count;
    Entry[]? _next;
    int _nextCount;
    int _epoch;
    readonly IUiDispatcher? _dispatcher;

    /// <param name="dispatcher">The thread the observer runs on; a change made on another is posted there.</param>
    public Dependencies(IUiDispatcher? dispatcher) => _dispatcher = dispatcher;

    /// <summary>How many sources the observer is linked to.</summary>
    public readonly int Count => _count;

    public readonly IUiDispatcher? Dispatcher => _dispatcher;

    /// <summary>The source of the link at <paramref name="index"/>, from 0 below <see cref="Count"/>.</summary>
    public readonly IObservable SourceAt(int index) => Link(index).Source!;

    /// <summary>The properties the observer read of <see cref="SourceAt"/>'s source, a bit each; bit 63 stands for
    /// 63 and up.</summary>
    public readonly ulong MaskAt(int index) => Link(index).Mask;

    readonly ref readonly Entry Link(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _count);
        return ref _entries![index];
    }

    /// <summary>Unlinks the observer from every source.</summary>
    public void Clear()
    {
        for (var i = 0; i < _count; i++)
        {
            ref var entry = ref _entries![i];
            if (entry.Slot >= 0)
                entry.Source!.Observers.Remove(entry.Slot);
            entry = default;
        }
        _count = 0;
        ClearNext();
        Return(ref _entries);
        Return(ref _next);
    }

    internal void Begin()
    {
        ClearNext();
        _epoch++;
    }

    internal void Record(IObservable source, ulong bit, IChangeObserver self)
    {
        ref var observers = ref source.Observers;
        if (observers.StampedBy(self, _epoch, out var at) && at < _nextCount)
        {
            _next![at].Mask |= bit;
            return;
        }

        Grow(ref _next, _nextCount + 1, _nextCount);
        _next![_nextCount] = new Entry(source, bit, -1);
        observers.Stamp(self, _epoch, _nextCount);
        _nextCount++;
    }

    internal void Commit(IChangeObserver self, bool keepDropped)
    {
        _epoch++;
        var written = 0;
        for (var read = 0; read < _nextCount; read++)
        {
            var entry = _next![read];
            ref var observers = ref entry.Source!.Observers;
            if (observers.StampedBy(self, _epoch, out var at))
            {
                _next[at].Mask |= entry.Mask;
                continue;
            }
            observers.Stamp(self, _epoch, written);
            _next[written++] = entry with { Slot = -1 };
        }
        if (_next is not null)
            Array.Clear(_next, written, _nextCount - written);
        _nextCount = written;

        if (Unchanged())
        {
            ClearNext();
            return;
        }

        for (var i = 0; i < _count; i++)
        {
            var old = _entries![i];
            ref var observers = ref old.Source!.Observers;
            if (observers.StampedBy(self, _epoch, out var at))
            {
                if (keepDropped)
                    _next![at].Mask |= old.Mask;
                if (old.Slot >= 0)
                {
                    _next![at].Slot = old.Slot;
                    observers.Update(old.Slot, _next[at].Mask, at);
                }
            }
            else if (keepDropped)
            {
                if (old.Slot >= 0)
                {
                    Grow(ref _next, _nextCount + 1, _nextCount);
                    _next![_nextCount] = old;
                    observers.Update(old.Slot, old.Mask, _nextCount);
                    _nextCount++;
                }
            }
            else if (old.Slot >= 0)
                observers.Remove(old.Slot);
        }

        for (var i = 0; i < _nextCount; i++)
        {
            ref var entry = ref _next![i];
            if (entry.Slot < 0)
                entry.Slot = entry.Source!.Observers.Add(self, _dispatcher, entry.Mask, i);
        }

        var dropped = _count;
        (_entries, _next) = (_next, _entries);
        _count = _nextCount;
        if (_next is not null)
            Array.Clear(_next, 0, dropped);
        _nextCount = 0;
        // The halves trade places each render, so one left smaller would grow again on every other render.
        if (_entries is { Length: var length } && (_next?.Length ?? 0) < length)
            Grow(ref _next, length, 0);
    }

    readonly bool Unchanged()
    {
        if (_nextCount != _count)
            return false;
        for (var i = 0; i < _count; i++)
        {
            ref readonly var old = ref _entries![i];
            ref readonly var now = ref _next![i];
            if (old.Slot < 0 || !ReferenceEquals(old.Source, now.Source) || old.Mask != now.Mask)
                return false;
        }
        return true;
    }

    internal readonly void SetSlot(int entry, int slot) => _entries![entry].Slot = slot;

    void ClearNext()
    {
        if (_next is not null && _nextCount > 0)
            Array.Clear(_next, 0, _nextCount);
        _nextCount = 0;
    }

    static void Grow(ref Entry[]? array, int needed, int count)
    {
        if (array is not null && array.Length >= needed)
            return;
        var grown = SharedArrays<Entry>.Rent(Math.Max(needed, (array?.Length ?? 0) * 2));
        if (array is not null)
        {
            array.AsSpan(0, count).CopyTo(grown);
            SharedArrays<Entry>.Return(array);
        }
        array = grown;
    }

    static void Return(ref Entry[]? array)
    {
        if (array is null)
            return;
        SharedArrays<Entry>.Return(array);
        array = null;
    }

    internal record struct Entry(IObservable? Source, ulong Mask, int Slot);
}

/// <summary>A source's links to the observers that read it: each with the bits it read and the thread it runs on. A
/// mutable struct kept in a field of its source. The first link is kept inline and the rest in an array the thread
/// pools, so linking allocates nothing once warm.</summary>
[FramePath]
public struct ObserverSet
{
    const int PooledLength = 4;

    Link _head;
    Link[]? _links;
    int _count;
    IChangeObserver? _stamp;
    int _stampEpoch;
    int _stampIndex;

    // Readers on other threads, which a relink must not recycle the array under.
    int _readers;

    /// <summary>How many observers are linked.</summary>
    public readonly int Count => _count;

    /// <summary>Tells each observer that read <paramref name="property"/> that it changed: at once on its own thread,
    /// posted to it from any other.</summary>
    public readonly void Notify(object source, int property)
    {
        var bit = Observation.Bit(property);
        for (var i = _count - 1; i >= 0; i--)
        {
            if (!TryGet(i, out var link) || (link.Mask & bit) == 0 || link.Observer is null)
                continue;
            if (link.Dispatcher is null || link.Dispatcher.CheckAccess())
                link.Observer.OnChanged(source, property);
            else
                link.Dispatcher.Post(new Notice(link.Observer, source, property), Notice.Deliver);
        }
    }

    /// <summary>Unlinks every observer, as a pooled source does before it is used again.</summary>
    public void Clear()
    {
        for (var i = 0; i < _count; i++)
        {
            ref var link = ref At(i);
            link.Observer?.Dependencies.SetSlot(link.Entry, -1);
            link = default;
        }
        _count = 0;
        _stamp = null;
        ReturnLinks();
    }

    // Read from any thread while the dispatcher's thread relinks: a torn or stale link only posts once more or less.
    internal readonly bool IsOffThread()
    {
        Interlocked.Increment(ref Unsafe.AsRef(in _readers));
        try
        {
            var count = Volatile.Read(ref Unsafe.AsRef(in _count));
            var observed = false;
            for (var i = 0; i < count; i++)
            {
                if (!TryGet(i, out var link) || link.Observer is null)
                    continue;
                if (link.Dispatcher is null || link.Dispatcher.CheckAccess())
                    return false;
                observed = true;
            }
            return observed;
        }
        finally
        {
            Interlocked.Decrement(ref Unsafe.AsRef(in _readers));
        }
    }

    internal readonly void PostToEach<TState>(TState state, Action<TState> action)
    {
        Interlocked.Increment(ref Unsafe.AsRef(in _readers));
        try
        {
            var count = Volatile.Read(ref Unsafe.AsRef(in _count));
            for (var i = 0; i < count; i++)
            {
                if (!TryGet(i, out var link) || link.Dispatcher is not { } dispatcher)
                    continue;
                if (PostedBefore(i, dispatcher))
                    continue;
                dispatcher.Post(state, action);
            }
        }
        finally
        {
            Interlocked.Decrement(ref Unsafe.AsRef(in _readers));
        }
    }

    readonly bool PostedBefore(int index, IUiDispatcher dispatcher)
    {
        for (var i = 0; i < index; i++)
        {
            if (TryGet(i, out var link) && ReferenceEquals(link.Dispatcher, dispatcher))
                return true;
        }
        return false;
    }

    readonly bool TryGet(int slot, out Link link)
    {
        if (slot == 0)
        {
            link = _head;
            return true;
        }
        var links = Volatile.Read(ref Unsafe.AsRef(in _links));
        if (links is null || slot - 1 >= links.Length)
        {
            link = default;
            return false;
        }
        link = links[slot - 1];
        return true;
    }

    [UnscopedRef]
    ref Link At(int slot) => ref slot == 0 ? ref _head : ref _links![slot - 1];

    internal readonly bool StampedBy(IChangeObserver observer, int epoch, out int index)
    {
        index = _stampIndex;
        return ReferenceEquals(_stamp, observer) && _stampEpoch == epoch;
    }

    internal void Stamp(IChangeObserver observer, int epoch, int index)
    {
        _stamp = observer;
        _stampEpoch = epoch;
        _stampIndex = index;
    }

    internal int Add(IChangeObserver observer, IUiDispatcher? dispatcher, ulong mask, int entry)
    {
        var slot = _count;
        if (slot > 0)
        {
            if (_links is null)
                _links = SharedArrays<Link>.Rent(PooledLength);
            else if (slot - 1 == _links.Length)
            {
                var old = _links;
                var grown = SharedArrays<Link>.Rent(old.Length * 2);
                Array.Copy(old, grown, old.Length);
                Interlocked.Exchange(ref _links, grown);
                Retire(old);
            }
        }
        At(slot) = new Link(observer, dispatcher, mask, entry);
        Volatile.Write(ref _count, slot + 1);
        return slot;
    }

    internal void Update(int slot, ulong mask, int entry)
    {
        ref var link = ref At(slot);
        link.Mask = mask;
        link.Entry = entry;
    }

    internal void Remove(int slot)
    {
        if (ReferenceEquals(_stamp, At(slot).Observer))
            _stamp = null;
        var last = _count - 1;
        if (slot != last)
        {
            var moved = At(last);
            At(slot) = moved;
            moved.Observer!.Dependencies.SetSlot(moved.Entry, slot);
        }
        At(last) = default;
        Volatile.Write(ref _count, last);
        if (last <= 1)
            ReturnLinks();
    }

    // Published first and fenced: a reader seen at zero here starts after the swap, so none still holds the array.
    readonly void Retire(Link[] links)
    {
        if (Volatile.Read(ref Unsafe.AsRef(in _readers)) == 0)
            SharedArrays<Link>.Return(links);
    }

    void ReturnLinks()
    {
        if (_links is not { } links)
            return;
        Interlocked.Exchange(ref _links, null);
        Retire(links);
    }

    record struct Link(IChangeObserver? Observer, IUiDispatcher? Dispatcher, ulong Mask, int Entry);

    readonly record struct Notice(IChangeObserver Observer, object Source, int Property)
    {
        public static readonly Action<Notice> Deliver = static notice =>
            notice.Observer.OnChanged(notice.Source, notice.Property);
    }
}

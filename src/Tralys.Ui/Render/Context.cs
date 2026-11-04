using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

/// <summary>A value a component provides to the components below it, read with <see cref="Ui.Use"/>.</summary>
public sealed class ContextKey<T>(string name, T fallback = default!)
    where T : IEquatable<T>
{
    public string Name { get; } = name;

    /// <summary>What a read with no provider above it gets.</summary>
    public T Fallback { get; } = fallback;

    public override string ToString() => Name;
}

[FramePath]
internal abstract class ProvidedEntry : IObservable
{
    internal ObserverSet _observers;
    public object Key = null!;
    public bool Touched;

    public ref ObserverSet Observers => ref _observers;

    public void Withdraw()
    {
        _observers.Notify(this, 0);
        _observers.Clear();
        Release();
    }

    protected abstract void Release();
}

[FramePath]
internal sealed class ProvidedEntry<T> : ProvidedEntry
    where T : IEquatable<T>
{
    public T Value = default!;
    public HeldText Held;

    protected override void Release()
    {
        Value = default!;
        Key = null!;
        Pool<ProvidedEntry<T>>.Return(this);
    }
}

[FramePath]
internal static class ContextLookup
{
    public static T Find<T>(Node? from, ContextKey<T> key, out ProvidedEntry<T>? entry)
        where T : IEquatable<T>
    {
        for (var node = from; node is not null; node = node._parent)
        {
            if (node.Host is not { } host)
                continue;
            var provided = host.Provided.AsSpan();
            for (var i = 0; i < provided.Length; i++)
            {
                if (ReferenceEquals(provided[i].Key, key))
                {
                    entry = (ProvidedEntry<T>)provided[i];
                    return entry.Value;
                }
            }
        }
        entry = null;
        return key.Fallback;
    }

    public static void Appeared(Node node, object key)
    {
        for (var child = node._first; child is not null; child = child._next)
        {
            if (child.Host is { } host && host.UsedKeys.IndexOf(key) >= 0)
                host.Invalidate();
            Appeared(child, key);
        }
    }
}

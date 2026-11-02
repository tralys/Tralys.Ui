namespace Tralys.Ui;

/// <summary>The listeners of an <see cref="IItemsSource"/>: a source keeps one in a field, hands it
/// <see cref="IItemsSource.AddListener"/> and <see cref="IItemsSource.RemoveListener"/>, and calls <see cref="Raise"/>
/// after each change.</summary>
/// <remarks>A listener may add or remove listeners, itself or another, while it is told of a change: one removed is
/// told nothing more, not even the rest of that change, one added is told from the next change on, and every other is
/// told of the change once. It may not change the source: a change raised while one is being told throws, as the
/// listeners still to be told would hear of the two out of order. Telling allocates nothing; adding allocates only as
/// the list grows.<para>A mutable struct: keep it in a field that is not <c>readonly</c>, and never copy it, or the copy
/// and the field go on apart.</para></remarks>
public struct ItemsSourceListeners
{
    HandlerList<IItemsSourceListener?> _listeners;

    internal readonly bool IsEmpty
    {
        get
        {
            for (var i = 0; i < _listeners.Count; i++)
            {
                if (_listeners[i] is not null)
                    return false;
            }
            return true;
        }
    }

    /// <summary>Adds <paramref name="listener"/>, told of every change raised from now on.</summary>
    public void Add(IItemsSourceListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _listeners.Add(listener);
    }

    /// <summary>Removes <paramref name="listener"/>, if it listens, which is then told of no change, even one being
    /// raised now.</summary>
    public void Remove(IItemsSourceListener listener)
    {
        for (var i = 0; i < _listeners.Count; i++)
        {
            if (ReferenceEquals(_listeners[i], listener))
            {
                _listeners.RemoveAtSwap(i);
                return;
            }
        }
    }

    /// <summary>Tells every listener that <paramref name="source"/> changed as <paramref name="change"/> says.</summary>
    /// <exception cref="InvalidOperationException">A listener is being told of another change.</exception>
    [FramePath]
    public void Raise(IItemsSource source, in ItemsChange change)
    {
        if (_listeners.Count == 0)
            return;
        if (_listeners.IsRaising)
            ThrowRaising();

        var count = _listeners.Enter();
        try
        {
            for (var i = 0; i < count; i++)
                _listeners[i]?.OnItemsChanged(source, change);
        }
        finally
        {
            _listeners.Exit();
        }
    }

    static void ThrowRaising() =>
        throw new InvalidOperationException(
            "An items source changed while its listeners heard of a change: make the change once they have, as by posting it."
        );
}

using System.ComponentModel;

namespace Tralys.Ui.Mvvm;

/// <summary>The keys a list shows selected, changed only by applying the changes the list asks for. Every member reads
/// <see cref="Version"/>, so each read is tracked.</summary>
[FramePath]
public sealed class SelectionSet : ObservableObject, ISelection
{
    static readonly PropertyChangedEventArgs VersionChanged = PropertyChangedArgs.For(
        nameof(Version)
    );

    readonly HashSet<UiKey> _keys = [];
    int _version;

    /// <summary>Changes whenever the keys do.</summary>
    public int Version
    {
        get
        {
            Observation.Read(this, 0);
            return _version;
        }
    }

    public int Count
    {
        get
        {
            Observation.Read(this, 0);
            return _keys.Count;
        }
    }

    /// <summary>The one key, when there is exactly one; else null.</summary>
    public UiKey? Single
    {
        get
        {
            Observation.Read(this, 0);
            if (_keys.Count != 1)
                return null;
            foreach (var key in _keys)
                return key;
            return null;
        }
    }

    public bool Contains(UiKey key)
    {
        Observation.Read(this, 0);
        return _keys.Contains(key);
    }

    public KeyEnumerator GetEnumerator()
    {
        Observation.Read(this, 0);
        return new KeyEnumerator(_keys.GetEnumerator());
    }

    /// <summary>Applies <paramref name="change"/>, reading the keys of <paramref name="items"/> for a range or all.</summary>
    public void Apply<TItem>(in SelectionChange change, IItemsSource<TItem> items, KeyOf<TItem> key)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(key);
        var changed = false;
        switch (change.Action)
        {
            case SelectionAction.Replace:
                changed = Replace(change.Key);
                break;
            case SelectionAction.Toggle when change.Key.IsKeyed:
                if (!_keys.Remove(change.Key))
                    _keys.Add(change.Key);
                changed = true;
                break;
            case SelectionAction.Range:
                changed = Range(change.Anchor, change.Key, items, key);
                break;
            case SelectionAction.All:
                var count = items.Count;
                for (var i = 0; i < count; i++)
                    changed |= _keys.Add(key(items.ItemAt(i)));
                break;
            case SelectionAction.Clear:
                changed = _keys.Count > 0;
                _keys.Clear();
                break;
        }
        if (!changed)
            return;
        _version++;
        NotifyObservers(0);
        OnPropertyChanged(VersionChanged);
    }

    bool Replace(UiKey key)
    {
        if (_keys.Count == 1 && _keys.Contains(key))
            return false;
        var changed = _keys.Count > 0;
        _keys.Clear();
        if (key.IsKeyed)
            changed |= _keys.Add(key);
        return changed;
    }

    bool Range<TItem>(UiKey anchor, UiKey to, IItemsSource<TItem> items, KeyOf<TItem> key)
    {
        var from = -1;
        var end = -1;
        var count = items.Count;
        for (var i = 0; i < count && (from < 0 || end < 0); i++)
        {
            var at = key(items.ItemAt(i));
            if (at == anchor)
                from = i;
            if (at == to)
                end = i;
        }
        if (from < 0 || end < 0)
            return Replace(to);
        if (from > end)
            (from, end) = (end, from);
        _keys.Clear();
        for (var i = from; i <= end; i++)
            _keys.Add(key(items.ItemAt(i)));
        return true;
    }

    /// <summary>Walks the keys without allocating.</summary>
    public struct KeyEnumerator
    {
        HashSet<UiKey>.Enumerator _keys;

        internal KeyEnumerator(HashSet<UiKey>.Enumerator keys) => _keys = keys;

        public readonly UiKey Current => _keys.Current;

        public bool MoveNext() => _keys.MoveNext();
    }
}

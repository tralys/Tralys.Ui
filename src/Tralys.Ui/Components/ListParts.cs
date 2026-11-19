using System.ComponentModel;
using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Components;

// A list passes its arrows on at its ends, which would move the focus out of a list that is one Tab stop.
[FramePath]
internal static class ListEnds
{
    public static readonly Handler<KeyEvent> UpDown = Keep(vertical: true, horizontal: false);

    public static readonly Handler<KeyEvent> LeftRight = Keep(vertical: false, horizontal: true);

    public static readonly Handler<KeyEvent> Arrows = Keep(vertical: true, horizontal: true);

    static Handler<KeyEvent> Keep(bool vertical, bool horizontal) =>
        Handler<KeyEvent>.Of(
            new Axes(vertical, horizontal),
            static (axes, ref e) =>
            {
                if (
                    e.Modifiers != ModifierKeys.None
                    || !(
                        (axes.Vertical && e.Key is Key.Up or Key.Down)
                        || (axes.Horizontal && e.Key is Key.Left or Key.Right)
                    )
                )
                    return;
                e.PreventDefault();
                e.StopPropagation();
            }
        );

    sealed record Axes(bool Vertical, bool Horizontal);
}

[FramePath]
internal static class ListBarParts
{
    public static float Space(ScrollHandle scroll, ScrollbarVisibility visibility) =>
        ScrollAreaBar.Shows(scroll, visibility, vertical: true) ? ScrollAreaBar.Space : 0f;

    public static float SidewaysSpace(ScrollHandle scroll, ScrollbarVisibility visibility) =>
        ScrollAreaBar.Shows(scroll, visibility, vertical: false) ? ScrollAreaBar.Space : 0f;

    public static void ListBar(
        this Ui ui,
        ScrollHandle scroll,
        ScrollbarVisibility visibility,
        float top = 0f,
        float bottom = 0f,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.ScrollAreaBar(
            handle: scroll,
            vertical: true,
            visibility: visibility,
            shown: ui.IsHovered || ui.IsFocusWithin || ui.IsCapturing,
            position: Position.Absolute,
            top: top,
            right: 0f,
            bottom: bottom,
            key: key,
            file: file,
            line: line
        );

    // In flow under the scroller, so the bar shortens the viewport rather than covering the last row in view.
    public static void SidewaysListBar(
        this Ui ui,
        ScrollHandle scroll,
        ScrollbarVisibility visibility,
        float right = 0f,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.ScrollAreaBar(
            handle: scroll,
            vertical: false,
            visibility: visibility,
            shown: ui.IsHovered || ui.IsFocusWithin || ui.IsCapturing,
            margin: new Thickness(0f, 0f, right, 0f),
            key: key,
            file: file,
            line: line
        );
}

/// <summary>The keys a list shows selected: one key, or none.</summary>
[FramePath]
internal sealed class KeySelection : ISelection
{
    internal UiKey Key;

    public int Version => 0;

    public bool Contains(UiKey key) => Key.IsKeyed && key == Key;
}

/// <summary>Two <see cref="KeySelection"/>s taken in turn, so a list handed the other one re-reads which rows are
/// selected whenever the key changes.</summary>
[FramePath]
internal sealed class ShownKey
{
    readonly KeySelection _one = new();
    readonly KeySelection _other = new();
    KeySelection _shown;

    public ShownKey() => _shown = _one;

    public ISelection Of(UiKey key)
    {
        if (_shown.Key != key)
        {
            _shown = ReferenceEquals(_shown, _one) ? _other : _one;
            _shown.Key = key;
        }
        return _shown;
    }
}

/// <summary>An app's selection seen through a view of its own, so a list never takes it for a set it may skip a
/// repeated pick on, and a new view whenever the app hands over another selection.</summary>
[FramePath]
internal sealed class ShownSelection
{
    readonly View _one = new();
    readonly View _other = new();
    View _shown;

    public ShownSelection() => _shown = _one;

    public ISelection Of(ISelection? selection)
    {
        if (!ReferenceEquals(_shown.Inner, selection))
        {
            _shown = ReferenceEquals(_shown, _one) ? _other : _one;
            _shown.Inner = selection;
        }
        return _shown;
    }

    sealed class View : ISelection
    {
        internal ISelection? Inner;

        public int Version => Inner?.Version ?? 0;

        public bool Contains(UiKey key) => Inner?.Contains(key) == true;
    }
}

/// <summary>A set of keys a component keeps for itself; every read is tracked, and a change notifies.</summary>
[FramePath]
internal sealed class KeySet : ObservableObject, ISelection
{
    static readonly PropertyChangedEventArgs VersionChanged = PropertyChangedArgs.For(
        nameof(Version)
    );

    readonly HashSet<UiKey> _keys = [];
    int _version;

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

    public bool Contains(UiKey key)
    {
        Observation.Read(this, 0);
        return _keys.Contains(key);
    }

    public void Toggle(UiKey key)
    {
        if (!key.IsKeyed)
            return;
        if (!_keys.Remove(key))
            _keys.Add(key);
        Changed();
    }

    public void Set(UiKey key, bool on)
    {
        if (key.IsKeyed && (on ? _keys.Add(key) : _keys.Remove(key)))
            Changed();
    }

    public void Clear()
    {
        if (_keys.Count == 0)
            return;
        _keys.Clear();
        Changed();
    }

    public bool SetEquals(KeySet other)
    {
        Observation.Read(this, 0);
        Observation.Read(other, 0);
        if (_keys.Count != other._keys.Count)
            return false;
        foreach (var key in other._keys)
        {
            if (!_keys.Contains(key))
                return false;
        }
        return true;
    }

    public void CopyFrom(KeySet other)
    {
        if (SetEquals(other))
            return;
        _keys.Clear();
        foreach (var key in other._keys)
            _keys.Add(key);
        Changed();
    }

    void Changed()
    {
        _version++;
        NotifyObservers(0);
        OnPropertyChanged(VersionChanged);
    }
}

using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

/// <summary>How Tab, or the arrows, move through a container's children. Any mode but <see cref="Continue"/> makes
/// the container a group, ordered within and taking one place in its parent by its own <c>tabIndex</c>.</summary>
public enum Navigation : byte
{
    /// <summary>No group: the children join the nearest group around them.</summary>
    Continue,

    /// <summary>Wraps inside the group.</summary>
    Cycle,

    /// <summary>Stops at the group's ends, still taking the key.</summary>
    Contained,

    /// <summary>Follows the group's own order, then leaves past its last child.</summary>
    Local,

    /// <summary>Keeps navigation out of the children; the container can still be a stop.</summary>
    None,

    /// <summary>Tab stops in the group once: on its selected stop, else the one last focused there, else the container,
    /// else its first stop. As <c>arrowNavigation</c> it acts as <see cref="Continue"/>.</summary>
    Once,
}

public enum NavDirection : byte
{
    Left,
    Up,
    Right,
    Down,
}

/// <summary>What a drop does with the dragged data.</summary>
[Flags]
public enum DragEffect : byte
{
    None = 0,
    Copy = 1,
    Move = 2,
    Link = 4,
}

/// <summary>Where a shortcut is active.</summary>
public enum ShortcutScope : byte
{
    /// <summary>While focus is on or inside its host, or, with nothing focused, its host is on the key target's chain.</summary>
    FocusWithin,

    /// <summary>From any mounted host not under a modal, focus or not.</summary>
    View,
}

/// <summary>A key and its modifiers, or a gamepad button. A chord with neither never matches.</summary>
[FramePath]
public readonly record struct Chord
{
    Chord(Key key, ModifierKeys modifiers, GamepadButton? button)
    {
        Key = key;
        Modifiers = modifiers;
        Button = button;
    }

    public static Chord Of(Key key, ModifierKeys modifiers = ModifierKeys.None) =>
        new(key, modifiers, null);

    public static Chord Of(GamepadButton button) => new(Key.None, ModifierKeys.None, button);

    public static implicit operator Chord(Key key) => new(key, ModifierKeys.None, null);

    public Key Key { get; }

    public ModifierKeys Modifiers { get; }

    public GamepadButton? Button { get; }

    internal bool IsEmpty => Key == Key.None && Button is null;
}

/// <summary>A named input a screen runs, bound to a key and a gamepad button in <see cref="UiContext.Actions"/>.
/// The default is no action.</summary>
[FramePath]
public readonly record struct UiAction(string? Name)
{
    public static UiAction Confirm { get; } = new("Confirm");

    public static UiAction Back { get; } = new("Back");

    public static UiAction PreviousTab { get; } = new("PreviousTab");

    public static UiAction NextTab { get; } = new("NextTab");
}

/// <summary>The key and the gamepad button an action runs on.</summary>
[FramePath]
public readonly record struct ActionBinding(Chord Key, GamepadButton? Button);

/// <summary>An action a scope shows, as an action bar lists it.</summary>
[FramePath]
public readonly record struct ActionInfo(
    UiAction Action,
    TextValue Label,
    ActionBinding Binding,
    bool Enabled
);

/// <summary>A context's action bindings. Rebinding takes effect at once, and prompts that read them render again.</summary>
[FramePath]
public sealed class ActionBindings : IObservable
{
    readonly Dictionary<UiAction, ActionBinding> _bindings = [];
    readonly Dispatcher _dispatcher;
    ObserverSet _observers;

    internal ActionBindings(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        Standard();
    }

    ref ObserverSet IObservable.Observers => ref _observers;

    /// <summary>The binding of <paramref name="action"/>; an unbound action reads <c>default</c>, which never
    /// matches.</summary>
    public ActionBinding this[UiAction action]
    {
        [FramePath]
        get => _bindings.TryGetValue(action, out var binding) ? binding : default;
        set
        {
            _dispatcher.VerifyAccess();
            if (action.Name is null)
                throw new ArgumentException("The default action can't be bound.", nameof(action));
            if (_bindings.TryGetValue(action, out var old) && old == value)
                return;
            _bindings[action] = value;
            Changed();
        }
    }

    /// <summary>Drops custom bindings and restores the standard four: Confirm on Enter and South, Back on Escape and
    /// East, PreviousTab on Control+PageUp and LeftShoulder, NextTab on Control+PageDown and RightShoulder.</summary>
    public void Reset()
    {
        _dispatcher.VerifyAccess();
        _bindings.Clear();
        Standard();
        Changed();
    }

    void Standard()
    {
        _bindings[UiAction.Confirm] = new(Key.Enter, GamepadButton.South);
        _bindings[UiAction.Back] = new(Key.Escape, GamepadButton.East);
        _bindings[UiAction.PreviousTab] = new(
            Chord.Of(Key.PageUp, ModifierKeys.Control),
            GamepadButton.LeftShoulder
        );
        _bindings[UiAction.NextTab] = new(
            Chord.Of(Key.PageDown, ModifierKeys.Control),
            GamepadButton.RightShoulder
        );
    }

    void Changed() => _observers.Notify(this, 0);
}

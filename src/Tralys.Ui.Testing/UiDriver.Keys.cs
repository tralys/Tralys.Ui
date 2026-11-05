namespace Tralys.Ui.Testing;

public sealed partial class UiDriver
{
    /// <summary>Sends <paramref name="key"/> down and keeps it held, as one of a held key's auto-repeats when
    /// <paramref name="repeat"/>.</summary>
    /// <returns>Whether the UI took it, so the host would keep it from the scene behind.</returns>
    public bool KeyDown(
        Key key,
        ModifierKeys modifiers = default,
        bool repeat = false,
        bool settle = true
    )
    {
        var taken = View.KeyDown(key, modifiers, repeat);
        if (settle)
            Settle();
        return taken;
    }

    /// <summary>Lets a held <paramref name="key"/> come up.</summary>
    public void KeyUp(Key key, ModifierKeys modifiers = default, bool settle = true)
    {
        View.KeyUp(key, modifiers);
        if (settle)
            Settle();
    }
}

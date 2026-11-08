namespace Tralys.Ui.Components;

public readonly record struct InputGlyphProps
{
    /// <summary>The action whose binding in the context's <see cref="ActionBindings"/> the glyph shows.</summary>
    public UiAction Action { get; init; }

    /// <summary>The key or button the glyph shows while it has no <see cref="Action"/>.</summary>
    public Chord Chord { get; init; }
}

/// <summary>The prompt for an action, or a chord, on the device the player last used in the view: its key as a
/// <see cref="Kbd"/> after a keyboard or a mouse, its gamepad button as a <see cref="GamepadGlyph"/> in the view's
/// style after a gamepad, and nothing after a touch or for a device it has no binding on. It follows the device, the
/// pad's style and any rebinding as they change. Input passes through it.</summary>
public sealed class InputGlyph : Component<InputGlyphProps>
{
    protected override void Render(Ui ui, in InputGlyphProps p)
    {
        var binding =
            p.Action.Name is not null ? ui.Actions[p.Action]
            : p.Chord.Button is { } pressed ? new ActionBinding(default, pressed)
            : new ActionBinding(p.Chord, null);
        switch (ui.InputDevice)
        {
            case InputDevice.KeyboardMouse when binding.Key.Key != Key.None:
                ui.Kbd(chord: binding.Key);
                break;
            case InputDevice.Gamepad when binding.Button is { } button:
                ui.GamepadGlyph(button: button);
                break;
        }
    }
}

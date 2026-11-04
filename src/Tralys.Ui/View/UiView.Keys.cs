using System.Numerics;

namespace Tralys.Ui;

public sealed partial class UiView
{
    const float StickDeadZone = 0.25f;
    static readonly TimeSpan DPadDelay = TimeSpan.FromSeconds(0.4);
    static readonly TimeSpan DPadInterval = TimeSpan.FromSeconds(0.08);

    InputDevice _device;
    GamepadStyle _gamepadStyle;
    GamepadButton? _dpadHeld;
    TimeSpan _dpadDue;
    ValueList<ButtonTarget> _buttonTargets;

    /// <summary>What the player last used: a key down, a mouse or pen moving, pressing or turning the wheel, a touch
    /// landing, a gamepad button, or a stick past its dead zone. A view starts at
    /// <see cref="InputDevice.KeyboardMouse"/>.</summary>
    public InputDevice InputDevice => _device;

    /// <summary>Whose layout the gamepad in hand is labelled in, which the host sets from the pad it reads.</summary>
    public GamepadStyle GamepadStyle
    {
        get => _gamepadStyle;
        set
        {
            VerifyAccess();
            if (_gamepadStyle == value)
                return;
            _gamepadStyle = value;
            InvalidateEnv(_root, EnvReads.Device);
        }
    }

    internal UiView ReadDevice(Builder builder)
    {
        if (builder.CurrentHost is { } host)
            host.EnvReads |= EnvReads.Device;
        return this;
    }

    internal void UseDevice(InputDevice device)
    {
        if (_device == device)
            return;
        _device = device;
        InvalidateEnv(_root, EnvReads.Device);
    }

    internal Node? KeyReceiver() => (_windowInactive ? null : _focused) ?? ContentRoot();

    bool AfterKeyDown(Key k, ModifierKeys m, bool repeat)
    {
        if (k == Key.Escape && DismissTopmostLayer())
            return true;
        var chord = Chord.Of(k, m);
        if (RunShortcuts(chord, repeat))
            return true;
        if (!repeat && RunActions(chord, null))
            return true;
        if ((m & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Gui)) != 0)
            return false;
        if (k == Key.Tab)
            return TabNavigate(backward: (m & ModifierKeys.Shift) != 0);
        return ArrowOf(k) is { } direction && Context.ArrowKeysNavigate && ArrowNavigate(direction);
    }

    static NavDirection? ArrowOf(Key key) =>
        key switch
        {
            Key.Left => NavDirection.Left,
            Key.Up => NavDirection.Up,
            Key.Right => NavDirection.Right,
            Key.Down => NavDirection.Down,
            _ => null,
        };

    static NavDirection? ArrowOf(GamepadButton button) =>
        button switch
        {
            GamepadButton.DPadLeft => NavDirection.Left,
            GamepadButton.DPadUp => NavDirection.Up,
            GamepadButton.DPadRight => NavDirection.Right,
            GamepadButton.DPadDown => NavDirection.Down,
            _ => null,
        };

    static Key KeyOf(GamepadButton button) =>
        button switch
        {
            GamepadButton.South => Key.Enter,
            GamepadButton.East => Key.Escape,
            GamepadButton.DPadUp => Key.Up,
            GamepadButton.DPadDown => Key.Down,
            GamepadButton.DPadLeft => Key.Left,
            GamepadButton.DPadRight => Key.Right,
            _ => Key.None,
        };

    /// <summary>A gamepad button going down or up. It bubbles from the focused node as a gamepad event, then as the
    /// key it stands for (the D-pad as arrows, <see cref="GamepadButton.South"/> as Enter and
    /// <see cref="GamepadButton.East"/> as Escape); then shortcuts and actions see the button itself, East dismisses
    /// or cancels a drag-and-drop, and the D-pad navigates. A held D-pad repeats.</summary>
    /// <returns>Whether the UI took it.</returns>
    public bool Gamepad(GamepadButton b, bool down)
    {
        BeforeInput();
        if (!down)
            return GamepadUp(b);
        UseDevice(InputDevice.Gamepad);
        _keyboardLast = true;
        if (ArrowOf(b) is not null)
        {
            _dpadHeld = b;
            _dpadDue = Context.Now + DPadDelay;
        }
        return GamepadDown(b, repeat: false);
    }

    bool GamepadDown(GamepadButton b, bool repeat)
    {
        var target = KeyReceiver();
        if (target is null)
            return false;
        if (!repeat)
            ButtonWentTo(b, target);
        var pad = new GamepadEvent(b, isDown: true, repeat);
        BubbleGamepad(target, ref pad);
        if (pad.Prevented)
            return true;
        var taken = pad.Stopped;
        if (!pad.Stopped && KeyOf(b) is not Key.None and var key)
        {
            var e = new KeyEvent(key, ModifierKeys.None, repeat) { FromGamepad = true };
            BubbleKey(target, ref e, down: true);
            if (e.Prevented || PressFocused(key, ModifierKeys.None, repeat, pad: true))
                return true;
            taken |= e.Stopped;
        }
        if (RunShortcuts(Chord.Of(b), repeat))
            return true;
        if (!repeat && RunActions(default, b))
            return true;
        if (b == GamepadButton.East)
        {
            if (DismissTopmostLayer())
                return true;
            if (_dragDrop.Active)
            {
                CancelDragDrop();
                return true;
            }
        }
        if (ArrowOf(b) is { } direction && Context.DPadNavigates && ArrowNavigate(direction))
            return true;
        return taken;
    }

    bool GamepadUp(GamepadButton b)
    {
        if (_dpadHeld == b)
            _dpadHeld = null;
        if (TakeButtonTarget(b) is not { IsMounted: true } target)
            return false;
        var pad = new GamepadEvent(b, isDown: false, isRepeat: false);
        BubbleGamepad(target, ref pad);
        if (pad.Stopped || pad.Prevented)
            return true;
        if (KeyOf(b) is Key.None)
            return false;
        var e = new KeyEvent(KeyOf(b), ModifierKeys.None, false);
        BubbleKey(target, ref e, down: false);
        return e.Stopped || e.Prevented;
    }

    /// <summary>A gamepad stick moved to <paramref name="value"/>, -1 to 1 on each axis with Y down; the host sends
    /// every change, the return to centre included. It bubbles from the focused node and never moves focus.</summary>
    /// <returns>Whether the UI took it.</returns>
    public bool Stick(GamepadStick s, Vector2 value)
    {
        BeforeInput();
        if (value.LengthSquared() > StickDeadZone * StickDeadZone)
            UseDevice(InputDevice.Gamepad);
        if (KeyReceiver() is not { } target)
            return false;
        var e = new GamepadEvent(s, value);
        BubbleGamepad(target, ref e);
        return e.Stopped || e.Prevented;
    }

    void BubbleGamepad(Node target, ref GamepadEvent e)
    {
        for (var node = target; node is not null && !e.Stopped; node = node._parent)
        {
            if (node.Interaction is not { } interaction || interaction.Props.OnGamepad.IsEmpty)
                continue;
            try
            {
                interaction.Props.OnGamepad.Invoke(ref e);
            }
            catch (Exception fault) when (Context.OnFault is not null)
            {
                Context.OnFault(fault);
            }
        }
    }

    void RepeatDPad(TimeSpan now)
    {
        if (_dpadHeld is not { } held || now < _dpadDue)
            return;
        _dpadDue += DPadInterval;
        if (_dpadDue <= now)
            _dpadDue = now + DPadInterval;
        DeliverNotices();
        ValidateFocus();
        GamepadDown(held, repeat: true);
    }

    void ButtonWentTo(GamepadButton button, Node target)
    {
        for (var i = 0; i < _buttonTargets.Count; i++)
        {
            if (_buttonTargets[i].Button == button)
            {
                _buttonTargets[i] = new ButtonTarget(button, target);
                return;
            }
        }
        _buttonTargets.Add(new ButtonTarget(button, target));
    }

    Node? TakeButtonTarget(GamepadButton button)
    {
        for (var i = 0; i < _buttonTargets.Count; i++)
        {
            if (_buttonTargets[i].Button != button)
                continue;
            var target = _buttonTargets[i].Target;
            _buttonTargets.RemoveAtSwap(i);
            return target;
        }
        return null;
    }

    readonly record struct ButtonTarget(GamepadButton Button, Node Target);
}

namespace Tralys.Ui;

/// <summary>A gamepad button, named by where it sits rather than by the label printed on it: the face buttons are
/// <see cref="South"/>, <see cref="East"/>, <see cref="West"/> and <see cref="North"/>, as SDL names them, so A on an
/// Xbox pad and Cross on a PlayStation one are both <see cref="South"/>.</summary>
public enum GamepadButton : byte
{
    South,
    East,
    West,
    North,
    Back,
    Guide,
    Start,
    LeftStick,
    RightStick,
    LeftShoulder,
    RightShoulder,
    LeftTrigger,
    RightTrigger,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
}

public enum GamepadStick : byte
{
    Left,
    Right,
}

/// <summary>Whose layout a gamepad's buttons are labelled in, which decides the glyphs its prompts draw.</summary>
public enum GamepadStyle : byte
{
    /// <summary>A, B, X and Y as an Xbox pad prints them, for a pad of no known make.</summary>
    Generic,
    Xbox,

    /// <summary>Cross, Circle, Square and Triangle.</summary>
    PlayStation,

    /// <summary>A on the east face and B on the south, X on the north and Y on the west.</summary>
    Nintendo,
}

/// <summary>What the player last used in a view, which decides the prompts it shows.</summary>
public enum InputDevice : byte
{
    /// <summary>A keyboard, a mouse or a pen.</summary>
    KeyboardMouse,
    Touch,
    Gamepad,
}

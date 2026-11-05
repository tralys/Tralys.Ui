using System.Numerics;

namespace Tralys.Ui.Testing;

/// <summary>A mouse button, finger or pen a <see cref="UiDriver"/> holds down until <see cref="Release"/> or
/// <see cref="LoseRelease"/>. The window losing the focus leaves it held, as the hand that holds it would.</summary>
public sealed class HeldPointer
{
    readonly UiDriver _driver;

    internal HeldPointer(UiDriver driver, Pointer pointer, MouseButton button, Vector2 position)
    {
        _driver = driver;
        Pointer = pointer;
        Button = button;
        Position = position;
    }

    public Pointer Pointer { get; }

    public MouseButton Button { get; }

    /// <summary>Where it is, in layout units.</summary>
    public Vector2 Position { get; private set; }

    public bool IsDown { get; private set; } = true;

    /// <summary>Moves by <paramref name="by"/> in <paramref name="steps"/> equal steps, still holding, with
    /// <paramref name="seconds"/> spread over them: a frame of that share runs before each step.</summary>
    public void MoveBy(Vector2 by, int steps = 1, float seconds = 0f, bool settle = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        VerifyDown();
        var from = Position;
        for (var step = 1; step <= steps; step++)
        {
            if (seconds > 0f)
                _driver.Frame(seconds / steps);
            Position = from + by * (step / (float)steps);
            _driver.MoveHeld(this);
        }
        if (settle)
            _driver.Settle();
    }

    /// <summary>Moves at once to the centre of <paramref name="target"/>'s visible bounds, still holding, whatever
    /// is there.</summary>
    public void MoveTo(Node target, bool settle = true) =>
        MoveBy(_driver.CentreOf(target) - Position, settle: settle);

    /// <summary>Releases the button, or lifts the finger or pen, where it is.</summary>
    public void Release(bool settle = true)
    {
        VerifyDown();
        IsDown = false;
        _driver.ReleaseHeld(this);
        if (settle)
            _driver.Settle();
    }

    /// <summary>Cancels it where it is, as a system that takes the touch or pen away does: no release, no click.</summary>
    public void Cancel(bool settle = true)
    {
        VerifyDown();
        IsDown = false;
        _driver.CancelHeld(this);
        if (settle)
            _driver.Settle();
    }

    /// <summary>Moves where it is with its button up or its contact gone, as a host does that missed the release.</summary>
    public void LoseRelease(bool settle = true)
    {
        VerifyDown();
        IsDown = false;
        _driver.LoseHeld(this);
        if (settle)
            _driver.Settle();
    }

    void VerifyDown()
    {
        if (!IsDown)
            throw new InvalidOperationException($"{Pointer} is no longer held.");
    }
}

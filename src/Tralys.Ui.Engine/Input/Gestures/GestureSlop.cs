namespace Tralys.Ui;

/// <summary>How far, in device-independent units, a pointer may go and still mean what it began.</summary>
public static class GestureSlop
{
    /// <summary>For a tap or a hold by a touch or pen, which a contact wandering further turns into a drag.</summary>
    public const float Tap = 8f;

    /// <summary>Between where the two taps of a double tap land.</summary>
    public const float DoubleTap = 24f;

    /// <summary>For a mouse press, which a move this far turns into a drag.</summary>
    public const float PointerDrag = 4f;
}

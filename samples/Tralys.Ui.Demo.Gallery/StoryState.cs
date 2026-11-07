namespace Tralys.Ui.Gallery;

/// <summary>A look a story's components are held in: as the story leaves them, or forced hovered, pressed, keyboard
/// focused or disabled.</summary>
public enum StoryState
{
    Normal,
    Hover,
    Pressed,
    Focused,
    Disabled,
}

public static class StoryStates
{
    public static readonly StoryState[] All = Enum.GetValues<StoryState>();

    /// <summary>The input states <paramref name="state"/> forces on a component, over its real ones.</summary>
    [FramePath]
    public static InputStates Forced(this StoryState state) =>
        state switch
        {
            StoryState.Hover => InputStates.Hovered,
            StoryState.Pressed => InputStates.Hovered | InputStates.Pressed,
            StoryState.Focused => InputStates.Focused | InputStates.FocusVisible,
            StoryState.Disabled => InputStates.Disabled,
            _ => InputStates.None,
        };
}

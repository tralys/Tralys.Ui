namespace Tralys.Ui.Docking;

/// <summary>A side of a panel another panel docks against, or its centre to join its tabs.</summary>
public enum DockDirection
{
    Left,
    Right,
    Top,
    Bottom,
    Center,
}

/// <summary>Where a dock group or element sits: one of the five regions, floating, or nowhere.</summary>
public enum DockPosition
{
    None,
    Left,
    Right,
    Top,
    Bottom,
    Center,
    Floating,
}

public static class DockPositionExtensions
{
    /// <summary>Whether <paramref name="position"/> is one of the five regions a panel can dock into.</summary>
    public static bool IsRootRegion(this DockPosition position) =>
        position
            is DockPosition.Left
                or DockPosition.Right
                or DockPosition.Top
                or DockPosition.Bottom
                or DockPosition.Center;
}

public enum DockState
{
    Docked,
    Floating,
    Hidden,
}

public enum DockType
{
    Tool,

    Document,
}

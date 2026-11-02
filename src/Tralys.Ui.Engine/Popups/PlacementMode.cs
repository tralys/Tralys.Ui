namespace Tralys.Ui;

/// <summary>Where a <c>Popup</c> opens: a side of its target and where along that side it lines up. A popup
/// with no room on its side flips to the opposite one, and slides along the other axis to stay in the view.</summary>
public enum PlacementMode : byte
{
    /// <summary>Below the target, centred on it.</summary>
    Bottom,

    /// <summary>Below the target, left edges lined up.</summary>
    BottomStart,

    /// <summary>Below the target, right edges lined up.</summary>
    BottomEnd,

    /// <summary>Above the target, centred on it.</summary>
    Top,

    /// <summary>Above the target, left edges lined up.</summary>
    TopStart,

    /// <summary>Above the target, right edges lined up.</summary>
    TopEnd,

    /// <summary>Left of the target, centred on it.</summary>
    Left,

    /// <summary>Left of the target, top edges lined up.</summary>
    LeftStart,

    /// <summary>Left of the target, bottom edges lined up.</summary>
    LeftEnd,

    /// <summary>Right of the target, centred on it.</summary>
    Right,

    /// <summary>Right of the target, top edges lined up.</summary>
    RightStart,

    /// <summary>Right of the target, bottom edges lined up.</summary>
    RightEnd,

    /// <summary>Below and right of where the pointer was when the popup opened, as a context menu opens; it flips
    /// above when there is no room below.</summary>
    Pointer,

    /// <summary>At <c>Popup.HorizontalOffset</c> and <c>Popup.VerticalOffset</c> in the view, slid back
    /// in where it would overhang.</summary>
    Absolute,
}

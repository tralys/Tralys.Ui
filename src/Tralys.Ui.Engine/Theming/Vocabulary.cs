namespace Tralys.Ui.Theming;

/// <summary>The intent a control's colours carry. Each scheme draws from its own family of semantic colours.</summary>
public enum ColorScheme
{
    Primary,
    Secondary,
    Danger,
    Success,
}

/// <summary>How a control sits on its surface: what fills it, what outlines it, and how hovering and pressing change
/// that.</summary>
public enum ComponentVariant
{
    Filled,
    Outlined,
    Ghost,
    GhostOutlined,

    /// <summary>Coloured text with no fill or border until hovered, when the scheme's muted wash shows.</summary>
    Text,

    /// <summary>Coloured text on the scheme's muted wash.</summary>
    Subtle,

    /// <summary>A standalone link: text with no fill or border that lights toward the accent on hover and press, and
    /// greys out while disabled.</summary>
    Link,
}

/// <summary>How a field, an input or a picker, sits in its container. Every field shares one set, so an input and a
/// picker beside it never rest on different surfaces.</summary>
public enum FieldVariant
{
    /// <summary>A sunken well whose border lights on hover and focus.</summary>
    Default,

    /// <summary>No fill and no box, only a static rule beneath: a field set flush into its container.</summary>
    Flush,
}

/// <summary>The interaction state a themed control draws in, resolved from its input state.</summary>
public enum ComponentState
{
    Normal,
    Hover,
    Pressed,
    Focused,
    Disabled,
}

/// <summary>A rung of the control size ramp, smallest first.</summary>
public enum ComponentSize
{
    Xxs,
    Xs,
    Sm,
    Md,
    Lg,
}

/// <summary>A rung of the type ramp, smallest first.</summary>
public enum TextVariant
{
    Badge,
    Caption,
    Body,
    Heading,
    Title,
    Headline,
    Display,
}

/// <summary>How large the controls are drawn, for what drives them.</summary>
public enum Density
{
    Pointer,
    Touch,
}

/// <summary>A font role; each theme names the family that fills it.</summary>
public enum FontToken
{
    Body,
    Code,
}

/// <summary>A weight role; each theme names the weight that fills it.</summary>
public enum WeightToken
{
    Content,
    Interface,
    Link,
}

/// <summary>A corner rounding rung.</summary>
public enum RadiusToken
{
    Sm,
    Md,
    Lg,
}

/// <summary>Where a resizable surface takes a drag: the band along an edge, and the square at a corner.</summary>
public enum GripToken
{
    Band,
    Corner,
}

/// <summary>How long a kind of movement takes.</summary>
public enum DurationToken
{
    Disclosure,
}

/// <summary>How a kind of movement eases.</summary>
public enum EasingToken
{
    /// <summary>Decelerates into place without overshooting, so nothing draws past the slot it lands in.</summary>
    Settle,
}

namespace Tralys.Ui.Theming;

/// <summary>A semantic colour: a role product code names, never a palette rung. Each is the token path its name
/// spells under <c>color.</c>, a trailing <c>default</c> dropped. A component's own fills are
/// <see cref="ComponentColor"/>s.</summary>
public enum ColorToken
{
    Bg,
    BgAccent,
    BgAccentHover,
    BgAccentPressed,
    BgAccentMuted,
    BgBackdrop,
    BgBase,
    BgCanvas,
    BgDanger,
    BgDangerHover,
    BgDangerPressed,
    BgDangerMuted,
    BgDisabled,
    BgInfo,
    BgMuted,
    BgOverlay,
    BgProgress,
    BgRaised,
    BgScrim,
    BgSelected,
    BgShadow,
    BgSubtle,
    BgSuccess,
    BgSuccessHover,
    BgSuccessPressed,
    BgSuccessMuted,
    BgSunken,
    BgTextSelection,
    BgTranslucent,
    BgWarning,

    Fg,
    FgAccent,
    FgAccentHover,
    FgDanger,
    FgDisabled,
    FgFilledAccent,
    FgFilledDanger,
    FgFilledSuccess,
    FgFilledWarning,
    FgHover,
    FgInfo,
    FgMuted,
    FgOnAccent,
    FgOnMedia,
    FgPlaceholder,
    FgStrong,
    FgSuccess,
    FgWarning,

    Border,
    BorderAccent,
    BorderAccentHover,
    BorderDanger,
    BorderDangerHover,
    BorderDisabled,
    BorderFocus,
    BorderHover,
    BorderInfo,
    BorderMuted,
    BorderOnMedia,
    BorderOverlay,
    BorderPressed,
    BorderSelected,
    BorderSuccess,
    BorderSuccessHover,

    Icon,
    IconDisabled,
    IconHover,
    IconOnAccent,
    IconPressed,

    /// <summary>A neutral control's hover, drawn over its own fill.</summary>
    StateHover,

    /// <summary>A neutral control's press, drawn over its own fill.</summary>
    StatePressed,

    /// <summary>Fully transparent: the palette's escape hatch, the same in every theme.</summary>
    Transparent,
}

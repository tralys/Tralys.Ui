using System.Runtime.CompilerServices;

namespace Tralys.Ui;

public readonly ref partial struct Ui
{
    /// <summary>Whether this component was removed and is playing its exit (<see cref="Presence"/>). Tracked.</summary>
    public bool IsExiting => Builder.View.ReadExiting(Builder);

    /// <summary>Whether this component shows: no node here or above, a layer's owner included, is hidden, collapsed or
    /// hidden by a fault. Tracked, so a component stops work no one sees, such as an offscreen render, while an
    /// ancestor hides it.</summary>
    public bool IsShown => Builder.View.ReadShown(Builder);

    /// <summary>A value that moves to <c>target</c> whenever it changes: after the transition's delay, over its
    /// duration through its easing, from the value shown; the default transition takes the theme's. On mount it is
    /// the target at once, and so is every target it takes before the update it mounted in ends.</summary>
    public float Animate(
        float target,
        Transition transition = default,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) => Builder.View.Animate<float, FloatBlend>(Builder, target, transition, key, file, line);

    /// <summary>A colour that moves to <c>target</c> as the <see cref="float"/> overload does, blended in linear light
    /// with straight alpha.</summary>
    public Color Animate(
        Color target,
        Transition transition = default,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) => Builder.View.Animate<Color, ColorBlend>(Builder, target, transition, key, file, line);

    /// <summary>A phase in [0, 1) over <c>period</c> seconds, linear unless <c>easing</c> is given, repeating while the
    /// component is mounted. With <c>alternate</c> it runs there and back, a period each way, which counts as one
    /// cycle. After <c>count</c> cycles (0: forever) it returns 1 and stops; <c>paused</c> holds it where it
    /// stands.</summary>
    public float Loop(
        float period,
        int count = 0,
        bool alternate = false,
        IEasingFunction? easing = null,
        bool paused = false,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) => Builder.View.Loop(Builder, period, count, alternate, easing, paused, key, file, line);

    /// <summary>This component's presence, one per component: 0 on mount rising to 1 with <c>enter</c>. Once its
    /// caller stops rendering it, it stays where it was as exiting (<see cref="IsExiting"/>) while presence falls to 0
    /// with <c>exit</c>, then unmounts; rendered again meanwhile, it turns back. <see cref="Transition.Instant"/> shows
    /// it at once, or unmounts it at once.</summary>
    public float Presence(Transition enter = default, Transition exit = default) =>
        Builder.View.Presence(Builder, enter, exit);
}

namespace Tralys.Ui;

/// <summary>How a value moves to a new target: over <see cref="Duration"/> seconds through <see cref="Easing"/>,
/// after holding for <see cref="Delay"/> seconds.</summary>
/// <param name="Duration">Seconds; null takes the theme's <c>Disclosure</c> duration.</param>
/// <param name="Easing">The curve; null takes the theme's <c>Settle</c> easing.</param>
/// <param name="Delay">Seconds the shown value holds before it moves.</param>
[FramePath]
public readonly record struct Transition(
    float? Duration = null,
    IEasingFunction? Easing = null,
    float Delay = 0
)
{
    /// <summary>No transition: the value jumps.</summary>
    public static Transition Instant => new(0f);
}

/// <summary>Which end of the motion an easing curve shapes.</summary>
public enum EasingMode : byte
{
    /// <summary>The ease-in curve as it is.</summary>
    In,

    /// <summary>The ease-in curve mirrored, so it slows into its end.</summary>
    Out,

    /// <summary>The ease-in curve over the first half, mirrored over the second.</summary>
    InOut,
}

/// <summary>Easing curves. <see cref="EasingMode.In"/>, <see cref="EasingMode.Out"/> (the default) and
/// <see cref="EasingMode.InOut"/> mirror one ease-in curve.</summary>
[FramePath]
public static class Easings
{
    /// <summary>Eases in along t³.</summary>
    public static readonly IEasingFunction CubicIn = new Cubic(EasingMode.In);

    /// <summary>Eases out along t³.</summary>
    public static readonly IEasingFunction CubicOut = new Cubic(EasingMode.Out);

    /// <summary>Eases in and out along t³.</summary>
    public static readonly IEasingFunction CubicInOut = new Cubic(EasingMode.InOut);

    static readonly Dictionary<(float, EasingMode), IEasingFunction> s_backs = [];
    static readonly Lock s_backsLock = new();

    /// <summary>Pulls back before moving (or overshoots after, eased out) by up to <paramref name="amplitude"/>,
    /// along t³ − t·a·sin(πt). One curve is made per amplitude and mode, and reused.</summary>
    [FramePath]
    public static IEasingFunction Back(float amplitude, EasingMode mode = EasingMode.Out)
    {
        lock (s_backsLock)
        {
            if (!s_backs.TryGetValue((amplitude, mode), out var back))
                s_backs[(amplitude, mode)] = back = new BackCurve(amplitude, mode);
            return back;
        }
    }

    abstract class Mirrored(EasingMode mode) : IEasingFunction
    {
        [FramePath]
        public double Ease(double normalizedTime) =>
            mode switch
            {
                EasingMode.In => In(normalizedTime),
                EasingMode.Out => 1.0 - In(1.0 - normalizedTime),
                _ => normalizedTime < 0.5
                    ? In(normalizedTime * 2.0) * 0.5
                    : (1.0 - In((1.0 - normalizedTime) * 2.0)) * 0.5 + 0.5,
            };

        [FramePath]
        protected abstract double In(double t);
    }

    sealed class Cubic(EasingMode mode) : Mirrored(mode)
    {
        [FramePath]
        protected override double In(double t) => t * t * t;
    }

    sealed class BackCurve(float amplitude, EasingMode mode) : Mirrored(mode)
    {
        readonly double _amplitude = Math.Max(0.0, amplitude);

        [FramePath]
        protected override double In(double t) =>
            t * t * t - t * _amplitude * Math.Sin(Math.PI * t);
    }
}

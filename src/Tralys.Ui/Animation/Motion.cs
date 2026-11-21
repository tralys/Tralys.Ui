namespace Tralys.Ui;

internal enum Step : byte
{
    Unchanged,
    Moved,
    Ended,
}

[FramePath]
internal abstract class Motion : Hook
{
    internal ComponentHost? Host;
    internal int Slot = -1;
    internal bool CountsTowardIdle;

    internal bool IsRunning => Slot >= 0;

    internal abstract Step Advance(double time, bool reduced);

    internal virtual void Ended() { }

    public sealed override void Discard()
    {
        if (Host is { } host)
        {
            host.View?.StopMotion(this);
            Detach(host);
        }
        Host = null;
        Release();
    }

    protected virtual void Detach(ComponentHost host) { }

    protected abstract void Release();
}

internal interface IBlend<T>
{
    static abstract T Blend(T from, T to, float progress);

    static abstract bool Same(T a, T b);
}

[FramePath]
internal readonly struct FloatBlend : IBlend<float>
{
    public static float Blend(float from, float to, float progress) =>
        from + (to - from) * progress;

    public static bool Same(float a, float b) => a.Equals(b);
}

[FramePath]
internal readonly struct ColorBlend : IBlend<Color>
{
    public static Color Blend(Color from, Color to, float progress) =>
        Color.Mix(from, to, progress);

    public static bool Same(Color a, Color b) => a == b;
}

[FramePath]
internal struct Tween<T, TBlend>
    where TBlend : struct, IBlend<T>
{
    public T From;
    public T Target;
    double _start;
    float _delay;
    float _duration;
    IEasingFunction? _easing;

    public void Rest(T value)
    {
        From = Target = value;
        _easing = null;
    }

    public void Begin(T from, T target, double time, in MotionCurve curve)
    {
        From = from;
        Target = target;
        _start = time;
        _delay = curve.Delay;
        _duration = curve.Duration;
        _easing = curve.Easing;
    }

    public readonly T Value(double time, bool running)
    {
        if (!running)
            return Target;
        var elapsed = time - _start - _delay;
        if (elapsed <= 0)
            return From;
        if (elapsed >= _duration)
            return Target;
        var linear = elapsed / _duration;
        return TBlend.Blend(From, Target, (float)(_easing?.Ease(linear) ?? linear));
    }

    public readonly Step Advance(double time, bool reduced)
    {
        if (reduced)
            return Step.Ended;
        var elapsed = time - _start - _delay;
        return elapsed >= _duration ? Step.Ended
            : elapsed > 0 ? Step.Moved
            : Step.Unchanged;
    }
}

[FramePath]
internal readonly record struct MotionCurve(float Duration, float Delay, IEasingFunction Easing)
{
    public bool IsInstant => Duration <= 0f && Delay <= 0f;
}

[FramePath]
internal sealed class AnimateHook<T, TBlend> : Motion
    where TBlend : struct, IBlend<T>
{
    internal Tween<T, TBlend> Tween;
    internal int Born;

    internal override Step Advance(double time, bool reduced) => Tween.Advance(time, reduced);

    protected override void Release()
    {
        Tween = default;
        Pool<AnimateHook<T, TBlend>>.Return(this);
    }
}

[FramePath]
internal sealed class LoopHook : Motion
{
    internal double Elapsed;
    internal double Last;
    internal float Period;
    internal int Count;
    internal bool Alternate;
    internal bool Resting;
    internal IEasingFunction? Easing;

    double Length => Count * (double)Period * (Alternate ? 2 : 1);

    internal bool Done => Count > 0 && Elapsed >= Length;

    internal float Phase
    {
        get
        {
            if (Done)
                return 1f;
            var turns = Elapsed / Period;
            var whole = Math.Floor(turns);
            var within = turns - whole;
            var linear = Alternate && ((long)whole & 1) == 1 ? 1.0 - within : within;
            return (float)(Easing?.Ease(linear) ?? linear);
        }
    }

    internal override Step Advance(double time, bool reduced)
    {
        if (reduced)
            return Step.Ended;
        Elapsed += time - Last;
        Last = time;
        if (!Done)
            return Step.Moved;
        Elapsed = Length;
        return Step.Ended;
    }

    protected override void Detach(ComponentHost host)
    {
        if (Resting)
            host.View?.Unrest(this);
    }

    protected override void Release()
    {
        Elapsed = Last = 0;
        Period = 0;
        Count = 0;
        Alternate = false;
        Easing = null;
        Pool<LoopHook>.Return(this);
    }
}

[FramePath]
internal sealed class PresenceHook : Motion
{
    internal const string Site = "<presence>";

    internal Tween<float, FloatBlend> Tween;
    internal Transition Enter;
    internal Transition Exit;

    internal override Step Advance(double time, bool reduced) => Tween.Advance(time, reduced);

    internal override void Ended()
    {
        if (Tween.Target == 0f && Host is { } host && UiView.ExitingWith(host) is { } node)
            host.View?.ExitEnded(node);
    }

    protected override void Detach(ComponentHost host)
    {
        if (host.Presence != this)
            return;
        var node = UiView.ExitingWith(host);
        host.Presence = null;
        if (node is not null)
            host.View?.ExitEnded(node);
    }

    protected override void Release()
    {
        Tween = default;
        Enter = Exit = default;
        Pool<PresenceHook>.Return(this);
    }
}

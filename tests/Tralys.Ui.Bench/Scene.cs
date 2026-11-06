namespace Bench;

/// <summary>One workload: a fresh view to mount into, the frames it runs, and the model it changes.</summary>
abstract class Scene
{
    protected static readonly TimeSpan FrameTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    public abstract string Name { get; }

    /// <summary>Nodes that lay out, mounted.</summary>
    public abstract int Count { get; }

    /// <summary>Makes the empty view the next <see cref="Mount"/> fills.</summary>
    public abstract void Prepare();

    /// <summary>Builds the view's content and runs its first frame.</summary>
    public abstract void Mount();

    public abstract void Unmount();

    /// <summary>Moves the clock a frame on, updates the view and records it.</summary>
    public abstract void Frame();

    public virtual void Rerender() => throw new NotSupportedException($"{Name} has no re-render.");

    public virtual void ScrollTo(float offset) =>
        throw new NotSupportedException($"{Name} doesn't scroll.");

    public virtual float ScrollOffset => 0f;

    public virtual float ScrollMax => 0f;
}

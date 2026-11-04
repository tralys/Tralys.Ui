using System.Collections.Concurrent;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

/// <summary>The thread a <see cref="UiContext"/> runs on, and the work posted to it, run in post order at its next
/// update.</summary>
[FramePath]
public sealed class Dispatcher : IUiDispatcher
{
    readonly int _thread = Environment.CurrentManagedThreadId;
    readonly Dictionary<Type, PostQueue> _queues = [];
    readonly Queue<PostQueue> _order = new();
    readonly ConcurrentQueue<CrossPost> _inbox = new();

    internal Dispatcher() { }

    internal bool HasWork => _order.Count > 0 || !_inbox.IsEmpty;

    /// <summary>Whether the calling thread is the context's.</summary>
    public bool CheckAccess() => Environment.CurrentManagedThreadId == _thread;

    /// <exception cref="InvalidOperationException">The calling thread is not the context's.</exception>
    public void VerifyAccess()
    {
        if (!CheckAccess())
            throw new InvalidOperationException(
                "This UI context belongs to another thread; post to it with Dispatcher.Post."
            );
    }

    /// <summary>Runs <paramref name="action"/> at the context's next update.</summary>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Post(action, static run => run());
    }

    /// <summary>Runs <paramref name="action"/> with <paramref name="state"/> at the context's next update. From the
    /// context's thread it allocates nothing once its state type has been seen; from another, it allocates there.</summary>
    public void Post<TState>(TState state, Action<TState> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!CheckAccess())
        {
            _inbox.Enqueue(new CrossPost<TState>(state, action));
            return;
        }

        if (!_queues.TryGetValue(typeof(TState), out var queue))
            _queues.Add(typeof(TState), queue = new PostQueue<TState>());
        ((PostQueue<TState>)queue).Items.Enqueue((state, action));
        _order.Enqueue(queue);
    }

    [FramePath]
    internal void Drain(Action<Exception>? onFault)
    {
        var count = _order.Count;
        for (var i = 0; i < count; i++)
        {
            var queue = _order.Dequeue();
            try
            {
                queue.RunNext();
            }
            catch (Exception e) when (onFault is not null)
            {
                onFault(e);
            }
        }

        var cross = _inbox.Count;
        for (var i = 0; i < cross && _inbox.TryDequeue(out var post); i++)
        {
            try
            {
                post.Run();
            }
            catch (Exception e) when (onFault is not null)
            {
                onFault(e);
            }
        }
    }

    abstract class PostQueue
    {
        public abstract void RunNext();
    }

    sealed class PostQueue<TState> : PostQueue
    {
        public readonly Queue<(TState State, Action<TState> Action)> Items = new();

        public override void RunNext()
        {
            var (state, action) = Items.Dequeue();
            action(state);
        }
    }

    abstract class CrossPost
    {
        public abstract void Run();
    }

    sealed class CrossPost<TState>(TState state, Action<TState> action) : CrossPost
    {
        public override void Run() => action(state);
    }
}

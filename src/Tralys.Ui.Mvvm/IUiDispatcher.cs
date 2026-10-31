namespace Tralys.Ui.Mvvm;

/// <summary>The thread a UI context runs on, as observation sees it: a change made elsewhere is posted here.</summary>
public interface IUiDispatcher
{
    /// <summary>Whether the calling thread is the dispatcher's.</summary>
    bool CheckAccess();

    /// <summary>Runs <paramref name="action"/> with <paramref name="state"/> on the dispatcher's thread, at its next
    /// update, after what was posted before it.</summary>
    void Post<TState>(TState state, Action<TState> action);
}

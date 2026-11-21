using System.Diagnostics.CodeAnalysis;

namespace Tralys.Ui.Mvvm;

sealed class Watcher<T> : IChangeObserver, IDisposable
{
    static readonly Action<(Watcher<T> Watcher, T Value)> React = static reacting =>
        reacting.Watcher._react(reacting.Value);

    readonly Func<T> _select;
    readonly Action<T> _react;
    readonly Action<Exception>? _onFault;
    Dependencies _dependencies;
    T? _value;
    bool _disposed;

    public Watcher(
        Func<T> select,
        Action<T> react,
        IUiDispatcher? dispatcher,
        bool now,
        Action<Exception>? onFault
    )
    {
        _select = select;
        _react = react;
        _onFault = onFault;
        _dependencies = new Dependencies(dispatcher);
        if (!TrySelect(out var first))
            return;
        _value = first;
        if (now)
            RunReact(first);
    }

    ref Dependencies IChangeObserver.Dependencies => ref _dependencies;

    void IChangeObserver.OnChanged(object source, int property)
    {
        if (_disposed)
            return;

        if (!TrySelect(out var next) || EqualityComparer<T>.Default.Equals(_value, next))
            return;

        _value = next;
        RunReact(next);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _dependencies.Clear();
    }

    bool TrySelect([MaybeNullWhen(false)] out T value)
    {
        var scope = Observation.Track(this);
        try
        {
            value = _select();
        }
        catch (Exception e)
        {
            scope.Complete(keepDropped: true);
            if (_onFault is null)
                throw;
            _onFault(e);
            value = default;
            return false;
        }
        scope.Dispose();
        return true;
    }

    void RunReact(T value)
    {
        try
        {
            Observation.Untracked((this, value), React);
        }
        catch (Exception e) when (_onFault is not null)
        {
            _onFault(e);
        }
    }
}

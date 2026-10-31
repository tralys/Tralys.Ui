namespace Tralys.Ui.Mvvm;

// An async method would box its state machine on every run in a Debug build, and on every suspension in any.
sealed class InFlight : IObservable
{
    readonly Action _settle;
    ObserverSet _observers;
    ValueTask _pending;
    volatile bool _running;

    public InFlight() => _settle = Settle;

    public ref ObserverSet Observers => ref _observers;

    public bool IsRunning
    {
        get
        {
            Observation.Read(this, 0);
            return _running;
        }
    }

    public void Start(Func<ValueTask> run)
    {
        Set(true);
        ValueTask running;
        try
        {
            running = run();
        }
        catch (Exception e)
        {
            running = ValueTask.FromException(e);
        }
        Await(running);
    }

    public void Start<T>(Func<T, ValueTask> run, T parameter)
    {
        Set(true);
        ValueTask running;
        try
        {
            running = run(parameter);
        }
        catch (Exception e)
        {
            running = ValueTask.FromException(e);
        }
        Await(running);
    }

    void Await(ValueTask running)
    {
        if (running.IsCompleted)
        {
            Settle(running);
            return;
        }

        _pending = running;
        running.GetAwaiter().UnsafeOnCompleted(_settle);
    }

    void Settle()
    {
        var running = _pending;
        _pending = default;
        Settle(running);
    }

    void Settle(ValueTask running)
    {
        try
        {
            if (running.IsCompletedSuccessfully)
                running.GetAwaiter().GetResult();
            else
                Rethrow(running);
        }
        finally
        {
            Set(false);
        }
    }

    void Set(bool running)
    {
        _running = running;
        _observers.Notify(this, 0);
    }

    // Nothing awaits the handler, so its fault goes where an async void's would: the context, or the thread pool.
    static async void Rethrow(ValueTask faulted) => await faulted;
}

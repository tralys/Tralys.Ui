namespace Tralys.Ui.Mvvm;

/// <summary>A verb a view model offers: what it runs, and whether it can run now. A control handed one greys while it
/// can't, and a run it refuses does nothing. An asynchronous one refuses until its handler completes.</summary>
public sealed class Command
{
    readonly Action? _run;
    readonly Func<ValueTask>? _runAsync;
    readonly Func<bool>? _canRun;
    readonly InFlight? _inFlight;

    /// <param name="run">What it runs.</param>
    /// <param name="canRun">Whether it can run now; always, when null. A render asking follows what it reads.</param>
    public Command(Action run, Func<bool>? canRun = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        _run = run;
        _canRun = canRun;
    }

    /// <param name="run">What it runs; it can't run again until the task completes.</param>
    /// <param name="canRun">Whether it can run now; always, when null. A render asking follows what it reads.</param>
    public Command(Func<ValueTask> run, Func<bool>? canRun = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        _runAsync = run;
        _canRun = canRun;
        _inFlight = new InFlight();
    }

    /// <summary>Whether <see cref="Run"/> runs now: no earlier run is in flight, and its check says yes.
    /// Tracked.</summary>
    public bool CanRun => !IsRunning && (_canRun?.Invoke() ?? true);

    /// <summary>Whether an asynchronous run is still in flight. Tracked.</summary>
    public bool IsRunning => _inFlight?.IsRunning ?? false;

    /// <summary>Runs it, unless it can't run now.</summary>
    public void Run()
    {
        if (!CanRun)
            return;
        if (_run is { } run)
            run();
        else if (_runAsync is { } runAsync && _inFlight is { } inFlight)
            inFlight.Start(runAsync);
    }
}

/// <summary>A <see cref="Command"/> whose run takes a <typeparamref name="T"/>, such as the row it acts on.</summary>
public sealed class Command<T>
{
    readonly Action<T>? _run;
    readonly Func<T, ValueTask>? _runAsync;
    readonly Func<T, bool>? _canRun;
    readonly InFlight? _inFlight;

    /// <param name="run">What it runs.</param>
    /// <param name="canRun">Whether it can run now with a parameter; always, when null. A render asking follows what
    /// it reads.</param>
    public Command(Action<T> run, Func<T, bool>? canRun = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        _run = run;
        _canRun = canRun;
    }

    /// <param name="run">What it runs; it can't run again, with any parameter, until the task completes.</param>
    /// <param name="canRun">Whether it can run now with a parameter; always, when null. A render asking follows what
    /// it reads.</param>
    public Command(Func<T, ValueTask> run, Func<T, bool>? canRun = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        _runAsync = run;
        _canRun = canRun;
        _inFlight = new InFlight();
    }

    /// <summary>Whether <see cref="Run"/> runs now with <paramref name="parameter"/>: no earlier run is in flight,
    /// and its check says yes. Tracked.</summary>
    public bool CanRun(T parameter) => !IsRunning && (_canRun?.Invoke(parameter) ?? true);

    /// <summary>Whether an asynchronous run is still in flight. Tracked.</summary>
    public bool IsRunning => _inFlight?.IsRunning ?? false;

    /// <summary>Runs it with <paramref name="parameter"/>, unless it can't run now.</summary>
    public void Run(T parameter)
    {
        if (!CanRun(parameter))
            return;
        if (_run is { } run)
            run(parameter);
        else if (_runAsync is { } runAsync && _inFlight is { } inFlight)
            inFlight.Start(runAsync, parameter);
    }
}

using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

/// <summary>A node payload that answers the edit commands, as every editable field does.</summary>
internal interface IEditCommandTarget
{
    [FramePath]
    bool CanRun(EditCommand command);

    [FramePath]
    void Run(UiView view, EditCommand command);
}

public sealed partial class UiView
{
    readonly EditCommandSource?[] _editCommands = new EditCommandSource?[7];

    internal Command EditCommandFor(EditCommand command) =>
        (_editCommands[(int)command] ??= new EditCommandSource(this, command)).Command;

    // Whether an edit command can run follows focus and the target's selection, text and history: call on each change.
    internal void EditStateChanged()
    {
        for (var i = 0; i < _editCommands.Length; i++)
            _editCommands[i]?.Changed();
    }

    internal IEditCommandTarget? EditTarget() =>
        CommandTarget() is { IsMounted: true, Payload: IEditCommandTarget target } node
        && (node._states & InputStates.Disabled) == 0
            ? target
            : null;
}

internal sealed class EditCommandSource : IObservable
{
    readonly UiView _view;
    readonly EditCommand _command;
    ObserverSet _observers;

    [ColdPath]
    public EditCommandSource(UiView view, EditCommand command)
    {
        _view = view;
        _command = command;
        Command = new Command(Run, CanRun);
    }

    public Command Command { get; }

    public ref ObserverSet Observers => ref _observers;

    public void Changed() => _observers.Notify(this, 0);

    bool CanRun()
    {
        Observation.Read(this, 0);
        return _view.EditTarget() is { } target && target.CanRun(_command);
    }

    void Run()
    {
        _view.VerifyAccess();
        if (_view.EditTarget() is { } target)
            target.Run(_view, _command);
    }
}

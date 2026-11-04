using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

public sealed partial class UiView
{
    readonly ScopeActionsSource _scopeActions = new();
    ValueList<ShortcutHook> _shortcuts;
    ValueList<ActionScopeState> _scopes;
    ActionScopeState? _rootScope;
    ValueList<NodeEntry> _actionBoxes;
    ValueList<ActionInfo> _actionInfos;

    internal void AddShortcut(ShortcutHook hook) => _shortcuts.Add(hook);

    internal void RemoveShortcut(ShortcutHook hook)
    {
        var index = _shortcuts.IndexOf(hook);
        if (index >= 0)
            _shortcuts.RemoveAt(index);
    }

    bool RunShortcuts(Chord chord, bool repeat)
    {
        var target = KeyReceiver();
        ShortcutHook? best = null;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < _shortcuts.Count; i++)
        {
            var shortcut = _shortcuts[i];
            if (
                shortcut.Disabled
                || (repeat && !shortcut.Repeat)
                || shortcut.Chord.IsEmpty
                || shortcut.Chord != chord
                || shortcut.Host is not { IsMounted: true } host
            )
                continue;
            var distance = Distance(target, host.Node);
            if (shortcut.Scope == ShortcutScope.View)
            {
                if (NewestModal() is { } modal && !modal.IsAncestorOrSelfOf(host.Node))
                    continue;
                if (distance < 0)
                    distance = int.MaxValue - 1;
            }
            else if (distance < 0)
                continue;
            if (distance >= bestDistance || !CanRun(shortcut.OnPress))
                continue;
            best = shortcut;
            bestDistance = distance;
        }
        if (best is null)
            return false;
        Invoke(best.OnPress);
        return true;
    }

    static int Distance(Node? from, Node ancestor)
    {
        var steps = 0;
        for (var node = from; node is not null; node = node._parent, steps++)
        {
            if (node == ancestor)
                return steps;
        }
        return -1;
    }

    bool CanRun(Callback callback)
    {
        try
        {
            return callback.CanRun;
        }
        catch (Exception fault) when (Context.OnFault is not null)
        {
            Context.OnFault(fault);
            return false;
        }
    }

    ActionScopeState RootScope =>
        _rootScope ??= new ActionScopeState
        {
            View = this,
            Node = _root,
            Active = true,
        };

    ActionScopeState NewestScope()
    {
        for (var i = _scopes.Count - 1; i >= 0; i--)
        {
            var scope = _scopes[i];
            if (scope.Active && scope.Node is { IsMounted: true })
                return scope;
        }
        return RootScope;
    }

    internal ActionScopeState ScopeOf(Node? node)
    {
        for (var n = node; n is not null; n = n._parent)
        {
            if (n.Kind == NodeKind.ActionScope && n.Payload is ActionScopeState scope)
                return scope;
        }
        return RootScope;
    }

    internal void SetScopeActive(ActionScopeState scope, bool active)
    {
        if (scope.Active == active)
            return;
        scope.Active = active;
        if (active)
            _scopes.Add(scope);
        else if (_scopes.IndexOf(scope) is >= 0 and var index)
            _scopes.RemoveAt(index);
        _scopeActions.Changed();
    }

    internal void ScopeActionsChanged(ActionScopeState scope) => _scopeActions.Changed();

    /// <summary>Runs <paramref name="action"/> as its key or button would, as an action prompt does when clicked: the
    /// newest active scope's first registration of it whose command can run, else the enabled, shown box there that
    /// it presses.</summary>
    /// <returns>Whether anything ran.</returns>
    public bool RunAction(UiAction action)
    {
        VerifyAccess();
        return action.Name is not null && RunActions(default, null, action);
    }

    bool RunActions(Chord key, GamepadButton? button, UiAction only = default)
    {
        var scope = NewestScope();
        var modal = NewestModal();
        var bindings = Context.Actions;
        for (var i = 0; i < scope.Actions.Count && Reached(scope.Node); i++)
        {
            var action = scope.Actions[i];
            if (!Matches(action.Action) || !CanRun(action.OnRun))
                continue;
            Invoke(action.OnRun);
            return true;
        }
        for (var i = 0; i < _actionBoxes.Count; )
        {
            var entry = _actionBoxes[i];
            var node = entry.Node;
            if (
                !node.IsMounted
                || node.Generation != entry.Generation
                || node.Interaction?.Props.Action is not { Name: not null } action
            )
            {
                _actionBoxes.RemoveAt(i);
                continue;
            }
            i++;
            if (
                (node._states & InputStates.Disabled) != 0
                || ScopeOf(node) != scope
                || !Matches(action)
                || !Reached(node)
                || !Drawn(node)
            )
                continue;
            Click(node);
            return true;
        }
        return false;

        bool Matches(UiAction action) =>
            only.Name is not null ? action == only : Bound(bindings[action], key, button);

        bool Reached(Node? node) => modal is null || modal.IsAncestorOrSelfOf(node);
    }

    static bool Bound(ActionBinding binding, Chord key, GamepadButton? button) =>
        button is { } pressed
            ? binding.Button == pressed
            : binding.Key.Key != Key.None
                && binding.Key.Button is null
                && binding.Key.Key == key.Key
                && binding.Key.Modifiers == key.Modifiers;

    internal ReadOnlySpan<ActionInfo> ReadScopeActions()
    {
        Observation.Read(_scopeActions, 0);
        var bindings = Context.Actions;
        Observation.Read(bindings, 0);
        _actionInfos.Clear();
        var scope = NewestScope();
        if (NewestModal() is { } modal && !modal.IsAncestorOrSelfOf(scope.Node))
            return default;
        for (var i = 0; i < scope.Actions.Count; i++)
        {
            var action = scope.Actions[i];
            if (action.Hidden)
                continue;
            _actionInfos.Add(
                new ActionInfo(
                    action.Action,
                    action.Label,
                    bindings[action.Action],
                    action.OnRun.CanRun
                )
            );
        }
        return _actionInfos.AsSpan();
    }

    internal void SetCommandRefused(Node node, bool refused)
    {
        node.CommandRefusals = (byte)Math.Max(0, node.CommandRefusals + (refused ? 1 : -1));
        UpdateDisabled(node);
        InvalidateEnv(node, EnvReads.Disabled);
    }

    sealed class ScopeActionsSource : IObservable
    {
        ObserverSet _observers;

        public ref ObserverSet Observers => ref _observers;

        public void Changed() => _observers.Notify(this, 0);
    }
}

using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

public readonly ref partial struct Ui
{
    /// <summary>What the player last used in this view. Tracked.</summary>
    public InputDevice InputDevice => Builder.View.ReadDevice(Builder).InputDevice;

    /// <summary>Whose layout the gamepad in hand is labelled in. Tracked.</summary>
    public GamepadStyle GamepadStyle => Builder.View.ReadDevice(Builder).GamepadStyle;

    /// <summary>Whether a drag-and-drop is over this component's root and it accepts. Tracked.</summary>
    public bool IsDropTarget => Builder.View.ReadStates(Builder, InputStates.DropTarget);

    /// <summary>Runs <paramref name="onPress"/> on <paramref name="chord"/>'s exact key and modifiers, or its button,
    /// once the focused node and its ancestors have passed the input on: with focus on or inside this component, or
    /// anywhere not under a modal for <see cref="ShortcutScope.View"/>. One made from a command that can't run passes
    /// the key on; <paramref name="repeat"/> runs it on a held key's repeats too. While called.</summary>
    public void Shortcut(
        Chord chord,
        Callback onPress,
        ShortcutScope scope = default,
        bool repeat = false,
        bool disabled = false,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var b = Builder;
        var hook = b.UseHook<ShortcutHook>(key, file, line, out var created);
        if (created)
        {
            hook.View = b.View;
            b.View.AddShortcut(hook);
        }
        hook.Host = b.CurrentHost;
        hook.Chord = chord;
        hook.OnPress = onPress;
        hook.Scope = scope;
        hook.Repeat = repeat;
        hook.Disabled = disabled;
    }

    /// <summary>Registers <paramref name="action"/> on the nearest action scope above, else the view's own, which is
    /// always active, to run <paramref name="onRun"/> on its binding. One made from a command that can't run passes
    /// the input on. Its prompt says <paramref name="label"/>, else the action's name; a <paramref name="hidden"/> one
    /// runs without showing in <see cref="ScopeActions"/>. While called.</summary>
    public void Action(
        UiAction action,
        Callback onRun,
        TextValue label = default,
        bool hidden = false,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var b = Builder;
        var view = b.View;
        var hook = b.UseHook<ActionHook>(key, file, line, out _);
        var scope = view.ScopeOf(b.OpenNode);
        if (hook.Scope != scope)
        {
            hook.Scope?.Remove(hook);
            hook.Scope = scope;
            scope.Add(hook);
        }
        var changed = hook.Action != action;
        hook.Action = action;
        changed |= hook.OnRun != onRun;
        hook.OnRun = onRun;
        changed |= hook.SetLabel(label);
        changed |= hook.Hidden != hidden;
        hook.Hidden = hidden;
        if (changed)
            view.ScopeActionsChanged(scope);
    }

    /// <summary>A transparent node that actions inside register on. Only the newest active scope in the view runs
    /// actions; older ones take over again as it deactivates or unmounts.</summary>
    public Scope ActionScope(
        bool active = true,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var b = Builder;
        if (b.Discarding)
            return default;
        var depth = b.FrameDepth;
        var hooks = b.HookDepth;
        var node = b.Claim(NodeKind.ActionScope, null, key, file, line, out _);
        if (node.Payload is not ActionScopeState scope)
        {
            scope = Pool<ActionScopeState>.TryRent() ?? new ActionScopeState();
            scope.View = b.View;
            scope.Node = node;
            scope.Active = false;
            node.Payload = scope;
        }
        b.View.SetScopeActive(scope, active);
        b.Push(node);
        return new Scope(b, depth, hooks);
    }

    /// <summary>The newest active scope's shown actions, as an action bar lists them. Tracked.</summary>
    public ReadOnlySpan<ActionInfo> ScopeActions => Builder.View.ReadScopeActions();

    /// <summary>The context's action bindings, which prompts resolve their glyphs through. Tracked.</summary>
    public ActionBindings Actions
    {
        get
        {
            var actions = Builder.View.Context.Actions;
            Observation.Read(actions, 0);
            return actions;
        }
    }

    /// <summary>A command the view owns that runs <paramref name="command"/> on the command target, the focused field,
    /// only while it can perform it now. Its <see cref="Mvvm.Command.CanRun"/> follows the target's focus, selection,
    /// text and history.</summary>
    public Command Command(EditCommand command) => Builder.View.EditCommandFor(command);

    /// <summary>Whether <paramref name="action"/> can run now, asked inside this render's tracking: false only while
    /// the command it was made from can't. While it says no, this component is disabled with its content.
    /// Tracked.</summary>
    public bool CanRun(Callback action)
    {
        if (!action.IsGated)
            return true;
        var b = Builder;
        var hook = b.UseHook<CanRunHook>(default, CanRunHook.Site, 0, out _);
        var can = action.CanRun;
        hook.Refuse(b.View, can ? null : b.CurrentHost?.Node);
        return can;
    }
}

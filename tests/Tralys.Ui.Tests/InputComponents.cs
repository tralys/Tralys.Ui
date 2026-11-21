using System.Numerics;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Tests;

public sealed class Log
{
    public readonly List<string> Items = [];

    public void Add(string item) => Items.Add(item);

    public override string ToString() => string.Join(" ", Items);
}

public sealed class ValueLog<T>(Func<T, string> spell)
{
    public readonly List<T> Items = [];

    public void Add(T item) => Items.Add(item);

    public List<string> Lines => [.. Items.Select(spell)];

    public override string ToString() => string.Join(" ", Lines);
}

public sealed class DragLog
{
    public readonly List<(DragPhase Phase, Vector2 Offset)> Items = [];

    public override string ToString() =>
        string.Join(" ", Items.Select(static i => $"{i.Phase}:{i.Offset.X}"));
}

public sealed class Seen<T>
{
    public T? Value;
}

public readonly record struct KeyedProps
{
    public required string Name { get; init; }

    public required Log Log { get; init; }

    public Chord Chord { get; init; }

    public ShortcutScope Scope { get; init; }

    /// <summary>Run in place of logging the name.</summary>
    public Command? Command { get; init; }
}

/// <summary>A focusable box holding its caller's children, with a shortcut that logs its name.</summary>
[Children]
public sealed class Keyed : Component<KeyedProps>
{
    protected override void Render(Ui ui, in KeyedProps p)
    {
        ui.Shortcut(
            p.Chord,
            p.Command is { } command
                ? command
                : Callback.Of(ui.Self(p), static h => h.Props.Log.Add(h.Props.Name)),
            p.Scope
        );
        using (
            ui.Box(
                direction: Direction.Column,
                align: Align.Start,
                padding: 10,
                focusable: true,
                background: Color.FromRgb(9, 9, 9)
            )
        )
            ui.Children();
    }
}

public readonly record struct ScreenProps
{
    public required string Name { get; init; }

    public required Log Log { get; init; }

    public bool Active { get; init; }

    /// <summary>Confirm's, in place of logging the name.</summary>
    public Command? Command { get; init; }
}

/// <summary>An action scope whose Confirm logs its name, labelled with it, and whose hidden Back logs "back".</summary>
public sealed class Screen : Component<ScreenProps>
{
    protected override void Render(Ui ui, in ScreenProps p)
    {
        using (ui.ActionScope(active: p.Active))
        {
            ui.Action(
                UiAction.Confirm,
                p.Command is { } command
                    ? command
                    : Callback.Of(ui.Self(p), static h => h.Props.Log.Add(h.Props.Name)),
                label: p.Name
            );
            ui.Action(
                UiAction.Back,
                Callback.Of(ui.Self(p), static h => h.Props.Log.Add("back")),
                hidden: true
            );
            using (ui.Box(width: 10, height: 10)) { }
        }
    }
}

public readonly record struct ActionBarProbeProps
{
    public int Tag { get; init; }
}

/// <summary>Shows the newest scope's actions as their labels, keys and whether they can run.</summary>
public sealed class ActionBarProbe : Component<ActionBarProbeProps>
{
    protected override void Render(Ui ui, in ActionBarProbeProps p)
    {
        var actions = ui.ScopeActions;
        using (ui.Box(direction: Direction.Row, gap: 4))
        {
            for (var i = 0; i < actions.Length; i++)
            {
                ui.Text(actions[i].Label, key: i);
                ui.Text(actions[i].Binding.Key.Key == Key.Enter ? "Enter" : "other", key: -1 - i);
                ui.Text(actions[i].Enabled ? "on" : "off", key: 1000 + i);
            }
        }
    }
}

public readonly record struct DeviceProbeProps
{
    public required Seen<InputDevice> Seen { get; init; }
}

/// <summary>Records the input device each render sees.</summary>
public sealed class DeviceProbe : Component<DeviceProbeProps>
{
    protected override void Render(Ui ui, in DeviceProbeProps p)
    {
        p.Seen.Value = ui.InputDevice;
        using (ui.Box(width: 20, height: 20, background: Color.FromRgb(5, 5, 5))) { }
    }
}

public readonly record struct CommandButtonProps
{
    public Callback OnClick { get; init; }

    public required Seen<bool> Disabled { get; init; }
}

/// <summary>A box that runs its click, disabled while the command it was made from can't run.</summary>
public sealed class CommandButton : Component<CommandButtonProps>
{
    protected override void Render(Ui ui, in CommandButtonProps p)
    {
        ui.CanRun(p.OnClick);
        p.Disabled.Value = ui.IsDisabled;
        using (ui.Box(width: 40, height: 40, onClick: p.OnClick)) { }
    }
}

public readonly record struct GestureStripProps
{
    public required Model Model { get; init; }
}

/// <summary>A strip that keeps the system's edge gestures off it, placed by its model's count.</summary>
public sealed class GestureStrip : Component<GestureStripProps>
{
    protected override void Render(Ui ui, in GestureStripProps p)
    {
        using (ui.Box())
        {
            using (
                ui.Box(
                    position: Position.Absolute,
                    left: p.Model.Count,
                    width: 20,
                    height: 20,
                    excludeSystemGestures: true
                )
            ) { }
        }
    }
}

public readonly record struct AutoFocusProbeProps
{
    public required Model Gate { get; init; }
}

/// <summary>A button beside an autofocusing box that its gate hides.</summary>
public sealed class AutoFocusProbe : Component<AutoFocusProbeProps>
{
    protected override void Render(Ui ui, in AutoFocusProbeProps p)
    {
        using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 4))
        {
            using (
                ui.Box(width: 20, height: 20, onClick: Callback.Of(static () => { }), id: "button")
            ) { }
            using (
                ui.Box(
                    width: 20,
                    height: 20,
                    focusable: true,
                    autoFocus: true,
                    hidden: p.Gate.Remember
                )
            ) { }
        }
    }
}

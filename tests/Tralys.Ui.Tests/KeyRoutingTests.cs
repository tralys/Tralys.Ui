using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class KeyRoutingTests
{
    static readonly Chord Save = Chord.Of(Key.S, ModifierKeys.Control);

    [Test]
    public async Task A_shortcut_runs_only_with_focus_inside_its_host_and_the_innermost_wins()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Keyed(name: "outer", log: log, chord: Save, id: "outer"))
            using (ui.Keyed(name: "inner", log: log, chord: Save, id: "inner")) { }
        });

        driver.Click(driver.Get<Keyed>("inner"));
        driver.Press(Key.S, ModifierKeys.Control);
        driver.Click(driver.Get<Keyed>("outer"));
        driver.Press(Key.S, ModifierKeys.Control);

        await Assert.That(log.ToString()).IsEqualTo("inner outer");
    }

    [Test]
    public async Task A_shortcut_runs_only_on_its_exact_chord_and_the_key_is_taken_only_when_it_runs()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            ui.Shortcut(Save, Callback.Of(log, static l => l.Add("save")));
            using (ui.Box(width: 10, height: 10)) { }
        });

        var taken = (
            Taken(driver, Key.S, ModifierKeys.None),
            Taken(driver, Key.S, ModifierKeys.Control | ModifierKeys.Shift),
            Taken(driver, Key.S, ModifierKeys.Control)
        );

        await Assert.That(taken).IsEqualTo((false, false, true));
        await Assert.That(log.ToString()).IsEqualTo("save");
    }

    static bool Taken(UiDriver driver, Key key, ModifierKeys modifiers)
    {
        var taken = driver.View.KeyDown(key, modifiers, false);
        driver.View.KeyUp(key, modifiers);
        driver.Settle();
        return taken;
    }

    [Test]
    public async Task A_shortcut_whose_command_cant_run_passes_the_key_to_a_view_shortcut()
    {
        var log = new Log();
        var never = new Command(static () => { }, static () => false);
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start))
            {
                using (
                    ui.Keyed(name: "focused", log: log, chord: Save, command: never, id: "focused")
                ) { }
                using (ui.Keyed(name: "view", log: log, chord: Save, scope: ShortcutScope.View)) { }
            }
        });

        driver.Click(driver.Get<Keyed>("focused"));
        driver.Press(Key.S, ModifierKeys.Control);

        await Assert.That(log.ToString()).IsEqualTo("view");
    }

    [Test]
    public async Task A_shortcut_and_an_action_ask_and_run_their_commands_with_their_parameters()
    {
        var ran = new List<string>();
        var command = new Command<string>(ran.Add, static p => p is not null);
        var enabled = new Seen<bool>();
        using var driver = UiDriver.Render(ui =>
        {
            ui.Shortcut(Save, command.With("shortcut"));
            ui.Action(UiAction.Confirm, command.With("action"));
            enabled.Value = ui.ScopeActions[0].Enabled;
            using (ui.Box(width: 10, height: 10)) { }
        });

        driver.Press(Key.S, ModifierKeys.Control);
        driver.Press(Key.Enter);

        await Assert.That(ran).IsEquivalentTo(["shortcut", "action"]);
        await Assert.That(enabled.Value).IsTrue();
    }

    [Test]
    public async Task An_actions_new_parameter_asks_its_command_again_so_its_prompt_and_its_key_follow_it()
    {
        var ran = new List<string?>();
        var command = new Command<string?>(ran.Add, static p => p is not null);
        var model = new Model { Name = "first" };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Action(UiAction.Confirm, command.With(model.Remember ? null : model.Name));
                ui.ActionBarProbe();
            }
        });
        var shown = driver.GetText("on");

        model.Remember = true;
        driver.Settle();
        var refused = driver.GetText("off");
        driver.Press(Key.Enter);

        await Assert.That(shown).IsNotNull();
        await Assert.That(refused).IsNotNull();
        await Assert.That(ran).IsEmpty();
    }

    [Test]
    public async Task A_held_key_comes_up_as_the_window_loses_the_focus_without_a_click_and_its_late_release_goes_nowhere()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    width: 20,
                    height: 20,
                    onClick: Callback.Of(log, static l => l.Add("click")),
                    onKeyUp: Handler<KeyEvent>.Of(
                        log,
                        static (Log l, ref KeyEvent e) => l.Add("up")
                    ),
                    id: "button"
                )
            ) { }
        });
        driver.Focus(driver.Get("button"));

        driver.KeyDown(Key.Space);
        driver.WindowFocus(false);
        var away = log.ToString();
        driver.WindowFocus(true);
        driver.KeyUp(Key.Space);

        await Assert.That(away).IsEqualTo("up");
        await Assert.That(log.ToString()).IsEqualTo("up");
    }

    [Test]
    public async Task Only_the_newest_active_scope_runs_actions_and_only_for_keys_passed_on()
    {
        var log = new Log();
        var over = new Model { Remember = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Screen(name: "base", log: log, active: true);
                ui.Screen(name: "over", log: log, active: over.Remember);
                using (
                    ui.Box(
                        width: 20,
                        height: 20,
                        focusable: true,
                        onKeyDown: Handler<KeyEvent>.Of(
                            static (ref KeyEvent e) =>
                            {
                                if (e.Key == Key.Enter)
                                    e.PreventDefault();
                            }
                        ),
                        background: Color.FromRgb(5, 5, 5),
                        id: "field"
                    )
                ) { }
            }
        });

        driver.Press(Key.Enter);
        over.Remember = false;
        driver.Rerender();
        driver.Press(Key.Enter);
        driver.Click(driver.Get("field"));
        driver.Press(Key.Enter);

        await Assert.That(log.ToString()).IsEqualTo("over base");
    }

    [Test]
    public async Task A_modal_box_without_a_scope_of_its_own_keeps_the_actions_behind_it_from_running()
    {
        var log = new Log();
        using (
            var driver = UiDriver.Render(ui =>
            {
                ui.Screen(name: "scope", log: log, active: true);
                ModalBox(ui);
            })
        )
            driver.Press(Key.Enter);
        using (
            var driver = UiDriver.Render(ui =>
            {
                using (
                    ui.Box(
                        width: 10,
                        height: 10,
                        action: UiAction.Confirm,
                        onClick: Callback.Of(log, static l => l.Add("box"))
                    )
                ) { }
                ModalBox(ui);
            })
        )
            driver.Press(Key.Enter);

        await Assert.That(log.ToString()).IsEqualTo("");
    }

    [FramePath]
    static void ModalBox(
        Ui ui,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                modal: true,
                position: Position.Absolute,
                left: 0,
                top: 0,
                width: 100,
                height: 100,
                background: Color.FromRgb(5, 5, 5),
                file: file,
                line: line
            )
        )
        using (ui.Box(width: 10, height: 10, focusable: true)) { }
    }

    [Test]
    public async Task Scope_actions_list_the_shown_actions_and_follow_a_rebinding()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Screen(name: "OK", log: log, active: true);
                ui.ActionBarProbe();
            }
        });
        var before = driver.GetText("Enter");

        driver.View.Context.Actions[UiAction.Confirm] = new ActionBinding(Key.F, null);
        driver.Settle();
        driver.Press(Key.F);

        await Assert.That(before).IsNotNull();
        await Assert.That(driver.GetText("OK")).IsNotNull();
        await Assert.That(driver.GetText("other")).IsNotNull();
        await Assert.That(log.ToString()).IsEqualTo("OK");
    }

    [Test]
    public async Task A_gamepad_moves_focus_by_its_d_pad_and_presses_with_south()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 4))
            {
                Button(ui, log, "first");
                Button(ui, log, "second");
            }
        });
        driver.Tab();

        driver.Press(GamepadButton.DPadRight);
        driver.Press(GamepadButton.South);
        driver.Press(Key.Enter, ModifierKeys.Control);

        await Assert.That(driver.Focused?.Id).IsEqualTo("second");
        await Assert.That(log.ToString()).IsEqualTo("second");
    }

    [Test]
    public async Task A_held_d_pad_repeats_after_its_delay_until_released()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 4))
            {
                for (var i = 0; i < 6; i++)
                    Button(ui, log, "row" + i);
            }
        });
        driver.Tab();

        driver.View.Gamepad(GamepadButton.DPadDown, true);
        driver.Frame(0.41f);
        driver.Frame(0.08f);
        driver.View.Gamepad(GamepadButton.DPadDown, false);
        driver.Frame(1f);

        await Assert.That(driver.Focused?.Id).IsEqualTo("row3");
    }

    [Test]
    public async Task The_input_device_follows_the_last_key_button_or_touch()
    {
        var seen = new Seen<InputDevice>();
        using var driver = UiDriver.Render(ui => ui.DeviceProbe(seen: seen, id: "probe"));

        driver.Press(GamepadButton.North);
        var pad = seen.Value;
        driver.Tap(driver.Get("probe"));
        var touch = seen.Value;
        driver.Press(Key.A);

        await Assert.That(pad).IsEqualTo(InputDevice.Gamepad);
        await Assert.That(touch).IsEqualTo(InputDevice.Touch);
        await Assert.That(seen.Value).IsEqualTo(InputDevice.KeyboardMouse);
    }

    [FramePath]
    static void Button(
        Ui ui,
        Log log,
        string id,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                width: 20,
                height: 20,
                onClick: Callback.Of(log, id, static (l, name) => l.Add(name)),
                id: id,
                key: id,
                file: file,
                line: line
            )
        ) { }
    }

    [Test]
    public async Task A_view_tells_a_key_held_from_its_down_to_its_up_or_the_window_losing_the_focus()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 10, height: 10)) { }
        });

        driver.KeyDown(Key.Space);
        var held = driver.View.IsKeyDown(Key.Space);
        driver.KeyUp(Key.Space);
        var up = driver.View.IsKeyDown(Key.Space);
        driver.KeyDown(Key.Space);
        driver.View.WindowFocus(false);

        await Assert
            .That((held, up, driver.View.IsKeyDown(Key.Space)))
            .IsEqualTo((true, false, false));
    }
}

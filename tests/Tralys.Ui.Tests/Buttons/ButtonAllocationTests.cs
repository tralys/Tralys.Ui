using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class ButtonAllocationTests
{
    [Test]
    public async Task A_buttons_command_run_by_a_click_every_frame_allocates_nothing()
    {
        var runs = new Counter();
        var frames = new Counter();
        var command = new Command(() => runs.Value++);
        using var driver = UiDriver.Render(ui =>
            ui.Button(text: "Save", onClick: command, id: "save")
        );
        var save = driver.Get("save");

        var bytes = driver.WorstFrameBytes(
            (save, frames),
            static (d, s) =>
            {
                d.Click(s.save, settle: false);
                s.frames.Value++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(runs.Value).IsEqualTo(frames.Value);
    }

    [Test]
    public async Task A_command_parameter_changed_and_run_by_a_button_a_shortcut_and_an_action_every_frame_allocates_nothing()
    {
        var runs = new Counter();
        var frames = new Counter();
        var command = new Command<int>(_ => runs.Value++, static p => p > 0);
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            var run = command.With(model.Remember ? 2 : 1);
            ui.Shortcut(Chord.Of(Key.S, ModifierKeys.Control), run);
            ui.Action(UiAction.Back, run);
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Button(text: "Save", onClick: run, id: "save");
                ui.ActionBarProbe();
            }
        });
        var save = driver.Get("save");

        var bytes = driver.WorstFrameBytes(
            (save, model, frames),
            static (d, s) =>
            {
                s.model.Remember = !s.model.Remember;
                d.Frame();
                d.Click(s.save, settle: false);
                d.Press(Key.S, ModifierKeys.Control, settle: false);
                d.Press(Key.Escape, settle: false);
                s.frames.Value++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(runs.Value).IsEqualTo(3 * frames.Value);
    }

    [Test]
    public async Task An_async_command_awaited_on_every_click_and_completed_allocates_nothing()
    {
        var handler = new PendingHandler();
        var command = new Command(handler.Run);
        using var driver = UiDriver.Render(ui =>
            ui.Button(text: "Save", onClick: command, id: "save")
        );
        var run = new AsyncRun(driver.Get("save"), handler);

        var bytes = driver.WorstFrameBytes(
            run,
            static (d, r) =>
            {
                d.Click(r.Save, settle: false);
                d.Frame();
                if ((r.Save.States & InputStates.Disabled) != 0)
                    r.Greyed++;
                r.Handler.Finish();
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(run.Greyed).IsEqualTo(handler.Runs);
    }

    [Test]
    public async Task A_flash_stopped_and_started_again_every_few_frames_allocates_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
            ui.Button(
                text: "Review",
                variant: Theming.ComponentVariant.Outlined,
                flashing: !model.Remember
            )
        );

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count++;
                if (m.Count % 12 is 0 or 2)
                    m.Remember = !m.Remember;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Space_held_on_a_repeat_button_for_many_frames_allocates_nothing()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
            ui.RepeatButton(
                text: "More",
                onClick: Callback.Of(clicks, static c => c.Value++),
                id: "more"
            )
        );
        driver.Focus(driver.Get("more"));
        driver.KeyDown(Key.Space, settle: false);

        var bytes = driver.WorstFrameBytes(static d =>
        {
            d.KeyDown(Key.Space, repeat: true, settle: false);
            d.Frame();
        });

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(clicks.Value).IsGreaterThan(10);
    }

    [Test]
    public async Task Toggles_flipped_menus_opened_and_closed_and_art_buttons_hovered_and_pressed_allocate_nothing()
    {
        using var driver = UiDriver
            .Render(ui =>
            {
                var bold = ui.State(false);
                var star = ui.State(false);
                using (ui.Box(align: Align.Start, gap: 8))
                {
                    ui.Toggle(
                        text: "Bold",
                        value: bold.Value,
                        onValueChange: bold.Setter,
                        id: "bold"
                    );
                    ui.Toggle(
                        icon: new IconGlyph(0xe176, "lucide"),
                        value: star.Value,
                        onValueChange: star.Setter,
                        id: "star"
                    );
                    ui.MenuButton(
                        text: "Mode",
                        chevron: true,
                        menu: MenuButtonTests.Rows,
                        id: "menu"
                    );
                    ui.SplitButton(text: "Sell", menu: MenuButtonTests.Rows, id: "split");
                    ui.CloseButton(id: "close");
                    ui.ImageButton(
                        source: "rest",
                        pressedSource: "down",
                        width: 32,
                        height: 32,
                        id: "art"
                    );
                }
            })
            .WithIcons();
        var nodes = new Targets(
            driver.Get("bold"),
            driver.Get("star"),
            driver.Get("menu"),
            driver.Get("split"),
            driver.All<SplitButtonPart>()[1],
            driver.Get("close"),
            driver.Get("art")
        );

        var bytes = driver.WorstFrameBytes(
            nodes,
            static (d, n) =>
            {
                d.Click(n.Bold, settle: false);
                d.Click(n.Star, settle: false);
                d.Frame();
                d.Click(n.Menu, settle: false);
                d.Frame();
                d.Press(Key.Escape, settle: false);
                d.Click(n.Chevron, settle: false);
                d.Frame();
                d.Click(n.Chevron, settle: false);
                d.Hover(n.Close, settle: false);
                d.Frame();
                d.Click(n.Art, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    sealed class AsyncRun(Node save, PendingHandler handler)
    {
        public readonly Node Save = save;
        public readonly PendingHandler Handler = handler;
        public int Greyed;
    }

    sealed record Targets(
        Node Bold,
        Node Star,
        Node Menu,
        Node Split,
        Node Chevron,
        Node Close,
        Node Art
    );
}

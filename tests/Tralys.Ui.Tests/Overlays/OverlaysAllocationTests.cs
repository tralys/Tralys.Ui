using System.Numerics;
using Tralys.Ui.Testing;
using Tralys.Ui.Tests;

namespace Tralys.Ui.Components.Tests;

public sealed class OverlaysAllocationTests
{
    static readonly Slot Body = Slot.Of(static ui =>
    {
        using (ui.Box(width: 60, height: 24, focusable: true)) { }
    });

    [Test]
    public async Task A_popovers_open_flipped_every_20_frames_allocates_nothing()
    {
        var open = new Switches();
        var frames = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Popover(open: open.A, content: Body))
            using (ui.PopoverTrigger())
                ui.Text("Filters");
        });

        var bytes = driver.WorstFrameBytes(
            (open, frames),
            static (d, s) =>
            {
                if (++s.frames.Value % 20 == 0)
                    s.open.A = !s.open.A;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_dialog_opened_by_enter_and_closed_by_escape_every_10_frames_tabbed_through_meanwhile_allocates_nothing()
    {
        var open = new Switches();
        var frames = new Counter();
        var inside = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            ui.Button(
                text: "Open",
                onClick: Callback.Of(open, static s => s.A = true),
                id: "opener"
            );
            using (
                ui.Dialog(
                    open: open.A,
                    onOpenChange: Callback<bool>.Of(open, static (s, value) => s.A = value)
                )
            )
            {
                ui.DialogTitle(text: "Rename");
                using (ui.Box(focusable: true, width: 40, height: 20)) { }
                using (ui.DialogFooter())
                    ui.DialogClose(text: "Cancel");
            }
        });
        driver.Focus(driver.Get("opener"));

        var bytes = AllocationGate.WorstFrameBytes(() =>
        {
            var n = ++frames.Value;
            if (n % 10 == 0)
                driver.Press(open.A ? Key.Escape : Key.Enter, settle: false);
            else if (open.A && n % 2 == 1)
                driver.Tab(settle: false);
            driver.Frame();
            if (open.A && driver.Focused is not null && driver.View.Layers.Length == 1)
                inside.Value++;
        });

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(inside.Value).IsGreaterThan(0);
    }

    [Test]
    public async Task A_window_moving_over_a_panel_whose_layer_draws_with_it_allocates_nothing()
    {
        var model = new Model();
        var fill = Color.FromRgb(90, 90, 90);
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 400, height: 300))
            {
                using (
                    ui.Box(
                        width: 200,
                        height: 100,
                        position: Position.Absolute,
                        containsLayers: true
                    )
                )
                using (ui.Layer(align: Align.Start))
                using (ui.Box(width: 150, height: 80, background: fill)) { }
                using (
                    ui.Box(
                        width: 100,
                        height: 100,
                        position: Position.Absolute,
                        left: 50 + model.Count % 50,
                        background: fill
                    )
                ) { }
            }
        });

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count++;
                d.View.HitTest(new System.Numerics.Vector2(60, 40));
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_dialog_fading_in_and_out_every_10_frames_allocates_nothing()
    {
        var open = new Switches();
        var frames = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Dialog(open: open.A, transition: new Transition(0.1f)))
                ui.DialogTitle(text: "Logging in");
        });

        var bytes = AllocationGate.WorstFrameBytes(() =>
        {
            if (++frames.Value % 10 == 0)
                open.A = !open.A;
            driver.Frame();
        });

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task An_overlay_opened_by_a_click_and_closed_by_escape_or_a_scrim_press_in_turn_allocates_nothing()
    {
        var open = new Switches();
        var frames = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 400, height: 200))
            {
                ui.Button(
                    text: "Open",
                    onClick: Callback.Of(open, static s => s.A = true),
                    id: "opener"
                );
                using (
                    ui.Overlay(
                        open: open.A,
                        onOpenChange: Callback<bool>.Of(open, static (s, value) => s.A = value),
                        closesOnScrimClick: true,
                        position: Position.Absolute,
                        left: 0,
                        top: 0,
                        right: 0,
                        bottom: 0
                    )
                )
                using (ui.Box(width: 60, height: 40)) { }
            }
        });
        var opener = driver.Get("opener");

        var bytes = driver.WorstFrameBytes(
            (opener, frames),
            static (d, s) =>
            {
                switch (++s.frames.Value % 30)
                {
                    case 0 or 15:
                        d.Click(s.opener, settle: false);
                        break;
                    case 10:
                        d.Press(Key.Escape, settle: false);
                        break;
                    case 25:
                        d.Click(new Vector2(390f, 190f), settle: false);
                        break;
                }
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_tip_shown_after_its_delay_hopped_to_the_next_trigger_and_hidden_every_90_frames_allocates_nothing()
    {
        var frames = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8, padding: 40))
            {
                using (ui.Tooltip(text: "Saves the file"))
                    ui.Button(text: "Save", id: "save");
                using (ui.Tooltip(text: "Opens a file"))
                    ui.Button(text: "Load", id: "load");
                using (ui.Box(width: 40, height: 20, id: "blank")) { }
            }
        });
        var nodes = (
            save: driver.Get("save"),
            load: driver.Get("load"),
            blank: driver.Get("blank")
        );
        var shown = new Counter();

        var bytes = driver.WorstFrameBytes(
            (nodes, frames, shown),
            static (d, s) =>
            {
                switch (++s.frames.Value % 90)
                {
                    case 0:
                        d.Hover(s.nodes.save, settle: false);
                        break;
                    case 30:
                        d.Hover(s.nodes.load, settle: false);
                        break;
                    case 60:
                        d.Hover(s.nodes.blank, settle: false);
                        break;
                }
                d.Frame();
                s.shown.Value += d.View.Layers.Length;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(shown.Value).IsGreaterThan(0);
    }

    [Test]
    public async Task A_tip_shown_by_a_held_finger_and_by_keyboard_focus_in_turn_allocates_nothing()
    {
        var frames = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8, padding: 40))
            {
                using (ui.Tooltip(text: "Saves the file"))
                    ui.Button(text: "Save", id: "save");
                using (ui.Tooltip(text: "Opens a file"))
                    ui.Button(text: "Load", id: "load");
            }
        });
        var save = driver.Get("save");

        var bytes = driver.WorstFrameBytes(
            (save, frames),
            static (d, s) =>
            {
                if (++s.frames.Value % 10 == 0)
                    d.LongPress(s.save, settle: false);
                else
                    d.Tab(settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }
}

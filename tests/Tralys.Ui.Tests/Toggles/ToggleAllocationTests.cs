using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class ToggleAllocationTests
{
    [Test]
    public async Task Checkboxes_a_radio_and_a_switch_clicked_in_turn_every_frame_and_fed_back_allocate_nothing()
    {
        var choices = new Choices { Picked = 1 };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Checkbox(value: choices.On, onValueChange: choices.SetOn, id: "two");
                ui.Checkbox(value: choices.State, onValueChange: choices.SetState, id: "three");
                using (ui.RadioGroup(value: choices.Picked, onValueChange: choices.SetPicked))
                {
                    ui.Radio(value: 1, label: "one", id: "one");
                    ui.Radio(value: 2, label: "two", id: "radio");
                }
                ui.Switch(value: choices.Open, onValueChange: choices.SetOpen, id: "switch");
            }
        });
        var targets = new Targets([
            driver.Get("two"),
            driver.Get("three"),
            driver.Get("one"),
            driver.Get("radio"),
            driver.Get("switch"),
        ]);

        var bytes = driver.WorstFrameBytes(
            targets,
            static (d, t) =>
            {
                d.Click(t.Nodes[t.Count++ % t.Nodes.Length], settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_radio_groups_pick_moved_by_an_arrow_every_frame_back_and_forth_allocates_nothing()
    {
        var choices = new Choices { Picked = 0 };
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.RadioGroup(
                    value: choices.Picked,
                    onValueChange: choices.SetPicked,
                    direction: Direction.Row
                )
            )
                for (var i = 0; i < 5; i++)
                    ui.Radio(value: i, label: Labels[i], id: Labels[i], key: i);
        });
        driver.Focus(driver.Get("a"));
        var state = new Targets([]);

        var bytes = driver.WorstFrameBytes(
            state,
            static (d, s) =>
            {
                var key = s.Count++ / 4 % 2 == 0 ? Key.Right : Key.Left;
                d.KeyDown(key, settle: false);
                d.KeyUp(key, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        var focused = await Assert.That(driver.Focused).IsNotNull();
        await Assert.That(focused.Id).IsEqualTo(Labels[(int)Number(choices.Picked)]);
    }

    [Test]
    public async Task A_parked_switch_shown_again_every_few_frames_with_its_value_swapped_allocates_nothing()
    {
        var choices = new Choices { Open = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(collapsed: !choices.Open))
                ui.Switch(value: choices.On, label: "Auto");
        });

        var bytes = driver.WorstFrameBytes(
            choices,
            static (d, c) =>
            {
                switch (++c.Count % 6)
                {
                    case 0:
                        c.Open = false;
                        break;
                    case 1:
                        c.On = !c.On;
                        break;
                    case 2:
                        c.Open = true;
                        break;
                }
                d.Frame(0.016f);
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_collapsible_clicked_mid_motion_every_few_frames_while_the_theme_switches_allocates_nothing()
    {
        var choices = new Choices();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, width: 200))
            {
                using (
                    ui.Collapsible(
                        header: "Tall",
                        open: choices.Open,
                        onOpenChange: choices.SetOpen,
                        id: "tall"
                    )
                )
                using (ui.Box(direction: Direction.Column, gap: 4))
                    for (var i = 0; i < Labels.Length; i++)
                        ui.Text(Labels[i], key: i);
                ui.Text("After");
            }
        });
        var header = await Assert.That(ToggleLooks.Tick(driver.Get("tall")).Parent).IsNotNull();
        var state = new Targets([header]);

        var bytes = driver.WorstFrameBytes(
            state,
            static (d, s) =>
            {
                var tick = ++s.Count;
                if (tick % 5 == 0)
                    d.Click(s.Nodes[0], settle: false);
                if (tick % 20 == 0)
                    d.View.Context.Theme =
                        d.View.Context.Theme == Themes.Dark ? Themes.Light : Themes.Dark;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    static readonly string[] Labels = ["a", "b", "c", "d", "e"];

    static long Number(UiKey key) => key.TryGetLong(out var value) ? value : -1;

    sealed class Targets(Node[] nodes)
    {
        public readonly Node[] Nodes = nodes;
        public int Count;
    }
}

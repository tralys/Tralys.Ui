using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class AnimationAllocationTests
{
    [Test]
    public async Task Sprites_turning_frames_inset_plain_and_sliced_allocate_nothing()
    {
        var sheet = new TestTexture(512, 320);
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            {
                ui.Image(sheet, sprite: new Sprite(new Size(64, 64), 8, 8, TexelInset: 0.5f));
                ui.Image(
                    sheet,
                    slice: new NineSlice(new Thickness(8f)),
                    sprite: new Sprite(new Size(64, 64), 8, 8, TexelInset: 0.5f),
                    width: 100,
                    height: 40
                );
            }
        });

        var bytes = driver.WorstFrameBytes(0, static (d, _) => d.Frame(0.1f));

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Animations_retargeted_loops_running_and_a_switch_clicked_every_few_frames_allocate_nothing()
    {
        var model = new Model();
        var probe = new Probe();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, gap: 4))
            {
                ui.Mover(
                    target: model.Count % 2 == 0 ? 0f : 100f,
                    color: model.Count % 2 == 0
                        ? Color.FromRgb(0, 0, 0)
                        : Color.FromRgb(255, 255, 255),
                    transition: new(0.2f, Easings.Back(1.5f)),
                    probe: probe
                );
                ui.Looper(period: 0.5f, probe: probe);
                ui.Looper(period: 0.25f, count: 2, alternate: true, probe: probe, key: model.Count);
                ui.Switch(value: model.Remember, onValueChange: model.SetRemember, label: "Auto");
            }
        });
        var state = new Ticks(model, driver.Get<Switch>());

        var bytes = driver.WorstFrameBytes(
            state,
            static (d, s) =>
            {
                var tick = ++s.Count;
                if (tick % 7 == 0)
                    s.Model.Count++;
                if (tick % 13 == 0)
                    d.Click(s.Switch, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Components_entering_and_exiting_every_few_frames_allocate_nothing()
    {
        var items = new ObservableList<int> { 0, 1, 2 };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, gap: 4))
            {
                foreach (var n in items)
                    ui.Notice(number: n, exit: new(0.3f), key: n);
            }
        });
        var state = new Churn(items);

        var bytes = driver.WorstFrameBytes(
            state,
            static (d, s) =>
            {
                if (++s.Count % 5 == 0)
                {
                    s.Items.Add(s.Next++);
                    s.Items.RemoveAt(s.Count % 3);
                }
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_mask_whose_middle_stop_fades_every_frame_redraws_each_frame_and_allocates_nothing()
    {
        using var driver = UiDriver.Render(ui => ui.Wiper());
        var frames = new Frames();

        var bytes = driver.WorstFrameBytes(
            frames,
            static (d, f) =>
            {
                d.Frame();
                f.Count++;
                if (d.Stats.DrawListRebuilt)
                    f.Redrawn++;
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(frames.Redrawn).IsEqualTo(frames.Count);
    }

    sealed class Frames
    {
        public int Count;
        public int Redrawn;
    }

    sealed class Ticks(Model model, Node @switch)
    {
        public readonly Model Model = model;
        public readonly Node Switch = @switch;
        public int Count;
    }

    sealed class Churn(ObservableList<int> items)
    {
        public readonly ObservableList<int> Items = items;
        public int Next = 3;
        public int Count;
    }
}

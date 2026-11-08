using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class DisplayAllocationTests
{
    [Test]
    public async Task A_badge_counting_and_a_cooldown_ticking_every_frame_allocate_nothing()
    {
        var model = new Ticker();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 8))
            {
                ui.Badge(count: model.Count, prefix: "@");
                using (ui.Cooldown(duration: 4f, remaining: model.Remaining, width: 40, height: 40))
                { }
            }
        });

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count = (m.Count + 7) % 130;
                m.Remaining = m.Remaining <= 0f ? 4f : MathF.Max(0f, m.Remaining - 0.15f);
                d.Rerender();
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task An_icon_whose_glyph_changes_every_frame_allocates_nothing()
    {
        var model = new Ticker();
        IconGlyph[] glyphs =
        [
            new(0xe176, "lucide"),
            IconGlyph.Of(IconRole.Check),
            default,
            IconGlyph.Of(IconRole.ChevronDown),
        ];
        using var driver = UiDriver
            .Render(ui =>
                ui.Icon(glyph: glyphs[model.Count % glyphs.Length], flipped: model.Count % 2 == 0)
            )
            .WithIcons();

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count++;
                d.Rerender();
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task An_action_bar_whose_device_switches_every_frame_redraws_its_glyphs_and_allocates_nothing()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.ActionScope())
            {
                ui.Action(UiAction.Confirm, onRun: default, label: "Open");
                ui.Action(UiAction.Back, onRun: default, label: "Close");
                ui.ActionBar();
            }
        });
        var seen = new Seen();

        var bytes = driver.WorstFrameBytes(
            seen,
            static (d, s) =>
            {
                if (s.Frames++ % 2 == 0)
                    d.Press(GamepadButton.DPadDown, settle: false);
                else
                    d.Press(Key.Down, settle: false);
                d.Frame();
                s.Keys += d.Stats.RendersOf<Kbd>();
                s.Pads += d.Stats.RendersOf<GamepadGlyph>();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That((seen.Keys > 0, seen.Pads > 0)).IsEqualTo((true, true));
    }

    sealed class Ticker
    {
        public int Count;
        public float Remaining = 4f;
    }

    sealed class Seen
    {
        public int Frames;
        public int Keys;
        public int Pads;
    }
}

using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class AnimationTests
{
    static readonly IEasingFunction Linear = new KeySpline();

    [Test]
    public async Task An_animation_holds_for_its_delay_runs_its_duration_then_stops_rerendering()
    {
        var model = new Model();
        var probe = new Probe();
        using var driver = UiDriver.Render(ui =>
            ui.Mover(target: model.Count, transition: new(1f, Linear, 0.5f), probe: probe)
        );

        model.Count = 100;
        driver.Frame(0.25f);
        driver.Frame(0.25f);
        var held = probe.Value;
        driver.Frame(0.5f);
        var running = probe.Value;
        driver.Frame(1f);
        var ended = probe.Value;
        driver.Frame();

        await Assert.That(held).IsEqualTo(0f);
        await Assert.That(running).IsEqualTo(25f).Within(1e-3f);
        await Assert.That(ended).IsEqualTo(100f);
        await Assert.That(driver.Stats.Renders).IsEqualTo(0);
        await Assert.That(driver.View.IsIdle).IsTrue();
    }

    [Test]
    public async Task A_value_mounts_at_rest_and_a_retarget_runs_from_the_value_shown()
    {
        var model = new Model { Count = 1 };
        var probe = new Probe();
        using var driver = UiDriver.Render(ui =>
            ui.Mover(target: model.Count, transition: new(1f, Linear), probe: probe)
        );

        model.Count = 0;
        driver.Frame(0.1f);
        driver.Frame(0.5f);
        model.Count = 1;
        driver.Frame(0.25f);
        driver.Frame(0.5f);

        await Assert.That(probe.First).IsEqualTo(1f);
        await Assert.That(probe.Value).IsEqualTo(0.625f).Within(1e-3f);
    }

    [Test]
    public async Task A_zero_duration_jumps_on_the_frame_of_the_change_or_once_its_delay_is_out()
    {
        var model = new Model { Count = 100 };
        var probe = new Probe();
        using var driver = UiDriver.Render(ui =>
            ui.Mover(
                target: model.Count,
                transition: model.Remember ? new(0f, Linear, 1f) : Transition.Instant,
                probe: probe
            )
        );

        model.Count = 50;
        driver.Frame(0.1f);
        var jumped = probe.Value;
        model.Remember = true;
        model.Count = 0;
        driver.Frame(0.5f);
        driver.Frame(0.75f);
        var held = probe.Value;
        driver.Frame(0.5f);

        await Assert.That((jumped, held, probe.Value)).IsEqualTo((50f, 50f, 0f));
    }

    [Test]
    public async Task A_retarget_with_a_delay_holds_the_value_shown_at_the_takeover_then_runs_from_it()
    {
        var model = new Model();
        var probe = new Probe();
        using var driver = UiDriver.Render(ui =>
            ui.Mover(
                target: model.Count,
                transition: new(1f, Linear, model.Remember ? 0.5f : 0f),
                probe: probe
            )
        );

        model.Count = 100;
        driver.Frame(0.1f);
        driver.Frame(0.5f);
        model.Remember = true;
        model.Count = 0;
        driver.Frame(0.25f);
        var takeover = probe.Value;
        driver.Frame(0.25f);
        var held = probe.Value;
        driver.Frame(0.5f);

        await Assert.That(takeover).IsEqualTo(75f).Within(1e-3f);
        await Assert.That(held).IsEqualTo(takeover);
        await Assert.That(probe.Value).IsEqualTo(56.25f).Within(1e-3f);
    }

    [Test]
    public async Task A_colour_blends_in_linear_light()
    {
        var model = new Model();
        var probe = new Probe();
        using var driver = UiDriver.Render(ui =>
            ui.Mover(
                color: model.Count == 0 ? Color.FromRgb(0, 0, 0) : Color.FromRgb(255, 255, 255),
                transition: new(1f, Linear),
                probe: probe
            )
        );

        model.Count = 1;
        driver.Frame(0.1f);
        driver.Frame(0.5f);

        await Assert.That(probe.Color).IsEqualTo(Color.FromRgb(188, 188, 188));
    }

    [Test]
    public async Task A_colour_fading_in_from_transparent_takes_no_tint_from_its_channels()
    {
        var model = new Model();
        var probe = new Probe();
        using var driver = UiDriver.Render(ui =>
            ui.Mover(
                color: model.Count == 0
                    ? Color.FromArgb(0, 255, 255, 255)
                    : Color.FromRgb(32, 48, 64),
                transition: new(1f, Linear),
                probe: probe
            )
        );

        model.Count = 1;
        driver.Frame(0.1f);
        driver.Frame(0.5f);

        await Assert.That(probe.Color).IsEqualTo(Color.FromArgb(128, 32, 48, 64));
    }

    [Test]
    public async Task Animation_speed_scales_time_and_reduced_motion_lands_a_transition_at_once()
    {
        var model = new Model();
        var probe = new Probe();
        using var driver = UiDriver.Render(ui =>
            ui.Mover(target: model.Count, transition: new(1f, Linear), probe: probe)
        );
        var context = driver.View.Context;

        context.AnimationSpeed = 0.5f;
        model.Count = 100;
        driver.Frame(0.1f);
        driver.Frame(1f);
        var halfway = probe.Value;
        context.AnimationSpeed = 1f;
        context.ReducedMotion = true;
        model.Count = 0;
        driver.Frame();

        await Assert.That(halfway).IsEqualTo(50f).Within(1e-3f);
        await Assert.That(probe.Value).IsEqualTo(0f);
        await Assert.That(driver.View.IsIdle).IsTrue();
    }

    [Test]
    public async Task Time_before_the_first_advance_does_not_pass_for_a_run_started_before_it()
    {
        var model = new Model();
        var probe = new Probe();
        using var fonts = UiDriver.Render(static _ => { });
        using var context = new UiContext(fonts.View.Context.Text);
        var view = new UiView(context, new NoHost()) { Size = new Size(100, 100) };
        view.Mount(
            Slot.Of(
                Tuple.Create(model, probe),
                static (ui, s) =>
                    ui.Mover(target: s.Item1.Count, transition: new(1f, Linear), probe: s.Item2)
            )
        );
        view.Update();
        model.Count = 1;
        view.Update();
        var started = TimeSpan.FromHours(5);
        context.Advance(started);
        view.Update();
        context.Advance(started + TimeSpan.FromSeconds(0.5));
        view.Update();

        await Assert.That(probe.Value).IsEqualTo(0.5f).Within(1e-3f);
    }

    [Test]
    public async Task Cubic_and_back_easings_shape_their_own_end_of_a_run()
    {
        var model = new Model();
        var cubicIn = new Probe();
        var cubicOut = new Probe();
        var cubicInOut = new Probe();
        var backIn = new Probe();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box())
            {
                ui.Mover(target: model.Count, transition: new(1f, Easings.CubicIn), probe: cubicIn);
                ui.Mover(
                    target: model.Count,
                    transition: new(1f, Easings.CubicOut),
                    probe: cubicOut
                );
                ui.Mover(
                    target: model.Count,
                    transition: new(1f, Easings.CubicInOut),
                    probe: cubicInOut
                );
                ui.Mover(
                    target: model.Count,
                    transition: new(1f, Easings.Back(1f, EasingMode.In)),
                    probe: backIn
                );
            }
        });

        model.Count = 100;
        driver.Frame(0.1f);
        driver.Frame(0.25f);
        var quarter = cubicInOut.Value;
        driver.Frame(0.25f);
        var half = (cubicIn.Value, cubicOut.Value, backIn.Value);
        driver.Frame(0.25f);
        var threeQuarters = cubicInOut.Value;

        await Assert.That(half.Item1).IsEqualTo(12.5f).Within(0.01f);
        await Assert.That(half.Item2).IsEqualTo(87.5f).Within(0.01f);
        await Assert.That(half.Item3).IsEqualTo(-37.5f).Within(0.01f);
        await Assert.That(quarter).IsEqualTo(6.25f).Within(0.01f);
        await Assert.That(threeQuarters).IsEqualTo(93.75f).Within(0.01f);
    }

    sealed class NoHost : IViewHost
    {
        public void SetCursor(Cursor? cursor) { }

        public string? GetClipboardText() => null;

        public void SetClipboardText(ReadOnlySpan<char> text) { }

        public void StartTextInput(TextInputKind kind, Rect area, bool byContact) { }

        public void StopTextInput() { }
    }

    [Test]
    public async Task A_node_clamps_a_value_out_of_range_and_keeps_its_last_on_one_not_finite()
    {
        var opacity = 0.5f;
        var width = 50f;
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    id: "box",
                    width: width,
                    height: 10,
                    opacity: opacity,
                    background: Color.FromRgb(200, 0, 0)
                )
            ) { }
        });

        opacity = 1.4f;
        width = float.NaN;
        driver.Rerender();
        var clamped = driver.Get("box").Visual.Opacity;
        var kept = driver.Get("box").Bounds.Width;
        opacity = float.PositiveInfinity;
        width = -20f;
        driver.Rerender();

        await Assert.That(clamped).IsEqualTo(1f);
        await Assert.That(kept).IsEqualTo(50f);
        await Assert.That(driver.Get("box").Visual.Opacity).IsEqualTo(1f);
        await Assert.That(driver.Get("box").Bounds.Width).IsEqualTo(0f);
    }

    [Test]
    public async Task An_alternating_loop_counts_there_and_back_as_one_cycle_then_rests_at_one()
    {
        var model = new Model();
        var probe = new Probe();
        using var driver = UiDriver.Render(ui =>
        {
            if (model.Remember)
                ui.Looper(period: 0.5f, count: 1, alternate: true, probe: probe);
        });

        model.Remember = true;
        driver.Frame(0.1f);
        driver.Frame(0.125f);
        var forward = probe.Value;
        driver.Frame(0.5f);
        var back = probe.Value;
        driver.Frame(0.5f);
        var rest = probe.Value;
        driver.Frame();

        await Assert.That(forward).IsEqualTo(0.25f).Within(1e-3f);
        await Assert.That(back).IsEqualTo(0.75f).Within(1e-3f);
        await Assert.That(rest).IsEqualTo(1f);
        await Assert.That(driver.Stats.Renders).IsEqualTo(0);
        await Assert.That(driver.View.IsIdle).IsTrue();
    }

    [Test]
    public async Task A_forever_loop_asks_for_frames_without_holding_up_idleness_until_paused_or_unmounted()
    {
        var model = new Model();
        var probe = new Probe();
        using var driver = UiDriver.Render(ui =>
        {
            if (model.Remember)
                ui.Looper(period: 1f, paused: model.Count == 1, probe: probe);
        });

        model.Remember = true;
        driver.Frame();
        driver.Frame(0.25f);
        var running = driver.View.NeedsFrame && driver.View.IsIdle;
        model.Count = 1;
        driver.Frame(0.25f);
        var paused = probe.Value;
        driver.Frame(0.25f);
        var stillPaused = probe.Value;
        var pausedNeedsFrame = driver.View.NeedsFrame;
        model.Count = 0;
        driver.Frame(0.25f);
        model.Remember = false;
        driver.Frame();

        await Assert.That(running).IsTrue();
        await Assert.That(paused).IsEqualTo(0.5f).Within(1e-3f);
        await Assert.That(stillPaused).IsEqualTo(paused);
        await Assert.That(pausedNeedsFrame).IsFalse();
        await Assert.That(driver.View.NeedsFrame).IsFalse();
    }

    [Test]
    public async Task A_loop_under_a_hidden_or_faulted_ancestor_rests_asking_for_no_frames_and_starts_over_once_shown()
    {
        var model = new Model();
        var probe = new Probe();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(hidden: model.Remember))
                ui.BrittleLooper(model: model, probe: probe);
        });
        driver.View.Context.OnFault = static _ => { };

        float RestedThenShownAgain(Action hide, Action show)
        {
            hide();
            driver.Frame(0.25f);
            driver.Frame(0.25f);
            var asks = driver.View.NeedsFrame || driver.Stats.Renders > 0;
            show();
            driver.Frame(0.25f);
            driver.Frame(0.25f);
            return asks ? float.NaN : probe.Value;
        }

        driver.Frame(0.4f);
        var hidden = RestedThenShownAgain(
            () => model.Remember = true,
            () => model.Remember = false
        );
        driver.Frame(0.4f);
        var faulted = RestedThenShownAgain(() => model.Count = 1, () => model.Count = 0);

        await Assert.That(hidden).IsEqualTo(0.25f).Within(1e-3f);
        await Assert.That(faulted).IsEqualTo(0.25f).Within(1e-3f);
        await Assert.That(driver.View.NeedsFrame).IsTrue();
    }

    [Test]
    public async Task A_removed_component_stays_in_its_slot_untouchable_until_its_exit_fades_out()
    {
        var items = new ObservableList<int> { 1, 2, 3 };
        var probe = new Probe();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, gap: 4))
            {
                foreach (var n in items)
                    ui.Notice(
                        number: n,
                        exit: new(1f, Linear),
                        probe: n == 2 ? probe : null,
                        key: n
                    );
            }
        });
        var third = driver.GetText("notice 3").ViewBounds.Y;
        var exiting = driver.GetText("notice 2");

        items.Remove(2);
        driver.Frame(0.1f);
        driver.Frame(0.5f);
        var exitingBox = await Assert.That(exiting.Parent).IsNotNull();
        var box = exitingBox.ViewBounds;
        var hit = driver.View.HitTest(
            new System.Numerics.Vector2(box.X + box.Width / 2f, box.Y + box.Height / 2f)
        );
        var notices = driver.All<Notice>().Count;
        var thirdWhileExiting = driver.GetText("notice 3").ViewBounds.Y;
        var shown = probe.Value;
        var flagged = probe.Exiting;
        driver.Frame(0.5f);

        await Assert.That(notices).IsEqualTo(3);
        await Assert.That(flagged).IsTrue();
        await Assert.That(shown).IsEqualTo(0.5f).Within(1e-3f);
        await Assert.That(hit is not null && exitingBox.IsAncestorOrSelfOf(hit)).IsFalse();
        await Assert.That(thirdWhileExiting).IsEqualTo(third);
        await Assert.That(driver.All<Notice>().Count).IsEqualTo(2);
        await Assert.That(driver.GetText("notice 3").ViewBounds.Y).IsLessThan(third);
    }

    [Test]
    public async Task An_added_component_enters_from_nothing_and_one_called_again_mid_exit_turns_back()
    {
        var items = new ObservableList<int> { 1 };
        var leaving = new Probe();
        var arriving = new Probe();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
            {
                foreach (var n in items)
                    ui.Notice(
                        number: n,
                        exit: new(1f, Linear),
                        probe: n == 1 ? leaving : arriving,
                        key: n
                    );
            }
        });
        var mount = driver.MountId(driver.Get<Notice>());

        items.Clear();
        driver.Frame(0.1f);
        driver.Frame(0.5f);
        items.Add(1);
        items.Add(2);
        driver.Frame(0.1f);
        var revived = driver.MountId(driver.All<Notice>()[0]);
        var turned = leaving.Value;
        driver.Settle();

        await Assert.That(revived).IsEqualTo(mount);
        await Assert.That(leaving.Exiting).IsFalse();
        await Assert.That(turned).IsEqualTo(0.4f).Within(1e-3f);
        await Assert.That(arriving.First).IsEqualTo(0f);
        await Assert.That(arriving.Value).IsEqualTo(1f);
        await Assert.That(leaving.Value).IsEqualTo(1f);
    }
}

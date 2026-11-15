using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;
using TUnit.Assertions.Enums;

namespace Tralys.Ui.Tests;

public sealed class TouchControlTests
{
    [Test]
    public async Task A_stick_follows_its_finger_within_its_radius_steps_toward_the_axis_it_leans_on_across_winning_a_tie_and_centres_on_lift_or_cancel()
    {
        var steps = new Steps();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
                ui.TouchStick(onStepChange: steps.Report, id: "stick");
        });
        var stick = driver.Get("stick");
        var knob = Knob(stick);

        var finger = driver.Hold(stick, PointerKind.Touch);
        finger.MoveBy(new Vector2(5f, 2f));
        var dead = steps.Last;
        finger.MoveBy(new Vector2(25f, -2f));
        var followed = (steps.Last, Offset(knob));
        finger.MoveBy(new Vector2(170f, 0f));
        var clamped = Offset(knob).X;
        finger.MoveBy(new Vector2(-200f, -40f));
        var up = steps.Last;
        finger.MoveBy(new Vector2(40f, 80f));
        var tie = steps.Last;
        finger.Release();
        var lifted = (steps.Last, Offset(knob));
        var cancelled = driver.Hold(stick, PointerKind.Touch);
        cancelled.MoveBy(new Vector2(30f, 0f));
        var steered = steps.Last;
        cancelled.LoseRelease();

        await Assert.That(dead).IsEqualTo(StickStep.None);
        await Assert.That(followed).IsEqualTo((StickStep.Right, new Vector2(30f, 0f)));
        await Assert.That(clamped).IsEqualTo(60f).Within(1e-3f);
        await Assert.That((up, tie)).IsEqualTo((StickStep.Up, StickStep.Right));
        await Assert.That(lifted).IsEqualTo((StickStep.None, Vector2.Zero));
        await Assert.That(steered).IsEqualTo(StickStep.Right);
        await Assert.That((steps.Last, Offset(knob))).IsEqualTo((StickStep.None, Vector2.Zero));
    }

    [Test]
    public async Task A_second_finger_on_a_steered_stick_leaves_it_to_the_first_while_taps_on_a_button_beside_it_click()
    {
        var steps = new Steps();
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui => StickAndButton(ui, steps, clicks));
        var stick = driver.Get("stick");

        var steering = driver.Hold(stick, PointerKind.Touch);
        steering.MoveBy(new Vector2(40f, 0f));
        var other = driver.Hold(stick, PointerKind.Touch);
        other.MoveBy(new Vector2(0f, -60f));
        other.Release();
        var steered = steps.Last;
        driver.Tap(driver.Get("button"));
        driver.Tap(driver.Get("button"));

        await Assert.That(steered).IsEqualTo(StickStep.Right);
        await Assert.That((clicks.Value, steps.Last)).IsEqualTo((2, StickStep.Right));
    }

    [Test]
    public async Task A_stick_and_a_button_disabled_while_held_let_their_fingers_go_and_take_new_ones_once_enabled()
    {
        var steps = new Steps();
        var clicks = new Counter();
        var disabled = false;
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(disabled: disabled))
                StickAndButton(ui, steps, clicks);
        });

        var steering = driver.Hold(driver.Get("stick"), PointerKind.Touch);
        steering.MoveBy(new Vector2(40f, 0f));
        var holding = driver.Hold(driver.Get("button"), PointerKind.Touch);
        disabled = true;
        driver.Rerender();
        var let = steps.Last;
        steering.Release();
        holding.Release();
        disabled = false;
        driver.Rerender();
        driver.Tap(driver.Get("button"));
        var again = driver.Hold(driver.Get("stick"), PointerKind.Touch);
        again.MoveBy(new Vector2(0f, -40f));

        await Assert.That(let).IsEqualTo(StickStep.None);
        await Assert.That((clicks.Value, steps.Last)).IsEqualTo((1, StickStep.Up));
    }

    [Test]
    public async Task A_finger_on_a_stick_or_aiming_from_a_button_inside_a_panning_scroll_area_is_left_to_them()
    {
        var steps = new Steps();
        var aims = new Aims();
        var offsets = new List<Vector2>();
        using var driver = UiDriver.Render(ui =>
        {
            var scroll = ui.ScrollHandle();
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (
                ui.Box(
                    direction: Direction.Column,
                    align: Align.Start,
                    gap: 8,
                    overflow: Overflow.Scroll,
                    scroll: scroll,
                    width: 200,
                    height: 200
                )
            )
            {
                ui.TouchStick(onStepChange: steps.Report, id: "stick");
                using (ui.TouchButton(onAim: aims.Report, width: 48, height: 48, id: "button")) { }
                using (ui.Box(width: 600, height: 600)) { }
            }
            offsets.Add(scroll.Offset);
        });

        driver.Drag(driver.Get("stick"), new Vector2(-30f, -40f), PointerKind.Touch);
        var steered = steps.Reports.ToArray();
        driver.Drag(driver.Get("button"), new Vector2(-40f, -40f), PointerKind.Touch);

        await Assert.That(steered).Contains(StickStep.Up);
        await Assert.That(aims.Taken.Count).IsGreaterThan(0);
        await Assert.That(offsets[^1]).IsEqualTo(Vector2.Zero);
    }

    [Test]
    public async Task A_tap_clicks_though_the_finger_wandered_within_the_slop_or_far_off_and_back_with_no_aim_to_take()
    {
        var clicks = new Counter();
        var aims = new Aims();
        var aiming = true;
        using var driver = UiDriver.Render(ui =>
            Button(ui, clicks, aiming ? aims.Report : default)
        );
        var button = driver.Get("button");

        var finger = driver.Hold(button, PointerKind.Touch);
        finger.MoveBy(new Vector2(4f, 3f));
        finger.Release();
        var wandered = clicks.Value;
        aiming = false;
        driver.Rerender();
        finger = driver.Hold(button, PointerKind.Touch);
        finger.MoveBy(new Vector2(300f, 40f));
        finger.MoveBy(new Vector2(-300f, -40f));
        finger.Release();

        await Assert.That(wandered).IsEqualTo(1);
        await Assert.That(aims.Taken).IsEmpty();
        await Assert.That(clicks.Value).IsEqualTo(2);
    }

    [Test]
    public async Task A_finger_dragged_off_an_aiming_button_aims_instead_and_lifting_back_over_it_or_a_cancel_calls_the_aim_off()
    {
        var clicks = new Counter();
        var aims = new Aims();
        using var driver = UiDriver.Render(ui => Button(ui, clicks, aims.Report));
        var button = driver.Get("button");

        var finger = driver.Hold(button, PointerKind.Touch);
        finger.MoveBy(new Vector2(200f, 40f));
        finger.MoveBy(new Vector2(-200f, -40f));
        finger.Release();
        var backOver = aims.Phases();
        aims.Taken.Clear();
        var cancelled = driver.Hold(button, PointerKind.Touch);
        cancelled.MoveBy(new Vector2(200f, 40f));
        cancelled.LoseRelease();

        await Assert.That(backOver).IsEqualTo("Move off, Move over, Release over");
        await Assert.That(aims.Phases()).IsEqualTo("Move off, Release over");
        await Assert.That(clicks.Value).IsEqualTo(0);
    }

    [Test]
    public async Task An_aim_lifted_off_the_button_fires_where_it_lifts_in_the_views_pixels()
    {
        var clicks = new Counter();
        var aims = new Aims();
        using var driver = UiDriver.Render(ui => Button(ui, clicks, aims.Report), scale: 2f);
        var button = driver.Get("button");
        var from = Centre(button.ViewBounds);

        var finger = driver.Hold(button, PointerKind.Touch);
        finger.MoveBy(new Vector2(250f, 40f));
        finger.Release();
        var off = (from + new Vector2(250f, 40f)) * 2f;

        await Assert
            .That(aims.Taken)
            .IsEquivalentTo(
                [
                    new TouchAim(TouchAimPhase.Move, off, false),
                    new TouchAim(TouchAimPhase.Release, off, false),
                ],
                CollectionOrdering.Matching
            );
    }

    [Test]
    public async Task A_second_finger_on_a_held_button_neither_aims_nor_clicks_and_leaves_the_first_its_click()
    {
        var clicks = new Counter();
        var aims = new Aims();
        using var driver = UiDriver.Render(ui => Button(ui, clicks, aims.Report));
        var button = driver.Get("button");

        var first = driver.Hold(button, PointerKind.Touch);
        var second = driver.Hold(button, PointerKind.Touch);
        second.MoveBy(new Vector2(35f, 0f));
        second.Release();
        var secondLifted = clicks.Value;
        first.Release();

        await Assert.That(secondLifted).IsEqualTo(0);
        await Assert.That(aims.Taken).IsEmpty();
        await Assert.That(clicks.Value).IsEqualTo(1);
    }

    [Test]
    public async Task A_press_anywhere_in_the_buttons_box_clicks_it_and_leaves_the_focus_where_it_was()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Button(text: "Focused", id: "focused");
                using (
                    ui.TouchButton(
                        onClick: Callback.Of(clicks, static c => c.Value++),
                        width: 48,
                        height: 48,
                        id: "button"
                    )
                )
                using (
                    ui.Box(
                        width: 8,
                        height: 8,
                        alignSelf: AlignSelf.Center,
                        justifySelf: AlignSelf.Center
                    )
                ) { }
            }
        });
        driver.Focus(driver.Get("focused"));

        driver.Click(driver.Get("button"), new Vector2(2f, 2f));
        driver.Tap(driver.Get("button"));

        await Assert.That(clicks.Value).IsEqualTo(2);
        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("focused"));
    }

    [FramePath]
    static void StickAndButton(
        Ui ui,
        Steps steps,
        Counter clicks,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Box(gap: 32, align: Align.End, file: file, line: line))
        {
            ui.TouchStick(onStepChange: steps.Report, id: "stick");
            using (
                ui.TouchButton(
                    onClick: Callback.Of(clicks, static c => c.Value++),
                    width: 76,
                    height: 76,
                    id: "button"
                )
            ) { }
        }
    }

    [FramePath]
    static void Button(
        Ui ui,
        Counter clicks,
        Callback<TouchAim> onAim,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                direction: Direction.Column,
                align: Align.Start,
                padding: 20,
                file: file,
                line: line
            )
        )
        using (
            ui.TouchButton(
                onClick: Callback.Of(clicks, static c => c.Value++),
                onAim: onAim,
                width: 48,
                height: 48,
                id: "button"
            )
        ) { }
    }

    static Node Knob(Node stick)
    {
        Node? knob = null;
        foreach (var child in stick.Children)
        {
            if (child.Kind == NodeKind.Box)
                knob = child;
        }
        return knob ?? throw new InvalidOperationException("The stick has no knob.");
    }

    static Vector2 Offset(Node knob) => knob.Visual.Transform.Matrix.Translation;

    static Vector2 Centre(Rect rect) => new(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
}

internal sealed class Steps
{
    public readonly List<StickStep> Reports = [];
    public readonly Callback<StickStep> Report;

    public Steps() =>
        Report = Callback<StickStep>.Of(this, static (s, step) => s.Reports.Add(step));

    public StickStep Last => Reports.Count == 0 ? StickStep.None : Reports[^1];
}

internal sealed class Aims
{
    public readonly List<TouchAim> Taken = [];
    public readonly Callback<TouchAim> Report;

    public Aims() => Report = Callback<TouchAim>.Of(this, static (a, aim) => a.Taken.Add(aim));

    public string Phases() =>
        string.Join(", ", Taken.Select(a => $"{a.Phase} {(a.IsOverButton ? "over" : "off")}"));
}

using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class ScrollbarTests
{
    [Test]
    public async Task Dragging_the_thumb_moves_the_value_by_the_tracks_scale_and_the_thumb_follows_once_the_value_does()
    {
        var owner = new BarOwner();
        using var driver = owner.Render(value: 0f, max: 300f, viewport: 100f, height: 224f);
        var thumb = Bars.Thumb(driver);
        var track = thumb.Parent!.Parent!;
        var length = thumb.ViewBounds.Height;

        var held = driver.Hold(thumb);
        held.MoveBy(new Vector2(0f, 30f));
        held.Release();

        await Assert.That(track.ViewBounds.Height).IsEqualTo(200f);
        await Assert.That(length).IsEqualTo(50f);
        await Assert
            .That(owner.Log)
            .IsEquivalentTo([new ScrollbarChange(60f, ScrollbarStep.Thumb)]);
        await Assert.That(thumb.ViewBounds.Y - track.ViewBounds.Y).IsEqualTo(30f);
    }

    [Test]
    public async Task Each_move_is_reported_with_its_kind_and_target_and_shows_only_once_the_owner_takes_it()
    {
        var owner = new BarOwner();
        using var driver = owner.Render(value: 50f, max: 300f, viewport: 100f, height: 200f);

        driver.Click(Bars.Page(driver, forward: false));
        var paged = Bars.Share(driver);
        var held = driver.Hold(Bars.Thumb(driver));
        held.MoveBy(new Vector2(0f, -20f));
        held.Release();
        owner.Applies = false;
        driver.Click(Bars.Page(driver, forward: true));

        await Assert.That(paged).IsEqualTo(0f);
        await Assert
            .That(owner.Log)
            .IsEquivalentTo([
                new ScrollbarChange(0f, ScrollbarStep.PageDecrease),
                new ScrollbarChange(100f, ScrollbarStep.PageIncrease),
            ]);
        await Assert.That(Bars.Share(driver)).IsEqualTo(0f);
    }

    [Test]
    public async Task A_held_arrow_steps_a_line_as_it_lands_then_every_50_ms_after_250_ms_while_over_it_and_leaves_the_focus()
    {
        var owner = new BarOwner();
        using var driver = owner.Render(
            value: 500f,
            max: 1000f,
            viewport: 100f,
            height: 200f,
            focusable: true
        );
        driver.Focus(driver.Get("before"));
        var down = Bars.Arrow(driver, forward: true);

        var held = driver.Hold(down, settle: false);
        var landed = owner.Log.Count;
        driver.Frame(0.2f);
        var waiting = owner.Log.Count;
        driver.Frame(0.06f);
        driver.Frame(0.05f);
        var repeating = owner.Log.Count;
        held.MoveTo(driver.Get("before"), settle: false);
        driver.Frame(0.05f);
        driver.Frame(0.05f);
        var away = owner.Log.Count;
        held.Release(settle: false);
        driver.Frame(0.3f);
        driver.Click(Bars.Arrow(driver, forward: false));

        await Assert.That((landed, waiting, repeating, away)).IsEqualTo((1, 1, 3, 3));
        await Assert.That(owner.Log.Count).IsEqualTo(4);
        await Assert
            .That(owner.Log[0])
            .IsEqualTo(new ScrollbarChange(516f, ScrollbarStep.LineIncrease));
        await Assert.That(owner.Log[3].Step).IsEqualTo(ScrollbarStep.LineDecrease);
        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("before"));
    }

    [Test]
    public async Task It_steps_from_its_value_clamped_into_the_range_and_a_maximum_below_the_minimum_counts_as_the_minimum()
    {
        var owner = new BarOwner { LineStep = 1f };
        using var driver = owner.Render(value: 20f, max: 10f, viewport: 1f, height: 200f);
        var steps = new List<float>();

        void StepUp()
        {
            driver.Click(Bars.Arrow(driver, forward: false));
            steps.Add(owner.Log[^1].Value);
        }

        owner.Applies = false;
        StepUp();
        owner.Max = 5f;
        driver.Rerender();
        StepUp();
        owner.Value.Value = 3f;
        driver.Settle();
        StepUp();
        owner.Min = 8f;
        driver.Rerender();
        StepUp();

        await Assert.That(steps).IsEquivalentTo([9f, 4f, 2f, 8f]);
        await Assert.That(driver.All<ScrollbarThumb>().Count).IsEqualTo(0);
    }

    [Test]
    public async Task On_a_long_extent_the_thumb_keeps_the_extra_small_height_and_a_drag_past_the_end_lands_flush()
    {
        var owner = new BarOwner();
        using var driver = owner.Render(value: 0f, max: 100000f, viewport: 20f, height: 160f);
        var thumb = Bars.Thumb(driver);
        var track = thumb.Parent!.Parent!;
        var length = thumb.ViewBounds.Height;

        var held = driver.Hold(thumb);
        held.MoveBy(new Vector2(0f, 400f));
        var lit = thumb.Visual.Background;
        held.Release();

        await Assert.That(length).IsEqualTo(owner.Shortest);
        await Assert.That(lit).IsEqualTo((Paint)Themes.Dark.Color(ColorToken.IconHover));
        await Assert.That(owner.Value.Peek).IsEqualTo(100000f);
        await Assert
            .That(MathF.Abs(thumb.ViewBounds.Bottom - track.ViewBounds.Bottom))
            .IsLessThanOrEqualTo(0.5f);
    }

    [Test]
    public async Task On_a_track_shorter_than_the_shortest_thumb_the_thumb_takes_the_viewports_share()
    {
        var owner = new BarOwner();
        using var driver = owner.Render(value: 0f, max: 100f, viewport: 100f, height: 40f);
        var thumb = Bars.Thumb(driver);

        await Assert
            .That(thumb.ViewBounds.Height)
            .IsEqualTo(thumb.Parent!.Parent!.ViewBounds.Height / 2f);
    }
}

/// <summary>Owns a scroll bar's value, taking each move it reports while <see cref="Applies"/>.</summary>
internal sealed class BarOwner
{
    public readonly List<ScrollbarChange> Log = [];
    public State<float> Value = null!;
    public bool Applies = true;
    public float Min;
    public float Max;
    public float? LineStep;
    public float Shortest;

    public UiDriver Render(
        float value,
        float max,
        float viewport,
        float height,
        bool focusable = false
    )
    {
        Max = max;
        return UiDriver.Render(ui =>
        {
            Value = ui.State(value);
            Shortest = ui.Theme.Metrics(ComponentSize.Xs).Height;
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                if (focusable)
                    using (ui.Box(width: 40, height: 20, focusable: true, id: "before")) { }
                ui.Scrollbar(
                    value: Value.Value,
                    onValueChange: Callback<ScrollbarChange>.Of(
                        this,
                        static (o, change) =>
                        {
                            o.Log.Add(change);
                            if (o.Applies)
                                o.Value.Value = change.Value;
                        }
                    ),
                    min: Min,
                    max: Max,
                    viewportSize: viewport,
                    lineStep: LineStep,
                    height: height
                );
            }
        });
    }
}

internal static class Bars
{
    public static Node Root(Node component)
    {
        foreach (var child in component.Children)
            return child;
        throw new InvalidOperationException($"{component.Component?.Name} rendered nothing.");
    }

    public static int Shown(UiDriver driver) =>
        driver.All<Scrollbar>().Count(bar => !Root(bar).Place.Collapsed);

    public static Node Thumb(UiDriver driver, int bar = 0) =>
        Root(driver.All<ScrollbarThumb>()[bar]);

    public static Node Arrow(UiDriver driver, bool forward, int bar = 0) =>
        Root(driver.All<ScrollbarArrow>()[bar * 2 + (forward ? 1 : 0)]);

    public static Node Track(UiDriver driver, int bar = 0)
    {
        var arrow = driver.All<ScrollbarArrow>()[bar * 2];
        var next = false;
        foreach (var child in arrow.Parent!.Children)
        {
            if (next)
                return child;
            next = child == arrow;
        }
        throw new InvalidOperationException("No track.");
    }

    public static Node Page(UiDriver driver, bool forward, int bar = 0)
    {
        Node? last = null;
        var index = 0;
        foreach (var child in Track(driver, bar).Children)
        {
            if (index++ == 0 && !forward)
                return child;
            last = child;
        }
        return last!;
    }

    /// <summary>How far down its track the thumb sits, as a share of the room it moves in.</summary>
    public static float Share(UiDriver driver, int bar = 0)
    {
        var thumb = Thumb(driver, bar);
        var track = thumb.Parent!.Parent!.ViewBounds;
        var room = track.Height - thumb.ViewBounds.Height;
        return room > 0f ? (thumb.ViewBounds.Y - track.Y) / room : 0f;
    }
}

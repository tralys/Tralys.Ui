using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class SliderTests
{
    [Test]
    public async Task A_press_on_the_track_steps_a_page_toward_it_either_side_and_repeats_while_held()
    {
        var owner = new SliderOwner { PageStep = 2f };
        using var driver = owner.Render(5f, width: 216f);
        var track = Sliders.Track(driver);

        driver.Click(track, new Vector2(190f, 10f));
        var right = owner.Value.Peek;
        driver.Click(track, new Vector2(10f, 10f));
        var left = owner.Value.Peek;
        owner.Value.Value = 1f;
        driver.Settle();
        var held = driver.Hold(track, settle: false);
        var landed = owner.Value.Peek;
        driver.Frame(0.2f);
        var waiting = owner.Value.Peek;
        driver.Frame(0.06f);
        driver.Frame(0.04f);
        var repeated = owner.Value.Peek;
        held.Release(settle: false);
        driver.Frame(0.3f);

        await Assert.That((right, left)).IsEqualTo((7f, 5f));
        await Assert.That((landed, waiting, repeated)).IsEqualTo((3f, 3f, 5f));
        await Assert.That(owner.Value.Peek).IsEqualTo(5f);
        await Assert.That(driver.Focused).IsSameReferenceAs(Sliders.Root(driver));
    }

    [Test]
    public async Task A_held_press_stops_stepping_once_the_thumb_reaches_the_pointer()
    {
        var owner = new SliderOwner { PageStep = 1f };
        using var driver = owner.Render(0f, width: 216f);
        var track = Sliders.Track(driver);

        var held = driver.Hold(track, settle: false);
        held.MoveBy(new Vector2(30f, 0f), settle: false);
        for (var i = 0; i < 30; i++)
            driver.Frame(0.1f);
        held.Release(settle: false);
        driver.Frame();

        await Assert.That(owner.Value.Peek).IsEqualTo(7f);
    }

    [Test]
    public async Task Its_keys_step_by_step_and_page_reach_its_ends_and_turn_round_when_inverted()
    {
        var owner = new SliderOwner { Step = 1f, PageStep = 3f };
        using var driver = owner.Render(5f);
        driver.Focus(Sliders.Root(driver));
        var shown = new List<float>();
        foreach (
            var key in (Key[])
                [Key.Right, Key.Up, Key.Left, Key.Down, Key.PageUp, Key.PageDown, Key.End, Key.Home]
        )
        {
            driver.Press(key);
            shown.Add(owner.Value.Peek);
        }
        var ctrl = driver.KeyDown(Key.Right, ModifierKeys.Control);
        driver.KeyUp(Key.Right, ModifierKeys.Control);
        var afterCtrl = owner.Value.Peek;
        owner.Inverted = true;
        owner.Value.Value = 5f;
        driver.Rerender();
        driver.Press(Key.Right);
        var invertedRight = owner.Value.Peek;
        driver.Press(Key.PageUp);

        await Assert.That(shown).IsEquivalentTo([6f, 7f, 6f, 5f, 8f, 5f, 10f, 0f]);
        await Assert.That((ctrl, afterCtrl)).IsEqualTo((false, 0f));
        await Assert.That((invertedRight, owner.Value.Peek)).IsEqualTo((4f, 1f));
    }

    [Test]
    public async Task A_snapping_step_lands_on_the_next_tick_its_way_and_with_no_frequency_on_an_end()
    {
        static float Step(float at, float frequency, float step, Key key)
        {
            var owner = new SliderOwner
            {
                Step = step,
                TickFrequency = frequency,
                Snap = true,
            };
            using var driver = owner.Render(at);
            driver.Focus(Sliders.Root(driver));
            driver.Press(key);
            return owner.Value.Peek;
        }

        await Assert.That(Step(0f, 2f, 0.5f, Key.Right)).IsEqualTo(2f);
        await Assert.That(Step(2f, 2f, 0.5f, Key.Left)).IsEqualTo(0f);
        await Assert.That(Step(3.4f, 1f, 0.1f, Key.Right)).IsEqualTo(4f);
        await Assert.That(Step(3.6f, 1f, 0.1f, Key.Left)).IsEqualTo(3f);
        await Assert.That(Step(10f, 3f, 0.1f, Key.Left)).IsEqualTo(9f);
        await Assert.That(Step(4f, 0f, 0.1f, Key.Right)).IsEqualTo(10f);
        await Assert.That(Step(4f, 0f, 0.1f, Key.Left)).IsEqualTo(0f);
    }

    [Test]
    public async Task It_shows_a_given_value_unsnapped_and_clamped_into_its_range()
    {
        var owner = new SliderOwner { TickFrequency = 1f, Snap = true };
        using var driver = owner.Render(3.3f, width: 216f);
        var unsnapped = Sliders.ThumbShare(driver);
        owner.Value.Value = 20f;
        driver.Settle();
        var past = Sliders.ThumbShare(driver);
        owner.Max = 30f;
        driver.Rerender();
        var widened = Sliders.ThumbShare(driver);
        owner.Min = 4f;
        owner.Max = 2f;
        driver.Rerender();

        await Assert.That(unsnapped).IsEqualTo(0.33f).Within(0.005f);
        await Assert.That(past).IsEqualTo(1f);
        await Assert.That(widened).IsEqualTo(20f / 30f).Within(0.005f);
        await Assert.That(Sliders.ThumbShare(driver)).IsEqualTo(0f);
    }

    [Test]
    public async Task Dragging_the_thumb_moves_the_value_by_the_tracks_scale_snapping_to_ticks_and_lights_the_thumb()
    {
        var owner = new SliderOwner { TickFrequency = 1f, Snap = true };
        using var driver = owner.Render(2f, width: 216f);
        var thumb = Sliders.Thumb(driver);

        var held = driver.Hold(thumb);
        var dragging = thumb.Visual.Background;
        held.MoveBy(new Vector2(47f, 0f));
        var moved = owner.Value.Peek;
        held.MoveBy(new Vector2(500f, 0f));
        held.Release();

        await Assert.That(dragging).IsEqualTo(Themes.Dark.Color(ColorToken.FgAccent));
        await Assert.That(moved).IsEqualTo(4f);
        await Assert.That(owner.Value.Peek).IsEqualTo(10f);
        await Assert.That(owner.Ends).IsEquivalentTo([false]);
        await Assert.That(driver.Focused).IsSameReferenceAs(Sliders.Root(driver));
    }

    [Test]
    public async Task An_inverted_slider_runs_its_thumb_and_fill_from_the_right()
    {
        var owner = new SliderOwner { Inverted = true };
        using var driver = owner.Render(0f, width: 216f);
        var track = Sliders.Track(driver).ViewBounds;
        var empty = Sliders.Fill(driver).ViewBounds;
        var at = Sliders.ThumbCentre(driver);
        owner.Value.Value = 2.5f;
        driver.Settle();
        var fill = Sliders.Fill(driver).ViewBounds;

        await Assert.That(at.X).IsEqualTo(track.Right);
        await Assert.That(empty.Width).IsEqualTo(0f);
        await Assert.That((fill.Right, fill.Width)).IsEqualTo((track.Right, 50f));
    }

    [Test]
    public async Task Its_ticks_run_from_the_minimum_three_quarters_deep_between_full_ends_and_never_closer_than_a_pixel()
    {
        var owner = new SliderOwner
        {
            TickFrequency = 3f,
            Ticks = TickPlacement.TopLeft,
            Vertical = true,
        };
        using var driver = owner.Render(0f, height: 216f);
        var ticks = Ticks(driver);
        owner.Vertical = false;
        owner.Max = 1000f;
        owner.TickFrequency = 1f;
        driver.Rerender();
        var dense = Ticks(driver).Count;

        await Assert
            .That(ticks)
            .IsEquivalentTo(["4,200 0,200", "4,0 0,0", "4,140 1,140", "4,80 1,80", "4,20 1,20"]);
        await Assert.That(dense).IsBetween(150, 201);
    }

    [Test]
    public async Task Its_skin_art_lights_under_the_pointer_and_fades_disabled_and_its_ring_shows_only_when_tabbed_to()
    {
        var owner = new SliderOwner { Focusable = true };
        using var driver = owner.Render(0f, width: 216f);
        driver.View.Context.Skin = new Skin([
            new(SkinPart.SliderThumb, new SkinPartImage(Art, default)),
            new(SkinPart.SliderThumbHover, new SkinPartImage(Art, default)),
        ]);
        driver.Settle();
        float Lit() => Sliders.Kid(Sliders.Thumb(driver), 1).Visual.Opacity ?? 1f;
        bool Ringed() =>
            Sliders.Kids(Sliders.Root(driver)).Any(static c => c.Layout.Border.Left > 0f);

        var resting = Lit();
        driver.Focus(driver.Get("before"));
        driver.Tab();
        var tabbed = Ringed();
        driver.Hover(Sliders.Track(driver));
        var hovered = Lit();
        driver.Click(Sliders.Track(driver));
        var clicked = Ringed();
        driver.Unhover();
        var left = Lit();
        owner.Disabled = true;
        driver.Rerender();

        await Assert.That((resting, hovered, left)).IsEqualTo((0f, 1f, 0f));
        await Assert.That((tabbed, clicked)).IsEqualTo((true, false));
        await Assert.That(Sliders.Kid(Sliders.Thumb(driver), 0).Visual.Opacity).IsEqualTo(0.5f);
    }

    [Test]
    public async Task A_thumb_dragged_as_its_rows_item_leaves_lets_go_and_reports_nothing_more()
    {
        var log = new List<float>();
        var rows = new RowItems { Items = ItemsSource.From(new[] { 1, 2, 3 }) };
        using var driver = UiDriver.Render(ui =>
            ui.VirtualList(
                items: rows.Items,
                itemKey: static item => item,
                row: Slot<int>.Of(
                    log,
                    static (ui, log, item, _) =>
                        ui.Slider(
                            value: 0f,
                            onValueChange: Callback<float>.Of(
                                log,
                                new Boxed<int>(item),
                                static (l, i, v) => l.Add(i.Value * 100 + v)
                            )
                        )
                ),
                rowHeight: 24,
                width: 216,
                height: 120
            )
        );

        var thumb = Nodes.Find(driver.All<SliderThumb>()[0], NodeKind.Box);
        var held = driver.Hold(thumb);
        held.MoveBy(new Vector2(40f, 0f));
        rows.Items = ItemsSource.From(new[] { 4, 5, 6 });
        driver.Rerender();
        held.MoveBy(new Vector2(40f, 0f));
        held.Release();

        await Assert.That(log).IsEquivalentTo([102f]);
    }

    static readonly TestTexture Art = new(14, 14);

    static List<string> Ticks(UiDriver driver)
    {
        var ticks = new List<string>();
        foreach (var line in driver.Draw().Lines)
        {
            var at = line.IndexOf(": line ", StringComparison.Ordinal);
            if (at >= 0)
                ticks.Add(line[(at + 7)..line.IndexOf(" width", StringComparison.Ordinal)]);
        }
        return ticks;
    }

    sealed class RowItems
    {
        public IItemsSource<int> Items = null!;
    }
}

internal sealed class SliderOwner
{
    public State<float> Value = null!;
    public readonly List<bool> Ends = [];
    public float Min;
    public float? Max;
    public float? Step;
    public float? PageStep;
    public float? TickFrequency;
    public TickPlacement Ticks;
    public bool Snap;
    public bool Inverted;
    public bool Vertical;
    public bool Disabled;
    public bool Focusable;

    public UiDriver Render(float value, float width = 216f, float height = 200f) =>
        UiDriver.Render(ui =>
        {
            Value = ui.State(value);
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                if (Focusable)
                    using (ui.Box(width: 40, height: 20, focusable: true, id: "before")) { }
                ui.Slider(
                    value: Value.Value,
                    onValueChange: Value.Setter,
                    min: Min,
                    max: Max,
                    step: Step,
                    pageStep: PageStep,
                    tickFrequency: TickFrequency,
                    ticks: Ticks,
                    snapToTicks: Snap,
                    inverted: Inverted,
                    orientation: Vertical ? Orientation.Vertical : Orientation.Horizontal,
                    width: width,
                    height: height,
                    disabled: Disabled,
                    onDragEnd: Callback<bool>.Of(Ends, static (e, cancelled) => e.Add(cancelled))
                );
            }
        });
}

internal static class Sliders
{
    public static Node Root(UiDriver driver) => Nodes.Find(driver.Get<Slider>(), NodeKind.Box);

    public static Node Track(UiDriver driver) => Kid(Root(driver), 0);

    public static Node Fill(UiDriver driver) => Kid(Track(driver), 1);

    public static List<Node> Kids(Node node)
    {
        var kids = new List<Node>();
        foreach (var child in node.Children)
            kids.Add(child);
        return kids;
    }

    public static Node Kid(Node node, int index) => Kids(node)[index];

    public static float Value(UiDriver driver) =>
        ((ComponentHost<SliderProps>)driver.Get<Slider>().Host!).Value.Value;

    public static Node Thumb(UiDriver driver) =>
        Nodes.Find(driver.Get<SliderThumb>(), NodeKind.Box);

    public static Vector2 ThumbCentre(UiDriver driver)
    {
        var bounds = Thumb(driver).ViewBounds;
        return new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
    }

    public static float ThumbShare(UiDriver driver)
    {
        var track = Track(driver).ViewBounds;
        return (ThumbCentre(driver).X - track.X) / track.Width;
    }
}

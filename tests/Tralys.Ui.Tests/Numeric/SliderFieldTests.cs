using System.Numerics;
using Tralys.Ui.Components;

namespace Tralys.Ui.Tests;

public sealed class SliderFieldTests
{
    [Test]
    public async Task A_dragged_thumb_reports_its_steps_as_it_goes_with_the_readout_following_and_stops_at_the_maximum()
    {
        var owner = new NumberOwner();
        var step = 5.0;
        using var driver = owner.Render(
            0.0,
            (ui, o) =>
                ui.SliderField(
                    label: "Volume",
                    value: o.Shown,
                    onValueChange: o.Set,
                    step: step,
                    id: "field"
                ),
            width: 360f
        );

        var held = driver.Hold(Sliders.Thumb(driver));
        held.MoveBy(new Vector2(37f, 0f));
        var dragged = (owner.Value.Peek, Numbers.Shown(driver), Sliders.Value(driver));
        held.Release();
        step = 1.0;
        driver.Rerender();
        held = driver.Hold(Sliders.Thumb(driver));
        held.MoveBy(new Vector2(13f, 0f), steps: 5);
        held.MoveBy(new Vector2(900f, 0f));
        held.Release();

        await Assert.That(dragged.Item1 % 5.0).IsEqualTo(0.0);
        await Assert.That(dragged.Item1).IsGreaterThan(0.0);
        await Assert
            .That((dragged.Item2, dragged.Item3))
            .IsEqualTo((dragged.Item1.ToString(), (float)dragged.Item1));
        await Assert.That(owner.Reports.Where(static v => v != Math.Round(v))).IsEmpty();
        await Assert.That(owner.Reports[^1]).IsEqualTo(100.0);
    }

    [Test]
    public async Task A_number_typed_into_its_readout_settles_there_and_moves_its_slider_and_a_given_value_keeps_its_precision()
    {
        var owner = new NumberOwner();
        using var driver = owner.Render(
            0.123,
            static (ui, o) =>
                ui.SliderField(
                    label: "Depth",
                    value: o.Shown,
                    onValueChange: o.Set,
                    max: 1,
                    step: 0.01,
                    mode: NumberMode.Decimal,
                    decimalPlaces: 3,
                    id: "field"
                ),
            width: 360f
        );
        var given = (owner.Reports.Count, Sliders.Value(driver));
        driver.Focus(Numbers.Text(driver));
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type(0.42.ToString(System.Globalization.CultureInfo.CurrentCulture));
        driver.Press(Key.Enter);
        var typed = Sliders.Value(driver);
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("150");
        driver.Press(Key.Enter);

        await Assert.That(given).IsEqualTo((0, 0.123f));
        await Assert.That(typed).IsEqualTo(0.42f);
        await Assert.That(owner.Reports).IsEquivalentTo([0.42, 150.0, 1.0]);
    }

    [Test]
    public async Task A_press_on_the_track_moves_the_value_to_the_pointer_and_a_drag_from_there_follows_it()
    {
        var owner = new NumberOwner();
        using var driver = owner.Render(
            0.0,
            static (ui, o) =>
                ui.SliderField(label: "Volume", value: o.Shown, onValueChange: o.Set, id: "field"),
            width: 360f
        );
        var track = Sliders.Track(driver).ViewBounds;

        var held = driver.Hold(
            new Vector2(track.X + track.Width * 0.25f, track.Y + track.Height / 2f)
        );
        var pressed = owner.Value.Peek;
        held.MoveBy(new Vector2(track.Width * 0.5f, 0f));
        var dragged = owner.Value.Peek;
        held.Release();

        await Assert.That((pressed, dragged)).IsEqualTo((25.0, 75.0));
    }

    [Test]
    public async Task Committing_on_release_holds_a_dragged_value_back_until_the_thumb_lets_go_and_drops_it_when_the_drag_is_cancelled()
    {
        var owner = new NumberOwner();
        using var driver = owner.Render(
            0.0,
            static (ui, o) =>
                ui.SliderField(
                    label: "Scale",
                    value: o.Shown,
                    onValueChange: o.Set,
                    commitOnRelease: true,
                    id: "field"
                ),
            width: 360f
        );

        var held = driver.Hold(Sliders.Thumb(driver));
        held.MoveBy(new Vector2(60f, 0f));
        var dragged = (owner.Reports.Count, Sliders.Value(driver), Numbers.Shown(driver));
        held.Release();
        var released = owner.Reports.ToList();
        held = driver.Hold(Sliders.Thumb(driver));
        held.MoveBy(new Vector2(60f, 0f));
        held.Cancel();

        await Assert.That((dragged.Item1, dragged.Item3)).IsEqualTo((0, "0"));
        await Assert.That(dragged.Item2).IsGreaterThan(0f);
        await Assert.That(released).IsEquivalentTo([(double)dragged.Item2]);
        await Assert.That(owner.Reports).IsEquivalentTo(released);
        await Assert.That(Sliders.Value(driver)).IsEqualTo((float)released[0]);
    }
}

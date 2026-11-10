using Tralys.Ui.Components;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class NumberPadTests
{
    [Test]
    public async Task Its_keys_clear_append_step_within_bounds_and_drop_a_digit_reporting_each_and_its_tick_lets_the_focus_go()
    {
        var owner = new NumberOwner { Max = 500 };
        using var driver = owner.Render(
            7.0,
            static (ui, o) =>
                ui.NumberInput(
                    value: o.Shown,
                    onValueChange: o.Set,
                    max: o.Max,
                    width: 200,
                    id: "field"
                ),
            Density.Touch
        );
        driver.Tap(Numbers.Text(driver));

        foreach (
            var key in (NumberPadKeyKind[])
                [
                    NumberPadKeyKind.Clear,
                    NumberPadKeyKind.Down,
                    NumberPadKeyKind.Digit4,
                    NumberPadKeyKind.Digit2,
                    NumberPadKeyKind.Up,
                    NumberPadKeyKind.Back,
                    NumberPadKeyKind.Digit9,
                    NumberPadKeyKind.Digit9,
                ]
        )
            driver.Tap(Pads.Key(driver, key));
        var open = (driver.All<NumberPad>().Count, driver.Focused == Numbers.Text(driver));
        driver.Tap(Pads.Key(driver, NumberPadKeyKind.Done));

        await Assert.That(owner.Reports).IsEquivalentTo([0.0, 4.0, 42.0, 43.0, 4.0, 49.0, 499.0]);
        await Assert.That(open).IsEqualTo((1, true));
        await Assert.That(driver.Focused).IsNull();
        await Assert.That(driver.All<NumberPad>().Count).IsEqualTo(0);
    }

    [Test]
    public async Task Digits_append_to_what_the_pad_typed_not_to_a_value_the_app_clamped_and_a_step_stays_within_bounds()
    {
        var owner = new NumberOwner { Min = 85, Max = 90 };
        using var driver = owner.Render(
            85.0,
            static (ui, o) =>
                ui.NumberInput(
                    value: o.Shown,
                    onValueChange: Callback<double>.Of(
                        o,
                        static (o, v) =>
                        {
                            o.Reports.Add(v);
                            o.Value.Value = Math.Clamp(v, o.Min, o.Max!.Value);
                        }
                    ),
                    max: o.Max,
                    width: 200,
                    id: "field"
                ),
            Density.Touch
        );
        driver.Tap(Numbers.Text(driver));

        driver.Tap(Pads.Key(driver, NumberPadKeyKind.Clear));
        driver.Tap(Pads.Key(driver, NumberPadKeyKind.Digit8));
        driver.Tap(Pads.Key(driver, NumberPadKeyKind.Digit8));
        var typed = owner.Value.Peek;
        driver.Tap(Pads.Key(driver, NumberPadKeyKind.Digit7));

        await Assert.That(typed).IsEqualTo(88.0);
        await Assert.That(owner.Reports).IsEquivalentTo([0.0, 8.0, 88.0, 90.0]);
    }

    [Test]
    public async Task A_value_past_a_whole_numbers_range_stays_as_a_step_cannot_move_it()
    {
        var owner = new NumberOwner();
        using var driver = owner.Render(
            1e30,
            static (ui, o) =>
                ui.NumberInput(value: o.Shown, onValueChange: o.Set, width: 200, id: "field"),
            Density.Touch
        );
        driver.Tap(Numbers.Text(driver));

        driver.Tap(Pads.Key(driver, NumberPadKeyKind.Up));

        await Assert.That(owner.Reports).IsEmpty();
        await Assert.That(owner.Value.Peek).IsEqualTo(1e30);
    }

    [Test]
    public async Task A_number_field_shows_its_steps_a_money_field_its_scales_and_max_shows_only_for_a_bound_of_its_own()
    {
        static List<NumberPadKeyKind> Keys(Action<Ui> field)
        {
            using var driver = UiDriver.Render(
                ui =>
                {
                    using (ui.Box(direction: Direction.Column, align: Align.Start))
                        field(ui);
                },
                density: Density.Touch
            );
            driver.Tap(Nodes.Find(driver.Get("field"), NodeKind.EditableText));
            return Pads.Shown(driver);
        }

        var number = Keys(static ui =>
            ui.NumberInput(value: 1, max: 5_000_000_000, width: 200, id: "field")
        );
        var unbounded = Keys(static ui => ui.NumberInput(value: 1, width: 200, id: "field"));
        var money = Keys(static ui => ui.MoneyInput(value: 1, width: 200, id: "field"));

        await Assert
            .That(number)
            .Contains(NumberPadKeyKind.Down)
            .And.Contains(NumberPadKeyKind.Up)
            .And.Contains(NumberPadKeyKind.Max)
            .And.DoesNotContain(NumberPadKeyKind.K);
        await Assert.That(unbounded).DoesNotContain(NumberPadKeyKind.Max);
        await Assert
            .That(money)
            .Contains(NumberPadKeyKind.K)
            .And.Contains(NumberPadKeyKind.M)
            .And.Contains(NumberPadKeyKind.B)
            .And.DoesNotContain(NumberPadKeyKind.Up)
            .And.DoesNotContain(NumberPadKeyKind.Max);
    }

    [Test]
    public async Task A_money_fields_pad_scales_an_amount_to_its_bound_and_opens_right_of_the_field()
    {
        var owner = new MoneyOwner { Max = 10_000 };
        using var driver = owner.Render(
            0,
            static (ui, o) =>
                ui.MoneyInput(
                    value: o.Shown,
                    onValueChange: o.Set,
                    max: o.Max,
                    width: 200,
                    id: "field"
                ),
            Density.Touch
        );
        driver.Tap(Numbers.Text(driver));
        var keyboard = driver.TextInput;

        driver.Tap(Pads.Key(driver, NumberPadKeyKind.Digit5));
        driver.Tap(Pads.Key(driver, NumberPadKeyKind.K));
        driver.Tap(Pads.Key(driver, NumberPadKeyKind.K));
        var field = driver.Get("field").ViewBounds;
        var pad = Nodes.Find(driver.Get<NumberPad>(), NodeKind.Box).ViewBounds;

        await Assert.That(keyboard).IsNull();
        await Assert.That(owner.Reports).IsEquivalentTo([5L, 5000L, 10_000L]);
        await Assert.That(driver.All<NumberPad>().Count).IsEqualTo(1);
        await Assert.That(pad.X).IsEqualTo(field.Right);
    }
}

internal static class Pads
{
    public static Node Key(UiDriver driver, NumberPadKeyKind kind)
    {
        foreach (var key in driver.All<NumberPad.Key>())
        {
            if (((ComponentHost<NumberPadKeyProps>)key.Host!).Value.Kind == kind)
                return key;
        }
        throw new InvalidOperationException($"No {kind} key.");
    }

    public static List<NumberPadKeyKind> Shown(UiDriver driver)
    {
        var shown = new List<NumberPadKeyKind>();
        foreach (var key in driver.All<NumberPad.Key>())
            shown.Add(((ComponentHost<NumberPadKeyProps>)key.Host!).Value.Kind);
        return shown;
    }
}

using System.Globalization;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class NumberInputTests
{
    static CultureInfo Culture => CultureInfo.CurrentCulture;

    static string Grouped(double value) => value.ToString("N0", Culture);

    [Test]
    public async Task Each_edit_reports_the_number_it_reads_grouped_as_typed_and_half_typed_text_stands_until_it_settles()
    {
        var owner = new NumberOwner();
        using var driver = owner.Render(
            5.0,
            static (ui, o) =>
                ui.NumberInput(value: o.Shown, onValueChange: o.Set, width: 200, id: "field")
        );
        driver.Focus(Numbers.Text(driver));
        driver.Press(Key.A, ModifierKeys.Control);

        driver.Type("-");
        var minus = (owner.Reports.Count, Numbers.Shown(driver));
        driver.Press(Key.BackSpace);
        var typed = new List<string>();
        foreach (var digit in (string[])["1", "2", "3", "4", "5"])
        {
            driver.Type(digit);
            typed.Add(Numbers.Shown(driver));
        }
        driver.Type("x");
        var halfTyped = (owner.Reports.Count, Numbers.Shown(driver));
        driver.Tab();
        var putBack = Numbers.Shown(driver);
        driver.Focus(Numbers.Text(driver));
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("999");
        driver.Press(Key.Home);
        driver.Press(Key.Right);
        driver.Type("1");

        await Assert.That(minus).IsEqualTo((0, "-"));
        await Assert
            .That(owner.Reports)
            .IsEquivalentTo([0.0, 1.0, 12.0, 123.0, 1234.0, 12345.0, 999.0, 9199.0]);
        await Assert.That(typed).IsEquivalentTo(["1", "12", "123", Grouped(1234), Grouped(12345)]);
        await Assert.That(halfTyped).IsEqualTo((6, Grouped(12345) + "x"));
        await Assert.That(putBack).IsEqualTo(Grouped(12345));
        await Assert.That(Numbers.Shown(driver)).IsEqualTo(Grouped(9199));
        await Assert.That(Numbers.Caret(driver)).IsEqualTo(Grouped(9199).Length - 2);
    }

    [Test]
    public async Task A_capped_field_refuses_a_digit_past_its_length_and_counts_what_it_holds()
    {
        var owner = new NumberOwner { Format = "0" };
        using var driver = owner.Render(
            0.0,
            static (ui, o) =>
                ui.NumberInput(
                    value: o.Shown,
                    onValueChange: o.Set,
                    format: o.Format,
                    maxLength: 4,
                    width: 200,
                    id: "field"
                )
        );
        driver.Focus(Numbers.Text(driver));

        driver.Type("12345");

        await Assert.That((owner.Shown, Numbers.Shown(driver))).IsEqualTo((1234.0, "1234"));
        await Assert.That(driver.GetText("4/4")).IsNotNull();
    }

    [Test]
    public async Task It_commits_the_value_it_settles_to_once_as_the_focus_leaves_and_nothing_for_a_visit_without_a_change()
    {
        var owner = new NumberOwner();
        var commits = new List<double>();
        using var driver = owner.Render(
            5.0,
            (ui, o) =>
                ui.NumberInput(
                    value: o.Shown,
                    onValueChange: o.Set,
                    onCommit: Callback<double>.Of(commits, static (c, v) => c.Add(v)),
                    width: 200,
                    id: "field"
                )
        );
        driver.Focus(Numbers.Text(driver));
        driver.Press(Key.A, ModifierKeys.Control);

        driver.Type("42");
        var typing = commits.Count;
        driver.Focus(driver.Get("after"));
        driver.Focus(Numbers.Text(driver));
        driver.Focus(driver.Get("after"));

        await Assert.That(typing).IsEqualTo(0);
        await Assert.That(commits).IsEquivalentTo([42.0]);
    }

    [Test]
    public async Task A_decimal_or_formatted_number_reported_as_typed_stands_as_typed_until_it_settles()
    {
        var owner = new NumberOwner { Mode = NumberMode.Decimal };
        using var driver = owner.Render(
            0.0,
            static (ui, o) =>
                ui.NumberInput(
                    value: o.Shown,
                    onValueChange: o.Set,
                    mode: o.Mode,
                    decimalPlaces: 2,
                    width: 200,
                    id: "field"
                )
        );
        var typed = 1.5.ToString(Culture);
        driver.Focus(Numbers.Text(driver));

        driver.Type(typed);
        var standing = Numbers.Shown(driver);
        driver.Tab();

        await Assert.That(owner.Reports).IsEquivalentTo([1.5]);
        await Assert.That(standing).IsEqualTo(typed);
        await Assert.That(Numbers.Shown(driver)).IsEqualTo(1.5.ToString("N2", Culture));
    }

    [Test]
    public async Task It_reads_typing_through_its_format_with_or_without_the_formats_own_text_and_refuses_an_infinite_number()
    {
        static double? Read(string? format, NumberMode mode, string typed, double start = 0.0)
        {
            var owner = new NumberOwner { Format = format, Mode = mode };
            using var driver = owner.Render(
                start,
                static (ui, o) =>
                    ui.NumberInput(
                        value: o.Shown,
                        onValueChange: o.Set,
                        mode: o.Mode,
                        format: o.Format,
                        width: 200,
                        id: "field"
                    )
            );
            driver.Focus(Numbers.Text(driver));
            driver.Press(Key.A, ModifierKeys.Control);
            driver.Type(typed);
            driver.Tab();
            return owner.Reports.Count == 0 ? null : owner.Reports[^1];
        }

        await Assert.That(Read(null, NumberMode.Decimal, "1" + new string('0', 400))).IsNull();
        await Assert.That(Read(null, NumberMode.Integer, "nan", 5.0)).IsNull();
        await Assert.That(Read("0.0", NumberMode.Decimal, "nan", 5.0)).IsNull();
        await Assert.That(Read("0.0", NumberMode.Decimal, "1e5")).IsNull();
        await Assert.That(Read("0'%'", NumberMode.Integer, "50%")).IsEqualTo(50.0);
        await Assert.That(Read("0‰", NumberMode.Decimal, "500‰")).IsEqualTo(0.5);
        await Assert
            .That(Read("0.0%", NumberMode.Decimal, 0.5.ToString("0.0%", Culture), 0.42))
            .IsEqualTo(0.5);
        await Assert.That(Read("0' pts'", NumberMode.Integer, "7")).IsEqualTo(7.0);
        await Assert
            .That(Read("P0", NumberMode.Decimal, 0.25.ToString("P0", Culture)))
            .IsEqualTo(0.25);
    }

    [Test]
    public async Task It_settles_into_its_range_and_kind_as_the_focus_leaves_or_on_enter_which_hands_on_the_settled_value()
    {
        var owner = new NumberOwner { Max = 100 };
        using var driver = owner.Render(
            12.7,
            static (ui, o) =>
                ui.NumberInput(
                    value: o.Shown,
                    onValueChange: o.Set,
                    max: o.Max,
                    onSubmit: o.Submit,
                    width: 200,
                    id: "field"
                )
        );
        driver.Focus(Numbers.Text(driver));
        driver.Tab();
        var truncated = owner.Value.Peek;
        driver.Focus(Numbers.Text(driver));
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("500");
        driver.Tab();
        var left = Numbers.Shown(driver);
        driver.Focus(Numbers.Text(driver));
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("250");
        driver.Press(Key.Enter);

        await Assert.That(truncated).IsEqualTo(12.0);
        await Assert.That(owner.Reports).IsEquivalentTo([12.0, 500.0, 100.0, 250.0, 100.0]);
        await Assert.That(left).IsEqualTo("100");
        await Assert.That(owner.Submits).IsEquivalentTo([100.0]);
        await Assert.That(Numbers.Shown(driver)).IsEqualTo("100");
    }

    [Test]
    public async Task An_integer_field_with_fractional_bounds_settles_to_the_whole_numbers_inside_them()
    {
        var owner = new NumberOwner();
        using var driver = owner.Render(
            5.0,
            static (ui, o) =>
                ui.NumberInput(
                    value: o.Shown,
                    onValueChange: o.Set,
                    min: 0.5,
                    max: 7.5,
                    width: 200,
                    id: "field"
                )
        );
        driver.Focus(Numbers.Text(driver));
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("0");
        driver.Tab();
        var low = owner.Value.Peek;
        driver.Focus(Numbers.Text(driver));
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("9");
        driver.Tab();

        await Assert.That((low, owner.Value.Peek)).IsEqualTo((1.0, 7.0));
    }

    [Test]
    public async Task A_minimum_past_the_maximum_wins_and_a_decimal_field_settles_to_its_places()
    {
        var past = new NumberOwner();
        using (
            var driver = past.Render(
                0.0,
                static (ui, o) =>
                    ui.NumberInput(
                        value: o.Shown,
                        onValueChange: o.Set,
                        min: 1,
                        max: 0,
                        width: 200,
                        id: "field"
                    )
            )
        )
        {
            driver.Focus(Numbers.Text(driver));
            driver.Type("5");
            driver.Tab();
        }
        var places = new NumberOwner();
        using var decimals = places.Render(
            0.0,
            static (ui, o) =>
                ui.NumberInput(
                    value: o.Shown,
                    onValueChange: o.Set,
                    mode: NumberMode.Decimal,
                    decimalPlaces: 3,
                    width: 200,
                    id: "field"
                )
        );
        var empty = Numbers.Shown(decimals);
        decimals.Focus(Numbers.Text(decimals));
        decimals.Type(1.5.ToString(Culture));
        decimals.Tab();

        await Assert.That(past.Reports).IsEquivalentTo([5.0, 1.0]);
        await Assert.That(empty).IsEqualTo("");
        await Assert.That(Numbers.Shown(decimals)).IsEqualTo(1.5.ToString("N3", Culture));
    }

    [Test]
    public async Task Up_and_down_step_from_the_typed_number_tenfold_with_shift_within_its_range_but_not_with_ctrl_or_read_only()
    {
        var owner = new NumberOwner { Max = 25 };
        using var driver = owner.Render(
            0.0,
            static (ui, o) =>
                ui.NumberInput(
                    value: o.Shown,
                    onValueChange: o.Set,
                    max: o.Max,
                    readOnly: o.ReadOnly,
                    width: 200,
                    id: "field"
                )
        );
        driver.Focus(Numbers.Text(driver));

        driver.Press(Key.Up);
        for (var i = 0; i < 3; i++)
            driver.Press(Key.Up, ModifierKeys.Shift);
        driver.Press(Key.Down);
        driver.Press(Key.Up, ModifierKeys.Control);
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("12");
        driver.Press(Key.Up);
        var shown = Numbers.Shown(driver);
        owner.ReadOnly = true;
        driver.Rerender();
        var taken = driver.KeyDown(Key.Up);
        driver.KeyUp(Key.Up);

        await Assert.That(owner.Reports).IsEquivalentTo([1.0, 11.0, 21.0, 25.0, 24.0, 12.0, 13.0]);
        await Assert.That(shown).IsEqualTo("13");
        await Assert.That(taken).IsFalse();
    }

    [Test]
    public async Task Its_steppers_step_tenfold_with_shift_disable_at_its_bounds_and_leave_a_read_only_field_alone()
    {
        var owner = new NumberOwner { Max = 12 };
        using var driver = owner.Render(
            0.0,
            static (ui, o) =>
                ui.NumberInput(
                    value: o.Shown,
                    onValueChange: o.Set,
                    max: o.Max,
                    readOnly: o.ReadOnly,
                    steppers: true,
                    width: 200,
                    id: "field"
                )
        );
        Node Down() => driver.All<NumberStepper>()[0];
        Node Up() => driver.All<NumberStepper>()[1];
        bool Off(Node stepper) =>
            (Nodes.Find(stepper, NodeKind.Box).States & InputStates.Disabled) != 0;

        var floor = Off(Down());
        driver.Click(Up());
        driver.Click(Up(), modifiers: ModifierKeys.Shift);
        driver.Click(Up());
        var ceiling = Off(Up());
        driver.Click(Down());
        owner.ReadOnly = true;
        owner.Value.Value = 3;
        driver.Rerender();
        driver.Click(Up());

        await Assert.That((floor, ceiling)).IsEqualTo((true, true));
        await Assert.That(owner.Reports).IsEquivalentTo([1.0, 11.0, 12.0, 11.0]);
        await Assert.That(owner.Value.Peek).IsEqualTo(3.0);
    }

    [Test]
    public async Task A_value_whose_text_runs_past_its_buffer_shows_whole()
    {
        using var driver = UiDriver.Render(ui =>
            ui.NumberInput(value: 1e120, width: 200, id: "field")
        );

        await Assert.That(Numbers.Shown(driver)).IsEqualTo(1e120.ToString("N0", Culture));
        await Assert.That(Numbers.Shown(driver).Length).IsGreaterThan(128);
    }

    [Test]
    public async Task It_asks_for_the_number_keyboard_and_under_touch_a_whole_number_field_opens_its_pad_in_its_place()
    {
        static (TextInputKind? Kind, int Pads) Focused(
            Density density,
            NumberMode mode,
            double min = 0,
            bool readOnly = false
        )
        {
            var owner = new NumberOwner
            {
                Mode = mode,
                Min = min,
                ReadOnly = readOnly,
            };
            using var driver = owner.Render(
                0.0,
                static (ui, o) =>
                    ui.NumberInput(
                        value: o.Shown,
                        onValueChange: o.Set,
                        mode: o.Mode,
                        min: o.Min,
                        readOnly: o.ReadOnly,
                        width: 200,
                        id: "field"
                    ),
                density
            );
            driver.Tap(Numbers.Text(driver));
            return (driver.TextInput?.Kind, driver.All<NumberPad>().Count);
        }

        await Assert
            .That(Focused(Density.Pointer, NumberMode.Integer))
            .IsEqualTo(((TextInputKind?)TextInputKind.Number, 0));
        await Assert
            .That(Focused(Density.Touch, NumberMode.Decimal))
            .IsEqualTo(((TextInputKind?)TextInputKind.Number, 0));
        await Assert
            .That(Focused(Density.Touch, NumberMode.Integer, min: -10))
            .IsEqualTo(((TextInputKind?)TextInputKind.Number, 0));
        await Assert
            .That(Focused(Density.Touch, NumberMode.Integer))
            .IsEqualTo(((TextInputKind?)null, 1));
        await Assert
            .That(Focused(Density.Touch, NumberMode.Integer, readOnly: true))
            .IsEqualTo(((TextInputKind?)null, 0));
    }
}

internal sealed class NumberOwner
{
    public State<double> Value = null!;
    public readonly List<double> Reports = [];
    public readonly List<double> Submits = [];
    public NumberMode Mode;
    public string? Format;
    public double Min;
    public double? Max;
    public bool ReadOnly;

    public double Shown => Value.Value;

    public Callback<double> Set =>
        Callback<double>.Of(
            this,
            static (o, v) =>
            {
                o.Reports.Add(v);
                o.Value.Value = v;
            }
        );

    public Callback<double> Submit => Callback<double>.Of(this, static (o, v) => o.Submits.Add(v));

    public UiDriver Render(
        double start,
        Action<Ui, NumberOwner> field,
        Density density = default,
        float width = 0f
    ) =>
        UiDriver.Render(
            ui =>
            {
                Value = ui.State(start);
                using (
                    ui.Box(
                        direction: Direction.Column,
                        align: width > 0f ? Align.Stretch : Align.Start,
                        gap: 8,
                        width: width > 0f ? width : Length.Auto
                    )
                )
                {
                    field(ui, this);
                    using (ui.Box(width: 40, height: 20, focusable: true, id: "after")) { }
                }
            },
            density: density
        );
}

internal static class Numbers
{
    public static Node Text(UiDriver driver, string id = "field") =>
        Nodes.Find(driver.Get(id), NodeKind.EditableText);

    public static string Shown(UiDriver driver, string id = "field") =>
        Text(driver, id).Text.ToString();

    public static int Caret(UiDriver driver, string id = "field") =>
        ((EditorPayload)Text(driver, id).Payload!).Selection.Caret;
}

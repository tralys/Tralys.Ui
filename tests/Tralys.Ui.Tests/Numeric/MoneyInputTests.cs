using System.Globalization;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class MoneyInputTests
{
    static string Grouped(long amount) => amount.ToString("N0", CultureInfo.CurrentCulture);

    [Test]
    public async Task An_amount_settles_unsigned_and_grouped_no_number_puts_it_back_and_emptied_it_reads_as_zero_shown_empty()
    {
        var owner = new MoneyOwner();
        using var driver = owner.Render(
            0,
            static (ui, o) =>
                ui.MoneyInput(value: o.Shown, onValueChange: o.Set, width: 200, id: "field")
        );

        void Retype(string text, bool paste = false)
        {
            driver.Focus(Numbers.Text(driver));
            driver.Press(Key.A, ModifierKeys.Control);
            if (paste)
            {
                driver.Clipboard = text;
                driver.Press(Key.V, ModifierKeys.Control);
            }
            else if (text.Length == 0)
                driver.Press(Key.BackSpace);
            else
                driver.Type(text);
            driver.Tab();
        }

        Retype("-1234");
        var unsigned = Numbers.Shown(driver);
        Retype("x");
        var kept = Numbers.Shown(driver);
        Retype(Grouped(5678), paste: true);
        Retype("-9223372036854775808");
        Retype("");
        var emptied = Numbers.Shown(driver);
        owner.Value.Value = 9876543;
        driver.Settle();

        await Assert.That((unsigned, kept)).IsEqualTo((Grouped(1234), Grouped(1234)));
        await Assert.That(owner.Reports).IsEquivalentTo([1234L, 5678L, long.MaxValue, 0L]);
        await Assert.That(emptied).IsEqualTo("");
        await Assert.That(Numbers.Shown(driver)).IsEqualTo(Grouped(9876543));
    }

    [Test]
    public async Task Each_edit_reports_the_amount_regrouped_as_typed_the_caret_kept_from_the_end_and_no_number_puts_it_back()
    {
        var owner = new MoneyOwner();
        using var driver = owner.Render(
            0,
            static (ui, o) =>
                ui.MoneyInput(value: o.Shown, onValueChange: o.Set, width: 200, id: "field")
        );
        driver.Focus(Numbers.Text(driver));

        driver.Type("-1234");
        var unsigned = Numbers.Shown(driver);
        driver.Type("x");
        var putBack = Numbers.Shown(driver);
        driver.Press(Key.Home);
        driver.Press(Key.Right);
        driver.Type("9");
        var regrouped = (Numbers.Shown(driver), Numbers.Caret(driver));
        driver.Press(Key.End);
        driver.Press(Key.BackSpace);

        await Assert.That(owner.Reports).IsEquivalentTo([1234L, 19234L, 1923L]);
        await Assert.That((unsigned, putBack)).IsEqualTo((Grouped(1234), Grouped(1234)));
        await Assert.That(regrouped).IsEqualTo((Grouped(19234), 2));
        await Assert.That(Numbers.Shown(driver)).IsEqualTo(Grouped(1923));
    }

    [Test]
    public async Task It_asks_for_the_number_keyboard_and_settles_into_its_range_as_the_focus_leaves_or_on_enter()
    {
        var owner = new MoneyOwner { Min = 100, Max = 1000 };
        using var driver = owner.Render(
            500,
            static (ui, o) =>
                ui.MoneyInput(
                    value: o.Shown,
                    onValueChange: o.Set,
                    min: o.Min,
                    max: o.Max,
                    onSubmit: o.Submit,
                    width: 200,
                    id: "field"
                )
        );

        driver.Focus(Numbers.Text(driver));
        var keyboard = driver.TextInput?.Kind;
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("5000");
        driver.Tab();
        var left = Numbers.Shown(driver);
        driver.Focus(Numbers.Text(driver));
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("5");
        driver.Press(Key.Enter);

        await Assert.That(keyboard).IsEqualTo(TextInputKind.Number);
        await Assert.That(left).IsEqualTo(Grouped(1000));
        await Assert.That(owner.Reports).IsEquivalentTo([5000L, 1000L, 5L, 100L]);
        await Assert.That(owner.Submits).IsEquivalentTo([100L]);
        await Assert.That(Numbers.Shown(driver)).IsEqualTo("100");
    }
}

internal sealed class MoneyOwner
{
    State<long>? _value;

    public State<long> Value
    {
        get
        {
            if (_value is not { } value)
                throw new InvalidOperationException("The owner has not rendered yet.");
            return value;
        }
    }

    public readonly List<long> Reports = [];
    public readonly List<long> Submits = [];
    public long Min;
    public long? Max;

    public long Shown => Value.Value;

    public Callback<long> Set =>
        Callback<long>.Of(
            this,
            static (o, v) =>
            {
                o.Reports.Add(v);
                o.Value.Value = v;
            }
        );

    public Callback<long> Submit => Callback<long>.Of(this, static (o, v) => o.Submits.Add(v));

    public UiDriver Render(
        long start,
        Action<Ui, MoneyOwner> field,
        Theming.Density density = default
    ) =>
        UiDriver.Render(
            ui =>
            {
                _value = ui.State(start);
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
                {
                    field(ui, this);
                    using (ui.Box(width: 40, height: 20, focusable: true, id: "after")) { }
                }
            },
            density: density
        );
}

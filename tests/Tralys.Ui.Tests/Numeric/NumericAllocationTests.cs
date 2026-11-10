using System.Globalization;
using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class NumericAllocationTests
{
    static readonly string[] Digits = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];

    [Test]
    public async Task Typing_a_digit_a_frame_into_a_number_input_reporting_and_regrouping_each_past_its_undo_cap_allocates_nothing()
    {
        var owner = new NumberOwner();
        using var driver = owner.Render(
            5.0,
            static (ui, o) =>
                ui.NumberInput(
                    value: o.Shown,
                    onValueChange: o.Value.Setter,
                    width: 200,
                    id: "field"
                )
        );
        driver.Focus(Numbers.Text(driver));
        var typing = new Counter();
        for (var i = 0; i < 1000; i++)
            TypeDigit(driver, typing);

        var bytes = driver.WorstFrameBytes(typing, TypeDigit);
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("1234");

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert
            .That(Numbers.Shown(driver))
            .IsEqualTo(1234.ToString("N0", CultureInfo.CurrentCulture));
    }

    [Test]
    public async Task Typing_a_digit_a_frame_into_a_money_input_reporting_and_regrouping_each_allocates_nothing()
    {
        var owner = new MoneyOwner();
        using var driver = owner.Render(
            5,
            static (ui, o) =>
                ui.MoneyInput(
                    value: o.Shown,
                    onValueChange: o.Value.Setter,
                    width: 200,
                    id: "field"
                )
        );
        driver.Focus(Numbers.Text(driver));
        var typing = new Counter();
        for (var i = 0; i < 1000; i++)
            TypeDigit(driver, typing);

        var bytes = driver.WorstFrameBytes(typing, TypeDigit);
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Type("1234");

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert
            .That(Numbers.Shown(driver))
            .IsEqualTo(1234L.ToString("N0", CultureInfo.CurrentCulture));
    }

    static void TypeDigit(UiDriver d, Counter c)
    {
        if (c.Value++ % 8 == 0)
        {
            d.Press(Key.A, ModifierKeys.Control, settle: false);
            d.Press(Key.BackSpace, settle: false);
        }
        else
            d.Type(Digits[c.Value % Digits.Length], settle: false);
        d.Frame();
    }

    [Test]
    public async Task A_number_input_given_a_value_too_long_for_its_buffer_every_frame_allocates_nothing()
    {
        var value = new Boxed<double[]>([1e120]);
        using var driver = UiDriver.Render(ui =>
            ui.NumberInput(value: value.Value[0], width: 200, id: "field")
        );

        var bytes = driver.WorstFrameBytes(
            value,
            static (d, v) =>
            {
                v.Value[0] = v.Value[0] == 1e120 ? 2e120 : 1e120;
                d.Rerender();
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(Numbers.Shown(driver).Length).IsGreaterThan(128);
    }

    [Test]
    public async Task A_pad_key_tapped_a_frame_under_touch_allocates_nothing()
    {
        var owner = new NumberOwner { Max = 999_999 };
        using var driver = owner.Render(
            0.0,
            static (ui, o) =>
                ui.NumberInput(
                    value: o.Shown,
                    onValueChange: o.Value.Setter,
                    max: o.Max,
                    width: 200,
                    id: "field"
                ),
            Density.Touch
        );
        driver.Tap(Numbers.Text(driver));
        var keys = new PadTaps([
            Pads.Key(driver, NumberPadKeyKind.Clear),
            Pads.Key(driver, NumberPadKeyKind.Digit1),
            Pads.Key(driver, NumberPadKeyKind.Digit2),
            Pads.Key(driver, NumberPadKeyKind.Digit3),
            Pads.Key(driver, NumberPadKeyKind.Up),
            Pads.Key(driver, NumberPadKeyKind.Back),
        ]);

        var bytes = driver.WorstFrameBytes(keys, PadTaps.Frame);

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(driver.All<NumberPad>().Count).IsEqualTo(1);
    }

    sealed class PadTaps(Node[] keys)
    {
        readonly Node[] _keys = keys;
        int _tick;

        public static void Frame(UiDriver d, PadTaps t)
        {
            d.Tap(t._keys[t._tick++ % t._keys.Length], settle: false);
            d.Frame();
        }
    }

    [Test]
    public async Task A_slider_fields_thumb_dragged_back_and_forth_a_step_a_frame_allocates_nothing()
    {
        var owner = new NumberOwner();
        using var driver = owner.Render(
            0.0,
            static (ui, o) =>
                ui.SliderField(label: "Volume", value: o.Shown, onValueChange: o.Value.Setter),
            width: 360f
        );
        var drag = new Dragging(driver.Hold(Sliders.Thumb(driver)));

        var bytes = driver.WorstFrameBytes(drag, Dragging.Frame);
        drag.Held.Release();

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(owner.Value.Peek).IsGreaterThan(0.0);
    }

    sealed class Dragging(HeldPointer held)
    {
        public readonly HeldPointer Held = held;
        int _tick;

        public static void Frame(UiDriver d, Dragging g)
        {
            var step = g._tick++ % 18 < 9 ? 9f : -9f;
            g.Held.MoveBy(new Vector2(step, 0f), settle: false);
            d.Frame();
        }
    }

    [Test]
    public async Task Filtering_a_suggestion_inputs_rows_and_moving_the_lit_one_every_frame_allocates_nothing()
    {
        string[] names = ["Dreamline", "Dreamland", "Nimbus", "Sunrise", "Driftwood", "Aurora"];
        var source = new LocalSuggestions(n => n.AddRange(names));
        var owner = new SuggestOwner { Source = source };
        using var driver = owner.Render();
        driver.Focus(Suggests.Text(driver));
        driver.Type("d");
        var rows = ((ComponentHost<SuggestionRowsProps>)driver.Get<SuggestionRows>().Host!)
            .Value
            .Rows!;
        var cycle = new Filtering(source, rows);

        var bytes = AllocationGate.WorstFrameBytes(
            () => Filtering.Frame(driver, cycle),
            warmup: 1000
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(Suggests.Rows(driver).Count).IsGreaterThan(0);
    }

    [Test]
    public async Task A_remote_answers_word_retyped_in_any_case_with_the_arrows_every_frame_allocates_nothing()
    {
        var server = new FakeServer();
        var source = (RemoteSuggestions<string[]>)server.Source();
        var owner = new SuggestOwner { Source = source, MaxSuggestions = 5 };
        using var driver = owner.Render();
        driver.Focus(Suggests.Text(driver));
        driver.Type("dragonfire", settle: false);
        driver.Frame(0.3f);
        driver.Frame(0f);
        server.Answer(0, "Dragonfire", "Dragonfire Keep");
        driver.Frame(0f);
        var rows = ((ComponentHost<SuggestionRowsProps>)driver.Get<SuggestionRows>().Host!)
            .Value
            .Rows!;
        var cycle = new Retyping(source, rows);

        var bytes = driver.WorstFrameBytes(cycle, Retyping.Frame);

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(server.Queries).IsEquivalentTo(["dragonfire"]);
        await Assert.That(cycle.SecondLit).IsTrue();
    }

    sealed class Filtering(ISuggestionSource source, SuggestionList rows)
    {
        static readonly string[] Queries = ["d", "dr", "n", "ni", "x", "r"];
        readonly ISuggestionSource _source = source;
        readonly SuggestionList _rows = rows;
        int _tick;

        public static void Frame(UiDriver d, Filtering f)
        {
            switch (f._tick++ % 4)
            {
                case 0:
                    f._source.Suggest(Queries[f._tick / 4 % Queries.Length], f._rows);
                    break;
                case 1:
                    d.KeyDown(Key.Down, settle: false);
                    break;
                case 2:
                    d.KeyDown(Key.Up, settle: false);
                    break;
            }
            d.Frame();
        }
    }

    sealed class Retyping(ISuggestionSource source, SuggestionList rows)
    {
        static readonly string[] Cases = ["dragonfire", "DRAGONFIRE", "DragonFire", "dRaGoNfIrE"];
        readonly ISuggestionSource _source = source;
        readonly SuggestionList _rows = rows;
        int _tick;
        public bool SecondLit = true;

        public static void Frame(UiDriver d, Retyping r)
        {
            var at = r._tick++;
            if (at % 3 == 0)
                r._source.Suggest(Cases[at / 3 % Cases.Length], r._rows);
            else if (at % 3 == 1)
                d.KeyDown(Key.Down, settle: false);
            else
            {
                r.SecondLit &= ReferenceEquals(r._rows.Selected, r._rows.Slots[1]);
                d.KeyDown(Key.Up, settle: false);
            }
            d.Frame();
        }
    }
}

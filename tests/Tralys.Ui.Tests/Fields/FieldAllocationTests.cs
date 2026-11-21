using System.Globalization;
using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class FieldAllocationTests
{
    static readonly string[] Letters =
    [
        .. "abcdefghijklmnopqrstuvwxyz".Select(static c => c.ToString()),
    ];

    static readonly string[] Values = ["", "a", "abc", "a longer text than the field shows"];

    [Test]
    public async Task Plain_and_masked_inputs_typed_into_and_set_anew_every_frame_past_their_undo_cap_allocate_nothing()
    {
        var plain = new ObservableText();
        var masked = new ObservableText();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Input(text: plain, maxLength: 20, clearable: true, width: 120, id: "plain");
                ui.Input(text: masked, kind: TextInputKind.Password, width: 120, id: "masked");
            }
        });
        var typing = new Typing([plain, masked], [driver.TextOf("plain"), driver.TextOf("masked")]);
        for (var i = 0; i < 400; i++)
            Typing.Frame(driver, typing);

        var bytes = driver.WorstFrameBytes(typing, Typing.Frame);

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Enter_and_escape_running_an_inputs_handlers_every_frame_allocate_nothing()
    {
        var runs = new Counter();
        var find = new ObservableText("find");
        using var driver = UiDriver.Render(ui =>
            ui.Input(
                text: find,
                onSubmit: Callback.Of(runs, static r => r.Value++),
                onEscape: Callback.Of(runs, static r => r.Value++),
                width: 120
            )
        );
        driver.Focus(driver.TextOf());
        var frames = new Counter();

        var bytes = driver.WorstFrameBytes(
            frames,
            static (d, f) =>
            {
                var key = f.Value++ % 2 == 0 ? Key.Enter : Key.Escape;
                d.KeyDown(key, settle: false);
                d.KeyUp(key, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(runs.Value).IsEqualTo(frames.Value);
    }

    [Test]
    public async Task A_held_mouse_dragging_a_selection_across_an_input_every_frame_allocates_nothing()
    {
        var line = new ObservableText("a line longer than the input is wide");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(padding: 40))
                ui.Input(text: line, width: 120);
        });
        var drag = new Drag(driver.Hold(driver.RootOf()));

        var bytes = driver.WorstFrameBytes(drag, Drag.Frame);

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(driver.TextOf().States.HasFlag(InputStates.Capturing)).IsTrue();
    }

    [Test]
    public async Task A_search_cleared_by_its_button_every_few_frames_and_filled_again_allocates_nothing()
    {
        var report = new ObservableText("report");
        var searches = new Counter();
        using var driver = UiDriver
            .Render(ui =>
                ui.SearchInput(
                    text: report,
                    onSubmit: Callback.Of(searches, static s => s.Value++),
                    width: 240
                )
            )
            .WithIcons();
        var clearRoot = await Assert.That(driver.Get<InputClear>().Host?.Root).IsNotNull();
        var clear = clearRoot.ViewBounds;
        var state = new Clearing(
            report,
            new Vector2(clear.X + clear.Width / 2f, clear.Y + clear.Height / 2f)
        );

        var bytes = driver.WorstFrameBytes(state, Clearing.Frame);

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(searches.Value).IsGreaterThan(state.Ticks / 6 - 1);
    }

    [Test]
    public async Task A_field_showing_a_number_that_changes_every_frame_allocates_nothing()
    {
        var model = new Model { Count = 999_990 };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Field(label: "Total", value: ui.Format($"{model.Count:N0}"))) { }
        });

        var bytes = driver.WorstFrameBytes(
            model,
            static (d, m) =>
            {
                m.Count++;
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert
            .That(driver.GetText(model.Count.ToString("N0", CultureInfo.InvariantCulture)))
            .IsNotNull();
    }

    [Test]
    public async Task An_editable_opened_with_f2_or_enter_and_shut_with_escape_or_enter_every_frame_allocates_nothing()
    {
        var entry = new Entry("#3399FF");
        using var driver = UiDriver.Render(ui =>
            ui.Editable(value: entry.Value, onValueChange: entry.Keep, width: 120, id: "editable")
        );
        driver.Focus(driver.Get("editable"));
        var frames = new Counter();

        var bytes = driver.WorstFrameBytes(
            frames,
            static (d, f) =>
            {
                var key = (f.Value++ % 4) switch
                {
                    0 => Key.F2,
                    1 => Key.Escape,
                    _ => Key.Enter,
                };
                d.KeyDown(key, settle: false);
                d.KeyUp(key, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(entry.Value).IsEqualTo("#3399FF");
    }

    [Test]
    public async Task Typing_into_an_open_editable_every_frame_allocates_nothing()
    {
        var entry = new Entry("#3399FF");
        using var driver = UiDriver.Render(ui =>
            ui.Editable(value: entry.Value, onValueChange: entry.Set, width: 120, id: "editable")
        );
        driver.Focus(driver.Get("editable"));
        driver.Press(Key.F2);
        var frames = new Counter();

        var bytes = driver.WorstFrameBytes(
            frames,
            static (d, f) =>
            {
                var tick = f.Value++;
                if (tick % 32 == 0)
                {
                    d.KeyDown(Key.A, ModifierKeys.Control, settle: false);
                    d.KeyUp(Key.A, ModifierKeys.Control, settle: false);
                }
                d.Type(Letters[tick % Letters.Length], settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(entry.Reports).IsEmpty();
    }

    sealed class Typing(ObservableText[] texts, Node[] fields)
    {
        readonly ObservableText[] _texts = texts;
        readonly Node[] _fields = fields;
        int _tick;

        public static void Frame(UiDriver d, Typing t)
        {
            var tick = t._tick++;
            if (tick % 64 == 0)
                d.Focus(t._fields[tick / 64 % 2], settle: false);
            if (tick % 8 == 0)
            {
                foreach (var text in t._texts)
                    text.Set(Values[tick / 8 % Values.Length]);
            }
            if (tick % 32 == 0)
            {
                d.KeyDown(Key.A, ModifierKeys.Control, settle: false);
                d.KeyUp(Key.A, ModifierKeys.Control, settle: false);
            }
            d.Type(Letters[tick % Letters.Length], settle: false);
            d.Frame();
        }
    }

    sealed class Drag(HeldPointer held)
    {
        static readonly float[] Xs = [40f, 400f, 1f, 90f];
        readonly HeldPointer _held = held;
        int _tick;

        public static void Frame(UiDriver d, Drag drag)
        {
            var x = Xs[drag._tick++ % Xs.Length];
            drag._held.MoveBy(new Vector2(x - drag._held.Position.X, 0f), settle: false);
            d.Frame();
        }
    }

    sealed class Clearing(ObservableText text, Vector2 at)
    {
        readonly ObservableText _text = text;
        readonly Vector2 _at = at;
        public int Ticks;

        public static void Frame(UiDriver d, Clearing c)
        {
            switch (c.Ticks++ % 6)
            {
                case 0:
                    d.Click(c._at, settle: false);
                    break;
                case 3:
                    c._text.Set("report");
                    break;
            }
            d.Frame();
        }
    }
}

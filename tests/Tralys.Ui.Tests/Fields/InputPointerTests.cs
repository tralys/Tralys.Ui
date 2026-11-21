using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class InputPointerTests
{
    [Test]
    public async Task Hovering_an_input_shows_the_i_beam_but_not_while_it_is_disabled()
    {
        var on = new ObservableText("on");
        var off = new ObservableText("off");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.Input(text: on, width: 200, id: "on");
                ui.Input(text: off, disabled: true, width: 200, id: "off");
            }
        });

        driver.Hover(driver.RootOf("on"));
        var enabled = driver.Cursor;
        driver.Hover(driver.RootOf("off"));

        await Assert.That(enabled).IsSameReferenceAs(Cursors.IBeam);
        await Assert.That(driver.Cursor).IsNotSameReferenceAs(Cursors.IBeam);
    }

    [Test]
    public async Task A_drag_holds_the_pointer_past_either_edge_a_shift_click_extends_from_the_caret_and_a_right_click_keeps_the_selection()
    {
        var hello = new ObservableText("hello world");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(padding: 100))
                ui.Input(text: hello, width: 200);
        });
        var text = driver.TextOf();

        var held = driver.Hold(text);
        var pressed = driver.Selection().Caret;
        held.MoveBy(new Vector2(-400f, 0f));
        var start = driver.Selection();
        held.MoveBy(new Vector2(800f, 0f));
        var end = driver.Selection();
        var capturing = text.States.HasFlag(InputStates.Capturing);
        held.Release();
        driver.CaretAt(1);
        driver.Click(text, modifiers: ModifierKeys.Shift);
        var extended = driver.Selection();
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Click(text, MouseButton.Right);

        await Assert.That((start, end)).IsEqualTo(((pressed, 0), (pressed, 11)));
        await Assert.That(capturing).IsTrue();
        await Assert.That(extended.Anchor).IsEqualTo(1);
        await Assert.That(extended.Caret).IsGreaterThan(1);
        await Assert.That(driver.Selection()).IsEqualTo((0, 11));
    }

    [Test]
    public async Task A_double_press_dragged_grows_by_whole_words_either_way_and_a_triple_press_dragged_keeps_all()
    {
        var words = new ObservableText("one two three");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(padding: 100, align: Align.Start))
                ui.Input(text: words);
        });
        var text = driver.TextOf();

        var word = driver.Hold(text, clicks: 2);
        var two = driver.Selection();
        word.MoveBy(new Vector2(30f, 0f));
        var on = driver.Selection();
        word.MoveBy(new Vector2(-90f, 0f));
        var back = driver.Selection();
        word.Release();
        var all = driver.Hold(text, clicks: 3);
        all.MoveBy(new Vector2(-40f, 0f));

        await Assert.That((two, on, back)).IsEqualTo(((4, 7), (4, 13), (7, 0)));
        await Assert.That(driver.Selection()).IsEqualTo((0, 13));
    }

    [Test]
    public async Task A_double_click_on_a_letters_right_half_selects_its_word_rather_than_the_space_after()
    {
        Length width = default;
        var hello = new ObservableText("hello world");
        using var driver = UiDriver.Render(ui => ui.Input(text: hello, width: width));
        driver.CaretAt(4);
        var before = (await Assert.That(driver.TextInput).IsNotNull()).Area.X;
        driver.Press(Key.Right);
        var after = (await Assert.That(driver.TextInput).IsNotNull()).Area.X;
        var root = driver.RootOf().ViewBounds;

        width = 2f * (before + (after - before) * 0.75f - root.X);
        driver.Rerender();
        driver.Unhover();
        driver.DoubleClick(driver.RootOf());

        await Assert.That(driver.Selection()).IsEqualTo((0, 5));
    }

    [Test]
    public async Task A_second_finger_takes_over_the_selection_once_the_first_lifts()
    {
        var hello = new ObservableText("hello world");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(padding: 100, align: Align.Start))
                ui.Input(text: hello);
        });
        var text = driver.TextOf();

        var first = driver.Hold(text, PointerKind.Touch);
        var second = driver.Hold(text, PointerKind.Touch);
        first.Release();
        second.MoveBy(new Vector2(-30f, 0f));

        var selection = driver.Selection();
        await Assert.That(selection.Caret).IsNotEqualTo(selection.Anchor);
    }

    [Test]
    public async Task A_finger_swiping_an_input_in_an_area_that_pans_both_ways_selects_rather_than_scrolls()
    {
        var scroll = new Seen<ScrollHandle?>();
        var hello = new ObservableText("hello world");
        using var driver = UiDriver.Render(ui =>
        {
            var handle = ui.ScrollHandle();
            scroll.Value = handle;
            using (
                ui.Box(
                    overflow: Overflow.Scroll,
                    scroll: handle,
                    dragToPan: true,
                    width: 150,
                    height: 60
                )
            )
            using (ui.Box(direction: Direction.Column, width: 400, height: 200, shrink: 0f))
                ui.Input(text: hello, width: 200);
        });

        driver.Drag(driver.TextOf(), new Vector2(-60f, -30f), PointerKind.Touch);

        var selection = driver.Selection();
        var handle = await Assert.That(scroll.Value).IsNotNull();
        await Assert.That(handle.Offset).IsEqualTo(Vector2.Zero);
        await Assert.That(selection.Caret).IsNotEqualTo(selection.Anchor);
    }
}

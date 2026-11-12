using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;
using static Tralys.Ui.Tests.Rows;

namespace Tralys.Ui.Tests;

public sealed class ListBoxReorderAllocationTests
{
    static readonly Callback<ListReorder> Ignore = Callback<ListReorder>.Of(static _ => { });

    static UiDriver Render(IItemsSource<string> items) =>
        UiDriver.Render(
            ui =>
                ui.ListBox(
                    items: items,
                    itemKey: Words.ByText,
                    itemText: Words.Text,
                    reorderable: true,
                    onReorder: Ignore,
                    width: 200
                ),
            new Size(400, 300)
        );

    [Test]
    public async Task A_carried_rows_insert_line_jumping_between_two_rows_every_frame_allocates_nothing()
    {
        using var driver = Render(Words.Of("a", "b", "c", "d"));
        var b = Of(driver, "b").ViewBounds;
        var c = Of(driver, "c").ViewBounds;
        var press = driver.Hold(Of(driver, "a"));
        press.MoveBy(new Vector2(0, 10));
        press.MoveBy(new Vector2(b.X + 10, b.Y + b.Height / 4) - press.Position);
        var jump = new Jump(press, new Vector2(0, c.Y + c.Height * 3 / 4 - press.Position.Y));

        var bytes = driver.WorstFrameBytes(
            jump,
            static (d, j) =>
            {
                j.Press.MoveBy(j.Tick++ % 2 == 0 ? j.By : -j.By, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(driver.View.DragDropActive).IsTrue();
    }

    [Test]
    public async Task A_carried_row_while_a_row_comes_and_goes_above_it_every_frame_allocates_nothing()
    {
        var words = Words.List("a", "b", "c", "d");
        using var driver = Render(ItemsSource.From(words));
        var press = driver.Hold(Of(driver, "c"));
        press.MoveBy(new Vector2(0, 10));
        var churn = new Churn(words);

        var bytes = driver.WorstFrameBytes(
            churn,
            static (d, c) =>
            {
                if (c.Tick++ % 2 == 0)
                    c.Words.Insert(0, "z");
                else
                    c.Words.RemoveAt(0);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(driver.View.DragDropActive).IsTrue();
    }

    [Test]
    public async Task A_press_leaving_a_row_short_of_the_threshold_and_released_every_frame_allocates_nothing()
    {
        using var driver = Render(Words.Of("a", "b", "c", "d"));
        var row = Of(driver, "a").ViewBounds;
        var list = List(driver).Parent!.ViewBounds;
        var inside = new Vector2(row.Right - 1, row.Y + row.Height / 2);
        var path = new Path(inside, new Vector2(list.Right + 1, inside.Y));

        var bytes = driver.WorstFrameBytes(
            path,
            static (d, p) =>
            {
                var view = d.View;
                view.PointerMove(
                    Pointer.Mouse,
                    p.Inside,
                    MouseButtons.None,
                    false,
                    default,
                    default
                );
                view.PointerDown(Pointer.Mouse, p.Inside, MouseButton.Left, default, default);
                view.PointerMove(Pointer.Mouse, p.Out, MouseButtons.Left, true, default, default);
                view.PointerUp(Pointer.Mouse, p.Out, MouseButton.Left, default, default);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(driver.View.DragDropActive).IsFalse();
    }

    sealed class Jump(HeldPointer press, Vector2 by)
    {
        public readonly HeldPointer Press = press;
        public readonly Vector2 By = by;
        public int Tick;
    }

    sealed class Churn(ObservableList<string> words)
    {
        public readonly ObservableList<string> Words = words;
        public int Tick;
    }

    sealed class Path(Vector2 inside, Vector2 outside)
    {
        public readonly Vector2 Inside = inside;
        public readonly Vector2 Out = outside;
    }
}

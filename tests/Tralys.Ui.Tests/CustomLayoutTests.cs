using System.Numerics;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class CustomLayoutTests
{
    [Test]
    public async Task A_custom_layout_sizes_itself_and_places_its_children_from_its_state_in_one_pass()
    {
        var model = new Model { Count = 1 };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (
                ui.Layout(
                    algorithm: Spread.Instance,
                    state: (float)model.Count,
                    padding: new Thickness(5),
                    id: "spread"
                )
            )
            {
                using (ui.Box(width: 20, height: 10, id: "a")) { }
                using (ui.Box(width: 30, height: 20, id: "b")) { }
                using (ui.Box(width: 99, height: 99, collapsed: true)) { }
                using (
                    ui.Box(
                        width: 8,
                        height: 8,
                        position: Position.Absolute,
                        right: 0,
                        bottom: 0,
                        id: "corner"
                    )
                ) { }
            }
        });
        var before = (driver.Get("spread").ViewBounds, driver.Get("b").ViewBounds);

        model.Count = 2;
        driver.Frame();

        await Assert.That(before).IsEqualTo((new Rect(0, 0, 40, 40), new Rect(5, 15, 30, 20)));
        await Assert.That(driver.Get("spread").ViewBounds).IsEqualTo(new Rect(0, 0, 70, 70));
        await Assert.That(driver.Get("b").ViewBounds).IsEqualTo(new Rect(5, 25, 30, 20));
        await Assert.That(driver.Get("corner").ViewBounds).IsEqualTo(new Rect(62, 62, 8, 8));
    }

    [Test]
    public async Task A_child_placed_with_a_transform_is_hit_where_it_draws_and_given_positions_in_its_own_units()
    {
        var down = new Landing();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (ui.Layout(algorithm: Spread.Instance, state: 4f))
            using (
                ui.Box(
                    width: 50,
                    height: 25,
                    onPointerDown: Handler<PointerEvent>.Of(
                        down,
                        static (s, ref e) => s.At = e.Position
                    )
                )
            ) { }
        });

        driver.Click(new Vector2(180, 90));

        await Assert.That(down.At.X).IsEqualTo(45f).Within(1e-3f);
        await Assert.That(down.At.Y).IsEqualTo(22.5f).Within(1e-3f);
    }

    [Test]
    public async Task An_algorithm_that_measures_in_or_returns_or_places_at_a_non_finite_size_throws_naming_itself()
    {
        var messages = new List<string>();

        foreach (var fault in (Fault[])[Fault.Constraint, Fault.Size, Fault.Place])
        {
            var model = new Model();
            using var driver = UiDriver.Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start))
                using (ui.Layout(algorithm: Faulty.Instance, state: (Fault)model.Count))
                using (ui.Box(width: 10, height: 10)) { }
            });
            model.Count = (int)fault;
            messages.Add(Assert.Throws<InvalidOperationException>(() => driver.Frame()).Message);
        }

        await Assert
            .That(
                messages.Count(static m => m.StartsWith(nameof(Faulty), StringComparison.Ordinal))
            )
            .IsEqualTo(3);
    }

    sealed class Landing
    {
        public Vector2 At;
    }

    enum Fault
    {
        None,
        Constraint,
        Size,
        Place,
    }

    /// <summary>Stacks its children down its height, each scaled by the state, and spreads what height is left
    /// between them.</summary>
    [Test]
    public async Task A_child_the_layout_hides_draws_hits_and_takes_tab_as_a_hidden_one_until_it_is_placed_again()
    {
        var model = new Model { Count = 300 };
        var fill = Color.FromRgb(90, 90, 90);
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (ui.Layout(algorithm: FirstThatFits.Instance, state: 0, width: model.Count))
            {
                using (
                    ui.Box(width: 200, height: 20, background: fill, focusable: true, id: "long")
                ) { }
                using (
                    ui.Box(width: 50, height: 20, background: fill, focusable: true, id: "short")
                ) { }
            }
        });
        driver.Tab();
        var wide = (driver.Focused?.Id, driver.View.HitTest(new Vector2(70, 10))?.Id);

        model.Count = 100;
        driver.Settle();
        driver.Tab();

        await Assert.That(wide).IsEqualTo(("long", "long"));
        await Assert.That(driver.Focused?.Id).IsEqualTo("short");
        await Assert.That(driver.View.HitTest(new Vector2(70, 10))).IsNull();
        await Assert.That(driver.Draw().Lines.Any(l => l.Contains("#long"))).IsFalse();
    }

    [Test]
    public async Task A_child_the_layout_hides_reads_as_hidden_from_outside_and_its_dump_says_so()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            using (ui.Layout(algorithm: FirstThatFits.Instance, state: 0, width: 100, id: "fit"))
            {
                using (ui.Box(width: 200, height: 20, id: "long"))
                    ui.Text("wide", id: "inside");
                using (ui.Box(width: 50, height: 20, id: "short")) { }
            }
        });
        var hidden = driver.Get("long");
        var dump = LayoutDump.Of(driver.Get("fit"));

        await Assert
            .That((hidden.IsLayoutHidden, hidden.Placement.Hidden))
            .IsEqualTo((true, false));
        await Assert.That(driver.Get("inside").IsShown).IsFalse();
        await Assert.That(driver.Get("short").IsShown).IsTrue();
        await Assert
            .That(dump.Nodes.Where(n => n.Hidden).Select(n => n.Path))
            .IsEquivalentTo(["Layout#fit/Box#long"]);
    }

    // Shows the first child that fits the width and hides the rest, as a headline picks the wording that fits.
    internal sealed class FirstThatFits : LayoutAlgorithm<int>
    {
        public static readonly FirstThatFits Instance = new();

        public override Size Measure(
            ref LayoutContext cx,
            int state,
            AvailableSpace width,
            AvailableSpace height
        ) => new(width.IsDefinite ? width.Value : 0f, 20f);

        public override void Arrange(ref LayoutContext cx, int state, Size size)
        {
            var shown = false;
            for (var i = 0; i < cx.ChildCount; i++)
            {
                var child = cx.MeasureChild(
                    i,
                    AvailableSpace.MaxContent,
                    AvailableSpace.MaxContent
                );
                if (shown || child.Width > size.Width)
                    cx.HideChild(i);
                else
                {
                    cx.PlaceChild(i, new Rect(0f, 0f, child.Width, child.Height));
                    shown = true;
                }
            }
        }
    }

    internal sealed class Spread : LayoutAlgorithm<float>
    {
        public static readonly Spread Instance = new();

        public override Size Measure(
            ref LayoutContext cx,
            float scale,
            AvailableSpace width,
            AvailableSpace height
        )
        {
            var widest = 0f;
            var tall = 0f;
            for (var i = 0; i < cx.ChildCount; i++)
            {
                var size = cx.MeasureChild(i, AvailableSpace.MaxContent, AvailableSpace.MaxContent);
                widest = MathF.Max(widest, size.Width * scale);
                tall += size.Height * scale;
            }
            var pad = cx.Padding;
            return new Size(widest + pad.Left + pad.Right, tall + pad.Top + pad.Bottom);
        }

        public override void Arrange(ref LayoutContext cx, float scale, Size size)
        {
            var pad = cx.Padding;
            var free = size.Height - pad.Top - pad.Bottom;
            for (var i = 0; i < cx.ChildCount; i++)
                free -=
                    cx.MeasureChild(i, AvailableSpace.MaxContent, AvailableSpace.MaxContent).Height
                    * scale;
            var between = cx.ChildCount > 1 ? MathF.Max(0f, free) / (cx.ChildCount - 1) : 0f;
            var y = pad.Top;
            for (var i = 0; i < cx.ChildCount; i++)
            {
                var child = cx.MeasureChild(
                    i,
                    AvailableSpace.MaxContent,
                    AvailableSpace.MaxContent
                );
                cx.PlaceChild(
                    i,
                    new Rect(pad.Left, y, child.Width, child.Height),
                    Transform.Scale(scale)
                );
                y += child.Height * scale + between;
            }
        }
    }

    sealed class Faulty : LayoutAlgorithm<Fault>
    {
        public static readonly Faulty Instance = new();

        public override Size Measure(
            ref LayoutContext cx,
            Fault fault,
            AvailableSpace width,
            AvailableSpace height
        )
        {
            var space = AvailableSpace.Definite(
                fault == Fault.Constraint ? float.PositiveInfinity : 10f
            );
            cx.MeasureChild(0, space, space);
            return fault == Fault.Size ? new Size(float.NaN, 10) : new Size(10, 10);
        }

        public override void Arrange(ref LayoutContext cx, Fault fault, Size size) =>
            cx.PlaceChild(
                0,
                new Rect(0, 0, fault == Fault.Place ? float.PositiveInfinity : 10f, 10)
            );
    }
}

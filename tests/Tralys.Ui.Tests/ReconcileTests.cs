using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class ReconcileTests
{
    [Test]
    public async Task Only_the_component_that_read_a_change_rerenders_once_for_many_changes()
    {
        var shared = new Model { Name = "a" };
        var other = new Model { Name = "b" };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box())
            {
                ui.Label(model: shared);
                ui.CountLabel(model: shared);
                ui.Label(model: other);
            }
        });

        shared.Name = "x";
        shared.Name = "y";
        shared.Name = "z";
        driver.Frame();

        await Assert.That(driver.Stats.Renders).IsEqualTo(1);
        await Assert.That(driver.Stats.RendersOf<Label>()).IsEqualTo(1);
        await Assert.That(driver.GetText("z")).IsNotNull();
    }

    [Test]
    public async Task Unchanged_output_writes_nothing()
    {
        var model = new Model { Name = "label" };
        using var driver = UiDriver.Render(ui =>
        {
            var big = model.Count > 10;
            using (
                ui.Box(
                    direction: Direction.Column,
                    padding: 8,
                    background: Color.FromRgb(10, 20, 30),
                    border: new Border(1, Color.FromRgb(90, 90, 90)),
                    onClick: Callback.Of(model, static m => m.Count++)
                )
            )
            {
                ui.Text(big ? "big" : "small", wrap: true);
                ui.Label(model: model);
            }
        });

        model.Count = 1;
        driver.Frame();

        await Assert.That(driver.Stats.Renders).IsEqualTo(1);
        await Assert.That(driver.Stats.NodeWrites).IsEqualTo(0);
        await Assert.That(driver.Stats.Paints).IsEqualTo(0);
        await Assert.That(driver.Stats.DrawListRebuilt).IsFalse();
    }

    [Test]
    public async Task A_branch_flip_and_a_keyed_reorder_keep_state()
    {
        var banner = new Model();
        var order = new ObservableList<int> { 1, 2, 3 };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
            {
                if (banner.Remember)
                    ui.Text("banner");
                ui.Ticker(id: "single");
                foreach (var k in order)
                    ui.Ticker(id: "row" + k, key: k);
            }
        });
        var single = driver.MountId(driver.Get<Ticker>("single"));
        var row2 = driver.MountId(driver.Get<Ticker>("row2"));
        driver.Click(driver.Get("single"));
        driver.Click(driver.Get("single"));
        driver.Click(driver.Get("row2"));

        banner.Remember = true;
        order.Move(0, 2);
        driver.Settle();

        await Assert.That(TextIn(driver.Get("single"))).IsEqualTo("2");
        await Assert.That(TextIn(driver.Get("row2"))).IsEqualTo("1");
        await Assert.That(driver.MountId(driver.Get<Ticker>("single"))).IsEqualTo(single);
        await Assert.That(driver.MountId(driver.Get<Ticker>("row2"))).IsEqualTo(row2);
    }

    [Test]
    public async Task A_keyed_repeat_shows_only_what_an_insert_and_two_removes_leave_in_their_order()
    {
        var items = new ObservableList<string> { "a", "b", "c" };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            using (ui.Box(direction: Direction.Column, id: "stack"))
            {
                foreach (var item in items)
                {
                    using (ui.Box(width: 20, height: 20, id: item, key: item)) { }
                }
            }
        });

        items.Insert(1, "x");
        items.RemoveAt(0);
        items.RemoveAt(2);
        driver.Frame();

        var shown = new List<string?>();
        foreach (var child in driver.Get("stack").Children)
            shown.Add(child.Id);
        await Assert.That(string.Join(" ", shown)).IsEqualTo("x b");
        var children = driver.Get("stack").Children;
        await Assert.That(children.Count).IsEqualTo(2);
        await Assert.That(children[1].Id).IsEqualTo("b");
        await Assert
            .That(() => driver.Get("stack").Children[2])
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(driver.Get("stack").Bounds.Height).IsEqualTo(40f);
    }

    [Test]
    public async Task A_removed_item_stops_being_observed_and_its_component_drops_its_subscriptions()
    {
        var kept = new Model { Name = "kept" };
        var removed = new Model { Name = "removed" };
        var items = new ObservableList<Model> { kept, removed };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box())
            {
                foreach (var item in items)
                    ui.Label(model: item, key: UiKey.Of(item));
            }
        });
        await Assert.That(((IObservable)removed).Observers.Count).IsEqualTo(1);

        items.Remove(removed);
        driver.Settle();
        removed.Name = "changed";
        driver.Frame();

        await Assert.That(((IObservable)removed).Observers.Count).IsEqualTo(0);
        await Assert.That(driver.Stats.Renders).IsEqualTo(0);
    }

    [Test]
    public async Task A_closed_view_leaves_the_model_it_read_no_observers_and_is_collected_while_the_model_lives_on()
    {
        var model = new Model { Name = "kept" };

        var (view, whileOpen) = OpenedAndClosed(model);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Assert.That(whileOpen).IsGreaterThan(0);
        await Assert.That(((IObservable)model).Observers.Count).IsEqualTo(0);
        await Assert.That(view.IsAlive).IsFalse();
        GC.KeepAlive(model);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static (WeakReference View, int WhileOpen) OpenedAndClosed(Model model)
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box())
            {
                ui.Label(model: model);
                ui.CountLabel(model: model);
            }
        });
        return (new WeakReference(driver.View), ((IObservable)model).Observers.Count);
    }

    [Test]
    public async Task A_helper_used_twice_keeps_both_identities()
    {
        var first = new Model { Remember = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box())
            {
                if (first.Remember)
                    Counter(ui, "first");
                Counter(ui, "second");
            }
        });
        driver.Click(driver.Get("second"));
        driver.Click(driver.Get("second"));

        first.Remember = false;
        driver.Settle();

        await Assert.That(TextIn(driver.Get("second"))).IsEqualTo("2");
    }

    [FramePath]
    static void Counter(
        Ui ui,
        string id,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Fragment(file: file, line: line))
        {
            var count = ui.State(0);
            using (
                ui.Box(
                    onClick: Callback.Of(count, static c => c.Value++),
                    width: 30,
                    height: 20,
                    id: id
                )
            )
                ui.Text(ui.Format($"{count.Value}"));
        }
    }

    [Test]
    public async Task An_omitted_argument_inherits_and_a_dropped_one_resets()
    {
        var model = new Model { Remember = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(direction: Direction.Column, fontSize: 24, color: Accent, align: Align.Start)
            )
            {
                ui.Text("omitted", id: "omitted");
                ui.Text(
                    "dropped",
                    fontSize: model.Remember ? 48 : 0,
                    color: model.Remember ? (Paint)Muted : default,
                    id: "dropped"
                );
            }
        });
        var inherited = driver.Get("omitted").ViewBounds.Height;
        await Assert.That(driver.Get("dropped").ViewBounds.Height).IsGreaterThan(inherited);
        await Assert.That(driver.Get("dropped").Env.Color).IsEqualTo((Paint)Muted);

        model.Remember = false;
        driver.Settle();

        await Assert.That(driver.Get("dropped").ViewBounds.Height).IsEqualTo(inherited);
        await Assert.That(driver.Get("dropped").Env.Color).IsEqualTo((Paint)Accent);
        using var plain = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
                ui.Text("omitted", id: "omitted");
        });
        await Assert.That(plain.Get("omitted").ViewBounds.Height).IsLessThan(inherited);
    }

    static readonly Color Accent = Color.FromRgb(40, 120, 220);
    static readonly Color Muted = Color.FromRgb(120, 120, 120);

    [Test]
    public async Task Setting_state_rerenders_only_its_owner()
    {
        var model = new Model { Name = "n" };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box())
            {
                ui.Ticker(id: "a");
                ui.Ticker(id: "b");
                ui.Label(model: model);
            }
        });

        driver.Click(driver.Get("a"), settle: false);
        driver.Frame();

        await Assert.That(driver.Stats.Renders).IsEqualTo(1);
        await Assert.That(driver.Stats.RendersOf<Ticker>()).IsEqualTo(1);
        await Assert.That(TextIn(driver.Get("a"))).IsEqualTo("1");
        await Assert.That(TextIn(driver.Get("b"))).IsEqualTo("0");
    }

    [Test]
    public async Task Rerendering_with_a_generated_setter_callback_writes_nothing()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            _ = model.Count;
            ui.Flip(value: model.Remember, onValueChange: model.SetRemember, id: "toggle");
        });

        model.Count++;
        driver.Frame();
        await Assert.That(driver.Stats.Renders).IsEqualTo(1);
        await Assert.That(driver.Stats.RendersOf<Flip>()).IsEqualTo(0);
        await Assert.That(driver.Stats.NodeWrites).IsEqualTo(0);

        driver.Click(driver.Get("toggle"));
        await Assert.That(model.Remember).IsTrue();
    }

    [Test]
    public async Task A_throwing_render_hides_its_subtree_and_keeps_it_mounted()
    {
        var model = new Model();
        var faults = new List<Exception>();
        using var driver = UiDriver.Render(ui => ui.Thrower(model: model));
        driver.View.Context.OnFault = faults.Add;
        driver.Click(driver.Get("ticker"));
        var mount = driver.MountId(driver.Get<Ticker>("ticker"));
        var bounds = driver.Get("ticker").ViewBounds;

        model.Count = 1;
        driver.Settle();

        await Assert.That(faults).Count().IsEqualTo(1);
        await Assert.That(driver.MountId(driver.Get<Ticker>("ticker"))).IsEqualTo(mount);
        await Assert.That(driver.Get("ticker").ViewBounds).IsEqualTo(bounds);
        await Assert
            .That(driver.View.HitTest(new System.Numerics.Vector2(bounds.X + 1, bounds.Y + 1)))
            .IsNull();

        model.Count = 2;
        driver.Settle();

        await Assert.That(TextIn(driver.Get("ticker"))).IsEqualTo("1");
        await Assert
            .That(driver.View.HitTest(new System.Numerics.Vector2(bounds.X + 1, bounds.Y + 1)))
            .IsNotNull();
    }

    [Test]
    public async Task A_host_rendering_more_than_16_times_in_one_update_throws_in_debug_and_waits_for_the_next_update_in_release()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui => ui.Restless(model: model));

        model.Remember = true;
#if DEBUG
        await Assert.That(() => driver.Frame()).Throws<InvalidOperationException>();
#else
        driver.Frame();

        await Assert.That(driver.Stats.RendersOf<Restless>()).IsEqualTo(16);
        await Assert.That(driver.View.IsIdle).IsFalse();
#endif
    }

    [Test]
    public async Task A_node_handle_lets_go_of_a_node_the_view_released_and_put_to_another_use()
    {
        var shown = true;
        using var driver = UiDriver.Render(ui =>
        {
            if (shown)
                using (ui.Box(id: "held")) { }
        });
        var held = driver.Get("held");
        var handle = new NodeHandle(held);
        shown = false;
        driver.Rerender();
        shown = true;
        driver.Rerender();

        await Assert
            .That(driver.Get("held"))
            .IsSameReferenceAs(held)
            .Because(
                "the view hands a released node out again, which is what a handle must not follow"
            );
        await Assert.That(handle.TryGet(out _)).IsFalse();
    }

    internal static string TextIn(Node node)
    {
        if (node.Kind == NodeKind.Text)
            return node.Text.ToString();
        foreach (var child in node.Children)
        {
            if (TextIn(child) is { Length: > 0 } text)
                return text;
        }
        return "";
    }
}

public readonly record struct ThrowerProps
{
    public required Model Model { get; init; }
}

/// <summary>Renders a ticker, then throws while its model's count is 1.</summary>
public sealed class Thrower : Component<ThrowerProps>
{
    protected override void Render(Ui ui, in ThrowerProps p)
    {
        var count = p.Model.Count;
        using (ui.Box())
        {
            ui.Ticker(id: "ticker");
            if (count == 1)
                throw new InvalidOperationException("The render failed.");
        }
    }
}

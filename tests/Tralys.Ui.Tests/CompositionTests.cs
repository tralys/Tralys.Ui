using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class CompositionTests
{
    [Test]
    public async Task A_callback_kept_past_its_components_unmount_cannot_reach_the_next_one_on_its_host()
    {
        var model = new Model();
        var log = new List<string>();
        var first = new Keeper();
        var second = new Keeper();
        using var driver = UiDriver.Render(ui =>
        {
            if (model.Count == 0)
                ui.Kept(name: "first", log: log, keeper: first);
            else if (model.Count == 2)
                ui.Kept(name: "second", log: log, keeper: second);
        });
        model.Count = 1;
        driver.Settle();
        model.Count = 2;
        driver.Settle();

        first.Callback.Invoke();
        second.Callback.Invoke();

        await Assert.That(log).IsEquivalentTo(["second"]);
    }

    [Test]
    public async Task A_component_its_caller_places_anew_moves_without_rendering_again()
    {
        var model = new Model { Name = "placed" };
        var place = new Model();
        using var driver = UiDriver.Render(ui =>
            ui.Label(model: model, margin: new Thickness(place.Count, 0, 0, 0))
        );

        place.Count = 12;
        driver.Settle();

        await Assert.That(driver.Stats.RendersOf<Label>()).IsEqualTo(0);
        await Assert.That(driver.GetText("placed").ViewBounds.X).IsEqualTo(12f);
    }

    [Test]
    public async Task Children_show_when_the_outlet_opens_and_unmount_when_it_closes()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Disclosure(id: "panel"))
                ui.Text("inside");
        });
        await Assert.That(Texts(driver.Root, "inside")).IsEqualTo(0);

        driver.Click(driver.Get("panel"));
        await Assert.That(Texts(driver.Root, "inside")).IsEqualTo(1);

        driver.Click(driver.Get("panel"));
        await Assert.That(Texts(driver.Root, "inside")).IsEqualTo(0);
    }

    [Test]
    public async Task A_provided_value_rerenders_its_readers_and_withdrawing_it_falls_back()
    {
        var model = new Model { Count = 1 };
        using var driver = UiDriver.Render(ui => ui.Provider(model: model));
        await Assert.That(ReconcileTests.TextIn(driver.Get("reader"))).IsEqualTo("1");

        model.Count = 2;
        driver.Frame();
        await Assert.That(driver.Stats.RendersOf<Reader>()).IsEqualTo(1);
        await Assert.That(ReconcileTests.TextIn(driver.Get("reader"))).IsEqualTo("2");

        model.Count = -1;
        driver.Settle();
        await Assert.That(ReconcileTests.TextIn(driver.Get("reader"))).IsEqualTo("-7");
    }

    [Test]
    public async Task Effects_run_after_layout_children_first_and_unmount_disposes_what_was_remembered()
    {
        var log = EffectParent.NewLog();
        var shown = new Model { Remember = true };
        using var driver = UiDriver.Render(ui =>
        {
            if (shown.Remember)
                ui.EffectParent(log: log);
        });

        await Assert.That(string.Join(", ", log.Lines)).IsEqualTo("child 10x10, parent 30x20");

        shown.Remember = false;
        driver.Settle();

        await Assert
            .That(string.Join(", ", log.Lines.Skip(2)))
            .IsEqualTo("child disposed, parent disposed");
    }

    [Test]
    public async Task An_effect_and_a_remembered_value_hanging_on_formatted_text_follow_each_change_of_it()
    {
        var model = new Model();
        var effects = new List<string>();
        var remembered = new List<string>();
        using var driver = UiDriver.Render(ui =>
        {
            var text = ui.Format($"n{model.Count}");
            ui.Effect(effects, text, static (log, dep, _) => log.Add(dep.ToString()));
            remembered.Add(ui.Remember(text, static dep => dep.ToString()));
        });

        model.Count = 1;
        driver.Settle();
        model.Count = 2;
        driver.Settle();

        await Assert.That(string.Join(" ", effects)).IsEqualTo("n0 n1 n2");
        await Assert.That(string.Join(" ", remembered)).IsEqualTo("n0 n1 n2");
    }

    [Test]
    public async Task Formatted_text_kept_in_state_or_provided_reads_as_written_in_later_renders()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            var shown = ui.Format($"x{model.Count}");
            var kept = ui.State<TextValue>(ui.Format($"kept"));
            ui.Provide(TextReader.Key, ui.Format($"n{model.Count}"));
            using (ui.Box(direction: Direction.Column))
            {
                ui.Text(shown);
                ui.Text(kept.Value, id: "kept");
                ui.TextReader(id: "reader");
            }
        });

        model.Count = 12345;
        driver.Settle();

        await Assert.That(ReconcileTests.TextIn(driver.Get("kept"))).IsEqualTo("kept");
        await Assert.That(ReconcileTests.TextIn(driver.Get("reader"))).IsEqualTo("n12345");
    }

    [Test]
    public async Task A_key_of_a_nullable_reference_reads_its_null_fallback_until_a_provider_gives_it_a_value()
    {
        string? name = null;
        using var driver = UiDriver.Render(ui =>
        {
            if (name is not null)
                ui.Provide(NameReader.Key, name);
            ui.NameReader(id: "reader");
        });
        var unprovided = ReconcileTests.TextIn(driver.Get("reader"));

        name = "Ada";
        driver.Rerender();

        await Assert.That(unprovided).IsEqualTo("none");
        await Assert.That(ReconcileTests.TextIn(driver.Get("reader"))).IsEqualTo("Ada");
    }

    [Test]
    public async Task An_effect_queued_by_a_component_its_owner_drops_in_the_same_update_does_not_run()
    {
        var model = new Model();
        var runs = new List<int>();
        var drop = false;
        using var driver = UiDriver.Render(ui =>
        {
            if (!drop)
                ui.RootFlipper(model: model, runs: runs);
        });

        drop = true;
        model.Count = 1;
        driver.Settle();

        await Assert.That(runs).IsEquivalentTo([0]);
    }

    [Test]
    public async Task A_size_read_through_a_ref_renders_its_reader_in_the_next_update_once_for_each_new_size()
    {
        var model = new Model { Count = 10 };
        var sizes = new List<Size>();
        using var driver = UiDriver.Render(ui =>
        {
            var box = ui.Ref();
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (ui.Box(width: model.Count, height: 10, nodeRef: box)) { }
                ui.Text(model.Name);
            }
            ui.SizeWatcher(target: box, sizes: sizes);
        });

        model.Name = "longer";
        driver.Settle();
        model.Count = 20;
        driver.Settle();

        await Assert
            .That(string.Join(" ", sizes.Select(static s => $"{s.Width}x{s.Height}")))
            .IsEqualTo("0x0 10x10 20x10");
    }

    static int Texts(Node node, string text)
    {
        var count = node.Kind == NodeKind.Text && node.Text.SequenceEqual(text) ? 1 : 0;
        foreach (var child in node.Children)
            count += Texts(child, text);
        return count;
    }
}

public readonly record struct DisclosureProps
{
    public int Tag { get; init; }
}

/// <summary>Shows its caller's children only while its own state says it's open.</summary>
[Children]
public sealed class Disclosure : Component<DisclosureProps>
{
    protected override void Render(Ui ui, in DisclosureProps p)
    {
        var open = ui.State(false);
        using (ui.Box(direction: Direction.Column, width: 100, height: 40, onClick: open.Toggle()))
        {
            if (open.Value)
            {
                using (ui.Box())
                    ui.Children();
            }
        }
    }
}

public readonly record struct ProviderProps
{
    public required Model Model { get; init; }
}

/// <summary>Provides its model's count while it is not negative, to a reader and a bystander.</summary>
public sealed class Provider : Component<ProviderProps>
{
    public static readonly ContextKey<int> Count = new("Count", -7);

    protected override void Render(Ui ui, in ProviderProps p)
    {
        var count = p.Model.Count;
        if (count >= 0)
            ui.Provide(Count, count);
        using (ui.Box())
        {
            ui.Reader(id: "reader");
            ui.Ticker(id: "bystander");
        }
    }
}

public readonly record struct ReaderProps
{
    public int Tag { get; init; }
}

public sealed class Reader : Component<ReaderProps>
{
    protected override void Render(Ui ui, in ReaderProps p)
    {
        using (ui.Box())
            ui.Text(ui.Format($"{ui.Use(Provider.Count)}"));
    }
}

public readonly record struct EffectParentProps
{
    public required ValueLog<(string Who, Size? Laid)> Log { get; init; }
}

/// <summary>Logs, from an effect, its box's size as laid out; its child does the same, and each one's remembered value
/// logs its disposal.</summary>
public sealed class EffectParent : Component<EffectParentProps>
{
    protected override void Render(Ui ui, in EffectParentProps p)
    {
        var log = p.Log;
        var box = ui.Ref();
        ui.Remember(log, static log => new Logged(log, "parent"));
        ui.Effect(log, box, static (log, box, cx) => log.Add(("parent", cx.BoundsOf(box).Size)));
        using (ui.Box(width: 30, height: 20, nodeRef: box))
            ui.EffectChild(log: log);
    }

    internal static ValueLog<(string Who, Size? Laid)> NewLog() =>
        new(static seen =>
            seen.Laid is { } size
                ? $"{seen.Who} {size.Width}x{size.Height}"
                : $"{seen.Who} disposed"
        );

    internal sealed class Logged(ValueLog<(string Who, Size? Laid)> log, string who) : IDisposable
    {
        public void Dispose() => log.Add((who, null));
    }
}

public sealed class EffectChild : Component<EffectParentProps>
{
    protected override void Render(Ui ui, in EffectParentProps p)
    {
        var box = ui.Ref();
        ui.Remember(p.Log, static log => new EffectParent.Logged(log, "child"));
        ui.Effect(p.Log, box, static (log, box, cx) => log.Add(("child", cx.BoundsOf(box).Size)));
        using (ui.Box(width: 10, height: 10, nodeRef: box)) { }
    }
}

public readonly record struct TextReaderProps
{
    public int Tag { get; init; }
}

/// <summary>Shows the text provided under its key.</summary>
public sealed class TextReader : Component<TextReaderProps>
{
    public static readonly ContextKey<TextValue> Key = new("ProvidedText", "");

    protected override void Render(Ui ui, in TextReaderProps p) => ui.Text(ui.Use(Key));
}

public readonly record struct NameReaderProps
{
    public int Tag { get; init; }
}

public sealed class NameReader : Component<NameReaderProps>
{
    public static readonly ContextKey<string?> Key = new("Name", null);

    protected override void Render(Ui ui, in NameReaderProps p) => ui.Text(ui.Use(Key) ?? "none");
}

public readonly record struct RootFlipperProps
{
    public required Model Model { get; init; }

    public required List<int> Runs { get; init; }
}

/// <summary>Logs each count it renders from an effect, and swaps its root node as the count leaves 0.</summary>
public sealed class RootFlipper : Component<RootFlipperProps>
{
    protected override void Render(Ui ui, in RootFlipperProps p)
    {
        var count = p.Model.Count;
        ui.Effect(p.Runs, count, static (runs, count, _) => runs.Add(count));
        if (count == 0)
            using (ui.Box(width: 10, height: 10)) { }
        else
            ui.Text("flipped");
    }
}

public readonly record struct SizeWatcherProps
{
    public required NodeRef Target { get; init; }

    public required List<Size> Sizes { get; init; }
}

/// <summary>Records each size it reads from its target's ref.</summary>
public sealed class SizeWatcher : Component<SizeWatcherProps>
{
    protected override void Render(Ui ui, in SizeWatcherProps p) => p.Sizes.Add(p.Target.Size);
}

using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class GeneratedCallTests
{
    [Test]
    public async Task A_formatted_prop_outlives_the_render_that_made_it()
    {
        var model = new Model { Count = 7 };
        using var driver = UiDriver.Render(ui =>
            ui.Tally(label: ui.Format($"item {model.Count}"), id: "badge")
        );

        driver.Click(driver.Get("badge"));

        await Assert.That(driver.GetText("item 7")).IsNotNull();
    }

    [Test]
    public async Task A_disabled_prop_disables_the_component_until_it_is_dropped()
    {
        var model = new Model { Remember = true };
        using var driver = UiDriver.Render(ui =>
            ui.Pick(value: "a", disabled: model.Remember, id: "pick")
        );
        await Assert.That(driver.Get("pick").States).IsEqualTo(InputStates.Disabled);

        model.Remember = false;
        driver.Settle();

        await Assert.That(driver.Get("pick").States).IsEqualTo(InputStates.None);
    }

    [Test]
    public async Task A_generic_component_the_view_mounts_itself_is_disabled_by_its_props()
    {
        using var driver = UiDriver.Render(static _ => { });

        driver.View.Mount<Pick<Guid>, PickProps<Guid>>(new PickProps<Guid> { Disabled = true });
        driver.Settle();

        await Assert
            .That(Nodes.Find(driver.Get<Pick<Guid>>(), NodeKind.Box).States)
            .IsEqualTo(InputStates.Disabled);
    }

    [Test]
    public async Task The_describer_hands_over_each_prop_in_declaration_order()
    {
        using var driver = UiDriver.Render(ui => ui.Tally(label: "x", count: 3, id: "badge"));
        var seen = new Seen();

        var described = driver.Get<Tally>("badge").DescribeProps(seen);

        await Assert.That(described).IsTrue();
        await Assert.That(seen.Props).IsEquivalentTo(["Label=x", "Count=3", "Disabled=False"]);
    }

    [Test]
    public async Task Stories_register_by_their_subjects_name_under_their_category_and_render_as_the_root()
    {
        var titled = StoryRegistry.All.Single(s => s.Key == "Panel.Header.Titled");
        using var driver = UiDriver.Render(titled.Render);

        await Assert.That(titled.Category).IsEqualTo("Panels");
        await Assert.That(titled.Component).IsEqualTo(typeof(Panel.Header));
        await Assert.That(driver.Get<Panel.Header>()).IsNotNull();
    }

    sealed class Seen : IPropsVisitor
    {
        public List<string> Props { get; } = [];

        public void Prop<T>(string name, in T value) => Props.Add($"{name}={value}");
    }
}

public readonly record struct TallyProps
{
    public TextValue Label { get; init; }

    public int Count { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>Formats its click count into the frame's arena before it shows its label, so a label it read from the
/// arena after the render that formatted it would show the count instead.</summary>
public sealed class Tally : Component<TallyProps>
{
    protected override void Render(Ui ui, in TallyProps p)
    {
        var clicks = ui.State(0);
        using (
            ui.Box(
                direction: Direction.Column,
                onClick: Callback.Of(clicks, static c => c.Value++),
                width: 60,
                height: 40
            )
        )
        {
            ui.Text(ui.Format($"{clicks.Value:D6}"));
            ui.Text(p.Label);
        }
    }
}

public readonly record struct PickProps<TItem>
{
    public TItem? Value { get; init; }

    public bool Disabled { get; init; }
}

public sealed partial class Pick<TItem> : Component<PickProps<TItem>>
{
    protected override void Render(Ui ui, in PickProps<TItem> p)
    {
        using (ui.Box(width: 20, height: 20)) { }
    }
}

public readonly record struct PanelProps
{
    public TextValue Title { get; init; }
}

[Children]
public sealed class Panel : Component<PanelProps>
{
    protected override void Render(Ui ui, in PanelProps p)
    {
        using (ui.Box(direction: Direction.Column))
        {
            ui.PanelHeader(title: p.Title);
            using (ui.Box())
                ui.Children();
        }
    }

    public sealed class Header : Component<PanelProps>
    {
        protected override void Render(Ui ui, in PanelProps p) => ui.Text(p.Title);
    }
}

[Stories<Panel.Header>("Panels")]
static class PanelHeaderStories
{
    [FramePath]
    public static void Titled(Ui ui)
    {
        using (ui.Panel(title: "Settings"))
            ui.Text("body");
    }
}

using Tralys.Ui.Components;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class EnvironmentTests
{
    [Test]
    public async Task An_inherited_disabled_state_and_a_forced_input_state_reach_the_component()
    {
        var gate = new Model { Remember = true };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
            {
                using (ui.Box(disabled: gate.Remember))
                    ui.StateProbe(name: "disabled", id: "disabled");
                using (ui.Box(forceStates: InputStates.Pressed))
                    ui.StateProbe(name: "pressed", id: "pressed");
            }
        });

        await Assert.That(Seen(driver, "disabled")).IsEqualTo((true, false));
        await Assert.That(Seen(driver, "pressed")).IsEqualTo((false, true));

        gate.Remember = false;
        driver.Settle();

        await Assert.That(Seen(driver, "disabled")).IsEqualTo((false, false));
    }

    [Test]
    public async Task States_forced_on_a_component_type_reach_only_that_type()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
            {
                using (ui.Box(forceStates: InputStates.Pressed, forceStatesOn: typeof(StateProbe)))
                    ui.StateProbe(name: "named", id: "named");
                using (ui.Box(forceStates: InputStates.Pressed, forceStatesOn: typeof(Button)))
                    ui.StateProbe(name: "other", id: "other");
            }
        });

        await Assert.That(Seen(driver, "named")).IsEqualTo((false, true));
        await Assert.That(Seen(driver, "other")).IsEqualTo((false, false));
    }

    [Test]
    public async Task A_component_type_forced_disabled_takes_no_click()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(forceStates: InputStates.Disabled, forceStatesOn: typeof(Button)))
                ui.Button(
                    text: "Save",
                    onClick: Callback.Of(clicks, static c => c.Value++),
                    id: "save"
                );
        });

        driver.Click(driver.Get("save"));

        await Assert.That(clicks.Value).IsEqualTo(0);
    }

    [Test]
    public async Task A_component_reading_the_views_size_renders_again_as_it_changes()
    {
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box())
                    ui.ViewSizeProbe();
            },
            new Size(400, 300)
        );
        var before = driver.GetText("400");

        driver.View.Size = new Size(500, 300);
        driver.Settle();

        await Assert.That(before).IsNotNull();
        await Assert.That(driver.GetText("500")).IsNotNull();
    }

    [Test]
    public async Task A_component_learns_it_is_hidden_as_an_ancestor_or_its_caller_hides_it_and_shown_again()
    {
        var gate = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
            {
                using (ui.Box(hidden: gate.Remember))
                using (ui.Box())
                    ui.ShownProbe(name: "under");
                ui.ShownProbe(name: "own", collapsed: gate.Count == 1);
            }
        });
        await Assert
            .That((ShownProbe.Last("under"), ShownProbe.Last("own")))
            .IsEqualTo((true, true));

        gate.Remember = true;
        gate.Count = 1;
        driver.Settle();
        await Assert
            .That((ShownProbe.Last("under"), ShownProbe.Last("own")))
            .IsEqualTo((false, false));

        gate.Remember = false;
        gate.Count = 0;
        driver.Settle();
        await Assert
            .That((ShownProbe.Last("under"), ShownProbe.Last("own")))
            .IsEqualTo((true, true));
    }

    [Test]
    public async Task A_component_learns_which_node_inside_it_holds_the_focus_as_focus_moves_between_them()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
            {
                ui.FocusedProbe(name: "moves");
                using (ui.Box(width: 20, height: 20, focusable: true, id: "outside")) { }
            }
        });

        driver.Click(driver.Get("first"));
        await Assert.That(FocusedProbe.Last("moves")).IsEqualTo("first");
        driver.Click(driver.Get("second"));
        await Assert.That(FocusedProbe.Last("moves")).IsEqualTo("second");
        driver.Click(driver.Get("outside"));
        await Assert.That(FocusedProbe.Last("moves")).IsNull();
    }

    [Test]
    public async Task Hover_and_press_focus_reach_the_component_that_owns_the_root()
    {
        using var driver = UiDriver.Render(ui =>
            ui.StateProbe(name: "probe", focusable: true, id: "probe")
        );
        driver.Hover(driver.Get("probe"));
        await Assert.That(StateProbe.Last("probe").Hovered).IsTrue();

        driver.Click(driver.Get("probe"));
        var seen = StateProbe.Last("probe");
        await Assert.That(seen.Focused).IsTrue();
        await Assert.That(seen.FocusVisible).IsFalse();
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("probe"));
    }

    [Test]
    public async Task A_hovered_button_looks_hovered_over_its_focus_ring_and_a_hovered_field_looks_focused()
    {
        using var driver = UiDriver.Render(ui =>
            ui.StateProbe(name: "look", focusable: true, id: "look")
        );

        driver.Tab();
        driver.Hover(driver.Get("look"));
        var seen = StateProbe.Last("look");

        await Assert
            .That((seen.FocusVisible, seen.Hovered, seen.Look, seen.FieldLook))
            .IsEqualTo((true, true, ComponentState.Hover, ComponentState.Focused));
    }

    [Test]
    public async Task A_theme_change_rerenders_what_read_the_theme()
    {
        var model = new Model { Name = "n" };
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box())
            {
                ui.ThemedSwatch(id: "swatch");
                ui.Label(model: model);
            }
        });

        driver.View.Context.Theme = Themes.Light;
        driver.Frame();

        await Assert.That(driver.Stats.RendersOf<ThemedSwatch>()).IsEqualTo(1);
        await Assert.That(driver.Stats.RendersOf<Label>()).IsEqualTo(0);
        await Assert.That(ThemedSwatch.Drawn).IsEqualTo(Themes.Light.Color(ColorToken.BgRaised));
    }

    static (bool Disabled, bool Pressed) Seen(UiDriver driver, string id)
    {
        driver.Get<StateProbe>(id);
        var last = StateProbe.Last(id);
        return (last.Disabled, last.Pressed);
    }
}

public readonly record struct ViewSizeProbeProps;

/// <summary>Writes the view's width as it renders.</summary>
public sealed class ViewSizeProbe : Component<ViewSizeProbeProps>
{
    protected override void Render(Ui ui, in ViewSizeProbeProps p) =>
        ui.Text(ui.Format($"{ui.ViewSize.Width:0}"));
}

public readonly record struct StateProbeProps
{
    public string? Name { get; init; }

    public bool Focusable { get; init; }
}

/// <summary>Records the input states each render sees.</summary>
public sealed class StateProbe : Component<StateProbeProps>
{
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, States> Seen = new();

    public readonly record struct States(
        bool Disabled,
        bool Pressed,
        bool Hovered,
        bool Focused,
        bool FocusVisible,
        ComponentState Look,
        ComponentState FieldLook
    );

    public static States Last(string name) =>
        Seen.TryGetValue(name, out var states) ? states : default;

    protected override void Render(Ui ui, in StateProbeProps p)
    {
        Seen[p.Name!] = new States(
            ui.IsDisabled,
            ui.IsPressed,
            ui.IsHovered,
            ui.IsFocused,
            ui.IsFocusVisible,
            ui.ComponentState,
            ui.FieldState
        );
        using (
            ui.Box(
                width: 20,
                height: 20,
                focusable: p.Focusable ? true : null,
                background: Color.FromRgb(5, 5, 5)
            )
        ) { }
    }
}

public readonly record struct ThemedSwatchProps
{
    public int Tag { get; init; }
}

/// <summary>Paints a theme colour, so it reads the theme.</summary>
public sealed class ThemedSwatch : Component<ThemedSwatchProps>
{
    public static Color Drawn;

    protected override void Render(Ui ui, in ThemedSwatchProps p)
    {
        var color = ui.Theme.Color(ColorToken.BgRaised);
        Drawn = color;
        using (ui.Box(width: 10, height: 10, background: color)) { }
    }
}

public readonly record struct ShownProbeProps
{
    public string? Name { get; init; }
}

/// <summary>Records whether each render saw itself shown, through a component its root belongs to.</summary>
public sealed class ShownProbe : Component<ShownProbeProps>
{
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> Seen = new();
    static readonly Model Shown = new() { Name = "shown" };

    public static bool Last(string name) => Seen.TryGetValue(name, out var shown) && shown;

    protected override void Render(Ui ui, in ShownProbeProps p)
    {
        Seen[p.Name!] = ui.IsShown;
        ui.Label(model: Shown);
    }
}

public readonly record struct FocusedProbeProps
{
    public string? Name { get; init; }
}

/// <summary>Records the id of the node inside it that each render saw holding the focus.</summary>
public sealed class FocusedProbe : Component<FocusedProbeProps>
{
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string?> Seen =
        new();

    public static string? Last(string name) => Seen.TryGetValue(name, out var id) ? id : null;

    protected override void Render(Ui ui, in FocusedProbeProps p)
    {
        Seen[p.Name!] = ui.FocusedWithin?.Id;
        using (ui.Box(gap: 4))
        {
            using (ui.Box(width: 20, height: 20, focusable: true, id: "first")) { }
            using (ui.Box(width: 20, height: 20, focusable: true, id: "second")) { }
        }
    }
}

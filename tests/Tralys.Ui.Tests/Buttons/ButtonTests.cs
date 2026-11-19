using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class ButtonTests
{
    static readonly IconGlyph Star = new(0xe176, "lucide");

    [Test]
    public async Task A_click_runs_onclick_and_a_release_off_it_or_a_right_press_clicks_nothing()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Button(
                    text: "Save",
                    onClick: Callback.Of(log, static l => l.Add("click")),
                    id: "save"
                );
                using (ui.Box(width: 40, height: 40, id: "away")) { }
            }
        });
        var save = driver.Get("save");

        driver.Click(save);
        driver.Click(save);
        var clicked = log.ToString();
        log.Items.Clear();
        var strayed = driver.Hold(save);
        strayed.MoveTo(driver.Get("away"));
        strayed.Release();
        var left = driver.Hold(save);
        driver.Hold(save, button: MouseButton.Right).Release();
        var duringLeft = log.ToString();
        left.Release();

        await Assert.That(clicked).IsEqualTo("click click");
        await Assert.That(duringLeft).IsEqualTo("");
        await Assert.That(log.ToString()).IsEqualTo("click");
    }

    [Test]
    public async Task A_finger_lifted_off_a_button_another_finger_rests_on_clicks_nothing()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
            ui.Button(text: "Save", onClick: Callback.Of(clicks, static c => c.Value++), id: "save")
        );
        var save = driver.Get("save");

        var first = driver.Hold(save, PointerKind.Touch);
        var second = driver.Hold(save, PointerKind.Touch);
        first.MoveBy(new Vector2(200f, 0f));
        first.Release();

        await Assert.That(clicks.Value).IsEqualTo(0);
        second.Release();
    }

    [Test]
    public async Task Enter_clicks_on_every_repeat_and_space_once_a_hold_as_it_comes_up_and_neither_with_a_modifier()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
            ui.Button(text: "Save", onClick: Callback.Of(clicks, static c => c.Value++), id: "save")
        );
        driver.Focus(driver.Get("save"));

        driver.KeyDown(Key.Enter);
        driver.KeyDown(Key.Enter, repeat: true);
        driver.KeyDown(Key.Enter, repeat: true);
        driver.KeyUp(Key.Enter);
        var entered = clicks.Value;
        driver.KeyDown(Key.Space);
        driver.KeyDown(Key.Space, repeat: true);
        driver.KeyDown(Key.Space, repeat: true);
        var spaceHeld = clicks.Value;
        driver.KeyUp(Key.Space);
        var spaced = clicks.Value;
        var controlEnter = driver.KeyDown(Key.Enter, ModifierKeys.Control);
        driver.KeyUp(Key.Enter, ModifierKeys.Control);
        var shiftSpace = driver.KeyDown(Key.Space, ModifierKeys.Shift);
        driver.KeyUp(Key.Space, ModifierKeys.Shift);

        await Assert.That(entered).IsEqualTo(3);
        await Assert.That(spaceHeld).IsEqualTo(3);
        await Assert.That(spaced).IsEqualTo(4);
        await Assert.That((controlEnter, shiftSpace)).IsEqualTo((false, false));
        await Assert.That(clicks.Value).IsEqualTo(4);
    }

    [Test]
    public async Task A_button_given_an_action_is_pressed_by_its_key_and_its_gamepad_button()
    {
        var clicks = new Counter();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.ActionScope())
                ui.Button(
                    text: "Back",
                    action: UiAction.Back,
                    onClick: Callback.Of(clicks, static c => c.Value++)
                );
        });

        driver.Press(Key.Escape);
        driver.Press(GamepadButton.East);

        await Assert.That(driver.Focused).IsNull();
        await Assert.That(clicks.Value).IsEqualTo(2);
    }

    [Test]
    public async Task A_clicked_button_shows_no_focus_ring_and_a_tabbed_one_does()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 8))
            {
                ui.Button(text: "One", id: "one");
                ui.Button(text: "Two", id: "two");
            }
        });
        var one = driver.Get("one");
        var two = driver.Get("two");

        driver.Click(one);
        var clicked = (driver.Focused == one, ButtonLooks.HasRing(one));
        driver.Tab();

        await Assert.That(clicked).IsEqualTo((true, false));
        await Assert.That(driver.Focused).IsSameReferenceAs(two);
        await Assert.That(ButtonLooks.HasRing(two)).IsTrue();
    }

    [Test]
    public async Task Enter_runs_its_command_once_with_its_parameter_and_a_new_parameter_while_shown_is_asked_again()
    {
        var ran = new List<string>();
        var command = new Command<string>(ran.Add, static p => p is not (null or "locked"));
        var parameter = "draft";
        using var driver = UiDriver.Render(ui =>
            ui.Button(text: "Save", onClick: command.With(parameter), id: "save")
        );
        var save = driver.Get("save");

        driver.Focus(save);
        driver.Press(Key.Enter);
        parameter = "locked";
        driver.Rerender();
        var locked = ButtonLooks.IsDisabled(save);
        parameter = "final";
        driver.Rerender();

        await Assert.That(ran).IsEquivalentTo(["draft"]);
        await Assert.That(locked).IsTrue();
        await Assert.That(ButtonLooks.IsDisabled(save)).IsFalse();
    }

    [Test]
    public async Task Every_button_kind_asks_and_runs_its_command_with_its_parameter()
    {
        var ran = new List<string>();
        var command = new Command<string>(ran.Add, static p => p is not (null or "locked"));
        var locked = false;
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(align: Align.Start, gap: 8))
                {
                    ui.IconButton(
                        icon: Star,
                        onClick: command.With(locked ? "locked" : "icon"),
                        id: "icon"
                    );
                    ui.CloseButton(onClick: command.With(locked ? "locked" : "close"), id: "close");
                    ui.ImageButton(
                        source: "rest",
                        width: 32,
                        height: 32,
                        onClick: command.With(locked ? "locked" : "image"),
                        id: "image"
                    );
                    ui.Toggle(
                        text: "Bold",
                        onClick: command.With(locked ? "locked" : "toggle"),
                        id: "toggle"
                    );
                    ui.RepeatButton(
                        text: "More",
                        onClick: command.With(locked ? "locked" : "repeat"),
                        id: "repeat"
                    );
                    ui.SplitButton(
                        text: "Sell",
                        onClick: command.With(locked ? "locked" : "split"),
                        id: "split"
                    );
                }
            })
            .WithIcons();
        var buttons = new List<Node>();
        foreach (var id in new[] { "icon", "close", "image", "toggle", "repeat" })
            buttons.Add(driver.Get(id));
        buttons.Add(Nodes.Find(driver.All<SplitButtonPart>()[0], NodeKind.Box));

        foreach (var button in buttons)
            driver.Click(button);
        locked = true;
        driver.Rerender();

        await Assert.That(string.Join(" ", ran)).IsEqualTo("icon close image toggle repeat split");
        await Assert.That(buttons.All(ButtonLooks.IsDisabled)).IsTrue();
    }

    [Test]
    public async Task A_command_that_cant_run_dims_and_disables_its_button_until_it_can_and_a_new_command_is_followed()
    {
        var open = new Switches();
        var runs = new Counter();
        var command = new Command(() => runs.Value++, () => open.A);
        var current = command;
        using var driver = UiDriver.Render(ui =>
            ui.Button(text: "Save", onClick: current, id: "save")
        );
        var save = driver.Get("save");

        driver.Click(save);
        var refused = (runs.Value, save.Visual.Opacity, ButtonLooks.IsDisabled(save));
        open.A = true;
        driver.Settle();
        var allowed = (save.Visual.Opacity, ButtonLooks.IsDisabled(save));
        driver.Click(save);
        current = new Command(static () => { }, static () => false);
        driver.Rerender();

        await Assert.That(refused).IsEqualTo((0, (float?)0.5f, true));
        await Assert.That(allowed).IsEqualTo(((float?)null, false));
        await Assert.That(runs.Value).IsEqualTo(1);
        await Assert.That(ButtonLooks.IsDisabled(save)).IsTrue();
    }

    [Test]
    public async Task An_async_command_greys_its_button_until_its_task_completes()
    {
        var handler = new PendingHandler();
        var command = new Command(handler.Run);
        using var driver = UiDriver.Render(ui =>
            ui.Button(text: "Save", onClick: command, id: "save")
        );
        var save = driver.Get("save");

        driver.Click(save);
        var running = ButtonLooks.IsDisabled(save);
        handler.Finish();
        driver.Settle();

        await Assert.That(handler.Runs).IsEqualTo(1);
        await Assert.That(running).IsTrue();
        await Assert.That(ButtonLooks.IsDisabled(save)).IsFalse();
    }

    [Test]
    public async Task A_command_that_throws_reaches_the_fault_handler_once_and_its_button_lets_go()
    {
        var faults = new List<Exception>();
        var command = new Command(Boom);
        using var driver = UiDriver.Render(ui =>
            ui.Button(text: "Save", onClick: command, id: "save")
        );
        driver.View.Context.OnFault = faults.Add;
        var save = driver.Get("save");

        driver.Click(save);

        await Assert.That(faults.Count).IsEqualTo(1);
        await Assert
            .That(save.States & (InputStates.Pressed | InputStates.Capturing))
            .IsEqualTo(InputStates.None);

        static void Boom() => throw new InvalidOperationException("boom");
    }

    [Test]
    public async Task A_button_that_closes_its_popover_runs_its_command_then_closes_it()
    {
        var runs = new Counter();
        var command = new Command(() => runs.Value++);
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Popover(
                    defaultOpen: true,
                    content: Slot.Of(
                        command,
                        static (ui, c) =>
                            ui.Button(
                                text: "Withdraw",
                                onClick: c,
                                closesPopover: true,
                                id: "withdraw"
                            )
                    )
                )
            )
                ui.Text("Gold");
        });
        var withdraw = driver.Get("withdraw");

        driver.Click(withdraw);

        await Assert.That(runs.Value).IsEqualTo(1);
        await Assert.That(withdraw.IsMounted).IsFalse();
    }

    [Test]
    public async Task An_active_button_wears_its_active_look_with_its_hover_fill()
    {
        var active = false;
        using var driver = UiDriver.Render(ui =>
            ui.Button(
                text: "Tab",
                scheme: ColorScheme.Secondary,
                variant: ComponentVariant.Outlined,
                active: active,
                id: "tab"
            )
        );
        var tab = driver.Get("tab");

        driver.Hover(tab);
        active = true;
        driver.Rerender();

        await Assert
            .That(tab.Visual.Background)
            .IsEqualTo(
                (Paint)
                    Themes
                        .Dark.Colors(
                            ColorScheme.Primary,
                            ComponentVariant.Filled,
                            ComponentState.Hover
                        )
                        .Background
            );
    }

    [Test]
    public async Task A_button_drawn_filled_primary_wears_the_skins_primary_art_and_its_hover_art()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start, gap: 8))
            {
                ui.Button(
                    text: "Active",
                    scheme: ColorScheme.Secondary,
                    variant: ComponentVariant.Ghost,
                    active: true,
                    id: "active"
                );
                ui.Button(text: "Outlined", variant: ComponentVariant.Outlined, id: "outlined");
            }
        });
        driver.View.Context.Skin = new Skin([
            new(
                SkinPart.ButtonPrimary,
                new SkinPartImage(new TestTexture(24, 24), new Thickness(8))
            ),
            new(
                SkinPart.ButtonPrimaryHover,
                new SkinPartImage(new TestTexture(24, 24), new Thickness(8))
            ),
        ]);
        driver.Settle();
        var active = driver.Get("active");

        var resting = (ButtonLooks.Images(active), active.Visual.Background);
        driver.Hover(active);

        await Assert.That(resting).IsEqualTo((1, default(Paint)));
        await Assert.That(ButtonLooks.Images(active)).IsEqualTo(2);
        await Assert.That(ButtonLooks.Images(driver.Get("outlined"))).IsEqualTo(0);
    }

    [Test]
    public async Task A_flash_pulses_outline_and_text_toward_the_accent_follows_the_theme_and_stops_at_rest()
    {
        var flashing = false;
        using var driver = UiDriver.Render(ui =>
            ui.Button(
                text: "Review",
                scheme: ColorScheme.Secondary,
                variant: ComponentVariant.Outlined,
                flashing: flashing,
                id: "review"
            )
        );
        var review = driver.Get("review");
        var dark = Themes.Dark;
        var rest = dark.Colors(
            ColorScheme.Secondary,
            ComponentVariant.Outlined,
            ComponentState.Normal
        );
        var accent = dark.Color(ColorToken.BorderAccent);

        flashing = true;
        driver.View.RerenderRoot();
        driver.Frame(0f);
        driver.Frame(0.25f);
        var halfway = (review.Visual.BorderPaint, ButtonLooks.TextColor(review));
        driver.View.Context.Theme = Themes.Light;
        driver.Frame(0.25f);
        var peak = review.Visual.BorderPaint;
        flashing = false;
        driver.Rerender();

        await Assert
            .That(halfway)
            .IsEqualTo(
                (
                    (Paint)ColorBlend.Blend(rest.Border, accent, 0.5f),
                    (Paint)ColorBlend.Blend(rest.Foreground, accent, 0.5f)
                )
            );
        await Assert.That(peak).IsEqualTo((Paint)Themes.Light.Color(ColorToken.BorderAccent));
        await Assert
            .That(review.Visual.BorderPaint)
            .IsEqualTo(
                (Paint)
                    Themes
                        .Light.Colors(
                            ColorScheme.Secondary,
                            ComponentVariant.Outlined,
                            ComponentState.Normal
                        )
                        .Border
            );
    }

    [Test]
    public async Task A_link_is_bare_text_in_fg_that_lights_toward_the_accent_and_greys_out_while_disabled()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Button(
                    text: "Read more",
                    variant: ComponentVariant.Link,
                    iconLeft: Star,
                    id: "link"
                );
                ui.Button(
                    text: "Status",
                    variant: ComponentVariant.Link,
                    scheme: ColorScheme.Primary,
                    id: "primary"
                );
                ui.Button(text: "Off", variant: ComponentVariant.Link, disabled: true, id: "off");
            }
        });
        var t = Themes.Dark;
        var m = t.Dimensions(Density.Pointer).Size(ComponentSize.Md);
        var link = driver.Get("link");
        var glyph = ButtonLooks.Glyph(link);
        var text = driver.GetText("Read more");
        var bare = (
            link.Visual.Background,
            link.Visual.BorderPaint,
            link.Layout.Padding,
            link.Inherit.FontWeight,
            text.ViewBounds.X - glyph.ViewBounds.Right,
            glyph.Bounds.Width
        );
        var rest = ButtonLooks.TextColor(link);
        driver.Hover(link);
        var hovered = ButtonLooks.TextColor(link);
        var press = driver.Hold(link);
        var pressed = ButtonLooks.TextColor(link);
        press.Release();
        var primary = driver.Get("primary");
        var primaryRest = ButtonLooks.TextColor(primary);
        driver.Hover(primary);

        await Assert
            .That(bare)
            .IsEqualTo(
                (
                    default(Paint),
                    default(Paint),
                    default(Thickness),
                    t.Weight(WeightToken.Link),
                    4f,
                    m.IconSize - 2f
                )
            );
        await Assert
            .That((rest, hovered, pressed))
            .IsEqualTo(
                (
                    (Paint)t.Color(ColorToken.Fg),
                    (Paint)t.Color(ColorToken.FgAccent),
                    (Paint)t.Color(ColorToken.FgAccentHover)
                )
            );
        await Assert
            .That((primaryRest, ButtonLooks.TextColor(primary)))
            .IsEqualTo(
                ((Paint)t.Color(ColorToken.FgAccent), (Paint)t.Color(ColorToken.FgAccentHover))
            );
        await Assert
            .That(ButtonLooks.TextColor(driver.Get("off")))
            .IsEqualTo((Paint)t.Color(ColorToken.FgDisabled));
        await Assert.That(driver.Get("off").Visual.Opacity).IsNull();
    }

    [Test]
    public async Task Icons_sit_either_side_of_the_text_a_gap_away_at_its_font_size_and_one_alone_is_centred()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Button(text: "Filter", iconLeft: Star, id: "left");
                ui.Button(text: "Next", iconRight: Star, id: "right");
                ui.Button(iconLeft: Star, width: 120, id: "alone");
            }
        });
        var m = Themes.Dark.Dimensions(Density.Pointer).Size(ComponentSize.Md);
        var left = driver.Get("left");
        var leftGlyph = ButtonLooks.Glyph(left);
        var leftText = driver.GetText("Filter").ViewBounds;
        var right = driver.Get("right");
        var rightGlyph = ButtonLooks.Glyph(right).ViewBounds;
        var rightText = driver.GetText("Next").ViewBounds;
        var alone = driver.Get("alone");
        var aloneGlyph = ButtonLooks.Glyph(alone).ViewBounds;

        await Assert
            .That((leftGlyph.Bounds.Width, leftText.X - leftGlyph.ViewBounds.Right))
            .IsEqualTo((m.FontSize, m.Gap));
        await Assert.That(rightGlyph.X - rightText.Right).IsEqualTo(m.Gap);
        await Assert
            .That(aloneGlyph.X + aloneGlyph.Width / 2f)
            .IsEqualTo(alone.ViewBounds.X + alone.ViewBounds.Width / 2f)
            .Within(0.5f);
    }

    [Test]
    public async Task A_button_takes_its_callers_padding_min_width_and_a_height_past_its_rung()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Button(
                    text: "Max",
                    variant: ComponentVariant.Text,
                    padding: default(Thickness),
                    id: "bare"
                );
                ui.Button(text: "Ok", minWidth: 120, height: 60, id: "sized");
            }
        });
        var bare = driver.Get("bare").ViewBounds;
        var text = driver.GetText("Max").ViewBounds;
        var sized = driver.Get("sized").ViewBounds;

        await Assert.That(text.X - bare.X).IsEqualTo(1f);
        await Assert.That((sized.Width, sized.Height)).IsEqualTo((120f, 60f));
    }

    [Test]
    public async Task A_button_sets_its_content_at_the_start_or_spreads_it_to_its_ends()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
            {
                ui.Button(text: "Start", width: 200, justify: Justify.Start, id: "start");
                ui.Button(
                    text: "Spread",
                    iconRight: Star,
                    width: 200,
                    justify: Justify.SpaceBetween,
                    id: "spread"
                );
            }
        });
        var m = Themes.Dark.Dimensions(Density.Pointer).Size(ComponentSize.Md);
        var start = driver.Get("start").ViewBounds;
        var spread = driver.Get("spread");
        var glyph = ButtonLooks.Glyph(spread).ViewBounds;

        await Assert
            .That(driver.GetText("Start").ViewBounds.X - start.X)
            .IsEqualTo(m.PaddingX.Left);
        await Assert
            .That(
                (
                    driver.GetText("Spread").ViewBounds.X - spread.ViewBounds.X,
                    spread.ViewBounds.Right - glyph.Right
                )
            )
            .IsEqualTo((m.PaddingX.Left, m.PaddingX.Right));
    }

    [Test]
    public async Task Buttons_toggles_and_icon_buttons_stand_their_rungs_height_in_every_theme_and_density()
    {
        var wrong = new List<string>();
        var sizes = Enum.GetValues<ComponentSize>();
        foreach (var theme in Themes.All)
        foreach (var density in Enum.GetValues<Density>())
        {
            using var driver = UiDriver.Render(
                ui =>
                {
                    using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8))
                        foreach (var size in sizes)
                            using (ui.Box(align: Align.Start, gap: 8, key: (long)size))
                            {
                                ui.Button(text: "Save", size: size, id: $"save-{size}");
                                ui.Button(
                                    text: "Filter",
                                    iconLeft: Star,
                                    size: size,
                                    id: $"filter-{size}"
                                );
                                ui.Toggle(text: "Body probe", size: size, id: $"toggle-{size}");
                                ui.Toggle(icon: Star, size: size, id: $"icon-toggle-{size}");
                                ui.IconButton(icon: Star, size: size, id: $"icon-{size}");
                            }
                },
                size: new Size(1600f, 900f),
                theme: theme,
                density: density
            );
            foreach (var size in sizes)
            {
                var m = theme.Dimensions(density).Size(size);
                foreach (var name in new[] { "save", "filter", "toggle", "icon-toggle", "icon" })
                {
                    var bounds = driver.Get($"{name}-{size}").Bounds;
                    if (MathF.Abs(bounds.Height - m.Height) > 0.01f)
                        wrong.Add(
                            $"{theme.Name} {density} {name}-{size}: {bounds.Height} not {m.Height}"
                        );
                }
                var icon = driver.Get($"icon-{size}");
                if (icon.Bounds.Width != icon.Bounds.Height)
                    wrong.Add($"{theme.Name} {density} icon-{size}: not square");
                if (ButtonLooks.Glyph(icon).Bounds.Width != m.IconSize)
                    wrong.Add($"{theme.Name} {density} icon-{size}: glyph not {m.IconSize}");
            }
        }

        await Assert.That(wrong).IsEmpty();
    }
}

internal static class ButtonLooks
{
    public static bool IsDisabled(Node root) => (root.States & InputStates.Disabled) != 0;

    public static bool HasRing(Node root, Theme? theme = null)
    {
        var ring = (Paint)(theme ?? Themes.Dark).Color(ColorToken.BorderFocus);
        return Any(root, n => n.Kind == NodeKind.Box && n.Visual.BorderPaint == ring);
    }

    public static int Images(Node root) => Count(root, static n => n.Kind == NodeKind.Image);

    public static Paint TextColor(Node root) => Nodes.Find(root, NodeKind.Text).Inherit.Color;

    public static Node Glyph(Node root) =>
        First(root, n => n.Kind == NodeKind.Box && !n.Layout.Width.IsAuto && Has(n, NodeKind.Text))
        ?? throw new InvalidOperationException("No glyph.");

    static bool Has(Node node, NodeKind kind)
    {
        foreach (var child in node.Children)
            if (child.Kind == kind)
                return true;
        return false;
    }

    static bool Any(Node node, Func<Node, bool> match) => First(node, match) is not null;

    static Node? First(Node node, Func<Node, bool> match)
    {
        foreach (var child in node.Children)
        {
            if (match(child))
                return child;
            if (First(child, match) is { } found)
                return found;
        }
        return null;
    }

    static int Count(Node node, Func<Node, bool> match)
    {
        var count = 0;
        foreach (var child in node.Children)
            count += (match(child) ? 1 : 0) + Count(child, match);
        return count;
    }
}

internal sealed class PendingHandler : System.Threading.Tasks.Sources.IValueTaskSource
{
    System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore<bool> _core;

    public int Runs { get; private set; }

    public ValueTask Run()
    {
        _core.Reset();
        Runs++;
        return new ValueTask(this, _core.Version);
    }

    public void Finish() => _core.SetResult(true);

    public void GetResult(short token) => _core.GetResult(token);

    public System.Threading.Tasks.Sources.ValueTaskSourceStatus GetStatus(short token) =>
        _core.GetStatus(token);

    public void OnCompleted(
        Action<object?> continuation,
        object? state,
        short token,
        System.Threading.Tasks.Sources.ValueTaskSourceOnCompletedFlags flags
    ) => _core.OnCompleted(continuation, state, token, flags);
}

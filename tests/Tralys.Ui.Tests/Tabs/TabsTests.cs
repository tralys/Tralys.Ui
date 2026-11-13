using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class TabsTests
{
    [Test]
    public async Task A_left_press_selects_as_it_lands_focusing_the_tab_and_a_click_on_one_the_app_moved_off_selects_it_again()
    {
        var log = new Log();
        UiKey value = "stats";
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Tabs(value: value, onValueChange: TabLog.Of(log)))
            using (ui.TabsList())
            {
                ui.TabsTab(value: "stats", text: "Stats", id: "stats");
                ui.TabsTab(value: "skills", text: "Skills", id: "skills");
            }
        });
        var skills = driver.Get("skills");

        driver.Click(skills, MouseButton.Right);
        var press = driver.Hold(skills);
        var landed = (log.ToString(), driver.Focused == skills, TabLog.Selected(driver, "skills"));
        press.Release();
        var released = log.ToString();
        value = "skills";
        driver.Rerender();
        value = "stats";
        driver.Rerender();
        driver.Click(skills);

        await Assert.That(landed).IsEqualTo(("skills", true, false));
        await Assert.That(released).IsEqualTo("skills");
        await Assert.That(log.ToString()).IsEqualTo("skills skills");
        await Assert.That(driver.Focused).IsEqualTo(skills);
    }

    [Test]
    public async Task An_unkeyed_value_shows_no_tab_selected_and_tab_still_reaches_the_strip()
    {
        UiKey value = default;
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(width: 40, height: 20, focusable: true, id: "before")) { }
            using (ui.Tabs(value: value))
            using (ui.TabsList())
            {
                ui.TabsTab(value: "stats", text: "Stats", id: "stats");
                ui.TabsTab(value: "skills", text: "Skills", id: "skills");
            }
        });
        driver.Focus(driver.Get("before"));

        driver.Tab();

        await Assert
            .That((TabLog.Selected(driver, "stats"), TabLog.Selected(driver, "skills")))
            .IsEqualTo((false, false));
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("stats"));
    }

    [Test]
    public async Task A_strip_scrolls_through_its_callers_handle_and_takes_the_wheel_from_its_callers_handler()
    {
        var strip = new Strip();
        using var driver = UiDriver.Render(
            ui =>
            {
                strip.Scroll = ui.ScrollHandle();
                using (ui.Tabs(width: 120))
                using (
                    ui.TabsList(
                        scroll: strip.Scroll,
                        onWheel: Handler<WheelEvent>.Of(
                            strip,
                            static (s, ref e) =>
                            {
                                e.PreventDefault();
                                s.Scroll.ScrollTo(37f, null);
                            }
                        )
                    )
                )
                    for (var i = 0; i < 8; i++)
                        ui.TabsTab(value: i, text: "Tab", key: i, id: i == 0 ? "first" : null);
            },
            new Size(300, 100)
        );
        driver.View.Context.SmoothScrolling = false;
        var overflows = strip.Scroll.Extent.Width > strip.Scroll.Viewport.Width;

        driver.Wheel(driver.Get("first"), new System.Numerics.Vector2(0f, -1f));

        await Assert.That((overflows, strip.Scroll.Offset.X)).IsEqualTo((true, 37f));
    }

    sealed class Strip
    {
        public ScrollHandle Scroll = null!;
    }

    [Test]
    public async Task The_arrows_on_a_focused_tab_move_the_focus_and_the_selection_wrapping_and_home_and_end_reach_the_ends()
    {
        using var driver = UiDriver.Render(static ui => TabLog.Three(ui));
        driver.Focus(driver.Get("1"));
        driver.Press(Key.End, ModifierKeys.Alt);
        var steps = new List<string> { $"{TabLog.SelectedOf(driver)}/{driver.Focused?.Id}" };

        foreach (
            var key in (Key[])[Key.Right, Key.Down, Key.Right, Key.Left, Key.Up, Key.End, Key.Home]
        )
        {
            driver.Press(key);
            steps.Add($"{TabLog.SelectedOf(driver)}/{driver.Focused?.Id}");
        }

        await Assert
            .That(string.Join(", ", steps))
            .IsEqualTo("1/1, 2/2, 3/3, 1/1, 3/3, 2/2, 3/3, 1/1");
        await Assert.That(TabLog.Ringed(driver.Focused!)).IsTrue();
    }

    [Test]
    public async Task Ctrl_tab_selects_and_focuses_the_next_tab_and_ctrl_shift_tab_the_previous_wrapping_even_from_a_panel()
    {
        using var driver = UiDriver.Render(static ui => TabLog.Three(ui, panels: true));
        driver.Focus(driver.Get("1"));
        var steps = new List<string>();

        foreach (var shift in (bool[])[false, false, false, true, true])
        {
            driver.Press(
                Key.Tab,
                ModifierKeys.Control | (shift ? ModifierKeys.Shift : ModifierKeys.None)
            );
            steps.Add(TabLog.SelectedOf(driver));
        }
        var focused = driver.Focused?.Id;
        driver.Focus(driver.Get("apply-2"));
        driver.Press(Key.Tab, ModifierKeys.Control);

        await Assert.That(string.Join(", ", steps)).IsEqualTo("2, 3, 1, 3, 2");
        await Assert.That(focused).IsEqualTo("2");
        await Assert.That((TabLog.SelectedOf(driver), driver.Focused?.Id)).IsEqualTo(("3", "3"));
    }

    [Test]
    public async Task The_arrows_in_a_panel_select_nothing_and_home_there_selects_the_first_tab_leaving_the_focus()
    {
        using var driver = UiDriver.Render(static ui =>
            TabLog.Three(ui, panels: true, defaultValue: "2")
        );
        var apply = driver.Get("apply-2");
        driver.Focus(apply);

        driver.Press(Key.Left);
        driver.Press(Key.Right);
        var kept = (TabLog.SelectedOf(driver), driver.Focused == apply);
        driver.Press(Key.Home);

        await Assert.That(kept).IsEqualTo(("2", true));
        await Assert.That(TabLog.SelectedOf(driver)).IsEqualTo("1");
        await Assert.That(driver.Focused?.Id).IsNotEqualTo("1");
    }

    [Test]
    public async Task The_arrows_and_ctrl_tab_pass_over_a_disabled_or_collapsed_tab()
    {
        using var driver = UiDriver.Render(static ui =>
        {
            using (ui.Tabs())
            using (ui.TabsList())
            {
                ui.TabsTab(value: "stats", text: "Stats", id: "stats");
                ui.TabsTab(value: "locked", text: "Locked", disabled: true, id: "locked");
                ui.TabsTab(value: "hidden", text: "Hidden", collapsed: true, id: "hidden");
                ui.TabsTab(value: "logs", text: "Logs", id: "logs");
            }
        });
        driver.Focus(driver.Get("stats"));

        driver.Press(Key.Right);
        var arrowed = (TabLog.SelectedOf(driver), driver.Focused?.Id);
        driver.Press(Key.Tab, ModifierKeys.Control);

        await Assert.That(arrowed).IsEqualTo(("logs", "logs"));
        await Assert
            .That((TabLog.SelectedOf(driver), driver.Focused?.Id))
            .IsEqualTo(("stats", "stats"));
    }

    [Test]
    public async Task Tab_lands_on_the_selected_tab_and_leaves_the_list_and_focus_tabbing_onto_an_unselected_tab_selects_it()
    {
        var log = new Log();
        var locked = false;
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (ui.Box(width: 20, height: 20, focusable: true, id: "before")) { }
                using (ui.Tabs(value: "2", onValueChange: TabLog.Of(log)))
                using (ui.TabsList())
                {
                    ui.TabsTab(value: "1", text: "One", id: "1");
                    ui.TabsTab(value: "2", text: "Two", disabled: locked, id: "2");
                    ui.TabsTab(value: "3", text: "Three", id: "3");
                }
                using (ui.Box(width: 20, height: 20, focusable: true, id: "after")) { }
            }
        });
        driver.Focus(driver.Get("before"));

        driver.Tab();
        var tabbed = (driver.Focused?.Id, log.ToString());
        driver.Tab();
        var left = driver.Focused?.Id;
        locked = true;
        driver.Rerender();
        driver.Focus(driver.Get("before"));
        driver.Tab();

        await Assert.That(tabbed).IsEqualTo(("2", ""));
        await Assert.That(left).IsEqualTo("after");
        await Assert.That((driver.Focused?.Id, log.ToString())).IsEqualTo(("1", "1"));
    }

    [Test]
    public async Task Under_manual_activation_the_arrows_move_only_the_focus_and_enter_selects()
    {
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Tabs(
                    defaultValue: "1",
                    activationMode: TabActivation.Manual,
                    onValueChange: TabLog.Of(log)
                )
            )
            using (ui.TabsList())
            {
                ui.TabsTab(value: "1", text: "One", id: "1");
                ui.TabsTab(value: "2", text: "Two", id: "2");
            }
        });
        driver.Focus(driver.Get("1"));

        driver.Press(Key.Right);
        var moved = (driver.Focused?.Id, TabLog.SelectedOf(driver), log.ToString());
        driver.Press(Key.Enter);

        await Assert.That(moved).IsEqualTo(("2", "1", ""));
        await Assert.That((TabLog.SelectedOf(driver), log.ToString())).IsEqualTo(("2", "2"));
    }

    [Test]
    public async Task Enter_and_space_on_a_focused_unselected_tab_under_automatic_activation_pass_on_selecting_nothing()
    {
        var confirmed = new Counter();
        var log = new Log();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.ActionScope())
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                using (ui.Tabs(defaultValue: "2", onValueChange: TabLog.Of(log)))
                using (ui.TabsList())
                {
                    ui.TabsTab(value: "1", text: "One", id: "1");
                    ui.TabsTab(value: "2", text: "Two", id: "2");
                }
                ui.Button(
                    text: "Save",
                    action: UiAction.Confirm,
                    onClick: Callback.Of(confirmed, static c => c.Value++)
                );
            }
        });
        driver.Focus(driver.Get("1"));

        driver.Press(Key.Enter);
        driver.Press(Key.Space);

        await Assert.That(confirmed.Value).IsEqualTo(1);
        await Assert.That(log.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task A_given_value_wins_over_the_first_tab_and_shows_its_tab_again_after_its_tabs_are_swapped_out_and_back()
    {
        var other = false;
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Tabs(value: "3"))
            {
                using (ui.TabsList())
                {
                    if (other)
                    {
                        ui.TabsTab(value: "a", text: "A", id: "a");
                        ui.TabsTab(value: "b", text: "B", id: "b");
                    }
                    else
                    {
                        ui.TabsTab(value: "1", text: "One", id: "1");
                        ui.TabsTab(value: "2", text: "Two", id: "2");
                        ui.TabsTab(value: "3", text: "Three", id: "3");
                    }
                }
                using (ui.TabsPanel(value: "3"))
                    ui.Text("Third");
            }
        });

        var mounted = (TabLog.SelectedOf(driver), TabLog.Shows(driver, "Third"));
        other = true;
        driver.Rerender();
        var swapped = TabLog.SelectedOf(driver);
        other = false;
        driver.Rerender();

        await Assert.That(mounted).IsEqualTo(("3", true));
        await Assert.That(swapped).IsEqualTo("a");
        await Assert
            .That((TabLog.SelectedOf(driver), TabLog.Shows(driver, "Third")))
            .IsEqualTo(("3", true));
    }

    [Test]
    public async Task Removing_the_selected_tab_reports_the_tab_now_in_its_place_or_before_it_passing_a_disabled_one()
    {
        var log = new Log();
        var tabs = new ObservableList<string>();
        foreach (var name in (string[])["1", "2", "3", "4"])
            tabs.Add(name);
        UiKey value = "2";
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Tabs(value: value, onValueChange: TabLog.Of(log)))
            using (ui.TabsList())
                foreach (var name in tabs)
                    ui.TabsTab(value: name, text: name, disabled: name == "4", key: name, id: name);
        });

        tabs.Remove("2");
        driver.Settle();
        var inItsPlace = (log.ToString(), TabLog.SelectedOf(driver));
        value = "3";
        driver.Rerender();
        tabs.Remove("3");
        driver.Settle();

        await Assert.That(inItsPlace).IsEqualTo(("3", "3"));
        await Assert.That(log.ToString()).IsEqualTo("3 1");
    }

    [Test]
    public async Task Removing_the_selected_tab_and_the_one_after_it_together_reports_a_tab_still_there()
    {
        var log = new Log();
        var tabs = new ObservableList<string>();
        foreach (var name in (string[])["1", "2", "3", "4"])
            tabs.Add(name);
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Tabs(value: "2", onValueChange: TabLog.Of(log)))
            using (ui.TabsList())
                foreach (var name in tabs)
                    ui.TabsTab(value: name, text: name, key: name, id: name);
        });

        tabs.Remove("2");
        tabs.Remove("3");
        driver.Settle();

        await Assert.That(log.ToString()).IsEqualTo("4");
    }

    [Test]
    public async Task A_selected_tab_shows_its_label_and_underline_in_the_accent_or_its_own_and_a_hovered_one_lights()
    {
        var disabled = false;
        using var driver = UiDriver.Render(ui =>
        {
            var t = ui.Theme;
            using (ui.Tabs(value: "1", disabled: disabled))
            using (ui.TabsList())
            {
                ui.TabsTab(value: "1", text: "One", id: "1");
                ui.TabsTab(value: "2", text: "Two", id: "2");
                ui.TabsTab(value: "3", text: "Three", disabled: true, id: "3");
            }
            using (ui.Tabs(value: "own", position: Position.Absolute, top: 100))
            using (ui.TabsList())
                ui.TabsTab(
                    value: "own",
                    text: "Own",
                    accent: t.Color(ColorToken.BgDanger),
                    id: "own"
                );
        });
        var dark = Themes.Dark;
        Paint Token(ColorToken token) => dark.Color(token);

        var resting = (
            TabLog.Label(driver, "1"),
            TabLog.Underline(driver, "1"),
            TabLog.Label(driver, "2"),
            TabLog.Underline(driver, "2"),
            TabLog.Label(driver, "3"),
            TabLog.Label(driver, "own"),
            TabLog.Underline(driver, "own")
        );
        driver.Hover(driver.Get("2"));
        var hovered = TabLog.Label(driver, "2");
        driver.Hover(driver.Get("1"));
        var hoveredSelected = TabLog.Label(driver, "1");
        driver.Unhover();
        disabled = true;
        driver.Rerender();

        await Assert
            .That(resting)
            .IsEqualTo(
                (
                    Token(ColorToken.FgAccent),
                    Token(ColorToken.BorderAccent),
                    Token(ColorToken.Fg),
                    default(Paint),
                    Token(ColorToken.FgDisabled),
                    Token(ColorToken.BgDanger),
                    Token(ColorToken.BgDanger)
                )
            );
        await Assert
            .That((hovered, hoveredSelected))
            .IsEqualTo((Token(ColorToken.FgHover), Token(ColorToken.FgAccent)));
        await Assert
            .That((TabLog.Label(driver, "1"), TabLog.Label(driver, "2")))
            .IsEqualTo((Token(ColorToken.FgDisabled), Token(ColorToken.FgDisabled)));
    }

    [Test]
    public async Task With_tab_art_the_selected_tab_draws_it_on_accent_and_an_unselected_one_its_hover_art_under_a_hand()
    {
        using var driver = UiDriver.Render(static ui => TabLog.Three(ui));
        driver.View.Context.Skin = new Skin([
            new(SkinPart.TabActive, new SkinPartImage(new TestTexture(16, 16), new Thickness(4))),
            new(SkinPart.TabHover, new SkinPartImage(new TestTexture(16, 16), new Thickness(4))),
        ]);
        driver.Settle();
        var one = driver.Get("1");
        var two = driver.Get("2");

        var resting = (
            ButtonLooks.Images(one),
            one.Visual.Background,
            TabLog.Label(driver, "1"),
            ButtonLooks.Images(two)
        );
        driver.Hover(one);
        var hoveredSelected = ButtonLooks.Images(one);
        driver.Hover(two);
        var hovered = (ButtonLooks.Images(two), driver.Cursor);
        driver.Unhover();

        await Assert
            .That(resting)
            .IsEqualTo((1, default(Paint), (Paint)Themes.Dark.Color(ColorToken.FgOnAccent), 0));
        await Assert.That(hoveredSelected).IsEqualTo(1);
        await Assert.That(hovered).IsEqualTo((1, (Cursor?)Cursors.Hand));
        await Assert
            .That((ButtonLooks.Images(two), driver.Cursor))
            .IsNotEqualTo((1, (Cursor?)Cursors.Hand));
    }

    [Test]
    public async Task A_tab_stands_its_rungs_height_at_every_size_in_every_theme_and_density()
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
                            using (ui.Tabs(size: size, key: (long)size))
                            using (ui.TabsList())
                                ui.TabsTab(value: "talk", text: "Talk", id: $"talk-{size}");
                },
                theme: theme,
                density: density
            );
            foreach (var size in sizes)
            {
                var height = theme.Dimensions(density).Size(size).Height;
                var bounds = driver.Get($"talk-{size}").Bounds;
                if (MathF.Abs(bounds.Height - height) > 0.01f)
                    wrong.Add($"{theme.Name} {density} {size}: {bounds.Height} not {height}");
            }
        }

        await Assert.That(wrong).IsEmpty();
    }

    [Test]
    public async Task A_finger_swiping_an_overflowing_tab_list_scrolls_it_and_a_tap_selects_where_a_drag_across_does_not()
    {
        using var driver = UiDriver.Render(static ui =>
        {
            using (ui.Tabs(width: 240))
            using (ui.TabsList())
                for (var i = 1; i <= 6; i++)
                    ui.TabsTab(value: i, text: $"Tab {i}", key: i, id: $"{i}");
        });
        var first = driver.Get("1").ViewBounds.X;

        driver.Tap(driver.Get("2"));
        var tapped = TabLog.SelectedOf(driver);
        driver.Drag(driver.Get("3"), new System.Numerics.Vector2(-100f, 0f), PointerKind.Touch);

        await Assert.That(tapped).IsEqualTo("2");
        await Assert.That(TabLog.SelectedOf(driver)).IsEqualTo("2");
        await Assert.That(driver.Get("1").ViewBounds.X).IsLessThan(first);
    }
}

internal static class TabLog
{
    public static Callback<UiKey> Of(Log log) =>
        Callback<UiKey>.Of(log, static (l, value) => l.Add(Name(value)));

    public static string Name(UiKey key) =>
        key.TryGetString(out var text) ? text ?? ""
        : key.TryGetLong(out var number)
            ? number.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : "";

    public static bool Selected(UiDriver driver, string id) =>
        (driver.Get(id).States & InputStates.Selected) != 0;

    public static string SelectedOf(UiDriver driver)
    {
        var found = "";
        Walk(driver.Root, ref found);
        return found;
    }

    static void Walk(Node node, ref string found)
    {
        foreach (var child in node.Children)
        {
            if (child.Component == typeof(Tabs.Tab) && FirstBox(child) is { } box)
            {
                if ((box.States & InputStates.Selected) != 0)
                    found = box.Id ?? "?";
                continue;
            }
            Walk(child, ref found);
        }
    }

    static Node? FirstBox(Node component)
    {
        foreach (var child in component.Children)
            return child;
        return null;
    }

    public static Paint Label(UiDriver driver, string id) => driver.Get(id).Inherit.Color;

    public static Paint Underline(UiDriver driver, string id)
    {
        foreach (var child in driver.Get(id).Children)
        {
            if (child.Kind == NodeKind.Box && child.Bounds.Height == 2f)
                return child.Visual.Background;
        }
        return default;
    }

    public static bool Ringed(Node tab) => ButtonLooks.HasRing(tab);

    public static bool Shows(UiDriver driver, string text) => Find(driver.Root, text);

    static bool Find(Node node, string text)
    {
        foreach (var child in node.Children)
        {
            if (child.Kind == NodeKind.Text && child.Text.SequenceEqual(text))
                return true;
            if (Find(child, text))
                return true;
        }
        return false;
    }

    [FramePath]
    public static void Three(
        Ui ui,
        bool panels = false,
        string? defaultValue = null,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Tabs(defaultValue: defaultValue, file: file, line: line))
        {
            using (ui.TabsList())
            {
                ui.TabsTab(value: "1", text: "One", id: "1");
                ui.TabsTab(value: "2", text: "Two", id: "2");
                ui.TabsTab(value: "3", text: "Three", id: "3");
            }
            if (!panels)
                return;
            Panel(ui, "1", "apply-1");
            Panel(ui, "2", "apply-2");
            Panel(ui, "3", "apply-3");
        }
    }

    [FramePath]
    static void Panel(
        Ui ui,
        string value,
        string button,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.TabsPanel(value: value, file: file, line: line))
        using (
            ui.Box(
                width: 60,
                height: 24,
                focusable: true,
                onClick: Callback.Of(static () => { }),
                id: button
            )
        ) { }
    }
}

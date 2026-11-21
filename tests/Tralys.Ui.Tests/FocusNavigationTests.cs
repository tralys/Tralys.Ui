using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class FocusNavigationTests
{
    [Test]
    public async Task Tab_goes_by_positive_tab_index_then_tree_order_skipping_what_cant_take_it()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 4))
            {
                Stop(ui, "a", tabIndex: 2);
                Stop(ui, "b");
                Stop(ui, "c", tabIndex: 1);
                Stop(ui, "d", tabIndex: -1);
                Stop(ui, "e", hidden: true);
                Stop(ui, "f", disabled: true);
                Stop(ui, "g");
            }
        });

        var forward = Walk(driver, 5, shift: false);
        driver.Focus(driver.Get("c"));
        var backward = Walk(driver, 1, shift: true);

        await Assert.That(forward).IsEqualTo("c a b g c");
        await Assert.That(backward).IsEqualTo("g");
    }

    [Test]
    public async Task Tab_stops_once_in_a_once_group_on_its_selected_stop_else_where_focus_last_was()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 4))
            {
                Stop(ui, "before");
                using (ui.Box(direction: Direction.Row, tabNavigation: Navigation.Once))
                {
                    Stop(ui, "x");
                    Stop(ui, "y", selected: true);
                    Stop(ui, "z");
                }
                Stop(ui, "middle");
                using (ui.Box(direction: Direction.Row, tabNavigation: Navigation.Once))
                {
                    Stop(ui, "p");
                    Stop(ui, "q");
                }
                Stop(ui, "after");
            }
        });

        driver.Focus(driver.Get("before"));
        var selected = Walk(driver, 2, shift: false) + " " + Walk(driver, 1, shift: true);
        driver.Click(driver.Get("q"));
        var remembered = Walk(driver, 1, shift: false) + " " + Walk(driver, 1, shift: true);

        await Assert.That(selected).IsEqualTo("y middle y");
        await Assert.That(remembered).IsEqualTo("after q");
    }

    [Test]
    public async Task Shift_tab_into_a_once_group_never_focused_lands_on_its_first_stop()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 4))
            {
                Stop(ui, "before");
                using (ui.Box(direction: Direction.Row, tabNavigation: Navigation.Once))
                {
                    Stop(ui, "x");
                    Stop(ui, "y");
                }
                Stop(ui, "after");
            }
        });

        driver.Focus(driver.Get("after"));

        await Assert.That(Walk(driver, 1, shift: true)).IsEqualTo("x");
    }

    [Test]
    public async Task Tab_stays_at_a_contained_groups_end_and_wraps_inside_a_cycle_group()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 4))
            {
                using (ui.Box(direction: Direction.Row, tabNavigation: Navigation.Contained))
                {
                    Stop(ui, "a1");
                    Stop(ui, "a2");
                }
                using (ui.Box(direction: Direction.Row, tabNavigation: Navigation.Cycle))
                {
                    Stop(ui, "b1");
                    Stop(ui, "b2");
                }
                Stop(ui, "out");
            }
        });

        driver.Focus(driver.Get("a2"));
        var contained = Walk(driver, 1, shift: false);
        driver.Focus(driver.Get("b2"));
        var cycled = Walk(driver, 1, shift: false);

        await Assert.That(contained).IsEqualTo("a2");
        await Assert.That(cycled).IsEqualTo("b1");
    }

    [Test]
    public async Task Arrows_move_to_the_nearest_stop_and_wrap_only_across_a_cycle_groups_axis()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 10))
            {
                using (ui.Box(direction: Direction.Row, gap: 10))
                {
                    Stop(ui, "a");
                    Stop(ui, "b");
                }
                using (ui.Box(direction: Direction.Row, gap: 10, arrowNavigation: Navigation.Cycle))
                {
                    Stop(ui, "p");
                    Stop(ui, "q");
                    Stop(ui, "r");
                }
            }
        });

        driver.Focus(driver.Get("a"));
        var moved = Arrow(driver, Key.Right) + " " + Arrow(driver, Key.Down);
        driver.Focus(driver.Get("r"));
        var wrapped = Arrow(driver, Key.Right);
        var downTaken = driver.View.KeyDown(Key.Down, default, false);
        driver.View.KeyUp(Key.Down, default);

        await Assert.That(moved).IsEqualTo("b q");
        await Assert.That(wrapped).IsEqualTo("p");
        await Assert.That(downTaken).IsFalse();
        await Assert.That(driver.Focused).IsEqualTo(driver.Get("p"));
    }

    [Test]
    public async Task Code_focus_shows_the_ring_only_after_a_key_though_the_mouse_moved_since_and_a_press_hides_it_in_place()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 10))
            {
                ui.StateProbe(name: "ring after a press", focusable: true, id: "pressed");
                ui.StateProbe(name: "ring after a key", focusable: true, id: "keyed");
            }
        });

        driver.Click(new Vector2(400, 300));
        driver.Focus(driver.Get("pressed"));
        var afterPress = StateProbe.Last("ring after a press").FocusVisible;
        driver.Press(Key.A);
        driver.Hover(driver.Get("keyed"));
        driver.Focus(driver.Get("keyed"));
        var afterKey = StateProbe.Last("ring after a key").FocusVisible;
        driver.Click(driver.Get("keyed"));
        var pressedOn = StateProbe.Last("ring after a key");

        await Assert.That((afterPress, afterKey)).IsEqualTo((false, true));
        await Assert.That((pressedOn.Focused, pressedOn.FocusVisible)).IsEqualTo((true, false));
    }

    [Test]
    public async Task A_window_losing_focus_keeps_the_focused_node_and_gives_it_back()
    {
        using var driver = UiDriver.Render(ui =>
            ui.StateProbe(name: "window", focusable: true, id: "window")
        );
        driver.Tab();

        driver.View.WindowFocus(false);
        driver.Settle();
        var away = StateProbe.Last("window");
        var tracked = driver.Focused;
        driver.View.WindowFocus(true);
        driver.Settle();

        await Assert.That(away.Focused).IsFalse();
        await Assert.That(tracked).IsEqualTo(driver.Get("window"));
        await Assert.That(StateProbe.Last("window").FocusVisible).IsTrue();
    }

    [Test]
    public async Task Auto_focus_takes_focus_as_its_node_shows_and_again_when_shown_after_hiding()
    {
        var gate = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 4))
            {
                Stop(ui, "other");
                using (
                    ui.Box(
                        width: 20,
                        height: 20,
                        focusable: true,
                        autoFocus: true,
                        hidden: gate.Remember,
                        id: "auto"
                    )
                ) { }
            }
        });
        var first = driver.Focused?.Id;
        driver.Click(driver.Get("other"));

        gate.Remember = true;
        driver.Rerender();
        gate.Remember = false;
        driver.Rerender();

        await Assert.That(first).IsEqualTo("auto");
        await Assert.That(driver.Focused?.Id).IsEqualTo("auto");
    }

    [Test]
    public async Task With_press_keeping_focus_off_a_clicked_button_lets_it_go_while_a_field_and_a_button_in_an_open_layer_keep_it()
    {
        var typed = new ObservableText("field");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 10))
            {
                using (
                    ui.Box(
                        width: 20,
                        height: 20,
                        onClick: Callback.Of(static () => { }),
                        id: "button"
                    )
                ) { }
                ui.EditableText(text: typed, width: 80, id: "field");
                using (ui.Layer(justify: Justify.End, align: Align.End))
                using (
                    ui.Box(
                        width: 20,
                        height: 20,
                        onClick: Callback.Of(static () => { }),
                        id: "inside"
                    )
                ) { }
            }
        });
        driver.View.Context.PressKeepsFocus = false;

        driver.Click(driver.Get("button"));
        var button = driver.Focused;
        driver.Click(driver.Get("field"));
        var field = driver.Focused?.Id;
        driver.Click(driver.Get("inside"));

        await Assert.That(button).IsNull();
        await Assert.That(field).IsEqualTo("field");
        await Assert.That(driver.Focused?.Id).IsEqualTo("inside");
    }

    [Test]
    public async Task A_press_on_empty_ground_beside_an_open_layer_that_stays_clears_the_focus_and_leaves_the_layer_open()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
            {
                Stop(ui, "field");
                using (ui.Layer(justify: Justify.End, align: Align.End))
                    Stop(ui, "inside");
            }
        });
        driver.Click(driver.Get("field"));

        driver.Click(new Vector2(400, 300));

        await Assert.That(driver.Focused).IsNull();
        await Assert.That(driver.Get("inside")).IsNotNull();
    }

    [Test]
    public async Task A_focused_field_leaving_with_the_focusable_group_around_it_tells_the_field_it_lost_the_focus_and_the_form_it_got_it_and_the_group_nothing()
    {
        var log = new Log();
        var gone = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (Told(ui, log, "form", FormTold, 100))
            {
                if (!gone.Remember)
                {
                    using (Told(ui, log, "group", GroupTold, 60))
                    using (Told(ui, log, "field", FieldTold, 20)) { }
                }
            }
        });
        driver.Focus(driver.Get("field"));
        log.Items.Clear();

        gone.Remember = true;
        driver.Settle();

        await Assert.That(log.ToString()).IsEqualTo("field- form+");
        await Assert.That(driver.Focused?.Id).IsEqualTo("form");
    }

    static readonly string[] FormTold = ["form+", "form-"];
    static readonly string[] GroupTold = ["group+", "group-"];
    static readonly string[] FieldTold = ["field+", "field-"];

    [FramePath]
    static Scope Told(
        Ui ui,
        Log log,
        string id,
        string[] told,
        float size,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Box(
            width: size,
            height: size,
            align: Align.Start,
            focusable: true,
            background: Color.FromRgb(5, 5, 5),
            onFocusChange: Callback<FocusChange>.Of(
                log,
                told,
                static (l, told, f) => l.Add(told[f.Focused ? 0 : 1])
            ),
            id: id,
            key: id,
            file: file,
            line: line
        );

    [Test]
    public async Task A_press_on_chrome_or_empty_space_clears_the_focus_after_its_handlers_unless_they_moved_it_and_a_non_focusable_pressable_leaves_it()
    {
        var probe = new PressProbe();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Row, align: Align.Start, gap: 10))
            {
                Stop(ui, "field");
                Stop(ui, "other");
                using (
                    ui.Box(
                        width: 20,
                        height: 20,
                        background: Color.FromRgb(5, 5, 5),
                        onPointerDown: Handler<PointerEvent>.Of(
                            probe,
                            static (PressProbe p, ref PointerEvent e) =>
                            {
                                if (p.View is not { } view)
                                    throw new InvalidOperationException(
                                        "Pressed before the probe had its view."
                                    );
                                p.Seen = view.Focused?.Id;
                            }
                        ),
                        id: "chrome"
                    )
                ) { }
                using (
                    ui.Box(
                        width: 20,
                        height: 20,
                        background: Color.FromRgb(5, 5, 5),
                        onPointerDown: Handler<PointerEvent>.Of(
                            probe,
                            static (PressProbe p, ref PointerEvent e) =>
                            {
                                if (p.View is not { } view || p.Other is not { } other)
                                    throw new InvalidOperationException(
                                        "Pressed before the probe had its view and target."
                                    );
                                view.Focus(other, FocusCause.Code, false);
                            }
                        ),
                        id: "mover"
                    )
                ) { }
                using (
                    ui.Box(
                        width: 20,
                        height: 20,
                        focusable: false,
                        onClick: Callback.Of(static () => { }),
                        id: "unfocusable"
                    )
                ) { }
            }
        });
        probe.View = driver.View;
        probe.Other = driver.Get("other");
        var field = driver.Get("field");

        driver.Click(field);
        driver.Click(driver.Get("chrome"));
        var chrome = driver.Focused;
        driver.Click(field);
        driver.Click(new Vector2(400, 300));
        var empty = driver.Focused;
        driver.Click(field);
        driver.Click(driver.Get("mover"));
        var moved = driver.Focused?.Id;
        driver.Click(field);
        driver.Click(driver.Get("unfocusable"));

        await Assert
            .That((probe.Seen, chrome, empty))
            .IsEqualTo(("field", (Node?)null, (Node?)null));
        await Assert.That(moved).IsEqualTo("other");
        await Assert.That(driver.Focused).IsEqualTo(field);
    }

    sealed class PressProbe
    {
        public UiView? View;
        public Node? Other;
        public string? Seen;
    }

    [Test]
    public async Task A_focused_node_disabled_or_collapsed_passes_the_focus_to_its_nearest_focusable_ancestor_even_while_the_window_is_away()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (
                ui.Box(
                    width: 100,
                    height: 100,
                    align: Align.Start,
                    focusable: true,
                    background: Color.FromRgb(5, 5, 5),
                    id: "form"
                )
            )
            using (ui.Box(align: Align.Start))
            using (
                ui.Box(
                    width: 20,
                    height: 20,
                    focusable: true,
                    disabled: model.Count == 1,
                    collapsed: model.Count == 2,
                    id: "field"
                )
            ) { }
        });
        var landed = new List<string?>();

        foreach (var away in new[] { 1, 2 })
        {
            model.Count = 0;
            driver.Settle();
            driver.Focus(driver.Get("field"));
            model.Count = away;
            driver.Settle();
            landed.Add(driver.Focused?.Id);
        }
        model.Count = 0;
        driver.Settle();
        driver.Focus(driver.Get("field"));
        driver.View.WindowFocus(false);
        model.Count = 1;
        driver.Settle();
        driver.View.WindowFocus(true);
        driver.Settle();
        landed.Add(driver.Focused?.Id);

        await Assert.That(string.Join(" ", landed)).IsEqualTo("form form form");
    }

    [Test]
    public async Task A_node_takes_focus_only_while_it_is_in_the_view_and_every_ancestor_shows()
    {
        var model = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(hidden: model.Count == 1))
            using (ui.Box())
            {
                if (model.Count != 2)
                {
                    using (ui.Box(focusable: true, width: 20, height: 20, id: "field")) { }
                }
            }
        });
        var field = driver.Get("field");

        model.Count = 1;
        driver.Settle();
        var underAHiddenAncestor = driver.Focus(field);
        model.Count = 0;
        driver.Settle();
        var shownAgain = driver.Focus(field);
        model.Count = 2;
        driver.Settle();
        var removed = driver.Focus(field);

        await Assert
            .That((underAHiddenAncestor, shownAgain, removed))
            .IsEqualTo((false, true, false));
    }

    [Test]
    public async Task An_arrow_stays_at_a_contained_edge_leaves_a_local_group_past_its_end_and_with_the_arrows_off_goes_untaken()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 10))
            {
                using (
                    ui.Box(direction: Direction.Row, gap: 10, arrowNavigation: Navigation.Contained)
                )
                {
                    Stop(ui, "c1");
                    Stop(ui, "c2");
                }
                using (ui.Box(direction: Direction.Row, gap: 10, arrowNavigation: Navigation.Local))
                {
                    Stop(ui, "l1");
                    Stop(ui, "l2");
                }
                Stop(ui, "below");
            }
        });

        driver.Focus(driver.Get("c2"));
        var contained = (driver.View.KeyDown(Key.Right, default, false), driver.Focused?.Id);
        driver.View.KeyUp(Key.Right, default);
        driver.Focus(driver.Get("l1"));
        var local = Arrow(driver, Key.Down);
        driver.Focus(driver.Get("l1"));
        driver.View.Context.ArrowKeysNavigate = false;
        var off = (driver.View.KeyDown(Key.Right, default, false), driver.Focused?.Id);
        driver.View.KeyUp(Key.Right, default);

        await Assert.That(contained).IsEqualTo((true, "c2"));
        await Assert.That(local).IsEqualTo("below");
        await Assert.That(off).IsEqualTo((false, "l1"));
    }

    [FramePath]
    static void Stop(
        Ui ui,
        string id,
        int tabIndex = 0,
        bool hidden = false,
        bool disabled = false,
        bool selected = false,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                width: 20,
                height: 20,
                focusable: true,
                tabIndex: tabIndex,
                hidden: hidden,
                disabled: disabled,
                selected: selected,
                background: Color.FromRgb(5, 5, 5),
                id: id,
                key: id,
                file: file,
                line: line
            )
        ) { }
    }

    static string Walk(UiDriver driver, int steps, bool shift)
    {
        var seen = new List<string?>();
        for (var i = 0; i < steps; i++)
        {
            driver.Tab(shift);
            seen.Add(driver.Focused?.Id);
        }
        return string.Join(" ", seen);
    }

    static string? Arrow(UiDriver driver, Key key)
    {
        driver.Press(key);
        return driver.Focused?.Id;
    }
}

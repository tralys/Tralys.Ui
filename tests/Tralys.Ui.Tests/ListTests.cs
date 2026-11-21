using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;
using TUnit.Assertions.Enums;

namespace Tralys.Ui.Tests;

public sealed class ListTests
{
    static readonly Size View = new(300, 200);

    static UiDriver Render(
        ObservableList<Line> lines,
        ListState state,
        SelectionMode mode = default,
        float rowHeight = 0
    ) =>
        UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                state.Scroll = scroll;
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: LineSlots.Rows,
                    rowHeight: rowHeight,
                    scroll: scroll,
                    selectionMode: mode,
                    selection: state.Selection,
                    onSelectionChange: Callback<SelectionChange>.Of(
                        state,
                        static (s, change) => s.Apply(change)
                    ),
                    height: 200,
                    id: "list"
                );
            },
            View
        );

    [Test]
    public async Task A_render_reading_a_row_list_or_an_adapted_collection_renders_again_as_it_changes()
    {
        var rows = new RowList<string>();
        rows.Add("a");
        rows.Apply();
        var names = new ObservableList<string> { "x" };
        var adapted = ItemsSource.From(names);
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
            {
                ui.SourceCount(source: rows, id: "rows");
                ui.SourceCount(source: adapted, id: "adapted");
            }
        });

        rows.Add("a");
        rows.Add("b");
        rows.Apply();
        names.Add("y");
        driver.Settle();

        await Assert
            .That((driver.Get("rows").Text.ToString(), driver.Get("adapted").Text.ToString()))
            .IsEqualTo(("2", "2"));
    }

    [Test]
    public async Task Render_before_first_layout_realizes_only_rows_in_reach()
    {
        var lines = Line.Many(10_000);
        var seen = new HashSet<int>();
        using var driver = UiDriver.Render(
            ui =>
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: Slot<Line>.Of(
                        seen,
                        static (ui, s, line, _) =>
                        {
                            s.Add(line.Id);
                            ui.LineRow(item: line, id: line.Name);
                        }
                    ),
                    height: 200
                ),
            View
        );

        await Assert.That(seen.Count).IsEqualTo(15);
        await Assert.That(seen.Max()).IsEqualTo(14);
    }

    [Test]
    public async Task A_far_scroll_pending_at_the_first_layout_renders_no_rows_from_the_top()
    {
        var lines = Line.Many(10_000);
        var seen = new HashSet<int>();
        var requested = new ListState();
        using var driver = UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                if (requested.Scroll is null)
                {
                    requested.Scroll = scroll;
                    scroll.ScrollToIndex(5000);
                }
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: Slot<Line>.Of(
                        seen,
                        static (ui, s, line, _) =>
                        {
                            s.Add(line.Id);
                            ui.LineRow(item: line, id: line.Name);
                        }
                    ),
                    scroll: scroll,
                    height: 200
                );
            },
            View
        );

        var shown = driver.Get("line 5000").ViewBounds;
        await Assert.That(seen.Min()).IsGreaterThan(4950);
        await Assert.That(shown.Y).IsGreaterThanOrEqualTo(0f);
        await Assert.That(shown.Bottom).IsLessThanOrEqualTo(200f);
    }

    [Test]
    public async Task A_recycled_row_is_a_new_identity_with_fresh_state()
    {
        var lines = Line.Many(1000);
        var state = new ListState();
        using var driver = Render(lines, state);
        var mounts = new Dictionary<Node, long>();
        for (var i = 0; i < 10; i++)
        {
            driver.Click(driver.Get(lines[i].Name));
            var row = driver.Get<LineRow>(lines[i].Name);
            mounts[row] = driver.MountId(row);
        }
        driver.Focus(driver.Get("list"));

        var scroll = await Assert.That(state.Scroll).IsNotNull();
        scroll.ScrollTo(null, 4000);
        driver.Settle();
        scroll.ScrollTo(null, 4400);
        driver.Settle();

        var reused = 0;
        for (var i = 220; i < 230; i++)
        {
            await Assert.That(Texts(driver.Get(lines[i].Name))[1]).IsEqualTo("clicks 0");
            var row = driver.Get<LineRow>(lines[i].Name);
            if (mounts.TryGetValue(row, out var before))
            {
                reused++;
                await Assert.That(driver.MountId(row)).IsNotEqualTo(before);
            }
        }
        await Assert.That(reused).IsGreaterThan(0);
    }

    [Test]
    public async Task A_retired_rows_item_notifies_nothing()
    {
        var lines = Line.Many(1000);
        var state = new ListState();
        using var driver = Render(lines, state);

        var scroll = await Assert.That(state.Scroll).IsNotNull();
        scroll.ScrollTo(null, 4000);
        driver.Settle();
        lines[0].Name = "renamed";
        driver.Frame();
        var retired = driver.Stats.Renders;
        lines[205].Name = "shown";
        driver.Frame();

        await Assert.That(retired).IsEqualTo(0);
        await Assert.That(driver.GetText("shown")).IsNotNull();
    }

    [Test]
    public async Task Selection_follows_keys_as_items_come_and_move()
    {
        var lines = Line.Many(20);
        var state = new ListState { Items = ItemsSource.From(lines) };
        using var driver = Render(lines, state, SelectionMode.Single);
        driver.Click(driver.Get("line 3"));

        lines.Insert(0, new Line { Id = 100, Name = "line 100" });
        lines.Move(4, 10);
        driver.Settle();

        await Assert.That(state.Selection.Single).IsEqualTo((UiKey)3);
        await Assert.That(Selected(driver.Get("list"))).IsEquivalentTo(["line 3"]);
    }

    [Test]
    public async Task A_reorder_with_unequal_row_heights_keeps_bounds_and_hits_right()
    {
        var lines = new ObservableList<Line>
        {
            new()
            {
                Id = 0,
                Name = "line 0",
                Height = 10,
            },
            new()
            {
                Id = 1,
                Name = "line 1",
                Height = 20,
            },
            new()
            {
                Id = 2,
                Name = "line 2",
                Height = 30,
            },
        };
        using var driver = Render(lines, new ListState());

        lines.Move(2, 0);
        driver.Settle();
        driver.Click(new Vector2(150, 25));
        driver.Click(new Vector2(150, 35));

        await Assert.That(driver.Get("line 2").ViewBounds).IsEqualTo(new Rect(0, 0, 300, 30));
        await Assert.That(driver.Get("line 0").ViewBounds).IsEqualTo(new Rect(0, 30, 300, 10));
        await Assert.That(driver.Get("line 1").ViewBounds).IsEqualTo(new Rect(0, 40, 300, 20));
        await Assert.That(Texts(driver.Get("line 2"))[1]).IsEqualTo("clicks 1");
        await Assert.That(Texts(driver.Get("line 0"))[1]).IsEqualTo("clicks 1");
    }

    [Test]
    public async Task A_focused_row_scrolled_away_stays_until_its_item_goes()
    {
        var lines = Line.Many(1000);
        var state = new ListState();
        using var driver = Render(lines, state);
        var row = driver.Get("line 1");
        driver.Focus(row);

        var scroll = await Assert.That(state.Scroll).IsNotNull();
        scroll.ScrollTo(null, 4000);
        driver.Settle();

        await Assert.That(driver.Focused).IsSameReferenceAs(row);
        await Assert.That(driver.Get("line 1")).IsSameReferenceAs(row);
        await Assert.That(row.ViewBounds.Bottom).IsLessThanOrEqualTo(0f);

        lines.RemoveAt(1);
        driver.Settle();

        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("list"));
    }

    [Test]
    public async Task A_focused_row_scrolled_away_goes_as_its_list_is_emptied()
    {
        var lines = Line.Many(1000);
        var state = new ListState();
        using var driver = Render(lines, state);
        driver.Focus(driver.Get("line 1"));
        var scroll = await Assert.That(state.Scroll).IsNotNull();
        scroll.ScrollTo(null, 4000);
        driver.Settle();

        lines.Clear();
        driver.Settle();

        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("list"));
        await Assert.That(() => driver.Get("line 1")).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task A_focused_row_scrolled_away_whose_item_is_replaced_hands_focus_to_the_row_now_there()
    {
        var lines = Line.Many(1000);
        var state = new ListState();
        using var driver = Render(lines, state);
        driver.Focus(driver.Get("line 1"));
        var scroll = await Assert.That(state.Scroll).IsNotNull();
        scroll.ScrollTo(null, 4000);
        driver.Settle();

        lines[1] = new Line { Id = 5000, Name = "fresh" };
        driver.Settle();
        var fresh = driver.Get("fresh");
        var focused = driver.Focused;
        lines[1] = new Line { Id = 5000, Name = "renamed" };
        driver.Settle();
        var renamed = driver.Get("renamed");
        scroll.ScrollTo(null, 0);
        driver.Settle();

        await Assert.That(focused).IsSameReferenceAs(fresh);
        await Assert.That(renamed).IsSameReferenceAs(fresh);
        await Assert.That(driver.Get("renamed")).IsSameReferenceAs(fresh);
        await Assert.That(driver.Focused).IsSameReferenceAs(fresh);
    }

    [Test]
    public async Task A_row_above_the_view_growing_leaves_the_rows_in_view_still()
    {
        var lines = Line.Many(1000);
        var state = new ListState();
        using var driver = Render(lines, state);
        var scroll = await Assert.That(state.Scroll).IsNotNull();
        scroll.ScrollTo(null, 400);
        driver.Settle();
        var top = driver.Get("line 20").ViewBounds;

        lines[18].Height = 50;
        driver.Settle();

        await Assert.That(driver.Get("line 20").ViewBounds).IsEqualTo(top);
        await Assert.That(scroll.Offset.Y).IsEqualTo(430f);
    }

    [Test]
    public async Task Items_inserted_above_the_realized_rows_leave_the_rows_in_view_still()
    {
        var lines = Line.Many(1000);
        var state = new ListState();
        using var driver = Render(lines, state);
        var scroll = await Assert.That(state.Scroll).IsNotNull();
        scroll.ScrollTo(null, 2000);
        driver.Settle();

        for (var i = 0; i < 5; i++)
            lines.Insert(0, new Line { Id = 2000 + i, Name = "new" });
        driver.Settle();

        await Assert.That(driver.Get("line 100").ViewBounds.Y).IsEqualTo(0f);
        await Assert.That(scroll.Offset.Y).IsEqualTo(2100f);
    }

    [Test]
    public async Task Scrolling_to_a_far_item_among_rows_taller_than_the_estimate_shows_it_whole()
    {
        var lines = Line.Many(1000);
        for (var i = 590; i <= 610; i++)
            lines[i].Height = 80;
        var state = new ListState();
        using var driver = Render(lines, state);

        var scroll = await Assert.That(state.Scroll).IsNotNull();
        scroll.ScrollToIndex(600);
        driver.Settle();

        var shown = driver.Get("line 600").ViewBounds;
        await Assert.That(shown.Y).IsGreaterThanOrEqualTo(0f);
        await Assert.That(shown.Bottom).IsLessThanOrEqualTo(200f);
    }

    [Test]
    public async Task Arrows_and_End_move_the_selection_and_hand_the_focus_on_even_far()
    {
        var lines = Line.Many(1000);
        var state = new ListState { Items = ItemsSource.From(lines) };
        using var driver = Render(lines, state, SelectionMode.Single);
        driver.Focus(driver.Get("line 0"));

        driver.Press(Key.Down);

        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("line 1"));
        await Assert.That(state.Selection.Single).IsEqualTo((UiKey)1);

        driver.Press(Key.End);

        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("line 999"));
        await Assert.That(state.Selection.Single).IsEqualTo((UiKey)999);
        await Assert.That(driver.Get("line 999").ViewBounds.Bottom).IsEqualTo(200f);
    }

    [Test]
    public async Task Tab_enters_a_list_once_on_its_selected_row()
    {
        var lines = Line.Many(20);
        var state = new ListState { Items = ItemsSource.From(lines) };
        state.Apply(new SelectionChange(SelectionAction.Replace, 5, 5));
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(direction: Direction.Column))
                {
                    using (ui.Box(height: 20, focusable: true, id: "before")) { }
                    ui.VirtualList(
                        items: lines,
                        itemKey: Line.ById,
                        row: LineSlots.Rows,
                        selectionMode: SelectionMode.Single,
                        selection: state.Selection,
                        height: 150
                    );
                    using (ui.Box(height: 20, focusable: true, id: "after")) { }
                }
            },
            View
        );
        driver.Focus(driver.Get("before"));

        driver.Tab();

        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("line 5"));

        driver.Tab();

        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("after"));
    }

    [Test]
    public async Task Control_toggles_and_shift_ranges_in_multiple_selection()
    {
        var lines = Line.Many(20);
        var state = new ListState { Items = ItemsSource.From(lines) };
        using var driver = Render(lines, state, SelectionMode.Multiple);

        driver.Click(driver.Get("line 2"));
        driver.Click(driver.Get("line 5"), modifiers: ModifierKeys.Control);
        await Assert.That(Selected(driver.Get("list"))).IsEquivalentTo(["line 2", "line 5"]);

        driver.Click(driver.Get("line 7"), modifiers: ModifierKeys.Shift);
        await Assert
            .That(Selected(driver.Get("list")))
            .IsEquivalentTo(["line 5", "line 6", "line 7"]);
    }

    [Test]
    public async Task Enter_on_a_pressable_in_a_row_that_leaves_enter_to_its_form_runs_the_lists_action_and_south_presses_it()
    {
        var lines = Line.Many(3);
        var log = new Log();
        using var driver = UiDriver.Render(
            ui =>
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: SpaceOnlyRows,
                    onAction: Callback<UiKey>.Of(log, static (l, key) => l.Add(key.ToString())),
                    height: 200
                ),
            View
        );
        driver.Focus(driver.Get("line 1"));

        driver.Press(Key.Enter);
        driver.Press(GamepadButton.South);

        await Assert.That(log.ToString()).IsEqualTo("1");
    }

    [Test]
    public async Task Space_on_a_pressable_in_a_multiple_selection_row_presses_it_and_leaves_the_selection()
    {
        var lines = Line.Many(3);
        var state = new ListState { Items = ItemsSource.From(lines) };
        var log = new Log();
        using var driver = UiDriver.Render(
            ui =>
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: Slot<Line>.Of(
                        log,
                        static (ui, log, line, _) =>
                        {
                            using (ui.Box(height: 20))
                            using (
                                ui.Box(
                                    width: 20,
                                    height: 20,
                                    onClick: Callback.Of(log, static l => l.Add("pressed")),
                                    pressKeys: PressKeys.Space,
                                    id: line.Name
                                )
                            ) { }
                        }
                    ),
                    selectionMode: SelectionMode.Multiple,
                    selection: state.Selection,
                    onSelectionChange: Callback<SelectionChange>.Of(
                        state,
                        static (s, change) => s.Apply(change)
                    ),
                    height: 200
                ),
            View
        );
        driver.Focus(driver.Get("line 1"));

        driver.Press(Key.Space);

        await Assert.That(log.ToString()).IsEqualTo("pressed");
        await Assert.That(state.Selection.Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_press_on_a_pressable_inside_a_row_leaves_the_selection_while_one_beside_it_selects()
    {
        var lines = Line.Many(3);
        var state = new ListState { Items = ItemsSource.From(lines) };
        using var driver = UiDriver.Render(
            ui =>
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: SpaceOnlyRows,
                    selectionMode: SelectionMode.Single,
                    selection: state.Selection,
                    onSelectionChange: Callback<SelectionChange>.Of(
                        state,
                        static (s, change) => s.Apply(change)
                    ),
                    height: 200
                ),
            View
        );

        driver.Click(driver.Get("line 1"));
        var pressedInside = state.Selection.Count;
        var row = await Assert.That(driver.Get("line 1").Parent).IsNotNull();
        driver.Click(row, new Vector2(100, 10));

        await Assert.That(pressedInside).IsEqualTo(0);
        await Assert.That(state.Selection.Single).IsEqualTo((UiKey)1);
    }

    static readonly Slot<Line> SpaceOnlyRows = Slot<Line>.Of(
        static (ui, line, _) =>
        {
            using (ui.Box(height: 20))
            using (
                ui.Box(
                    width: 20,
                    height: 20,
                    onClick: Callback.Of(static () => { }),
                    pressKeys: PressKeys.Space,
                    id: line.Name
                )
            ) { }
        }
    );

    [Test]
    public async Task A_matching_source_builds_no_row_for_what_doesnt_match_and_scrolls_by_source_index()
    {
        var source = new PickSource(200);
        var state = new ListState();
        var seen = new HashSet<int>();
        using var driver = UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                state.Scroll = scroll;
                ui.VirtualList(
                    items: ItemsSource.Matching<Pick>(source),
                    itemKey: Pick.ById,
                    row: Slot<Pick>.Of(
                        seen,
                        static (ui, s, pick, _) =>
                        {
                            s.Add(pick.Id);
                            using (ui.Box(height: 20))
                                ui.Text(pick.Name);
                        }
                    ),
                    scroll: scroll,
                    height: 200
                );
            },
            View
        );

        var scroll = await Assert.That(state.Scroll).IsNotNull();
        scroll.ScrollToIndex(150);
        driver.Settle();

        await Assert.That(seen.All(static id => id % 2 == 0)).IsTrue();
        var shown = driver.GetText("pick 150").ViewBounds;
        await Assert.That(shown.Y).IsGreaterThanOrEqualTo(0f);
        await Assert.That(shown.Bottom).IsLessThanOrEqualTo(200f);
    }

    [Test]
    public async Task With_no_items_the_empty_content_fills_the_viewport()
    {
        var lines = new ObservableList<Line>();
        using var driver = UiDriver.Render(
            ui =>
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: LineSlots.Rows,
                    empty: Slot.Of(static ui =>
                    {
                        using (ui.Box(id: "empty"))
                            ui.Text("nothing");
                    }),
                    padding: 10,
                    height: 200
                ),
            View
        );

        await Assert.That(driver.Get("empty").ViewBounds).IsEqualTo(new Rect(0, 0, 300, 200));

        lines.Add(new Line { Id = 1, Name = "line 1" });
        driver.Settle();

        await Assert.That(() => driver.Get("empty")).Throws<InvalidOperationException>();
        await Assert.That(driver.Get("line 1").ViewBounds.Y).IsEqualTo(10f);
    }

    [Test]
    public async Task A_list_sized_to_its_rows_keeps_its_widest_rows_width_once_scrolled_past_it()
    {
        var lines = Line.Many(1000, height: 100);
        lines[0].Height = 300;
        var state = new ListState();
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start))
                {
                    var scroll = ui.ScrollHandle();
                    state.Scroll = scroll;
                    ui.VirtualList(
                        items: lines,
                        itemKey: Line.ById,
                        row: Slot<Line>.Of(
                            static (ui, line, _) =>
                            {
                                using (ui.Box(width: line.Height, height: 20)) { }
                            }
                        ),
                        scroll: scroll,
                        height: 200,
                        id: "list"
                    );
                }
            },
            View
        );
        var before = driver.Get("list").ViewBounds.Width;

        var scroll = await Assert.That(state.Scroll).IsNotNull();
        scroll.ScrollTo(null, 10000);
        driver.Settle();

        await Assert.That((before, driver.Get("list").ViewBounds.Width)).IsEqualTo((300f, 300f));
    }

    [Test]
    public async Task A_rows_own_height_stands_across_a_horizontal_list()
    {
        var lines = Line.Many(3, height: 40);
        using var driver = UiDriver.Render(
            ui =>
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: Slot<Line>.Of(
                        static (ui, line, _) =>
                        {
                            using (ui.Box(width: 88, height: line.Height, id: line.Name)) { }
                        }
                    ),
                    horizontal: true,
                    height: 112
                ),
            View
        );

        await Assert.That(driver.Get("line 1").ViewBounds).IsEqualTo(new Rect(88, 0, 88, 40));
    }

    [Test]
    public async Task A_list_in_a_grids_fr_row_realizes_only_the_rows_in_reach()
    {
        var lines = Line.Many(1000);
        var seen = new HashSet<int>();
        using var driver = UiDriver.Render(
            ui =>
            {
                using (
                    ui.Grid(
                        columns: new GridTracks(Track.Px(250)),
                        rows: new GridTracks(Track.Auto, Track.Fr(1))
                    )
                )
                {
                    ui.Text("Head");
                    ui.VirtualList(
                        items: lines,
                        itemKey: Line.ById,
                        row: Slot<Line>.Of(
                            seen,
                            static (ui, s, line, _) =>
                            {
                                s.Add(line.Id);
                                ui.LineRow(item: line, id: line.Name);
                            }
                        )
                    );
                }
            },
            View
        );

        await Assert.That(seen.Max()).IsLessThan(20);
    }

    [Test]
    public async Task A_row_whose_root_changed_while_its_list_was_collapsed_leaves_the_node_another_row_took_as_it_was()
    {
        var swap = new Model();
        var later = new ObservableList<string> { "b" };
        var collapsed = false;
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
            {
                ui.VirtualList(
                    items: ItemsSource.From(later),
                    itemKey: Words.ByText,
                    row: Slot<string>.Of(
                        static (ui, item, _) =>
                        {
                            using (ui.Box(width: 50, height: 20, id: item)) { }
                        }
                    ),
                    selectionMode: SelectionMode.Single,
                    selection: Everything.Selected,
                    height: 60
                );
                using (ui.Box(collapsed: collapsed))
                    ui.VirtualList(
                        items: Words.Of("a"),
                        itemKey: Words.ByText,
                        row: Slot<string>.Of(
                            swap,
                            static (ui, s, _, _) =>
                            {
                                if (s.Remember)
                                    using (ui.Box(width: 50, height: 20, key: 1)) { }
                                else
                                    using (ui.Box(width: 50, height: 20, key: 2)) { }
                            }
                        ),
                        selectionMode: SelectionMode.Single,
                        selection: Everything.Selected,
                        height: 60
                    );
            }
        });
        collapsed = true;
        driver.Rerender();

        swap.Remember = true;
        driver.Frame();
        later.Add("c");
        driver.Frame();
        collapsed = false;
        driver.Rerender();

        await Assert
            .That(driver.Get("c").States & InputStates.Selected)
            .IsEqualTo(InputStates.Selected);
    }

    [Test]
    public async Task A_list_whose_call_site_changes_its_item_type_shows_only_the_new_items_rows()
    {
        var numbers = false;
        using var driver = UiDriver.Render(ui => Listed(ui, numbers));

        numbers = true;
        driver.Rerender();

        await Assert.That(driver.All<VirtualListRow>().Count).IsEqualTo(2);
    }

    static readonly IItemsSource<string> Letters = Words.Of("a", "b", "c");

    static readonly IItemsSource<int> Digits = ItemsSource.From(new[] { 1, 2 });

    [FramePath]
    static void Listed(
        Ui ui,
        bool numbers,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        if (numbers)
            Show(ui, Digits, static n => n, file, line);
        else
            Show(ui, Letters, Words.ByText, file, line);
    }

    [FramePath]
    static void Show<T>(
        Ui ui,
        IItemsSource<T> items,
        KeyOf<T> key,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.VirtualList(
            items: items,
            itemKey: key,
            row: Slot<T>.Of(
                static (ui, _, _) =>
                {
                    using (ui.Box(width: 50, height: 20)) { }
                }
            ),
            height: 100,
            file: file,
            line: line
        );

    [Test]
    public async Task A_list_a_grids_auto_column_measures_with_no_height_limit_faults_once_realizes_only_the_rows_in_reach_and_goes_idle()
    {
        var lines = Line.Many(200);
        var seen = new HashSet<int>();
        var faults = new List<Exception>();
        var shown = false;
        using var driver = UiDriver.Render(
            ui =>
            {
                if (!shown)
                    return;
                using (ui.Grid(rows: new GridTracks(Track.Auto, Track.Fr(1))))
                {
                    ui.Text("Head");
                    ui.VirtualList(
                        items: lines,
                        itemKey: Line.ById,
                        row: Slot<Line>.Of(
                            seen,
                            static (ui, s, line, _) =>
                            {
                                s.Add(line.Id);
                                using (ui.Box(width: 50 + line.Id, height: 20)) { }
                            }
                        ),
                        justifySelf: AlignSelf.Start
                    );
                }
            },
            View
        );
        driver.View.Context.OnFault = faults.Add;
        shown = true;
        driver.Rerender();
        driver.View.Size = new Size(320, 200);
        driver.Settle();

        driver.Frame();

        await Assert.That(faults.Count).IsEqualTo(1);
        await Assert.That(faults[0]).IsTypeOf<InvalidOperationException>();
        await Assert.That(seen.Max()).IsLessThan(20);
        await Assert.That(driver.Stats.Layouts).IsEqualTo(0);
    }

    [Test]
    public async Task A_single_selection_reports_no_click_or_space_on_its_selected_row_nor_a_modified_arrow_and_an_arrow_on_the_focused_list_keeps_the_focus_there()
    {
        var lines = Line.Many(20);
        var state = new ListState { Items = ItemsSource.From(lines) };
        using var driver = Render(lines, state, SelectionMode.Single);

        driver.Click(driver.Get("line 2"));
        driver.Click(driver.Get("line 2"));
        driver.Press(Key.Space);
        driver.Press(Key.Down, ModifierKeys.Control);
        driver.Press(Key.Down, ModifierKeys.Alt);
        var still = (state.Reports, state.Selection.Single, driver.Focused?.Id);
        driver.Focus(driver.Get("list"));
        driver.Press(Key.Down);

        await Assert.That(still).IsEqualTo((1, (UiKey?)2, "line 2"));
        await Assert.That(state.Selection.Single).IsEqualTo((UiKey)3);
        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("list"));
    }

    [Test]
    public async Task A_horizontal_list_steps_on_left_and_right_and_leaves_up_and_down_alone()
    {
        var lines = Line.Many(10);
        var state = new ListState { Items = ItemsSource.From(lines) };
        using var driver = UiDriver.Render(
            ui =>
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: Slot<Line>.Of(
                        static (ui, line, _) =>
                        {
                            using (ui.Box(width: 40, height: 20, id: line.Name)) { }
                        }
                    ),
                    horizontal: true,
                    selectionMode: SelectionMode.Single,
                    selection: state.Selection,
                    onSelectionChange: Callback<SelectionChange>.Of(
                        state,
                        static (s, change) => s.Apply(change)
                    ),
                    height: 20
                ),
            View
        );
        driver.Click(driver.Get("line 1"));
        var steps = new List<UiKey?>();

        foreach (var key in (Key[])[Key.Right, Key.Right, Key.Left, Key.Down, Key.Up])
        {
            driver.Press(key);
            steps.Add(state.Selection.Single);
        }

        await Assert.That(string.Join(" ", steps)).IsEqualTo("2 3 2 2 2");
    }

    [Test]
    public async Task Page_down_and_up_go_to_the_last_or_first_whole_row_in_view_then_a_page_further()
    {
        var lines = Line.Many(10_000);
        var state = new ListState { Items = ItemsSource.From(lines) };
        using var driver = Render(lines, state, SelectionMode.Single);
        driver.Click(driver.Get("line 0"));
        var steps = new List<UiKey?>();

        foreach (var key in (Key[])[Key.PageDown, Key.PageDown, Key.PageUp, Key.PageUp])
        {
            driver.Press(key);
            steps.Add(state.Selection.Single);
        }

        await Assert.That(string.Join(" ", steps)).IsEqualTo("9 19 10 0");
        await Assert.That(driver.Focused).IsSameReferenceAs(driver.Get("line 0"));
    }

    [Test]
    public async Task A_selected_rows_container_recycled_for_another_item_shows_it_unselected_and_the_selection_returns_with_its_item()
    {
        var lines = Line.Many(100);
        var state = new ListState { Items = ItemsSource.From(lines) };
        using var driver = Render(lines, state, SelectionMode.Single);
        driver.Click(driver.Get("line 2"));
        driver.Focus(driver.Get("list"));

        var scroll = await Assert.That(state.Scroll).IsNotNull();
        scroll.ScrollTo(null, 1500);
        driver.Settle();
        var far = Selected(driver.Get("list"));
        scroll.ScrollTo(null, 0);
        driver.Settle();

        await Assert.That(far).IsEmpty();
        await Assert.That(Selected(driver.Get("list"))).IsEquivalentTo(["line 2"]);
    }

    [Test]
    public async Task A_list_follows_inserts_moves_replaces_and_removes_in_order_from_an_observable_list()
    {
        var lines = Line.Many(5);
        using var driver = UiDriver.Render(
            ui =>
                ui.VirtualList(
                    items: ItemsSource.From(lines),
                    itemKey: Line.ById,
                    row: LineSlots.Rows,
                    realizeAll: true,
                    id: "list"
                ),
            View
        );
        var shown = new List<string>();
        void Check()
        {
            driver.Settle();
            var expected = new List<string?>();
            foreach (var line in lines)
                expected.Add(line.Name);
            shown.Add(
                ListAllocationTests.Shown(driver.Get("list")).SequenceEqual(expected) ? "ok" : "off"
            );
        }

        lines.Insert(1, new Line { Id = 10, Name = "line 10" });
        Check();
        lines.Move(0, 3);
        Check();
        lines[2] = new Line { Id = 11, Name = "line 11" };
        Check();
        lines.RemoveAt(4);
        Check();

        await Assert.That(string.Join(" ", shown)).IsEqualTo("ok ok ok ok");
    }

    [Test]
    public async Task A_list_refilled_in_a_new_order_keeps_each_staying_items_row_and_the_selection_on_its_item()
    {
        var lines = Line.Many(10);
        var state = new ListState { Items = ItemsSource.From(lines) };
        using var driver = Render(lines, state, SelectionMode.Single);
        driver.Click(driver.Get("line 8"));
        var rows = new Dictionary<string, long>();
        foreach (var line in lines)
            rows[line.Name] = driver.MountId(driver.Get<LineRow>(line.Name));
        var order = new[] { 8, 0, 1, 3, 4, 5, 6, -1, 7, 9 };
        var kept = new List<Line>(lines);

        lines.Clear();
        foreach (var at in order)
            lines.Add(at < 0 ? new Line { Id = 100, Name = "line 100" } : kept[at]);
        driver.Settle();

        var moved = 0;
        foreach (var at in order)
        {
            if (at >= 0 && driver.MountId(driver.Get<LineRow>("line " + at)) != rows["line " + at])
                moved++;
        }
        var expected = new List<string?>();
        foreach (var line in lines)
            expected.Add(line.Name);
        await Assert
            .That(ListAllocationTests.Shown(driver.Get("list")))
            .IsEquivalentTo(expected, CollectionOrdering.Matching);
        await Assert.That(moved).IsEqualTo(0);
        await Assert.That(Selected(driver.Get("list"))).IsEquivalentTo(["line 8"]);
    }

    [Test]
    public async Task A_list_out_of_the_view_listens_to_nothing_and_shown_again_shows_what_changed_meanwhile()
    {
        var items = new CountedLines(Line.Many(1));
        var shown = new Model { Remember = true };
        using var driver = UiDriver.Render(
            ui =>
            {
                if (shown.Remember)
                    ui.VirtualList(
                        items: items,
                        itemKey: Line.ById,
                        row: LineSlots.Rows,
                        height: 200,
                        id: "list"
                    );
            },
            View
        );
        var listening = items.Listeners;

        shown.Remember = false;
        driver.Settle();
        var away = items.Listeners;
        items.Lines.Add(new Line { Id = 1, Name = "line 1" });
        shown.Remember = true;
        driver.Settle();

        await Assert.That((listening, away)).IsEqualTo((1, 0));
        await Assert
            .That(string.Join(", ", ListAllocationTests.Shown(driver.Get("list"))))
            .IsEqualTo("line 0, line 1");
    }

    [Test]
    public async Task A_list_keeps_no_more_spare_rows_than_it_shows()
    {
        var lines = Line.Many(100);
        using var driver = UiDriver.Render(
            ui =>
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: LineSlots.Rows,
                    realizeAll: true,
                    id: "list"
                ),
            View
        );

        for (var i = 0; i < 98; i++)
            lines.RemoveAt(0);
        driver.Settle();
        var shrunk = RowNodes(driver.Get("list"));
        lines.Clear();
        driver.Settle();
        var cleared = RowNodes(driver.Get("list"));

        await Assert.That(shrunk).IsLessThanOrEqualTo(4);
        await Assert.That(cleared).IsEqualTo(0);
    }

    [Test]
    public async Task The_extent_counts_rows_never_realized_at_the_estimate_so_items_appended_past_them_grow_it()
    {
        var lines = Line.Many(50);
        var state = new ListState();
        using var driver = Render(lines, state);
        var scroll = await Assert.That(state.Scroll).IsNotNull();
        var before = scroll.Extent.Height;

        for (var i = 50; i < 60; i++)
            lines.Add(new Line { Id = i, Name = "line " + i });
        driver.Settle();

        await Assert.That((before, scroll.Extent.Height)).IsEqualTo((1000f, 1200f));
    }

    [Test]
    public async Task A_horizontal_list_lays_its_items_in_a_row_and_realizes_only_those_in_reach()
    {
        var lines = Line.Many(100);
        var state = new ListState();
        using var driver = UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                state.Scroll = scroll;
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: Slot<Line>.Of(
                        static (ui, line, _) =>
                        {
                            using (ui.Box(width: 100, height: 20, id: line.Name)) { }
                        }
                    ),
                    horizontal: true,
                    scroll: scroll,
                    width: 200,
                    height: 20,
                    id: "list"
                );
            },
            View
        );

        var scroll = await Assert.That(state.Scroll).IsNotNull();
        scroll.ScrollTo(1000, null);
        driver.Settle();

        await Assert.That(scroll.Extent.Width).IsEqualTo(10_000f);
        await Assert.That(driver.Get("line 10").ViewBounds).IsEqualTo(new Rect(0, 0, 100, 20));
        await Assert.That(driver.Get("line 11").ViewBounds.X).IsEqualTo(100f);
        await Assert.That(ListAllocationTests.Shown(driver.Get("list")).Count).IsLessThan(6);
    }

    [Test]
    public async Task A_row_in_view_growing_leaves_the_rows_above_it_still()
    {
        var lines = Line.Many(1000);
        var state = new ListState();
        using var driver = Render(lines, state);
        var scroll = await Assert.That(state.Scroll).IsNotNull();
        scroll.ScrollTo(null, 2000);
        driver.Settle();
        var top = driver.Get("line 100");

        lines[101].Height = 200;
        driver.Settle();

        await Assert.That(driver.Get("line 100")).IsSameReferenceAs(top);
        await Assert.That(top.ViewBounds.Y).IsEqualTo(0f);
        await Assert.That(scroll.Offset.Y).IsEqualTo(2000f);
    }

    [Test]
    public async Task Among_rows_of_mixed_heights_the_rows_in_view_stay_still_as_items_come_go_and_are_replaced_above_them_or_reset()
    {
        var lines = Line.Many(1000);
        for (var i = 50; i < lines.Count; i++)
            lines[i].Height = 60;
        var items = new CountedLines(lines);
        var state = new ListState();
        using var driver = UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                state.Scroll = scroll;
                ui.VirtualList(
                    items: items,
                    itemKey: Line.ById,
                    row: LineSlots.Rows,
                    scroll: scroll,
                    height: 200,
                    id: "list"
                );
            },
            View
        );
        var scroll = await Assert.That(state.Scroll).IsNotNull();
        scroll.ScrollTo(null, 3000);
        driver.Settle();
        var name = InView(driver);
        var row = driver.Get(name);
        var y = row.ViewBounds.Y;
        var offset = scroll.Offset.Y;
        var moves = new List<string>();
        void Still(string step)
        {
            driver.Settle();
            if (driver.Get(name).ViewBounds.Y != y)
                moves.Add(step);
        }

        lines.Insert(0, new Line { Id = 2000, Name = "new" });
        Still("insert");
        var sameRow = driver.Get(name) == row;
        lines.RemoveAt(0);
        Still("remove");
        var back = scroll.Offset.Y;
        var first = FirstShown(driver, lines);
        lines[first] = new Line
        {
            Id = 3000,
            Name = "replaced",
            Height = lines[first].Height,
        };
        Still("replace first");
        lines.RemoveAt(FirstShown(driver, lines));
        Still("remove first");
        var kept = driver.Get(name) == row;
        items.Reset();
        Still("reset");

        await Assert.That(moves).IsEmpty();
        await Assert.That((sameRow, kept)).IsEqualTo((true, true));
        await Assert.That(back).IsEqualTo(offset);
    }

    static int RowNodes(Node list)
    {
        var count = 0;
        foreach (var _ in list.Children)
            count++;
        return count;
    }

    static string InView(UiDriver driver)
    {
        foreach (var child in driver.Get("list").Children)
        {
            if (
                !child.SpareRow
                && child.Host?.Root is { Id: { } id } root
                && root.ViewBounds.Y >= 20f
            )
                return id;
        }
        throw new InvalidOperationException("No row shows.");
    }

    static int FirstShown(UiDriver driver, ObservableList<Line> lines)
    {
        foreach (var child in driver.Get("list").Children)
        {
            if (child.SpareRow || child.Host?.Root?.Id is not { } id)
                continue;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Name == id)
                    return i;
            }
        }
        throw new InvalidOperationException("No row shows.");
    }

    [Test]
    public async Task An_item_added_or_replaced_and_scrolled_to_in_the_same_frame_shows_whole()
    {
        var all = Line.Many(10);
        var allState = new ListState();
        using var stack = UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                allState.Scroll = scroll;
                ui.VirtualList(
                    items: all,
                    itemKey: Line.ById,
                    row: LineSlots.Rows,
                    realizeAll: true,
                    scroll: scroll,
                    height: 100
                );
            },
            View
        );
        var lines = Line.Many(100);
        var state = new ListState();
        using var driver = UiDriver.Render(
            ui =>
            {
                var scroll = ui.ScrollHandle();
                state.Scroll = scroll;
                ui.VirtualList(
                    items: lines,
                    itemKey: Line.ById,
                    row: LineSlots.Rows,
                    scroll: scroll,
                    height: 100
                );
            },
            View
        );

        var allScroll = await Assert.That(allState.Scroll).IsNotNull();
        var scroll = await Assert.That(state.Scroll).IsNotNull();
        all.Add(new Line { Id = 10, Name = "line 10" });
        allScroll.ScrollToIndex(10);
        stack.Settle();
        lines[6] = new Line { Id = 600, Name = "line 600" };
        scroll.ScrollToIndex(6);
        driver.Settle();

        await Assert.That(stack.Get("line 10").ViewBounds.Y).IsEqualTo(80f);
        var replaced = driver.Get("line 600").ViewBounds;
        await Assert.That(replaced.Y).IsGreaterThanOrEqualTo(0f);
        await Assert.That(replaced.Bottom).IsLessThanOrEqualTo(100f);
    }

    [Test]
    public async Task A_focused_row_is_kept_through_a_scroll_while_the_window_is_away_and_one_row_at_a_time()
    {
        var lines = Line.Many(1000);
        var state = new ListState();
        using var driver = Render(lines, state);
        driver.Click(driver.Get("line 2"));
        var two = driver.Get("line 2");

        var scroll = await Assert.That(state.Scroll).IsNotNull();
        driver.WindowFocus(false);
        scroll.ScrollTo(null, 1000);
        driver.Settle();
        scroll.ScrollTo(null, 2000);
        driver.Settle();
        driver.WindowFocus(true);
        var away = (driver.Focused == two, driver.Get("line 2") == two);
        scroll.ScrollTo(null, 1000);
        driver.Settle();
        var fiftyTwo = driver.Get("line 52");
        driver.Focus(fiftyTwo);
        scroll.ScrollTo(null, 4000);
        driver.Settle();

        await Assert.That(away).IsEqualTo((true, true));
        await Assert.That(driver.Focused).IsSameReferenceAs(fiftyTwo);
        await Assert.That(driver.Get("line 52")).IsSameReferenceAs(fiftyTwo);
        await Assert.That(fiftyTwo.ViewBounds.Bottom).IsLessThanOrEqualTo(0f);
        await Assert.That(() => driver.Get("line 2")).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task A_kept_focused_row_never_shows_in_the_viewport_wherever_its_estimate_falls()
    {
        var lines = Line.Many(1000);
        for (var i = 50; i < lines.Count; i++)
            lines[i].Height = 60;
        var state = new ListState();
        using var driver = Render(lines, state);
        var scroll = await Assert.That(state.Scroll).IsNotNull();
        scroll.ScrollTo(null, 700);
        driver.Settle();
        var row = driver.Get("line 40");
        driver.Focus(row);
        var shownAt = new List<float>();

        for (var y = 1000f; y <= 12_000f; y += 50f)
        {
            scroll.ScrollTo(null, y);
            driver.Settle();
            if (row.ViewBounds.Bottom > 0f && row.ViewBounds.Y < 200f)
                shownAt.Add(y);
        }

        await Assert.That(shownAt).IsEmpty();
        await Assert.That(driver.Focused).IsSameReferenceAs(row);
    }

    [Test]
    public async Task A_focused_rows_item_moved_in_and_out_of_the_realized_run_or_in_a_list_showing_every_row_keeps_the_same_focused_row()
    {
        var lines = Line.Many(200);
        var state = new ListState();
        using var driver = Render(lines, state);
        driver.Click(driver.Get("line 2"));
        var row = driver.Get("line 2");
        var followed = new List<bool>();

        foreach (var (from, to) in (ReadOnlySpan<(int, int)>)[(2, 3), (3, 150), (150, 151)])
        {
            lines.Move(from, to);
            driver.Settle();
            followed.Add(driver.Focused == row && driver.Get("line 2") == row);
        }
        var all = Line.Many(5);
        using var stack = UiDriver.Render(
            ui =>
                ui.VirtualList(
                    items: all,
                    itemKey: Line.ById,
                    row: LineSlots.Rows,
                    realizeAll: true
                ),
            View
        );
        stack.Click(stack.Get("line 2"));
        var stacked = stack.Get("line 2");
        var y = stacked.ViewBounds.Y;
        all.Move(2, 3);
        stack.Frame();

        await Assert.That(followed).IsEquivalentTo([true, true, true]);
        await Assert.That(stack.Focused).IsSameReferenceAs(stacked);
        await Assert.That(stacked.ViewBounds.Y).IsEqualTo(y + 20f);
    }

    static List<string> Texts(Node node)
    {
        var texts = new List<string>();
        foreach (var child in node.Children)
        {
            if (child.Kind == NodeKind.Text)
                texts.Add(child.Text.ToString());
        }
        return texts;
    }

    static List<string> Selected(Node list)
    {
        var names = new List<string>();
        foreach (var row in list.Children)
        {
            foreach (var component in row.Children)
            {
                foreach (var root in component.Children)
                {
                    if ((root.States & InputStates.Selected) != 0 && root.Id is { } id)
                        names.Add(id);
                }
            }
        }
        return names;
    }
}

internal sealed class Everything : ISelection
{
    public static readonly Everything Selected = new();

    public bool Contains(UiKey key) => true;

    public int Version => 0;
}

internal sealed class ListState
{
    public ScrollHandle? Scroll;
    public IItemsSource<Line>? Items;
    public readonly SelectionSet Selection = new();
    public int Reports;

    public void Apply(in SelectionChange change)
    {
        Reports++;
        if (Items is not { } items)
            throw new InvalidOperationException("A list that selects needs its items.");
        Selection.Apply(change, items, Line.ById);
    }
}

internal sealed class Pick(int id)
{
    public int Id { get; } = id;

    public string Name { get; } = "pick " + id;

    public static readonly KeyOf<Pick> ById = static pick => pick.Id;
}

internal sealed class PickSource : IFilteredRows
{
    readonly List<Pick> _rows = [];

    public PickSource(int count)
    {
        for (var i = 0; i < count; i++)
            _rows.Add(new Pick(i));
    }

    public IReadOnlyList<object> Rows => _rows;

    public int ShownCount => (_rows.Count + 1) / 2;

    public bool IsShown(int index) => _rows[index].Id % 2 == 0;

    public event RowsChangedHandler? Changed
    {
        add { }
        remove { }
    }
}

internal sealed class CountedLines(ObservableList<Line> lines)
    : IItemsSource<Line>,
        IItemsSourceListener
{
    readonly List<IItemsSourceListener> _listeners = [];
    readonly IItemsSource<Line> _lines = ItemsSource.From(lines);

    public ObservableList<Line> Lines { get; } = lines;

    public int Listeners => _listeners.Count;

    public int Count => Lines.Count;

    public object? this[int index] => Lines[index];

    public Line ItemAt(int index) => Lines[index];

    public int IndexOf(object? item) => item is Line line ? Lines.IndexOf(line) : -1;

    public void AddListener(IItemsSourceListener listener)
    {
        if (_listeners.Count == 0)
            _lines.AddListener(this);
        _listeners.Add(listener);
    }

    public void RemoveListener(IItemsSourceListener listener)
    {
        _listeners.Remove(listener);
        if (_listeners.Count == 0)
            _lines.RemoveListener(this);
    }

    public void Reset()
    {
        for (var i = 0; i < _listeners.Count; i++)
            _listeners[i].OnItemsChanged(this, ItemsChange.Reset);
    }

    public void OnItemsChanged(IItemsSource source, in ItemsChange change)
    {
        for (var i = 0; i < _listeners.Count; i++)
            _listeners[i].OnItemsChanged(this, change);
    }
}

public readonly record struct SourceCountProps
{
    public required IItemsSource Source { get; init; }
}

/// <summary>Writes how many items its source holds as it renders.</summary>
public sealed class SourceCount : Component<SourceCountProps>
{
    protected override void Render(Ui ui, in SourceCountProps p) =>
        ui.Text(ui.Format($"{p.Source.Count}"));
}

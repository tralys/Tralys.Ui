using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;
using TUnit.Assertions.Enums;
using static Tralys.Ui.Tests.TableNodes;

namespace Tralys.Ui.Tests;

public sealed class TableTests
{
    static readonly IItemsSource<Member> Five = ItemsSource.From(Team.Five());

    static UiDriver Render(
        TableState state,
        TableColumn<Member>[]? columns = null,
        IItemsSource<Member>? items = null,
        bool selectable = false,
        bool sorts = true,
        bool resizable = false,
        bool? reorderable = null,
        ScrollHandle? scroll = null,
        float width = 380,
        float height = 0
    ) =>
        UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
                ui.Table(
                    items: items ?? Five,
                    itemKey: Team.ByName,
                    columns: columns ?? Team.Columns(),
                    value: state.Value,
                    onValueChange: state.OnValueChange,
                    selectable: selectable,
                    sort: state.Sort,
                    onSortChange: sorts ? state.OnSortChange : default,
                    resizable: resizable,
                    reorderable: reorderable,
                    scroll: scroll,
                    realizeAll: height <= 0,
                    width: width,
                    height: height > 0 ? height : Length.Auto
                );
        });

    [Test]
    public async Task A_row_takes_the_band_and_opacity_its_look_gives_and_a_lit_row_its_light()
    {
        var band = Color.FromArgb(255, 30, 40, 50);
        using var driver = UiDriver.Render(ui =>
            ui.Table(
                items: Five,
                itemKey: Team.ByName,
                columns: Team.Columns(),
                rowLook: static m =>
                    m.Name == "Bram"
                        ? new TableRowLook
                        {
                            Background = Color.FromArgb(255, 30, 40, 50),
                            Opacity = 0.4f,
                        }
                        : default,
                width: 380
            )
        );
        var bram = Root(Row(driver, "Bram"));
        var look = (
            bram.Visual.Background,
            bram.Visual.Opacity,
            Root(Row(driver, "Alice")).Visual.Background.IsVisible
        );

        driver.Hover(bram);

        await Assert.That(look).IsEqualTo(((Tralys.Ui.Rendering.Paint)band, (float?)0.4f, false));
        await Assert
            .That(Root(Row(driver, "Bram")).Visual.Background)
            .IsNotEqualTo((Tralys.Ui.Rendering.Paint)band);
    }

    [Test]
    public async Task A_double_click_or_enter_on_a_row_activates_it_and_a_single_click_doesnt()
    {
        var activated = new List<string>();
        using var driver = UiDriver.Render(ui =>
            ui.Table(
                items: Five,
                itemKey: Team.ByName,
                columns: Team.Columns(),
                onRowActivate: Callback<UiKey>.Of(
                    activated,
                    static (a, key) => a.Add(key.ToString())
                ),
                width: 380
            )
        );
        var first = Team.Five()[0].Name;
        var second = Team.Five()[1].Name;

        driver.Click(Row(driver, first));
        driver.DoubleClick(Row(driver, first));
        driver.Press(Key.Down);
        driver.Press(Key.Enter);

        await Assert.That(activated).IsEquivalentTo([first, second]);
    }

    [Test]
    public async Task A_press_selects_a_row_only_once_the_table_opts_in_and_down_steps_the_selection_and_focus()
    {
        var plain = new TableState();
        using (var driver = Render(plain))
            driver.Click(Row(driver, "Alice"));
        var state = new TableState();
        using var many = Render(
            state,
            items: ItemsSource.From(Team.Many(60)),
            selectable: true,
            height: 200
        );

        many.Click(Row(many, "Member 00001"));
        for (var i = 0; i < 9; i++)
            many.Press(Key.Down);

        await Assert.That(plain.Values).IsEmpty();
        await Assert.That(state.Values[0]).IsEqualTo((UiKey)"Member 00001");
        await Assert.That(state.Values[^1]).IsEqualTo((UiKey)"Member 00010");
        var reached = Root(Row(many, "Member 00010"));
        await Assert.That(many.Focused).IsSameReferenceAs(reached);
        await Assert.That((reached.States & InputStates.Selected) != 0).IsTrue();
        await Assert.That(reached.ViewBounds.Bottom).IsLessThanOrEqualTo(200f);
    }

    [Test]
    public async Task A_header_click_reports_descending_then_ascending_then_none_and_its_header_shows_the_direction()
    {
        var state = new TableState { Sort = new TableSort("Level", SortDirection.Descending) };
        using var driver = Render(state).WithIcons();
        var start = Arrows(driver);

        driver.Click(Header(driver, "Name"));
        var descending = Arrows(driver);
        driver.Click(Header(driver, "Name"));
        var ascending = Arrows(driver);
        driver.Click(Header(driver, "Name"));
        var none = Arrows(driver);
        driver.Click(Header(driver, "Level"));

        await Assert
            .That(state.Sorts)
            .IsEquivalentTo(
                [
                    new TableSort("Name", SortDirection.Descending),
                    new TableSort("Name", SortDirection.Ascending),
                    new TableSort("Name", SortDirection.None),
                    new TableSort("Level", SortDirection.Descending),
                ],
                CollectionOrdering.Matching
            );
        await Assert.That(start).IsEquivalentTo(["", "", "down"], CollectionOrdering.Matching);
        await Assert.That(descending).IsEquivalentTo(["down", "", ""], CollectionOrdering.Matching);
        await Assert.That(ascending).IsEquivalentTo(["up", "", ""], CollectionOrdering.Matching);
        await Assert.That(none).IsEquivalentTo(["", "", ""], CollectionOrdering.Matching);
    }

    /// <summary>Each header's sort arrow, by the glyph it draws: up, down, or none.</summary>
    static string[] Arrows(UiDriver driver) =>
        [
            .. driver
                .All<TableHeaderCell>()
                .Select(header =>
                    header.Props<TableHeaderCellProps>().Sorted switch
                    {
                        SortDirection.Ascending when Arrow(driver, header) == IconRole.ArrowUp =>
                            "up",
                        SortDirection.Descending when Arrow(driver, header) == IconRole.ArrowDown =>
                            "down",
                        SortDirection.None when Arrow(driver, header) is null => "",
                        _ => "?",
                    }
                ),
        ];

    static IconRole? Arrow(UiDriver driver, Node header) =>
        driver
            .All<Icon>()
            .Where(icon => Within(icon, header) && !Root(icon).Place.Collapsed)
            .Select(icon => icon.Props<IconProps>())
            .Where(icon => icon.Variant == IconVariant.Plain)
            .Select(icon => icon.Glyph.Role)
            .FirstOrDefault();

    static bool Within(Node node, Node ancestor)
    {
        for (var at = node.Parent; at is not null; at = at.Parent)
        {
            if (at == ancestor)
                return true;
        }
        return false;
    }

    [Test]
    public async Task A_sort_arrow_is_drawn_in_the_accent()
    {
        var state = new TableState { Sort = new TableSort("Level", SortDirection.Ascending) };
        using var driver = Render(state).WithIcons();

        var arrow = driver
            .All<Icon>()
            .Single(icon => !Root(icon).Place.Collapsed)
            .Props<IconProps>();

        await Assert.That(arrow.Glyph.Role).IsEqualTo(IconRole.ArrowUp);
        await Assert.That(arrow.Color).IsEqualTo(Dark(ColorToken.FgAccent));
    }

    [Test]
    public async Task Headers_sort_nothing_without_a_sort_key_or_a_sort_report_and_the_row_past_the_last_column_sorts_nothing()
    {
        var off = new TableState { Sort = new TableSort("Level", SortDirection.Descending) };
        using (var driver = Render(off, sorts: false))
            driver.Click(Header(driver, "Name"));
        var state = new TableState { Sort = new TableSort("Level", SortDirection.Descending) };
        var columns = Team.Columns();
        columns[0] = new TableColumn<Member>
        {
            Header = "Name",
            Width = 160,
            Text = Team.Name,
        };
        using var table = Render(state, columns: columns, width: 500);

        table.Click(Header(table, "Name"));
        var row = Root(table.Get<Table.HeaderRow<Member>>()).ViewBounds;
        table.Click(new Vector2(row.Right - 10, row.Y + row.Height / 2));

        await Assert.That(off.Sorts).IsEmpty();
        await Assert.That(state.Sorts).IsEmpty();
    }

    [Test]
    public async Task A_sortable_header_lights_under_the_pointer_and_a_hand_where_one_that_doesnt_sort_stays_dark()
    {
        var state = new TableState();
        var columns = Team.Columns();
        columns[1] = new TableColumn<Member>
        {
            Header = "Role",
            Width = 120,
            Text = Team.Role,
        };
        using var driver = Render(state, columns: columns);
        var name = Header(driver, "Name");

        driver.Hover(name);
        var hovered = (Root(name).Visual.Background, driver.Cursor);
        var held = driver.Hold(name);
        var pressed = Root(name).Visual.Background;
        held.Release();
        driver.Hover(Header(driver, "Role"));

        await Assert.That(hovered).IsEqualTo((Dark(ColorToken.StateHover), Cursors.Hand));
        await Assert.That(pressed).IsEqualTo(Dark(ColorToken.StatePressed));
        await Assert.That(Root(Header(driver, "Role")).Visual.Background.IsVisible).IsFalse();
        await Assert.That(driver.Cursor).IsNotEqualTo(Cursors.Hand);
    }

    [Test]
    public async Task A_header_dragged_past_a_columns_middle_moves_its_column_there_marking_the_edge_and_sorting_nothing()
    {
        var state = new TableState();
        using var driver = Render(state);
        var role = Root(Header(driver, "Role")).ViewBounds;

        var held = driver.Hold(Header(driver, "Name"));
        held.MoveBy(new Vector2(role.X + role.Width * 0.75f - held.Position.X, 0f));
        var line = DropLine(driver);
        var lineAt = line?.ViewBounds;
        var lineColor = line?.Visual.Background;
        held.Release();
        var once = Headers(driver);
        var aliceAt =
            driver.GetText("Alice").ViewBounds.X - Root(Header(driver, "Name")).ViewBounds.X;

        var level = Root(Header(driver, "Level")).ViewBounds;
        held = driver.Hold(Header(driver, "Role"));
        held.MoveBy(new Vector2(level.X + level.Width * 0.75f - held.Position.X, 0f));
        held.Release();
        var toEnd = Headers(driver);
        var name = Root(Header(driver, "Name")).ViewBounds;
        held = driver.Hold(Header(driver, "Name"));
        held.MoveBy(new Vector2(name.X + name.Width * 0.75f - held.Position.X, 0f));
        held.Release();

        await Assert
            .That(once)
            .IsEquivalentTo(["Role", "Name", "Level"], CollectionOrdering.Matching);
        await Assert.That(aliceAt).IsEqualTo(6f);
        await Assert.That(lineAt?.X + lineAt?.Width / 2f).IsEqualTo(role.Right);
        await Assert.That(lineColor).IsEqualTo(Dark(ColorToken.BorderAccent));
        await Assert
            .That(toEnd)
            .IsEquivalentTo(["Name", "Level", "Role"], CollectionOrdering.Matching);
        await Assert.That(Headers(driver)).IsEquivalentTo(toEnd, CollectionOrdering.Matching);
        await Assert.That(DropLine(driver)).IsNull();
        await Assert.That(state.Sorts).IsEmpty();
    }

    [Test]
    public async Task A_column_taken_away_mid_header_drag_ends_the_drag_and_moves_nothing()
    {
        var all = Team.Columns();
        var columns = all;
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
                ui.Table(
                    items: Five,
                    itemKey: Team.ByName,
                    columns: columns,
                    realizeAll: true,
                    width: 380
                );
        });

        var held = driver.Hold(Header(driver, "Role"));
        held.MoveBy(new Vector2(-10f, 0f));
        columns = all[1..];
        driver.Rerender();
        held.MoveBy(new Vector2(-300f, 0f));
        held.Release();

        await Assert
            .That(Headers(driver))
            .IsEquivalentTo(["Role", "Level"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task A_header_drag_cut_short_by_the_window_hides_its_line_and_leaves_the_next_click_to_sort()
    {
        var state = new TableState();
        using var driver = Render(state);

        var held = driver.Hold(Header(driver, "Name"));
        held.MoveBy(new Vector2(10f, 0f));
        var shown = DropLine(driver) is not null;
        driver.WindowFocus(false);
        driver.WindowFocus(true);
        var after = DropLine(driver);
        driver.Click(Header(driver, "Name"));

        await Assert.That(shown).IsTrue();
        await Assert.That(after).IsNull();
        await Assert
            .That(state.Sorts)
            .IsEquivalentTo(
                [new TableSort("Name", SortDirection.Descending)],
                CollectionOrdering.Matching
            );
    }

    [Test]
    public async Task A_table_that_doesnt_reorder_keeps_its_columns_under_a_header_drag()
    {
        var state = new TableState();
        using var driver = Render(state, reorderable: false);

        driver.Drag(Header(driver, "Name"), new Vector2(300f, 0f));

        await Assert
            .That(Headers(driver))
            .IsEquivalentTo(["Name", "Role", "Level"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task A_gripper_follows_the_pointers_whole_travel_clamps_at_zero_and_a_cancel_puts_the_width_back()
    {
        var state = new TableState();
        using var driver = Render(state, resizable: true);
        var name = Header(driver, "Name");

        var held = driver.Hold(Gripper(driver, "Name"));
        held.MoveBy(new Vector2(10f, 0f), settle: false);
        held.MoveBy(new Vector2(10f, 0f));
        held.MoveBy(new Vector2(140f, 0f));
        held.Release();
        var widened = Root(name).ViewBounds.Width;
        var cell = driver.GetText("Alice").ViewBounds.Width;
        held = driver.Hold(Gripper(driver, "Name"));
        held.MoveBy(new Vector2(-500f, 0f));
        var clamped = Root(name).ViewBounds.Width;
        driver.WindowFocus(false);
        driver.WindowFocus(true);

        await Assert.That(widened).IsEqualTo(320f);
        await Assert.That(cell).IsEqualTo(320f - 12f);
        await Assert.That(clamped).IsEqualTo(0f);
        await Assert.That(Root(name).ViewBounds.Width).IsEqualTo(320f);
        await Assert
            .That(Headers(driver))
            .IsEquivalentTo(["Name", "Role", "Level"], CollectionOrdering.Matching);
        await Assert.That(state.Sorts).IsEmpty();
    }

    [Test]
    public async Task A_cancelled_gripper_drag_leaves_a_column_fitting_its_cells_fitting_them()
    {
        var state = new TableState();
        using var driver = Render(state, columns: Team.Columns(role: null), resizable: true);
        var role = Header(driver, "Role");
        var fitted = Root(role).ViewBounds.Width;

        var held = driver.Hold(Gripper(driver, "Role"));
        held.MoveBy(new Vector2(30f, 0f));
        var dragged = Root(role).ViewBounds.Width;
        driver.WindowFocus(false);

        await Assert.That(dragged).IsEqualTo(fitted + 30f);
        await Assert.That(Root(role).ViewBounds.Width).IsEqualTo(fitted);
        await Assert.That(fitted).IsGreaterThan(0f);
    }

    [Test]
    public async Task A_gripper_reaching_over_the_next_header_takes_the_press_and_shows_its_line_while_hovered_or_dragged()
    {
        var state = new TableState();
        using var driver = Render(state, resizable: true);
        var gripper = Gripper(driver, "Name");
        var role = Root(Header(driver, "Role")).ViewBounds;

        driver.Hover(gripper);
        var hovered = (Line(gripper).Place.Hidden, driver.Cursor);
        driver.Unhover();
        var left = Line(gripper).Place.Hidden;
        driver.Click(new Vector2(role.X + 2f, role.Y + role.Height / 2f));
        var held = driver.Hold(gripper);
        held.MoveBy(new Vector2(0f, 120f));
        var dragged = Line(gripper).Place.Hidden;
        held.Release();

        await Assert.That(hovered).IsEqualTo((false, Cursors.SizeWe));
        await Assert.That(left).IsTrue();
        await Assert.That(state.Sorts).IsEmpty();
        await Assert.That(dragged).IsFalse();
        await Assert.That(Line(gripper).Place.Hidden).IsTrue();
    }

    static Node Line(Node gripper) => Root(Root(gripper));

    [Test]
    public async Task A_table_that_doesnt_resize_shows_no_gripper()
    {
        using var driver = Render(new TableState());

        await Assert.That(driver.All<TableGripper>()).IsEmpty();
    }

    [Test]
    public async Task A_wheel_turn_over_the_header_row_scrolls_the_rows_down_and_sideways_with_the_headers_over_their_cells()
    {
        var rows = new ScrollHolder();
        using var driver = UiDriver.Render(ui =>
        {
            rows.Handle = ui.ScrollHandle();
            ui.Table(
                items: ItemsSource.From(Team.Many(40)),
                itemKey: Team.ByName,
                columns: Team.Columns(role: 600),
                scroll: rows.Handle,
                width: 380,
                height: 200
            );
        });
        driver.View.Context.SmoothScrolling = false;

        driver.Wheel(Header(driver, "Name"), new Vector2(0f, -1f));
        var down = rows.Handle!.Offset.Y;
        driver.Wheel(Header(driver, "Name"), new Vector2(0.5f, -1f));
        var name = Root(Header(driver, "Name")).ViewBounds;
        var row = driver.All<Table.Row<Member>>()[0];
        var cell = Cell(row, row.Props<TableRowProps<Member>>().Item.Name).ViewBounds;
        var sideways = Sideways(driver);
        sideways.ScrollTo(float.MaxValue, null);
        driver.Settle();
        var level = Root(Header(driver, "Level")).ViewBounds;
        var end = (sideways.Offset.X, Max: sideways.Extent.Width - sideways.Viewport.Width);
        var levelCell = Cell(
            row,
            row.Props<TableRowProps<Member>>().Item.Level.ToString()
        ).ViewBounds;

        await Assert.That(down).IsGreaterThan(0f);
        await Assert.That(rows.Handle.Offset.Y).IsGreaterThan(down);
        await Assert.That(Sideways(driver).Offset.X).IsGreaterThan(0f);
        await Assert.That(cell.X).IsEqualTo(name.X + 6f);
        await Assert.That(end.X).IsEqualTo(end.Max);
        await Assert.That(levelCell.X).IsEqualTo(level.X + 6f);
    }

    [Test]
    public async Task A_wheel_turn_in_a_headers_open_dropdown_leaves_the_rows_where_they_are()
    {
        var rows = new ScrollHolder();
        var columns = Team.Columns();
        columns[1] = new TableColumn<Member>
        {
            Width = 160,
            Text = Team.Role,
            HeaderContent = Slot.Of(static ui =>
                ui.Combobox(
                    items: Fruits.Items,
                    itemKey: Fruits.ByName,
                    itemText: Fruits.Named,
                    defaultOpen: true,
                    width: 140
                )
            ),
        };
        using var driver = UiDriver.Render(ui =>
        {
            rows.Handle = ui.ScrollHandle();
            ui.Table(
                items: ItemsSource.From(Team.Many(40)),
                itemKey: Team.ByName,
                columns: columns,
                scroll: rows.Handle,
                width: 480,
                height: 300
            );
        });

        driver.Wheel(driver.All<Combobox.Item>()[2], new Vector2(0f, -1f));

        await Assert.That(rows.Handle!.Offset.Y).IsEqualTo(0f);
    }

    [Test]
    public async Task A_finger_on_the_header_row_pans_the_rows_rather_than_a_board_around_them()
    {
        var rows = new ScrollHolder();
        var board = new ScrollHolder();
        using var driver = UiDriver.Render(ui =>
        {
            rows.Handle = ui.ScrollHandle();
            board.Handle = ui.ScrollHandle();
            using (ui.Box(overflow: Overflow.Scroll, scroll: board.Handle, width: 400, height: 300))
            using (ui.Box(width: 1200, height: 1200))
                ui.Table(
                    items: ItemsSource.From(Team.Many(40)),
                    itemKey: Team.ByName,
                    columns: Team.Columns(),
                    scroll: rows.Handle,
                    width: 380,
                    height: 120
                );
        });

        var finger = driver.Hold(Header(driver, "Role"), PointerKind.Touch);
        finger.MoveBy(new Vector2(0f, -60f), steps: 6);
        finger.Release();

        await Assert.That(rows.Handle!.Offset.Y).IsGreaterThan(0f);
        await Assert.That(board.Handle!.Offset).IsEqualTo(Vector2.Zero);
    }

    [Test]
    public async Task A_finger_dragging_a_header_across_drops_its_column_even_off_the_row_where_small_steps_on_a_wide_table_pan()
    {
        var rows = new ScrollHolder();
        var role = new Model();
        using var driver = UiDriver.Render(ui =>
        {
            rows.Handle = ui.ScrollHandle();
            ui.Table(
                items: ItemsSource.From(Team.Many(40)),
                itemKey: Team.ByName,
                columns: role.Count == 0 ? Team.Columns() : Team.Columns(role: 600),
                scroll: rows.Handle,
                width: 380,
                height: 300
            );
        });
        var level = Root(Header(driver, "Level")).ViewBounds;

        var finger = driver.Hold(Header(driver, "Role"), PointerKind.Touch);
        finger.MoveBy(new Vector2(level.X + level.Width * 0.75f - finger.Position.X, 0f));
        finger.MoveBy(new Vector2(0f, -200f));
        finger.Release();
        var dropped = Headers(driver);
        var still = rows.Handle!.Offset.Y;
        role.Count = 1;
        driver.Settle();
        finger = driver.Hold(Header(driver, "Role"), PointerKind.Touch);
        for (var step = 0; step < 8; step++)
            finger.MoveBy(new Vector2(-5f, 0f));
        finger.Release();

        await Assert
            .That(dropped)
            .IsEquivalentTo(["Name", "Level", "Role"], CollectionOrdering.Matching);
        await Assert.That(still).IsEqualTo(0f);
        await Assert.That(Sideways(driver).Offset.X).IsGreaterThan(0f);
        await Assert.That(Headers(driver)).IsEquivalentTo(dropped, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Tab_passes_the_headers_by_and_lands_on_the_rows()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
            {
                using (ui.Box(width: 20, height: 20, focusable: true, id: "before")) { }
                ui.Table(
                    items: Five,
                    itemKey: Team.ByName,
                    columns: Team.Columns(),
                    onSortChange: Callback<TableSort>.Of(static _ => { }),
                    resizable: true,
                    realizeAll: true,
                    width: 380
                );
            }
        });
        driver.Focus(driver.Get("before"));

        driver.Tab();

        await Assert.That(driver.Focused?.Kind).IsEqualTo(NodeKind.VirtualList);
    }

    [Test]
    public async Task An_info_text_puts_an_info_icon_beside_its_header_and_none_without()
    {
        var columns = Team.Columns();
        columns[1] = new TableColumn<Member>
        {
            Header = "Role",
            Info = "What the member does",
            Width = 120,
            Text = Team.Role,
        };
        columns[2] = new TableColumn<Member>
        {
            Header = "Level",
            Info = "",
            Width = 80,
        };
        using var driver = Render(new TableState(), columns: columns).WithIcons();
        var icons = driver
            .All<Icon>()
            .Where(icon => icon.Props<IconProps>().Variant == IconVariant.Info)
            .ToArray();
        var infos = icons
            .Select(icon =>
                Headers(driver)[
                    driver.All<TableHeaderCell>().ToList().FindIndex(h => Within(icon, h))
                ]
            )
            .ToArray();
        driver.Hover(Root(Header(driver, "Role")), settle: false);
        driver.Frame(Rest);
        var onHeader = Components.Tests.OverlayProbes.ShownTip(driver);

        driver.Hover(Root(icons[0]), settle: false);
        driver.Frame(Rest);

        await Assert.That(infos).IsEquivalentTo(["Role"]);
        await Assert
            .That((onHeader, Components.Tests.OverlayProbes.ShownTip(driver)))
            .IsEqualTo((null, "What the member does"));
    }

    const float Rest = 0.35f;

    [Test]
    public async Task An_empty_table_shows_its_empty_text_until_a_row_arrives()
    {
        var members = new Tralys.Ui.Mvvm.ObservableList<Member>();
        var items = ItemsSource.From(members);
        using var driver = UiDriver.Render(ui =>
            ui.Table(
                items: items,
                itemKey: Team.ByName,
                columns: Team.Columns(),
                emptyText: "Nobody yet",
                width: 380,
                height: 200
            )
        );
        var empty = driver.All<EmptyState>().Count;

        members.Add(Team.Five()[0]);
        driver.Settle();

        await Assert.That(empty).IsEqualTo(1);
        await Assert.That(driver.All<EmptyState>()).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Rungs))]
    public async Task Rows_stand_on_their_sizes_rung_in_every_theme_and_density_and_on_sm_by_default(
        ComponentSize? size,
        Theme theme,
        Density density
    )
    {
        using var driver = UiDriver.Render(
            ui =>
                ui.Table(
                    items: Five,
                    itemKey: Team.ByName,
                    columns: Team.Columns(),
                    size: size,
                    width: 380
                ),
            theme: theme,
            density: density
        );

        var height = Root(Row(driver, "Alice")).ViewBounds.Height;

        await Assert
            .That(height)
            .IsEqualTo(theme.Dimensions(density).Size(size ?? ComponentSize.Sm).Height)
            .Within(0.01f);
    }

    public static IEnumerable<(ComponentSize?, Theme, Density)> Rungs()
    {
        foreach (var theme in Themes.All)
        foreach (var density in Enum.GetValues<Density>())
        {
            yield return (null, theme, density);
            foreach (var size in Enum.GetValues<ComponentSize>())
                yield return (size, theme, density);
        }
    }

    [Test]
    public async Task Turned_off_sideways_a_table_clips_its_columns_and_a_sideways_wheel_keeps_its_headers_over_their_cells()
    {
        using var driver = UiDriver.Render(ui =>
            ui.Table(
                items: Five,
                itemKey: Team.ByName,
                columns: Team.Columns(name: 800),
                scrollsSideways: false,
                width: 200,
                height: 200
            )
        );
        var before = Root(Header(driver, "Name")).ViewBounds;

        driver.Wheel(Header(driver, "Name"), new Vector2(1f, 0f));

        await Assert.That(Root(Header(driver, "Name")).ViewBounds).IsEqualTo(before);
        await Assert.That(driver.GetText("Alice").ViewBounds.X).IsEqualTo(before.X + 6f);
        await Assert.That(Sideways(driver).Extent.Width).IsEqualTo(Sideways(driver).Viewport.Width);
    }

    [Test]
    public async Task Its_vertical_bar_stands_beside_the_overflowing_rows_under_the_header_and_hidden_they_scroll_with_none()
    {
        var many = ItemsSource.From(Team.Many(40));
        using var shown = UiDriver.Render(ui =>
            ui.Table(items: many, itemKey: Team.ByName, columns: Team.Columns(), height: 200)
        );
        using var hidden = UiDriver.Render(ui =>
            ui.Table(
                items: many,
                itemKey: Team.ByName,
                columns: Team.Columns(),
                verticalScrollbar: ScrollbarVisibility.Hidden,
                height: 200
            )
        );
        hidden.View.Context.SmoothScrolling = false;
        var bar = Root(shown.All<Scrollbar>().Single()).ViewBounds;
        var header = Root(shown.Get<Table.HeaderRow<Member>>()).ViewBounds;
        var row = Root(Row(shown, "Member 00001")).ViewBounds;

        hidden.Wheel(Root(Row(hidden, "Member 00001")), new Vector2(0f, -1f));

        await Assert.That((bar.Y, bar.Right)).IsEqualTo((header.Bottom, header.Right));
        await Assert.That(row.Right).IsEqualTo(bar.X);
        await Assert.That(hidden.All<Scrollbar>()).IsEmpty();
        await Assert
            .That(
                Walk(Root(hidden.Get<Table<Member>>()))
                    .First(n => n.Kind == NodeKind.VirtualList)
                    .Scroller!.Offset.Y
            )
            .IsGreaterThan(0f);
    }

    [Test]
    public async Task An_empty_table_wider_than_itself_still_scrolls_its_header_row_sideways()
    {
        using var driver = UiDriver.Render(ui =>
            ui.Table(
                items: ItemsSource.From<Member>([]),
                itemKey: Team.ByName,
                columns: Team.Columns(name: 800),
                width: 200,
                height: 200
            )
        );
        driver.View.Context.SmoothScrolling = false;
        var before = Root(Header(driver, "Name")).ViewBounds.X;

        driver.Wheel(Header(driver, "Name"), new Vector2(1f, 0f));

        await Assert.That(Root(Header(driver, "Name")).ViewBounds.X).IsLessThan(before);
    }

    [Test]
    public async Task A_text_column_shows_each_rows_text_and_one_with_no_width_fits_its_widest_cell_never_narrowing()
    {
        var members = Team.Many(200);
        members[0] = members[0] with { Name = "A name far wider than any other" };
        var rows = new ScrollHolder();
        using var driver = UiDriver.Render(ui =>
        {
            rows.Handle = ui.ScrollHandle();
            ui.Table(
                items: ItemsSource.From(members),
                itemKey: Team.ByName,
                columns: Team.Columns(name: null),
                scroll: rows.Handle,
                width: 600,
                height: 200
            );
        });
        var widest = driver.GetText("A name far wider than any other").ViewBounds.Width;
        var fitted = Root(Header(driver, "Name")).ViewBounds.Width;
        var role = Cell(Row(driver, members[1].Name), members[1].Role).ViewBounds;

        rows.Handle!.ScrollTo(null, 2000f);
        driver.Settle();

        await Assert.That(role.X).IsEqualTo(Root(Header(driver, "Role")).ViewBounds.X + 6f);
        await Assert.That(fitted).IsEqualTo(widest + 12f);
        await Assert.That(Root(Header(driver, "Name")).ViewBounds.Width).IsEqualTo(fitted);
    }

    static TableColumn<Member>[] Filling(float? name = 120) =>
        [
            new()
            {
                Header = "Name",
                Width = name,
                Fill = true,
                Text = Team.Name,
            },
            new()
            {
                Header = "Role",
                Width = 80,
                Fill = true,
                Text = Team.Role,
            },
            new()
            {
                Header = "Level",
                Width = 60,
                Cell = Team.Level,
            },
        ];

    static float Width(UiDriver driver, string header) =>
        Root(Header(driver, header)).ViewBounds.Width;

    [Test]
    public async Task Fill_columns_share_what_the_others_leave_of_the_viewport_and_fall_back_to_their_own_widths_as_it_narrows()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
                ui.Table(items: Five, itemKey: Team.ByName, columns: Filling(), height: 240);
        });
        var cells = new List<(float Header, float Cell)>();

        driver.View.Size = new Size(700, 600);
        driver.Settle();
        var wide = (Name: Width(driver, "Name"), Role: Width(driver, "Role"));
        var extent = Sideways(driver).Extent.Width;
        var viewport = Sideways(driver).Viewport.Width;
        foreach (var column in new[] { "Name", "Role", "Level" })
        {
            var at = Root(Header(driver, column)).ViewBounds;
            var cell = Walk(Root(Row(driver, "Alice")))
                .First(n => n.Kind == NodeKind.Box && n.ViewBounds.X == at.X);
            cells.Add((at.Width, cell.ViewBounds.Width));
        }
        driver.View.Size = new Size(200, 600);
        driver.Settle();

        await Assert.That(wide.Name - 120f).IsEqualTo(wide.Role - 80f);
        await Assert.That(wide.Name).IsGreaterThan(120f);
        await Assert.That(extent).IsEqualTo(viewport);
        await Assert.That(cells.All(c => c.Header == c.Cell)).IsTrue();
        await Assert.That((Width(driver, "Name"), Width(driver, "Role"))).IsEqualTo((120f, 80f));
    }

    [Test]
    public async Task A_gripper_on_a_fixed_column_takes_its_width_from_the_fill_and_one_on_a_fill_column_leaves_the_fill_once_released()
    {
        using var driver = UiDriver.Render(ui =>
            ui.Table(
                items: Five,
                itemKey: Team.ByName,
                columns: Filling(),
                resizable: true,
                width: 480
            )
        );
        var filled = Width(driver, "Name") + Width(driver, "Role");

        var held = driver.Hold(Gripper(driver, "Level"));
        held.MoveBy(new Vector2(30f, 0f));
        held.Release();
        var level = Width(driver, "Level");
        var fills = Width(driver, "Name") + Width(driver, "Role");
        var name = Width(driver, "Name");
        held = driver.Hold(Gripper(driver, "Name"));
        held.MoveBy(new Vector2(-30f, 0f));
        driver.WindowFocus(false);
        driver.WindowFocus(true);
        var cancelled = Width(driver, "Name");
        held = driver.Hold(Gripper(driver, "Name"));
        held.MoveBy(new Vector2(-30f, 0f));
        held.Release();
        var released = Width(driver, "Name");
        var role = Width(driver, "Role");

        await Assert.That(level).IsEqualTo(90f);
        await Assert.That(fills).IsEqualTo(filled - 30f);
        await Assert.That(Sideways(driver).Extent.Width).IsEqualTo(Sideways(driver).Viewport.Width);
        await Assert.That(cancelled).IsEqualTo(name);
        await Assert.That(released).IsEqualTo(name - 30f);
        await Assert.That(role).IsEqualTo(Width(driver, "Role"));
    }

    [Test]
    public async Task Columns_handed_anew_take_effect_a_column_given_no_width_after_none_fits_again_and_a_fill_column_keeps_filling()
    {
        var step = new Model();
        using var driver = UiDriver.Render(ui =>
            ui.Table(
                items: Five,
                itemKey: Team.ByName,
                columns: step.Count switch
                {
                    0 => Team.Columns(name: 0),
                    1 => Team.Columns(name: null),
                    _ => Filling(name: null),
                },
                width: 480
            )
        );
        var none = Width(driver, "Name");

        step.Count = 1;
        driver.Settle();
        var fits = Width(driver, "Name");
        var widest = Team.Five().Max(m => driver.GetText(m.Name).ViewBounds.Width);
        step.Count = 2;
        driver.Settle();

        await Assert.That(none).IsEqualTo(0f);
        await Assert.That(fits).IsEqualTo(widest + 12f);
        await Assert.That(Width(driver, "Name")).IsGreaterThan(fits);
        await Assert.That(Sideways(driver).Extent.Width).IsEqualTo(Sideways(driver).Viewport.Width);
    }

    [Test]
    public async Task A_table_offered_unbounded_width_settles_its_fill_columns()
    {
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box())
                ui.Table(items: Five, itemKey: Team.ByName, columns: Filling());
        });
        var settled = Width(driver, "Name");

        for (var i = 0; i < 4; i++)
            driver.Frame();

        await Assert.That(driver.Stats.Renders).IsEqualTo(0);
        await Assert.That(Width(driver, "Name")).IsEqualTo(settled);
    }

    [Test]
    public async Task A_full_row_spans_every_column_at_its_inset_while_the_other_rows_keep_their_cells()
    {
        var inset = new Model { Count = -1 };
        using var driver = UiDriver.Render(ui =>
            ui.Table(
                items: Five,
                itemKey: Team.ByName,
                columns: Team.Columns(100, 80, 60),
                fullRow: Slot<Member>.Of(static (ui, m, _) => ui.Text(m.Role, id: "spanning")),
                isFullRow: static m => m.Name == "Bram",
                fullRowInset: inset.Count < 0 ? null : new Thickness(inset.Count, 0, 0, 0),
                width: 380
            )
        );
        var row = Root(Row(driver, "Bram")).ViewBounds;
        var spanning = driver.Get("spanning").Parent!.ViewBounds;
        var at = driver.Get("spanning").ViewBounds.X;

        inset.Count = 0;
        driver.Settle();

        await Assert.That(spanning.Width).IsGreaterThanOrEqualTo(240f - 12f);
        await Assert.That(at).IsEqualTo(row.X + 6f);
        await Assert.That(driver.Get("spanning").ViewBounds.X).IsEqualTo(row.X);
        await Assert
            .That(Cell(Row(driver, "Alice"), "Engineer").ViewBounds.X)
            .IsEqualTo(row.X + 106f);
    }

    [Test]
    public async Task A_header_row_and_rows_on_their_own_lay_out_by_their_columns_with_each_cell_centred_down_its_row()
    {
        var columns = Team.Columns(level: null);
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, width: 400))
            {
                ui.TableHeaderRow(columns: columns);
                foreach (var member in Team.Five())
                    ui.TableRow(
                        item: member,
                        columns: columns,
                        size: ComponentSize.Lg,
                        key: member.Name
                    );
            }
        });
        var row = Root(Row(driver, "Alice")).ViewBounds;
        var name = driver.GetText("Alice").ViewBounds;
        var level = driver.GetText("120").ViewBounds;

        await Assert
            .That(Headers(driver))
            .IsEquivalentTo(["Name", "Role", "Level"], CollectionOrdering.Matching);
        await Assert
            .That(name.Y + name.Height / 2f)
            .IsEqualTo(row.Y + row.Height / 2f)
            .Within(0.5f);
        await Assert
            .That(level.Y + level.Height / 2f)
            .IsEqualTo(row.Y + row.Height / 2f)
            .Within(0.5f);
    }

    [Test]
    public async Task A_table_in_another_tables_cell_lays_out_its_own_columns()
    {
        var inner = new TableColumn<Member>[]
        {
            new()
            {
                Header = "Inner",
                Width = 50,
                Text = Team.Name,
            },
        };
        var nested = new Nested(
            inner,
            Team.Five()
                .ToDictionary(
                    m => m.Name,
                    m => ItemsSource.From(new[] { m with { Name = m.Name + " inside" } })
                )
        );
        var outer = new TableColumn<Member>[]
        {
            new()
            {
                Header = "Outer",
                Width = 300,
                Cell = Slot<Member>.Of(
                    nested,
                    static (ui, n, m, _) =>
                        ui.Table(
                            items: n.Items[m.Name],
                            itemKey: Team.ByName,
                            columns: n.Columns,
                            realizeAll: true
                        )
                ),
            },
        };
        using var driver = UiDriver.Render(ui =>
            ui.Table(items: Five, itemKey: Team.ByName, columns: outer, width: 380)
        );

        var text = driver.GetText("Alice inside").ViewBounds;
        var cell = Root(
            driver
                .All<TableHeaderCell>()
                .First(n => n.Props<TableHeaderCellProps>().Header.ToString() == "Inner")
        ).ViewBounds;

        await Assert.That(cell.Width).IsEqualTo(50f);
        await Assert.That(text.Width).IsEqualTo(50f - 12f);
    }

    [Test]
    public async Task A_table_out_of_the_view_mid_header_drag_comes_back_with_no_drop_line_and_its_new_headers()
    {
        var shown = new Model { Count = 1 };
        var header = new Model { Name = "Name" };
        using var driver = UiDriver.Render(ui =>
        {
            if (shown.Count == 0)
                return;
            var columns = Team.Columns();
            columns[0] = new TableColumn<Member>
            {
                Header = header.Name,
                Width = 160,
                Text = Team.Name,
            };
            ui.Table(items: Five, itemKey: Team.ByName, columns: columns, width: 380);
        });

        var held = driver.Hold(Header(driver, "Name"));
        held.MoveBy(new Vector2(60f, 0f));
        shown.Count = 0;
        driver.Settle();
        header.Name = "Calling";
        shown.Count = 1;
        driver.Settle();
        held.Release();

        await Assert.That(DropLine(driver)).IsNull();
        await Assert
            .That(Headers(driver))
            .IsEquivalentTo(["Calling", "Role", "Level"], CollectionOrdering.Matching);
    }

    sealed record Nested(
        TableColumn<Member>[] Columns,
        Dictionary<string, IItemsSource<Member>> Items
    );
}

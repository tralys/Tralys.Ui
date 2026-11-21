using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;
using static Tralys.Ui.Tests.TableNodes;

namespace Tralys.Ui.Tests;

public sealed class TableAllocationTests
{
    static readonly IItemsSource<Member> TenThousand = ItemsSource.From(Team.Many(10_000));

    static readonly TableColumn<Member>[] Columns = Team.Columns();

    [Test]
    public async Task Wheeling_a_ten_thousand_row_table_down_every_frame_allocates_nothing()
    {
        using var driver = UiDriver.Render(static ui =>
            ui.Table(
                items: TenThousand,
                itemKey: Team.ByName,
                columns: Columns,
                width: 380,
                height: 320
            )
        );
        driver.View.Context.SmoothScrolling = false;
        var list = Walk(driver.Root).First(n => n.Kind == NodeKind.VirtualList);

        var bytes = driver.WorstFrameBytes(
            list,
            static (d, l) =>
            {
                d.Wheel(l, new Vector2(0, -1), settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task Down_on_a_focused_row_of_a_ten_thousand_row_table_every_frame_fed_back_allocates_nothing()
    {
        var state = new TableState { Records = false };
        using var driver = UiDriver.Render(ui =>
            ui.Table(
                items: TenThousand,
                itemKey: Team.ByName,
                columns: Columns,
                value: state.Value,
                onValueChange: state.OnValueChange,
                selectable: true,
                width: 380,
                height: 320
            )
        );
        driver.Click(Row(driver, "Member 00001"));

        var bytes = driver.WorstFrameBytes(static d =>
        {
            d.Press(Key.Down, settle: false);
            d.Frame();
        });

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(state.Reports).IsGreaterThan(100);
    }

    [Test]
    public async Task A_sideways_wheel_over_a_wide_tables_header_every_frame_allocates_nothing()
    {
        using var driver = UiDriver.Render(static ui =>
            ui.Table(
                items: TenThousand,
                itemKey: Team.ByName,
                columns: Team.Columns(name: 400, role: 400),
                width: 380,
                height: 320
            )
        );
        driver.View.Context.SmoothScrolling = false;
        var walk = new Steps(Header(driver, "Name"));

        var bytes = driver.WorstFrameBytes(
            walk,
            static (d, w) =>
            {
                d.Wheel(w.Target, new Vector2(w.Tick++ % 16 < 8 ? 1f : -1f, 0f), settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_gripper_dragged_back_and_forth_every_frame_allocates_nothing()
    {
        using var driver = UiDriver.Render(static ui =>
            ui.Table(
                items: TenThousand,
                itemKey: Team.ByName,
                columns: Columns,
                resizable: true,
                width: 380,
                height: 320
            )
        );
        var moves = new Moves(driver.Hold(Gripper(driver, "Name")), new Vector2(3f, 0f));

        var bytes = driver.WorstFrameBytes(
            moves,
            static (d, m) =>
            {
                m.Press.MoveBy(m.Tick++ % 16 < 8 ? m.By : -m.By, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_header_click_resorting_a_ten_thousand_row_data_table_every_few_frames_allocates_nothing()
    {
        var state = new DataTableState { Records = false };
        using var driver = UiDriver.Render(ui =>
            ui.DataTable(
                items: TenThousand,
                itemKey: Team.ByName,
                columns: Columns,
                sort: state.Sort,
                onSortChange: state.OnSortChange,
                width: 380,
                height: 320
            )
        );
        var walk = new Steps(Header(driver, "Level"));

        var bytes = driver.WorstFrameBytes(
            walk,
            static (d, w) =>
            {
                if (w.Tick++ % 10 == 0)
                    d.Click(w.Target, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
    }

    [Test]
    public async Task A_paged_data_tables_pager_stepped_every_few_frames_allocates_nothing()
    {
        var state = new DataTableState { Records = false };
        using var driver = UiDriver.Render(ui =>
            ui.DataTable(
                items: TenThousand,
                itemKey: Team.ByName,
                columns: Columns,
                page: state.Page,
                onPageChange: state.OnPageChange,
                pageSize: 1000,
                width: 380,
                height: 320
            )
        );
        var steps = new PagerSteps(driver, state);

        var bytes = driver.WorstFrameBytes(
            steps,
            static (d, s) =>
            {
                if (s.Tick++ % 4 == 0)
                    d.Click(s.State.Page >= 10 ? s.First : s.Next, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(state.Reports).IsGreaterThan(20);
    }

    sealed class Steps(Node target)
    {
        public readonly Node Target = target;
        public int Tick;
    }

    sealed class PagerSteps(UiDriver driver, DataTableState state)
    {
        public readonly DataTableState State = state;
        public readonly Node First = Step(driver, IconRole.ChevronsLeft);
        public readonly Node Next = Step(driver, IconRole.ChevronRight);
        public int Tick;

        static Node Step(UiDriver driver, IconRole icon) =>
            driver.All<IconButton>().Single(n => n.Props<IconButtonProps>().Icon.Role == icon);
    }
}

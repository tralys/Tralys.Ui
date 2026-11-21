using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;
using TUnit.Assertions.Enums;
using static Tralys.Ui.Tests.TableNodes;

namespace Tralys.Ui.Tests;

public sealed partial class DataTableTests
{
    static UiDriver Render(DataTableState state, IItemsSource<Member> items, int pageSize = 0) =>
        UiDriver.Render(ui =>
            ui.DataTable(
                items: state.Items ?? items,
                itemKey: Team.ByName,
                columns: Team.Columns(),
                page: state.Page,
                onPageChange: state.OnPageChange,
                pageSize: state.PageSize ?? pageSize,
                sort: state.Sort,
                onSortChange: state.OnSortChange,
                searchText: state.Search,
                filter: state.Filter,
                loading: state.Loading,
                value: state.Value,
                onValueChange: state.OnValueChange,
                selectable: true,
                emptyText: "Nobody",
                noMatchText: "No match",
                width: 380,
                height: 240
            )
        );

    static Node Step(UiDriver driver, IconRole icon) =>
        driver.All<IconButton>().Single(n => n.Props<IconButtonProps>().Icon.Role == icon);

    static ScrollHandle Rows(UiDriver driver)
    {
        var list = Walk(Root(driver.Get<DataTable<Member>>()))
            .First(n => n.Kind == NodeKind.VirtualList);
        if (list.Scroller is not { } rows)
            throw new InvalidOperationException("The data table's rows have no scroller.");
        return rows;
    }

    static string? EmptyText(UiDriver driver)
    {
        var empty = driver.All<EmptyState>().SingleOrDefault();
        if (empty is null)
            return null;
        var props = empty.Props<EmptyStateProps>();
        return (props.SearchMiss ? props.NoMatchText : props.Text).ToString();
    }

    [Test]
    public async Task The_pager_reports_the_page_it_turns_to_and_a_new_page_shows_from_its_top()
    {
        var state = new DataTableState();
        using var driver = Render(state, ItemsSource.From(Team.Many(30)), pageSize: 10);
        driver.View.Context.SmoothScrolling = false;
        driver.Wheel(Row(driver, "Member 00005"), new Vector2(0f, -2f));
        var wheeled = Rows(driver).Offset.Y;

        driver.Click(Step(driver, IconRole.ChevronRight));

        await Assert.That(wheeled).IsGreaterThan(0f);
        await Assert.That(state.Pages).IsEquivalentTo([2]);
        await Assert.That(driver.GetText("Page 2 of 3")).IsNotNull();
        await Assert.That(Shown(driver)[0]).IsEqualTo("Member 00011");
        await Assert.That(Rows(driver).Offset.Y).IsEqualTo(0f);
    }

    [Test]
    public async Task Header_clicks_order_held_rows_by_value_down_then_up_then_as_given_and_tied_rows_keep_their_order()
    {
        var members = new[]
        {
            new Member("Alice", "Engineer", 120),
            new Member("Bram", "Designer", 9),
            new Member("Cyra", "Writer", 104),
            new Member("Doran", "analyst", 87),
            new Member("Elin", "Engineer", 112),
        };
        var state = new DataTableState();
        using var driver = Render(state, ItemsSource.From(members));
        var orders = new List<string>();

        for (var i = 0; i < 4; i++)
        {
            driver.Click(Header(driver, "Level"));
            orders.Add(string.Join(",", Shown(driver)));
        }
        driver.Click(Header(driver, "Role"));

        await Assert
            .That(orders)
            .IsEquivalentTo(
                [
                    "Alice,Elin,Cyra,Doran,Bram",
                    "Bram,Doran,Cyra,Elin,Alice",
                    "Alice,Bram,Cyra,Doran,Elin",
                    "Alice,Elin,Cyra,Doran,Bram",
                ],
                CollectionOrdering.Matching
            );
        await Assert
            .That(Shown(driver))
            .IsEquivalentTo(
                ["Cyra", "Alice", "Elin", "Bram", "Doran"],
                CollectionOrdering.Matching
            );
    }

    [Test]
    public async Task A_sort_or_new_items_report_the_first_page_and_a_sort_a_search_or_a_page_size_bring_the_rows_back_to_the_top()
    {
        var state = new DataTableState { Page = 2, PageSize = 20 };
        using var driver = Render(state, ItemsSource.From(Team.Many(60)));
        driver.View.Context.SmoothScrolling = false;

        driver.Click(Header(driver, "Name"));
        var sorted = state.Pages.ToArray();
        state.Page = 2;
        driver.Settle();
        state.Items = ItemsSource.From(Team.Many(50));
        driver.Settle();
        var replaced = state.Pages.ToArray();
        var tops = new List<float>();
        foreach (
            var change in new Action[]
            {
                () => state.Sort = new TableSort("Level", SortDirection.Descending),
                () => state.Search = "Member",
                () => state.PageSize = 25,
            }
        )
        {
            Rows(driver).ScrollTo(null, 200f);
            driver.Settle();
            var scrolled = Rows(driver).Offset.Y;
            change();
            driver.Settle();
            tops.Add(scrolled > 0f ? Rows(driver).Offset.Y : -1f);
        }

        await Assert.That(sorted).IsEquivalentTo([1]);
        await Assert.That(replaced).IsEquivalentTo([1, 1]);
        await Assert.That(tops).IsEquivalentTo([0f, 0f, 0f]);
    }

    [Test]
    public async Task A_search_finds_any_columns_text_ignoring_case_from_the_first_page_and_a_miss_shows_the_no_match_text()
    {
        var members = Team.Many(40);
        var state = new DataTableState { Page = 3, PageSize = 5 };
        using var driver = Render(state, ItemsSource.From(members));

        state.Search = "WRITER";
        driver.Settle();
        var writers = Shown(driver);
        var pages = driver.GetText("Page 1 of 2").Text.ToString();
        var miss = EmptyText(driver);
        state.Search = "nobody at all";
        driver.Settle();

        await Assert.That(state.Pages).IsEquivalentTo([1]);
        await Assert
            .That(writers)
            .IsEquivalentTo(members.Where(m => m.Role == "Writer").Take(5).Select(m => m.Name));
        await Assert.That(pages).IsEqualTo("Page 1 of 2");
        await Assert.That(miss).IsNull();
        await Assert.That(Shown(driver)).IsEmpty();
        await Assert.That(EmptyText(driver)).IsEqualTo("No match");
    }

    [Test]
    public async Task A_filter_reading_what_changes_drops_the_rows_it_no_longer_matches_keeping_the_focus_on_its_row()
    {
        var roles = new Roles();
        var state = new DataTableState { Filter = roles.Shows };
        using var driver = Render(state, ItemsSource.From(Team.Five()));
        driver.Click(Row(driver, "Cyra"));

        roles.HideDesigners = true;
        driver.Settle();

        await Assert
            .That(Shown(driver))
            .IsEquivalentTo(["Alice", "Cyra", "Doran", "Elin"], CollectionOrdering.Matching);
        await Assert.That(driver.Focused).IsSameReferenceAs(Root(Row(driver, "Cyra")));
    }

    [Test]
    public async Task An_empty_page_the_app_answered_a_search_with_shows_the_no_match_text()
    {
        var search = "zz";
        using var driver = UiDriver.Render(ui =>
            ui.DataTable(
                items: ItemsSource.From(Array.Empty<Member>()),
                itemKey: Team.ByName,
                columns: Team.Columns(),
                totalItems: 0,
                searchText: search,
                emptyText: "Nobody",
                noMatchText: "No match",
                width: 380,
                height: 240
            )
        );
        var missed = EmptyText(driver);

        search = "";
        driver.Rerender();

        await Assert.That((missed, EmptyText(driver))).IsEqualTo(("No match", "Nobody"));
    }

    [Test]
    public async Task While_loading_a_table_is_not_empty_keeps_its_page_and_shows_its_rows_sorted_as_they_arrive()
    {
        var members = new ObservableList<Member>();
        var state = new DataTableState
        {
            Loading = true,
            Sort = new TableSort("Level", SortDirection.Descending),
        };
        using (var driver = Render(state, ItemsSource.From(members)))
        {
            var loading = EmptyText(driver);
            members.Add(new Member("Low", "Engineer", 3));
            members.Add(new Member("High", "Engineer", 90));
            members.Add(new Member("Mid", "Engineer", 40));
            state.Loading = false;
            driver.Settle();

            await Assert.That(loading).IsNull();
            await Assert
                .That(Shown(driver))
                .IsEquivalentTo(["High", "Mid", "Low"], CollectionOrdering.Matching);
        }

        var paged = new ObservableList<Member>();
        var later = new DataTableState
        {
            Loading = true,
            Page = 2,
            PageSize = 1,
        };
        using var table = Render(later, ItemsSource.From(paged));
        var none = Shown(table);
        paged.Add(new Member("First", "Engineer", 1));
        paged.Add(new Member("Second", "Engineer", 2));
        table.Settle();
        var second = Shown(table);
        paged.RemoveAt(1);
        later.Loading = false;
        table.Settle();

        await Assert.That(none).IsEmpty();
        await Assert.That(second).IsEquivalentTo(["Second"]);
        await Assert.That(later.Pages).IsEquivalentTo([1]);
        await Assert.That(Shown(table)).IsEquivalentTo(["First"]);
    }

    [Test]
    public async Task A_row_added_keeps_the_rows_built_and_a_selected_row_removed_settles_to_none_selected()
    {
        var members = new ObservableList<Member>();
        foreach (var member in Team.Five()[..3])
            members.Add(member);
        var state = new DataTableState();
        using var driver = Render(state, ItemsSource.From(members));
        var built = driver.All<Table.Row<Member>>().Select(driver.MountId).ToArray();

        members.Add(Team.Five()[3]);
        driver.Settle();
        var kept = Team.Five()[..3].Select(m => driver.MountId(Row(driver, m.Name))).ToArray();
        driver.Click(Row(driver, "Cyra"));
        members.RemoveAt(2);
        state.Value = default;
        driver.Settle();

        await Assert.That(kept).IsEquivalentTo(built);
        await Assert
            .That(Shown(driver))
            .IsEquivalentTo(["Alice", "Bram", "Doran"], CollectionOrdering.Matching);
        await Assert
            .That(
                driver
                    .All<Table.Row<Member>>()
                    .Any(n => (Root(n).States & InputStates.Selected) != 0)
            )
            .IsFalse();
    }

    [Test]
    public async Task With_rows_for_one_page_or_none_the_pager_and_its_rule_are_gone_and_with_none_the_empty_text_shows()
    {
        var state = new DataTableState { PageSize = 10 };
        using var driver = Render(state, ItemsSource.From<Member>([]));

        await Assert.That(driver.All<Pagination>()).IsEmpty();
        await Assert.That(driver.All<Separator>()).IsEmpty();
        await Assert.That(EmptyText(driver)).IsEqualTo("Nobody");
    }

    sealed partial class Roles : ObservableObject
    {
        public Roles() => Shows = Matches;

        public Predicate<Member> Shows { get; }

        [ObservableProperty]
        public partial bool HideDesigners { get; set; }

        bool Matches(Member member) => !(HideDesigners && member.Role == "Designer");
    }
}

/// <summary>What a data table reported, fed back as its props the way an app's state would be.</summary>
internal sealed partial class DataTableState : ObservableObject
{
    public readonly List<int> Pages = [];

    /// <summary>Whether each page reported joins <see cref="Pages"/>; off, reports only count, allocating nothing.</summary>
    public bool Records = true;

    public int Reports;

    public DataTableState()
    {
        OnPageChange = Callback<int>.Of(
            this,
            static (s, page) =>
            {
                s.Reports++;
                if (s.Records)
                    s.Pages.Add(page);
                s.Page = page;
            }
        );
        OnSortChange = Callback<TableSort>.Of(this, static (s, sort) => s.Sort = sort);
        OnValueChange = Callback<UiKey>.Of(this, static (s, value) => s.Value = value);
    }

    public Callback<int> OnPageChange { get; }

    public Callback<TableSort> OnSortChange { get; }

    public Callback<UiKey> OnValueChange { get; }

    [ObservableProperty]
    public partial int Page { get; set; }

    [ObservableProperty]
    public partial int? PageSize { get; set; }

    [ObservableProperty]
    public partial TableSort Sort { get; set; }

    [ObservableProperty]
    public partial string? Search { get; set; }

    [ObservableProperty]
    public partial Predicate<Member>? Filter { get; set; }

    [ObservableProperty]
    public partial bool Loading { get; set; }

    [ObservableProperty]
    public partial UiKey Value { get; set; }

    [ObservableProperty]
    public partial IItemsSource<Member>? Items { get; set; }
}

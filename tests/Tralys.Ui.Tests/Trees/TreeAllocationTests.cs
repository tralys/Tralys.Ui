using Tralys.Ui.Controls;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class TreeAllocationTests
{
    [Test]
    public async Task A_thousand_row_tree_opened_shut_and_walked_by_its_keys_every_frame_allocates_nothing()
    {
        var id = 0;
        var regions = new Leaf[10];
        for (var r = 0; r < regions.Length; r++)
        {
            var towns = new Leaf[10];
            for (var t = 0; t < towns.Length; t++)
            {
                var districts = new Leaf[10];
                for (var d = 0; d < districts.Length; d++)
                    districts[d] = new Leaf(id++, $"District {d + 1}");
                towns[t] = new Leaf(id++, $"Town {t + 1}", districts);
            }
            regions[r] = new Leaf(id++, $"Region {r + 1}", towns);
        }
        var grove = new Grove(regions) { Logs = false };
        using var driver = grove.Render(height: 320);
        driver.Click(driver.Get("Region 1"));
        var walk = new Walk();

        var bytes = driver.WorstFrameBytes(
            walk,
            static (d, w) =>
            {
                var key = Keys[w.Step++ % Keys.Length];
                d.KeyDown(key, settle: false);
                d.KeyUp(key, settle: false);
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(grove.NameOf(grove.Value)).IsEqualTo(driver.Focused?.Id ?? "");
    }

    static readonly Key[] Keys =
    [
        Key.Right,
        Key.Down,
        Key.Down,
        Key.Left,
        Key.Left,
        Key.Down,
        Key.Multiply,
        Key.Subtract,
        Key.End,
        Key.Home,
    ];

    [Test]
    public async Task A_tree_moving_its_focused_row_and_replacing_another_every_frame_allocates_nothing_and_keeps_the_focus()
    {
        var grove = new Grove(
            new Leaf(1, "A"),
            new Leaf(2, "B"),
            new Leaf(3, "C"),
            new Leaf(4, "D"),
            new Leaf(5, "E"),
            new Leaf(6, "F")
        )
        {
            Logs = false,
        };
        var swaps = new Leaf[] { new(7, "D2"), grove.Roots[3] };
        using var driver = grove.Render();
        driver.Click(driver.Get("B"));
        var focused = driver.Focused;
        var walk = new Walk();

        var bytes = driver.WorstFrameBytes(
            (grove, swaps, walk),
            static (d, s) =>
            {
                var roots = s.grove.Roots;
                var b = roots[0].Id == 2 ? 0 : 1;
                var moved = roots[b];
                roots.RemoveAt(b);
                roots.Insert(1 - b, moved);
                roots[3] = s.swaps[s.walk.Step++ % 2];
                s.grove.Rows.Refresh();
                d.Frame();
            }
        );

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(driver.Focused).IsEqualTo(focused);
    }

    [Test]
    public async Task A_lists_tree_rows_opened_filtered_and_sorted_again_every_frame_allocate_nothing()
    {
        var id = 0;
        var branches = new Leaf[6];
        for (var b = 0; b < branches.Length; b++)
        {
            var leaves = new Leaf[8];
            for (var l = 0; l < leaves.Length; l++)
                leaves[l] = new Leaf(id++, $"Leaf {b}.{l}") { Score = (b * 8 + l) * 7 % 50 };
            branches[b] = new Leaf(id++, $"Branch {b}", leaves).Open();
        }
        var grove = new Grove(branches) { Logs = false };
        var scores = new Threshold();
        grove.Rows.Comparer = Comparer<Leaf>.Create(static (a, b) => a.Score.CompareTo(b.Score));
        var filter = new RowFilter<Leaf>(scores.Passes);
        grove.Rows.Filter = filter;
        var row = Slot<Leaf>.Of(static (ui, leaf, _) => ui.Text(leaf.Name));
        using var driver = UiDriver
            .Render(ui =>
                ui.VirtualList(items: grove.Rows, itemKey: Leaf.Key, row: row, height: 400)
            )
            .WithIcons();
        var churn = (grove, scores, filter, walk: new Walk());

        var bytes = driver.WorstFrameBytes(churn, Churn);

        await Assert.That(bytes).IsEqualTo(0L);
        await Assert.That(churn.walk.Most).IsGreaterThan(churn.walk.Fewest);
    }

    static void Churn(
        UiDriver driver,
        (Grove grove, Threshold scores, RowFilter<Leaf> filter, Walk walk) s
    )
    {
        var step = s.walk.Step++;
        var branch = s.grove.Roots[step % s.grove.Roots.Count];
        branch.IsExpanded = !branch.IsExpanded;
        if (step % 3 == 0)
        {
            s.scores.Least = (s.scores.Least + 11) % 40;
            s.filter.Refresh();
        }
        if (step % 5 == 0)
        {
            var leaves = s.grove.Roots[0].Children;
            (leaves[0].Score, leaves[1].Score) = (leaves[1].Score, leaves[0].Score);
            s.grove.Rows.Refresh();
        }
        s.walk.Fewest = Math.Min(s.walk.Fewest, s.grove.Rows.Count);
        s.walk.Most = Math.Max(s.walk.Most, s.grove.Rows.Count);
        driver.Frame();
    }

    sealed class Threshold
    {
        public int Least;

        public bool Passes(Leaf leaf) => leaf.Children.Count > 0 || leaf.Score >= Least;
    }

    sealed class Walk
    {
        public int Step;
        public int Fewest = int.MaxValue;
        public int Most;
    }
}

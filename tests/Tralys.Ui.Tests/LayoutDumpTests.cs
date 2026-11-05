using Tralys.Ui.Testing;
using TUnit.Assertions.Enums;

namespace Tralys.Ui.Tests;

public sealed class LayoutDumpTests
{
    [Test]
    public async Task Siblings_of_one_kind_sharing_an_id_are_indexed_while_another_kind_keeps_it()
    {
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(id: "stack"))
                {
                    using (ui.Box(width: 1, height: 1, id: "slot")) { }
                    using (ui.Box(width: 1, height: 1, id: "slot")) { }
                    ui.Text("x", id: "slot");
                    using (ui.Box(width: 1, height: 1, id: "other")) { }
                }
            },
            new Size(100, 100)
        );

        var steps = LayoutDump
            .Of(driver.View)
            .Nodes.Select(static n => n.Path[(n.Path.LastIndexOf('/') + 1)..]);

        await Assert
            .That(steps)
            .IsEquivalentTo(
                ["Box#stack", "Box[0]", "Box[1]", "Text#slot", "Box#other"],
                CollectionOrdering.Matching
            );
    }

    [Test]
    public async Task A_nodes_dump_holds_its_subtree_and_the_layers_opened_in_it_by_paths_from_its_own_step()
    {
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(direction: Direction.Row, align: Align.Start))
                {
                    using (ui.Box(width: 50, height: 20, id: "window"))
                    {
                        ui.Text("in", id: "label");
                        using (ui.Layer(id: "popup"))
                            ui.Text("over");
                    }
                    using (ui.Box(width: 30, height: 20, id: "beside")) { }
                }
            },
            new Size(200, 100)
        );

        var whole = LayoutDump.Of(driver.View).Nodes;
        var part = LayoutDump.Of(driver.Get("window")).Nodes;

        await Assert
            .That(part.Select(static n => n.Path))
            .IsEquivalentTo(
                [
                    "Box#window",
                    "Box#window/Text#label",
                    "Box#window/Layer#popup",
                    "Box#window/Layer#popup/Text[0]",
                ],
                CollectionOrdering.Matching
            );
        foreach (var entry in part)
        {
            await Assert
                .That(whole.Any(w => w.Path.EndsWith("/" + entry.Path) && w.Bounds == entry.Bounds))
                .IsTrue();
        }
    }

    [Test]
    public async Task The_roots_dump_is_the_views_under_one_line_for_the_root()
    {
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(id: "frame"))
                    ui.Text("x");
            },
            new Size(100, 50)
        );

        var whole = LayoutDump.Of(driver.View).Nodes;
        var rooted = LayoutDump.Of(driver.View.Root).Nodes;

        await Assert
            .That(rooted[0])
            .IsEqualTo(new LayoutEntry("Root", new Rect(0, 0, 100, 50), null, false, false));
        await Assert
            .That(rooted.Skip(1))
            .IsEquivalentTo(
                whole.Select(static n => n with { Path = "Root/" + n.Path }),
                CollectionOrdering.Matching
            );
    }

    [Test]
    public async Task A_consume_map_reads_where_a_press_is_taken_across_the_views_pixels_at_its_scale()
    {
        using var driver = UiDriver.Render(
            ui =>
            {
                using (ui.Box(align: Align.Start, background: Color.FromRgb(3, 3, 3)))
                using (ui.Box(width: 50, height: 25, background: Color.FromRgb(9, 9, 9))) { }
            },
            new Size(100, 50),
            scale: 2
        );

        var map = LayoutDump.Of(driver.View).ConsumeMap;
        var column = string.Concat(map.Select(static row => row[0]));

        await Assert.That(map.Count).IsEqualTo(32);
        await Assert.That(map[0]).IsEqualTo(new string('1', 16) + new string('0', 16));
        await Assert.That(column).IsEqualTo(new string('1', 16) + new string('0', 16));
    }
}

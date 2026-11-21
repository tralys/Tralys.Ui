using System.Globalization;
using Tralys.Ui.Testing;

namespace Tralys.Ui.DevTools.Tests;

public sealed class InspectorPanelTests
{
    [Test]
    public async Task A_component_shows_as_itself_a_fragment_gives_no_row_and_a_layer_sits_under_its_owner_with_a_badge()
    {
        var gauge = new Gauge();
        using var app = UiDriver.Render(ui =>
        {
            using (ui.Box(id: "owner"))
            {
                using (ui.Fragment())
                    ui.Dial(gauge: gauge);
                using (ui.Layer(tier: LayerTier.Tooltip, id: "tip")) { }
            }
        });
        using var h = PanelHarness.Over(app);

        h.Model.Select(app.Get("tip"));
        h.Sync();
        var rows = h.Model.Tree.Rows;
        int DepthOf(Node node)
        {
            foreach (var row in rows)
            {
                if (row.Peek == node)
                    return row.Depth;
            }
            return -1;
        }

        await Assert.That(DepthOf(app.Get<Dial>())).IsEqualTo(DepthOf(app.Get("owner")) + 1);
        await Assert.That(DepthOf(app.Get("tip"))).IsEqualTo(DepthOf(app.Get("owner")) + 1);
        await Assert.That(h.Panel.GetText("Dial")).IsNotNull();
        await Assert.That(h.Panel.GetText("layer · tooltip")).IsNotNull();
    }

    [Test]
    public async Task A_row_click_selects_its_node_and_selecting_a_deep_node_opens_every_row_above_it()
    {
        var gauge = new Gauge();
        using var app = UiDriver.Render(ui =>
        {
            using (ui.Box(id: "outer"))
                ui.Dial(gauge: gauge);
        });
        using var h = PanelHarness.Over(app);

        h.Model.Select(app.GetText("tick 0"));
        h.Sync();
        var revealed = h.Model.TreeValue;
        h.Panel.Click(h.Panel.GetText("Meter"));
        h.Model.Refresh();

        var row = await Assert.That(h.Model.Tree.RowOf(app.GetText("tick 0"))).IsNotNull();
        await Assert.That(revealed).IsEqualTo(row.Key);
        await Assert.That(h.Model.Selected).IsSameReferenceAs(app.Get<Meter>());
    }

    [Test]
    public async Task Selecting_a_node_far_down_the_tree_scrolls_its_row_into_view()
    {
        using var app = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column))
            {
                for (var i = 0; i < 80; i++)
                    using (ui.Box(id: Ids[i], width: 10, height: 2, key: i)) { }
            }
        });
        using var h = PanelHarness.Over(app);

        h.Model.Select(app.Get(Ids[79]));
        h.Sync();

        await Assert.That(Find.Text(h.Panel.Root, "#" + Ids[79])).IsNotNull();
    }

    static readonly string[] Ids = [.. Enumerable.Range(0, 80).Select(i => $"b{i}")];

    [Test]
    public async Task Hovering_a_row_puts_the_hover_box_over_its_node_until_the_pointer_leaves()
    {
        var gauge = new Gauge();
        using var app = UiDriver.Render(ui => ui.Dial(gauge: gauge));
        using var h = PanelHarness.Over(app);
        h.Model.Select(app.Get("meter"));
        h.Sync();

        h.Panel.Hover(h.Panel.GetText("Dial"));
        var hovered = h.Model.HoverBox;
        h.Panel.Unhover();

        var dial = await Assert.That(InspectorModel.Boxed(app.Get<Dial>())).IsNotNull();
        await Assert.That(hovered).IsEqualTo(dial.ViewBounds);
        await Assert.That(h.Model.HoverBox).IsNull();
    }

    [Test]
    public async Task Rows_follow_the_target_as_nodes_mount_unmount_and_change_their_text_and_a_lost_selection_clears()
    {
        var gauge = new Gauge();
        using var app = UiDriver.Render(ui => ui.Dial(gauge: gauge));
        using var h = PanelHarness.Over(app);
        h.Model.Select(app.GetText("tick 0"));
        h.Model.Select(app.Get<Dial>());
        h.Sync();

        gauge.Extra = true;
        gauge.Tick = 7;
        h.Sync();
        var extraRow = h.Model.Tree.Rows.IndexOf(h.Model.Tree.RowOf(app.Get("extra")));
        var text = h.Panel.GetText("\"tick 7\"");
        h.Model.Select(app.Get("extra"));
        gauge.Extra = false;
        h.Sync();

        await Assert.That(extraRow).IsGreaterThanOrEqualTo(0);
        await Assert.That(text).IsNotNull();
        await Assert.That(h.Model.Selected).IsNull();
        await Assert.That(h.Model.SelectedBox).IsNull();
        await Assert.That(h.Panel.GetText("Nothing selected")).IsNotNull();
    }

    [Test]
    public async Task Props_show_through_the_describer_the_set_ones_unless_defaults_are_asked_for_and_follow_a_change()
    {
        var gauge = new Gauge { Level = 3f };
        using var app = UiDriver.Render(ui => ui.Dial(gauge: gauge));
        using var h = PanelHarness.Over(app);
        h.Model.Select(app.Get<Meter>());
        h.Sync();

        var level = h.Panel.GetText("3");
        var stepsHidden = Find.Text(h.Panel.Root, "Steps") is null;
        gauge.Level = 4.5f;
        h.Sync();
        var changed = h.Panel.GetText("4.5");
        h.Panel.Click(h.Panel.GetText("Defaults"));

        await Assert.That(level).IsNotNull();
        await Assert.That(stepsHidden).IsTrue();
        await Assert.That(changed).IsNotNull();
        await Assert.That(h.Panel.GetText("Steps")).IsNotNull();
    }

    [Test]
    public async Task Values_read_short_and_culture_free()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            using var app = UiDriver.Render(ui =>
                ui.Swatch(
                    name: "Save",
                    inset: new Thickness(1, 0, 0, 0),
                    gap: new Thickness(8),
                    tint: Color.FromArgb(0x80, 0xFF, 0, 0),
                    ratio: 0.5f,
                    tag: Tags.One
                )
            );
            using var h = PanelHarness.Over(app);
            h.Model.Select(app.Get<Swatch>());
            h.Sync();

            await Assert.That(h.Panel.GetText("\"Save\"")).IsNotNull();
            await Assert.That(h.Panel.GetText("1,0,0,0")).IsNotNull();
            await Assert.That(h.Panel.GetText("8")).IsNotNull();
            await Assert.That(h.Panel.GetText("#80FF0000")).IsNotNull();
            await Assert.That(h.Panel.GetText("0.5")).IsNotNull();
            await Assert.That(h.Panel.GetText("Tags")).IsNotNull();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Test]
    public async Task A_watched_prop_counts_each_change_until_another_is_chosen()
    {
        var gauge = new Gauge { Level = 1f };
        using var app = UiDriver.Render(ui => ui.Dial(gauge: gauge));
        using var h = PanelHarness.Over(app);
        h.Model.Select(app.Get<Meter>());
        h.Sync();

        h.Panel.Click(h.Panel.GetText("Level"));
        h.Panel.Click(h.Panel.GetText("Break on change"));
        gauge.Level = 2f;
        h.Sync();
        gauge.Tick = 1;
        h.Sync();
        gauge.Level = 3f;
        h.Sync();
        var counted = h.Panel.GetText("Break on change · 2");
        h.Panel.Click(h.Panel.GetText("Caption"));

        await Assert.That(counted).IsNotNull();
        await Assert.That(h.Panel.GetText("Break on change")).IsNotNull();
    }

    [Test]
    public async Task Open_hands_a_vscode_link_to_the_file_and_line_the_node_was_written_at()
    {
        using var app = UiDriver.Render(ui =>
        {
            using (
                ui.Box(id: "written", width: 20, height: 20, file: "/work/c#/My View.cs", line: 3)
            ) { }
        });
        using var h = PanelHarness.Over(app);
        string? opened = null;
        h.Model.OpenSource = link => opened = link;
        h.Model.Select(app.Get("written"));
        h.Sync();

        h.Panel.Click(h.Panel.GetText("Open"));

        await Assert.That(opened).IsEqualTo("vscode://file/work/c%23/My%20View.cs:3");
    }
}

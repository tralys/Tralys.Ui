using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Controls;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class TreeTests
{
    [Test]
    public async Task A_click_reports_a_row_that_shows_selected_only_once_value_names_it_and_a_focused_unselected_row_rings_itself()
    {
        var grove = Grove.Staff();
        grove.Follows = false;
        using var driver = grove.Render();
        var designers = driver.Get("Designers");

        driver.Click(designers);
        var refused = (Trees.Selected(driver, "Designers"), Ring(designers));
        grove.Value = grove.KeyOf("Designers");
        driver.Settle();
        var shown = (Trees.Selected(driver, "Designers"), Ring(designers));
        grove.Value = grove.KeyOf("Engineers");
        driver.Settle();
        var away = (
            Trees.Selected(driver, "Designers"),
            Ring(designers),
            driver.Focused == designers
        );
        grove.Follows = true;
        driver.Click(designers);
        var bounds = designers.ViewBounds;

        await Assert.That(refused).IsEqualTo((false, true));
        await Assert.That(shown).IsEqualTo((true, false));
        await Assert.That(away).IsEqualTo((false, true, true));
        await Assert.That(grove.Picks).IsEquivalentTo(["Designers", "Designers"]);
        await Assert
            .That((Trees.Selected(driver, "Designers"), Ring(designers)))
            .IsEqualTo((true, false));
        await Assert.That(designers.Visual.Background).IsEqualTo(Selected);
        await Assert.That(bounds.X).IsEqualTo(driver.Get("Staff").ViewBounds.X);
    }

    static readonly Paint Selected = Themes.Dark.Color(ComponentColor.RowBgSelected);

    static bool Ring(Node row)
    {
        foreach (var child in row.Children)
        {
            if (child.Place.Position == Position.Absolute)
                return true;
        }
        return false;
    }

    [Test]
    public async Task With_selection_off_or_on_a_disabled_row_a_click_selects_nothing()
    {
        var grove = Grove.Staff();
        var locked = new Grove(new Leaf(1, "Open", new Leaf(2, "Child") { Locked = true }).Open());
        using var off = grove.Render(selectionMode: SelectionMode.None);
        using var disabled = locked.Render(item: Leaf.Locking);

        off.Click(off.Get("Designers"));
        disabled.Click(disabled.Get("Child"));

        await Assert.That(grove.Picks).IsEmpty();
        await Assert.That(Trees.Selected(off, "Designers")).IsFalse();
        await Assert.That(locked.Picks).IsEmpty();
    }

    [Test]
    public async Task Tab_lands_on_the_first_row_and_selects_it_and_the_arrows_walk_the_shown_rows_without_wrapping()
    {
        var grove = Grove.Staff();
        grove.Find("Designers").IsExpanded = true;
        grove.Find("Designers").IsExpanded = false;
        using var driver = UiDriver
            .Render(ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start))
                {
                    ui.Button(text: "Before", id: "before");
                    ui.Tree(
                        items: grove.Rows,
                        itemKey: Leaf.Key,
                        itemText: Leaf.Named,
                        item: Named,
                        value: grove.Value,
                        onValueChange: grove.OnValueChange,
                        width: 240,
                        height: 300
                    );
                }
            })
            .WithIcons();
        driver.Focus(driver.Get("before"));

        driver.Tab();
        var tabbed = (Trees.Focused(driver), grove.Picks.ToArray());
        var walked = new List<string>();
        foreach (var key in Walk)
        {
            driver.Press(key);
            walked.Add(Trees.Focused(driver) ?? "");
        }
        driver.Press(Key.Home);
        driver.Press(Key.Up);
        var top = Trees.Focused(driver);
        driver.Press(Key.End);
        driver.Press(Key.Down);

        await Assert.That(tabbed.Item1).IsEqualTo("Staff");
        await Assert.That(tabbed.Item2).IsEquivalentTo(["Staff"]);
        await Assert
            .That(walked)
            .IsEquivalentTo([
                "Engineers",
                "Alice",
                "Dave",
                "Designers",
                "Contractors",
                "Designers",
                "Dave",
                "Contractors",
                "Staff",
            ]);
        await Assert.That(top).IsEqualTo("Staff");
        await Assert.That(Trees.Focused(driver)).IsEqualTo("Contractors");
        await Assert.That(grove.NameOf(grove.Value)).IsEqualTo("Contractors");
        await Assert.That(grove.Picks.Skip(1).Take(9)).IsEquivalentTo(walked);
    }

    static readonly Key[] Walk =
    [
        Key.Down,
        Key.Down,
        Key.Down,
        Key.Down,
        Key.Down,
        Key.Up,
        Key.Up,
        Key.End,
        Key.Home,
    ];

    static readonly Slot<Leaf> Named = Slot<Leaf>.Of(
        static (ui, leaf, _) => ui.TreeItem(text: leaf.Name, id: leaf.Name)
    );

    [Test]
    public async Task Right_and_left_open_enter_shut_and_leave_rows_plus_and_minus_open_and_shut_and_star_opens_all_below()
    {
        var grove = Grove.Staff();
        using var driver = grove.Render();
        driver.Focus(driver.Get("Engineers"));

        driver.Press(Key.Right);
        var into = (Trees.Focused(driver), grove.Find("Engineers").IsExpanded);
        driver.Press(Key.Left);
        var up = (Trees.Focused(driver), grove.Find("Engineers").IsExpanded);
        driver.Press(Key.Left);
        var shut = (Trees.Focused(driver), grove.Find("Engineers").IsExpanded);
        driver.Press(Key.Left);
        var parent = Trees.Focused(driver);
        driver.Focus(driver.Get("Contractors"));
        driver.Press(Key.Add);
        var added = grove.Find("Contractors").IsExpanded;
        driver.Press(Key.Subtract);
        var subtracted = grove.Find("Contractors").IsExpanded;
        driver.Press(Key.Right);
        grove.Find("Contractors").IsExpanded = false;
        driver.Settle();
        var followed = Trees.Shown(grove);
        driver.Focus(driver.Get("Staff"));
        driver.Press(Key.Multiply);

        await Assert.That(into).IsEqualTo(("Alice", true));
        await Assert.That(up).IsEqualTo(("Engineers", true));
        await Assert.That(shut).IsEqualTo(("Engineers", false));
        await Assert.That(parent).IsEqualTo("Staff");
        await Assert.That((added, subtracted)).IsEqualTo((true, false));
        await Assert.That(followed).IsEqualTo("Staff Engineers Designers Contractors");
        await Assert
            .That(Trees.Shown(grove))
            .IsEqualTo("Staff Engineers Alice Dave Designers Bob Contractors");
        await Assert
            .That(grove.Expansions.Lines)
            .IsEquivalentTo([
                "-Engineers",
                "+Contractors",
                "-Contractors",
                "+Contractors",
                "+Engineers",
                "+Designers",
            ]);
    }

    [Test]
    public async Task The_arrows_pass_over_a_disabled_row()
    {
        var grove = new Grove(
            new Leaf(1, "A"),
            new Leaf(2, "B") { Locked = true },
            new Leaf(3, "C")
        );
        using var driver = grove.Render(item: Leaf.Locking);
        driver.Click(driver.Get("A"));

        driver.Press(Key.Down);

        await Assert.That(Trees.Focused(driver)).IsEqualTo("C");
        await Assert.That(grove.Picks).IsEquivalentTo(["A", "C"]);
    }

    [Test]
    public async Task Removing_or_replacing_the_selected_rows_item_reports_nothing_and_its_new_row_shows_unselected_and_shut()
    {
        var grove = Grove.Staff();
        using var driver = grove.Render();
        driver.Click(driver.Get("Alice"));

        var staff = grove.Roots[0];
        grove.Roots.RemoveAt(0);
        grove.Rows.Refresh();
        driver.Settle();
        var removed = (grove.Picks.Count, driver.All<Tree.Item>().Count);
        driver.Click(driver.Get("Contractors"));
        driver.Press(Key.Right);
        grove.Roots[0] = new Leaf(10, "Agency", new Leaf(11, "Gail"));
        grove.Rows.Refresh();
        driver.Settle();
        var replaced = (
            Trees.Selected(driver, "Agency"),
            grove.Find("Agency").IsExpanded,
            Trees.Shown(grove)
        );
        grove.Roots.Add(new Leaf(12, "Freelance"));
        grove.Roots.RemoveAt(0);
        grove.Rows.Refresh();
        driver.Settle();
        var arrowHidden = Arrow(driver.Get("Freelance")).Placement.Hidden;

        await Assert.That(staff.Name).IsEqualTo("Staff");
        await Assert.That(removed).IsEqualTo((1, 1));
        await Assert.That(replaced).IsEqualTo((false, false, "Agency"));
        await Assert.That(grove.Picks).IsEquivalentTo(["Alice", "Contractors"]);
        await Assert.That(Trees.Shown(grove)).IsEqualTo("Freelance");
        await Assert.That(arrowHidden).IsTrue();
    }

    [Test]
    public async Task A_tree_narrower_or_shorter_than_its_rows_scrolls_both_ways()
    {
        var grove = new Grove(
            new Leaf(
                1,
                "A folder whose name runs much wider than the tree",
                new Leaf(2, "Child")
            ).Open(),
            new Leaf(3, "B"),
            new Leaf(4, "C"),
            new Leaf(5, "D")
        );
        using var driver = grove.Render(width: 120, height: 64);
        var tree = driver.Get("tree");

        var sideways = driver.Wheel(tree, new Vector2(1f, 0f));
        var down = driver.Wheel(tree, new Vector2(0f, -1f));

        await Assert.That((sideways, down)).IsEqualTo((true, true));
    }

    [Test]
    public async Task A_row_lights_under_a_hand_its_arrow_lights_when_hovered_and_greys_when_disabled()
    {
        var grove = new Grove(
            new Leaf(1, "Staff", new Leaf(2, "Alice")),
            new Leaf(3, "Locked", new Leaf(4, "Hidden")) { Locked = true }
        );
        using var driver = grove.Render(item: Leaf.Locking);
        var staff = driver.Get("Staff");
        var t = Themes.Dark;

        driver.Hover(staff);
        var lit = (staff.Visual.Background, staff.Interaction!.Props.Cursor);
        driver.Hover(Arrow(staff));
        var arrow = Glyph(staff);
        driver.Unhover();

        await Assert.That(lit).IsEqualTo(((Paint)t.Color(ColorToken.StateHover), Cursors.Hand));
        await Assert.That(arrow).IsEqualTo((Paint)t.Color(ColorToken.IconHover));
        await Assert.That(staff.Visual.Background).IsEqualTo(default(Paint));
        await Assert
            .That(Glyph(driver.Get("Locked")))
            .IsEqualTo((Paint)t.Color(ColorToken.IconDisabled));
    }

    static Node Arrow(Node row) => Menus.RootOf(Find<TreeDisclosure>(row));

    static Paint Glyph(Node row) => Menus.RootOf(Nodes.Glyph(Arrow(row))).Inherit.Color;

    static Node Find<T>(Node within)
    {
        foreach (var child in within.Children)
        {
            if (child.Component == typeof(T))
                return child;
        }
        throw new InvalidOperationException($"No {typeof(T).Name}.");
    }

    [Test]
    public async Task A_double_click_toggles_a_row_with_children_and_its_arrow_opens_it_without_selecting_leaving_the_keys_on_it()
    {
        var grove = Grove.Staff();
        using var driver = grove.Render();

        driver.DoubleClick(driver.Get("Contractors"));
        var doubled = grove.Find("Contractors").IsExpanded;
        driver.DoubleClick(driver.Get("Contractors"));
        var again = grove.Find("Contractors").IsExpanded;
        driver.DoubleClick(driver.Get("Dave"));
        var leaf = (grove.Find("Dave").IsExpanded, grove.Picks.Last());
        var picks = grove.Picks.Count;
        driver.Click(Arrow(driver.Get("Designers")));
        var arrowed = (
            grove.Find("Designers").IsExpanded,
            grove.Picks.Count,
            Trees.Focused(driver)
        );
        driver.Press(Key.Left);

        await Assert.That((doubled, again)).IsEqualTo((true, false));
        await Assert.That(leaf).IsEqualTo((false, "Dave"));
        await Assert.That(arrowed).IsEqualTo((true, picks, "Designers"));
        await Assert.That(grove.Find("Designers").IsExpanded).IsFalse();
    }

    [Test]
    public async Task Each_level_indents_one_step_a_leaf_keeps_the_arrows_room_and_a_flat_lists_row_indents_as_deep()
    {
        var grove = new Grove(
            new Leaf(
                1,
                "First",
                new Leaf(2, "Second", new Leaf(3, "Third")).Open(),
                new Leaf(4, "Leaf")
            ).Open()
        );
        var flat = new Grove(
            new Leaf(1, "Top", new Leaf(2, "Deeper", new Leaf(3, "Deepest")).Open()).Open()
        );
        var row = Slot<Leaf>.Of(
            static (ui, leaf, _) =>
                ui.TreeItem(
                    text: leaf.Name,
                    depth: leaf.Depth,
                    hasChildren: leaf.HasChildren,
                    expanded: leaf.IsExpanded,
                    id: leaf.Name
                )
        );
        using var driver = grove.Render();
        using var list = UiDriver
            .Render(ui =>
                ui.VirtualList(
                    items: flat.Rows,
                    itemKey: Leaf.Key,
                    row: row,
                    width: 240,
                    height: 200
                )
            )
            .WithIcons();
        var step = 4f * Themes.Dark.Dimensions(Density.Pointer).Spacing;
        float ArrowX(UiDriver d, string name) => Arrow(d.Get(name)).ViewBounds.X;
        float LabelX(UiDriver d, string name) => d.GetText(name).ViewBounds.X;

        await Assert.That(ArrowX(driver, "Second") - ArrowX(driver, "First")).IsEqualTo(step);
        await Assert.That(ArrowX(driver, "Third") - ArrowX(driver, "First")).IsEqualTo(2 * step);
        await Assert.That(LabelX(driver, "Leaf")).IsEqualTo(LabelX(driver, "Second"));
        await Assert
            .That(LabelX(list, "Deepest") - list.Get("Deepest").ViewBounds.X)
            .IsEqualTo(LabelX(driver, "Third") - driver.Get("Third").ViewBounds.X);
    }

    [Test]
    public async Task A_filtered_tree_shows_each_match_beneath_every_row_above_it()
    {
        var grove = new Grove(
            new Leaf(
                1,
                "Fleet",
                new Leaf(2, "Wing", new Leaf(3, "Ace"), new Leaf(4, "Rookie")).Open(),
                new Leaf(5, "Tanker")
            ).Open(),
            new Leaf(6, "Depot", new Leaf(7, "Crate")).Open()
        );
        grove.Rows.Filter = new RowFilter<Leaf>(static leaf => leaf.Name == "Ace");
        using var driver = grove.Render();

        await Assert.That(driver.All<Tree.Item>().Count).IsEqualTo(3);
        await Assert
            .That(Arrow(driver.Get("Fleet")).ViewBounds.X)
            .IsLessThan(Arrow(driver.Get("Ace")).ViewBounds.X);
    }
}

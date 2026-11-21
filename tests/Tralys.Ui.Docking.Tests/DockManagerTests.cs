namespace Tralys.Ui.Docking.Tests;

public sealed class DockManagerTests
{
    private static DockElement Element(string id) => new(id, id);

    private static (DockManager m, DockElement e) DockedManager(
        string id,
        DockPosition pos = DockPosition.Left
    )
    {
        var m = DockView.NewManager(id);
        var e = m.Element(id);
        m.Dock(e, pos);
        return (m, e);
    }

    [Test]
    public async Task NewManager_HasFiveEmptyRootRegions()
    {
        var m = new DockManager();

        await Assert.That(m.LeftGroup.Elements.Count).IsEqualTo(0);
        await Assert.That(m.CenterGroup.Elements.Count).IsEqualTo(0);
        await Assert.That(m.Elements.Count).IsEqualTo(0);
        await Assert.That(m.FloatingWindows.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Dock_PlacesElement_SetsStatePositionParent()
    {
        var (m, e) = DockedManager("a", DockPosition.Left);

        await Assert.That(e.State).IsEqualTo(DockState.Docked);
        await Assert.That(e.Position).IsEqualTo(DockPosition.Left);
        await Assert.That(ReferenceEquals(e.Parent, m.LeftGroup)).IsTrue();
        await Assert.That(m.LeftGroup.Elements.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Register_DuplicateId_Throws()
    {
        var m = new DockManager();
        m.Register(Element("dup"));

        await Assert.That(() => m.Register(Element("dup"))).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ReorderingATab_RaisesLayoutChanged()
    {
        var m = DockView.Docked(("a", DockPosition.Left), ("b", DockPosition.Left));
        var changes = 0;
        m.LayoutChanged += (_, _) => changes++;

        m.LeftGroup.MoveTo(m.Element("b"), 0);

        await Assert.That(changes).IsEqualTo(1);
    }

    [Test]
    public async Task Unregister_RemovesFromGroupAndRegistry()
    {
        var (m, e) = DockedManager("a", DockPosition.Right);

        var removed = m.Unregister(e);

        await Assert.That(removed).IsTrue();
        await Assert.That(m.Elements.Count).IsEqualTo(0);
        await Assert.That(m.RightGroup.Elements.Count).IsEqualTo(0);
        await Assert.That(e.Parent).IsNull();
    }

    [Test]
    public async Task DockRelativeTo_Center_TabsIntoSameGroup()
    {
        var (m, a) = DockedManager("a", DockPosition.Left);
        var b = Element("b");
        m.Register(b);

        m.DockRelativeTo(b, a, DockDirection.Center);

        await Assert.That(ReferenceEquals(a.Parent, b.Parent)).IsTrue();
        var group = await Assert.That(a.Parent).IsNotNull();
        await Assert.That(group.Elements.Count).IsEqualTo(2);
        await Assert.That(group.IsTabbedGroup).IsTrue();
    }

    [Test]
    public async Task DockRelativeTo_Right_CreatesHorizontalSplit_ExistingFirst_NewSecond()
    {
        var (m, a) = DockedManager("a", DockPosition.Left);
        var b = Element("b");
        m.Register(b);

        m.DockRelativeTo(b, a, DockDirection.Right);

        var root = m.LeftGroup;
        await Assert.That(root.IsSplitGroup).IsTrue();
        await Assert.That(root.Children.Count).IsEqualTo(2);
        await Assert.That(root.Orientation).IsEqualTo(SplitOrientation.Horizontal);
        await Assert.That(ReferenceEquals(root.Children[0].Elements[0], a)).IsTrue();
        await Assert.That(ReferenceEquals(root.Children[1].Elements[0], b)).IsTrue();
    }

    [Test]
    public async Task DockRelativeTo_Bottom_CreatesVerticalSplit()
    {
        var (m, a) = DockedManager("a", DockPosition.Center);
        var b = Element("b");
        m.Register(b);

        m.DockRelativeTo(b, a, DockDirection.Bottom);

        var root = m.CenterGroup;
        await Assert.That(root.IsSplitGroup).IsTrue();
        await Assert.That(root.Orientation).IsEqualTo(SplitOrientation.Vertical);
        await Assert.That(ReferenceEquals(root.Children[0].Elements[0], a)).IsTrue();
        await Assert.That(ReferenceEquals(root.Children[1].Elements[0], b)).IsTrue();
    }

    [Test]
    public async Task Float_MovesElementIntoFloatingWindow()
    {
        var (m, a) = DockedManager("a", DockPosition.Left);

        var fw = m.Float(a, 10, 20, 300, 200);

        await Assert.That(m.FloatingWindows.Count).IsEqualTo(1);
        await Assert.That(a.State).IsEqualTo(DockState.Floating);
        await Assert.That(a.Position).IsEqualTo(DockPosition.Floating);
        await Assert.That(ReferenceEquals(fw.Group, a.Parent)).IsTrue();
        await Assert.That(fw.Group.Position).IsEqualTo(DockPosition.Floating);
        await Assert.That(m.LeftGroup.Elements.Count).IsEqualTo(0);
    }

    [Test]
    public async Task FloatingWindow_BoundsChanged_FiresOnlyOnRealChanges()
    {
        var group = new DockGroup(DockPosition.Floating);
        var window = new FloatingWindow(group, x: 10, y: 20, width: 300, height: 200);
        var changes = 0;
        window.BoundsChanged += (_, _) => changes++;

        window.X = 10;
        window.Y = 20;
        window.Width = 300;
        window.Height = 200;

        await Assert.That(changes).IsEqualTo(0);

        window.X = 11;
        window.Y = 21;
        window.Width = 301;
        window.Height = 201;

        await Assert.That(changes).IsEqualTo(4);
    }

    [Test]
    public async Task ClosingOneSideOfSplit_CollapsesSynchronously()
    {
        var (m, a) = DockedManager("a", DockPosition.Left);
        var b = Element("b");
        m.Register(b);
        m.DockRelativeTo(b, a, DockDirection.Right); // LeftGroup → [a | b]

        b.Hide(); // empties b's side → model must collapse the wrapper immediately

        await Assert.That(m.LeftGroup.IsSplitGroup).IsFalse();
        await Assert.That(m.LeftGroup.Elements.Count).IsEqualTo(1);
        await Assert.That(ReferenceEquals(m.LeftGroup.Elements[0], a)).IsTrue();
        await Assert.That(ReferenceEquals(a.Parent, m.LeftGroup)).IsTrue();
    }

    [Test]
    public async Task ClosingDeepestPaneInNestedSplit_CollapsesOnlyThatLevel()
    {
        var (m, a) = DockedManager("a", DockPosition.Center);
        var b = Element("b");
        var c = Element("c");
        m.Register(b);
        m.Register(c);
        m.DockRelativeTo(b, a, DockDirection.Right); // [a | b]
        m.DockRelativeTo(c, b, DockDirection.Bottom); // [a | (b / c)]

        c.Close();

        var root = m.CenterGroup;
        await Assert.That(root.IsSplitGroup).IsTrue();
        await Assert.That(root.Children.Count).IsEqualTo(2);
        await Assert.That(root.Children[0].Elements[0].Id).IsEqualTo("a");
        await Assert.That(root.Children[1].IsSplitGroup).IsFalse();
        await Assert.That(root.Children[1].Elements[0].Id).IsEqualTo("b");
    }

    [Test]
    public async Task ClosingLeafInThreeLevelNestedSplit_CollapsesAllRedundantWrappers()
    {
        var (m, a) = DockedManager("a", DockPosition.Center);
        var b = Element("b");
        var c = Element("c");
        var d = Element("d");
        m.Register(b);
        m.Register(c);
        m.Register(d);
        m.DockRelativeTo(b, a, DockDirection.Right); // [a | b]
        m.DockRelativeTo(c, b, DockDirection.Bottom); // [a | (b / c)]
        m.DockRelativeTo(d, c, DockDirection.Right); // [a | (b / (c | d))]

        d.Close(); // chain: (c|d) → c; then (b/c) stays a split; check intermediate shape.

        var root = m.CenterGroup;
        await Assert.That(root.IsSplitGroup).IsTrue();
        await Assert.That(root.Children.Count).IsEqualTo(2);
        await Assert.That(root.Children[0].Elements[0].Id).IsEqualTo("a");
        await Assert.That(root.Children[1].IsSplitGroup).IsTrue();
        await Assert.That(root.Children[1].Children[0].IsSplitGroup).IsFalse();
        await Assert.That(root.Children[1].Children[0].Elements[0].Id).IsEqualTo("b");
        await Assert.That(root.Children[1].Children[1].IsSplitGroup).IsFalse();
        await Assert.That(root.Children[1].Children[1].Elements[0].Id).IsEqualTo("c");

        c.Close();

        await Assert.That(root.IsSplitGroup).IsTrue();
        await Assert.That(root.Children.Count).IsEqualTo(2);
        await Assert
            .That(root.Children[1].IsSplitGroup)
            .IsFalse()
            .Because(
                "Wrapper around the surviving (b) leaf must collapse — no single-child split."
            );
        await Assert.That(root.Children[1].Elements[0].Id).IsEqualTo("b");
    }

    [Test]
    public async Task ElementsChanged_FiresAddThenRemove()
    {
        var m = new DockManager();
        var a = Element("a");
        m.Register(a);

        int adds = 0,
            removes = 0;
        int? lastAddIndex = null;
        m.LeftGroup.ElementsChanged += (_, e) =>
        {
            if (e.Change == DockElementsChange.Added)
            {
                adds++;
                lastAddIndex = e.Index;
            }
            else if (e.Change == DockElementsChange.Removed)
            {
                removes++;
            }
        };

        m.Dock(a, DockPosition.Left);
        await Assert.That(adds).IsEqualTo(1);
        await Assert.That(lastAddIndex).IsEqualTo(0);

        a.Hide();
        await Assert.That(removes).IsEqualTo(1);
    }

    [Test]
    public async Task ElementsChanged_FiresMoveOnReorder()
    {
        var (m, a) = DockedManager("a", DockPosition.Center);
        var b = Element("b");
        m.Register(b);
        m.DockToGroup(b, m.CenterGroup); // [a, b]

        int moves = 0;
        m.CenterGroup.ElementsChanged += (_, e) =>
        {
            if (e.Change == DockElementsChange.Moved)
                moves++;
        };

        m.CenterGroup.MoveTo(b, 0);
        await Assert.That(moves).IsEqualTo(1);
        await Assert.That(m.CenterGroup.Elements[0].Id).IsEqualTo("b");
    }

    [Test]
    public async Task ResetLayout_EmptiesRegions_KeepsRootInstances()
    {
        var (m, a) = DockedManager("a", DockPosition.Left);
        var leftBefore = m.LeftGroup;

        m.ResetLayout();

        await Assert.That(ReferenceEquals(m.LeftGroup, leftBefore)).IsTrue();
        await Assert.That(m.LeftGroup.Elements.Count).IsEqualTo(0);
        await Assert.That(a.State).IsEqualTo(DockState.Hidden);
        await Assert.That(m.Elements.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ResetLayout_DocksTheOpenDocumentsBackInTheCentre_StillActive_LeavingAClosedOneClosed()
    {
        var m = new DockManager();
        var docked = new DockElement("docked", "docked", DockType.Document);
        var floating = new DockElement("floating", "floating", DockType.Document);
        var closed = new DockElement("closed", "closed", DockType.Document);
        m.Register(docked);
        m.Register(floating);
        m.Register(closed);
        m.Dock(docked, DockPosition.Right);
        m.Float(floating);
        m.Dock(closed, DockPosition.Center);
        closed.Close();
        docked.Activate();

        m.ResetLayout();

        await Assert.That(docked.Parent).IsSameReferenceAs(m.CenterGroup);
        await Assert.That(floating.Parent).IsSameReferenceAs(m.CenterGroup);
        await Assert.That(m.ActiveElement).IsSameReferenceAs(docked);
        await Assert.That(closed.State).IsEqualTo(DockState.Hidden);
    }

    [Test]
    public async Task RegisteringAndUnregistering_ReportEachChangeOnElements()
    {
        var m = new DockManager();
        var a = Element("a");
        var changes = new List<string>();
        using var subscription = m.Elements.Subscribe(
            (_, in e) =>
                changes.Add($"{e.Kind} {(e.NewItems.IsEmpty ? e.OldItems : e.NewItems)[0].Id}")
        );

        m.Register(a);
        m.Unregister(a);

        await Assert.That(changes).IsEquivalentTo(["Add a", "Remove a"]);
    }

    [Test]
    public async Task Dock_RootIsSplit_RoutesToActiveLeaf()
    {
        var m = new DockManager();
        var a = Element("a");
        var b = Element("b");
        var c = Element("c");
        m.Register(a);
        m.Register(b);
        m.Register(c);

        m.Dock(a, DockPosition.Center);
        m.DockRelativeTo(b, a, DockDirection.Right); // splits Center: [a | b]

        b.Activate(); // user clicked b → manager-level active = b
        var bLeaf = await Assert.That(b.Parent).IsNotNull();

        m.Dock(c, DockPosition.Center);

        await Assert.That(m.CenterGroup.Elements.Count).IsEqualTo(0); // no orphan on root
        await Assert.That(ReferenceEquals(c.Parent, bLeaf)).IsTrue(); // shared leaf with b
        await Assert.That(bLeaf.Elements.Contains(c)).IsTrue();
    }

    [Test]
    public async Task Dock_RootIsSplit_NoActiveElement_RoutesToFirstLeaf()
    {
        var m = new DockManager();
        var a = Element("a");
        var b = Element("b");
        var c = Element("c");
        m.Register(a);
        m.Register(b);
        m.Register(c);

        m.Dock(a, DockPosition.Center);
        m.DockRelativeTo(b, a, DockDirection.Right); // [a | b]

        m.SetActiveElement(null);

        m.Dock(c, DockPosition.Center);

        await Assert.That(m.CenterGroup.Elements.Count).IsEqualTo(0);
        await Assert.That(ReferenceEquals(c.Parent, m.CenterGroup.Children[0])).IsTrue();
    }

    [Test]
    public async Task DockingIntoASplitRoot_LandsInALeaf()
    {
        var (m, a) = DockedManager("a", DockPosition.Center);
        var b = Element("b");
        var c = Element("c");
        var d = Element("d");
        m.Register(b);
        m.Register(c);
        m.Register(d);
        m.DockRelativeTo(b, a, DockDirection.Right);

        m.DockToGroup(c, m.CenterGroup);
        m.DockBySplit(d, m.CenterGroup, DockDirection.Bottom);

        await Assert.That(m.CenterGroup.Elements.Count).IsEqualTo(0);
        await Assert.That(m.CenterGroup.Children.Count).IsEqualTo(2);
        await Assert.That(c.Parent?.IsTabbedGroup).IsTrue();
        await Assert.That(d.Parent?.IsTabbedGroup).IsTrue();
    }

    [Test]
    public async Task SplittingATabOutOfItsGroup_LeavesTheTabThatTookItsPlaceShowing()
    {
        var m = DockView.Docked(
            ("a", DockPosition.Center),
            ("b", DockPosition.Center),
            ("c", DockPosition.Center),
            ("d", DockPosition.Center)
        );
        var b = m.Element("b");
        var c = m.Element("c");
        b.Activate();

        m.DockBySplit(b, m.CenterGroup, DockDirection.Right);

        await Assert.That(c.Parent?.ActiveElement).IsSameReferenceAs(c);
        await Assert.That(b.Parent?.ActiveElement).IsSameReferenceAs(b);
    }

    [Test]
    public async Task SplittingAnEmptyRegion_TabsTheElementIntoIt()
    {
        var m = DockView.NewManager("a");
        var a = m.Element("a");

        m.DockBySplit(a, m.CenterGroup, DockDirection.Right);

        await Assert.That(m.CenterGroup.IsSplitGroup).IsFalse();
        await Assert.That(a.Parent).IsSameReferenceAs(m.CenterGroup);
    }

    [Test]
    public async Task AWindowClosedAgain_LeavesATabDockedSinceWhereItIs()
    {
        var (m, a) = DockedManager("a");
        var window = m.Float(a);
        window.RequestClose();
        m.Dock(a, DockPosition.Right);

        window.RequestClose();

        await Assert.That(a.Parent).IsSameReferenceAs(m.RightGroup);
    }

    [Test]
    public async Task MovingAnElement_NeverReportsItHidden()
    {
        var (m, a) = DockedManager("a");
        var states = new List<DockState>();
        a.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DockElement.State))
                states.Add(a.State);
        };

        m.Dock(a, DockPosition.Right);
        m.Float(a);
        m.Dock(a, DockPosition.Left);

        await Assert.That(states).DoesNotContain(DockState.Hidden);
    }

    [Test]
    public async Task UnregisteringTheActiveElement_ActivatesItsNeighbour()
    {
        var m = DockView.Docked(("a", DockPosition.Center), ("b", DockPosition.Center));
        var a = m.Element("a");
        a.Activate();

        m.Unregister(a);

        await Assert.That(m.ActiveElement).IsSameReferenceAs(m.GetElement("b"));
    }

    [Test]
    public async Task AHostSplittingAGroupByItsChildren_KeepsItsTabs_AndCollapsesWhenOneGoes()
    {
        var m = DockView.Docked(("a", DockPosition.Left), ("b", DockPosition.Left));
        var x = new DockElement("x", "x");
        m.Register(x);
        var changes = 0;
        m.LayoutChanged += (_, _) => changes++;
        var extra = new DockGroup();
        extra.Add(x);

        m.LeftGroup.AddChild(extra);
        var tabsMoved =
            m.LeftGroup.Elements.Count == 0 && m.LeftGroup.Children[0].Elements.Count == 2;
        var position = x.Position;
        var changesAdding = changes;
        m.LeftGroup.RemoveChild(extra);

        await Assert.That(tabsMoved).IsTrue();
        await Assert.That(position).IsEqualTo(DockPosition.Left);
        await Assert.That(changesAdding).IsGreaterThan(0);
        await Assert.That(m.LeftGroup.IsTabbedGroup).IsTrue();
        await Assert.That(m.LeftGroup.Elements.Count).IsEqualTo(2);
        await Assert.That(x.Parent).IsNull();
        await Assert.That(changes).IsGreaterThan(changesAdding);
    }

    [Test]
    public async Task A_tab_docked_into_a_floating_window_reads_floating()
    {
        var (m, a) = DockedManager("a", DockPosition.Left);
        var b = Element("b");
        m.Register(b);
        var window = m.Float(a);
        var changes = 0;
        b.PropertyChanged += (_, e) =>
            changes += e.PropertyName == nameof(DockElement.State) ? 1 : 0;

        m.DockToGroup(b, window.Group);

        await Assert.That(b.State).IsEqualTo(DockState.Floating);
        await Assert.That(changes).IsEqualTo(1);
    }
}

namespace Tralys.Ui.Docking.Tests;

public sealed class LayoutSerializerTests
{
    [Test]
    public async Task RoundTrip_PreservesTabbedGroupAndActiveElement()
    {
        var m = DockView.NewManager("a", "b");
        var a = m.GetElement("a")!;
        var b = m.GetElement("b")!;
        m.Dock(a, DockPosition.Left);
        m.DockRelativeTo(b, a, DockDirection.Center); // tab b alongside a
        a.Activate();

        var json = m.SaveLayout();
        var m2 = DockView.NewManager("a", "b");
        m2.LoadLayout(json);

        await Assert.That(m2.LeftGroup!.Elements.Count).IsEqualTo(2);
        await Assert.That(m2.LeftGroup!.IsTabbedGroup).IsTrue();
        await Assert.That(m2.ActiveElement!.Id).IsEqualTo("a");
    }

    [Test]
    public async Task RestoreFloatingWindow_MissingFirstElement_KeepsSurvivingTab()
    {
        var m = DockView.NewManager("a", "b");
        var a = m.GetElement("a")!;
        var b = m.GetElement("b")!;
        m.Dock(a, DockPosition.Left);
        var fw = m.Float(a, 10, 10, 300, 200);
        m.DockToGroup(b, fw.Group);

        var json = m.SaveLayout();
        var m2 = DockView.NewManager("b"); // "a" removed since the layout was saved
        m2.LoadLayout(json);

        await Assert.That(m2.FloatingWindows.Count).IsEqualTo(1);
        await Assert.That(m2.FloatingWindows[0].Group.Elements.Count).IsEqualTo(1);
        await Assert.That(m2.GetElement("b")!.State).IsEqualTo(DockState.Floating);
    }

    [Test]
    public async Task Load_PrunesASplitSideWhoseIdsAreGone()
    {
        var m = DockView.NewManager("a", "b", "c");
        var a = m.GetElement("a")!;
        var b = m.GetElement("b")!;
        m.Dock(a, DockPosition.Center);
        m.DockRelativeTo(b, a, DockDirection.Right);
        m.DockRelativeTo(m.GetElement("c")!, b, DockDirection.Bottom);
        var json = m.SaveLayout();

        var m2 = DockView.NewManager("a", "c");
        m2.LoadLayout(json);

        var root = m2.CenterGroup;
        await Assert.That(root.Children.Count).IsEqualTo(2);
        await Assert.That(root.Children[0].Elements.Single().Id).IsEqualTo("a");
        await Assert
            .That(root.Children[1].IsSplitGroup)
            .IsFalse()
            .Because("the side that held only b goes, and the wrapper it leaves collapses");
        await Assert.That(root.Children[1].Elements.Single().Id).IsEqualTo("c");
    }

    [Test]
    public async Task Load_DropsEverySplitSideWhenNoIdResolves()
    {
        var m = DockView.NewManager("a", "b");
        var a = m.GetElement("a")!;
        m.Dock(a, DockPosition.Left);
        m.DockRelativeTo(m.GetElement("b")!, a, DockDirection.Bottom);
        var json = m.SaveLayout();

        var m2 = DockView.NewManager();
        m2.LoadLayout(json);

        await Assert.That(m2.LeftGroup.IsSplitGroup).IsFalse();
        await Assert.That(m2.LeftGroup.Elements.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Load_ReadsAGroupAsASplitOrALeafNeverBoth()
    {
        const string layout = """
            {
              "version": "2",
              "center": {
                "elements": ["c"],
                "children": [{ "elements": ["a"] }, { "elements": ["b"] }]
              }
            }
            """;
        var m = DockView.NewManager("a", "b", "c");

        m.LoadLayout(layout);

        await Assert.That(m.CenterGroup.Elements.Count).IsEqualTo(0);
        await Assert.That(m.CenterGroup.Children.Count).IsEqualTo(2);
        await Assert.That(m.GetElement("c")!.State).IsEqualTo(DockState.Hidden);
    }

    [Test]
    public async Task Load_DocksAnOpenDocumentTheLayoutLeavesOut_BackInTheCentre()
    {
        var layout = DockView.Docked(("tool", DockPosition.Left)).SaveLayout();
        var m = DockView.Docked(("tool", DockPosition.Right));
        var scene = new DockElement("scene", "scene", DockType.Document);
        m.Register(scene);
        m.Dock(scene, DockPosition.Center);

        m.LoadLayout(layout);

        await Assert.That(scene.Parent).IsSameReferenceAs(m.CenterGroup);
    }

    [Test]
    public async Task PanelsRegisteredAfterALoad_TakeTheirSavedPlaces_EvenThroughASaveMadeBeforeThey()
    {
        var saved = DockView.NewManager("a", "b", "f", "h");
        saved.Dock(saved.GetElement("a")!, DockPosition.Left);
        saved.DockToGroup(saved.GetElement("b")!, saved.LeftGroup);
        saved.Float(saved.GetElement("f")!, 10f, 20f, 300f, 200f);
        saved.Dock(saved.GetElement("h")!, DockPosition.Right);
        saved.GetElement("h")!.Hide();
        var early = DockView.NewManager("a");
        early.LoadLayout(saved.SaveLayout());
        var m = DockView.NewManager("a");
        m.LoadLayout(early.SaveLayout());

        m.Register(new DockElement("h", "h"));
        m.Register(new DockElement("f", "f"));
        m.Register(new DockElement("b", "b"));

        await Assert.That(m.LeftGroup.Elements.Select(e => e.Id)).IsEquivalentTo(["a", "b"]);
        await Assert.That(m.GetElement("f")!.State).IsEqualTo(DockState.Floating);
        await Assert.That(m.FloatingWindows.Single().Width).IsEqualTo(300f);
        await Assert.That(m.GetElement("h")!.State).IsEqualTo(DockState.Hidden);
        await Assert.That(m.GetElement("h")!.Position).IsEqualTo(DockPosition.Right);
    }

    [Test]
    public async Task Load_NamingAHiddenElementActive_LeavesNothingActive()
    {
        const string layout = """
            { "version": "2", "activeElementId": "hidden", "center": { "elements": ["a"] } }
            """;
        var m = DockView.NewManager("a", "hidden");

        m.LoadLayout(layout);

        await Assert.That(m.ActiveElement).IsNull();
    }

    [Test]
    [Arguments(
        """{ "version": "1.0", "groups": [], "floatingWindows": [], "hiddenElements": [] }"""
    )]
    [Arguments("""{ "groups": [] }""")]
    [Arguments("""{ "version": "2", "left": { "elements": [{ "id": "a" }] } }""")]
    [Arguments("null")]
    [Arguments("""{ "version": "2", "left": null }""")]
    [Arguments("""{ "version": "2", "center": { "elements": [null] } }""")]
    public async Task Load_RefusesALayoutOfAnotherFormat_LeavingTheDockAsItWas(string layout)
    {
        var m = DockView.NewManager("a");
        m.Dock(m.GetElement("a")!, DockPosition.Right);
        var changes = 0;
        m.LayoutChanged += (_, _) => changes++;

        await Assert.That(() => m.LoadLayout(layout)).Throws<InvalidDataException>();

        await Assert.That(m.GetElement("a")!.Position).IsEqualTo(DockPosition.Right);
        await Assert.That(m.RightGroup.Elements.Count).IsEqualTo(1);
        await Assert.That(changes).IsEqualTo(0);
    }
}

using TUnit.Assertions.Enums;

namespace Tralys.Ui.Docking.Tests;

public sealed class DockCloseHandlerTests
{
    [Test]
    public async Task Close_AnElementTheHandlerHandles_GoesToItAndStaysOpen()
    {
        var (m, owner) = WithOwner();
        var scene = Docked(m, "scene", DockType.Document);

        var closed = scene.Element.Close();

        await Assert.That(closed).IsFalse();
        await Assert.That(owner.Requests).IsEquivalentTo(["scene"]);
        await Assert.That(scene.Element.State).IsEqualTo(DockState.Docked);
        await Assert.That(ReferenceEquals(scene.Element.Parent, m.CenterGroup)).IsTrue();
        await Assert.That(scene.ClosedCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Close_AnElementTheHandlerLeaves_ClosesHereOnce()
    {
        var (m, owner) = WithOwner();
        var tool = Docked(m, "tool", DockType.Tool, DockPosition.Left);

        var closed = tool.Element.Close();

        await Assert.That(closed).IsTrue();
        await Assert.That(owner.Requests.Count).IsEqualTo(0);
        await Assert.That(tool.Element.State).IsEqualTo(DockState.Hidden);
        await Assert.That(tool.Element.Parent).IsNull();
        await Assert.That(tool.ClosedCalls).IsEqualTo(1);
    }

    [Test]
    public async Task Close_ANonCloseableElement_IsNotHandedOver()
    {
        var (m, owner) = WithOwner();
        var scene = Docked(m, "scene", DockType.Document);
        scene.Element.CanClose = false;

        var closed = scene.Element.Close();

        await Assert.That(closed).IsFalse();
        await Assert.That(owner.Requests.Count).IsEqualTo(0);
        await Assert.That(scene.Element.State).IsEqualTo(DockState.Docked);
    }

    [Test]
    public async Task CloseAll_HandsTheHandledTabsOverInOneRequest_AndClosesTheRestHere()
    {
        var (m, owner) = WithOwner();
        var first = Docked(m, "scene-1", DockType.Document);
        var tool = Docked(m, "tool", DockType.Tool);
        var second = Docked(m, "scene-2", DockType.Document);

        var closedHere = m.Close([.. m.CenterGroup.Elements]);

        await Assert.That(closedHere).IsTrue();
        await Assert
            .That(owner.Requests)
            .IsEquivalentTo(["scene-1,scene-2"])
            .Because("one request in tab order, so the owner can stop at the first cancel.");
        await Assert.That(tool.Element.State).IsEqualTo(DockState.Hidden);
        await Assert.That(tool.ClosedCalls).IsEqualTo(1);
        await Assert
            .That(Ids(m.CenterGroup.Elements))
            .IsEquivalentTo(["scene-1", "scene-2"], CollectionOrdering.Matching);
        await Assert.That(first.ClosedCalls + second.ClosedCalls).IsEqualTo(0);
    }

    [Test]
    public async Task CloseAll_WithNothingClosedHere_ReportsSo()
    {
        var (m, owner) = WithOwner();
        Docked(m, "scene-1", DockType.Document);
        Docked(m, "scene-2", DockType.Document);

        var closedHere = m.Close([.. m.CenterGroup.Elements]);

        await Assert.That(closedHere).IsFalse();
        await Assert.That(owner.Requests).IsEquivalentTo(["scene-1,scene-2"]);
        await Assert.That(m.CenterGroup.Elements.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Close_WithoutAHandler_ClosesEveryElementHere()
    {
        var m = new DockManager();
        var scene = Docked(m, "scene", DockType.Document);
        var tool = Docked(m, "tool", DockType.Tool);

        var closedHere = m.Close([.. m.CenterGroup.Elements]);

        await Assert.That(closedHere).IsTrue();
        await Assert.That(m.CenterGroup.Elements.Count).IsEqualTo(0);
        await Assert.That(scene.ClosedCalls).IsEqualTo(1);
        await Assert.That(tool.ClosedCalls).IsEqualTo(1);
    }

    [Test]
    public async Task FloatingWindowClose_HandsItsHandledTabsOver_HidesTheRest_AndStaysOpen()
    {
        var (m, owner) = WithOwner();
        var scene = Docked(m, "scene", DockType.Document);
        var tool = Docked(m, "tool", DockType.Tool, DockPosition.Left);
        var window = m.Float(scene.Element);
        m.DockToGroup(tool.Element, window.Group);

        window.RequestClose();

        await Assert.That(owner.Requests).IsEquivalentTo(["scene"]);
        await Assert.That(tool.Element.State).IsEqualTo(DockState.Hidden);
        await Assert.That(tool.Element.Parent).IsNull();
        await Assert.That(tool.ClosedCalls).IsEqualTo(0).Because("a window close hides panels.");
        await Assert.That(m.FloatingWindows.Contains(window)).IsTrue();
        await Assert.That(Ids(window.Group.Elements)).IsEquivalentTo(["scene"]);
        await Assert.That(scene.ClosedCalls).IsEqualTo(0);
    }

    [Test]
    public async Task FloatingWindowClose_ClosesOnceTheOwnerRemovesItsLastTab()
    {
        var (m, _) = WithOwner();
        var scene = Docked(m, "scene", DockType.Document);
        var window = m.Float(scene.Element);
        var removed = 0;
        m.FloatingWindowRemoved += (_, e) => removed += ReferenceEquals(e.Window, window) ? 1 : 0;

        window.RequestClose();
        await Assert.That(removed).IsEqualTo(0);

        m.Unregister(scene.Element);

        await Assert.That(removed).IsEqualTo(1);
        await Assert.That(m.FloatingWindows.Count).IsEqualTo(0);
    }

    [Test]
    public async Task FloatingWindowClose_WithNothingHandled_HidesEveryTabAndCloses()
    {
        var (m, owner) = WithOwner();
        var tool = Docked(m, "tool", DockType.Tool, DockPosition.Left);
        var window = m.Float(tool.Element);

        window.RequestClose();

        await Assert.That(owner.Requests.Count).IsEqualTo(0);
        await Assert.That(m.FloatingWindows.Count).IsEqualTo(0);
        await Assert.That(tool.Element.State).IsEqualTo(DockState.Hidden);
        await Assert.That(tool.Element.Parent).IsNull();
        await Assert.That(tool.ClosedCalls).IsEqualTo(0);
    }

    [Test]
    public async Task FloatingWindowClose_ClosesItsDocuments_DocksBackWhatCannotClose_AndHidesItsTools()
    {
        var m = new DockManager();
        var scene = Docked(m, "scene", DockType.Document);
        var pinned = Docked(m, "pinned", DockType.Document);
        var tool = Docked(m, "tool", DockType.Tool, DockPosition.Left);
        pinned.Element.CanClose = false;
        var window = m.Float(scene.Element);
        m.DockToGroup(pinned.Element, window.Group);
        m.DockToGroup(tool.Element, window.Group);

        window.RequestClose();

        await Assert.That(m.FloatingWindows.Count).IsEqualTo(0);
        await Assert.That(scene.Element.State).IsEqualTo(DockState.Hidden);
        await Assert.That(scene.ClosedCalls).IsEqualTo(1);
        await Assert.That(pinned.Element.Parent).IsSameReferenceAs(m.CenterGroup);
        await Assert.That(tool.Element.State).IsEqualTo(DockState.Hidden);
        await Assert.That(tool.ClosedCalls).IsEqualTo(0);
    }

    [Test]
    public async Task FloatingWindowClose_RaisesLayoutChangedOnce()
    {
        var m = new DockManager();
        var window = m.Float(Docked(m, "tool", DockType.Tool).Element);
        var changes = 0;
        m.LayoutChanged += (_, _) => changes++;

        window.RequestClose();

        await Assert.That(changes).IsEqualTo(1);
    }

    [Test]
    public async Task Close_TheActiveTab_ActivatesItsNeighbour_ThenNothingOnceTheGroupEmpties()
    {
        var m = new DockManager();
        var first = Docked(m, "first", DockType.Tool).Element;
        var second = Docked(m, "second", DockType.Tool).Element;
        first.Activate();

        first.Close();
        await Assert.That(m.ActiveElement).IsSameReferenceAs(second);

        second.Close();
        await Assert.That(m.ActiveElement).IsNull();
    }

    [Test]
    public async Task Unregister_ADockedElement_RaisesLayoutChanged()
    {
        var m = new DockManager();
        var docked = Docked(m, "docked", DockType.Document);
        var hidden = Docked(m, "hidden", DockType.Tool, DockPosition.Left);
        hidden.Element.Hide();
        var changes = 0;
        m.LayoutChanged += (_, _) => changes++;

        m.Unregister(hidden.Element);
        await Assert.That(changes).IsEqualTo(0).Because("nothing on screen moved.");

        m.Unregister(docked.Element);
        await Assert
            .That(changes)
            .IsEqualTo(1)
            .Because("a control must rebuild a region the removal emptied or collapsed.");
    }

    private static string[] Ids(IEnumerable<DockElement> elements) =>
        [.. elements.Select(e => e.Id)];

    private static (DockManager, RecordingOwner) WithOwner()
    {
        var owner = new RecordingOwner();
        return (new DockManager { CloseHandler = owner }, owner);
    }

    private static Panel Docked(
        DockManager m,
        string id,
        DockType type,
        DockPosition position = DockPosition.Center
    )
    {
        var source = new CountingSource(id);
        var element = m.Register(source, type);
        m.Dock(element, position);
        return new Panel(source, element);
    }

    private sealed record Panel(CountingSource Source, DockElement Element)
    {
        public int ClosedCalls => Source.ClosedCalls;
    }

    private sealed class RecordingOwner : IDockCloseHandler
    {
        public List<string> Requests { get; } = [];

        public bool Handles(DockElement element) => element.DockType == DockType.Document;

        public void RequestClose(IReadOnlyList<DockElement> elements) =>
            Requests.Add(string.Join(',', Ids(elements)));
    }

    private sealed class CountingSource(string id) : DockSource(id, id)
    {
        public override DockPosition DefaultPosition => DockPosition.Center;

        public int ClosedCalls { get; private set; }

        public override void OnClosed() => ClosedCalls++;
    }
}

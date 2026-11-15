namespace Tralys.Ui.Docking.Tests;

public sealed class DockSourceTests
{
    [Test]
    public async Task ASourcesElementHiddenAndShown_ReportsItsVisibilityEachTime()
    {
        var m = new DockManager();
        var panel = new Panel("panel");
        var element = m.Register(panel);
        m.Dock(element, DockPosition.Left);
        var seen = new List<bool>();
        panel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DockSource.IsVisible))
                seen.Add(panel.IsVisible);
        };

        element.Hide();
        element.Show();

        await Assert.That(seen).IsEquivalentTo([false, true]);
    }

    [Test]
    public async Task AnUnregisteredSource_LetsGoOfItsElement_AndPushesItNothingMore()
    {
        var m = new DockManager();
        var panel = new Panel("panel");
        var element = m.Register(panel);
        m.Dock(element, DockPosition.Left);

        m.Unregister(element);
        panel.Edit("Renamed");

        await Assert.That(panel.DockElement).IsNull();
        await Assert.That(element.Title).IsEqualTo("panel");
    }

    [Test]
    public async Task ASourceWhoseIdIsTaken_ThrowsOnRegistering_AndHoldsNoElement()
    {
        var m = new DockManager();
        m.Register(new Panel("panel"));
        var second = new Panel("panel");

        await Assert.That(() => m.Register(second)).Throws<InvalidOperationException>();
        await Assert.That(second.DockElement).IsNull();
    }

    private sealed class Panel(string id) : DockSource(id, id)
    {
        public override DockPosition DefaultPosition => DockPosition.Left;

        public void Edit(string title)
        {
            Title = title;
            IsDirty = true;
        }
    }
}

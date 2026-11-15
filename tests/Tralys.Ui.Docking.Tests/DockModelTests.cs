namespace Tralys.Ui.Docking.Tests;

public sealed class DockModelTests
{
    sealed class Source(string id, DockPosition position) : DockSource(id, id)
    {
        public override DockPosition DefaultPosition => position;

        public int Reopened { get; private set; }

        public override void OnReopened() => Reopened++;
    }

    [Test]
    public async Task Showing_a_hidden_element_docks_it_where_it_last_sat_else_its_sources_region_else_by_its_type()
    {
        var manager = new DockManager();
        var tool = new DockElement("tool", "tool");
        var document = new DockElement("doc", "doc", DockType.Document);
        var moved = new DockElement("moved", "moved");
        manager.Register(tool);
        manager.Register(document);
        manager.Register(moved);
        var sourced = manager.Register(new Source("src", DockPosition.Bottom));
        manager.Dock(moved, DockPosition.Top);
        moved.Hide();
        tool.Activate();

        tool.Show();
        document.Show();
        sourced.Show();
        moved.Show();

        await Assert.That(tool.Position).IsEqualTo(DockPosition.Right);
        await Assert.That(document.Position).IsEqualTo(DockPosition.Center);
        await Assert.That(sourced.Position).IsEqualTo(DockPosition.Bottom);
        await Assert.That(moved.Position).IsEqualTo(DockPosition.Top);
        await Assert.That(manager.ActiveElement).IsSameReferenceAs(moved);
    }

    [Test]
    public async Task Moving_a_tab_past_either_end_clamps_and_moving_it_onto_its_own_slot_reports_nothing()
    {
        var manager = DockView.Docked(
            ("a", DockPosition.Left),
            ("b", DockPosition.Left),
            ("c", DockPosition.Left)
        );
        var group = manager.LeftGroup;
        var a = manager.GetElement("a")!;
        var changes = 0;
        manager.LayoutChanged += (_, _) => changes++;

        group.MoveTo(a, 99);
        var last = group.Elements[^1];
        group.MoveTo(a, 2);
        var unchanged = changes;
        group.MoveTo(a, -5);

        await Assert.That(last).IsSameReferenceAs(a);
        await Assert.That(unchanged).IsEqualTo(1);
        await Assert.That(group.Elements[0]).IsSameReferenceAs(a);
        await Assert.That(changes).IsEqualTo(2);
    }

    [Test]
    public async Task A_closed_source_reopens_once_whichever_path_brings_it_back()
    {
        var manager = new DockManager();
        var source = new Source("s", DockPosition.Left);
        var element = manager.Register(source);
        manager.Dock(element, DockPosition.Left);

        element.Close();
        manager.Float(element);
        element.Close();
        element.Show();
        element.Hide();
        element.Show();

        await Assert.That(source.Reopened).IsEqualTo(2);
    }

    [Test]
    public async Task A_sources_visibility_follows_its_element_and_setting_it_shows_or_hides_without_closing()
    {
        var manager = new DockManager();
        var source = new Source("s", DockPosition.Left);
        var unregistered = new Source("u", DockPosition.Left);
        var element = manager.Register(source);

        source.IsVisible = true;
        var shown = (element.State, manager.ActiveElement, source.IsVisible);
        source.IsVisible = false;
        unregistered.IsVisible = true;

        await Assert.That(shown).IsEqualTo((DockState.Docked, (DockElement?)element, true));
        await Assert.That(element.State).IsEqualTo(DockState.Hidden);
        await Assert.That(source.IsVisible).IsFalse();
        await Assert.That(source.Reopened).IsEqualTo(0);
        await Assert.That(unregistered.IsVisible).IsFalse();
    }
}

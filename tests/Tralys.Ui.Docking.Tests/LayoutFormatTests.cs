namespace Tralys.Ui.Docking.Tests;

/// <summary>Saved layouts outlive builds: the bytes an arrangement saves to are pinned, and a pinned file
/// saves back to the same bytes after a load.</summary>
public sealed class LayoutFormatTests
{
    static string Pinned(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Layouts", name + ".json"))
            .ReplaceLineEndings("\n")
            .TrimEnd();

    static string Saved(DockManager manager) => manager.SaveLayout().ReplaceLineEndings("\n");

    static DockManager NestedSplits()
    {
        var m = DockView.NewManager("a", "b", "c", "d");
        var a = m.GetElement("a")!;
        var b = m.GetElement("b")!;
        m.Dock(a, DockPosition.Center);
        m.DockRelativeTo(b, a, DockDirection.Right);
        m.DockRelativeTo(m.GetElement("c")!, b, DockDirection.Bottom);
        m.DockRelativeTo(m.GetElement("d")!, a, DockDirection.Top);
        m.CenterGroup.Children[0].SizeProportion = 0.3f;
        m.CenterGroup.Children[1].SizeProportion = 0.7f;
        b.Parent!.Parent!.Children[0].SizeProportion = 0.123456f;
        m.PanelSizes = new DockPanelSizes
        {
            LeftWidth = 412,
            RightWidth = 280,
            BottomHeight = 175,
        };
        return m;
    }

    static DockManager Floats()
    {
        var m = DockView.NewManager("a", "b", "c", "d");
        var a = m.GetElement("a")!;
        m.Dock(a, DockPosition.Left);
        var window = m.Float(a, 50, 60, 320, 240);
        m.DockToGroup(m.GetElement("b")!, window.Group);
        m.DockToGroup(m.GetElement("c")!, window.Group, 0);
        m.Float(m.GetElement("d")!, 1.5f, 2.25f, 333.3f, 99.9f);
        m.FloatingWindows[1].X = 77;
        return m;
    }

    static DockManager Hidden()
    {
        var m = DockView.NewManager("a", "b", "c", "d");
        m.Dock(m.GetElement("a")!, DockPosition.Left);
        m.Dock(m.GetElement("b")!, DockPosition.Right);
        m.GetElement("b")!.Hide();
        m.Dock(m.GetElement("c")!, DockPosition.Bottom);
        m.GetElement("c")!.Close();
        return m;
    }

    [Test]
    public async Task Nested_splits_save_to_the_pinned_bytes() =>
        await Assert.That(Saved(NestedSplits())).IsEqualTo(Pinned("nested-splits"));

    [Test]
    public async Task Floating_windows_save_to_the_pinned_bytes() =>
        await Assert.That(Saved(Floats())).IsEqualTo(Pinned("floats"));

    [Test]
    public async Task Hidden_panels_save_to_the_pinned_bytes() =>
        await Assert.That(Saved(Hidden())).IsEqualTo(Pinned("hidden"));

    [Test]
    [Arguments("nested-splits")]
    [Arguments("hidden")]
    [Arguments("floats")]
    public async Task A_pinned_layout_saves_back_to_its_own_bytes(string name)
    {
        var manager = DockView.NewManager("a", "b", "c", "d");

        manager.LoadLayout(Pinned(name));

        await Assert.That(Saved(manager)).IsEqualTo(Pinned(name));
    }
}

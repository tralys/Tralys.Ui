using Tralys.Ui.Docking;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Dock>("Layout")]
static class DockStories
{
    enum Layout : byte
    {
        Docked,
        Split,
        Floating,
    }

    static readonly Slot<DockElement> Title = Slot<DockElement>.Of(
        static (ui, element, _) =>
        {
            using (ui.Box(grow: 1, align: Align.Center, justify: Justify.Center))
                ui.Text(
                    element.Title,
                    fontWeight: ui.Theme.Weight(WeightToken.Interface),
                    wrap: true
                );
        }
    );

    [FramePath]
    public static void Default(Ui ui) => Show(ui, ui.Remember(static () => Made(Layout.Docked)));

    [FramePath]
    public static void Split(Ui ui) => Show(ui, ui.Remember(static () => Made(Layout.Split)));

    [FramePath]
    public static void Floating(Ui ui) => Show(ui, ui.Remember(static () => Made(Layout.Floating)));

    [FramePath]
    static void Show(
        Ui ui,
        DockManager manager,
        UiKey key = default,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    ) =>
        ui.Dock(
            manager: manager,
            content: Title,
            windowHost: ui.Use(StoryWindows.Key).Host,
            width: 1000,
            height: 640,
            key: key,
            file: file,
            line: line
        );

    static DockManager Made(Layout layout)
    {
        var manager = new DockManager();
        Place(manager, "Solution Explorer", DockPosition.Left);
        Place(manager, "Properties", DockPosition.Right);
        if (layout == Layout.Docked)
            Place(manager, "Output", DockPosition.Bottom);
        var first = Place(manager, "Document 1", DockPosition.Center);
        var second = Place(manager, "Document 2", DockPosition.Center);
        if (layout == Layout.Split)
            manager.DockRelativeTo(second, first, DockDirection.Right);
        if (layout == Layout.Floating)
            manager.Float(
                Element(manager, "Floating Panel"),
                x: 200f,
                y: 80f,
                width: 320f,
                height: 180f
            );
        return manager;
    }

    static DockElement Element(DockManager manager, string title)
    {
        var element = new DockElement(title);
        manager.Register(element);
        return element;
    }

    static DockElement Place(DockManager manager, string title, DockPosition position)
    {
        var element = Element(manager, title);
        manager.Dock(element, position);
        return element;
    }
}

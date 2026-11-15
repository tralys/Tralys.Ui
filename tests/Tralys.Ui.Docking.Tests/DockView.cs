using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Docking.Tests;

/// <summary>A dock over a manager with an element docked at each position given, in a 1000 by 700 view; an element's
/// panel shows its content when that is a string.</summary>
sealed class DockView
{
    internal static readonly Slot<DockElement> Text = Slot<DockElement>.Of(
        static (ui, element, _) =>
        {
            if (element.Content is string text)
                ui.Text(text);
        }
    );

    public DockView(params (string Id, DockPosition Position)[] docked)
        : this(Docked(docked)) { }

    public DockView(
        DockManager manager,
        IDockWindowHost? host = null,
        Slot<DockElement> content = default,
        bool centerDockable = true
    )
    {
        Manager = manager;
        Shown = manager;
        Host = host;
        Content = content.IsEmpty ? Text : content;
        CenterDockable = centerDockable;
        Ui = UiDriver.Render(
            ui =>
            {
                if (Mounted)
                    ui.Dock(
                        manager: Shown,
                        content: Content,
                        windowHost: Host,
                        centerDockable: CenterDockable,
                        onResize: Callback.Of(this, static d => d.Resizes++)
                    );
            },
            size: new Size(1000f, 700f)
        );
    }

    public DockManager Manager { get; }

    public DockManager? Shown { get; set; }

    public bool Mounted { get; set; } = true;

    public IDockWindowHost? Host { get; set; }

    public Slot<DockElement> Content { get; }

    public bool CenterDockable { get; set; }

    public int Resizes { get; private set; }

    public UiDriver Ui { get; }

    public static DockManager NewManager(params string[] ids)
    {
        var manager = new DockManager();
        foreach (var id in ids)
            manager.Register(new DockElement(id, id));
        return manager;
    }

    public static DockManager Docked(params (string Id, DockPosition Position)[] docked)
    {
        var manager = NewManager([.. docked.Select(entry => entry.Id)]);
        foreach (var (id, position) in docked)
            manager.Dock(manager.GetElement(id)!, position);
        return manager;
    }

    public DockElement this[string id] => Manager.GetElement(id)!;

    /// <summary>The tab of the element titled <paramref name="title"/>.</summary>
    public Node Tab(string title) => Tab(Ui.Root, title);

    public static Node Tab(Node root, string title) =>
        RootOf(
            Single(
                root,
                node =>
                    node.Kind == NodeKind.Component
                    && node.Component == typeof(Tabs.Tab)
                    && Any(
                        node,
                        text => text.Kind == NodeKind.Text && text.Text.SequenceEqual(title)
                    )
            )
        );

    /// <summary>The tab group that shows <paramref name="element"/>'s tab.</summary>
    public Node Strip(DockElement element) =>
        RootOf(Above(Tab(element.Title), typeof(DockTabGroup))!);

    /// <summary>The title bar of the one floating panel, or of the one with <paramref name="title"/>.</summary>
    public Node Title(string? title = null) => Title(Ui.Root, title);

    public static Node Title(Node root, string? title = null) =>
        RootOf(
            Single(
                root,
                node =>
                    node.Kind == NodeKind.Component
                    && node.Component == typeof(DockFloatTitle)
                    && (
                        title is null
                        || Any(
                            node,
                            text => text.Kind == NodeKind.Text && text.Text.SequenceEqual(title)
                        )
                    )
            )
        );

    public Node Overlay => RootOf(Single(Ui.Root, node => node.Component == typeof(DockOverlay)));

    public bool OverlayShown => Overlay.Children.MoveNext();

    /// <summary>The overlay's boxes <paramref name="size"/> square: 48 for the compass, 40 for the root indicators.</summary>
    public List<Node> Boxes(float size) =>
        All(Overlay, node => node.ViewBounds.Width == size && node.ViewBounds.Height == size);

    public List<Node> Compass => Boxes(DockLook.CompassSize);

    public List<Node> Indicators => Boxes(DockLook.RootSize);

    /// <summary>The previews of where a drop lands: the overlay's boxes that are neither indicators nor the label.</summary>
    public List<Node> Previews
    {
        get
        {
            var previews = new List<Node>();
            foreach (var child in Overlay.Children)
            {
                var size = child.ViewBounds.Width;
                if (
                    size != DockLook.CompassSize
                    && size != DockLook.RootSize
                    && !Any(child, node => node.Kind == NodeKind.Text)
                )
                    previews.Add(child);
            }
            return previews;
        }
    }

    /// <summary>The handles of the edges, then those of the splits inside the regions, in tree order.</summary>
    public List<Node> Handles(bool nested) =>
        All(
                Ui.Root,
                node =>
                    node.Kind == NodeKind.Component
                    && node.Component == typeof(ResizeHandle)
                    && (Above(node, typeof(DockSplit)) is not null) == nested
            )
            .ConvertAll(RootOf);

    /// <summary>The width of the region <paramref name="element"/>'s group shows in, or 0 while it shows none.</summary>
    public float WidthOf(DockElement element) =>
        Any(
            Ui.Root,
            node =>
                node.Component == typeof(Tabs.Tab)
                && Any(
                    node,
                    text => text.Kind == NodeKind.Text && text.Text.SequenceEqual(element.Title)
                )
        )
            ? Strip(element).ViewBounds.Width
            : 0f;

    public static Vector2 CentreOf(Node node) =>
        new(
            node.ViewBounds.X + node.ViewBounds.Width / 2f,
            node.ViewBounds.Y + node.ViewBounds.Height / 2f
        );

    /// <summary>The first node in or under <paramref name="node"/> that lays out with an area.</summary>
    public static Node Laid(Node node)
    {
        if (!node.ViewBounds.IsEmpty)
            return node;
        foreach (var child in node.Children)
        {
            if (Laid(child) is { ViewBounds.IsEmpty: false } found)
                return found;
        }
        return node;
    }

    public static Node RootOf(Node component)
    {
        var children = component.Children;
        return children.MoveNext() ? children.Current : component;
    }

    public static Node? Above(Node node, Type component)
    {
        for (var at = node.Parent; at is not null; at = at.Parent)
        {
            if (at.Component == component)
                return at;
        }
        return null;
    }

    public static bool Any(Node root, Func<Node, bool> match)
    {
        if (match(root))
            return true;
        foreach (var child in root.Children)
        {
            if (Any(child, match))
                return true;
        }
        return false;
    }

    public static List<Node> All(Node root, Func<Node, bool> match)
    {
        var found = new List<Node>();
        Collect(root, match, found);
        return found;
    }

    static void Collect(Node node, Func<Node, bool> match, List<Node> found)
    {
        if (match(node))
            found.Add(node);
        foreach (var child in node.Children)
            Collect(child, match, found);
    }

    static Node Single(Node root, Func<Node, bool> match)
    {
        var found = All(root, match);
        return found.Count == 1
            ? found[0]
            : throw new InvalidOperationException($"Found {found.Count} matches, not one.");
    }
}

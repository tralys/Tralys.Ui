using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tralys.Ui.Docking;

internal static class LayoutSerializer
{
    internal const string FormatVersion = "2";

    public static string Serialize(DockManager manager)
    {
        var tabs = new Dictionary<DockGroup, List<string>>();
        var layout = new SavedLayout
        {
            Version = FormatVersion,
            ActiveElementId = manager.ActiveElement?.Id,
            PanelSizes = manager.PanelSizes,
            Left = SerializeGroup(manager.LeftGroup, tabs),
            Right = SerializeGroup(manager.RightGroup, tabs),
            Top = SerializeGroup(manager.TopGroup, tabs),
            Bottom = SerializeGroup(manager.BottomGroup, tabs),
            Center = SerializeGroup(manager.CenterGroup, tabs),
            FloatingWindows = SerializeFloatingWindows(manager.FloatingWindows, tabs),
            HiddenElements = SerializeHiddenElements(manager.Elements),
        };
        manager.Unplaced.WriteBack(manager, layout, tabs);

        return JsonSerializer.Serialize(layout, DockLayoutJsonContext.Default.SavedLayout);
    }

    public static void Deserialize(DockManager manager, string layoutJson)
    {
        var layout = Read(layoutJson);

        var unplaced = new SavedPlaces();
        RememberGroup(manager, unplaced, DockPosition.Left, layout.Left);
        RememberGroup(manager, unplaced, DockPosition.Right, layout.Right);
        RememberGroup(manager, unplaced, DockPosition.Top, layout.Top);
        RememberGroup(manager, unplaced, DockPosition.Bottom, layout.Bottom);
        RememberGroup(manager, unplaced, DockPosition.Center, layout.Center);
        foreach (var window in layout.FloatingWindows)
            unplaced.Remember(
                manager,
                DockPosition.Floating,
                window.Elements,
                window.ActiveElementId,
                window
            );
        foreach (var hidden in layout.HiddenElements)
            unplaced.Remember(manager, hidden.Position, [hidden.Id], null, hidden: true);
        manager.Unplaced = unplaced;

        var documents = manager.ClearLayout();
        manager.PanelSizes = layout.PanelSizes;

        RestoreGroup(manager, manager.LeftGroup, layout.Left);
        RestoreGroup(manager, manager.RightGroup, layout.Right);
        RestoreGroup(manager, manager.TopGroup, layout.Top);
        RestoreGroup(manager, manager.BottomGroup, layout.Bottom);
        RestoreGroup(manager, manager.CenterGroup, layout.Center);

        foreach (var windowData in layout.FloatingWindows)
        {
            RestoreFloatingWindow(manager, windowData);
        }

        // The reset cleared hidden elements' regions, which a later Show docks them back into.
        foreach (var hidden in layout.HiddenElements)
        {
            if (
                manager.GetElement(hidden.Id) is { } dockElement
                && dockElement.State == DockState.Hidden
                && hidden.Position.IsRootRegion()
            )
            {
                dockElement.Position = hidden.Position;
            }
        }

        manager.DockBack(documents);

        if (!string.IsNullOrEmpty(layout.ActiveElementId))
        {
            var activeElement = manager.GetElement(layout.ActiveElementId);
            activeElement?.Activate();
        }
    }

    private static SavedLayout Read(string layoutJson)
    {
        SavedLayout? layout;
        try
        {
            layout = JsonSerializer.Deserialize(
                layoutJson,
                DockLayoutJsonContext.Default.SavedLayout
            );
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Saved dock layout is unreadable: {ex.Message}", ex);
        }

        if (layout?.Version != FormatVersion)
            throw new InvalidDataException(
                $"Saved dock layout is format '{layout?.Version}'; this build reads format '{FormatVersion}'."
            );
        if (HasNullEntry(layout))
            throw new InvalidDataException("Saved dock layout has a null list entry.");
        return layout;
    }

    // Nullable annotations stop short of a list's entries.
    private static bool HasNullEntry(SavedLayout layout) =>
        HasNullEntry(layout.Left)
        || HasNullEntry(layout.Right)
        || HasNullEntry(layout.Top)
        || HasNullEntry(layout.Bottom)
        || HasNullEntry(layout.Center)
        || layout.HiddenElements.Any(hidden => hidden is null)
        || layout.FloatingWindows.Any(w => w is null || w.Elements.Any(id => id is null));

    private static bool HasNullEntry(SavedGroup group) =>
        group.Elements.Any(id => id is null)
        || group.Children.Any(child => child is null || HasNullEntry(child));

    // A load shows none of a splitting group's own elements, only its children's.
    private static void RememberGroup(
        DockManager manager,
        SavedPlaces unplaced,
        DockPosition position,
        SavedGroup group
    )
    {
        if (group.Children.Count == 0)
        {
            unplaced.Remember(manager, position, group.Elements, group.ActiveElementId);
            return;
        }
        foreach (var child in group.Children)
            RememberGroup(manager, unplaced, position, child);
    }

    private static SavedGroup SerializeGroup(
        DockGroup group,
        Dictionary<DockGroup, List<string>> tabs
    )
    {
        var saved = new SavedGroup
        {
            Orientation = group.Orientation,
            SizeProportion = group.SizeProportion,
            Elements = group.Elements.Select(e => e.Id).ToList(),
            Children = group.Children.Select(child => SerializeGroup(child, tabs)).ToList(),
            ActiveElementId = group.ActiveElement?.Id,
        };
        if (!group.IsSplitGroup)
            tabs[group] = saved.Elements;
        return saved;
    }

    private static List<SavedElementPosition> SerializeHiddenElements(
        IReadOnlyList<DockElement> elements
    )
    {
        return elements
            .Where(e => e.State == DockState.Hidden && e.Position.IsRootRegion())
            .Select(e => new SavedElementPosition { Id = e.Id, Position = e.Position })
            .ToList();
    }

    private static List<SavedFloatingWindow> SerializeFloatingWindows(
        IReadOnlyList<FloatingWindow> windows,
        Dictionary<DockGroup, List<string>> tabs
    )
    {
        var saved = new List<SavedFloatingWindow>(windows.Count);
        foreach (var window in windows)
        {
            var elements = window.Group.Elements.Select(e => e.Id).ToList();
            tabs[window.Group] = elements;
            saved.Add(
                new SavedFloatingWindow
                {
                    X = window.X,
                    Y = window.Y,
                    Width = window.Width,
                    Height = window.Height,
                    Elements = elements,
                    ActiveElementId = window.Group.ActiveElement?.Id,
                }
            );
        }
        return saved;
    }

    private static void RestoreGroup(DockManager manager, DockGroup group, SavedGroup groupData)
    {
        group.Orientation = groupData.Orientation;
        group.RestoreSizeProportion(groupData.SizeProportion);

        if (groupData.Children.Count > 0)
        {
            foreach (var childData in groupData.Children)
            {
                var child = new DockGroup(group.Position);
                child.SetManager(manager);
                RestoreGroup(manager, child, childData);
                if (child.Elements.Count > 0 || child.IsSplitGroup)
                    group.AttachChild(child);
            }

            group.CollapseRedundantWrapper();
            return;
        }

        foreach (var id in groupData.Elements)
        {
            if (manager.GetElement(id) is { } element)
                group.Insert(element);
        }

        if (
            !string.IsNullOrEmpty(groupData.ActiveElementId)
            && manager.GetElement(groupData.ActiveElementId) is { } activeElement
        )
        {
            group.ActiveElement = activeElement;
        }
    }

    private static void RestoreFloatingWindow(DockManager manager, SavedFloatingWindow windowData)
    {
        // A panel since removed drops out rather than the window; a torn-out window comes back as an in-app float.
        var elements = windowData
            .Elements.Select(manager.GetElement)
            .OfType<DockElement>()
            .ToList();

        if (elements.Count == 0)
            return;

        var floatingWindow = manager.Float(
            elements[0],
            windowData.X,
            windowData.Y,
            windowData.Width,
            windowData.Height
        );

        foreach (var additional in elements.Skip(1))
        {
            floatingWindow.Group.Insert(additional);
        }

        if (
            !string.IsNullOrEmpty(windowData.ActiveElementId)
            && manager.GetElement(windowData.ActiveElementId) is { } activeElement
        )
        {
            floatingWindow.Group.ActiveElement = activeElement;
        }
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    RespectNullableAnnotations = true,
    WriteIndented = true
)]
[JsonSerializable(typeof(SavedLayout))]
internal partial class DockLayoutJsonContext : JsonSerializerContext;

internal sealed class SavedLayout
{
    public string Version { get; set; } = string.Empty;
    public string? ActiveElementId { get; set; }
    public DockPanelSizes PanelSizes { get; set; }
    public SavedGroup Left { get; set; } = new();
    public SavedGroup Right { get; set; } = new();
    public SavedGroup Top { get; set; } = new();
    public SavedGroup Bottom { get; set; } = new();
    public SavedGroup Center { get; set; } = new();
    public List<SavedFloatingWindow> FloatingWindows { get; set; } = [];

    public List<SavedElementPosition> HiddenElements { get; set; } = [];
}

internal sealed class SavedElementPosition
{
    public string Id { get; set; } = string.Empty;
    public DockPosition Position { get; set; }
}

internal sealed class SavedGroup
{
    public SplitOrientation Orientation { get; set; }
    public float SizeProportion { get; set; } = 1.0f;
    public List<string> Elements { get; set; } = [];
    public List<SavedGroup> Children { get; set; } = [];
    public string? ActiveElementId { get; set; }
}

internal sealed class SavedFloatingWindow
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public List<string> Elements { get; set; } = [];
    public string? ActiveElementId { get; set; }
}

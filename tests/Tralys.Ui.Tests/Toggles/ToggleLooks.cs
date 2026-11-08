using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

/// <summary>A toggle family test's controlled values, fed back through their generated setters.</summary>
public sealed partial class Choices : ObservableObject
{
    [ObservableProperty]
    public partial bool On { get; set; }

    [ObservableProperty]
    public partial CheckState State { get; set; }

    [ObservableProperty]
    public partial UiKey Picked { get; set; }

    [ObservableProperty]
    public partial bool Open { get; set; }

    [ObservableProperty]
    public partial int Count { get; set; }
}

internal static class ToggleLooks
{
    public static Paint Dark(ColorToken token) => Themes.Dark.Color(token);

    /// <summary>The first box with a border: a mark's surface, a segment's ring, a card's or tile's frame.</summary>
    public static Node Edged(Node root) =>
        First(root, static n => n.Kind == NodeKind.Box && n.Layout.Border.Left > 0f)
        ?? throw new InvalidOperationException("Nothing edged.");

    /// <summary>A switch's knob: the box placed absolutely with a fill and no edge.</summary>
    public static Node Knob(Node root) =>
        First(
            root,
            static n =>
                n.Kind == NodeKind.Box
                && n.Place.Position == Position.Absolute
                && n.Layout.Border.Left == 0f
                && n.Visual.Background.IsVisible
        ) ?? throw new InvalidOperationException("No knob.");

    public static Node Tick(Node root) =>
        First(root, static n => n.Kind == NodeKind.Canvas)
        ?? throw new InvalidOperationException("No tick.");

    public static float Scale(Node node) => node.Visual.Transform.Matrix.M11;

    public static bool Has(Node root, Func<Node, bool> match) => First(root, match) is not null;

    public static Node? First(Node node, Func<Node, bool> match)
    {
        foreach (var child in node.Children)
        {
            if (match(child))
                return child;
            if (First(child, match) is { } found)
                return found;
        }
        return null;
    }
}

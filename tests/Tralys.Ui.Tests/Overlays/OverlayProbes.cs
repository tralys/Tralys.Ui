using System.Numerics;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Components.Tests;

internal static class OverlayProbes
{
    public static bool Within(Node? node, Node ancestor)
    {
        for (var n = node; n is not null; n = n.Parent)
        {
            if (n == ancestor)
                return true;
        }
        return false;
    }

    /// <summary>The text of the one tooltip shown, or null while none is.</summary>
    public static string? ShownTip(UiDriver driver) =>
        driver.View.Layers is [var layer]
        && FirstOf(layer, static n => n.Kind == NodeKind.Text) is { } text
            ? text.Text.ToString()
            : null;

    public static Node? FirstOf(Node within, Func<Node, bool> match)
    {
        foreach (var child in within.Children)
        {
            if (match(child))
                return child;
            if (FirstOf(child, match) is { } found)
                return found;
        }
        return null;
    }

    public static Vector2 Centre(Node node)
    {
        var bounds = node.ViewBounds;
        return new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
    }

    public static Node RootOf<TComponent>(this UiDriver driver) => BoxOf(driver.Get<TComponent>());

    public static Node BoxOf(Node component) =>
        FirstOf(component, static n => n.Kind == NodeKind.Box)
        ?? throw new InvalidOperationException($"{component.Component?.Name} drew no box.");
}

internal sealed class Reports
{
    public readonly List<bool> Opens = [];
    public int Submits;
    public int Keys;

    public Callback<bool> OnOpenChange =>
        Callback<bool>.Of(this, static (r, open) => r.Opens.Add(open));

    public Callback OnSubmit => Callback.Of(this, static r => r.Submits++);
}

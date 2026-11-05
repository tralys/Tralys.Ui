using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

internal static class Nodes
{
    public static Node Find(Node within, NodeKind kind) =>
        FindOrNull(within, kind)
        ?? throw new InvalidOperationException($"No {kind} in {within.Kind}.");

    static Node? FindOrNull(Node within, NodeKind kind)
    {
        foreach (var child in within.Children)
        {
            if (child.Kind == kind)
                return child;
            if (FindOrNull(child, kind) is { } found)
                return found;
        }
        return null;
    }

    /// <summary>The box the last <see cref="Icon"/> within <paramref name="within"/> draws its glyph in.</summary>
    public static Node Glyph(Node within) =>
        LastIcon(within) ?? throw new InvalidOperationException("No icon.");

    static Node? LastIcon(Node within)
    {
        Node? found = null;
        foreach (var child in within.Children)
        {
            if (child.Component == typeof(Icon))
                foreach (var box in child.Children)
                    found = box;
            if (LastIcon(child) is { } inner)
                found = inner;
        }
        return found;
    }
}

/// <summary>An icon set whose every role draws a glyph of its own.</summary>
internal static class TestIcons
{
    public static readonly IconSet Set = Make();

    public static UiDriver WithIcons(this UiDriver driver)
    {
        driver.View.Context.Icons = Set;
        driver.Settle();
        return driver;
    }

    static IconSet Make()
    {
        var roles = new IconGlyph[Enum.GetValues<IconRole>().Length];
        for (var i = 0; i < roles.Length; i++)
            roles[i] = new IconGlyph(0xE000 + i, "lucide");
        return new IconSet(roles);
    }
}

internal static class Fruits
{
    public static readonly string[] Names =
    [
        "Apple",
        "Banana",
        "Cherry",
        "Grape",
        "Mango",
        "Orange",
    ];

    public static readonly IItemsSource<string> Items = ItemsSource.From(Names);

    public static readonly KeyOf<string> ByName = static name => name;

    public static readonly TextOf<string> Named = static name => name;
}

/// <summary>A combobox's controlled value, and what it reported.</summary>
public sealed partial class Picked : ObservableObject
{
    public readonly List<string> Values = [];
    public readonly List<bool> Opens = [];

    [ObservableProperty]
    public partial string? Value { get; set; }

    public Callback<UiKey> OnValueChange =>
        Callback<UiKey>.Of(
            this,
            static (p, key) =>
            {
                key.TryGetString(out var name);
                p.Values.Add(name ?? "");
                p.Value = name;
            }
        );

    public Callback<bool> OnOpenChange =>
        Callback<bool>.Of(this, static (p, open) => p.Opens.Add(open));
}

public readonly record struct FaderProps
{
    public Callback OnClick { get; init; }
}

/// <summary>A panel that fades in and out over a second through its presence.</summary>
public sealed class Fader : Component<FaderProps>
{
    protected override void Render(Ui ui, in FaderProps p)
    {
        var shown = ui.Presence(exit: new Transition(1f));
        using (
            ui.Box(
                width: 100,
                height: 100,
                opacity: shown,
                background: ui.Theme.Color(ColorToken.BgOverlay),
                onClick: p.OnClick
            )
        ) { }
    }
}

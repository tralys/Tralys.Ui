using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

internal static class Menus
{
    /// <summary>The focusable frame of the <paramref name="level"/>th open menu panel, the menu's own first.</summary>
    public static Node Panel(UiDriver driver, int level = 0) =>
        RootOf(driver.All<MenuPanel>()[level]);

    public static int Panels(UiDriver driver) => driver.All<MenuPanel>().Count;

    public static Node RootOf(Node component)
    {
        foreach (var child in component.Children)
            return child;
        throw new InvalidOperationException("The component rendered nothing.");
    }

    public static Callback Logs(Log log, string what) =>
        Callback.Of(log, what, static (l, w) => l.Add(w));

    public static Paint Background(Node row) => row.Visual.Background;

    public static Paint Hover => Themes.Dark.Color(ColorToken.StateHover);

    public static Paint Selected => Themes.Dark.Color(ComponentColor.RowBgSelected);

    public static bool Has(Node node, InputStates state) => (node.States & state) != 0;
}

/// <summary>A menu's controlled open, and what its rows reported.</summary>
public sealed partial class MenuBoard : ObservableObject
{
    public readonly Log Log = new();
    public readonly List<bool> Opens = [];
    public readonly List<bool> Checks = [];
    public readonly ObservableText Field = new();
    public Callback Command;

    public MenuBoard() => Text = "";

    [ObservableProperty]
    public partial bool Open { get; set; }

    [ObservableProperty]
    public partial bool Checked { get; set; }

    [ObservableProperty]
    public partial int Picked { get; set; }

    [ObservableProperty]
    public partial string Text { get; set; }

    public Callback<bool> OnOpenChange =>
        Callback<bool>.Of(
            this,
            static (b, open) =>
            {
                b.Opens.Add(open);
                b.Open = open;
            }
        );

    public Callback<bool> OnCheckedChange =>
        Callback<bool>.Of(this, static (b, value) => b.Checks.Add(value));
}

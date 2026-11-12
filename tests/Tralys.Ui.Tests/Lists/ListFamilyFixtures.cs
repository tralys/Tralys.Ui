using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

/// <summary>A list family test's controlled values, fed back as the app would, and what was reported.</summary>
public sealed partial class ListPick : ObservableObject
{
    public readonly List<string> Reports = [];
    public readonly List<string> Activated = [];
    public readonly List<SelectionChange> Toggled = [];
    public readonly SelectionSet Selection = new();
    public IItemsSource<string> Items = Words.Of("a", "b", "c", "d");

    [ObservableProperty]
    public partial UiKey Value { get; set; }

    [ObservableProperty]
    public partial bool Open { get; set; }

    public Callback<UiKey> OnValueChange =>
        Callback<UiKey>.Of(
            this,
            static (p, key) =>
            {
                p.Reports.Add(key.ToString());
                p.Value = key;
            }
        );

    public Callback<UiKey> OnActivate =>
        Callback<UiKey>.Of(this, static (p, key) => p.Activated.Add(key.ToString()));

    public Callback<SelectionChange> OnSelectionChange =>
        Callback<SelectionChange>.Of(
            this,
            static (p, change) =>
            {
                p.Toggled.Add(change);
                p.Selection.Apply(change, p.Items, Words.ByText);
            }
        );

    public Callback<bool> OnOpenChange =>
        Callback<bool>.Of(
            this,
            static (p, open) =>
            {
                p.Reports.Add(open ? "open" : "close");
                p.Open = open;
            }
        );

    public string Selected()
    {
        var keys = new List<string>();
        foreach (var key in Selection)
            keys.Add(key.ToString());
        keys.Sort(StringComparer.Ordinal);
        return string.Join(" ", keys);
    }
}

internal static class Words
{
    public static readonly KeyOf<string> ByText = static text => text;

    public static readonly TextOf<string> Text = static text => text;

    public static ObservableList<string> List(params string[] words)
    {
        var list = new ObservableList<string>(words.Length);
        foreach (var word in words)
            list.Add(word);
        return list;
    }

    public static IItemsSource<string> Of(params string[] words) => ItemsSource.From(List(words));

    public static ObservableList<string> Numbered(int count)
    {
        var list = new ObservableList<string>(count);
        for (var i = 0; i < count; i++)
            list.Add(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return list;
    }
}

internal static class Rows
{
    public static Paint Dark(ColorToken token) => Themes.Dark.Color(token);

    public static Paint Dark(ComponentColor token) => Themes.Dark.Color(token);

    /// <summary>The node a list flags for the row showing <paramref name="text"/>.</summary>
    public static Node Of(UiDriver driver, string text)
    {
        for (Node? n = driver.GetText(text); n is not null; n = n.Parent)
        {
            if (n.RowFocusable)
                return n;
        }
        throw new InvalidOperationException($"'{text}' is in no row.");
    }

    public static bool IsSelected(UiDriver driver, string text) =>
        (Of(driver, text).States & InputStates.Selected) != 0;

    public static Node List(UiDriver driver) => Nodes.Find(driver.Root, NodeKind.VirtualList);

    /// <summary>The texts the realized rows show, in order, spares left out.</summary>
    public static List<string> Shown(UiDriver driver)
    {
        var texts = new List<string>();
        Collect(List(driver), texts);
        return texts;
    }

    static void Collect(Node node, List<string> texts)
    {
        foreach (var child in node.Children)
        {
            if (child.SpareRow)
                continue;
            if (child.Kind == NodeKind.Text)
                texts.Add(child.Text.ToString());
            Collect(child, texts);
        }
    }
}

/// <summary>Items that can be put back in reverse, as one reset, or matched against a filter.</summary>
internal sealed class Shelf : IItemsSource<string>
{
    readonly List<string> _items = [];
    ItemsSourceListeners _listeners;
    Func<string, bool>? _filter;
    readonly List<string> _all = [];

    public Shelf(int count)
    {
        for (var i = 0; i < count; i++)
            _all.Add(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Apply();
    }

    public int Count => _items.Count;

    public object? this[int index] => _items[index];

    public string ItemAt(int index) => _items[index];

    public int IndexOf(object? item) => item is string s ? _items.IndexOf(s) : -1;

    public void AddListener(IItemsSourceListener listener) => _listeners.Add(listener);

    public void RemoveListener(IItemsSourceListener listener) => _listeners.Remove(listener);

    public void Reverse()
    {
        _all.Reverse();
        Apply();
    }

    public void Filter(Func<string, bool>? filter)
    {
        _filter = filter;
        Apply();
    }

    void Apply()
    {
        _items.Clear();
        foreach (var item in _all)
        {
            if (_filter is null || _filter(item))
                _items.Add(item);
        }
        _listeners.Raise(this, ItemsChange.Reset);
    }
}

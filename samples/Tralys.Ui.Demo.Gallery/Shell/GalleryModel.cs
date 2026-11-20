using System.Globalization;
using System.Reflection;
using Tralys.Ui.Components;
using Tralys.Ui.Docking;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery;

/// <summary>A row of the gallery's tree: a category, a component with several stories, or a story.</summary>
public sealed class GalleryEntry(int id, string label, int story, List<GalleryEntry> children)
    : TreeRow
{
    public static readonly KeyOf<GalleryEntry> ById = static e => e.Id;

    public static readonly TextOf<GalleryEntry> Named = static e => e.Label;

    public static readonly Callback<TreeExpandedChange<GalleryEntry>> Expand = Callback<
        TreeExpandedChange<GalleryEntry>
    >.Of(static change => change.Row.IsExpanded = change.Expanded);

    public int Id { get; } = id;

    public string Label { get; } = label;

    public int Story { get; } = story;

    public List<GalleryEntry> Children { get; } = children;

    public string? Match { get; init; }

    /// <summary>How many stories a category row holds; 0 on the rows beneath one.</summary>
    public int Count { get; set; }
}

/// <summary>The gallery a host shows with <see cref="GalleryShell.Root"/>: every story by category, searchable, beside
/// the one picked, with menus for the theme and density.</summary>
public sealed partial class GalleryModel : ObservableObject, ITreeChildren<GalleryEntry>
{
    public static readonly string[] ThemeNames = TitleCased(Themes.All);
    public static readonly string[] DensityNames = ["Pointer", "Touch"];
    public static readonly string[] StateNames = Enum.GetNames<StoryState>();

    /// <summary>The Tralys.Ui repository's address, from the library's <c>RepositoryUrl</c> metadata.</summary>
    public static readonly string Repository = RepositoryOf(typeof(Story).Assembly);

    static readonly string Built =
        typeof(Story)
            .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "";

    /// <summary>The library's version, without the commit it was built from.</summary>
    public static readonly string Version = Built.Split('+')[0];

    static readonly string Commit = Built.Contains('+', StringComparison.Ordinal)
        ? Built[(Built.IndexOf('+', StringComparison.Ordinal) + 1)..]
        : "";

    const int CategoryIds = -1;
    const int ComponentIds = -1000;
    const int FoundIds = -100000;

    readonly List<GalleryEntry> _folded = [];
    readonly List<GalleryEntry> _found = [];
    readonly GalleryEntry[] _categoryOf;
    readonly GalleryEntry?[] _componentOf;
    readonly GalleryEntry[] _entryOf;
    readonly string?[] _sources;
    readonly List<int> _shownStories = [];
    List<GalleryEntry> _roots;

    /// <param name="story">The key of the story shown first, ignoring case; the first story when it names
    /// none.</param>
    /// <param name="states">Shows the story held in each <see cref="StoryState"/> from the start.</param>
    /// <param name="windows">The windows a story's docks tear panels out into; floats of their own without
    /// them.</param>
    public GalleryModel(string? story, bool states, IDockWindowHost? windows)
    {
        Stories = Ordered();
        _categoryOf = new GalleryEntry[Stories.Length];
        _componentOf = new GalleryEntry?[Stories.Length];
        _entryOf = new GalleryEntry[Stories.Length];
        _sources = new string?[Stories.Length];
        Fold();
        _roots = _folded;
        Rows = new TreeRows<GalleryEntry>(this);
        Windows = windows;
        States = states;
        Query.Changed += Find;
        Picked = Callback<UiKey>.Of(this, static (m, key) => m.Pick(key));
        Submitted = Callback.Of(this, static m => m.PickFirstFound());
        Cleared = Callback.Of(this, static m => m.Query.Clear());
        CopyKey = Callback.Of(this, static m => m.Copy?.Invoke(m.Stories[m.Shown].Key));
        OpenRepository = Callback.Of(this, static m => m.OpenLink?.Invoke(Repository));
        OpenSource = Callback.Of(this, static m => m.ShowSource());
        Previous = Callback.Of(this, static m => m.Step(-1));
        Next = Callback.Of(this, static m => m.Step(1));
        Keys = Handler<KeyEvent>.Of(this, static (m, ref e) => m.KeyDown(ref e));
        ThemePicks = Picks(
            ThemeNames.Length,
            static (m, i) => m.View?.Context.Theme = Themes.All[i]
        );
        DensityPicks = Picks(
            DensityNames.Length,
            static (m, i) => m.View?.Context.Density = (Density)i
        );
        Select(IndexOf(story));
        Rows.Refresh();
    }

    public Story[] Stories { get; }

    public TreeRows<GalleryEntry> Rows { get; }

    public IDockWindowHost? Windows { get; }

    /// <summary>Where the bar's copy button puts the shown story's key: the host's clipboard.</summary>
    public Action<string>? Copy { get; set; }

    /// <summary>Where the gallery's links go, such as the repository and a story's source: the host's browser. The link
    /// buttons show only while it is set.</summary>
    public Action<string>? OpenLink { get; set; }

    public bool Linked => OpenLink is not null && Repository.Length > 0;

    public UiView? View { get; set; }

    public NodeRef Search { get; } = new();

    [ObservableProperty]
    public partial int Shown { get; private set; }

    public ObservableText Query { get; } = new();

    [ObservableProperty]
    public partial bool States { get; set; }

    public bool Missed => Query.Length > 0 && _found.Count == 0;

    public Callback<UiKey> Picked { get; }

    public Callback Submitted { get; }

    public Callback Cleared { get; }

    public Callback CopyKey { get; }

    public Callback OpenRepository { get; }

    public Callback OpenSource { get; }

    public Callback Previous { get; }

    public Callback Next { get; }

    public Handler<KeyEvent> Keys { get; }

    public Callback<bool>[] ThemePicks { get; }

    public Callback<bool>[] DensityPicks { get; }

    public int ChildCount(GalleryEntry? parent) => (parent?.Children ?? _roots).Count;

    public GalleryEntry ChildAt(GalleryEntry? parent, int index) =>
        (parent?.Children ?? _roots)[index];

    public void Select(int index)
    {
        Shown = index;
        _categoryOf[index].IsExpanded = true;
        _componentOf[index]?.IsExpanded = true;
    }

    public void Step(int by)
    {
        if (_shownStories.Count == 0)
            return;
        var at = _shownStories.IndexOf(Shown);
        var next = at < 0 ? (by > 0 ? 0 : _shownStories.Count - 1) : at + by;
        Select(_shownStories[Math.Clamp(next, 0, _shownStories.Count - 1)]);
    }

    internal void Reveal(ScrollHandle scroll)
    {
        var row = Rows.IndexOf(RowOf(Shown));
        if (row >= 0)
            scroll.ScrollToIndex(row, ScrollAlign.Nearest);
    }

    GalleryEntry? RowOf(int story)
    {
        if (Query.Length == 0)
            return _entryOf[story];
        foreach (var category in _found)
        {
            foreach (var entry in category.Children)
            {
                if (entry.Story == story)
                    return entry;
            }
        }
        return null;
    }

    void ShowSource()
    {
        if (Stories[Shown].SourcePath is not null)
            OpenLink?.Invoke(_sources[Shown] ??= SourceOf(Stories[Shown]));
    }

    [ColdPath]
    static string SourceOf(Story story)
    {
        var file = string.Join('/', story.SourcePath!.Split('/').Select(Uri.EscapeDataString));
        var at = Commit.Length > 0 ? Commit : "HEAD";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Repository}/blob/{at}/{file}#L{story.SourceLine}"
        );
    }

    static string RepositoryOf(Assembly assembly)
    {
        foreach (var data in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (data.Key == "RepositoryUrl" && data.Value is { } url)
                return url.TrimEnd('/');
        }
        return "";
    }

    int IndexOf(string? key)
    {
        for (var i = 0; i < Stories.Length; i++)
        {
            if (string.Equals(Stories[i].Key, key, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 0;
    }

    // Arrow keys report the rows they reach too, so a parent opens only by the tree's own disclosure.
    void Pick(UiKey key)
    {
        if (key.TryGetLong(out var id) && id >= 0)
            Select((int)id);
    }

    void Find()
    {
        var query = Query.ToString();
        _found.Clear();
        _shownStories.Clear();
        if (query.Length == 0)
        {
            _roots = _folded;
            for (var i = 0; i < Stories.Length; i++)
                _shownStories.Add(i);
        }
        else
        {
            GalleryEntry? category = null;
            for (var i = 0; i < Stories.Length; i++)
            {
                var key = Stories[i].Key;
                if (!key.Contains(query, StringComparison.OrdinalIgnoreCase))
                    continue;
                var name = Stories[i].Category;
                if (category?.Label != name)
                {
                    category = new GalleryEntry(FoundIds - _found.Count, name, -1, [])
                    {
                        IsExpanded = true,
                    };
                    _found.Add(category);
                }
                category.Children.Add(new GalleryEntry(i, key, i, []) { Match = query });
                category.Count++;
                _shownStories.Add(i);
            }
            _roots = _found;
        }
        Rows.Refresh();
    }

    void PickFirstFound()
    {
        if (Query.Length > 0 && _shownStories.Count > 0)
            Select(_shownStories[0]);
    }

    void KeyDown(ref KeyEvent e)
    {
        if (e.Modifiers != ModifierKeys.None)
            return;
        switch (e.Key)
        {
            case Key.Down:
                Step(1);
                e.PreventDefault();
                break;
            case Key.Up:
                Step(-1);
                e.PreventDefault();
                break;
            case Key.OemQuestion:
                Search.Focus(visible: true);
                e.PreventDefault();
                break;
        }
    }

    void Fold()
    {
        var i = 0;
        while (i < Stories.Length)
        {
            var name = Stories[i].Category;
            var category = new GalleryEntry(CategoryIds - _folded.Count, name, -1, []);
            _folded.Add(category);
            while (i < Stories.Length && Stories[i].Category == name)
            {
                var component = ComponentOf(Stories[i]);
                var end = i + 1;
                while (end < Stories.Length && ComponentOf(Stories[end]).SequenceEqual(component))
                    end++;
                if (end - i == 1)
                {
                    _entryOf[i] = new GalleryEntry(i, component.ToString(), i, []);
                    category.Children.Add(_entryOf[i]);
                }
                else
                {
                    var parent = new GalleryEntry(ComponentIds - i, component.ToString(), -1, []);
                    category.Children.Add(parent);
                    for (var story = i; story < end; story++)
                    {
                        var key = Stories[story].Key;
                        _entryOf[story] = new GalleryEntry(
                            story,
                            key[(key.LastIndexOf('.') + 1)..],
                            story,
                            []
                        );
                        parent.Children.Add(_entryOf[story]);
                        _componentOf[story] = parent;
                    }
                }
                for (var story = i; story < end; story++)
                    _categoryOf[story] = category;
                category.Count += end - i;
                i = end;
            }
        }
        for (var story = 0; story < Stories.Length; story++)
            _shownStories.Add(story);
    }

    public static ReadOnlySpan<char> ComponentOf(Story story) =>
        story.Key.AsSpan(0, story.Key.LastIndexOf('.'));

    public static ReadOnlySpan<char> NameOf(Story story) =>
        story.Key.AsSpan(story.Key.LastIndexOf('.') + 1);

    static Story[] Ordered()
    {
        var stories = StoryRegistry.All.ToArray();
        Array.Sort(
            stories,
            static (a, b) =>
            {
                var byCategory = string.CompareOrdinal(a.Category, b.Category);
                if (byCategory != 0)
                    return byCategory;
                var byComponent = ComponentOf(a)
                    .CompareTo(ComponentOf(b), StringComparison.Ordinal);
                return byComponent != 0
                    ? byComponent
                    : NameOf(a).CompareTo(NameOf(b), StringComparison.Ordinal);
            }
        );
        return stories;
    }

    Callback<bool>[] Picks(int count, Action<GalleryModel, int> pick)
    {
        var picks = new Callback<bool>[count];
        for (var i = 0; i < count; i++)
            picks[i] = Callback<bool>.Of(new Choice(this, i, pick), static (c, _) => c.Make());
        return picks;
    }

    static string[] TitleCased(IReadOnlyList<Theme> themes)
    {
        var names = new string[themes.Count];
        for (var i = 0; i < names.Length; i++)
            names[i] = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(themes[i].Name);
        return names;
    }

    sealed class Choice(GalleryModel model, int index, Action<GalleryModel, int> pick)
    {
        public void Make() => pick(model, index);
    }
}

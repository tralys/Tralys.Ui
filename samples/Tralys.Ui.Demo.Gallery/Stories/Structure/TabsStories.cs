using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Tabs>("Structure")]
static class TabsStories
{
    static readonly string[] Many = ["Tab 1", "Tab 2", "Tab 3", "Tab 4", "Tab 5", "Tab 6"];

    static readonly string[] ManyContent =
    [
        "Content 1",
        "Content 2",
        "Content 3",
        "Content 4",
        "Content 5",
        "Content 6",
    ];

    static readonly (string Name, string Body)[] Sections =
    [
        ("Tasks", "Three tasks are open"),
        ("Reviews", "No review is waiting"),
        ("Releases", "Two releases wait to ship"),
    ];

    static readonly Slot Reviews = Slot.Of(static ui =>
    {
        using (ui.Box(align: Align.Center, gap: 8))
        {
            ui.Text("Reviews");
            ui.Badge(count: 4);
        }
    });

    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Tabs(width: 340))
        {
            using (ui.TabsList())
            {
                ui.TabsTab(value: "Overview", text: "Overview");
                ui.TabsTab(value: "Stats", text: "Stats");
                ui.TabsTab(value: "History", text: "History");
            }
            Panel(ui, "Overview", "Overview content");
            Panel(ui, "Stats", "Stats content");
            Panel(ui, "History", "History content");
        }
    }

    [FramePath]
    public static void Reorder(Ui ui)
    {
        var order = ui.Remember(static () => StoryTabs.Of(["Overview", "Stats", "History"]));
        using (ui.Tabs(width: 340, reorderable: true, onReorder: StoryTabs.Moves(order)))
        {
            using (ui.TabsList())
                foreach (var name in order)
                    ui.TabsTab(value: name, text: name, key: name);
            Panel(ui, "Overview", "Drag a tab past its neighbour to move it");
            Panel(ui, "Stats", "Stats content");
            Panel(ui, "History", "History content");
        }
    }

    [FramePath]
    public static void TwoStrips(Ui ui)
    {
        var strips = ui.Remember(static () => new StoryStrips());
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 12))
        {
            Strip(ui, strips, strips.Left, "left");
            Strip(ui, strips, strips.Right, "right");
        }
    }

    [FramePath]
    public static void ManyTabs(Ui ui)
    {
        using (ui.Tabs(width: 400))
        {
            using (ui.TabsList())
                for (var i = 0; i < Many.Length; i++)
                    ui.TabsTab(value: Many[i], text: Many[i], key: i);
            for (var i = 0; i < Many.Length; i++)
                Panel(ui, Many[i], ManyContent[i], key: i);
        }
    }

    [FramePath]
    public static void Sizes(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 16))
        {
            using (ui.Tabs(width: 360, size: ComponentSize.Sm))
            using (ui.TabsList())
            {
                ui.TabsTab(value: "Documents", text: "Documents");
                ui.TabsTab(value: "Templates", text: "Templates");
                ui.TabsTab(value: "Locked", text: "Locked", disabled: true);
            }
            using (ui.Tabs(width: 360))
            using (ui.TabsList())
            {
                ui.TabsTab(value: "Documents", text: "Documents");
                ui.TabsTab(value: "Templates", text: "Templates");
            }
            using (ui.Tabs(width: 360, size: ComponentSize.Lg))
            using (ui.TabsList())
            {
                ui.TabsTab(value: "Documents", text: "Documents");
                ui.TabsTab(value: "Templates", text: "Templates");
            }
        }
    }

    [FramePath]
    public static void Bound(Ui ui)
    {
        using (ui.Tabs(width: 340, defaultValue: Sections[1].Name))
        {
            using (ui.TabsList())
                foreach (var (name, _) in Sections)
                    ui.TabsTab(value: name, text: name, key: name);
            foreach (var (name, body) in Sections)
                Panel(ui, name, body, key: name);
        }
    }

    [FramePath]
    public static void TabContent(Ui ui)
    {
        using (ui.Tabs(width: 340, defaultValue: "Reviews"))
        {
            using (ui.TabsList())
            {
                ui.TabsTab(value: "Profile", text: "Profile");
                ui.TabsTab(value: "Reviews", content: Reviews);
            }
            Panel(ui, "Profile", "Your public profile");
            Panel(ui, "Reviews", "Reviews waiting for you");
        }
    }

    [FramePath]
    static void Panel(
        Ui ui,
        string value,
        string text,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.TabsPanel(value: value, key: key, file: file, line: line))
            ui.Text(text, fontWeight: ui.Theme.Weight(WeightToken.Interface), margin: 8);
    }

    [FramePath]
    static void Strip(
        Ui ui,
        StoryStrips strips,
        ObservableList<string> tabs,
        string name,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Tabs(
                width: 340,
                reorderable: true,
                dragGroup: strips.Group,
                groupKey: name,
                onReorder: StoryTabs.Moves(tabs),
                onTabMove: strips.Move,
                key: name,
                file: file,
                line: line
            )
        )
        {
            using (ui.TabsList())
                foreach (var tab in tabs)
                    ui.TabsTab(value: tab, text: tab, key: tab);
            foreach (var tab in tabs)
                Panel(ui, tab, StoryStrips.Body(tab), key: tab);
        }
    }
}

[Stories<Tabs.Tab>("Structure")]
static class TabsTabStories
{
    static readonly Slot Mentions = Slot.Of(static ui =>
    {
        using (ui.Box(align: Align.Center, gap: 4))
        {
            ui.Text("Mentions");
            ui.Badge(count: 3);
        }
    });

    [FramePath]
    public static void States(Ui ui)
    {
        var t = ui.Theme;
        using (ui.Box())
        {
            using (ui.Tabs(value: "Active"))
            using (ui.TabsList())
            {
                ui.TabsTab(value: "Resting", text: "Resting");
                ui.TabsTab(value: "Active", text: "Active");
            }
            using (ui.Tabs(value: "Own accent"))
            using (ui.TabsList())
                ui.TabsTab(
                    value: "Own accent",
                    text: "Own accent",
                    accent: t.Color(ColorToken.BgDanger)
                );
            using (ui.Tabs())
            using (ui.TabsList())
                ui.TabsTab(value: "Disabled", text: "Disabled", disabled: true);
        }
    }

    [FramePath]
    public static void Strip(Ui ui)
    {
        using (ui.Tabs(value: "Talk", size: ComponentSize.Sm))
        using (ui.TabsList())
        {
            ui.TabsTab(value: "All", text: "All");
            ui.TabsTab(value: "Talk", text: "Talk");
            ui.TabsTab(value: "Team", text: "Team");
            ui.TabsTab(value: "Disabled", text: "Disabled", disabled: true);
        }
    }

    [FramePath]
    public static void Accent(Ui ui)
    {
        var t = ui.Theme;
        using (ui.Box())
        {
            using (ui.Tabs(value: "Team"))
            using (ui.TabsList())
                ui.TabsTab(value: "Team", text: "Team", accent: t.Color(ColorToken.BgInfo));
            using (ui.Tabs(value: "Mentions"))
            using (ui.TabsList())
                ui.TabsTab(
                    value: "Mentions",
                    content: Mentions,
                    accent: t.Color(ColorToken.BgSuccess)
                );
        }
    }
}

static class StoryTabs
{
    public static ObservableList<string> Of(ReadOnlySpan<string> names)
    {
        var tabs = new ObservableList<string>(names.Length);
        foreach (var name in names)
            tabs.Add(name);
        return tabs;
    }

    [FramePath]
    public static Callback<TabReorder> Moves(ObservableList<string> tabs) =>
        Callback<TabReorder>.Of(tabs, static (t, r) => t.Move(r.From, r.To));
}

sealed class StoryStrips
{
    public readonly TabDragGroup Group = new();
    public readonly ObservableList<string> Left = StoryTabs.Of(["Inbox", "Drafts"]);
    public readonly ObservableList<string> Right = StoryTabs.Of(["Sent", "Archive"]);
    public readonly Callback<TabMove> Move;

    public StoryStrips() => Move = Callback<TabMove>.Of(this, static (s, m) => s.Moved(m));

    [FramePath]
    public static string Body(string tab) =>
        tab switch
        {
            "Inbox" => "Drag a tab onto the other strip",
            "Drafts" => "Drafts content",
            "Sent" => "Sent content",
            _ => "Archive content",
        };

    [FramePath]
    void Moved(TabMove move)
    {
        if (!move.Value.TryGetString(out var tab) || tab is null)
            return;
        var from = Left.Contains(tab) ? Left : Right;
        var to = move.Target == (UiKey)"left" ? Left : Right;
        from.Remove(tab);
        to.Insert(Math.Min(move.Gap, to.Count), tab);
    }
}

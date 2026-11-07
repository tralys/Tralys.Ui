using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Fonts;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery;

public readonly record struct GalleryShellProps
{
    public GalleryModel? Model { get; init; }
}

/// <summary>The gallery's sidebar of stories, its bar naming the shown story with the theme and density menus, and
/// its stage showing the story, which holds the dialogs, menus and popups the story opens.</summary>
public sealed class GalleryShell : Component<GalleryShellProps>
{
    internal const float SidebarWidth = 260f;
    internal const float BarHeight = 44f;

    /// <summary>The gallery across a whole view, as a view mounts it: <c>view.Mount(Slot.Of(model,
    /// GalleryShell.Root))</c>.</summary>
    public static readonly Action<Ui, GalleryModel> Root = static (ui, model) =>
    {
        using (ui.Box(grow: 1, basis: 0, onKeyDown: model.Keys))
            ui.GalleryShell(model: model, grow: 1);
    };

    protected override void Render(Ui ui, in GalleryShellProps p)
    {
        var model = p.Model!;
        var t = ui.Theme;
        ui.Effect(model, 0, static (m, _, cx) => m.View = cx.View);
        ui.Provide(StoryWindows.Key, new StoryWindows(model.Windows));
        using (
            ui.Box(
                background: t.Color(ColorToken.Bg),
                color: t.Color(ColorToken.Fg),
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            ui.GallerySidebar(model: model);
            using (ui.Box(direction: Direction.Column, grow: 1, basis: 0, minWidth: 0))
            {
                ui.GalleryBar(model: model);
                ui.GalleryStage(model: model, grow: 1, basis: 0);
            }
        }
    }
}

readonly record struct GalleryPartProps
{
    public GalleryModel? Model { get; init; }
}

sealed class GallerySidebar : Component<GalleryPartProps>
{
    static readonly Slot<GalleryEntry> Row = Slot<GalleryEntry>.Of(
        static (ui, entry, _) =>
        {
            if (entry.Match is null)
                ui.TreeItem(text: entry.Label);
            else
                ui.TreeItem(content: Slot.Of(entry, static (ui, e) => Matched(ui, e)));
        }
    );

    static readonly Slot SearchKey = Slot.Of(static ui => ui.Kbd(text: "/"));

    protected override void Render(Ui ui, in GalleryPartProps p)
    {
        var model = p.Model!;
        var t = ui.Theme;
        using (
            ui.Box(
                direction: Direction.Column,
                background: t.Color(ColorToken.BgSunken),
                border: new Border(new Thickness(0f, 0f, 1f, 0f), t.Color(ColorToken.Border)),
                width: GalleryShell.SidebarWidth
            )
        )
        {
            using (ui.Box(direction: Direction.Column, padding: 12))
                ui.SearchInput(
                    text: model.Query,
                    onSubmit: model.Submitted,
                    placeholder: "Find a story",
                    trailing: model.Query.Length == 0 ? SearchKey : default,
                    nodeRef: model.Search
                );
            if (model.Missed)
            {
                using (
                    ui.Box(
                        direction: Direction.Column,
                        gap: 8,
                        align: Align.Center,
                        margin: new Thickness(12f, 32f, 12f, 0f)
                    )
                )
                {
                    ui.EmptyState(
                        noMatchText: ui.Format($"No story matches “{model.Query.Span}”"),
                        searchMiss: true
                    );
                    ui.Button(
                        text: "Clear search",
                        scheme: ColorScheme.Primary,
                        variant: ComponentVariant.Text,
                        onClick: model.Cleared
                    );
                }
                return;
            }
            using (
                ui.Box(
                    direction: Direction.Column,
                    grow: 1,
                    basis: 0,
                    padding: new Thickness(8f, 0f, 8f, 8f)
                )
            )
                ui.Tree(
                    items: model.Rows,
                    itemKey: GalleryEntry.ById,
                    itemText: GalleryEntry.Named,
                    item: Row,
                    value: model.Shown,
                    onValueChange: model.Picked,
                    onExpandedChange: GalleryEntry.Expand,
                    size: ComponentSize.Xs,
                    grow: 1
                );
        }
    }

    [FramePath]
    static void Matched(
        Ui ui,
        GalleryEntry entry,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Text(
            entry.Label,
            highlight: entry.Match,
            highlightColor: ui.Theme.Color(ColorToken.FgAccent),
            trimming: TextTrimming.CharacterEllipsis,
            file: file,
            line: line
        );
}

sealed class GalleryBar : Component<GalleryPartProps>
{
    static readonly IconGlyph[] DensityIcons = [Icons.MousePointer, Icons.Smartphone];

    static readonly Slot<GalleryModel> ThemeMenu = Slot<GalleryModel>.Of(
        static (ui, model, _) =>
            Choices(ui, GalleryModel.ThemeNames, ThemeIndex(ui), model.ThemePicks)
    );

    static readonly Slot<GalleryModel> DensityMenu = Slot<GalleryModel>.Of(
        static (ui, model, _) =>
            Choices(ui, GalleryModel.DensityNames, (int)ui.Density, model.DensityPicks)
    );

    protected override void Render(Ui ui, in GalleryPartProps p)
    {
        var model = p.Model!;
        var t = ui.Theme;
        var story = model.Stories[model.Shown];
        var density = (int)ui.Density;
        using (
            ui.Box(
                align: Align.Center,
                height: GalleryShell.BarHeight,
                padding: new Thickness(24f, 0f, 12f, 0f),
                border: new Border(new Thickness(0f, 0f, 0f, 1f), t.Color(ColorToken.Border))
            )
        )
        {
            using (ui.Box(gap: 8, align: Align.Center, grow: 1))
            {
                ui.Text(
                    ui.Format($"{GalleryModel.ComponentOf(story)}"),
                    color: t.Color(ColorToken.FgMuted)
                );
                ui.Text(ui.Format($"{GalleryModel.NameOf(story)}"), variant: TextVariant.Heading);
                using (ui.Tooltip(text: "Copy the story's key"))
                    ui.IconButton(
                        icon: Icons.Copy,
                        scheme: ColorScheme.Secondary,
                        variant: ComponentVariant.Ghost,
                        size: ComponentSize.Sm,
                        onClick: model.CopyKey
                    );
            }
            using (ui.Box(gap: 4, align: Align.Center))
            {
                ui.MenuButton(
                    text: GalleryModel.ThemeNames[ThemeIndex(ui)],
                    iconLeft: Icons.Droplet,
                    chevron: true,
                    scheme: ColorScheme.Secondary,
                    variant: ComponentVariant.Ghost,
                    size: ComponentSize.Sm,
                    menuSize: ComponentSize.Sm,
                    menu: Slot.Of(
                        model,
                        static (ui, m) =>
                            Choices(ui, GalleryModel.ThemeNames, ThemeIndex(ui), m.ThemePicks)
                    )
                );
                ui.MenuButton(
                    text: GalleryModel.DensityNames[density],
                    iconLeft: DensityIcons[density],
                    chevron: true,
                    scheme: ColorScheme.Secondary,
                    variant: ComponentVariant.Ghost,
                    size: ComponentSize.Sm,
                    menuSize: ComponentSize.Sm,
                    menu: Slot.Of(
                        model,
                        static (ui, m) =>
                            Choices(ui, GalleryModel.DensityNames, (int)ui.Density, m.DensityPicks)
                    )
                );
                using (ui.Tooltip(text: "Show every state"))
                    ui.Toggle(
                        value: model.States,
                        onValueChange: model.SetStates,
                        icon: Icons.Layers,
                        activeScheme: ColorScheme.Primary,
                        size: ComponentSize.Sm
                    );
            }
        }
    }

    [FramePath]
    static int ThemeIndex(Ui ui)
    {
        var themes = Themes.All;
        for (var i = 0; i < themes.Count; i++)
        {
            if (themes[i] == ui.Theme.Theme)
                return i;
        }
        return 0;
    }

    [FramePath]
    static void Choices(
        Ui ui,
        string[] names,
        int chosen,
        Callback<bool>[] picks,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Fragment(file: file, line: line))
        {
            for (var i = 0; i < names.Length; i++)
                ui.MenuItem(
                    text: names[i],
                    checkable: true,
                    @checked: i == chosen,
                    onCheckedChange: picks[i],
                    key: i
                );
        }
    }
}

sealed class GalleryStage : Component<GalleryPartProps>
{
    protected override void Render(Ui ui, in GalleryPartProps p)
    {
        var model = p.Model!;
        var story = model.Stories[model.Shown];
        if (!model.States)
        {
            // A story pads itself by 16, so it lands 24 in.
            using (
                ui.Box(
                    direction: Direction.Column,
                    align: Align.Start,
                    padding: 8,
                    containsLayers: true
                )
            )
                ui.GalleryStory(story: story, key: story.Key);
            return;
        }

        var t = ui.Theme;
        using (ui.Box(direction: Direction.Column, padding: 24, minHeight: 0, containsLayers: true))
        using (ui.ScrollArea(horizontal: ScrollbarVisibility.Disabled, grow: 1))
        using (ui.Box(wrap: FlexWrap.Wrap, align: Align.Start))
        {
            for (var i = 0; i < model.Columns.Length; i++)
            {
                using (
                    ui.Box(
                        direction: Direction.Column,
                        gap: 8,
                        align: Align.Start,
                        margin: new Thickness(0f, 0f, 24f, 24f),
                        nodeRef: model.Columns[i],
                        key: i
                    )
                )
                {
                    ui.Text(
                        GalleryModel.StateNames[i],
                        variant: TextVariant.Caption,
                        color: t.Color(ColorToken.FgMuted)
                    );
                    ui.GalleryStory(story: story, key: story.Key);
                }
            }
        }
    }
}

readonly record struct GalleryStoryProps
{
    public Story Story { get; init; }
}

sealed class GalleryStory : Component<GalleryStoryProps>
{
    protected override void Render(Ui ui, in GalleryStoryProps p) => p.Story.Render(ui);
}

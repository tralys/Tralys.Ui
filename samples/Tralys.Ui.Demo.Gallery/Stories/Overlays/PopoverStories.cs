using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Popover>("Overlays")]
static class PopoverStories
{
    static readonly Slot Above = Slot.Of(static ui =>
        StoryText.Plain(ui, "Above the trigger, centred")
    );

    static readonly Slot Below = Slot.Of(static ui =>
        StoryText.Plain(ui, "Below, right edges lined up")
    );

    static readonly Slot Left = Slot.Of(static ui =>
        StoryText.Plain(ui, "Left, tops lined up, a small frame")
    );

    static readonly Slot Right = Slot.Of(static ui =>
        StoryText.Plain(ui, "Right, tops lined up, a large frame")
    );

    static readonly Slot Rich = Slot.Of(static ui =>
    {
        using (ui.Box(direction: Direction.Column, gap: 4))
        {
            ui.Text(
                "Rich tooltip",
                variant: TextVariant.Heading,
                fontWeight: ui.Theme.Weight(WeightToken.Interface),
                wrap: true
            );
            ui.Text(
                "Held until tooltips show",
                color: ui.Theme.Color(ColorToken.FgMuted),
                fontWeight: ui.Theme.Weight(WeightToken.Interface),
                wrap: true
            );
        }
    });

    static readonly Slot Rows = Slot.Of(static ui =>
    {
        using (ui.Box(direction: Direction.Column, gap: 4, width: 180))
        {
            using (ui.PopoverClose())
                ui.StoryRow(text: "Closes the popup");
            using (ui.PopoverClose())
                ui.StoryRow(text: "So does this one");
            ui.StoryRow(text: "This one keeps it open", muted: true);
        }
    });

    [FramePath]
    public static void Placements(Ui ui)
    {
        using (ui.Box(width: 480, height: 240, align: Align.Center, justify: Justify.Center))
        using (ui.Box(gap: 8))
        {
            using (ui.Tooltip(text: "Opens above"))
            using (ui.Popover(placement: PlacementMode.Top, content: Above))
            using (ui.PopoverTrigger())
                Face(ui, "Top");
            using (ui.Popover(placement: PlacementMode.BottomEnd, content: Below))
            using (ui.PopoverTrigger())
                Face(ui, "BottomEnd");
            using (
                ui.Popover(
                    placement: PlacementMode.LeftStart,
                    size: ComponentSize.Sm,
                    content: Left
                )
            )
            using (ui.PopoverTrigger())
                Face(ui, "LeftStart");
            using (ui.Tooltip(content: Rich))
            using (
                ui.Popover(
                    placement: PlacementMode.RightStart,
                    size: ComponentSize.Lg,
                    content: Right
                )
            )
            using (ui.PopoverTrigger())
                Face(ui, "RightStart");
        }
    }

    [FramePath]
    public static void CloseOnClick(Ui ui)
    {
        using (ui.Box(width: 320, height: 200, align: Align.Start))
        using (ui.Popover(defaultOpen: true, content: Rows))
        using (ui.PopoverTrigger())
            ui.Text("Menu");
    }

    [FramePath]
    static void Face(
        Ui ui,
        string text,
        UiKey key = default,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        using (
            ui.Box(
                padding: new Thickness(8, 4, 8, 4),
                background: t.Color(ColorToken.Bg),
                border: new Border(1f, t.Color(ColorToken.Border)),
                radius: t.Radius(RadiusToken.Md),
                key: key,
                file: file,
                line: line
            )
        )
            ui.Text(text, fontWeight: t.Weight(WeightToken.Interface));
    }
}

static class StoryText
{
    [FramePath]
    public static void Plain(
        Ui ui,
        string text,
        UiKey key = default,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    ) =>
        ui.Text(
            text,
            fontWeight: ui.Theme.Weight(WeightToken.Interface),
            wrap: true,
            key: key,
            file: file,
            line: line
        );
}

using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Collapsible>("Toggles")]
static class CollapsibleStories
{
    static readonly string[] Lines =
    [
        "Line 1",
        "Line 2",
        "Line 3",
        "Line 4",
        "Line 5",
        "Line 6",
        "Line 7",
        "Line 8",
    ];

    [FramePath]
    public static void Expanded(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 16,
                padding: 16,
                width: 312,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            using (ui.Collapsible(header: "Expanded section", defaultOpen: true))
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 16))
            {
                ui.Text("This content is visible");
                ui.Button(text: "Action inside expander", size: ComponentSize.Sm);
            }
            using (ui.Collapsible(header: "Collapsed section"))
                ui.Text("Hidden content");
        }
    }

    [FramePath]
    public static void TallContent(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 4,
                padding: 16,
                width: 312,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            using (ui.Collapsible(header: "Tall section"))
            using (ui.Box(direction: Direction.Column, gap: 4))
                foreach (var line in Lines)
                    ui.Text(line, key: line);
            ui.Text(
                "This text slides with the section above",
                color: ui.Theme.Color(ColorToken.FgMuted),
                wrap: true
            );
        }
    }

    [FramePath]
    public static void Disabled(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 4,
                padding: 16,
                width: 312,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            using (ui.Collapsible(header: "Locked section", disabled: true))
                ui.Text("Unreachable");
            using (ui.Collapsible(header: "Locked open", defaultOpen: true, disabled: true))
                ui.Text("Shown, but cannot be closed");
        }
    }

    [FramePath]
    public static void Nested(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                padding: 16,
                width: 312,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        using (ui.Collapsible(header: "Outer", defaultOpen: true))
        using (ui.Collapsible(header: "Inner", defaultOpen: true))
            ui.Text("Nested content");
    }

    [FramePath]
    public static void Up(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 4,
                padding: 16,
                width: 312,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Text(
                "Opens upward from its header",
                color: ui.Theme.Color(ColorToken.FgMuted),
                wrap: true
            );
            using (
                ui.Collapsible(
                    header: "Older messages",
                    defaultOpen: true,
                    direction: ExpandDirection.Up
                )
            )
            using (ui.Box(direction: Direction.Column, gap: 4))
            {
                ui.Text("Earlier line 1");
                ui.Text("Earlier line 2");
            }
        }
    }

    [FramePath]
    public static void Sideways(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 16,
                padding: 16,
                width: 352,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            using (
                ui.Collapsible(
                    header: "Filters",
                    defaultOpen: true,
                    direction: ExpandDirection.Right
                )
            )
            using (ui.Box(direction: Direction.Column, gap: 4))
            {
                ui.Text("Opens to the right");
                ui.Text("of its header");
            }
            using (
                ui.Collapsible(
                    header: "Details",
                    defaultOpen: true,
                    direction: ExpandDirection.Left
                )
            )
            using (ui.Box(direction: Direction.Column, gap: 4))
            {
                ui.Text("Opens to the left");
                ui.Text("of its header");
            }
            using (ui.Collapsible(header: "Closed", direction: ExpandDirection.Right))
                ui.Text("Hidden content");
        }
    }
}

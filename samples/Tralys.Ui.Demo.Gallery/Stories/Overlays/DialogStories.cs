using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Dialog>("Overlays")]
static class DialogStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var t = ui.Theme;
        var open = ui.State(true);
        using (ui.Box(direction: Direction.Column, gap: 16))
        {
            ui.Text(
                "A dialog is an overlay with a title and footer actions",
                color: t.Color(ColorToken.FgMuted),
                fontWeight: t.Weight(WeightToken.Interface),
                wrap: true
            );
            using (ui.Box(width: 480, height: 260, align: Align.Center, justify: Justify.Center))
                ui.Button(text: "Open dialog", onClick: StoryOpen.Opener(open));
            using (
                ui.Dialog(
                    open: open.Value,
                    onOpenChange: open.Setter,
                    onSubmit: StoryOpen.Closer(open)
                )
            )
            {
                ui.DialogTitle(text: "Confirm Action");
                ui.Text(
                    "Are you sure you want to continue? This cannot be undone.",
                    width: 280,
                    color: t.Color(ColorToken.FgMuted),
                    fontWeight: t.Weight(WeightToken.Interface),
                    wrap: true
                );
                using (ui.DialogFooter())
                {
                    ui.DialogClose(text: "Cancel");
                    ui.DialogSubmit(text: "Confirm");
                }
            }
        }
    }

    [FramePath]
    public static void Destructive(Ui ui)
    {
        var t = ui.Theme;
        var open = ui.State(true);
        using (ui.Box(width: 480, height: 260)) { }
        using (
            ui.Dialog(
                open: open.Value,
                onOpenChange: open.Setter,
                onSubmit: StoryOpen.Closer(open),
                width: t.Metrics(ComponentSize.Md).Measure
            )
        )
        {
            ui.DialogTitle(text: "Delete preset");
            ui.Text(
                "The Weekend preset is removed for every user on this PC. Enter deletes; Escape cancels.",
                color: t.Color(ColorToken.FgStrong),
                fontWeight: t.Weight(WeightToken.Interface),
                wrap: true
            );
            using (ui.DialogFooter())
            {
                ui.DialogClose(text: "Cancel");
                ui.DialogSubmit(text: "Delete", destructive: true);
            }
        }
    }

    [FramePath]
    public static void Connecting(Ui ui)
    {
        var t = ui.Theme;
        var open = ui.State(true);
        using (ui.Box(width: 480, height: 200)) { }
        using (ui.Dialog(open: open.Value, onOpenChange: open.Setter))
        {
            ui.DialogTitle(text: "Logging in");
            using (ui.Box(gap: 8))
            {
                StorySpinner.Draw(ui, t.Color(ColorToken.FgAccent));
                ui.Text(
                    "Connecting to the login server…",
                    width: 240,
                    color: t.Color(ColorToken.FgStrong),
                    fontWeight: t.Weight(WeightToken.Interface),
                    wrap: true
                );
            }
            using (ui.DialogFooter())
                ui.DialogClose(text: "Cancel");
        }
    }

    [FramePath]
    public static void NoFooter(Ui ui)
    {
        var t = ui.Theme;
        using (ui.Box(width: 480, height: 200)) { }
        using (ui.Dialog(open: true, width: t.Metrics(ComponentSize.Sm).Measure))
        {
            ui.DialogTitle(text: "Logging in");
            ui.Text(
                "With no actions, the footer collapses.",
                color: t.Color(ColorToken.FgMuted),
                fontWeight: t.Weight(WeightToken.Interface),
                wrap: true
            );
        }
    }
}

static class StoryOpen
{
    public static Callback Opener(State<bool> open) =>
        Callback.Of(open, static s => s.Value = true);

    public static Callback Closer(State<bool> open) =>
        Callback.Of(open, static s => s.Value = false);
}

static class StorySpinner
{
    [FramePath]
    public static void Draw(
        Ui ui,
        Color color,
        UiKey key = default,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    )
    {
        var size = ui.Theme.Metrics(ComponentSize.Md).IconSize;
        using (ui.Fragment(key: key, file: file, line: line))
        using (
            ui.Box(
                width: size,
                height: size,
                shrink: 0f,
                transform: Transform.Rotate(360f * ui.Loop(1f))
            )
        )
            ui.Icon(glyph: Icons.Loader, size: size, color: color);
    }
}

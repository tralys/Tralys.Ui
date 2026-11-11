using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Scrollbar>("Structure")]
static class ScrollbarStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var t = ui.Theme;
        var vertical = ui.State(40f);
        var horizontal = ui.State(60f);
        using (ui.Box(gap: 24))
        {
            using (ui.Box(direction: Direction.Column, gap: 16))
            {
                Caption(ui, "Vertical");
                ui.Scrollbar(
                    value: vertical.Value,
                    onValueChange: Follow(vertical),
                    max: 100,
                    viewportSize: 20,
                    height: 160,
                    alignSelf: AlignSelf.Center
                );
            }
            using (ui.Box(direction: Direction.Column, gap: 16))
            {
                Caption(ui, "Horizontal");
                ui.Scrollbar(
                    value: horizontal.Value,
                    onValueChange: Follow(horizontal),
                    max: 100,
                    viewportSize: 20,
                    orientation: Orientation.Horizontal,
                    width: 200,
                    alignSelf: AlignSelf.Start
                );
            }
        }
    }

    [FramePath]
    public static void Disabled(Ui ui)
    {
        var value = ui.State(40f);
        ui.Scrollbar(
            value: value.Value,
            onValueChange: Follow(value),
            max: 100,
            viewportSize: 20,
            height: 160,
            disabled: true
        );
    }

    [FramePath]
    public static void LongExtent(Ui ui)
    {
        var value = ui.State(40000f);
        ui.Scrollbar(
            value: value.Value,
            onValueChange: Follow(value),
            max: 100000,
            viewportSize: 20,
            height: 160
        );
    }

    static readonly string[] Captions =
    [
        "Half shown",
        "At the start",
        "At the end",
        "Shortest thumb",
        "Nothing to scroll",
    ];

    static readonly float[] Maxes = [100f, 100f, 100f, 1000f, 0f];

    static readonly float[] Viewports = [100f, 20f, 20f, 1f, 20f];

    static readonly float[] Starts = [0f, 0f, 100f, 500f, 0f];

    [FramePath]
    public static void Thumbs(Ui ui)
    {
        using (ui.Box(gap: 24))
            for (var i = 0; i < Captions.Length; i++)
            {
                var value = ui.State(Starts[i], key: i);
                using (ui.Box(direction: Direction.Column, gap: 16, key: i))
                {
                    Caption(ui, Captions[i]);
                    ui.Scrollbar(
                        value: value.Value,
                        onValueChange: Follow(value),
                        max: Maxes[i],
                        viewportSize: Viewports[i],
                        height: 160,
                        alignSelf: AlignSelf.Center
                    );
                }
            }
    }

    [FramePath]
    static void Caption(
        Ui ui,
        string text,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Text(
            text,
            color: ui.Theme.Color(ColorToken.FgMuted),
            fontWeight: ui.Theme.Weight(WeightToken.Interface),
            wrap: true,
            key: key,
            file: file,
            line: line
        );

    static Callback<ScrollbarChange> Follow(State<float> value) =>
        Callback<ScrollbarChange>.Of(value, static (v, change) => v.Value = change.Value);
}

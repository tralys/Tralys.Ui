using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.DevTools;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<InspectorPanel>("DevTools")]
static class InspectorPanelStories
{
    static readonly Slot Profile = Slot.Of(static ui =>
    {
        var t = ui.Theme;
        using (ui.Box(direction: Direction.Column, gap: 12, padding: 16))
        {
            ui.Text(
                "Profile",
                variant: TextVariant.Heading,
                fontWeight: t.Weight(WeightToken.Interface)
            );
            ui.Checkbox(value: true, label: "Email me updates");
            using (ui.Box(gap: 8))
            {
                ui.Button(text: "Cancel", variant: ComponentVariant.Outlined);
                ui.Button(text: "Save", id: "save");
            }
        }
    });

    static readonly Slot Share = Slot.Of(static ui =>
    {
        var t = ui.Theme;
        using (ui.Box(direction: Direction.Column, gap: 12, padding: 16))
        {
            ui.Text(
                "Share",
                variant: TextVariant.Heading,
                fontWeight: t.Weight(WeightToken.Interface)
            );
            using (
                ui.Tooltip(
                    open: true,
                    content: Slot.Of(static ui => ui.Text("Copied to clipboard", id: "tip-text"))
                )
            )
                ui.Button(text: "Copy link", id: "trigger");
        }
    });

    [FramePath]
    public static void Selected(Ui ui)
    {
        var root = ui.Ref();
        var model = ui.Remember(root, static r => new InspectorModel(r));
        ui.Effect(model, 0, static (m, _, _) => m.Inspect("save"));
        Stage(ui, model, root, Profile);
    }

    [FramePath]
    public static void Layers(Ui ui)
    {
        var root = ui.Ref();
        var model = ui.Remember(root, static r => new InspectorModel(r));
        ui.Effect(model, 0, static (m, _, _) => m.Inspect("tip-text", "trigger"));
        Stage(ui, model, root, Share);
    }

    [FramePath]
    static void Stage(
        Ui ui,
        InspectorModel model,
        NodeRef root,
        Slot app,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Box(height: 520, key: key, file: file, line: line))
        {
            using (ui.Box(width: 300, height: Length.Full))
            {
                using (
                    ui.Box(
                        direction: Direction.Column,
                        width: Length.Full,
                        height: Length.Full,
                        nodeRef: root
                    )
                )
                    app.Render(ui);
                ui.InspectorOverlay(
                    model: model,
                    position: Position.Absolute,
                    left: 0,
                    top: 0,
                    right: 0,
                    bottom: 0
                );
            }
            ui.InspectorPanel(model: model);
        }
    }
}

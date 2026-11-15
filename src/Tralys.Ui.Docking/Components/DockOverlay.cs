using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Docking;

internal readonly record struct DockOverlayProps;

/// <summary>While a tab or a floating panel drags: the root indicators, the compass over the group under the pointer,
/// the preview of where a drop lands, and the dragged tab's title by the pointer.</summary>
internal sealed class DockOverlay : Component<DockOverlayProps>
{
    static readonly IconRole[] Glyphs =
    [
        IconRole.ChevronLeft,
        IconRole.ChevronRight,
        IconRole.ChevronUp,
        IconRole.ChevronDown,
        IconRole.Plus,
    ];

    protected override void Render(Ui ui, in DockOverlayProps p)
    {
        var dock = ui.Use(Dock.Context).Dock;
        var state = ui.State(default(DockLook));
        dock?.Look = state;
        var look = state.Value;
        using (ui.Box(pointerEvents: PointerEvents.None))
        {
            if (!look.Shown)
                return;
            var t = ui.Theme;
            if (!look.RootPreview.IsEmpty)
                Preview(ui, look.RootPreview, key: 1);
            if (!look.GroupPreview.IsEmpty)
                Preview(ui, look.GroupPreview, key: 2);
            for (var i = 0; i < 5; i++)
            {
                if (look.RootShown(i))
                    Indicator(ui, look.RootBox(i), i, look.RootLit == i, key: 10 + i);
            }
            for (var i = 0; i < 5; i++)
            {
                if (look.CompassShown(i))
                    Indicator(ui, look.CompassBox(i), i, look.CompassLit == i, key: 20 + i);
            }
            if (look.Label is { } label)
                using (
                    ui.Box(
                        padding: new Thickness(8f, 4f, 8f, 4f),
                        background: t.Color(ColorToken.BgAccent),
                        border: new Border(1f, t.Color(ColorToken.BorderAccentHover)),
                        radius: t.Radius(RadiusToken.Md),
                        position: Position.Absolute,
                        left: look.LabelAt.X,
                        top: look.LabelAt.Y,
                        key: 30
                    )
                )
                    ui.Text(
                        label,
                        color: t.Color(ColorToken.BgCanvas),
                        fontWeight: t.Weight(WeightToken.Interface)
                    );
        }
    }

    [FramePath]
    static void Preview(
        Ui ui,
        Rect at,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        using (
            ui.Box(
                background: t.Color(ColorToken.BgTranslucent),
                border: new Border(2f, t.Color(ColorToken.BorderAccent)),
                position: Position.Absolute,
                left: at.X,
                top: at.Y,
                width: at.Width,
                height: at.Height,
                key: key,
                file: file,
                line: line
            )
        ) { }
    }

    [FramePath]
    static void Indicator(
        Ui ui,
        Rect at,
        int glyph,
        bool lit,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                background: t.Color(lit ? ColorToken.BgAccent : ColorToken.BgOverlay),
                border: new Border(
                    2f,
                    t.Color(lit ? ColorToken.BorderAccentHover : ColorToken.BorderMuted)
                ),
                radius: t.Radius(RadiusToken.Md),
                position: Position.Absolute,
                left: at.X,
                top: at.Y,
                width: at.Width,
                height: at.Height,
                key: key,
                file: file,
                line: line
            )
        )
            ui.Icon(glyph: IconGlyph.Of(Glyphs[glyph]));
    }
}

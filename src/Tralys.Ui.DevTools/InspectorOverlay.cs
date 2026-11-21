using System.Runtime.CompilerServices;
using Tralys.Ui.Fonts;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.DevTools;

internal sealed class InspectorOverlay : Component<InspectorProps>
{
    internal static readonly Color Selection = Color.FromRgb(64, 156, 255);
    internal static readonly Color Hover = Color.FromRgb(120, 200, 255);
    internal static readonly Color Flash = Color.FromRgb(255, 72, 72);
    internal static readonly Color Margin = Color.FromArgb(110, 246, 178, 107);
    internal static readonly Color BorderBand = Color.FromArgb(120, 255, 229, 153);
    internal static readonly Color Padding = Color.FromArgb(110, 147, 196, 125);
    internal static readonly Color Content = Color.FromArgb(90, 111, 168, 220);
    internal static readonly Color Gap = Color.FromArgb(110, 147, 112, 219);

    protected override void Render(Ui ui, in InspectorProps p)
    {
        var model = p.Model;
        using (ui.Box(width: Length.Full, height: Length.Full, pointerEvents: PointerEvents.None))
        {
            ui.RepaintFlashes(
                model: model,
                position: Position.Absolute,
                left: 0f,
                top: 0f,
                right: 0f,
                bottom: 0f
            );
            if (model.SelectedBox is { } box)
                Selected(ui, box);
            ui.GapBands(
                model: model,
                position: Position.Absolute,
                left: 0f,
                top: 0f,
                right: 0f,
                bottom: 0f
            );
            if (model.HoverBox is { } hover)
                At(ui, hover, new Border(1f, Hover), Hover.WithOpacity(0.3f));
            ui.EnvironmentTag(
                model: model,
                position: Position.Absolute,
                left: 0f,
                top: 0f,
                right: 0f,
                bottom: 0f
            );
            if (model.ShowsFrameBytes)
                ui.FrameBytesBadge(
                    model: model,
                    position: Position.Absolute,
                    right: 8f,
                    bottom: 8f
                );
        }
    }

    [FramePath]
    static void Selected(
        Ui ui,
        BoxModel box,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Fragment(file: file, line: line))
        {
            var bounds = box.Bounds;
            if (!box.Margin.IsZero)
                At(ui, box.MarginBox, new Border(box.Margin, Margin), default(Paint));
            if (!box.Border.IsZero)
                At(ui, bounds, new Border(box.Border, BorderBand), default(Paint));
            var inner = bounds.Deflate(box.Border);
            if (!box.Padding.IsZero)
                At(ui, inner, new Border(box.Padding, Padding), default(Paint));
            At(ui, inner.Deflate(box.Padding), default(Border), Content);
            At(ui, bounds, new Border(1f, Selection), default(Paint));
        }
    }

    [FramePath]
    internal static void At(
        Ui ui,
        Rect box,
        Border border,
        Paint fill,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                position: Position.Absolute,
                left: box.X,
                top: box.Y,
                width: MathF.Max(0f, box.Width),
                height: MathF.Max(0f, box.Height),
                border: border,
                background: fill,
                key: key,
                file: file,
                line: line
            )
        ) { }
    }
}

internal sealed class RepaintFlashes : Component<InspectorProps>
{
    protected override void Render(Ui ui, in InspectorProps p)
    {
        var model = p.Model;
        _ = model.FlashRevision;
        var flashes = model.Flashes;
        using (ui.Box(pointerEvents: PointerEvents.None))
        {
            for (var i = 0; i < flashes.Length; i++)
            {
                var flash = flashes[i];
                var strength = 1f - (float)flash.Age / InspectorModel.FlashFrames;
                InspectorOverlay.At(
                    ui,
                    flash.Box,
                    new Border(1f, InspectorOverlay.Flash.WithOpacity(0.9f * strength)),
                    InspectorOverlay.Flash.WithOpacity(0.18f * strength),
                    key: i
                );
            }
        }
    }
}

internal sealed class GapBands : Component<InspectorProps>
{
    protected override void Render(Ui ui, in InspectorProps p)
    {
        var model = p.Model;
        _ = model.GapRevision;
        var gaps = model.Gaps;
        using (ui.Box(pointerEvents: PointerEvents.None))
        {
            for (var i = 0; i < gaps.Length; i++)
                InspectorOverlay.At(ui, gaps[i], default(Border), InspectorOverlay.Gap, key: i);
        }
    }
}

internal sealed class EnvironmentTag : Component<InspectorProps>
{
    const float KeyWidth = 64f;

    protected override void Render(Ui ui, in InspectorProps p)
    {
        var model = p.Model;
        var box = model.SelectedBox;
        var environment = model.SelectedEnvironment;
        using (ui.Layout(algorithm: TagPlacement.Instance, state: box?.MarginBox ?? default))
        {
            if (box is not null && environment is { } shown)
                Tag(ui, model, shown);
        }
    }

    [FramePath]
    static void Tag(
        Ui ui,
        InspectorModel model,
        in ResolvedEnvironment environment,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        var style = environment.Style;
        using (
            ui.Box(
                id: "environment",
                direction: Direction.Column,
                padding: new Thickness(6f, 2f, 6f, 2f),
                radius: 4f,
                background: t.Color(ColorToken.BgSunken),
                border: new Border(1f, t.Color(ColorToken.Border)),
                fontSize: t.FontSize(TextVariant.Caption),
                fontWeight: t.Weight(WeightToken.Interface),
                file: file,
                line: line
            )
        )
        {
            Row(ui, "Theme", ui.Format($"{environment.Theme.Name} · {style.Density}"));
            Row(
                ui,
                "Font",
                style.FontStyle == FontStyle.Normal
                    ? ui.Format(
                        $"{style.FontFamily} {style.FontSize:0.##} · {(int)style.FontWeight}"
                    )
                    : ui.Format(
                        $"{style.FontFamily} {style.FontSize:0.##} · {(int)style.FontWeight} {style.FontStyle}"
                    )
            );
            if (style.LineHeight > 0f)
                Row(ui, "Line height", ui.Format($"{style.LineHeight:0.##}"));
            if (style.LetterSpacing != 0f)
                Row(ui, "Spacing", ui.Format($"{style.LetterSpacing:0.##}"));
            if (style.TabularDigits)
                Row(ui, "Digits", "tabular");
            using (ui.Box(align: Align.Center, gap: 8))
            {
                Key(ui, "Color");
                using (ui.Box(align: Align.Center, gap: 4))
                {
                    using (
                        ui.Box(
                            width: 10,
                            height: 10,
                            background: style.Color,
                            border: new Border(1f, t.Color(ColorToken.Border))
                        )
                    ) { }
                    ui.Text(PaintText(ui, style.Color));
                }
            }
            if (style.TextAlign != TextAlign.Left)
                Row(ui, "Align", ui.Format($"{style.TextAlign}"));
            if (style.PointerEvents == PointerEvents.None)
                Row(ui, "Pointer", "none");
            if (environment.Disabled)
                Row(ui, "Disabled", "true");
            if (environment.Held != InputStates.None)
                Row(ui, "Held", model.Names(environment.Held));
            if (environment.Exiting)
                Row(ui, "Exiting", "true");
        }
    }

    [FramePath]
    static void Row(
        Ui ui,
        TextValue key,
        TextValue value,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Box(gap: 8, file: file, line: line))
        {
            Key(ui, key);
            ui.Text(value);
        }
    }

    [FramePath]
    static void Key(
        Ui ui,
        TextValue key,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Text(
            key,
            color: ui.Theme.Color(ColorToken.FgMuted),
            width: KeyWidth,
            maxLines: 1,
            file: file,
            line: line
        );

    [FramePath]
    static TextValue PaintText(Ui ui, Paint paint)
    {
        if (paint.Kind != PaintKind.Solid)
            return ui.Format($"{paint.Kind}");
        var c = paint.Color;
        return c.A == 255
            ? ui.Format($"#{c.R:X2}{c.G:X2}{c.B:X2}")
            : ui.Format($"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}");
    }
}

internal sealed class TagPlacement : LayoutAlgorithm<Rect>
{
    const float Space = 4f;

    public static readonly TagPlacement Instance = new();

    public override Size Measure(
        ref LayoutContext cx,
        Rect anchor,
        AvailableSpace width,
        AvailableSpace height
    ) => new(width.IsDefinite ? width.Value : 0f, height.IsDefinite ? height.Value : 0f);

    public override void Arrange(ref LayoutContext cx, Rect anchor, Size size)
    {
        if (cx.ChildCount == 0)
            return;
        var tag = cx.MeasureChild(
            0,
            AvailableSpace.Definite(size.Width),
            AvailableSpace.MaxContent
        );
        var lowest = MathF.Max(0f, size.Height - tag.Height);
        var y =
            anchor.Bottom + Space <= lowest ? anchor.Bottom + Space
            : anchor.Y - Space - tag.Height >= 0f ? anchor.Y - Space - tag.Height
            : Math.Clamp(anchor.Y + Space, 0f, lowest);
        var x = Math.Clamp(anchor.X, 0f, MathF.Max(0f, size.Width - tag.Width));
        cx.PlaceChild(0, new Rect(x, y, tag.Width, tag.Height));
    }
}

internal sealed class FrameBytesBadge : Component<InspectorProps>
{
    protected override void Render(Ui ui, in InspectorProps p)
    {
        var model = p.Model;
        var t = ui.Theme;
        using (
            ui.Box(
                padding: new Thickness(6f, 2f, 6f, 2f),
                radius: 4f,
                background: t.Color(ColorToken.BgSunken),
                border: new Border(1f, t.Color(ColorToken.Border)),
                pointerEvents: PointerEvents.None
            )
        )
            ui.Text(
                model.AtlasResets > 0
                    ? ui.Format(
                        $"{model.FrameBytes} B/frame · worst {model.WorstBytes} B in {InspectorModel.RecentFrames} · {model.AtlasResets} atlas resets"
                    )
                    : ui.Format(
                        $"{model.FrameBytes} B/frame · worst {model.WorstBytes} B in {InspectorModel.RecentFrames}"
                    ),
                variant: TextVariant.Caption,
                fontWeight: t.Weight(WeightToken.Interface)
            );
    }
}

using System.Globalization;
using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.DevTools;

internal readonly record struct InspectorProps
{
    public InspectorModel? Model { get; init; }
}

internal sealed class InspectorRoot : Component<InspectorProps>
{
    protected override void Render(Ui ui, in InspectorProps p)
    {
        using (ui.Box(width: Length.Full, height: Length.Full))
        {
            using (ui.Box(grow: 1, overflow: Overflow.Clip, pointerEvents: PointerEvents.None))
                ui.InspectorOverlay(model: p.Model);
            ui.InspectorPanel(model: p.Model);
        }
    }
}

internal sealed class InspectorPanel : Component<InspectorProps>
{
    protected override void Render(Ui ui, in InspectorProps p)
    {
        var model = p.Model!;
        var t = ui.Theme;
        using (
            ui.Box(
                direction: Direction.Column,
                width: Inspector.PanelWidth,
                height: Length.Full,
                overflow: Overflow.Clip,
                background: t.Color(ColorToken.BgSunken),
                border: new Border(new Thickness(1f, 0f, 0f, 0f), t.Color(ColorToken.Border)),
                fontSize: t.FontSize(TextVariant.Caption),
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            ui.InspectorToolbar(model: model);
            Caption(ui, "Elements");
            using (ui.Box(direction: Direction.Column, height: model.TreeHeight, shrink: 0f))
                ui.ElementTreePane(model: model, grow: 1);
            ui.ResizeHandle(
                edge: ResizeEdge.Bottom,
                value: model.TreeHeight,
                min: 48f,
                height: 4f,
                onValueChange: Callback<float>.Of(
                    model,
                    static (m, height) => m.TreeHeight = height
                )
            );
            using (ui.ScrollArea(horizontal: ScrollbarVisibility.Disabled, grow: 1, basis: 0f))
                ui.SelectionDetails(model: model);
        }
    }

    [FramePath]
    internal static void Caption(
        Ui ui,
        TextValue text,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Text(
            text,
            variant: TextVariant.Caption,
            color: ui.Theme.Color(ColorToken.FgMuted),
            margin: new Thickness(8f, 6f, 8f, 2f),
            file: file,
            line: line
        );
}

internal sealed class InspectorToolbar : Component<InspectorProps>
{
    protected override void Render(Ui ui, in InspectorProps p)
    {
        var model = p.Model!;
        using (ui.Box(align: Align.Center, gap: 4, padding: 6))
        {
            using (ui.Tooltip(text: "Pick an element · Ctrl+Shift+C"))
                ui.Toggle(
                    text: "Pick",
                    value: model.Picking,
                    onValueChange: Callback<bool>.Of(model, static (m, on) => m.ChangePicking(on)),
                    size: ComponentSize.Xs
                );
            using (ui.Tooltip(text: "Flash what repaints"))
                ui.Toggle(
                    text: "Repaints",
                    value: model.ShowsRepaints,
                    onValueChange: Callback<bool>.Of(model, static (m, on) => m.ShowsRepaints = on),
                    size: ComponentSize.Xs
                );
            using (ui.Tooltip(text: "What the app's frames allocate"))
                ui.Toggle(
                    text: "Bytes",
                    value: model.ShowsFrameBytes,
                    onValueChange: Callback<bool>.Of(
                        model,
                        static (m, on) => m.ShowsFrameBytes = on
                    ),
                    size: ComponentSize.Xs
                );
            using (ui.Box(grow: 1)) { }
            ui.Button(
                text: InspectorModel.SpeedNames[model.SpeedStep],
                variant: ComponentVariant.Ghost,
                size: ComponentSize.Xs,
                onClick: Callback.Of(model, static m => m.CycleSpeed())
            );
        }
    }
}

internal sealed class ElementTreePane : Component<InspectorProps>
{
    static readonly KeyOf<InspectorRow> Key = static row => row.Key;

    static readonly Callback<TreeExpandedChange<InspectorRow>> Expand = Callback<
        TreeExpandedChange<InspectorRow>
    >.Of(static change => change.Row.IsExpanded = change.Expanded);

    protected override void Render(Ui ui, in InspectorProps p)
    {
        var model = p.Model!;
        model.TreeScroll = ui.ScrollHandle();
        ui.Tree(
            items: model.Tree.Rows,
            itemKey: Key,
            item: Slot<InspectorRow>.Of(
                model,
                static (ui, m, row, _) => ui.ElementRow(item: row, model: m)
            ),
            value: model.TreeValue,
            onValueChange: Callback<UiKey>.Of(model, static (m, key) => m.SelectRow(key)),
            onExpandedChange: Expand,
            scroll: model.TreeScroll,
            size: ComponentSize.Xs,
            height: Length.Full
        );
    }
}

internal readonly record struct ElementRowProps
{
    public InspectorRow? Item { get; init; }

    public InspectorModel? Model { get; init; }
}

internal sealed class ElementRow : Component<ElementRowProps>
{
    protected override void Render(Ui ui, in ElementRowProps p)
    {
        var row = p.Item!;
        var entry = ui.Remember(static () => ShownRow.Rent());
        entry.Bind(p.Model!, row);
        ui.TreeItem(content: Slot.Of(row, static (ui, r) => RowLabel(ui, r)));
    }

    [FramePath]
    static void RowLabel(
        Ui ui,
        InspectorRow row,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var node = row.Node;
        var t = ui.Theme;
        var muted = t.Color(ColorToken.FgMuted);
        using (
            ui.Box(align: Align.Center, gap: 6, onHoverChange: row.Hover, file: file, line: line)
        )
        {
            if (node.Host is { } host)
                ui.Text(ui.Format($"{ValueText.ShortName(host.ComponentType)}"));
            else
                ui.Text(Names.Kind(node.Kind), color: muted);
            if (Names.IdOf(node) is { } id)
                ui.Text(ui.Format($"#{id}"), color: muted);
            if (node.Kind == NodeKind.Text)
            {
                var text = node.Text;
                row.Show(text);
                ui.Text(
                    ui.Format($"\"{text[..Math.Min(text.Length, 40)]}\""),
                    color: t.Color(ColorToken.FgAccent)
                );
            }
            if (node.Kind == NodeKind.Layer)
                using (
                    ui.Box(
                        id: "layer-badge",
                        padding: new Thickness(4f, 0f, 4f, 0f),
                        radius: t.Radius(RadiusToken.Sm),
                        background: t.Color(ColorToken.BgAccentMuted)
                    )
                )
                    ui.Text(Names.Layer(node), color: t.Color(ColorToken.FgAccent));
        }
    }
}

internal sealed class ShownRow : IDisposable
{
    InspectorModel? _model;
    InspectorRow? _row;

    public static ShownRow Rent() => Pool<ShownRow>.TryRent() ?? new ShownRow();

    public void Bind(InspectorModel model, InspectorRow row)
    {
        if (ReferenceEquals(_row, row) && ReferenceEquals(_model, model))
            return;
        Leave();
        _model = model;
        _row = row;
        model.ShownRows.Add(row);
    }

    void Leave()
    {
        if (_row is { } row && _model is { } model)
        {
            model.ShownRows.Remove(row);
            if (row.Hovered)
                row.HoverChanged(false);
        }
        _row = null;
        _model = null;
    }

    public void Dispose()
    {
        Leave();
        Pool<ShownRow>.Return(this);
    }
}

internal sealed class SelectionDetails : Component<InspectorProps>
{
    protected override void Render(Ui ui, in InspectorProps p)
    {
        var model = p.Model!;
        _ = model.Revision;
        var t = ui.Theme;
        var muted = t.Color(ColorToken.FgMuted);
        using (ui.Box(direction: Direction.Column, gap: 4, padding: new Thickness(8f, 4f, 8f, 8f)))
        {
            if (model.Selected is not { } node)
            {
                ui.Text("Nothing selected", color: muted);
                return;
            }

            using (ui.Box(align: Align.Center, gap: 6))
            {
                if (node.Host is { } own)
                    ui.Text(
                        ui.Format($"{ValueText.ShortName(own.ComponentType)}"),
                        color: t.Color(ColorToken.FgStrong)
                    );
                else
                    ui.Text(Names.Kind(node.Kind), color: t.Color(ColorToken.FgStrong));
                if (Names.IdOf(node) is { } id)
                    ui.Text(ui.Format($"#{id}"), color: muted);
            }
            if (node.File is { } file)
                using (ui.Box(align: Align.Center, gap: 6))
                {
                    ui.Text(
                        ui.Format($"{Path.GetFileName(file.AsSpan())}:{node.Line}"),
                        color: muted,
                        grow: 1,
                        maxLines: 1
                    );
                    if (model.OpenSource is not null)
                        ui.Button(
                            text: "Open",
                            variant: ComponentVariant.Ghost,
                            size: ComponentSize.Xs,
                            onClick: Callback.Of(model, static m => m.Open())
                        );
                }
            var boxed = InspectorModel.Boxed(node) ?? node;
            var bounds = boxed.ViewBounds;
            ui.Text(
                ui.Format(
                    $"at {bounds.X:0.##}, {bounds.Y:0.##}  {bounds.Width:0.##} × {bounds.Height:0.##}"
                )
            );
            ui.Text(
                ui.Format(
                    $"margin {new SidesText(boxed.Place.Margin)} · border {new SidesText(boxed.Layout.Border)} · padding {new SidesText(boxed.Layout.Padding)}"
                ),
                color: muted
            );
            if (node.Place.Collapsed || node.Place.Hidden)
                ui.Text(node.Place.Collapsed ? "collapsed" : "hidden", color: muted);
            if (node.States != InputStates.None)
                ui.Text(ui.Format($"states {model.Names(node.States)}"), color: muted);

            using (ui.Box(gap: 10, padding: new Thickness(0f, 4f, 0f, 4f)))
            {
                var held = model.Held;
                ui.Checkbox(
                    label: "Hover",
                    value: (held & InputStates.Hovered) != 0,
                    onValueChange: Callback<bool>.Of(
                        model,
                        static (m, _) => m.Toggle(InputStates.Hovered)
                    ),
                    size: ComponentSize.Xs
                );
                ui.Checkbox(
                    label: "Pressed",
                    value: (held & InputStates.Pressed) != 0,
                    onValueChange: Callback<bool>.Of(
                        model,
                        static (m, _) => m.Toggle(InputStates.Pressed)
                    ),
                    size: ComponentSize.Xs
                );
                ui.Checkbox(
                    label: "Focused",
                    value: (held & InputStates.Focused) != 0,
                    onValueChange: Callback<bool>.Of(
                        model,
                        static (m, _) => m.Toggle(InputStates.Focused)
                    ),
                    size: ComponentSize.Xs
                );
                ui.Checkbox(
                    label: "Disabled",
                    value: (held & InputStates.Disabled) != 0,
                    onValueChange: Callback<bool>.Of(
                        model,
                        static (m, _) => m.Toggle(InputStates.Disabled)
                    ),
                    size: ComponentSize.Xs
                );
            }

            if (InspectorModel.ComponentOf(node) is not { Host: { } host })
                return;
            using (ui.Box(align: Align.Center, gap: 6, padding: new Thickness(0f, 6f, 0f, 2f)))
            {
                ui.Text(
                    ui.Format(
                        $"{ValueText.ShortName(host.ComponentType)} · renders {host.RenderCount}"
                    ),
                    color: t.Color(ColorToken.FgStrong),
                    grow: 1
                );
                ui.Checkbox(
                    label: "Defaults",
                    value: model.ShowsDefaults,
                    onValueChange: Callback<bool>.Of(model, static (m, on) => m.ShowsDefaults = on),
                    size: ComponentSize.Xs
                );
            }
            ui.PropertyList(model: model);
            ui.Checkbox(
                label: model.WatchCount > 0
                    ? ui.Format($"Break on change · {model.WatchCount}")
                    : "Break on change",
                value: model.Watching,
                onValueChange: Callback<bool>.Of(model, static (m, on) => m.Watching = on),
                disabled: model.WatchedProp is null,
                size: ComponentSize.Xs
            );

            ref var dependencies = ref host.Dependencies;
            ui.Text(ui.Format($"Reads {dependencies.Count} sources"), color: muted);
            if (host.EnvReads != EnvReads.None)
                ui.Text(ui.Format($"  environment {model.Names(host.EnvReads)}"), color: muted);
            if (host.StateReads != InputStates.None)
                ui.Text(ui.Format($"  states {model.Names(host.StateReads)}"), color: muted);
            using (ui.Box(direction: Direction.Column))
            {
                for (var i = 0; i < dependencies.Count; i++)
                    ui.Text(
                        ui.Format(
                            $"  {ValueText.ShortName(dependencies.SourceAt(i).GetType())} {model.Bits(dependencies.MaskAt(i))}"
                        ),
                        color: muted,
                        key: i
                    );
            }
            using (ui.Box(direction: Direction.Column))
            {
                var keys = HostInfo.ContextKeys(host);
                for (var i = 0; i < keys.Length; i++)
                    ui.Text(ui.Format($"  context {keys[i].ToString()}"), color: muted, key: i);
            }
        }
    }
}

internal sealed class PropertyList : Component<InspectorProps>
{
    protected override void Render(Ui ui, in InspectorProps p)
    {
        var model = p.Model!;
        _ = model.Revision;
        var t = ui.Theme;
        var defaults = model.ShowsDefaults;
        var watched = model.WatchedProp;
        var props = model.Props;
        using (ui.Box(direction: Direction.Column))
        {
            for (var i = 0; i < props.Count; i++)
            {
                var row = props[i];
                if (row.IsDefault && !defaults)
                    continue;
                var selected = ReferenceEquals(row.Name, watched);
                using (
                    ui.Box(
                        gap: 8,
                        padding: new Thickness(4f, 1f, 4f, 1f),
                        radius: t.Radius(RadiusToken.Sm),
                        background: selected ? t.Color(ColorToken.BgAccentMuted) : default(Paint),
                        onClick: Callback.Of(model, row.Name, static (m, name) => m.Watch(name)),
                        key: i
                    )
                )
                {
                    ui.Text(
                        row.Name,
                        color: t.Color(ColorToken.FgMuted),
                        width: 120f,
                        maxLines: 1,
                        key: 0
                    );
                    ui.Text(row.Text, grow: 1, maxLines: 1, key: 1);
                }
            }
        }
    }
}

internal static class Names
{
    public static string Kind(NodeKind kind) =>
        kind switch
        {
            NodeKind.Root => "Root",
            NodeKind.Box => "Box",
            NodeKind.Grid => "Grid",
            NodeKind.Text => "Text",
            NodeKind.Span => "Span",
            NodeKind.EditableText => "EditableText",
            NodeKind.Image => "Image",
            NodeKind.Canvas => "Canvas",
            NodeKind.Layout => "Layout",
            NodeKind.VirtualList => "VirtualList",
            NodeKind.Layer => "Layer",
            NodeKind.Component => "Component",
            NodeKind.Fragment => "Fragment",
            _ => "ActionScope",
        };

    public static string? IdOf(Node node) => node.Host is { Root: { } root } ? root.Id : node.Id;

    public static string Layer(Node layer) =>
        ((LayerPayload)layer.Payload!).Props.Tier switch
        {
            LayerTier.Toast => "layer · toast",
            LayerTier.Tooltip => "layer · tooltip",
            _ => "layer",
        };
}

internal readonly struct SidesText(Thickness t) : ISpanFormattable
{
    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider
    )
    {
        charsWritten = 0;
        if (t.Left == t.Top && t.Top == t.Right && t.Right == t.Bottom)
            return Number(destination, ref charsWritten, t.Left);
        return Number(destination, ref charsWritten, t.Left)
            && Comma(destination, ref charsWritten)
            && Number(destination, ref charsWritten, t.Top)
            && Comma(destination, ref charsWritten)
            && Number(destination, ref charsWritten, t.Right)
            && Comma(destination, ref charsWritten)
            && Number(destination, ref charsWritten, t.Bottom);
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        Span<char> chars = stackalloc char[64];
        return TryFormat(chars, out var written, default, null) ? new string(chars[..written]) : "";
    }

    public override string ToString() => ToString(null, null);

    static bool Number(Span<char> destination, ref int at, float value)
    {
        if (value == 0f)
            value = 0f;
        if (
            !value.TryFormat(
                destination[at..],
                out var written,
                "0.##",
                CultureInfo.InvariantCulture
            )
        )
            return false;
        at += written;
        return true;
    }

    static bool Comma(Span<char> destination, ref int at)
    {
        if (at >= destination.Length)
            return false;
        destination[at++] = ',';
        return true;
    }
}

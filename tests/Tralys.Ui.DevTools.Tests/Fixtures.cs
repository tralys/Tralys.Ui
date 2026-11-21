using System.Numerics;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.DevTools.Tests;

sealed class NullHost : IViewHost
{
    public void SetCursor(Cursor? cursor) { }

    public string? GetClipboardText() => null;

    public void SetClipboardText(ReadOnlySpan<char> text) { }

    public void StartTextInput(TextInputKind kind, Rect area, bool byContact) { }

    public void StopTextInput() { }
}

sealed class Counter
{
    public int Value;
}

public sealed class HoverProbe
{
    public bool Hovered;
    public bool Pressed;
}

public readonly record struct LampProps
{
    public required HoverProbe Probe { get; init; }
}

public sealed class Lamp : Component<LampProps>
{
    protected override void Render(Ui ui, in LampProps p)
    {
        var hovered = ui.IsHovered;
        p.Probe.Hovered = hovered;
        p.Probe.Pressed = ui.IsPressed;
        using (
            ui.Box(
                width: 40,
                height: 20,
                background: hovered ? Color.FromRgb(250, 200, 0) : Color.FromRgb(40, 40, 40)
            )
        )
            ui.Text("lamp");
    }
}

public sealed partial class Gauge : ObservableObject
{
    [ObservableProperty]
    public partial float Level { get; set; }

    [ObservableProperty]
    public partial int Tick { get; set; }

    [ObservableProperty]
    public partial bool Extra { get; set; }
}

public readonly record struct MeterProps
{
    public float Level { get; init; }

    public TextValue Caption { get; init; }

    public int Steps { get; init; }
}

public sealed class Meter : Component<MeterProps>
{
    protected override void Render(Ui ui, in MeterProps p)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                width: 80,
                height: 30,
                padding: new Thickness(4, 2, 4, 2)
            )
        )
        {
            using (ui.Box(width: 10 + p.Level, height: 6, background: Color.FromRgb(90, 160, 90)))
            { }
            if (!p.Caption.IsNull)
                ui.Text(p.Caption);
        }
    }
}

public readonly record struct DialProps
{
    public required Gauge Gauge { get; init; }
}

public sealed class Dial : Component<DialProps>
{
    protected override void Render(Ui ui, in DialProps p)
    {
        var gauge = p.Gauge;
        using (ui.Box(direction: Direction.Column, gap: 4, padding: 4))
        {
            ui.Meter(level: gauge.Level, caption: ui.Format($"tick {gauge.Tick}"), id: "meter");
            if (gauge.Extra)
                using (ui.Box(id: "extra", width: 20, height: 20)) { }
        }
    }
}

static class Find
{
    public static Node? Text(Node node, string text)
    {
        foreach (var child in node.Children)
        {
            if (child.Kind == NodeKind.Text && child.Text.SequenceEqual(text))
                return child;
            if (Text(child, text) is { } found)
                return found;
        }
        return null;
    }

    public static Node? Id(Node node, string id)
    {
        foreach (var child in node.Children)
        {
            if (child.Id == id)
                return child;
            if (Id(child, id) is { } found)
                return found;
        }
        return null;
    }

    public static Vector2 Corner(Node node) => new(node.ViewBounds.X, node.ViewBounds.Y);

    public static Vector2 Centre(Node node)
    {
        var b = node.ViewBounds;
        return new Vector2(b.X + b.Width / 2f, b.Y + b.Height / 2f);
    }
}

sealed class PanelHarness : IDisposable
{
    PanelHarness(UiDriver app, InspectorModel model, UiDriver panel)
    {
        App = app;
        Model = model;
        Panel = panel;
    }

    public UiDriver App { get; }

    public InspectorModel Model { get; }

    public UiDriver Panel { get; }

    public static PanelHarness Over(UiDriver app)
    {
        var model = new InspectorModel(app.View);
        model.Refresh();
        var panel = UiDriver.Render(
            ui => ui.InspectorPanel(model: model),
            size: new Size(Inspector.PanelWidth, 600f)
        );
        return new PanelHarness(app, model, panel);
    }

    public void Sync()
    {
        App.Settle();
        Model.Refresh();
        Panel.Settle();
    }

    public void Dispose()
    {
        Model.Release();
        Panel.Dispose();
    }
}

public sealed class Tags
{
    public static readonly Tags One = new();
}

public readonly record struct SwatchProps
{
    public string? Name { get; init; }

    public Thickness Inset { get; init; }

    public Thickness Gap { get; init; }

    public Color Tint { get; init; }

    public float Ratio { get; init; }

    public Tags? Tag { get; init; }
}

public sealed class Swatch : Component<SwatchProps>
{
    protected override void Render(Ui ui, in SwatchProps p)
    {
        using (ui.Box(width: 20, height: 20, background: p.Tint)) { }
    }
}

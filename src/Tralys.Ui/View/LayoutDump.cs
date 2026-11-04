using System.Buffers;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Tralys.Ui;

/// <summary>One node of a <see cref="LayoutDump"/>: its path, each step a component's type without a generic's arity or
/// any other node's kind, with its id, or its index among its siblings when it has none or a sibling of its name shares
/// it; its border box in view space, its text when it shows text, though never a masked or password field's, and whether it is hidden,
/// by its placement or by the custom layout around it leaving it out (<see cref="Node.IsLayoutHidden"/>), or
/// collapsed.</summary>
public readonly record struct LayoutEntry(
    string Path,
    Rect Bounds,
    string? Text,
    bool Hidden,
    bool Collapsed
);

/// <summary>The boxes layout gave a view's nodes, in tree order: a layer under its owner, nothing under a collapsed
/// node, since nothing lays it out, and no component, fragment or action scope, which lay out nothing themselves;
/// values rounded to 0.01. With them, whether the view takes a press (<see cref="UiView.TakesPress"/>) at each centre
/// of a 32 by 32 grid across it, a row a string of <c>0</c> and <c>1</c>. It reads any view, a host's capture tool's
/// included, and is what the goldens hold.</summary>
public sealed class LayoutDump
{
    const int ConsumeCells = 32;

    LayoutDump(Size view, float scale, LayoutEntry[] nodes, string[] consume)
    {
        View = view;
        Scale = scale;
        Nodes = nodes;
        ConsumeMap = consume;
    }

    /// <summary>The view's size in layout units.</summary>
    public Size View { get; }

    public float Scale { get; }

    public IReadOnlyList<LayoutEntry> Nodes { get; }

    public IReadOnlyList<string> ConsumeMap { get; }

    /// <summary>The layout of <paramref name="view"/> as it was last laid out.</summary>
    public static LayoutDump Of(UiView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        view.Context.Dispatcher.VerifyAccess();
        var nodes = new List<LayoutEntry>();
        Read(view.Root, "", nodes);
        return new LayoutDump(view.Size, view.Scale, [.. nodes], Consume(view));
    }

    /// <summary>The layout of <paramref name="node"/> and everything under it, the layers opened inside it included,
    /// as <see cref="Of(UiView)"/> holds them but led by the node's own line, with paths starting at its step, so a part
    /// of a screen dumps the same wherever it sits. The view's root steps as <c>Root</c>, so its dump is the whole
    /// view's under one enclosing line. <see cref="View"/>, <see cref="Scale"/> and <see cref="ConsumeMap"/> are still
    /// the whole view's.</summary>
    /// <exception cref="ArgumentException">The node is not in a view.</exception>
    public static LayoutDump Of(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!node.IsMounted || node.View is not { } view)
            throw new ArgumentException("The node is not in a view.", nameof(node));
        view.Context.Dispatcher.VerifyAccess();
        var nodes = new List<LayoutEntry>();
        var step = node.Parent is { } parent ? NodePath.Step(node, parent) : NodePath.Name(node);
        ReadNode(node, step, nodes);
        return new LayoutDump(view.Size, view.Scale, [.. nodes], Consume(view));
    }

    static void Read(Node parent, string parentPath, List<LayoutEntry> nodes)
    {
        foreach (var node in parent.Children)
        {
            if (node.SpareRow)
                continue;
            var step = NodePath.Step(node, parent);
            ReadNode(node, parentPath.Length == 0 ? step : $"{parentPath}/{step}", nodes);
        }
    }

    static void ReadNode(Node node, string path, List<LayoutEntry> nodes)
    {
        var collapsed = node.Place.Collapsed;
        if (!node.IsTransparent)
        {
            var text = node.Payload is EditorPayload { Props.Masking: true } ? default : node.Text;
            nodes.Add(
                new LayoutEntry(
                    path,
                    Rounded(node.ViewBounds),
                    text.IsEmpty ? null : text.ToString(),
                    node.Place.Hidden || node.LayoutHidden,
                    collapsed
                )
            );
        }
        if (!collapsed)
            Read(node, path, nodes);
    }

    static string[] Consume(UiView view)
    {
        var rows = new string[ConsumeCells];
        Span<char> row = stackalloc char[ConsumeCells];
        for (var y = 0; y < ConsumeCells; y++)
        {
            for (var x = 0; x < ConsumeCells; x++)
            {
                var cell = new Vector2(
                    (x + 0.5f) * view.PixelWidth / ConsumeCells,
                    (y + 0.5f) * view.PixelHeight / ConsumeCells
                );
                row[x] = view.TakesPress(cell) ? '1' : '0';
            }
            rows[y] = new string(row);
        }
        return rows;
    }

    static Rect Rounded(Rect r) => new(Round(r.X), Round(r.Y), Round(r.Width), Round(r.Height));

    static float Round(float value)
    {
        var rounded = MathF.Round(value * 100f) / 100f;
        // -0 equals 0 but is written "-0".
        return rounded == 0f ? 0f : rounded;
    }

    /// <summary>The dump as JSON, a node a line.</summary>
    public string ToJson() => Encoding.UTF8.GetString(Utf8Json());

    /// <summary>Writes <see cref="ToJson"/> to <paramref name="path"/>, making its directory.</summary>
    public void Save(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
            Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, Utf8Json());
    }

    byte[] Utf8Json()
    {
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output);
        output.Write("{\"view\":"u8);
        writer.WriteStartObject();
        writer.WriteNumber("width", View.Width);
        writer.WriteNumber("height", View.Height);
        writer.WriteNumber("scale", Scale);
        writer.WriteEndObject();
        writer.Flush();
        output.Write(",\"nodes\":["u8);
        for (var i = 0; i < Nodes.Count; i++)
        {
            output.Write(i == 0 ? "\n"u8 : ",\n"u8);
            writer.Reset();
            Write(writer, Nodes[i]);
            writer.Flush();
        }
        output.Write("\n],\"consume\":"u8);
        writer.Reset();
        writer.WriteStartArray();
        foreach (var row in ConsumeMap)
            writer.WriteStringValue(row);
        writer.WriteEndArray();
        writer.Flush();
        output.Write("}\n"u8);
        return output.WrittenSpan.ToArray();
    }

    static void Write(Utf8JsonWriter writer, in LayoutEntry node)
    {
        writer.WriteStartObject();
        writer.WriteString("path", node.Path);
        writer.WriteNumber("x", node.Bounds.X);
        writer.WriteNumber("y", node.Bounds.Y);
        writer.WriteNumber("width", node.Bounds.Width);
        writer.WriteNumber("height", node.Bounds.Height);
        if (node.Text is { } text)
            writer.WriteString("text", text);
        if (node.Hidden)
            writer.WriteBoolean("hidden", true);
        if (node.Collapsed)
            writer.WriteBoolean("collapsed", true);
        writer.WriteEndObject();
    }
}

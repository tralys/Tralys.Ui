using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Tests;

public sealed partial class Line : ObservableObject
{
    public Line()
    {
        Name = "";
        Height = 20;
    }

    public required int Id { get; init; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial float Height { get; set; }

    public static ObservableList<Line> Many(int count, float height = 20)
    {
        var lines = new ObservableList<Line>(count);
        for (var i = 0; i < count; i++)
            lines.Add(
                new Line
                {
                    Id = i,
                    Name = "line " + i,
                    Height = height,
                }
            );
        return lines;
    }

    public static readonly KeyOf<Line> ById = static line => line.Id;
}

public readonly record struct LineRowProps
{
    public required Line Item { get; init; }
}

/// <summary>A row: its line's name, as tall as its line, keeping a click count of its own.</summary>
public sealed class LineRow : Component<LineRowProps>
{
    protected override void Render(Ui ui, in LineRowProps p)
    {
        var line = p.Item;
        var clicks = ui.State(0);
        using (
            ui.Box(
                height: line.Height,
                onClick: Callback.Of(clicks, static c => c.Value++),
                focusable: false
            )
        )
        {
            ui.Text(line.Name);
            ui.Text(ui.Format($"clicks {clicks.Value}"));
        }
    }
}

public static class LineSlots
{
    public static readonly Slot<Line> Rows = Slot<Line>.Of(
        static (ui, line, _) => ui.LineRow(item: line, id: line.Name)
    );
}

public readonly record struct CountBoxProps
{
    public required Model Model { get; init; }
}

/// <summary>A box as tall as its model's count.</summary>
public sealed class CountBox : Component<CountBoxProps>
{
    protected override void Render(Ui ui, in CountBoxProps p)
    {
        using (ui.Box(height: p.Model.Count)) { }
    }
}

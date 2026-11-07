using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery;

public readonly record struct InsertLineProps
{
    /// <summary>Draws the line across the row's bottom when true, its top when false, and none when null.</summary>
    public bool? Below { get; init; }
}

/// <summary>A sample of a component an app builds on the core: a list row with the line a sortable list draws where a
/// dragged row would land, 1.5 units in the accent border colour across the row's top or bottom edge. The line is an
/// absolute child of the row's box, so it moves and clips with the row and takes no hits.</summary>
[Children]
public sealed class InsertLine : Component<InsertLineProps>
{
    const float Thickness = 1.5f;

    protected override void Render(Ui ui, in InsertLineProps p)
    {
        using (ui.Box(direction: Direction.Column))
        {
            using (ui.ListBoxRow())
                ui.Children();
            using (
                ui.Box(
                    position: Position.Absolute,
                    left: 0,
                    right: 0,
                    top: p.Below == false ? -Thickness / 2 : Length.Auto,
                    bottom: p.Below == true ? -Thickness / 2 : Length.Auto,
                    height: Thickness,
                    background: ui.Theme.Color(ColorToken.BorderAccent),
                    pointerEvents: PointerEvents.None,
                    collapsed: p.Below is null
                )
            ) { }
        }
    }
}

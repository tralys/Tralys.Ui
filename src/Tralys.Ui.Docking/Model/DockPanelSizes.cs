namespace Tralys.Ui.Docking;

/// <summary>The four edge regions' sizes; zero leaves an edge at the dock's default.</summary>
public record struct DockPanelSizes
{
    public float LeftWidth { get; set; }
    public float RightWidth { get; set; }
    public float TopHeight { get; set; }
    public float BottomHeight { get; set; }
}

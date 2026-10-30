namespace Tralys.Ui;

/// <summary>Where an image paint's image sits across the bounds it paints when it is narrower or wider than them.</summary>
public enum AlignmentX : byte
{
    Left,
    Center,
    Right,
}

/// <summary>Where an image paint's image sits down the bounds it paints when it is shorter or taller than them.</summary>
public enum AlignmentY : byte
{
    Top,
    Center,
    Bottom,
}

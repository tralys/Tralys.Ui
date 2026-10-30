namespace Tralys.Ui;

/// <summary>A width and a height; either may be <see cref="float.PositiveInfinity"/> when it is a constraint.</summary>
public readonly record struct Size(float Width, float Height);

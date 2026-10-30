namespace Tralys.Ui;

/// <summary>Marks a member a frame path calls that runs only once for what it makes, such as a box kept until its value
/// changes or a cache filled on first use. The Tralys.Ui analyzers hold what a frame path calls to its bans, and stop
/// here; work a frame at rest repeats does not belong here. It takes nothing off a frame path it is marked onto.</summary>
[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor,
    Inherited = false
)]
public sealed class ColdPathAttribute : Attribute;

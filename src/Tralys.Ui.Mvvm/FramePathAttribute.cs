namespace Tralys.Ui;

// The analyzers find the attribute by name; the engine's public one sits above this assembly.
[AttributeUsage(
    AttributeTargets.Class
        | AttributeTargets.Struct
        | AttributeTargets.Method
        | AttributeTargets.Property
        | AttributeTargets.Constructor,
    Inherited = false
)]
sealed class FramePathAttribute : Attribute;

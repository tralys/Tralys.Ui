namespace Tralys.Ui;

/// <summary>Marks code that runs every frame. The Tralys.Ui analyzers then reject what would allocate in it: LINQ,
/// iterators and allocating enumerators, <c>params</c> arrays, string concatenation, capturing delegates and boxing.
/// On a type it covers every member; on a virtual, abstract or interface member, every override and implementation
/// too, in any assembly. On a delegate type it covers every lambda converted to it, and on a parameter every lambda
/// passed to it, wherever that lambda is written.</summary>
[AttributeUsage(
    AttributeTargets.Class
        | AttributeTargets.Struct
        | AttributeTargets.Method
        | AttributeTargets.Property
        | AttributeTargets.Constructor
        | AttributeTargets.Delegate
        | AttributeTargets.Parameter,
    Inherited = false
)]
public sealed class FramePathAttribute : Attribute;

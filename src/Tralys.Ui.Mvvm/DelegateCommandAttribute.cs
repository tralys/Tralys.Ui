namespace Tralys.Ui.Mvvm;

/// <summary>Turns an instance method into a <see cref="Command"/>: the generator adds a <c>{Method}Command</c> property
/// to the containing partial type that runs a <see langword="void"/> method, or one returning
/// <see cref="ValueTask"/>, which can't run again until its task completes. A single parameter makes it a
/// <see cref="Command{T}"/>.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class DelegateCommandAttribute : Attribute;

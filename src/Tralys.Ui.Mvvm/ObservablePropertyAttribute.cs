namespace Tralys.Ui.Mvvm;

/// <summary>Turns a partial property of an <see cref="ObservableObject"/> into a stored, notifying one.</summary>
/// <remarks>The generator writes its body, which keeps the value in the property's backing field and, when a set
/// changes it, calls the <c>On{Name}Changed(oldValue, newValue)</c> hook, if the type implements it, then raises
/// <see cref="ObservableObject.PropertyChanged"/> for the property and for each
/// <see cref="NotifyPropertyChangedForAttribute"/> name, with cached args.</remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ObservablePropertyAttribute : Attribute;

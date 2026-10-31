namespace Tralys.Ui.Mvvm;

/// <summary>Raises <see cref="ObservableObject.PropertyChanged"/> for <paramref name="propertyName"/> too whenever the
/// <see cref="ObservablePropertyAttribute"/> property it is written on changes, as a property computed from it
/// needs.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
public sealed class NotifyPropertyChangedForAttribute(string propertyName) : Attribute
{
    public string PropertyName { get; } = propertyName;
}

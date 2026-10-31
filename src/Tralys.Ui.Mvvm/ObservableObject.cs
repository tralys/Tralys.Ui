using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Tralys.Ui.Mvvm;

/// <summary>A view model base whose change notifications allocate nothing once warm: each raise passes a cached
/// <see cref="PropertyChangedEventArgs"/>. Its <see cref="ObservablePropertyAttribute"/> properties report their reads
/// to <see cref="Observation"/>, and tell the observers that read them of each change.</summary>
public abstract class ObservableObject : INotifyPropertyChanged, IObservable
{
    ObserverSet _observers;

    public event PropertyChangedEventHandler? PropertyChanged;

    ref ObserverSet IObservable.Observers => ref _observers;

    /// <summary>Tells the observers that read observable property <paramref name="property"/> that it changed.</summary>
    protected void NotifyObservers(int property) => _observers.Notify(this, property);

    /// <summary>Stores <paramref name="value"/> in <paramref name="field"/> and raises <paramref name="args"/> when
    /// it differs from what the field held.</summary>
    /// <returns>Whether the value changed.</returns>
    protected bool SetProperty<T>(ref T field, T value, PropertyChangedEventArgs args)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(args);
        return true;
    }

    /// <summary>As <see cref="SetProperty{T}(ref T, T, PropertyChangedEventArgs)"/>, raising the cached args of
    /// <paramref name="propertyName"/>.</summary>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string propertyName = ""
    ) => SetProperty(ref field, value, PropertyChangedArgs.For(propertyName));

    /// <summary>As <see cref="SetProperty{T}(ref T, T, string)"/>, raising each of <paramref name="dependents"/> too
    /// when the value changed, for properties derived from this one.</summary>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        ReadOnlySpan<string> dependents,
        [CallerMemberName] string propertyName = ""
    )
    {
        if (!SetProperty(ref field, value, propertyName))
            return false;

        foreach (var dependent in dependents)
            OnPropertyChanged(dependent);
        return true;
    }

    protected void OnPropertyChanged(PropertyChangedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        PropertyChanged?.Invoke(this, args);
    }

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
        OnPropertyChanged(PropertyChangedArgs.For(propertyName));
}

/// <summary>One <see cref="PropertyChangedEventArgs"/> per property name, made the first time the name is asked for and
/// shared from then on.</summary>
public static class PropertyChangedArgs
{
    static readonly ConcurrentDictionary<string, PropertyChangedEventArgs> Cache = new(
        StringComparer.Ordinal
    );

    public static PropertyChangedEventArgs For(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        return Cache.GetOrAdd(propertyName, static name => new PropertyChangedEventArgs(name));
    }
}

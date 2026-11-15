namespace Tralys.Ui.Docking;

/// <summary>What registers a panel with a <see cref="DockManager"/>: the view model behind it.</summary>
public interface IDockSource
{
    DockElement? DockElement { get; set; }

    string ContentId { get; }

    /// <summary>Copied onto the element at registration; a <see cref="DockSource"/> keeps it in step after.</summary>
    string Title { get; }

    /// <summary>The element's <see cref="DockElement.Content"/>, which a <see cref="Dock"/>'s content slot draws.</summary>
    object Content { get; }

    /// <summary>Where the element docks when it is shown with no remembered position.</summary>
    DockPosition DefaultPosition { get; }

    /// <summary>Copied onto the element at registration, so its tab shows the unsaved-changes dot; a
    /// <see cref="DockSource"/> keeps it in step after.</summary>
    bool IsDirty { get; }

    void OnClosed();

    /// <summary>Called when a closed element is shown again, by whichever path docks or floats it, so a source that
    /// tears down in <see cref="OnClosed"/> rebuilds here.</summary>
    void OnReopened() { }
}

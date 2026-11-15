using System.ComponentModel;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Docking;

/// <summary>A view model behind one panel of a <see cref="DockManager"/>, its element's
/// <see cref="DockElement.Content"/>, which a <see cref="Dock"/>'s content slot draws. Once <see cref="DockManager.Register(IDockSource, DockType)"/> registers it, its
/// <see cref="Title"/> and <see cref="IsDirty"/> reach the element as they change, and <see cref="IsVisible"/>
/// follows the element's state.</summary>
public abstract partial class DockSource : ObservableObject, IDockSource
{
    private static readonly PropertyChangedEventArgs DockElementArgs = PropertyChangedArgs.For(
        nameof(DockElement)
    );
    private static readonly PropertyChangedEventArgs IsVisibleArgs = PropertyChangedArgs.For(
        nameof(IsVisible)
    );

    private readonly PropertyChangedEventHandler _onElementChanged;
    private DockElement? _element;

    /// <param name="contentId">The element's ID, which a saved layout names it by.</param>
    /// <param name="title">The tab's title.</param>
    protected DockSource(string contentId, string title)
        : this(contentId, title, null) { }

    private protected DockSource(string contentId, string title, object? content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);
        ArgumentNullException.ThrowIfNull(title);
        ContentId = contentId;
        Title = title;
        Content = content ?? this;
        _onElementChanged = OnElementChanged;
    }

    public string ContentId { get; }

    /// <summary>The tab's title; a change reaches the element before this source reports it.</summary>
    [ObservableProperty]
    public partial string Title { get; protected set; }

    /// <summary>Drives the tab's unsaved-changes dot; a change reaches the element before this source reports
    /// it.</summary>
    [ObservableProperty]
    public partial bool IsDirty { get; protected set; }

    /// <summary>The element registering this source made; null until then.</summary>
    public DockElement? DockElement => _element;

    DockElement? IDockSource.DockElement
    {
        get => _element;
        set
        {
            if (ReferenceEquals(_element, value))
                return;

            _element?.PropertyChanged -= _onElementChanged;
            _element = value;
            value?.PropertyChanged += _onElementChanged;
            OnPropertyChanged(DockElementArgs);
            OnPropertyChanged(IsVisibleArgs);
        }
    }

    /// <summary>Whether the element is docked or floating, for a menu to check. Setting it shows and activates the
    /// element, or hides it without closing it; before registration it is false and setting it does nothing.</summary>
    public bool IsVisible
    {
        get => _element is { State: not DockState.Hidden };
        set
        {
            if (value)
                _element?.Show();
            else
                _element?.Hide();
        }
    }

    public abstract DockPosition DefaultPosition { get; }

    /// <summary>What the element's tab presents: this source, or the content a
    /// <see cref="DockSource{TContent}"/> was made with.</summary>
    public object Content { get; }

    public virtual void OnClosed() { }

    public virtual void OnReopened() { }

    partial void OnTitleChanged(string oldValue, string newValue) => _element?.Title = newValue;

    partial void OnIsDirtyChanged(bool oldValue, bool newValue) => _element?.IsDirty = newValue;

    private void OnElementChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DockElement.State))
            OnPropertyChanged(IsVisibleArgs);
    }
}

/// <summary>A <see cref="DockSource"/> whose element's content is a view model of its own rather than the
/// source.</summary>
public abstract class DockSource<TContent> : DockSource
    where TContent : class
{
    /// <param name="contentId">The element's ID, which a saved layout names it by.</param>
    /// <param name="title">The tab's title.</param>
    /// <param name="content">What the tab presents.</param>
    protected DockSource(string contentId, string title, TContent content)
        : base(contentId, title, content ?? throw new ArgumentNullException(nameof(content)))
    {
        Content = content;
    }

    /// <summary>What the element's tab presents.</summary>
    public new TContent Content { get; }
}

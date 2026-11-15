using System.ComponentModel;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Docking;

/// <summary>A panel of a <see cref="DockManager"/>: a tab in one of its groups while docked or floating, else
/// hidden.</summary>
public sealed class DockElement : INotifyPropertyChanged, IObservable
{
    const int TitleBit = 0;
    const int StateBit = 1;
    const int IsDirtyBit = 2;
    const int CanCloseBit = 3;
    const int ContentBit = 4;

    private static readonly PropertyChangedEventArgs TitleArgs = PropertyChangedArgs.For(
        nameof(Title)
    );
    private static readonly PropertyChangedEventArgs StateArgs = PropertyChangedArgs.For(
        nameof(State)
    );
    private static readonly PropertyChangedEventArgs PositionArgs = PropertyChangedArgs.For(
        nameof(Position)
    );
    private static readonly PropertyChangedEventArgs IsDirtyArgs = PropertyChangedArgs.For(
        nameof(IsDirty)
    );
    private static readonly PropertyChangedEventArgs CanCloseArgs = PropertyChangedArgs.For(
        nameof(CanClose)
    );
    private static readonly PropertyChangedEventArgs ContentArgs = PropertyChangedArgs.For(
        nameof(Content)
    );
    private static readonly PropertyChangedEventArgs ParentArgs = PropertyChangedArgs.For(
        nameof(Parent)
    );
    private static readonly PropertyChangedEventArgs ManagerArgs = PropertyChangedArgs.For(
        nameof(Manager)
    );

    private string _title = string.Empty;
    private DockGroup? _parent;
    private DockPosition _position = DockPosition.None;
    private DockManager? _manager;
    private ActiveElementChangedEventArgs? _activated;
    private ObserverSet _observers;

    ref ObserverSet IObservable.Observers => ref _observers;

    public string Id { get; }

    internal ActiveElementChangedEventArgs Activated => _activated ??= new(this);

    public string Title
    {
        get
        {
            Observation.Read(this, TitleBit);
            return _title;
        }
        set
        {
            if (_title != value)
            {
                _title = value;
                _observers.Notify(this, TitleBit);
                Raise(TitleArgs);
            }
        }
    }

    public DockState State
    {
        get
        {
            Observation.Read(this, StateBit);
            return StateOf(_parent);
        }
    }

    static DockState StateOf(DockGroup? parent) =>
        parent is null ? DockState.Hidden
        : parent.Position == DockPosition.Floating ? DockState.Floating
        : DockState.Docked;

    /// <summary>Where the element sits, or last sat while hidden.</summary>
    public DockPosition Position
    {
        get => _position;
        internal set
        {
            if (_position != value)
            {
                _position = value;
                Raise(PositionArgs);
            }
        }
    }

    public DockType DockType { get; }

    private bool _isDirty;

    /// <summary>Drives the tab's unsaved-changes dot.</summary>
    public bool IsDirty
    {
        get
        {
            Observation.Read(this, IsDirtyBit);
            return _isDirty;
        }
        set
        {
            if (_isDirty != value)
            {
                _isDirty = value;
                _observers.Notify(this, IsDirtyBit);
                Raise(IsDirtyArgs);
            }
        }
    }

    private bool _canClose = true;

    public bool CanClose
    {
        get
        {
            Observation.Read(this, CanCloseBit);
            return _canClose;
        }
        set
        {
            if (_canClose != value)
            {
                _canClose = value;
                _observers.Notify(this, CanCloseBit);
                Raise(CanCloseArgs);
            }
        }
    }

    private object? _content;

    public object? Content
    {
        get
        {
            Observation.Read(this, ContentBit);
            return _content;
        }
        set
        {
            if (!ReferenceEquals(_content, value))
            {
                _content = value;
                _observers.Notify(this, ContentBit);
                Raise(ContentArgs);
            }
        }
    }

    public DockGroup? Parent
    {
        get => _parent;
        internal set
        {
            if (_parent == value)
                return;
            var before = StateOf(_parent);
            _parent = value;
            Raise(ParentArgs);
            if (StateOf(value) == before)
                return;
            _observers.Notify(this, StateBit);
            Raise(StateArgs);
            if (before == DockState.Hidden && IsClosed)
            {
                IsClosed = false;
                Source?.OnReopened();
            }
        }
    }

    internal bool IsClosed { get; set; }

    public DockManager? Manager
    {
        get => _manager;
        internal set
        {
            if (_manager != value)
            {
                _manager = value;
                Raise(ManagerArgs);
            }
        }
    }

    public IDockSource? Source { get; internal set; }

    /// <summary>Raised with cached args, so following an element allocates nothing per change. A move between groups
    /// never reports it <see cref="DockState.Hidden"/> on the way. <see cref="Title"/>, <see cref="State"/>,
    /// <see cref="IsDirty"/>, <see cref="CanClose"/> and <see cref="Content"/> also report their reads, so a component
    /// that read one renders again as it changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    public DockElement(string id, string title, DockType type = DockType.Tool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        _title = title;
        DockType = type;
    }

    public DockElement(string title, DockType type = DockType.Tool)
        : this(Guid.NewGuid().ToString("N"), title, type) { }

    private void Raise(PropertyChangedEventArgs args) => PropertyChanged?.Invoke(this, args);

    public void Show()
    {
        if (Manager is { } manager)
        {
            if (_parent is null)
            {
                manager.Dock(this, ResolveShowPosition());
            }
            Activate();
        }
    }

    // None and Floating have no root group to dock into.
    internal DockPosition ResolveShowPosition()
    {
        if (Position.IsRootRegion())
            return Position;
        if (Source?.DefaultPosition is { } preferred && preferred.IsRootRegion())
            return preferred;
        return DockType == DockType.Document ? DockPosition.Center : DockPosition.Right;
    }

    public void Hide() => Parent?.Remove(this);

    /// <summary>A user close. False while the element stays open: it cannot close, or the manager's close
    /// handler took the close over.</summary>
    public bool Close() => Manager?.Close([this]) ?? false;

    /// <summary>Makes the element the active one of its group and its manager; a hidden element has no tab to make
    /// active, so nothing changes.</summary>
    public void Activate()
    {
        if (Parent is not { } parent)
            return;

        Manager?.SetActiveElement(this);
        parent.ActiveElement = this;
    }

    public override string ToString() => $"DockElement[{Id}]: {Title} ({DockType}, {State})";
}

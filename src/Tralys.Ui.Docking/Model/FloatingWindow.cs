using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Docking;

/// <summary>A floating group's place: in the dock's own layer, or in a window of its own once torn out. Its bounds
/// report their reads, so a component that read them renders again as they change.</summary>
public sealed class FloatingWindow : IObservable
{
    const float MinWidth = 120f;
    const float MinHeight = 80f;

    private ObserverSet _observers;

    ref ObserverSet IObservable.Observers => ref _observers;

    public DockGroup Group { get; }

    private float _x;
    private float _y;
    private float _width;
    private float _height;

    public float X
    {
        get
        {
            Observation.Read(this, 0);
            return _x;
        }
        set => SetBoundsValue(ref _x, value);
    }

    public float Y
    {
        get
        {
            Observation.Read(this, 0);
            return _y;
        }
        set => SetBoundsValue(ref _y, value);
    }

    public float Width
    {
        get
        {
            Observation.Read(this, 0);
            return _width;
        }
        set => SetBoundsValue(ref _width, value);
    }

    public float Height
    {
        get
        {
            Observation.Read(this, 0);
            return _height;
        }
        set => SetBoundsValue(ref _height, value);
    }

    /// <summary>The dock's extent once it has laid out, which a drag or a resize keeps the window inside.</summary>
    public float BoundsWidth { get; private set; }

    /// <inheritdoc cref="BoundsWidth"/>
    public float BoundsHeight { get; private set; }

    public float ClampWidth(float width) => MathF.Max(width, MinWidth);

    public float ClampHeight(float height) => MathF.Max(height, MinHeight);

    internal DockManager? Manager { get; set; }

    public event EventHandler? BoundsChanged;

    public FloatingWindow(DockGroup group, float x, float y, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(group);

        Group = group;
        _x = x;
        _y = y;
        _width = width;
        _height = height;

        group.Position = DockPosition.Floating;
        group.Window = this;
    }

    [FramePath]
    internal void KeepWithin(float width, float height)
    {
        BoundsWidth = width;
        BoundsHeight = height;
        X = ClampOrigin(_x, ClampWidth(_width), width);
        Y = ClampOrigin(_y, ClampHeight(_height), height);
    }

    static float ClampOrigin(float origin, float extent, float bound) =>
        bound <= 0f ? origin : Math.Clamp(origin, 0f, MathF.Max(0f, bound - extent));

    private void SetBoundsValue(ref float field, float value)
    {
        if (field == value)
            return;

        field = value;
        _observers.Notify(this, 0);
        BoundsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A user close of every tab in the window: a document closes as <see cref="DockManager.Close"/> closes
    /// it, a tool the close handler takes goes to it, the other tools hide, and an element that cannot close docks back
    /// where <see cref="DockElement.Show"/> would put it. The window goes once it is empty, so it stays open while the
    /// handler keeps any of its tabs; closing it again once gone does nothing.</summary>
    public void RequestClose()
    {
        if (Manager is not { } manager)
            return;

        List<DockElement>? closing = null;
        foreach (var element in new List<DockElement>(Group.Elements))
        {
            if (!element.CanClose)
                manager.Dock(element, element.ResolveShowPosition());
            else if (
                element.DockType == DockType.Document
                || manager.CloseHandler?.Handles(element) == true
            )
                (closing ??= []).Add(element);
            else
                element.Hide();
        }

        if (closing is not null)
            manager.Close(closing);
    }

    // For a caller partway through a layout change, which raises its own notification.
    internal void Discard()
    {
        if (Manager is { } manager)
        {
            Manager = null;
            manager.RemoveFloatingWindow(this);
            if (ReferenceEquals(manager.ActiveElement?.Parent, Group))
                manager.SetActiveElement(null);
        }

        Group.Clear();
    }

    public override string ToString() => $"FloatingWindow: ({X}, {Y}) {Width}x{Height}";
}

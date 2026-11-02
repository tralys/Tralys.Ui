using System.ComponentModel;
using Tralys.Ui.Mvvm;

namespace Tralys.Ui;

/// <summary>A node of a tree as a row of the <see cref="TreeRows{T}"/> showing it: the tree sets <see cref="Depth"/> and
/// <see cref="HasChildren"/> as it lays the row out, and <see cref="IsExpanded"/>, which a binding follows both ways,
/// shows the row's children beneath it, the tree laying its rows out again as it changes. A row keeps whether it is
/// expanded while a collapsed row above it hides it, and belongs to the tree that last laid it out. Reading any of the
/// three reports to <see cref="Observation"/>, and a change tells the observers that read it.</summary>
public abstract class TreeRow : ObservableObject
{
    static readonly PropertyChangedEventArgs DepthChanged = new(nameof(Depth));
    static readonly PropertyChangedEventArgs HasChildrenChanged = new(nameof(HasChildren));
    static readonly PropertyChangedEventArgs IsExpandedChanged = new(nameof(IsExpanded));

    int _depth;
    bool _hasChildren;
    bool _isExpanded;

    /// <summary>How many rows lie above this one in the tree, 0 for a row at its top, as of the tree's last
    /// layout.</summary>
    public int Depth
    {
        [FramePath]
        get
        {
            Observation.Read(this, 0);
            return _depth;
        }
        [FramePath]
        internal set
        {
            if (SetProperty(ref _depth, value, DepthChanged))
                NotifyObservers(0);
        }
    }

    /// <summary>Whether expanding the row shows rows beneath it: whether it has children, and under the tree's filter
    /// whether any of them shows.</summary>
    public bool HasChildren
    {
        [FramePath]
        get
        {
            Observation.Read(this, 1);
            return _hasChildren;
        }
        [FramePath]
        internal set
        {
            if (SetProperty(ref _hasChildren, value, HasChildrenChanged))
                NotifyObservers(1);
        }
    }

    /// <summary>Whether the row's children show beneath it.</summary>
    public bool IsExpanded
    {
        [FramePath]
        get
        {
            Observation.Read(this, 2);
            return _isExpanded;
        }
        [FramePath]
        set
        {
            if (!SetProperty(ref _isExpanded, value, IsExpandedChanged))
                return;
            NotifyObservers(2);
            Owner?.OnExpandedChanged();
        }
    }

    internal ITreeRowsOwner? Owner { get; set; }
}

interface ITreeRowsOwner
{
    [FramePath]
    void OnExpandedChanged();

    void EndDeferral();
}

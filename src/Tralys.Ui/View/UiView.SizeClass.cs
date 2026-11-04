namespace Tralys.Ui;

/// <summary>How much room a view's layout has, ordered from least to most.</summary>
public enum SizeClass : byte
{
    /// <summary>Under 640 wide or 480 tall.</summary>
    Compact,

    /// <summary>Under 960 wide or 720 tall.</summary>
    Medium,

    /// <summary>At least 960 wide and 720 tall.</summary>
    Expanded,
}

public sealed partial class UiView
{
    const float SizeClassBand = 24f;

    SizeClass? _sizeClass;
    SizeClass? _sizeClassOverride;
    bool _classified;

    /// <summary>The class the size inside <see cref="SafeInsets"/> fell in at the last update, holding until the size
    /// passes a threshold by 24 units, and while a touch is down. Before the first update it is the class the view
    /// opens in, which that update keeps. Read through <see cref="Ui.SizeClass"/>.</summary>
    public SizeClass SizeClass => _sizeClass ?? NextSizeClass();

    /// <summary>The class to lay out in whatever the size, as when previewing a phone layout on a desktop; null
    /// classifies the size afresh.</summary>
    public SizeClass? SizeClassOverride
    {
        get => _sizeClassOverride;
        set
        {
            VerifyAccess();
            _sizeClassOverride = value;
        }
    }

    /// <summary>Raised from the update that changes <see cref="SizeClass"/>, once the view reads the new class and before
    /// that update renders, so what a handler changes, such as a model picking a presentation by class, renders and lays
    /// out in that update. The first update raises nothing.</summary>
    public event EventHandler? SizeClassChanged;

    internal SizeClass ReadSizeClass(Builder builder)
    {
        if (builder.CurrentHost is { } host)
            host.EnvReads |= EnvReads.SizeClass;
        return SizeClass;
    }

    bool SizeClassDue => NextSizeClass() != SizeClass;

    SizeClass NextSizeClass()
    {
        if (_sizeClassOverride is { } forced)
            return forced;
        if (!_classified || _sizeClass is not { } shown)
            return Classify(UsableSize());
        return AnyTouchDown() ? shown : Classify(UsableSize(), shown);
    }

    void UpdateSizeClass()
    {
        var shown = SizeClass;
        var next = NextSizeClass();
        _classified = _sizeClassOverride is null;
        _sizeClass = next;
        if (next == shown)
            return;
        InvalidateEnv(_root, EnvReads.SizeClass);
        try
        {
            SizeClassChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception fault) when (Context.OnFault is not null)
        {
            Context.OnFault(fault);
        }
    }

    static SizeClass Classify(Size usable) =>
        usable.Height < 480f || usable.Width < 640f ? SizeClass.Compact
        : usable.Height < 720f || usable.Width < 960f ? SizeClass.Medium
        : SizeClass.Expanded;

    static SizeClass Classify(Size usable, SizeClass shown)
    {
        var smaller = Classify(
            new Size(usable.Width - SizeClassBand, usable.Height - SizeClassBand)
        );
        var larger = Classify(
            new Size(usable.Width + SizeClassBand, usable.Height + SizeClassBand)
        );
        return shown >= smaller && shown <= larger ? shown : Classify(usable);
    }

    Size UsableSize() =>
        new(
            MathF.Max(0f, _size.Width - _safeInsets.Left - _safeInsets.Right),
            MathF.Max(0f, _size.Height - _safeInsets.Top - _safeInsets.Bottom)
        );

    bool AnyTouchDown()
    {
        for (var i = 0; i < _pointers.Count; i++)
        {
            var state = _pointers[i];
            if (state.Pointer.Kind == PointerKind.Touch && state.InContact)
                return true;
        }
        return false;
    }
}

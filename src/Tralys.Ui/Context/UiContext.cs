using Tralys.Ui.Fonts;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui;

/// <summary>What every view on one thread shares: the dispatcher, text services, textures, theme, skin, density and
/// clock. Setting a value it already has does nothing.</summary>
public sealed partial class UiContext : IDisposable
{
    readonly List<UiView> _views = [];
    Theme _theme;
    Density _density;
    bool _clockStarted;
    float _animationSpeed = 1f;

    /// <param name="text">The fonts and glyph atlas text is set with.</param>
    /// <param name="textures">Resolves image keys; null resolves none.</param>
    /// <param name="theme">The theme; <see cref="Themes.Dark"/> when null.</param>
    /// <param name="density">The control density, which only the app sets.</param>
    public UiContext(
        TextServices text,
        ITextureProvider? textures = null,
        Theme? theme = null,
        Density density = default
    )
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        Textures = textures;
        _theme = theme ?? Themes.Dark;
        _density = density;
    }

    public Dispatcher Dispatcher { get; } = new();

    public TextServices Text { get; }

    public ITextureProvider? Textures { get; }

    /// <summary>Reports a fault in a render, a handler, posted work or an effect, and lets what it interrupted carry
    /// on; without one, the fault goes to the caller.</summary>
    public Action<Exception>? OnFault { get; set; }

    /// <summary>The theme; a change re-renders every mounted host that read it.</summary>
    /// <exception cref="InvalidOperationException">Set from another thread.</exception>
    public Theme Theme
    {
        get => _theme;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Dispatcher.VerifyAccess();
            if (ReferenceEquals(_theme, value))
                return;
            _theme = value;
            foreach (var view in _views)
                view.EnvironmentChanged(EnvReads.Theme);
        }
    }

    /// <summary>The control density; a change re-renders every host that read it and lays everything out again.</summary>
    public Density Density
    {
        get => _density;
        set
        {
            Dispatcher.VerifyAccess();
            if (_density == value)
                return;
            _density = value;
            foreach (var view in _views)
                view.EnvironmentChanged(EnvReads.Density);
        }
    }

    /// <summary>Whether wheel steps and animated scroll requests glide; off, they move at once. On by default.</summary>
    public bool SmoothScrolling { get; set; } = true;

    /// <summary>The time the last <see cref="Advance"/> moved to.</summary>
    public TimeSpan Now { get; private set; }

    /// <summary>How fast animations, transitions, loops and sprites run against real time: 0.5 halves their rate, 0
    /// holds them. Input, scrolling, the caret blink and timers keep real time.</summary>
    public float AnimationSpeed
    {
        get => _animationSpeed;
        set
        {
            Dispatcher.VerifyAccess();
            _animationSpeed =
                value >= 0f && float.IsFinite(value)
                    ? value
                    : throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "An animation speed is finite and not negative."
                    );
        }
    }

    /// <summary>Lands every transition at its end at once, and shows loops at rest and sprites on their first
    /// frame.</summary>
    public bool ReducedMotion { get; set; }

    internal double AnimationTime { get; private set; }

    /// <summary>Moves the clock to <paramref name="now"/>, which never goes back, and with it every animation,
    /// transition and loop, invalidating their hosts, and every sprite, drawing again those whose frame turns, and
    /// starts a frame of the glyph atlas.</summary>
    [FramePath]
    public void Advance(TimeSpan now)
    {
        Dispatcher.VerifyAccess();
        if (_clockStarted && now < Now)
            throw Faults.OutOfRange(nameof(now), now, "The clock never goes back.");
        if (_clockStarted)
            AnimationTime += (now - Now).TotalSeconds * _animationSpeed;
        _clockStarted = true;
        Now = now;
        Text.Atlas.BeginFrame();
        if (ClockRang && Interlocked.Exchange(ref _clockRang, 0) != 0)
            WakeClockReaders();
        for (var i = 0; i < _views.Count; i++)
        {
            _views[i].AdvanceMotions();
            _views[i].AdvanceSprites();
            _views[i].AdvanceScroll(now);
        }
    }

    internal void Attach(UiView view) => _views.Add(view);

    internal void Detach(UiView view) => _views.Remove(view);

    internal void Report(Exception fault)
    {
        if (OnFault is { } onFault)
            onFault(fault);
        else
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(fault);
    }

    /// <summary>Disposes every view on the context.</summary>
    public void Dispose()
    {
        for (var i = _views.Count - 1; i >= 0; i--)
            _views[i].Dispose();
        _timer?.Dispose();
        _timer = null;
    }
}

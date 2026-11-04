using System.Runtime.CompilerServices;
using Tralys.Ui.Theming;

namespace Tralys.Ui;

/// <summary>The render scope: the view, the current host and the open node. A ref struct, so it can't be stored or
/// captured, and every callback is static.</summary>
[FramePath]
public readonly ref partial struct Ui
{
    internal readonly Builder Builder;

    internal Ui(Builder builder) => Builder = builder;

    /// <summary>The theme and density at this component's position. Tracked.</summary>
    public UiTheme Theme => Builder.View.ReadTheme(Builder);

    /// <summary>The density at this component's position. Tracked.</summary>
    public Density Density => Builder.View.ReadDensity(Builder);

    /// <summary>The view's size class. Tracked: a change renders this component again before that update's
    /// layout.</summary>
    public SizeClass SizeClass => Builder.View.ReadSizeClass(Builder);

    /// <summary>The view's size in layout units, for what sizes itself by the screen, as CSS's viewport units do,
    /// where no box around it has that size: a drag preview, a layer. Tracked.</summary>
    public Size ViewSize => Builder.View.ReadViewSize(Builder);

    /// <summary>The view's safe-area insets. Tracked.</summary>
    public Thickness SafeInsets => Builder.View.ReadInsets(Builder).SafeInsets;

    /// <summary>How far an on-screen keyboard covers the view's bottom. Tracked.</summary>
    public float KeyboardInset => Builder.View.ReadInsets(Builder).KeyboardInset;

    public bool IsHovered => Builder.View.ReadStates(Builder, InputStates.Hovered);

    public bool IsPressed => Builder.View.ReadStates(Builder, InputStates.Pressed);

    public bool IsFocused => Builder.View.ReadStates(Builder, InputStates.Focused);

    public bool IsFocusVisible => Builder.View.ReadStates(Builder, InputStates.FocusVisible);

    public bool IsFocusWithin => Builder.View.ReadStates(Builder, InputStates.FocusWithin);

    /// <summary>The focused node when it is this component's root or inside it, else null. Tracked: renders again as
    /// focus moves from one node inside to another, as well as in and out.</summary>
    public Node? FocusedWithin => Builder.View.ReadFocusedWithin(Builder);

    public bool IsCapturing => Builder.View.ReadStates(Builder, InputStates.Capturing);

    /// <summary>Whether this component is disabled: a disabled ancestor, its root's own <c>disabled</c>, or a held
    /// state. Tracked.</summary>
    public bool IsDisabled => Builder.View.ReadStates(Builder, InputStates.Disabled);

    public bool IsSelected => Builder.View.ReadStates(Builder, InputStates.Selected);

    /// <summary>A button's look state: Disabled, then Pressed, Hover, visible focus, Normal.</summary>
    public ComponentState ComponentState
    {
        get
        {
            var states = UiView.ReadStateSet(
                Builder,
                InputStates.Disabled
                    | InputStates.Pressed
                    | InputStates.Hovered
                    | InputStates.FocusVisible
            );
            return (states & InputStates.Disabled) != 0 ? ComponentState.Disabled
                : (states & InputStates.Pressed) != 0 ? ComponentState.Pressed
                : (states & InputStates.Hovered) != 0 ? ComponentState.Hover
                : (states & InputStates.FocusVisible) != 0 ? ComponentState.Focused
                : ComponentState.Normal;
        }
    }

    /// <summary>A field's look state: Disabled, then any focus (focus within included), Hover, Normal.</summary>
    public ComponentState FieldState
    {
        get
        {
            var states = UiView.ReadStateSet(
                Builder,
                InputStates.Disabled
                    | InputStates.Focused
                    | InputStates.FocusWithin
                    | InputStates.Hovered
            );
            return (states & InputStates.Disabled) != 0 ? ComponentState.Disabled
                : (states & (InputStates.Focused | InputStates.FocusWithin)) != 0
                    ? ComponentState.Focused
                : (states & InputStates.Hovered) != 0 ? ComponentState.Hover
                : ComponentState.Normal;
        }
    }

    /// <summary>A cell of state, kept from mount to unmount, compared with <see cref="EqualityComparer{T}.Default"/>.</summary>
    public State<T> State<T>(
        T initial = default!,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) => Cell(initial, byReference: false, key, file, line);

    /// <summary>A cell of a class, compared by reference.</summary>
    public State<T> RefState<T>(
        T? initial = null,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
        where T : class => Cell(initial!, byReference: true, key, file, line);

    State<T> Cell<T>(T initial, bool byReference, UiKey key, string file, int line)
    {
        var hook = Builder.UseHook<StateHook<T>>(key, file, line, out var created);
        if (created)
        {
            var cell = Pool<State<T>>.TryRent() ?? new State<T>();
            cell.Init(Builder.CurrentHost!, initial, byReference);
            hook.Cell = cell;
        }
        return hook.Cell;
    }

    /// <summary>A value made once, on mount, and kept until unmount; an <see cref="IDisposable"/> is disposed then.</summary>
    public T Remember<T>(
        Func<T> create,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
        where T : class
    {
        var hook = Builder.UseHook<RememberHook>(key, file, line, out var created);
        if (created)
            hook.Value = create();
        return Unsafe.As<T>(hook.Value!);
    }

    /// <summary>A value made from <paramref name="dep"/>, made again when it changes.</summary>
    public T Remember<TDep, T>(
        TDep dep,
        Func<TDep, T> create,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
        where T : class
    {
        var hook = Builder.UseHook<RememberHook<TDep>>(key, file, line, out var created);
        if (created || !EqualityComparer<TDep>.Default.Equals(hook.Dep, dep))
        {
            (hook.Value as IDisposable)?.Dispose();
            hook.Dep = hook.Held.Hold(dep);
            hook.Value = create(dep);
        }
        return Unsafe.As<T>(hook.Value!);
    }

    /// <summary>A handle to pass a primitive as its <c>nodeRef</c>.</summary>
    public NodeRef Ref(
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var hook = Builder.UseHook<RefHook>(key, file, line, out var created);
        if (created)
            hook.Ref = Pool<NodeRef>.TryRent() ?? new NodeRef();
        return hook.Ref;
    }

    /// <summary>A handle to pass a scroll container as its <c>scroll</c>, to read and move its offset.</summary>
    public ScrollHandle ScrollHandle(
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var hook = Builder.UseHook<ScrollHandleHook>(key, file, line, out var created);
        if (created)
            hook.Handle = ScrollHandleHook.Rent();
        return hook.Handle;
    }

    /// <summary>Runs <paramref name="run"/> after the update's layout, on mount and whenever <paramref name="dep"/>
    /// changes; children run first.</summary>
    public void Effect<TState, TDep>(
        TState state,
        TDep dep,
        EffectFn<TState, TDep> run,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
        where TState : class
    {
        var hook = Builder.UseHook<EffectHook<TState, TDep>>(key, file, line, out var created);
        var changed = created || !EqualityComparer<TDep>.Default.Equals(hook.Dep, dep);
        hook.Live = true;
        hook.Host = Builder.CurrentHost;
        hook.State = state;
        hook.Dep = hook.Held.Hold(dep);
        hook.Fn = run;
        if (changed && !hook.Pending)
        {
            hook.Pending = true;
            Builder.CurrentHost?.PendingEffects.Add(hook);
        }
    }

    /// <summary>The wall clock: <see cref="UiContext.Time"/>'s local time. This component renders again once the local
    /// clock crosses the next multiple of <paramref name="granularity"/>, so a minute turns at :00 and a day at
    /// midnight, and asks for no frames until then.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="granularity"/> is not positive.</exception>
    public DateTimeOffset Now(TimeSpan granularity) =>
        Builder.View.Context.ReadClock(Builder.CurrentHost, granularity);

    /// <summary>Provides <paramref name="value"/> under <paramref name="key"/> to what this component renders; a change
    /// re-renders only its readers.</summary>
    public void Provide<T>(ContextKey<T> key, T value)
        where T : IEquatable<T> => Builder.View.Provide(Builder, key, value);

    /// <summary>The nearest provided value of <paramref name="key"/> above, else its fallback. Tracked.</summary>
    public T Use<T>(ContextKey<T> key)
        where T : IEquatable<T> => Builder.View.Use(Builder, key);

    /// <summary>This component's host, for callbacks that read its latest props.</summary>
    public Host<TProps> Self<TProps>(in TProps props)
        where TProps : struct, IEquatable<TProps> =>
        Builder.CurrentHost is ComponentHost<TProps> host
            ? host.Self
            : throw new InvalidOperationException(
                "Self takes the props of the component that renders."
            );

    /// <summary>Places the caller's children in the innermost open node, which holds nothing else.</summary>
    public void Children() => Builder.Children();

    /// <summary>Scopes the identity of what it holds, hooks included, to one node, so a helper with several calls
    /// keeps them apart when used twice.</summary>
    public Scope Fragment(
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var b = Builder;
        if (b.Discarding)
            return default;
        var depth = b.FrameDepth;
        var hooks = b.HookDepth;
        var node = b.Claim(NodeKind.Fragment, null, key, file, line, out _);
        b.Push(node);
        b.PushHooks(node);
        return new Scope(b, depth, hooks);
    }

    /// <summary>Formats <paramref name="text"/> into the frame's arena, in the invariant culture; valid until the
    /// update's render ends.</summary>
    public TextValue Format([InterpolatedStringHandlerArgument("")] ref UiFormat text) =>
        text.ToText();

    /// <summary>Renders a component; what generated calls lower to.</summary>
    public void Component<TComponent, TProps>(
        in TProps props,
        in Placement place,
        UiKey key,
        string file,
        int line
    )
        where TComponent : Component<TProps>, new()
        where TProps : struct, IEquatable<TProps> =>
        Builder.Component<TComponent, TProps>(props, place, key, file, line);

    /// <summary>Renders a component with children; the scope's block fills its outlet.</summary>
    public Scope ComponentWithChildren<TComponent, TProps>(
        scoped in TProps props,
        scoped in Placement place,
        UiKey key,
        string file,
        int line
    )
        where TComponent : Component<TProps>, new()
        where TProps : struct, IEquatable<TProps>
    {
        var b = Builder;
        var depth = b.FrameDepth;
        var hooks = b.HookDepth;
        var host = b.Component<TComponent, TProps>(props, place, key, file, line);
        if (host?.Outlet is { IsMounted: true } outlet)
            b.Push(outlet);
        else
            b.PushDiscard();
        return new Scope(b, depth, hooks);
    }
}

/// <summary>An open node; disposing it closes the node, unmounting what the block no longer wrote.</summary>
[FramePath]
public readonly ref struct Scope
{
    readonly Builder? _builder;
    readonly int _depth;
    readonly int _hooks;

    internal Scope(Builder builder, int depth, int hooks)
    {
        _builder = builder;
        _depth = depth;
        _hooks = hooks;
    }

    public void Dispose() => _builder?.Close(_depth, _hooks);
}

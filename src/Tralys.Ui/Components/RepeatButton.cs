using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct RepeatButtonProps
{
    public TextValue Text { get; init; }

    public Slot Content { get; init; }

    public IconGlyph IconLeft { get; init; }

    public ComponentSize? Size { get; init; }

    /// <summary>The scheme; <see cref="ColorScheme.Primary"/> by default.</summary>
    public ColorScheme? Scheme { get; init; }

    public ComponentVariant Variant { get; init; }

    /// <summary>Seconds a held press waits before it clicks again: 0.5 by default, and never negative.</summary>
    public float? Delay { get; init; }

    /// <summary>Seconds between later clicks: 0.033 by default, and more than 0.</summary>
    public float? Interval { get; init; }

    public bool Disabled { get; init; }

    public Length Width { get; init; }

    /// <summary>Space between the border and the content; the size's padding by default.</summary>
    public Thickness? Padding { get; init; }

    public Callback OnClick { get; init; }
}

/// <summary>A <see cref="Button"/> that clicks as a press lands, then again once <see cref="RepeatButtonProps.Delay"/>
/// has passed and every <see cref="RepeatButtonProps.Interval"/> after, while the press stays over it, on the
/// context's clock. Space held while it has the focus clicks on the same timing, not on the key's repeats, until the
/// key comes up, another key goes down or the focus moves. Each click runs
/// <see cref="RepeatButtonProps.OnClick"/>.</summary>
public sealed class RepeatButton : Component<RepeatButtonProps>
{
    const float DefaultDelay = 0.5f;
    const float DefaultInterval = 0.033f;

    protected override void Render(Ui ui, in RepeatButtonProps p)
    {
        if (p.Delay is { } delay && !(delay >= 0f))
            throw Refused(nameof(p.Delay), delay, "A repeat button's delay is 0 or more.");
        if (p.Interval is { } interval && !(interval > 0f))
            throw Refused(
                nameof(p.Interval),
                interval,
                "A repeat button's interval is more than 0."
            );
        var t = ui.Theme;
        var m = t.Metrics(p.Size ?? ComponentSize.Md);
        ui.CanRun(p.OnClick);
        var model = ui.Remember(static () => new Repeater());
        model.Host = ui.Self(p);
        model.Root = ui.Ref();
        ui.Effect(model, 0, static (m, _, cx) => m.View = cx.View);
        var look = ui.Animated(
            t.Palette(p.Scheme ?? ColorScheme.Primary, p.Variant, ui.ComponentState)
        );
        var radius = t.Radius(RadiusToken.Md);
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                gap: m.Gap,
                width: p.Width,
                minHeight: m.Height,
                padding: p.Padding ?? ButtonParts.Inside(m.PaddingX, 1f),
                background: look.Background,
                border: new Border(1f, look.Border),
                radius: radius,
                fontSize: m.FontSize,
                fontWeight: t.Weight(WeightToken.Interface),
                opacity: ui.IsDisabled ? 0.5f : null,
                cursor: Cursors.Hand,
                onClick: model.Click,
                pressMode: PressMode.Press,
                onPointerDown: model.PointerDown,
                onPointerUp: model.PointerUp,
                onPointerCancel: model.PointerUp,
                onKeyDown: model.KeyDown,
                onKeyUp: model.KeyUp,
                onFocusChange: model.FocusChange,
                nodeRef: model.Root
            )
        )
        {
            ui.Label(
                text: p.Text,
                content: p.Content,
                iconLeft: p.IconLeft,
                iconRight: default,
                iconSize: m.FontSize,
                gap: m.Gap,
                color: look.Foreground
            );
            if (ui.IsFocusVisible)
                ui.FocusRing(radius: radius);
        }
    }

    static ArgumentOutOfRangeException Refused(string prop, float value, string message) =>
        new(prop, value, message);

    [FramePath]
    sealed class Repeater
    {
        internal readonly Callback Click;
        internal readonly Handler<PointerEvent> PointerDown;
        internal readonly Handler<PointerEvent> PointerUp;
        internal readonly Handler<KeyEvent> KeyDown;
        internal readonly Handler<KeyEvent> KeyUp;
        internal readonly Callback<FocusChange> FocusChange;
        internal Host<RepeatButtonProps> Host;
        internal NodeRef Root = null!;
        internal UiView? View;
        TimeSpan _next;
        bool _pointer;
        bool _space;
        bool _ticking;

        public Repeater()
        {
            Click = Callback.Of(this, static m => m.Run());
            PointerDown = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Pressed(ref e));
            PointerUp = Handler<PointerEvent>.Of(this, static (m, ref e) => m._pointer = false);
            KeyDown = Handler<KeyEvent>.Of(this, static (m, ref e) => m.Down(ref e));
            KeyUp = Handler<KeyEvent>.Of(this, static (m, ref e) => m.Up(ref e));
            FocusChange = Callback<FocusChange>.Of(
                this,
                static (m, change) =>
                {
                    if (!change.Focused)
                        m._space = false;
                }
            );
        }

        void Run()
        {
            ref readonly var p = ref Host.Props;
            p.OnClick.Invoke();
        }

        void Pressed(ref PointerEvent e)
        {
            if (e.Button != MouseButton.Left && e.Pointer.Kind == PointerKind.Mouse)
                return;
            e.Capture();
            var repeating = _pointer || _space;
            _pointer = true;
            if (!repeating)
                Start();
        }

        void Down(ref KeyEvent e)
        {
            if (e.Key != Key.Space)
            {
                _space = false;
                return;
            }
            if (e.Modifiers != ModifierKeys.None)
                return;
            e.PreventDefault();
            if (e.IsRepeat || _space)
                return;
            var repeating = _pointer;
            _space = true;
            Run();
            if (!repeating)
                Start();
        }

        void Up(ref KeyEvent e)
        {
            if (e.Key == Key.Space)
                _space = false;
        }

        void Start()
        {
            if (View is not { } view)
                return;
            _next = view.Context.Now + Seconds(Host.Props.Delay ?? DefaultDelay);
            Tick(view);
        }

        void OnTick()
        {
            _ticking = false;
            if (!Host.IsMounted || View is not { } view || Root.Node is not { } root)
            {
                _pointer = _space = false;
                return;
            }
            if ((root.States & InputStates.Disabled) != 0)
                _pointer = _space = false;
            if (!_pointer && !_space)
                return;
            var now = view.Context.Now;
            if ((_space || (root.States & InputStates.Pressed) != 0) && now >= _next)
            {
                _next = now + Seconds(Host.Props.Interval ?? DefaultInterval);
                Run();
            }
            Tick(view);
        }

        void Tick(UiView view)
        {
            if (_ticking || (!_pointer && !_space))
                return;
            _ticking = true;
            view.Context.Dispatcher.Post(this, static m => m.OnTick());
        }

        static TimeSpan Seconds(float seconds) => TimeSpan.FromSeconds(seconds);
    }
}

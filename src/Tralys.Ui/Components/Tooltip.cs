using System.Numerics;
using System.Runtime.CompilerServices;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct TooltipProps
{
    /// <summary>What a worded tooltip says; empty or blank shows nothing.</summary>
    public TextValue Text { get; init; }

    /// <summary>What a rich tooltip shows when it has no <see cref="Text"/>.</summary>
    public Slot Content { get; init; }

    /// <summary>Whether the tooltip shows; null leaves it to hover, a finger's hold and keyboard focus.</summary>
    public bool? Open { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    /// <summary>Where the tooltip opens against what is written inside it; <see cref="PlacementMode.Top"/> by
    /// default.</summary>
    public PlacementMode? Placement { get; init; }

    /// <summary>Seconds a pointer rests on the trigger before the tooltip shows: 0.3 by default, and never
    /// negative.</summary>
    public float? Delay { get; init; }

    /// <summary>The frame's rung, which sets a worded tooltip's font and the width it wraps at;
    /// <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }

    /// <summary>The node the tip opens against in place of what is written inside it, such as a link inside a rich text
    /// that nothing can be written around; <see cref="Open"/> then shows it, from the link's
    /// <c>onHoverChange</c>.</summary>
    public NodeRef? Anchor { get; init; }
}

/// <summary>Shows a tip in a floating frame against what is written inside it, its trigger, above it by default and
/// 4 units off, taking no input. A view shows one at a time: the mouse resting on a trigger shows its tip after
/// <see cref="TooltipProps.Delay"/>, and moving onto another while one shows, or within half a second after, shows that
/// one at once; leaving a trigger nested in another hands the tip back to the outer one still under the pointer. A
/// finger held on a trigger shows its tip after the delay, and it goes as the finger lifts or strays past a tap.
/// Keyboard or gamepad focus moving inside a trigger shows its tip at once. It draws nothing while its trigger is
/// hidden or collapsed, and goes as its trigger is clipped out of view. <see cref="TooltipProps.Open"/> reports through
/// <see cref="TooltipProps.OnOpenChange"/>; null leaves it to the tooltip.</summary>
/// <remarks>A worded tip draws in the theme's strong foreground, padded as text and wrapping at its size's measure;
/// a rich one is padded as a block, at its natural width.</remarks>
[Children]
public sealed class Tooltip : Component<TooltipProps>
{
    protected override void Render(Ui ui, in TooltipProps p)
    {
        if (p.Delay is { } delay && !(delay >= 0f))
            throw Refused(nameof(p.Delay), delay);
        var model = ui.Remember(static () => TooltipModel.Rent());
        model.Host = ui.Self(p);
        model.Shown = ui.State(false);
        model.Anchor = ui.Ref();
        ui.Effect(model, 0, static (m, _, cx) => m.Attach(cx.View));
        var focused = p.Open is null && ui.IsFocusWithin && model.FocusVisible;
        ui.Effect(model, focused, static (m, f, _) => m.FocusMoved(f));
        using (
            ui.Box(
                onHoverChange: model.Hover,
                onPointerDown: model.Down,
                onPointerMove: model.Move,
                onPointerUp: model.Up,
                onPointerCancel: model.Up,
                nodeRef: model.Anchor
            )
        )
            ui.Children();
        if (!(p.Open ?? model.Shown.Value) || !TooltipModel.Shows(p))
            return;
        var t = ui.Theme;
        var size = p.Size ?? ComponentSize.Md;
        var m = t.Metrics(size);
        var worded = !p.Text.IsNull;
        using (
            ui.Positioner(
                anchor: p.Anchor ?? model.Anchor,
                placement: p.Placement ?? PlacementMode.Top,
                sideOffset: 4f,
                hit: LayerHit.PassThrough,
                tier: LayerTier.Tooltip,
                dismissOn: Dismiss.AnchorHidden,
                onDismiss: model.Dismissed,
                maxWidth: worded ? m.Measure : Length.Auto
            )
        )
        using (ui.OverlayFrame(size: size, inset: worded ? OverlayInset.Text : OverlayInset.Block))
        {
            if (worded)
                ui.Text(
                    p.Text,
                    color: t.Color(ColorToken.FgStrong),
                    fontSize: m.FontSize,
                    fontWeight: t.Weight(WeightToken.Interface),
                    wrap: true
                );
            else
                p.Content.Render(ui);
        }
    }

    static ArgumentOutOfRangeException Refused(string prop, float value) =>
        new(prop, value, "A tooltip's delay is 0 or more.");
}

[FramePath]
internal sealed class TooltipModel : IDisposable
{
    const float DefaultDelay = 0.3f;

    internal readonly Callback<bool> Hover;
    internal readonly Handler<PointerEvent> Down;
    internal readonly Handler<PointerEvent> Move;
    internal readonly Handler<PointerEvent> Up;
    internal readonly Callback Dismissed;
    internal Host<TooltipProps> Host;
    internal State<bool> Shown = null!;
    internal NodeRef Anchor = null!;
    internal UiView? View;
    TooltipCoordinator? _coordinator;
    bool _hovered;
    bool _disposed;

    public TooltipModel()
    {
        Hover = Callback<bool>.Of(this, static (m, on) => m.Hovered(on));
        Down = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Pressed(ref e));
        Move = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Moved(ref e));
        Up = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Lifted(ref e));
        Dismissed = Callback.Of(this, static m => m.LostAnchor());
    }

    internal bool FocusVisible =>
        View?.Focused is { } focused && (focused.States & InputStates.FocusVisible) != 0;

    internal bool HasTip => Shows(Host.Props);

    internal TimeSpan Delay => TimeSpan.FromSeconds(Host.Props.Delay ?? DefaultDelay);

    internal static bool Shows(in TooltipProps p) =>
        p.Text.IsNull ? !p.Content.IsEmpty : !p.Text.Span.IsWhiteSpace();

    internal void Attach(UiView view)
    {
        View = view;
        _coordinator = TooltipCoordinator.Of(view);
        if (_hovered)
            _coordinator.Hovered(this, true);
    }

    internal void Report(bool shown)
    {
        Shown.Value = shown;
        Host.Props.OnOpenChange.Invoke(shown);
    }

    internal void FocusMoved(bool focused) => _coordinator?.Focused(this, focused);

    void Hovered(bool on)
    {
        _hovered = on;
        _coordinator?.Hovered(this, on);
    }

    void Pressed(ref PointerEvent e)
    {
        if (e.Pointer.Kind == PointerKind.Touch)
            _coordinator?.FingerDown(this, e.Pointer, e.ViewPosition);
    }

    void Moved(ref PointerEvent e)
    {
        if (e.Pointer.Kind == PointerKind.Touch)
            _coordinator?.FingerMoved(e.Pointer, e.ViewPosition);
    }

    void Lifted(ref PointerEvent e)
    {
        if (e.Pointer.Kind == PointerKind.Touch)
            _coordinator?.FingerGone(e.Pointer);
    }

    void LostAnchor()
    {
        if (Host.Props.Open is null)
            _coordinator?.Hide(this);
        else
            Host.Props.OnOpenChange.Invoke(false);
    }

    internal static TooltipModel Rent()
    {
        var model = Pool<TooltipModel>.TryRent() ?? new TooltipModel();
        model._disposed = false;
        return model;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _coordinator?.Leave(this);
        _coordinator = null;
        _hovered = false;
        View = null;
        Host = default;
        Pool<TooltipModel>.Return(this);
    }
}

[FramePath]
internal sealed class TooltipCoordinator
{
    static readonly ConditionalWeakTable<UiView, TooltipCoordinator> Views = new();
    static readonly TimeSpan Afterglow = TimeSpan.FromSeconds(0.5);

    readonly UiView _view;
    readonly List<TooltipModel> _hovered = new(8);
    TooltipModel? _current;
    bool _revealed;
    bool _touch;
    bool _ticking;
    TimeSpan _revealAt;
    TimeSpan _hiddenAt;
    bool _hasHidden;
    TooltipModel? _fingerOwner;
    Pointer _finger;
    Vector2 _fingerFrom;
    bool _strayed;

    TooltipCoordinator(UiView view) => _view = view;

    public static TooltipCoordinator Of(UiView view) =>
        Views.GetValue(view, static v => new TooltipCoordinator(v));

    TimeSpan Now => _view.Context.Now;

    public void Hovered(TooltipModel model, bool on)
    {
        if (!on)
        {
            Left(model);
            return;
        }
        if (!_hovered.Contains(model))
            _hovered.Add(model);
        if (_view.InputDevice != InputDevice.Touch && model.HasTip)
            Arm(model, delayed: true, touch: false);
    }

    public void Focused(TooltipModel model, bool on)
    {
        if (on && model.HasTip)
            Arm(model, delayed: false, touch: false);
        else if (!on && _current == model && !_hovered.Contains(model))
            Hide(model);
    }

    public void FingerDown(TooltipModel model, Pointer finger, Vector2 at)
    {
        if (_fingerOwner is not null || !model.HasTip)
            return;
        _fingerOwner = model;
        _finger = finger;
        _fingerFrom = at;
        _strayed = false;
        Arm(model, delayed: true, touch: true);
    }

    public void FingerMoved(Pointer finger, Vector2 at)
    {
        if (_fingerOwner is null || finger != _finger || _strayed)
            return;
        if (Vector2.Distance(at, _fingerFrom) <= GestureSlop.Tap)
            return;
        _strayed = true;
        if (_touch && _current is { } shown)
            Hide(shown);
    }

    public void FingerGone(Pointer finger)
    {
        if (_fingerOwner is null || finger != _finger)
            return;
        _fingerOwner = null;
        if (_touch && _current is { } shown)
            Hide(shown);
    }

    public void Hide(TooltipModel model)
    {
        if (_current != model)
            return;
        var revealed = _revealed;
        _current = null;
        _revealed = false;
        if (!revealed)
            return;
        _hiddenAt = Now;
        _hasHidden = true;
        model.Report(false);
    }

    public void Leave(TooltipModel model)
    {
        _hovered.Remove(model);
        if (_fingerOwner == model)
            _fingerOwner = null;
        if (_current != model)
            return;
        if (_revealed)
        {
            _hiddenAt = Now;
            _hasHidden = true;
        }
        _current = null;
        _revealed = false;
    }

    void Left(TooltipModel model)
    {
        _hovered.Remove(model);
        if (_fingerOwner == model)
            FingerGone(_finger);
        if (_current != model)
            return;
        if (!_touch)
        {
            for (var i = _hovered.Count - 1; i >= 0; i--)
            {
                if (!_hovered[i].HasTip)
                    continue;
                Arm(_hovered[i], delayed: false, touch: false);
                return;
            }
        }
        Hide(model);
    }

    void Arm(TooltipModel model, bool delayed, bool touch)
    {
        if (_current == model && _touch == touch)
        {
            if (!delayed && !_revealed)
                Reveal();
            return;
        }
        var now = Now;
        var hop = !touch && (_revealed || _hasHidden && now - _hiddenAt < Afterglow);
        if (_current is { } shown)
            Hide(shown);
        _current = model;
        _touch = touch;
        if (delayed && !hop)
        {
            _revealAt = now + model.Delay;
            Tick();
        }
        else
            Reveal();
    }

    void Reveal()
    {
        _revealed = true;
        _current!.Report(true);
    }

    void Tick()
    {
        if (_ticking)
            return;
        _ticking = true;
        _view.Context.Dispatcher.Post(this, static c => c.OnTick());
    }

    void OnTick()
    {
        _ticking = false;
        if (_current is null || _revealed)
            return;
        if (Now >= _revealAt)
            Reveal();
        else
            Tick();
    }
}

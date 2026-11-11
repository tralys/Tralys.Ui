using System.Runtime.CompilerServices;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>How far a floating frame's content sits in from its edge.</summary>
public enum OverlayInset : byte
{
    /// <summary>A list of rows, each carrying its own padding: a thin margin round them.</summary>
    List,

    /// <summary>A line or two of text, padded as a control of the frame's size.</summary>
    Text,

    /// <summary>A panel of content, padded evenly by the size's block inset.</summary>
    Block,

    /// <summary>None: the content meets the frame's edge, clipped to its corners, as a body that pads itself
    /// does.</summary>
    None,
}

public readonly record struct OverlayProps
{
    public bool Open { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    /// <summary>Whether a left press on the scrim around the panel reports
    /// <see cref="OnOpenChange"/>(false).</summary>
    public bool ClosesOnScrimClick { get; init; }

    /// <summary>The space the scrim keeps clear round the panel: 16 on each side by default. An overlay over the view's
    /// edges adds <see cref="Ui.SafeInsets"/>, so its panel keeps clear of a cutout.</summary>
    public Thickness? Padding { get; init; }
}

public readonly record struct OverlayFrameProps
{
    /// <summary>The rung the frame pads its content by and its rows take; <see cref="ComponentSize.Md"/> by
    /// default.</summary>
    public ComponentSize? Size { get; init; }

    public OverlayInset Inset { get; init; }

    /// <summary>The frame's shadow, in place of the theme's overlay shadow.</summary>
    public BoxShadow? Shadow { get; init; }

    public Length Width { get; init; }
}

/// <summary>What a floating frame tells what it holds: the rung its rows take.</summary>
public readonly record struct OverlayFrameContext(ComponentSize Size);

/// <summary>A panel over a scrim that covers the area its caller places the overlay over, a modal there while
/// <see cref="OverlayProps.Open"/>: presses within it land on it, Tab and the arrows keep to the panel, and focus
/// moves to the panel and goes back to what had it as it closes. Escape alone, the corner close button shown while
/// there is an <see cref="OverlayProps.OnOpenChange"/>, and with <see cref="OverlayProps.ClosesOnScrimClick"/> a left
/// press on the scrim report <see cref="OverlayProps.OnOpenChange"/>(false). It takes Enter, so nothing behind it
/// sees the key, and its action scope keeps the actions behind it from running. Closed, it draws nothing and takes
/// nothing.</summary>
[Children]
public sealed class Overlay : Component<OverlayProps>
{
    protected override void Render(Ui ui, in OverlayProps p)
    {
        var modal = ui.Remember(static () => new Modal());
        modal.OnOpenChange = p.OnOpenChange;
        modal.ClosesOnEscape = true;
        modal.ClosesOnScrim = p.ClosesOnScrimClick;
        modal.OnSubmit = default;
        modal.Panel = ui.Ref();
        if (!p.Open)
            return;
        var t = ui.Theme;
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                padding: p.Padding ?? new Thickness(Modal.ScrimPadding),
                background: t.Color(ColorToken.BgScrim),
                modal: true,
                onKeyDown: modal.Keys,
                onPointerDown: modal.ScrimPress
            )
        )
        using (ui.ActionScope())
        using (ui.ModalPanel(model: modal))
        {
            using (ui.Box(direction: Direction.Column, minHeight: 0f))
                ui.Children();
            ui.ModalClose(model: modal);
        }
    }

    /// <summary>The one surface everything floating is drawn on, a popover, a tooltip, a dropdown or a menu, so they
    /// share a fill, an edge, a shadow and an inset: the theme's overlay fill inside a 1-unit overlay edge, rounded to
    /// <see cref="RadiusToken.Md"/>, casting the theme's overlay shadow unless given its own, and padding its content
    /// as <see cref="OverlayFrameProps.Inset"/> says for its size. What it holds reads its size from
    /// <see cref="Context"/>, and its text takes the theme's foreground.</summary>
    [Children]
    public sealed class Frame : Component<OverlayFrameProps>
    {
        public static readonly ContextKey<OverlayFrameContext> Context = new(
            "OverlayFrame",
            new OverlayFrameContext(ComponentSize.Md)
        );

        const float ShadowOpacity = 0.6f;

        protected override void Render(Ui ui, in OverlayFrameProps p)
        {
            var t = ui.Theme;
            var size = p.Size ?? ComponentSize.Md;
            var m = t.Metrics(size);
            ui.Provide(Context, new OverlayFrameContext(size));
            using (
                ui.Box(
                    direction: Direction.Column,
                    width: p.Width,
                    padding: p.Inset switch
                    {
                        OverlayInset.Text => m.Padding,
                        OverlayInset.Block => m.Inset,
                        OverlayInset.None => default,
                        _ => new Thickness(4f),
                    },
                    overflow: p.Inset == OverlayInset.None ? Overflow.Clip : Overflow.Visible,
                    background: t.Color(ColorToken.BgOverlay),
                    border: new Border(1f, t.Color(ColorToken.BorderOverlay)),
                    radius: t.Radius(RadiusToken.Md),
                    shadow: p.Shadow
                        ?? new BoxShadow(
                            0f,
                            2f,
                            10f,
                            0f,
                            t.Color(ColorToken.BgShadow).WithOpacity(ShadowOpacity)
                        ),
                    color: t.Color(ColorToken.Fg)
                )
            )
                ui.Children();
        }
    }
}

[FramePath]
internal sealed class Modal
{
    internal const float ScrimPadding = 16f;

    internal readonly Callback Close;
    internal readonly Callback Submit;
    internal readonly Handler<KeyEvent> Keys;
    internal readonly Handler<PointerEvent> ScrimPress;
    internal Callback<bool> OnOpenChange;
    internal Callback OnSubmit;
    internal bool SubmitDisabled;
    internal bool ClosesOnEscape;
    internal bool ClosesOnScrim;
    internal NodeRef Panel = null!;

    public Modal()
    {
        Close = Callback.Of(this, static m => m.OnOpenChange.Invoke(false));
        Submit = Callback.Of(this, static m => m.RunSubmit());
        Keys = Handler<KeyEvent>.Of(this, static (m, ref e) => m.KeyDown(ref e));
        ScrimPress = Handler<PointerEvent>.Of(this, static (m, ref e) => m.Pressed(ref e));
    }

    internal bool CanClose => !OnOpenChange.IsEmpty;

    internal bool CanSubmit => !OnSubmit.IsEmpty && !SubmitDisabled;

    void RunSubmit()
    {
        if (CanSubmit)
            OnSubmit.Invoke();
    }

    // A plain Enter isn't prevented: the focused button's press and the surface's Confirm action run after bubbling.
    void KeyDown(ref KeyEvent e)
    {
        switch (e.Key)
        {
            case Key.Escape when ClosesOnEscape && e.Modifiers == ModifierKeys.None && CanClose:
                OnOpenChange.Invoke(false);
                e.PreventDefault();
                e.StopPropagation();
                break;
            case Key.Enter:
                e.StopPropagation();
                if (e.Modifiers == ModifierKeys.None)
                    break;
                if (e.Modifiers == ModifierKeys.Control)
                    RunSubmit();
                e.PreventDefault();
                break;
        }
    }

    void Pressed(ref PointerEvent e)
    {
        if (
            !ClosesOnScrim
            || !CanClose
            || e.Button != MouseButton.Left
            || Panel.Bounds.Contains(e.ViewPosition)
        )
            return;
        OnOpenChange.Invoke(false);
        e.PreventDefault();
        e.StopPropagation();
    }
}

[FramePath]
internal static class ModalParts
{
    public static Scope ModalPanel(
        this Ui ui,
        Modal model,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        return ui.Box(
            direction: Direction.Column,
            maxWidth: Length.Full,
            maxHeight: Length.Full,
            focusable: true,
            tabNavigation: Navigation.Cycle,
            arrowNavigation: Navigation.Contained,
            background: t.Color(ColorToken.Bg),
            border: new Border(1f, t.Color(ColorToken.Border)),
            radius: t.Radius(RadiusToken.Md),
            nodeRef: model.Panel,
            key: key,
            file: file,
            line: line
        );
    }

    public static void ModalClose(
        this Ui ui,
        Modal model,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        if (model.CanClose)
            ui.CloseButton(
                size: ComponentSize.Xs,
                onClick: model.Close,
                position: Position.Absolute,
                top: -10f,
                right: -10f,
                key: key,
                file: file,
                line: line
            );
    }
}

using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct DialogProps
{
    public bool Open { get; init; }

    public Callback<bool> OnOpenChange { get; init; }

    /// <summary>What Enter, Ctrl+Enter and a <see cref="Dialog.Submit"/> run.</summary>
    public Callback OnSubmit { get; init; }

    /// <summary>Holds <see cref="OnSubmit"/> back, greying out the submit button.</summary>
    public bool SubmitDisabled { get; init; }

    /// <summary>The body's width; auto fits the content.</summary>
    public Length Width { get; init; }

    /// <summary>How the dialog, scrim and panel together, fades in as it opens and out as it closes; null shows it
    /// and takes it away at once. A default <see cref="Tralys.Ui.Transition"/> takes the theme's.</summary>
    public Transition? Transition { get; init; }
}

internal readonly record struct DialogScrimProps
{
    public Transition? Transition { get; init; }

    public Handler<KeyEvent> OnKeyDown { get; init; }
}

/// <summary>What a <see cref="Dialog"/> tells its parts: how to close it and submit it, and whether it can.</summary>
public readonly record struct DialogContext(
    Callback Close,
    Callback Submit,
    bool CanClose,
    bool HasSubmit,
    bool SubmitDisabled
);

public readonly record struct DialogTitleProps
{
    public TextValue Text { get; init; }
}

public readonly record struct DialogFooterProps
{
    /// <summary>What the footer shows at its start, opposite its buttons.</summary>
    public Slot Start { get; init; }
}

public readonly record struct DialogCloseProps
{
    /// <summary>The button's label; "Close" by default.</summary>
    public TextValue Text { get; init; }
}

public readonly record struct DialogSubmitProps
{
    public TextValue Text { get; init; }

    /// <summary>Draws the button in the danger scheme, for a submit that destroys something.</summary>
    public bool Destructive { get; init; }
}

/// <summary>A modal panel over a scrim that covers the view, in a layer of its own while
/// <see cref="DialogProps.Open"/>, laying out what is written inside it in a column: its
/// <see cref="Title"/>, its body, and its <see cref="Footer"/>. Focus moves into it as it opens, Tab and the arrows keep
/// to its panel, and focus goes back to what had it as it closes. Escape, a press on the scrim and the corner close
/// button shown while there is an <see cref="DialogProps.OnOpenChange"/> report
/// <see cref="DialogProps.OnOpenChange"/>(false). Enter, alone or with Ctrl, runs
/// <see cref="DialogProps.OnSubmit"/> unless the focused control takes the key itself or the submit is disabled, and no
/// Enter reaches anything outside it. Closed, it draws nothing and builds nothing written inside it.</summary>
[Children]
public sealed class Dialog : Component<DialogProps>
{
    public static readonly ContextKey<DialogContext> Context = new("Dialog", default);

    protected override void Render(Ui ui, in DialogProps p)
    {
        var modal = ui.Remember(ui.Ref(), static panel => new Modal(panel));
        modal.OnOpenChange = p.OnOpenChange;
        modal.ClosesOnEscape = false;
        modal.ClosesOnScrim = false;
        modal.OnSubmit = p.OnSubmit;
        modal.SubmitDisabled = p.SubmitDisabled;
        if (!p.Open)
            return;
        var t = ui.Theme;
        ui.Provide(
            Context,
            new DialogContext(
                modal.Close,
                modal.Submit,
                modal.CanClose,
                !p.OnSubmit.IsEmpty,
                p.SubmitDisabled
            )
        );
        using (
            ui.Layer(
                restoreFocus: true,
                content: modal.Panel,
                dismissOn: modal.CanClose ? Dismiss.Escape | Dismiss.OutsidePress : Dismiss.None,
                onDismiss: modal.Close
            )
        )
        using (ui.DialogScrim(transition: p.Transition, onKeyDown: modal.Keys, grow: 1f))
        using (ui.ActionScope())
        {
            if (modal.CanSubmit)
                ui.Action(UiAction.Confirm, onRun: modal.Submit);
            using (ui.ModalPanel(model: modal))
            {
                using (ui.Box(direction: Direction.Column, minHeight: 0f, padding: 16f))
                using (ui.Box(direction: Direction.Column, width: p.Width, minHeight: 0f))
                    ui.Children();
                ui.ModalClose(model: modal);
            }
        }
    }

    // The layer holds it directly, so its presence fades the layer out as the dialog closes.
    [Children]
    internal sealed class Scrim : Component<DialogScrimProps>
    {
        protected override void Render(Ui ui, in DialogScrimProps p)
        {
            float? shown = p.Transition is { } fade ? ui.Presence(fade, fade) : null;
            using (
                ui.Box(
                    align: Align.Center,
                    justify: Justify.Center,
                    padding: Modal.ScrimPadding,
                    background: ui.Theme.Color(ColorToken.BgScrim),
                    modal: true,
                    onKeyDown: p.OnKeyDown,
                    opacity: shown
                )
            )
                ui.Children();
        }
    }

    /// <summary>The dialog's title, above its body in the theme's title text and accent colour.</summary>
    public sealed class Title : Component<DialogTitleProps>
    {
        protected override void Render(Ui ui, in DialogTitleProps p)
        {
            var t = ui.Theme;
            using (ui.Box(padding: new Thickness(0f, 0f, 0f, 32f)))
                ui.Text(
                    p.Text,
                    variant: TextVariant.Title,
                    color: t.Color(ColorToken.FgAccent),
                    fontWeight: t.Weight(WeightToken.Interface),
                    wrap: true,
                    grow: 1f
                );
        }
    }

    /// <summary>The dialog's footer, below its body: <see cref="DialogFooterProps.Start"/> at its start, and what is
    /// written inside it, its buttons, in a row at its end.</summary>
    [Children]
    public sealed class Footer : Component<DialogFooterProps>
    {
        protected override void Render(Ui ui, in DialogFooterProps p)
        {
            using (ui.Box(align: Align.Center, padding: new Thickness(0f, 24f, 0f, 0f)))
            {
                using (ui.Box(align: Align.Center, grow: 1f))
                    p.Start.Render(ui);
                using (ui.Box(align: Align.Center, gap: 8f))
                    ui.Children();
            }
        }
    }

    /// <summary>A footer button that reports the dialog's <see cref="DialogProps.OnOpenChange"/>(false), drawn only
    /// while the dialog can close.</summary>
    public sealed class Close : Component<DialogCloseProps>
    {
        protected override void Render(Ui ui, in DialogCloseProps p)
        {
            var dialog = ui.Use(Context);
            if (!dialog.CanClose)
                return;
            ui.Button(
                text: p.Text.IsNull ? "Close" : p.Text,
                scheme: ColorScheme.Secondary,
                variant: ComponentVariant.GhostOutlined,
                onClick: dialog.Close
            );
        }
    }

    /// <summary>A footer button that runs the dialog's <see cref="DialogProps.OnSubmit"/>, greyed out while
    /// <see cref="DialogProps.SubmitDisabled"/>, drawn only while the dialog has a submit.</summary>
    public sealed class Submit : Component<DialogSubmitProps>
    {
        protected override void Render(Ui ui, in DialogSubmitProps p)
        {
            var dialog = ui.Use(Context);
            if (!dialog.HasSubmit)
                return;
            ui.Button(
                text: p.Text,
                scheme: p.Destructive ? ColorScheme.Danger : ColorScheme.Primary,
                disabled: dialog.SubmitDisabled,
                onClick: dialog.Submit
            );
        }
    }
}

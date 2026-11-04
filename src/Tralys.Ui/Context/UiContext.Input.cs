namespace Tralys.Ui;

public sealed partial class UiContext
{
    ActionBindings? _actions;

    /// <summary>Whether a pressed pressable keeps the focus it took; off, it gives focus up after each release or
    /// cancel, while fields and anything in an open layer or modal keep it. On by default.</summary>
    public bool PressKeepsFocus { get; set; } = true;

    /// <summary>Whether an arrow key nothing took moves the focus. Off, it comes back untaken; Tab still navigates.
    /// On by default.</summary>
    public bool ArrowKeysNavigate { get; set; } = true;

    /// <summary>Whether a D-pad press nothing took moves the focus. On by default.</summary>
    public bool DPadNavigates { get; set; } = true;

    /// <summary>The keys and gamepad buttons actions run on, shared by every view on the context.</summary>
    [FramePath]
    public ActionBindings Actions => _actions ??= new ActionBindings(Dispatcher);
}

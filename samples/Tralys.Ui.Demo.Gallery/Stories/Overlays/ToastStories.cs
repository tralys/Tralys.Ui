using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Toaster>("Overlays")]
static class ToasterStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var toasts = ui.Remember(static () => new StoryToasts());
        using (
            ui.Box(
                direction: Direction.Column,
                justify: Justify.End,
                width: 480,
                height: 240,
                background: ui.Theme.Color(ColorToken.BgSunken)
            )
        )
        {
            using (ui.Box(gap: 8, alignSelf: AlignSelf.Center, margin: new Thickness(0, 0, 0, 16)))
            {
                ui.Button(text: "Save", onClick: toasts.ShowSuccess);
                ui.Button(text: "Warn", onClick: toasts.ShowWarning);
                ui.Button(text: "Fail", onClick: toasts.ShowError);
            }
            using (ui.Toaster())
                for (var i = 0; i < toasts.Shown.Count; i++)
                {
                    var toast = toasts.Shown[i];
                    ui.Toast(
                        title: toast.Message,
                        severity: toast.Severity,
                        onDismiss: toast.Dismiss,
                        key: toast.Id
                    );
                }
        }
    }
}

[Stories<Toast>("Overlays")]
static class ToastStories
{
    [FramePath]
    public static void Variants(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 16, width: 320))
        {
            ui.Toast(title: "Operation completed successfully.", severity: ToastSeverity.Success);
            ui.Toast(
                title: "Something went wrong. Please try again.",
                severity: ToastSeverity.Error
            );
            ui.Toast(
                title: "Your session will expire in 5 minutes.",
                severity: ToastSeverity.Warning
            );
        }
    }
}

/// <summary>The toasts the toaster story shows, one of each severity to begin with, each leaving the list as its
/// toaster reports it.</summary>
sealed class StoryToasts
{
    readonly Stack<StoryToast> _spare = new();
    long _shown;

    public StoryToasts()
    {
        ShowSuccess = Callback.Of(this, static s => s.Show(ToastSeverity.Success, "Preset saved"));
        ShowWarning = Callback.Of(
            this,
            static s => s.Show(ToastSeverity.Warning, "Your disk is nearly full")
        );
        ShowError = Callback.Of(
            this,
            static s => s.Show(ToastSeverity.Error, "The upload was cancelled")
        );
        ShowSuccess.Invoke();
        ShowWarning.Invoke();
        ShowError.Invoke();
    }

    public ObservableList<StoryToast> Shown { get; } = new(16);

    public Callback ShowSuccess { get; }

    public Callback ShowWarning { get; }

    public Callback ShowError { get; }

    void Show(ToastSeverity severity, string message)
    {
        var toast = _spare.Count > 0 ? _spare.Pop() : new StoryToast(this);
        toast.Id = ++_shown;
        toast.Severity = severity;
        toast.Message = message;
        Shown.Add(toast);
    }

    internal void Dismiss(StoryToast toast)
    {
        if (Shown.Remove(toast))
            _spare.Push(toast);
    }
}

sealed class StoryToast
{
    public StoryToast(StoryToasts toasts) =>
        Dismiss = Callback.Of(toasts, this, static (all, toast) => all.Dismiss(toast));

    public long Id { get; set; }

    public ToastSeverity Severity { get; set; }

    public string Message { get; set; } = "";

    public Callback Dismiss { get; }
}

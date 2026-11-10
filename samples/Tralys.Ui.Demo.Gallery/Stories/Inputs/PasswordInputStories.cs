using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<PasswordInput>("Inputs")]
static class PasswordInputStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var secret = ui.Remember(static () => new ObservableText("secret"));
        var typed = ui.Remember(static () => new ObservableText());
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 16, padding: 16))
        {
            ui.PasswordInput(text: secret, width: 200);
            ui.PasswordInput(text: typed, placeholder: "Enter password...", width: 200);
        }
    }

    [FramePath]
    public static void Reveal(Ui ui)
    {
        var revealed = ui.State(false);
        var password = ui.Remember(static () => new ObservableText("hunter2"));
        using (ui.Box(direction: Direction.Column, align: Align.Start, padding: 16))
            ui.PasswordInput(
                text: password,
                revealed: revealed.Value,
                width: 200,
                trailing: Slot.Of(
                    revealed,
                    static (ui, revealed) =>
                        ui.Toggle(
                            value: revealed.Value,
                            onValueChange: revealed.Setter,
                            icon: Icons.Eye,
                            activeIcon: Icons.EyeOff,
                            size: ComponentSize.Xs,
                            margin: new Thickness(0f, 0f, 4f, 0f)
                        )
                )
            );
    }
}

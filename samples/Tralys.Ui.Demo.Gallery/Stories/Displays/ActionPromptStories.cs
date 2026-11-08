using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<ActionPrompt>("Displays")]
static class ActionPromptStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Box(gap: 48))
        {
            ui.ActionPrompt(action: UiAction.Confirm, label: "Equip");
            ui.ActionPrompt(action: UiAction.Back, label: "Close");
            ui.ActionPrompt(action: UiAction.PreviousTab, label: "Previous bag");
        }
    }
}

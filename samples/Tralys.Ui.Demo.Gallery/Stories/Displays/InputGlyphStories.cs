using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<InputGlyph>("Displays")]
static class InputGlyphStories
{
    [FramePath]
    public static void BuiltInActions(Ui ui)
    {
        using (ui.Box(gap: 32))
        {
            ui.InputGlyph(action: UiAction.Confirm);
            ui.InputGlyph(action: UiAction.Back);
            ui.InputGlyph(action: UiAction.PreviousTab);
            ui.InputGlyph(action: UiAction.NextTab);
        }
    }
}

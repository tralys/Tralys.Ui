using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<MoneyInput>("Inputs")]
static class MoneyInputStories
{
    [FramePath]
    public static void Default(Ui ui) => ui.MoneyInput(width: 200);
}

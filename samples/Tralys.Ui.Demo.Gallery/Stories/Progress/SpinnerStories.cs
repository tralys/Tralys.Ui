using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Spinner>("Progress")]
static class SpinnerStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        using (ui.Box(align: Align.Center, gap: 8))
        {
            ui.Spinner();
            ui.Text(
                "Connecting to the server…",
                fontWeight: ui.Theme.Weight(WeightToken.Interface),
                wrap: true
            );
        }
    }

    [FramePath]
    public static void Sizes(Ui ui)
    {
        using (ui.Box(align: Align.Center, gap: 16))
        {
            ui.Spinner(size: ComponentSize.Xs);
            ui.Spinner();
            ui.Spinner(size: ComponentSize.Lg);
        }
    }
}

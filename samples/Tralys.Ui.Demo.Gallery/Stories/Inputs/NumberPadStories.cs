using Tralys.Ui.Components;

namespace Tralys.Ui.Gallery.Stories;

[Stories<NumberPad>("Inputs")]
static class NumberPadStories
{
    [FramePath]
    public static void Amount(Ui ui) => ui.NumberPad(hasMax: true, hasScales: true);

    [FramePath]
    public static void Stepped(Ui ui) => ui.NumberPad(hasStep: true);
}

[Stories<NumberPad.Key>("Inputs")]
static class NumberPadKeyStories
{
    [FramePath]
    public static void Kinds(Ui ui)
    {
        using (ui.Box(gap: 4))
        {
            ui.NumberPadKey(kind: NumberPadKeyKind.Digit7);
            ui.NumberPadKey(kind: NumberPadKeyKind.K);
            ui.NumberPadKey(kind: NumberPadKeyKind.Max);
            ui.NumberPadKey(kind: NumberPadKeyKind.Back);
            ui.NumberPadKey(kind: NumberPadKeyKind.Done);
        }
    }
}

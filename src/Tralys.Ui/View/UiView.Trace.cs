using Tralys.Ui.Diagnostics;
using Tralys.Ui.Rendering;

namespace Tralys.Ui;

public sealed partial class UiView
{
    // A list of its own, with no spans recorded, so the view's list and the spans it patches stay as they were.
    internal void Trace(Action<object?, string> line)
    {
        VerifyAccess();
        DrawFrame(
            new DrawList(new GradientRampAtlas())
            {
                RecordsSpans = false,
                Trace = new DrawTrace(line),
            }
        );
    }
}

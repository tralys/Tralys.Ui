using System.Runtime.CompilerServices;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

[FramePath]
public static class FocusRingCalls
{
    internal const float Width = 2f;

    /// <summary>A focus ring around the open node: a 2-unit <see cref="ColorToken.BorderFocus"/> outline
    /// <paramref name="inset"/> outside it, rounded to its <paramref name="radius"/> plus the inset, taking no
    /// input.</summary>
    public static void FocusRing(
        this Ui ui,
        CornerRadius radius,
        float inset = 2f,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                position: Position.Absolute,
                left: -inset,
                top: -inset,
                right: -inset,
                bottom: -inset,
                radius: new CornerRadius(
                    radius.TopLeft + inset,
                    radius.TopRight + inset,
                    radius.BottomRight + inset,
                    radius.BottomLeft + inset
                ),
                border: new Border(Width, ui.Theme.Color(ColorToken.BorderFocus)),
                pointerEvents: PointerEvents.None,
                key: key,
                file: file,
                line: line
            )
        ) { }
    }
}

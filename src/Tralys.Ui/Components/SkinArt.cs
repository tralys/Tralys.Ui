using System.Runtime.CompilerServices;

namespace Tralys.Ui.Components;

[FramePath]
public static class SkinArtCalls
{
    /// <summary>A skin part's art as the open node's backdrop: its nine-slice stretched over the node's padding box,
    /// behind what follows, at <paramref name="opacity"/>, taking no input. What a component or an app draws in place
    /// of a part's vector look when <c>ui.Theme.Skin(part)</c> finds the part; art in flow at a size of its own is
    /// <c>ui.Image(art.Source, slice: art.NineSlice, …)</c>.</summary>
    public static void Art(
        this Ui ui,
        SkinPartImage art,
        float opacity = 1f,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Image(
            art.Source,
            slice: art.NineSlice,
            opacity: opacity,
            pointerEvents: PointerEvents.None,
            position: Position.Absolute,
            left: 0f,
            top: 0f,
            right: 0f,
            bottom: 0f,
            key: key,
            file: file,
            line: line
        );
}

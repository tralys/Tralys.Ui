using Tralys.Ui.Media;
using Tralys.Ui.Rendering;

namespace Tralys.Ui;

/// <summary>A part of a component that a <see cref="Skin"/> can draw as art in place of its vector look.</summary>
public enum SkinPart : ushort
{
    WindowFrame,
    TitleBacking,
    CloseButton,
    CloseButtonHover,
    ItemSlot,
    MoneyIcon,
    ButtonPrimary,
    ButtonPrimaryHover,
    TabActive,
    TabHover,
    ProgressTrack,
    ProgressFillOverlay,
    SliderTrack,
    SliderThumb,
    SliderThumbHover,
    ToggleTrackOff,
    ToggleTrackOn,
    ToggleThumb,
    ToastFrame,
}

/// <summary>A part's art: <see cref="Source"/> cut into nine slices <see cref="Slice"/> texels in from each edge,
/// its corners drawn at <see cref="Scale"/> units per texel and its edges and centre filled by
/// <see cref="Fill"/>.</summary>
[FramePath]
public readonly record struct SkinPartImage(
    ImageSource Source,
    Thickness Slice,
    SliceFill Fill = SliceFill.Stretch,
    float Scale = 1f
)
{
    /// <summary>The slicing to draw the art with through <c>ui.Image</c>'s <c>slice</c>.</summary>
    public NineSlice NineSlice => new(Slice, Scale, Fill, TexelInset: 0.5f);
}

/// <summary>Art for component parts, set as <see cref="UiContext.Skin"/>. A component that offers art for a part
/// draws it when <see cref="UiTheme.Skin"/> finds the part, and its vector look otherwise. Immutable, so one skin may
/// be shared by contexts on any thread.</summary>
public sealed class Skin
{
    readonly SkinPartImage?[] _parts;

    public Skin(ReadOnlySpan<KeyValuePair<SkinPart, SkinPartImage>> parts)
    {
        var count = 0;
        foreach (var part in parts)
            count = Math.Max(count, (int)part.Key + 1);
        _parts = new SkinPartImage?[count];
        foreach (var part in parts)
            _parts[(int)part.Key] = part.Value;
    }

    /// <summary>The art for <paramref name="part"/>, if the skin has any.</summary>
    [FramePath]
    public bool TryGet(SkinPart part, out SkinPartImage image)
    {
        if ((int)part < _parts.Length && _parts[(int)part] is { } found)
        {
            image = found;
            return true;
        }
        image = default;
        return false;
    }
}

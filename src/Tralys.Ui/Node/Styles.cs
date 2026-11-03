using System.Numerics;
using Tralys.Ui.Fonts;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui;

[Flags]
internal enum Dirty : ushort
{
    None = 0,
    Measure = 1,
    Layout = 2,
    Paint = 4,
    Composite = 8,
    Children = 16,
    Env = 32,
}

[FramePath]
internal record struct LayoutStyle
{
    public Direction Direction;
    public FlexWrap Wrap;
    public Align Align;
    public Justify Justify;
    public AlignContent AlignContent;
    public float RowGap;
    public float ColumnGap;
    public Thickness Padding;
    public Thickness Border;
    public Overflow Overflow;
    public byte QuarterTurns;
    public Length Width;
    public Length Height;
    public Length MinWidth;
    public Length MinHeight;
    public Length MaxWidth;
    public Length MaxHeight;
    public float AspectRatio;
    public Size IntrinsicSize;

    /// <summary>What <c>Equals</c> says, without copying either style.</summary>
    public readonly bool Same(in LayoutStyle other) =>
        Direction == other.Direction
        && Wrap == other.Wrap
        && Align == other.Align
        && Justify == other.Justify
        && AlignContent == other.AlignContent
        && RowGap.Equals(other.RowGap)
        && ColumnGap.Equals(other.ColumnGap)
        && Padding == other.Padding
        && Border == other.Border
        && Overflow == other.Overflow
        && QuarterTurns == other.QuarterTurns
        && Width == other.Width
        && Height == other.Height
        && MinWidth == other.MinWidth
        && MinHeight == other.MinHeight
        && MaxWidth == other.MaxWidth
        && MaxHeight == other.MaxHeight
        && AspectRatio.Equals(other.AspectRatio)
        && IntrinsicSize == other.IntrinsicSize;
}

[FramePath]
internal record struct VisualStyle
{
    public Paint Background;
    public Paint BorderPaint;
    public CornerRadius Radius;
    public BoxShadow Shadow;
    public Paint Mask;
    public float? Opacity;
    public Transform Transform;
    public Vector2? TransformOrigin;

    /// <summary>What <c>Equals</c> says, without copying either style.</summary>
    public readonly bool Same(in VisualStyle other) => DrawsLike(other) && CompositesLike(other);

    public readonly bool DrawsLike(in VisualStyle other) =>
        Background == other.Background
        && BorderPaint == other.BorderPaint
        && Radius == other.Radius
        && Shadow == other.Shadow;

    public readonly bool CompositesLike(in VisualStyle other) =>
        Mask == other.Mask
        && Nullable.Equals(Opacity, other.Opacity)
        && Transform == other.Transform
        && Nullable.Equals(TransformOrigin, other.TransformOrigin)
        && Radius == other.Radius;

    public readonly bool CompositesLikeAtAnyOpacity(in VisualStyle other) =>
        Mask == other.Mask
        && Transform == other.Transform
        && Nullable.Equals(TransformOrigin, other.TransformOrigin)
        && Radius == other.Radius;
}

[FramePath]
internal record struct InheritStyle
{
    public Paint Color;
    public string? FontFamily;
    public float FontSize;
    public FontWeight FontWeight;
    public FontStyle? FontStyle;
    public float LineHeight;
    public float? LetterSpacing;
    public bool? TabularDigits;
    public TextAlign TextAlign;
    public TextVariant? TextVariant;
    public PointerEvents PointerEvents;
    public bool Disabled;
    public Theme? Theme;
    public Density? Density;
    public InputStates ForceStates;

    /// <summary>What <c>Equals</c> says, without copying either style.</summary>
    public readonly bool Same(in InheritStyle other) =>
        StylesLike(other) && Disabled == other.Disabled && ForceStates == other.ForceStates;

    public readonly bool StylesLike(in InheritStyle other) =>
        Color == other.Color
        && string.Equals(FontFamily, other.FontFamily, StringComparison.Ordinal)
        && FontSize.Equals(other.FontSize)
        && FontWeight == other.FontWeight
        && FontStyle == other.FontStyle
        && LineHeight.Equals(other.LineHeight)
        && Nullable.Equals(LetterSpacing, other.LetterSpacing)
        && TabularDigits == other.TabularDigits
        && TextAlign == other.TextAlign
        && TextVariant == other.TextVariant
        && PointerEvents == other.PointerEvents
        && ReferenceEquals(Theme, other.Theme)
        && Density == other.Density;
}

[FramePath]
internal record struct InteractionProps
{
    public Callback OnClick;
    public PressMode PressMode;
    public PressKeys PressKeys;
    public Handler<PointerEvent> OnPreviewPointerDown;
    public Handler<PointerEvent> OnPointerDown;
    public Handler<PointerEvent> OnPointerMove;
    public Handler<PointerEvent> OnPointerUp;
    public Handler<PointerEvent> OnPointerCancel;
    public Callback<bool> OnHoverChange;
    public Handler<WheelEvent> OnWheel;
    public Handler<KeyEvent> OnKeyDown;
    public Handler<KeyEvent> OnKeyUp;
    public Callback<FocusChange> OnFocusChange;
    public bool? Focusable;
    public Cursor? Cursor;
    public bool Modal;
    public bool ContainsLayers;
    public Callback OnDoubleClick;
    public Callback OnLongPress;
    public Handler<PointerEvent> OnContextMenu;
    public Handler<DragEvent> OnDrag;
    public Handler<GamepadEvent> OnGamepad;
    public int TabIndex;
    public bool AutoFocus;
    public Navigation TabNavigation;
    public Navigation ArrowNavigation;
    public UiAction Action;
    public bool ExcludeSystemGestures;
    public object? DragData;
    public DragEffect DragEffects;
    public Slot DragPreview;
    public Callback<DragEffect> OnDragEnd;
    public Handler<DropEvent> OnDragOver;
    public Callback OnDragLeave;
    public Handler<DropEvent> OnDrop;

    public readonly bool IsEmpty =>
        !HandlesPointer
        && OnKeyDown.IsEmpty
        && OnKeyUp.IsEmpty
        && OnFocusChange.IsEmpty
        && Focusable is null
        && Cursor is null
        && !Modal
        && !ContainsLayers
        && PressMode == default
        && PressKeys == default
        && OnGamepad.IsEmpty
        && TabIndex == 0
        && !AutoFocus
        && TabNavigation == default
        && ArrowNavigation == default
        && Action.Name is null
        && !ExcludeSystemGestures
        && DragEffects == default
        && DragPreview.IsEmpty
        && OnDragEnd.IsEmpty
        && OnDragLeave.IsEmpty;

    public readonly bool IsPressable =>
        !OnClick.IsEmpty || !OnDoubleClick.IsEmpty || !OnLongPress.IsEmpty;

    public readonly bool EnterPresses => IsPressable && PressKeys == PressKeys.EnterAndSpace;

    public readonly bool SpacePresses => IsPressable && PressKeys != PressKeys.None;

    public readonly bool HandlesPointer =>
        IsPressable
        || !OnPreviewPointerDown.IsEmpty
        || !OnPointerDown.IsEmpty
        || !OnPointerMove.IsEmpty
        || !OnPointerUp.IsEmpty
        || !OnPointerCancel.IsEmpty
        || !OnHoverChange.IsEmpty
        || !OnWheel.IsEmpty
        || !OnContextMenu.IsEmpty
        || !OnDrag.IsEmpty
        || DragData is not null
        || !OnDragOver.IsEmpty
        || !OnDrop.IsEmpty;

    public readonly bool Draggable => !OnDrag.IsEmpty || DragData is not null;

    public readonly bool IsFocusable => Focusable ?? IsPressable;
}

[FramePath]
internal record struct ResolvedStyle
{
    public Theme Theme;
    public Density Density;
    public Paint Color;
    public string FontFamily;
    public float FontSize;
    public FontWeight FontWeight;
    public FontStyle FontStyle;
    public float LineHeight;
    public float LetterSpacing;
    public bool TabularDigits;
    public TextAlign TextAlign;
    public PointerEvents PointerEvents;

    public static ResolvedStyle Root(Theme theme, Density density) =>
        Defaults(theme, density, PointerEvents.Auto, TextAlign.Left);

    public static ResolvedStyle Defaults(
        Theme theme,
        Density density,
        PointerEvents pointer,
        TextAlign align
    ) =>
        new()
        {
            Theme = theme,
            Density = density,
            Color = theme.Color(ColorToken.Fg),
            FontFamily = theme.FontFamily(FontToken.Body),
            FontSize = theme.Dimensions(density).FontSize(Theming.TextVariant.Body),
            FontWeight = theme.Weight(WeightToken.Content),
            FontStyle = Fonts.FontStyle.Normal,
            TextAlign = align,
            PointerEvents = pointer,
        };

    public readonly ResolvedStyle Under(in InheritStyle own)
    {
        var next = this;
        if (own.Theme is not null || own.Density is not null)
        {
            var theme = own.Theme ?? Theme;
            var density = own.Density ?? Density;
            if (!ReferenceEquals(theme, Theme) || density != Density)
                next = Defaults(theme, density, PointerEvents, TextAlign);
        }

        if (own.Color.Kind != PaintKind.None)
            next.Color = own.Color;
        if (own.FontFamily is not null)
            next.FontFamily = own.FontFamily;
        if (own.TextVariant is { } variant)
            next.FontSize = next.Theme.Dimensions(next.Density).FontSize(variant);
        if (own.FontSize > 0f)
            next.FontSize = own.FontSize;
        if (own.FontWeight != 0)
            next.FontWeight = own.FontWeight;
        if (own.FontStyle is { } style)
            next.FontStyle = style;
        if (own.LineHeight > 0f)
            next.LineHeight = own.LineHeight;
        if (own.LetterSpacing is { } spacing)
            next.LetterSpacing = spacing;
        if (own.TabularDigits is { } tabular)
            next.TabularDigits = tabular;
        if (own.TextAlign != TextAlign.Inherit)
            next.TextAlign = own.TextAlign;
        if (own.PointerEvents != PointerEvents.Inherit)
            next.PointerEvents = own.PointerEvents;
        return next;
    }
}

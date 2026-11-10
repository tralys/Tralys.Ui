using Tralys.Ui.Fonts;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct EditableProps
{
    public TextValue Value { get; init; }

    /// <summary>Reports the edit as it commits: on Enter, or as the focus leaves the input, when it changed.</summary>
    public Callback<string> OnValueChange { get; init; }

    public TextValue Placeholder { get; init; }

    public ComponentSize? Size { get; init; }

    public Length Width { get; init; }

    /// <summary>The most UTF-16 units the edit may hold; 0 for no limit.</summary>
    public int MaxLength { get; init; }

    /// <summary>Outlines the input in the danger colour while editing.</summary>
    public bool Invalid { get; init; }

    /// <summary>Whether the input shows; null leaves it to the editable, starting at <see cref="DefaultEditing"/>.</summary>
    public bool? Editing { get; init; }

    public bool DefaultEditing { get; init; }

    public Callback<bool> OnEditingChange { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>Text that turns into an <see cref="Input"/> in its place when clicked, or on Enter or F2 while focused.
/// Typing makes no string: Enter, or the focus leaving the input, reports the edit through
/// <see cref="EditableProps.OnValueChange"/> once, when it changed, and ends editing; Escape drops it. It shows <see cref="EditableProps.Value"/>, so a source that normalises or
/// refuses an edit shows what it kept.</summary>
public sealed class Editable : Component<EditableProps>
{
    protected override void Render(Ui ui, in EditableProps p)
    {
        var t = ui.Theme;
        var size = p.Size ?? ComponentSize.Md;
        var m = t.Metrics(size);
        var model = ui.Remember(static () => new EditableModel());
        model.Host = ui.Self(p);
        model.Own = ui.State(p.DefaultEditing);
        model.Root = ui.Ref();
        model.Text = ui.Ref();
        var disabled = ui.IsDisabled;
        var editing = model.IsEditing && !disabled;
        if (!model.IsOpen)
            model.Draft.Set(p.Value.Span);
        var within = ui.IsFocusWithin;
        ui.Effect(model, editing, static (m, editing, _) => m.Opened(editing));
        ui.Effect(model, within, static (m, within, _) => m.FocusWithin(within));

        var hovered = ui.IsHovered && !editing && !disabled;
        var focused = ui.IsFocused && !editing;
        var shown = !p.Value.Span.IsEmpty;
        using (
            ui.Box(
                width: p.Width,
                cursor: disabled ? null : Cursors.IBeam,
                focusable: true,
                onClick: editing ? default : model.Begin,
                onKeyDown: model.Keys,
                nodeRef: model.Root
            )
        )
        {
            using (
                ui.Box(
                    align: Align.Center,
                    grow: 1f,
                    minHeight: m.Height,
                    padding: ButtonParts.Inside(m.FieldPaddingX, 1f),
                    background: hovered ? t.Color(ColorToken.StateHover) : default(Paint),
                    border: new Border(
                        1f,
                        focused ? t.Color(ColorToken.BorderFocus) : default(Paint)
                    ),
                    radius: t.Radius(RadiusToken.Md),
                    hidden: editing
                )
            )
                ui.Text(
                    shown ? p.Value : p.Placeholder,
                    color: t.Color(
                        disabled ? ColorToken.FgDisabled
                        : !shown ? ColorToken.FgPlaceholder
                        : hovered ? ColorToken.FgStrong
                        : ColorToken.FgMuted
                    ),
                    fontSize: m.FontSize,
                    fontWeight: t.Weight(WeightToken.Interface),
                    trimming: TextTrimming.CharacterEllipsis,
                    shrink: 1f
                );
            if (editing)
                ui.Input(
                    text: model.Draft,
                    placeholder: p.Placeholder,
                    maxLength: p.MaxLength,
                    invalid: p.Invalid,
                    size: size,
                    nodeRef: model.Text,
                    position: Position.Absolute,
                    left: 0f,
                    top: 0f,
                    right: 0f,
                    bottom: 0f
                );
        }
    }
}

[FramePath]
internal sealed class EditableModel
{
    internal readonly Callback Begin;
    internal readonly Handler<KeyEvent> Keys;
    internal readonly ObservableText Draft = new();
    internal Host<EditableProps> Host;
    internal State<bool> Own = null!;
    internal NodeRef Root = null!;
    internal NodeRef Text = null!;
    bool _open;
    bool _within;

    public EditableModel()
    {
        Begin = Callback.Of(this, static m => m.Start());
        Keys = Handler<KeyEvent>.Of(this, static (m, ref e) => m.KeyDown(ref e));
    }

    internal bool IsEditing => Host.Props.Editing ?? Own.Peek;

    internal bool IsOpen => _open;

    void Start()
    {
        if (!IsEditing)
            Set(true);
    }

    void KeyDown(ref KeyEvent e)
    {
        if (e.Modifiers != ModifierKeys.None)
            return;
        if (_open)
        {
            if (e.Key is not (Key.Enter or Key.Escape))
                return;
            End(keep: e.Key == Key.Enter, refocus: true);
            e.StopPropagation();
        }
        else if (e.Key is Key.Enter or Key.F2)
            Start();
        else
            return;
        e.PreventDefault();
    }

    internal void Opened(bool editing)
    {
        _open = editing;
        if (editing)
            Text.Focus();
    }

    internal void FocusWithin(bool within)
    {
        var left = _within && !within;
        _within = within;
        if (left)
            End(keep: true, refocus: false);
    }

    void End(bool keep, bool refocus)
    {
        if (!_open)
            return;
        _open = false;
        Set(false);
        if (keep && !Draft.Span.SequenceEqual(Host.Props.Value.Span))
            Host.Props.OnValueChange.Invoke(Draft.ToString());
        if (refocus)
            Root.Focus();
    }

    void Set(bool editing)
    {
        if (Host.Props.Editing is null)
            Own.Value = editing;
        Host.Props.OnEditingChange.Invoke(editing);
    }
}

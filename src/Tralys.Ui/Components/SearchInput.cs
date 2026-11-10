using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct SearchInputProps
{
    /// <summary>The text it edits, which its owner keeps.</summary>
    public required ObservableText Text { get; init; }

    /// <summary>Runs once after each edit, never for a change from outside.</summary>
    public Callback OnEdit { get; init; }

    public TextValue Placeholder { get; init; }

    /// <summary>The most UTF-16 units typing and pasting may make it hold; 0 for no limit.</summary>
    public int MaxLength { get; init; }

    public FieldVariant Variant { get; init; }

    public ComponentSize? Size { get; init; }

    public Length Width { get; init; }

    /// <summary>Inside the border after the text and the clear button, its presses its own.</summary>
    public Slot Trailing { get; init; }

    public bool Disabled { get; init; }

    public bool AutoFocus { get; init; }

    /// <summary>Bound to its text, the node that takes the focus.</summary>
    public NodeRef? NodeRef { get; init; }

    /// <summary>Runs the search: on Enter, and once the clear button empties it.</summary>
    public Callback OnSubmit { get; init; }

    /// <summary>Runs as the clear button empties it, before <see cref="OnSubmit"/>.</summary>
    public Callback OnClear { get; init; }

    public Callback OnCommit { get; init; }

    public Callback OnEscape { get; init; }
}

/// <summary>An <see cref="Input"/> that searches: it leads with a search glass and shows its clear button while it has
/// text, and clearing it runs <see cref="SearchInputProps.OnSubmit"/> on the emptied text, to search for
/// everything again.</summary>
public sealed class SearchInput : Component<SearchInputProps>
{
    protected override void Render(Ui ui, in SearchInputProps p) =>
        ui.Input(
            text: p.Text,
            onEdit: p.OnEdit,
            placeholder: p.Placeholder,
            maxLength: p.MaxLength,
            clearable: true,
            onClear: Callback.Of(
                ui.Self(p),
                static h =>
                {
                    h.Props.OnClear.Invoke();
                    h.Props.OnSubmit.Invoke();
                }
            ),
            variant: p.Variant,
            size: p.Size,
            width: p.Width,
            leadingIcon: IconGlyph.Of(IconRole.Search),
            trailing: p.Trailing,
            autoFocus: p.AutoFocus,
            nodeRef: p.NodeRef,
            onSubmit: p.OnSubmit,
            onCommit: p.OnCommit,
            onEscape: p.OnEscape
        );
}

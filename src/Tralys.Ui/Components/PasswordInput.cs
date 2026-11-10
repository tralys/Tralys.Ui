using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct PasswordInputProps
{
    /// <summary>The text it edits, which its owner keeps.</summary>
    public required ObservableText Text { get; init; }

    public TextValue Placeholder { get; init; }

    /// <summary>Shows the characters in place of the dots, as a reveal toggle beside it asks; it stays a password field
    /// otherwise.</summary>
    public bool Revealed { get; init; }

    public bool ReadOnly { get; init; }

    /// <summary>The most UTF-16 units typing and pasting may make it hold; 0 for no limit.</summary>
    public int MaxLength { get; init; }

    public bool Invalid { get; init; }

    public FieldVariant Variant { get; init; }

    public ComponentSize? Size { get; init; }

    public Length Width { get; init; }

    /// <summary>Inside the border before the text, its presses its own.</summary>
    public Slot Leading { get; init; }

    /// <summary>Inside the border after the text, its presses its own: where a reveal toggle sits.</summary>
    public Slot Trailing { get; init; }

    public bool Disabled { get; init; }

    public bool AutoFocus { get; init; }

    /// <summary>The field Enter moves the focus to, when it can take it, in place of submitting.</summary>
    public NodeRef? Next { get; init; }

    /// <summary>Bound to its text, the node that takes the focus.</summary>
    public NodeRef? NodeRef { get; init; }

    public Callback OnSubmit { get; init; }

    public Callback OnCommit { get; init; }

    public Callback OnEscape { get; init; }
}

/// <summary>An <see cref="Input"/> for a password: it shows a dot for each character, copies and cuts nothing, keeps
/// no undo history and raises the password keyboard. While <see cref="PasswordInputProps.Revealed"/> it shows the
/// characters and is a password field still.</summary>
public sealed class PasswordInput : Component<PasswordInputProps>
{
    protected override void Render(Ui ui, in PasswordInputProps p) =>
        ui.Input(
            text: p.Text,
            placeholder: p.Placeholder,
            kind: TextInputKind.Password,
            masked: !p.Revealed,
            readOnly: p.ReadOnly,
            maxLength: p.MaxLength,
            invalid: p.Invalid,
            variant: p.Variant,
            size: p.Size,
            width: p.Width,
            leading: p.Leading,
            trailing: p.Trailing,
            autoFocus: p.AutoFocus,
            next: p.Next,
            nodeRef: p.NodeRef,
            onSubmit: p.OnSubmit,
            onCommit: p.OnCommit,
            onEscape: p.OnEscape
        );
}

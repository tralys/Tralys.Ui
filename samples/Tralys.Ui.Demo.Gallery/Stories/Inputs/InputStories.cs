using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Input>("Inputs")]
static class InputStories
{
    static readonly Slot Talk = Slot.Of(static ui =>
        ui.Button(text: "Talk", size: ComponentSize.Xs, variant: ComponentVariant.Ghost)
    );

    static readonly Slot Send = Slot.Of(static ui =>
        ui.IconButton(
            icon: Icons.Send,
            size: ComponentSize.Xs,
            scheme: ColorScheme.Secondary,
            variant: ComponentVariant.Ghost
        )
    );

    static readonly Slot Glass = Slot.Of(static ui =>
        ui.Icon(
            glyph: Icons.Search,
            size: 14,
            color: ui.Theme.Color(ColorToken.FgMuted),
            margin: new Thickness(8f, 0f, 0f, 0f)
        )
    );

    [FramePath]
    public static void States(Ui ui)
    {
        var text = ui.Remember(static () => new ObservableText("Edit me"));
        var email = ui.Remember(static () => new ObservableText());
        var invalid = ui.Remember(static () => new ObservableText("not an email"));
        var readOnly = ui.Remember(static () => new ObservableText("Read only"));
        var disabled = ui.Remember(static () => new ObservableText("Disabled"));
        var flush = ui.Remember(static () => new ObservableText("Flush"));
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 12, padding: 16))
        {
            ui.Input(text: text, width: 240);
            ui.Input(text: email, placeholder: "you@example.com", width: 240);
            ui.Input(text: invalid, invalid: true, width: 240);
            ui.Input(text: readOnly, readOnly: true, width: 240);
            ui.Input(text: disabled, disabled: true, width: 240);
            ui.Input(text: flush, variant: FieldVariant.Flush, width: 240);
        }
    }

    [FramePath]
    public static void Sizes(Ui ui)
    {
        var small = ui.Remember(static () => new ObservableText());
        var medium = ui.Remember(static () => new ObservableText());
        var large = ui.Remember(static () => new ObservableText());
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 12, padding: 16))
        {
            ui.Input(text: small, placeholder: "Small", size: ComponentSize.Sm, width: 200);
            ui.Input(text: medium, placeholder: "Medium", size: ComponentSize.Md, width: 200);
            ui.Input(text: large, placeholder: "Large", size: ComponentSize.Lg, width: 200);
        }
    }

    [FramePath]
    public static void Default(Ui ui)
    {
        var hello = ui.Remember(static () => new ObservableText("Hello"));
        var typed = ui.Remember(static () => new ObservableText());
        var disabled = ui.Remember(static () => new ObservableText("Disabled"));
        using (ui.Box(direction: Direction.Column, gap: 16, padding: 16, width: 352))
        {
            ui.Input(text: hello);
            ui.Input(text: typed, clearable: true, placeholder: "Type here...");
            ui.Input(text: disabled, disabled: true);
        }
    }

    [FramePath]
    public static void Placeholder(Ui ui)
    {
        var name = ui.Remember(static () => new ObservableText());
        var email = ui.Remember(static () => new ObservableText());
        using (ui.Box(direction: Direction.Column, gap: 16, padding: 16, width: 352))
        {
            ui.Input(text: name, placeholder: "Type your name...");
            ui.Input(text: email, placeholder: "Type your email...");
        }
    }

    [FramePath]
    public static void Adornments(Ui ui)
    {
        var message = ui.Remember(static () => new ObservableText());
        var search = ui.Remember(static () => new ObservableText("Search term"));
        using (ui.Box(direction: Direction.Column, gap: 16, padding: 16, width: 352))
        {
            ui.Input(
                text: message,
                placeholder: "Message…",
                size: ComponentSize.Sm,
                leading: Talk,
                trailing: Send
            );
            ui.Input(text: search, clearable: true, leading: Glass);
        }
    }

    [FramePath]
    public static void Counter(Ui ui)
    {
        var always = ui.Remember(static () => new ObservableText("Always counted"));
        var near = ui.Remember(static () => new ObservableText("Shows near the max"));
        var hidden = ui.Remember(static () => new ObservableText("Hidden"));
        using (ui.Box(direction: Direction.Column, gap: 16, padding: 16, width: 352))
        {
            ui.Input(text: always, maxLength: 20);
            ui.Input(text: near, maxLength: 20, countWithin: 5);
            ui.Input(text: hidden, maxLength: 20, countWithin: 5);
        }
    }

    [FramePath]
    public static void Invalid(Ui ui)
    {
        var t = ui.Theme;
        var name = ui.Remember(static () => new ObservableText("root_admin"));
        using (ui.Box(direction: Direction.Column, gap: 4, padding: 16, width: 272))
        {
            ui.Input(text: name, invalid: true);
            ui.Text(
                "That name is taken",
                color: t.Color(ColorToken.FgDanger),
                fontWeight: t.Weight(WeightToken.Interface),
                variant: TextVariant.Caption,
                wrap: true
            );
        }
    }

    [FramePath]
    public static void ReadOnly(Ui ui)
    {
        var text = ui.Remember(static () =>
            new ObservableText("Read-only: selectable, not editable")
        );
        using (ui.Box(direction: Direction.Column, align: Align.Start, padding: 16))
            ui.Input(text: text, readOnly: true, width: 320);
    }

    [FramePath]
    public static void Bound(Ui ui)
    {
        var name = ui.Remember(static () => new ObservableText("Aria"));
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8, padding: 16))
        {
            Caption(
                ui,
                "Bound both ways: what is typed reaches the model on every edit, and the line below follows it"
            );
            ui.Input(text: name, width: 280);
            ui.Text(
                ui.Format($"Welcome back, {name.Span}."),
                fontWeight: ui.Theme.Weight(WeightToken.Interface),
                wrap: true
            );
        }
    }

    [FramePath]
    public static void Password(Ui ui)
    {
        var password = ui.Remember(static () => new ObservableText("hunter2"));
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8, padding: 16))
        {
            Caption(
                ui,
                "PasswordChar shows a dot per character; nothing copies out and there is no undo history"
            );
            ui.Input(text: password, kind: TextInputKind.Password, width: 280);
            Caption(ui, ui.Format($"The model holds: {password.Span}"));
        }
    }

    [FramePath]
    public static void Multiline(Ui ui)
    {
        var notes = ui.Remember(static () =>
            new ObservableText("Meet at the front desk.\nBring the badge.")
        );
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8, padding: 16))
        {
            Caption(
                ui,
                "AcceptsReturn and Wrap: Enter types a line break, long lines wrap, and the box scrolls to the caret"
            );
            ui.Input(text: notes, multiline: true, width: 280, height: 96);
        }
    }

    [FramePath]
    public static void Limits(Ui ui)
    {
        var readOnly = ui.Remember(static () => new ObservableText("Selectable, not editable"));
        var limited = ui.Remember(static () => new ObservableText("12345678"));
        var centred = ui.Remember(static () => new ObservableText("Centred"));
        var disabled = ui.Remember(static () => new ObservableText("Disabled"));
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8, padding: 16))
        {
            Caption(ui, "Read-only, MaxLength 8, centred, and disabled");
            ui.Input(text: readOnly, readOnly: true, width: 280);
            ui.Input(text: limited, maxLength: 8, width: 280);
            ui.Input(text: centred, textAlign: TextAlign.Center, width: 280);
            ui.Input(text: disabled, disabled: true, width: 280);
        }
    }

    [FramePath]
    static void Caption(
        Ui ui,
        TextValue text,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Text(
            text,
            color: ui.Theme.Color(ColorToken.FgMuted),
            fontWeight: ui.Theme.Weight(WeightToken.Interface),
            wrap: true,
            file: file,
            line: line
        );
}

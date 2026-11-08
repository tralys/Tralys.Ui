using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct ActionPromptProps
{
    /// <summary>The action the prompt shows the glyph of and runs when clicked.</summary>
    public UiAction Action { get; init; }

    /// <summary>The key or button the prompt shows while it has no <see cref="Action"/>.</summary>
    public Chord Chord { get; init; }

    /// <summary>What the action does, on one line after the glyph.</summary>
    public TextValue Label { get; init; }
}

/// <summary>An action's <see cref="InputGlyph"/> for the device in hand, then 8 on, what it does. Clicked or tapped,
/// it runs its action as the key or button would, under a hand cursor and with its label lit while hovered; it is
/// never a Tab stop.</summary>
public sealed class ActionPrompt : Component<ActionPromptProps>
{
    protected override void Render(Ui ui, in ActionPromptProps p)
    {
        var prompt = ui.Remember(static () => new Prompt());
        prompt.Action = p.Action;
        ui.Effect(prompt, 0, static (m, _, cx) => m.View = cx.View);
        var runs = p.Action.Name is not null;
        using (
            ui.Box(
                align: Align.Center,
                gap: 8f,
                cursor: runs ? Cursors.Hand : null,
                focusable: false,
                onClick: runs ? prompt.Run : default
            )
        )
        {
            ui.InputGlyph(action: p.Action, chord: p.Chord);
            var t = ui.Theme;
            ui.Text(
                p.Label,
                color: runs && ui.IsHovered ? t.Color(ColorToken.FgStrong) : default(Paint),
                fontWeight: t.Weight(WeightToken.Interface)
            );
        }
    }

    [FramePath]
    sealed class Prompt
    {
        internal readonly Callback Run;
        internal UiAction Action;
        internal UiView? View;

        public Prompt() => Run = Callback.Of(this, static m => m.View?.RunAction(m.Action));
    }
}

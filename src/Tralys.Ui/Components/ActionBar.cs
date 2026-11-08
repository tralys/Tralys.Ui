namespace Tralys.Ui.Components;

public readonly record struct ActionBarProps;

/// <summary>A footer of prompts for the shown actions of the view's newest active action scope, one
/// <see cref="ActionPrompt"/> each in the scope's order, in a row against its end edge, 24 apart. It follows the scope
/// on top, its actions and the device in hand as they change; a prompt clicked or tapped runs its action.</summary>
public sealed class ActionBar : Component<ActionBarProps>
{
    protected override void Render(Ui ui, in ActionBarProps p)
    {
        using (ui.Box(justify: Justify.End, gap: 24f))
            foreach (var action in ui.ScopeActions)
                if (action.Action.Name is not null)
                    ui.ActionPrompt(
                        action: action.Action,
                        label: action.Label,
                        key: action.Action.Name
                    );
    }
}

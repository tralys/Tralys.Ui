using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>Where the work a <see cref="StatusBadge"/> labels stands.</summary>
public enum StatusBadgeState : byte
{
    Pending,
    Active,
    Done,
    Locked,
}

public readonly record struct StatusBadgeProps
{
    public TextValue Text { get; init; }

    public StatusBadgeState State { get; init; }
}

/// <summary>A <see cref="Tag"/> whose scheme and icon follow its state, so one state reads the same in every window:
/// pending on the secondary scheme, active on the primary, done on the success scheme with a check, locked on the
/// secondary with a padlock.</summary>
public sealed class StatusBadge : Component<StatusBadgeProps>
{
    protected override void Render(Ui ui, in StatusBadgeProps p) =>
        ui.Tag(
            text: p.Text,
            icon: p.State switch
            {
                StatusBadgeState.Done => IconGlyph.Of(IconRole.Check),
                StatusBadgeState.Locked => IconGlyph.Of(IconRole.Lock),
                _ => default,
            },
            scheme: p.State switch
            {
                StatusBadgeState.Active => ColorScheme.Primary,
                StatusBadgeState.Done => ColorScheme.Success,
                _ => ColorScheme.Secondary,
            }
        );
}

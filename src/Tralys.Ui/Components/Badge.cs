using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct BadgeProps
{
    /// <summary>What there is to count; 0 or less shows nothing.</summary>
    public int Count { get; init; }

    /// <summary>Written before the count, as "@" marks mentions.</summary>
    public TextValue Prefix { get; init; }

    /// <summary>The scheme; <see cref="ColorScheme.Primary"/> by default.</summary>
    public ColorScheme? Scheme { get; init; }

    /// <summary>The variant; <see cref="ComponentVariant.Filled"/> by default.</summary>
    public ComponentVariant? Variant { get; init; }
}

/// <summary>A count pill that shows nothing, taking no space, with nothing to count: a <see cref="Tag"/>'s look 14
/// tall, at least as wide and fully rounded, borderless but for a 1-unit edge on the secondary scheme. Past 99 it reads
/// "99+".</summary>
public sealed class Badge : Component<BadgeProps>
{
    const int MostShown = 99;

    protected override void Render(Ui ui, in BadgeProps p)
    {
        if (p.Count <= 0)
            return;
        var t = ui.Theme;
        var scheme = p.Scheme ?? ColorScheme.Primary;
        ui.Pill(
            text: p.Count > MostShown
                ? ui.Format($"{p.Prefix}{MostShown}+")
                : ui.Format($"{p.Prefix}{p.Count}"),
            icon: default,
            look: t.Palette(scheme, p.Variant ?? ComponentVariant.Filled, ui.LabelState()),
            height: 14f,
            minWidth: 14f,
            padX: 4f,
            radius: 7f,
            border: scheme == ColorScheme.Secondary ? 1f : 0f
        );
    }
}

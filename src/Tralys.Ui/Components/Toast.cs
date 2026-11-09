using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>What a <see cref="Toast"/> tells of, which picks its colour and mark.</summary>
public enum ToastSeverity : byte
{
    Success,
    Warning,
    Error,
}

public readonly record struct ToastProps
{
    public TextValue Title { get; init; }

    /// <summary>A muted line under the title.</summary>
    public TextValue Description { get; init; }

    public ToastSeverity Severity { get; init; }

    /// <summary>Seconds it shows in a <see cref="Toaster"/> before it reports <see cref="OnDismiss"/>, with its fade:
    /// the toaster's duration by default, and more than 0.</summary>
    public float? Duration { get; init; }

    /// <summary>Reported once its time in a <see cref="Toaster"/> is up but for its fade; the caller removes it then,
    /// and it fades out.</summary>
    public Callback OnDismiss { get; init; }
}

/// <summary>One toast: its title, and a description under it, beside the mark of its
/// <see cref="ToastProps.Severity"/> in the severity's colour, on an overlay card edged in that colour, or on the skin's
/// toast frame when it has one. It shows at once at full opacity, and fades out over 0.15 s as it leaves. A
/// <see cref="Toaster"/> times it; anywhere else it only shows.</summary>
public sealed class Toast : Component<ToastProps>
{
    /// <summary>How long a toast takes to fade out as it leaves, and so how early its toaster reports it.</summary>
    internal const float LeaveSeconds = 0.15f;

    const float GlyphSize = 14f;
    const float GlyphGap = 8f;

    static readonly IEasingFunction Linear = new KeySpline(0f, 0f, 1f, 1f);

    protected override void Render(Ui ui, in ToastProps p)
    {
        if (p.Duration is { } duration && !(duration > 0f && float.IsFinite(duration)))
            throw Refused(duration);
        var shown = ui.Presence(
            enter: Transition.Instant,
            exit: new Transition(LeaveSeconds, Linear)
        );
        var toaster = ui.Use(Toaster.Context);
        if (toaster.Timer is { } timer)
        {
            var entry = ui.Remember(timer, static t => t.Join());
            entry.Host = ui.Self(p);
            entry.Life = p.Duration ?? toaster.Duration;
            entry.Leaving = ui.IsExiting;
            ui.Effect(entry, entry, static (e, _, cx) => e.Show(cx.View));
        }

        var t = ui.Theme;
        var (edge, ink, mark) = p.Severity switch
        {
            ToastSeverity.Warning => (
                ColorToken.BgWarning,
                ColorToken.FgWarning,
                IconRole.AlertTriangle
            ),
            ToastSeverity.Error => (ColorToken.BgDanger, ColorToken.FgDanger, IconRole.XCircle),
            _ => (ColorToken.BgSuccess, ColorToken.FgSuccess, IconRole.CheckCircle),
        };
        var color = t.Color(ink);
        var art = t.Skin(SkinPart.ToastFrame);
        using (ui.Box(direction: Direction.Column, opacity: shown))
        {
            if (art is { } frame)
                ui.Art(frame);
            using (
                ui.Box(
                    align: Align.Center,
                    gap: GlyphGap,
                    margin: art is { } inset ? AnimatedParts.Inset(inset) : default,
                    padding: t.Metrics(ComponentSize.Sm).Padding,
                    background: art is null ? t.Color(ColorToken.BgOverlay) : default(Paint),
                    border: new Border(1f, art is null ? t.Color(edge) : default(Paint)),
                    radius: art is null ? t.Radius(RadiusToken.Md) : default,
                    shadow: art is null ? Shadow(t) : default,
                    color: color,
                    fontWeight: t.Weight(WeightToken.Interface)
                )
            )
            {
                ui.Icon(glyph: IconGlyph.Of(mark), size: GlyphSize, color: color, shrink: 0f);
                using (ui.Box(direction: Direction.Column, grow: 1f))
                {
                    ui.Text(p.Title, wrap: true);
                    if (!p.Description.IsNull)
                        ui.Text(
                            p.Description,
                            color: t.Color(ColorToken.FgMuted),
                            variant: TextVariant.Caption,
                            wrap: true
                        );
                }
            }
        }
    }

    static BoxShadow Shadow(UiTheme t) =>
        new(0f, 2f, 10f, 0f, t.Color(ColorToken.BgShadow).WithOpacity(0.6f));

    static ArgumentOutOfRangeException Refused(float duration) =>
        new(nameof(ToastProps.Duration), duration, "A toast's duration is more than 0 seconds.");
}

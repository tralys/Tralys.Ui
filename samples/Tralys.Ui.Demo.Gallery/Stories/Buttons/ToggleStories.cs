using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Toggle>("Buttons")]
static class ToggleStories
{
    static readonly ColorScheme[] Schemes =
    [
        ColorScheme.Primary,
        ColorScheme.Secondary,
        ColorScheme.Success,
        ColorScheme.Danger,
    ];

    static readonly string[] Names = Enum.GetNames<ColorScheme>();

    static readonly IconGlyph[] SchemeIcons =
    [
        Gallery.Icons.Heart,
        Gallery.Icons.Bell,
        Gallery.Icons.Bookmark,
        Gallery.Icons.Star,
    ];

    /// <summary>Toggle buttons: one to press on and off, one held on, and one disabled.</summary>
    [FramePath]
    public static void Default(Ui ui)
    {
        var on = ui.State(false);
        using (ui.Box(align: Align.Start, gap: 16, padding: 16))
        {
            ui.Toggle(text: "Unchecked", value: on.Value, onValueChange: on.Setter);
            ui.Toggle(text: "Checked", value: true);
            ui.Toggle(text: "Disabled", disabled: true);
        }
    }

    /// <summary>A toggle held on in each colour scheme.</summary>
    [FramePath]
    public static void Checked(Ui ui)
    {
        using (ui.Box(align: Align.Start, gap: 16, padding: 16))
            foreach (var scheme in Schemes)
                ui.Toggle(
                    text: Names[(int)scheme],
                    value: true,
                    activeScheme: scheme,
                    key: (long)scheme
                );
    }

    /// <summary>Disabled toggles, off and on.</summary>
    [FramePath]
    public static void Disabled(Ui ui)
    {
        using (ui.Box(align: Align.Start, gap: 16, padding: 16))
        {
            ui.Toggle(text: "Unchecked Disabled", disabled: true);
            ui.Toggle(text: "Checked Disabled", value: true, disabled: true);
        }
    }

    /// <summary>Heart, bell and bookmark icon toggles, each pressed on and off.</summary>
    [FramePath]
    public static void Icons(Ui ui)
    {
        var heart = ui.State(false);
        var bell = ui.State(false);
        var bookmark = ui.State(false);
        using (ui.Box(align: Align.Start, gap: 16, padding: 16))
        {
            ui.Toggle(icon: Gallery.Icons.Heart, value: heart.Value, onValueChange: heart.Setter);
            ui.Toggle(icon: Gallery.Icons.Bell, value: bell.Value, onValueChange: bell.Setter);
            ui.Toggle(
                icon: Gallery.Icons.Bookmark,
                value: bookmark.Value,
                onValueChange: bookmark.Setter
            );
        }
    }

    /// <summary>Icon toggles held on in each colour scheme, filled and then outlined.</summary>
    [FramePath]
    public static void IconsChecked(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 32, padding: 16))
        {
            using (ui.Box(gap: 16))
                foreach (var scheme in Schemes)
                    ui.Toggle(
                        icon: SchemeIcons[(int)scheme],
                        value: true,
                        activeScheme: scheme,
                        key: (long)scheme
                    );
            using (ui.Box(gap: 16))
                foreach (var scheme in Schemes)
                    ui.Toggle(
                        icon: SchemeIcons[(int)scheme],
                        value: true,
                        variant: ComponentVariant.Outlined,
                        activeScheme: scheme,
                        key: (long)scheme
                    );
        }
    }

    /// <summary>Icon toggles that change when on: play turns to pause in Success, and a star takes the Primary colour;
    /// each pair has one to press and one held on.</summary>
    [FramePath]
    public static void ActiveIcon(Ui ui)
    {
        var playing = ui.State(false);
        var starred = ui.State(false);
        using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 32, padding: 16))
        {
            using (ui.Box(gap: 16))
            {
                ui.Toggle(
                    icon: Gallery.Icons.PlayCircle,
                    activeIcon: Gallery.Icons.PauseCircle,
                    activeScheme: ColorScheme.Success,
                    value: playing.Value,
                    onValueChange: playing.Setter
                );
                ui.Toggle(
                    icon: Gallery.Icons.PlayCircle,
                    activeIcon: Gallery.Icons.PauseCircle,
                    activeScheme: ColorScheme.Success,
                    value: true
                );
            }
            using (ui.Box(gap: 16))
            {
                ui.Toggle(
                    icon: Gallery.Icons.Star,
                    activeScheme: ColorScheme.Primary,
                    value: starred.Value,
                    onValueChange: starred.Setter
                );
                ui.Toggle(icon: Gallery.Icons.Star, activeScheme: ColorScheme.Primary, value: true);
            }
        }
    }
}

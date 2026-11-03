using Tralys.Ui.Mvvm;

namespace Tralys.Ui.Bgfx.Tests;

/// <summary>A box filling its view, in a background and opacity a test changes.</summary>
sealed partial class Swatch : ObservableObject
{
    public Swatch() => Opacity = 1f;

    [ObservableProperty]
    public partial Color Background { get; set; }

    [ObservableProperty]
    public partial float Opacity { get; set; }

    public void Draw(Ui ui)
    {
        using (
            ui.Box(grow: 1, alignSelf: AlignSelf.Stretch, background: Background, opacity: Opacity)
        ) { }
    }
}

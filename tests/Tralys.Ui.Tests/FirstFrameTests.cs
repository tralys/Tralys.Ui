using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class FirstFrameTests
{
    static Paint Dark(ColorToken token) => Themes.Dark.Color(token);

    [Test]
    public async Task What_a_click_mounts_wears_its_autofocus_and_the_hover_of_the_pointer_over_it_on_its_first_frame()
    {
        var text = new ObservableText();
        using var driver = UiDriver.Render(ui =>
        {
            var open = ui.State(false);
            using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 8, padding: 8))
            {
                if (open.Value)
                {
                    ui.Editable(value: "Under the pointer", width: 200, id: "editable");
                    ui.Input(text: text, autoFocus: true, width: 200);
                }
                else
                    ui.Button(
                        text: "Open",
                        width: 200,
                        onClick: Callback.Of(open, static o => o.Value = true),
                        id: "open"
                    );
            }
        });

        driver.Click(driver.Get("open"), settle: false);
        driver.Frame();

        await Assert
            .That(Nodes.Find(driver.Get("editable"), NodeKind.Box).Visual.Background)
            .IsEqualTo(Dark(ColorToken.StateHover));
        await Assert
            .That(driver.RootOf().Visual.BorderPaint)
            .IsEqualTo(Dark(ColorToken.BorderFocus));
    }
}

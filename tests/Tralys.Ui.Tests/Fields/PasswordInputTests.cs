using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class PasswordInputTests
{
    [Test]
    public async Task A_password_shows_one_dot_per_cluster_as_wide_as_the_dots_shown_plain()
    {
        var password = new ObservableText("😀a");
        var plain = new ObservableText("••");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.PasswordInput(text: password, id: "password");
                ui.Input(text: plain, id: "plain");
            }
        });

        await Assert
            .That(driver.TextOf("password").Bounds.Width)
            .IsEqualTo(driver.TextOf("plain").Bounds.Width);
    }

    [Test]
    public async Task Its_reveal_toggle_lays_out_the_characters_and_hiding_shows_the_dots_again()
    {
        var revealed = new Choices();
        var password = new ObservableText("hunter2");
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(align: Align.Start))
                ui.PasswordInput(
                    text: password,
                    revealed: revealed.On,
                    trailing: Slot.Of(
                        revealed,
                        static (ui, r) =>
                            ui.Toggle(
                                value: r.On,
                                onValueChange: r.SetOn,
                                text: "Show",
                                id: "reveal"
                            )
                    )
                );
        });
        var masked = driver.TextOf().Bounds.Width;

        driver.Click(driver.Get("reveal"));
        var shown = driver.TextOf().Bounds.Width;
        driver.Click(driver.Get("reveal"));

        await Assert.That(shown).IsNotEqualTo(masked);
        await Assert.That(driver.TextOf().Bounds.Width).IsEqualTo(masked);
    }

    [Test]
    public async Task A_password_has_no_word_breaks_so_word_moves_and_erases_reach_its_ends_and_a_double_click_selects_all()
    {
        var entry = new Entry("ab cd");
        using var driver = UiDriver.Render(ui => ui.PasswordInput(text: entry.Text));

        driver.CaretAt(1);
        driver.Press(Key.Right, ModifierKeys.Control);
        var right = driver.Selection();
        driver.CaretAt(4);
        driver.Press(Key.Left, ModifierKeys.Control);
        var left = driver.Selection();
        driver.Unhover();
        driver.DoubleClick(driver.TextOf());
        var word = driver.Selection();
        driver.CaretAt(4);
        driver.Press(Key.BackSpace, ModifierKeys.Control);

        await Assert.That((right, left, word)).IsEqualTo(((5, 5), (0, 0), (0, 5)));
        await Assert.That(entry.Value).IsEqualTo("d");
    }

    [Test]
    public async Task A_password_revealed_or_not_copies_and_cuts_nothing_keeps_no_undo_and_asks_for_the_password_keyboard()
    {
        var entry = new Entry("secret");
        var revealed = false;
        using var driver = UiDriver.Render(ui =>
        {
            using (ui.Box(direction: Direction.Column, align: Align.Start))
            {
                ui.PasswordInput(text: entry.Text, revealed: revealed, width: 200);
                using (ui.Box(focusable: true, width: 20, height: 20, id: "other")) { }
            }
        });
        driver.Focus(driver.TextOf());

        driver.Press(Key.C, ModifierKeys.Control);
        driver.Press(Key.X, ModifierKeys.Control);
        var kept = (driver.Clipboard, entry.Value);
        driver.Type("abc");
        driver.Press(Key.Z, ModifierKeys.Control);
        var masked = (entry.Value, driver.TextInput?.Kind);
        revealed = true;
        driver.Rerender();
        driver.Press(Key.A, ModifierKeys.Control);
        driver.Press(Key.C, ModifierKeys.Control);
        driver.Press(Key.BackSpace);
        driver.Press(Key.Z, ModifierKeys.Control);
        var shown = (driver.Clipboard, entry.Value, driver.TextInput?.Kind);
        driver.Focus(driver.Get("other"));

        await Assert.That(kept).IsEqualTo(((string?)null, "secret"));
        await Assert.That(masked).IsEqualTo(("abc", (TextInputKind?)TextInputKind.Password));
        await Assert
            .That(shown)
            .IsEqualTo(((string?)null, "", (TextInputKind?)TextInputKind.Password));
        await Assert.That(driver.TextInput).IsNull();
    }
}

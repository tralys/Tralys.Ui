using Tralys.Ui.Components;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class SearchInputTests
{
    [Test]
    public async Task A_search_input_leads_with_a_search_glass_and_shows_its_clear_button_only_while_it_has_text()
    {
        var entry = new Entry();
        using var driver = UiDriver
            .Render(ui => ui.SearchInput(text: entry.Text, width: 200))
            .WithIcons();
        var glyph = Nodes.Glyph(driver.Get<SearchInput>());
        var empty = driver.All<InputClear>().Count;

        driver.Focus(driver.TextOf());
        driver.Type("a");

        await Assert.That(glyph.ViewBounds.Right).IsLessThanOrEqualTo(driver.TextOf().ViewBounds.X);
        await Assert
            .That(FirstText(glyph))
            .IsEqualTo(char.ConvertFromUtf32(0xE000 + (int)IconRole.Search));
        await Assert.That((empty, driver.All<InputClear>().Count)).IsEqualTo((0, 1));
    }

    [Test]
    public async Task A_small_touch_field_keeps_its_height_while_its_clear_button_shows()
    {
        var entry = new Entry();
        using var driver = UiDriver
            .Render(
                ui =>
                {
                    using (ui.Box(align: Align.Start))
                        ui.SearchInput(
                            text: entry.Text,
                            size: ComponentSize.Sm,
                            width: 200,
                            id: "search"
                        );
                },
                density: Density.Touch
            )
            .WithIcons();
        var empty = driver.Get("search").ViewBounds.Height;

        entry.Value = "abc";
        driver.Rerender();

        await Assert.That(driver.All<InputClear>().Count).IsEqualTo(1);
        await Assert.That(driver.Get("search").ViewBounds.Height).IsEqualTo(empty);
    }

    [Test]
    public async Task Its_clear_button_empties_it_at_once_searches_for_everything_and_keeps_the_focus_where_a_trailing_button_does_neither()
    {
        var entry = new Entry("abc");
        var log = new List<(string, string)>();
        var go = new Counter();
        using var driver = UiDriver
            .Render(ui =>
                ui.SearchInput(
                    text: entry.Text,
                    onSubmit: Fields.Into(log, "search", entry.Text),
                    onCommit: Fields.Into(log, "commit", entry.Text),
                    width: 240,
                    trailing: Slot.Of(
                        go,
                        static (ui, go) =>
                        {
                            using (
                                ui.Box(
                                    width: 24,
                                    height: 24,
                                    focusable: false,
                                    onClick: Callback.Of(go, static g => g.Value++),
                                    id: "go"
                                )
                            ) { }
                        }
                    )
                )
            )
            .WithIcons();
        driver.CaretAt(1);

        driver.Click(driver.Get("go"));
        var after = (entry.Value, log.Count, go.Value);
        driver.Click(driver.Get<InputClear>());

        await Assert.That(after).IsEqualTo(("abc", 0, 1));
        await Assert.That(entry.Reports).IsEquivalentTo([""]);
        await Assert.That(log).IsEquivalentTo([("search", ""), ("commit", "")]);
        await Assert.That(driver.Focused).IsSameReferenceAs(driver.TextOf());
    }

    static string FirstText(Node within)
    {
        foreach (var child in within.Children)
            if (child.Kind == NodeKind.Text)
                return child.Text.ToString();
        return "";
    }
}

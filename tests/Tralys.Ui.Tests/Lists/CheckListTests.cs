using System.Numerics;
using Tralys.Ui.Components;
using Tralys.Ui.Testing;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Tests;

public sealed class CheckListTests
{
    static IItemsSource<string> Kinds() => Words.Of("Images", "Audio", "Text", "Sheets");

    static readonly TextOf<string> GroupOf = static kind =>
        kind is "Images" or "Audio" ? "Media" : "Documents";

    static UiDriver Render(CheckListFilter filter, float maxHeight = 0)
    {
        var kinds = Kinds();
        return UiDriver.Render(
            ui =>
            {
                using (ui.Box(direction: Direction.Column, align: Align.Start))
                    ui.CheckList(
                        items: kinds,
                        itemKey: Words.ByText,
                        itemText: Words.Text,
                        itemGroup: GroupOf,
                        value: filter.Checked,
                        onValueChange: filter.Change,
                        width: 200,
                        maxHeight: maxHeight > 0 ? maxHeight : Length.Auto
                    );
            },
            new Size(400, 400)
        );
    }

    static bool Ticked(UiDriver driver, string label) =>
        driver
            .All<Checkbox<bool>>()
            .Single(n => n.Props<CheckboxProps<bool>>().Label.ToString() == label)
            .Props<CheckboxProps<bool>>()
            .Value;

    static Node Box(UiDriver driver, string label) =>
        driver
            .All<Checkbox<bool>>()
            .Single(n => n.Props<CheckboxProps<bool>>().Label.ToString() == label);

    [Test]
    public async Task A_press_on_a_box_reports_its_key_toggled_and_it_shows_checked_once_the_value_follows()
    {
        var reported = new List<SelectionChange>();
        var kinds = Kinds();
        var checkedKeys = new CheckListFilter();
        using var driver = UiDriver.Render(ui =>
            ui.CheckList(
                items: kinds,
                itemKey: Words.ByText,
                itemText: Words.Text,
                value: checkedKeys.Checked,
                onValueChange: Callback<SelectionChange>.Of(
                    reported,
                    checkedKeys,
                    static (r, f, change) =>
                    {
                        r.Add(change);
                        f.Change.Invoke(change);
                    }
                ),
                width: 200
            )
        );

        driver.Click(Box(driver, "Audio"));
        var once = Ticked(driver, "Audio");
        driver.Click(Box(driver, "Audio"));

        await Assert
            .That(reported.Select(c => (c.Action, c.Key.ToString())))
            .IsEquivalentTo([(SelectionAction.Toggle, "Audio"), (SelectionAction.Toggle, "Audio")]);
        await Assert.That(once).IsTrue();
        await Assert.That(Ticked(driver, "Audio")).IsFalse();
    }

    [Test]
    public async Task The_first_item_of_each_group_shows_a_muted_caption_naming_it()
    {
        using var driver = Render(new CheckListFilter());

        var captions = driver
            .All<CheckList.Item>()
            .Select(n => n.Props<CheckListItemProps>().Group.ToString())
            .ToList();

        await Assert.That(captions).IsEquivalentTo(["Media", "", "Documents", ""]);
        await Assert
            .That(driver.GetText("Media").Inherit.Color)
            .IsEqualTo(Rows.Dark(ColorToken.FgMuted));
    }

    [Test]
    public async Task Rows_past_its_most_height_scroll_down_and_never_sideways()
    {
        using var driver = Render(new CheckListFilter(), maxHeight: 60);
        var scroller = driver.GetText("Images").Parent!;
        for (; scroller.Scroller is null; scroller = scroller.Parent!) { }

        driver.Wheel(scroller, new Vector2(-3, -3));

        await Assert.That(scroller.ViewBounds.Height).IsEqualTo(60f);
        await Assert.That(scroller.Scroller!.Offset.Y).IsGreaterThan(0f);
        await Assert.That(scroller.Scroller.Offset.X).IsEqualTo(0f);
    }

    [Test]
    public async Task A_filter_made_with_a_key_checked_applies_it_at_once()
    {
        var filter = new CheckListFilter(["report"], autoApply: false);

        await Assert.That(filter.Matches("report")).IsTrue();
        await Assert.That(filter.Matches("invoice")).IsFalse();
    }

    [Test]
    public async Task With_auto_apply_a_check_applies_at_once_and_unchecking_the_last_lets_every_key_pass()
    {
        var filter = new CheckListFilter();
        using var driver = Render(filter);

        driver.Click(Box(driver, "Text"));
        var one = (filter.Matches("Text"), filter.Matches("Audio"), filter.IsAnyActive);
        driver.Click(Box(driver, "Text"));

        await Assert.That(one).IsEqualTo((true, false, true));
        await Assert.That(filter.Matches("Audio")).IsTrue();
        await Assert.That(filter.IsAnyActive).IsFalse();
    }

    [Test]
    public async Task Without_auto_apply_checks_are_a_draft_that_commit_applies_and_revert_puts_back()
    {
        var filter = new CheckListFilter(autoApply: false);
        using var driver = Render(filter);

        driver.Click(Box(driver, "Text"));
        var draft = (filter.Matches("Audio"), filter.CommitCommand.CanRun);
        filter.CommitCommand.Run();
        var committed = (
            filter.Matches("Audio"),
            filter.Matches("Text"),
            filter.CommitCommand.CanRun
        );
        driver.Click(Box(driver, "Sheets"));
        filter.RevertCommand.Run();
        driver.Settle();

        await Assert.That(draft).IsEqualTo((true, true));
        await Assert.That(committed).IsEqualTo((false, true, false));
        await Assert.That(Ticked(driver, "Sheets")).IsFalse();
        await Assert.That(Ticked(driver, "Text")).IsTrue();
        await Assert.That(filter.CommitCommand.CanRun).IsFalse();
    }

    [Test]
    public async Task Clear_all_unchecks_every_row_and_with_auto_apply_lets_every_key_pass()
    {
        var filter = new CheckListFilter(["Images", "Sheets"]);
        using var driver = Render(filter);

        filter.ClearAllCommand.Run();
        driver.Settle();

        await Assert.That(Ticked(driver, "Images")).IsFalse();
        await Assert.That(Ticked(driver, "Sheets")).IsFalse();
        await Assert.That(filter.Matches("Audio")).IsTrue();
    }
}

using Tralys.Ui.Components;
using Tralys.Ui.Gallery;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Demo.Tests;

public sealed class GalleryTests
{
    [Test]
    public async Task Down_from_a_categorys_last_story_shows_the_next_categorys_first_story_in_its_opened_category()
    {
        var stories = new GalleryModel(null, false, null).Stories;
        var last = Array.FindLastIndex(stories, s => s.Category == "Buttons");
        var model = new GalleryModel(stories[last].Key, false, null);
        using var driver = Open(model);

        driver.Press(Key.Down);

        var shown = model.Stories[model.Shown];
        await Assert.That(shown.Category).IsEqualTo("Custom");
        await Assert.That(model.Stories[model.Shown - 1].Category).IsEqualTo("Buttons");
        await Assert.That(model.Rows.IndexOf(Listed(model, model.Shown))).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task A_component_is_listed_once_though_its_parts_keys_sort_between_its_own()
    {
        var model = new GalleryModel(null, false, null);
        var twice = new List<string>();
        for (var i = 0; i < model.ChildCount(null); i++)
        {
            var listed = new HashSet<string>();
            foreach (var component in model.ChildAt(null, i).Children)
            {
                if (!listed.Add(component.Label))
                    twice.Add(component.Label);
            }
        }

        await Assert.That(twice).IsEmpty();
    }

    [Test]
    public async Task Slash_goes_to_the_search_without_typing_itself_and_Down_then_steps_through_the_matches_alone()
    {
        var model = new GalleryModel(null, false, null);
        using var driver = Open(model);

        driver.KeyDown(Key.OemQuestion);
        driver.Type("/");
        driver.KeyUp(Key.OemQuestion);
        driver.Type("switch.");
        driver.Press(Key.Down);
        driver.Press(Key.Down);

        var matches = Array.FindAll(
            model.Stories,
            s => s.Key.StartsWith("Switch.", StringComparison.Ordinal)
        );
        await Assert.That(model.Query.ToString()).IsEqualTo("switch.");
        await Assert.That(model.Stories[model.Shown].Key).IsEqualTo(matches[1].Key);
    }

    [Test]
    public async Task Slash_typed_into_a_storys_field_types_there_and_leaves_the_search_alone()
    {
        var model = new GalleryModel("Input.Default", false, null);
        using var driver = Open(model);
        var field = await Assert.That(Field(driver.Root, "Hello")).IsNotNull();
        driver.Click(field);

        driver.KeyDown(Key.OemQuestion);
        driver.Type("/");
        driver.KeyUp(Key.OemQuestion);

        await Assert.That(field.Text.ToString()).Contains("/");
        await Assert.That(driver.View.Focused).IsEqualTo(field);
        await Assert.That(model.Query.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task Picking_Light_from_the_theme_menu_shows_the_light_theme_and_names_it_on_the_menu()
    {
        var model = new GalleryModel(null, false, null);
        using var driver = Open(model);

        driver.Click(driver.GetText("Dark"));
        driver.Click(driver.GetText("Light"));

        await Assert.That(driver.View.Context.Theme).IsEqualTo(Themes.Light);
        await Assert.That(driver.GetText("Light")).IsNotNull();
    }

    [Test]
    public async Task Opening_at_the_last_story_scrolls_the_tree_to_show_its_row()
    {
        var stories = new GalleryModel(null, false, null).Stories;
        var model = new GalleryModel(stories[^1].Key, false, null);
        using var driver = UiDriver.Render(
            ui => GalleryShell.Root(ui, model),
            size: new Size(1280, 480)
        );

        var tree = driver.Get<Tree<GalleryEntry>>();
        var shown = tree.Children[0].ViewBounds;
        var row = await Assert.That(Selected(tree)).IsNotNull();

        await Assert.That(row.ViewBounds.Y).IsGreaterThanOrEqualTo(shown.Y);
        await Assert.That(row.ViewBounds.Bottom).IsLessThanOrEqualTo(shown.Bottom);
    }

    static UiDriver Open(GalleryModel model) =>
        UiDriver.Render(ui => GalleryShell.Root(ui, model), size: new Size(1280, 720));

    static Node? Selected(Node node)
    {
        foreach (var child in node.Children)
        {
            if ((child.States & InputStates.Selected) != 0)
                return child;
            if (Selected(child) is { } found)
                return found;
        }
        return null;
    }

    static Node? Field(Node node, string text)
    {
        foreach (var child in node.Children)
        {
            if (child.Kind == NodeKind.EditableText && child.Text.SequenceEqual(text))
                return child;
            if (Field(child, text) is { } found)
                return found;
        }
        return null;
    }

    static GalleryEntry Listed(GalleryModel model, int story)
    {
        foreach (var row in model.Rows)
        {
            if (row.Story == story)
                return row;
        }
        throw new InvalidOperationException($"The tree lists no row for story {story}.");
    }
}

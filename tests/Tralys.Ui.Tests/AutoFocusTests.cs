using Tralys.Ui.Mvvm;
using Tralys.Ui.Testing;

namespace Tralys.Ui.Tests;

public sealed class AutoFocusTests
{
    [Test]
    public async Task Boxes_and_fields_autofocus_in_one_queue_and_a_field_keeping_its_selection_ends_with_its_caret_at_the_end()
    {
        var prefilled = new ObservableText("prefilled");
        var kept = new ObservableText("prefilled");
        using var both = UiDriver.Render(ui =>
        {
            using (ui.Box(autoFocus: true, focusable: true, id: "box", width: 20, height: 20)) { }
            ui.EditableText(text: prefilled, autoFocus: true, id: "field", width: 200);
        });
        var selections = new List<TextSelection>();
        using var keeping = UiDriver.Render(ui =>
            ui.EditableText(
                text: kept,
                autoFocus: true,
                keepSelectionOnFocus: true,
                onSelectionChange: Callback<TextSelection>.Of(
                    selections,
                    static (log, selection) => log.Add(selection)
                ),
                id: "field",
                width: 200
            )
        );

        await Assert.That(both.Focused).IsSameReferenceAs(both.Get("box"));
        await Assert.That(keeping.Focused).IsSameReferenceAs(keeping.Get("field"));
        await Assert.That(selections[^1]).IsEqualTo(new TextSelection(9, 9));
    }
}

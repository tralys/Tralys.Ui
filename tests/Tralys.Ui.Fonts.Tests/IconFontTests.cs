using System.Text;
using System.Text.Json;
using Tralys.Ui.FreeType;

namespace Tralys.Ui.Fonts.Tests;

public sealed class IconFontTests
{
    [Test]
    public async Task Every_icon_the_table_names_has_a_glyph_in_the_icon_font()
    {
        using var backend = new FreeTypeBackend();
        using var fonts = new FontCollection(backend);
        var lucide = fonts.Add(
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "Lucide.ttf"))
        );
        using var table = JsonDocument.Parse(
            File.ReadAllBytes(
                Path.Combine(AppContext.BaseDirectory, "Icons", "IconCodepoints.json")
            )
        );

        var missing = new List<string>();
        foreach (var icon in table.RootElement.EnumerateObject())
        {
            var codepoint = icon.Value.GetProperty("cp").GetInt32();
            if (codepoint != 0 && lucide.Face.GetGlyphIndex(new Rune(codepoint)) == 0)
                missing.Add(icon.Name);
        }

        await Assert.That(missing).IsEmpty();
    }
}

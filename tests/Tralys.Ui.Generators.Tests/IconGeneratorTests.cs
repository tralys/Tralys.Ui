using Microsoft.CodeAnalysis;
using Tralys.Ui.Generators.Icons;

namespace Tralys.Ui.Generators.Tests;

public sealed class IconGeneratorTests
{
    [Test]
    public async Task AnIconNamedTwice_OrNamedIcons_IsAnIconSetError()
    {
        var roles = Enum.GetNames<IconRole>().Select((role, i) => Entry(role, 57400 + i)).ToArray();

        var twice = Errors([.. roles, Entry("Spark", 58000), Entry("Spark", 58001)]);
        var icons = Errors([.. roles, Entry("Icons", 58000)]);

        await Assert.That(twice).IsEquivalentTo(["TRL4101"]);
        await Assert.That(icons).IsEquivalentTo(["TRL4101"]);
    }

    [Test]
    public async Task AnIconNamedAfterAKeyword_Compiles()
    {
        var roles = Enum.GetNames<IconRole>().Select((role, i) => Entry(role, 57400 + i)).ToArray();

        var errors = Errors([.. roles, Entry("lock", 58000)]);

        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task ACodepointNoCharacterHas_OrAFontKeyWithoutAFamily_IsAnIconSetError()
    {
        var roles = Enum.GetNames<IconRole>().Select((role, i) => Entry(role, 57400 + i)).ToArray();

        var surrogate = Errors([.. roles, Entry("Spark", 0xD800)]);
        var unnamed = Errors(roles, "lucide=");

        await Assert.That(surrogate).IsEquivalentTo(["TRL4101"]);
        await Assert.That(unnamed).IsEquivalentTo(["TRL4101"]);
    }

    static string Entry(string name, int codepoint) =>
        $"\"{name}\": {{\"cp\": {codepoint}, \"font\": \"lucide\"}}";

    static IEnumerable<string> Errors(string[] entries, string fonts = "lucide=lucide") =>
        Harness
            .Diagnostics(
                new IconGenerator(),
                "namespace Sample;",
                [
                    new Harness.Text(
                        "/app/IconCodepoints.json",
                        "{" + string.Join(",\n", entries) + "}"
                    ),
                ],
                new Dictionary<string, string> { ["TralysIconFonts"] = fonts }
            )
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.Id);
}

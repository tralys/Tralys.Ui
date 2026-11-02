using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Tralys.Ui.Generators.Theming;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Generators.Tests;

public sealed class ThemeCompilerTests
{
    const string Source = "namespace Sample; static class Anchor { }";

    [Test]
    public async Task AnAppsTokens_LayOverTralyss_SoWhatTheyLeaveOutKeepsTralyssValue()
    {
        ImmutableArray<AdditionalText> files =
        [
            new Harness.Text(
                "/app/theme/app.resolver.json",
                """
                {
                  "version": "2025.10",
                  "sets": {"app": {"sources": [{"$ref": "palettes/brand.tokens.json"}]}},
                  "resolutionOrder": [{"$ref": "#/sets/app"}]
                }
                """
            ),
            new Harness.Text(
                "/app/theme/palettes/brand.tokens.json",
                """
                {
                  "color": {
                    "$type": "color",
                    "brand": {"76": {"$value": {"colorSpace": "srgb", "components": [1, 0, 0], "hex": "#FF0000"}}}
                  }
                }
                """
            ),
        ];

        var themes = Harness.Load(new ThemeTokenGenerator(), Source, files).GetType("Themes")!;
        var dark = (Theme)themes.GetProperty("Dark")!.GetValue(null)!;
        var light = (Theme)themes.GetProperty("Light")!.GetValue(null)!;

        await Assert.That(dark.Color(ColorToken.BgAccent)).IsEqualTo(Color.FromRgb(255, 0, 0));
        await Assert.That(dark.Color(ColorToken.Bg)).IsEqualTo(Themes.Dark.Color(ColorToken.Bg));
        await Assert
            .That(light.Color(ColorToken.BgDanger))
            .IsEqualTo(Themes.Light.Color(ColorToken.BgDanger));
    }

    [Test]
    public async Task ACurveWithAnXOrYOutsideZeroToOne_IsAnErrorNamingThatToken()
    {
        var files = TokenFiles(static semantic =>
            semantic["curve"] = JsonNode.Parse(
                """
                {
                  "$type": "cubicBezier",
                  "fits": {"$value": [0, 0, 1, 1]},
                  "wide": {"$value": [1.5, 0, 1, 1]},
                  "tall": {"$value": [0, 0, 1, 1.5]}
                }
                """
            )
        );

        var errors = Harness
            .Diagnostics(new ThemeTokenGenerator(), Source, files)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage())
            .ToList();

        await Assert.That(errors.Any(m => m.Contains("'curve.fits'"))).IsFalse();
        await Assert.That(errors.Any(m => m.Contains("'curve.wide'"))).IsTrue();
        await Assert.That(errors.Any(m => m.Contains("'curve.tall'"))).IsTrue();
    }

    [Test]
    public async Task AWeightOver999_IsAnErrorNamingThatToken()
    {
        var files = TokenFiles(static semantic =>
            semantic["heft"] = JsonNode.Parse(
                """
                {
                  "$type": "fontWeight",
                  "fits": {"$value": 999},
                  "over": {"$value": 1000}
                }
                """
            )
        );

        var errors = Harness
            .Diagnostics(new ThemeTokenGenerator(), Source, files)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage())
            .ToList();

        await Assert.That(errors.Any(m => m.Contains("'heft.fits'"))).IsFalse();
        await Assert.That(errors.Any(m => m.Contains("'heft.over'"))).IsTrue();
    }

    [Test]
    public async Task ANumberBeyondADouble_IsAnError()
    {
        var files = TokenFiles(static semantic =>
            semantic["reach"] = JsonNode.Parse(
                """
                {
                  "$type": "dimension",
                  "far": {"$value": {"value": 1e999, "unit": "px"}}
                }
                """
            )
        );

        var errors = Harness
            .Diagnostics(new ThemeTokenGenerator(), Source, files)
            .Where(d => d.Severity == DiagnosticSeverity.Error);

        await Assert.That(errors).IsNotEmpty();
    }

    [Test]
    public async Task AColourComposedFromATranslucentOne_WithoutAnAlphaOfItsOwn_IsOpaque()
    {
        var files = TokenFiles(static semantic =>
            semantic["color"]!["bg"]!["text-selection"]!["$value"] = JsonNode.Parse(
                """
                {
                  "colorSpace": "srgb",
                  "components": {"$ref": "#/color/state/hover/$value/components"},
                  "hex": {"$ref": "#/color/state/hover/$value/hex"}
                }
                """
            )
        );

        var themes = Harness.Load(new ThemeTokenGenerator(), Source, files).GetType("Themes")!;
        var dark = (Theme)themes.GetProperty("Dark")!.GetValue(null)!;
        var hover = dark.Color(ColorToken.StateHover);

        await Assert.That(hover.A).IsLessThan((byte)255);
        await Assert
            .That(dark.Color(ColorToken.BgTextSelection))
            .IsEqualTo(Color.FromArgb(255, hover.R, hover.G, hover.B));
    }

    [Test]
    public async Task ADefaultDensityThatSetsMoreThanDimensions_IsAnErrorNamingThatDensity()
    {
        const string pointer = """
            {
              "color": {"$type": "color", "bg": {"canvas": {"$value": "{color.neutral.6}"}}},
              "data": {"$type": "number", "pointer-only": {"$value": 1}}
            }
            """;
        var files = WithResolver(
                TokenFiles(null),
                static file => new Harness.Text(
                    file.Path,
                    file.GetText()!
                        .ToString()
                        .Replace(
                            "\"pointer\": []",
                            "\"pointer\": [{\"$ref\": \"platforms/client-pointer.tokens.json\"}]"
                        )
                )
            )
            .Add(new Harness.Text("/app/tokens/platforms/client-pointer.tokens.json", pointer));

        var errors = Harness
            .Diagnostics(new ThemeTokenGenerator(), Source, files)
            .Where(d => d.Id == "TRL4001")
            .Select(d => d.GetMessage())
            .ToList();

        await Assert
            .That(errors.Any(m => m.Contains("pointer") && m.Contains("'color.bg.canvas'")))
            .IsTrue();
        await Assert
            .That(errors.Any(m => m.Contains("pointer") && m.Contains("'data.pointer-only'")))
            .IsTrue();
        await Assert.That(errors.Any(m => m.Contains("touch"))).IsFalse();
    }

    [Test]
    public async Task AResolverOutsideTheTokensFolder_NamesThemByRelativePath()
    {
        var files = WithResolver(
            TokenFiles(null),
            static file => new Harness.Text(
                "/app/host/theme/" + Path.GetFileName(file.Path),
                file.GetText()!
                    .ToString()
                    .Replace("\"$ref\": \"", "\"$ref\": \"../../tokens/")
                    .Replace("../../tokens/#", "#")
            )
        );

        var errors = Harness
            .Diagnostics(new ThemeTokenGenerator(), Source, files)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage());

        await Assert.That(errors).IsEmpty();
    }

    static ImmutableArray<AdditionalText> WithResolver(
        ImmutableArray<AdditionalText> files,
        Func<AdditionalText, AdditionalText> rewrite
    )
    {
        var rewritten = ImmutableArray.CreateBuilder<AdditionalText>(files.Length);
        foreach (var file in files)
            rewritten.Add(
                file.Path.EndsWith(".resolver.json", StringComparison.Ordinal)
                    ? rewrite(file)
                    : file
            );
        return rewritten.ToImmutable();
    }

    static ImmutableArray<AdditionalText> TokenFiles(Action<JsonNode>? editSemantic)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Tokens");
        var files = ImmutableArray.CreateBuilder<AdditionalText>();
        foreach (var path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            var text = File.ReadAllText(path);
            if (editSemantic is not null && relative == "semantic.tokens.json")
            {
                var semantic = JsonNode.Parse(text)!;
                editSemantic(semantic);
                text = semantic.ToJsonString();
            }
            files.Add(new Harness.Text("/app/tokens/" + relative, text));
        }
        return files.ToImmutable();
    }
}

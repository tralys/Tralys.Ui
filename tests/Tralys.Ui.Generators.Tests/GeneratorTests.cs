using Microsoft.CodeAnalysis;
using Tralys.Ui.Generators.Stories;

namespace Tralys.Ui.Generators.Tests;

public sealed class GeneratorTests
{
    [Test]
    public async Task A_story_key_registered_twice_is_an_error_at_each_story()
    {
        const string source = """
            using Tralys.Ui;

            namespace Sample;

            [Stories<Ui>("Samples")]
            static class FirstStories
            {
                [FramePath]
                public static void Plain(Ui ui) { }

                [FramePath]
                public static void Wide(Ui ui) { }
            }

            [Stories<Ui>("Samples")]
            static class SecondStories
            {
                [FramePath]
                public static void Plain(Ui ui) => ui.Text("second");
            }
            """;

        Harness.Generate(source, out var reported);

        await Assert
            .That(reported.Select(Harness.Located))
            .IsEquivalentTo([
                ("TRL3013", Harness.LineOf(source, "Plain(Ui ui) { }")),
                ("TRL3013", Harness.LineOf(source, "Plain(Ui ui) =>")),
            ]);
    }

    [Test]
    public async Task A_story_carries_its_doc_summary_as_plain_text_and_its_file_and_line_under_the_story_root()
    {
        const string source = """
            using Tralys.Ui;

            namespace Sample;

            public sealed class Gauge;

            [Stories<Gauge>("Samples")]
            static class GaugeStories
            {
                /// <summary>A <see cref="Gauge"/> drawn in
                /// <c>two</c> lines.</summary>
                [FramePath]
                public static void Plain(Ui ui) { }
            }
            """;

        var registry = Harness
            .Load(
                new StoryRegistryGenerator(),
                source,
                buildProperties: new Dictionary<string, string> { ["TralysStoryRoot"] = "/repo/" },
                path: "/repo/samples/GaugeStories.cs"
            )
            .GetType("StoryRegistry")!;
        var story = ((IReadOnlyList<Story>)registry.GetProperty("All")!.GetValue(null)!).Single();

        await Assert.That(story.Description).IsEqualTo("A Gauge drawn in two lines.");
        await Assert.That(story.SourcePath).IsEqualTo("samples/GaugeStories.cs");
        await Assert.That(story.SourceLine).IsEqualTo(Harness.LineOf(source, "Plain(Ui ui)") + 1);
    }

    [Test]
    public async Task A_generic_component_and_a_generic_part_compile_with_their_constraints()
    {
        const string source = """
            using Tralys.Ui;

            namespace Sample;

            public readonly record struct ListProps<TItem>
                where TItem : class, new()
            {
                public TItem? Selected { get; init; }
            }

            public sealed partial class Picker<TItem> : Component<ListProps<TItem>>
                where TItem : class, new()
            {
                protected override void Render(Ui ui, in ListProps<TItem> p) { }
            }

            public static partial class Lists
            {
                public sealed partial class Row<TItem> : Component<ListProps<TItem>>
                    where TItem : class, new()
                {
                    protected override void Render(Ui ui, in ListProps<TItem> p) { }
                }
            }

            public sealed class Choice;

            public sealed class Form : Component<ListProps<Choice>>
            {
                protected override void Render(Ui ui, in ListProps<Choice> p)
                {
                    ui.Picker(selected: p.Selected);
                    ui.ListsRow(selected: p.Selected);
                }
            }
            """;

        var compiled = Harness.Generate(source, out _);

        await Assert
            .That(compiled.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
            .IsEmpty();
    }

    [Test]
    public async Task A_required_prop_is_a_leading_argument_a_call_cannot_leave_out()
    {
        const string source = """
            using Tralys.Ui;

            namespace Sample;

            public readonly record struct BadgeProps
            {
                public int Count { get; init; }

                public required string Label { get; init; }
            }

            public sealed class Badge : Component<BadgeProps>
            {
                protected override void Render(Ui ui, in BadgeProps p) { }
            }

            public sealed class Board : Component<BadgeProps>
            {
                protected override void Render(Ui ui, in BadgeProps p)
                {
                    ui.Badge("kept", count: 2);
                    ui.Badge(count: 3);
                }
            }
            """;

        var compiled = Harness.Generate(source, out _);

        await Assert
            .That(
                compiled
                    .GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(Harness.Located)
            )
            .IsEquivalentTo([("CS7036", Harness.LineOf(source, "ui.Badge(count: 3)"))]);
    }
}

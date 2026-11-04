using Microsoft.CodeAnalysis;
using Tralys.Ui.Mvvm.Generators;

namespace Tralys.Ui.Generators.Tests;

public sealed class IncrementalTests
{
    const string Alpha = """
        namespace Sample;

        public sealed partial class Alpha : Tralys.Ui.Mvvm.ObservableObject
        {
            [Tralys.Ui.Mvvm.ObservableProperty]
            public partial int Count { get; set; }

            [Tralys.Ui.Mvvm.DelegateCommand]
            void Add() => Count++;
        }
        """;

    const string Beta = """
        namespace Sample;

        public sealed partial class Beta : Tralys.Ui.Mvvm.ObservableObject
        {
            [Tralys.Ui.Mvvm.ObservableProperty]
            public partial string? Name { get; set; }
        }
        """;

    [Test]
    public async Task AnEditElsewhere_LeavesEveryOutputCached_AndAnEditToOneOwner_ReemitsOnlyThatOwner()
    {
        var compilation = Harness.Compile(
            ("Alpha.cs", Alpha),
            ("Beta.cs", Beta),
            ("Other.cs", "namespace Sample; static class Other { }")
        );
        var driver = Harness
            .Tracking(new ObservablePropertyGenerator(), new DelegateCommandGenerator())
            .RunGenerators(compilation);

        var other = compilation.SyntaxTrees.Single(t => t.FilePath == "Other.cs");
        compilation = compilation.ReplaceSyntaxTree(
            other,
            Harness.Edited(other, "namespace Sample; static class Other { static int _x; }")
        );
        driver = driver.RunGenerators(compilation);
        var afterOther = Steps(driver.GetRunResult());

        var beta = compilation.SyntaxTrees.Single(t => t.FilePath == "Beta.cs");
        compilation = compilation.ReplaceSyntaxTree(
            beta,
            Harness.Edited(beta, Beta.Replace("string? Name", "string? Title"))
        );
        driver = driver.RunGenerators(compilation);
        var afterBeta = Steps(driver.GetRunResult())
            .Where(s => s.Generator == nameof(ObservablePropertyGenerator))
            .ToList();

        await Assert.That(afterOther.Where(s => s.Reemitted)).IsEmpty();
        await Assert.That(afterBeta.Count(s => s.Reemitted)).IsEqualTo(1);
        await Assert.That(afterBeta.Count(s => !s.Reemitted)).IsEqualTo(1);
    }

    static IEnumerable<(string Generator, bool Reemitted)> Steps(GeneratorDriverRunResult run) =>
        run.Results.SelectMany(result =>
            result.TrackedOutputSteps.Values.SelectMany(steps =>
                steps.Select(step =>
                    (
                        result.Generator.GetGeneratorType().Name,
                        step.Outputs.Any(output =>
                            output.Reason
                                is not (
                                    IncrementalStepRunReason.Cached
                                    or IncrementalStepRunReason.Unchanged
                                )
                        )
                    )
                )
            )
        );
}

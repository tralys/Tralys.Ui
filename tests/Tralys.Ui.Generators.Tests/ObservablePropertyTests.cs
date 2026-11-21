using Microsoft.CodeAnalysis;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Mvvm.Generators;

namespace Tralys.Ui.Generators.Tests;

public sealed class ObservablePropertyTests
{
    [Test]
    public async Task ASet_RunsTheHook_ThenRaisesTheProperty_ThenItsDependents_AndAnEqualSetDoesNothing()
    {
        var reading = new Reading();
        reading.PropertyChanged += (_, e) => reading.Log.Add(e.PropertyName);

        reading.Score = 30;
        reading.Score = 30;

        await Assert.That(reading.Score).IsEqualTo(30);
        await Assert
            .That(reading.Log.SequenceEqual(["OnScoreChanged 0 30", "Score", "Ratio"]))
            .IsTrue();
    }

    [Test]
    public async Task ASet_RunsTheChangingHookFirst_SoAValueItClampsToTheOneHeldRaisesNothing()
    {
        var reading = new Reading();
        reading.PropertyChanged += (_, e) => reading.Log.Add(e.PropertyName);

        reading.Level = 30;
        reading.Level = 50;

        await Assert.That(reading.Level).IsEqualTo(10);
        await Assert
            .That(reading.Log)
            .IsEquivalentTo(
                (string?[])["OnLevelChanging 0 30", "Level", "OnLevelChanging 10 50"],
                TUnit.Assertions.Enums.CollectionOrdering.Matching
            );
    }

    [Test]
    public async Task WhatTheGeneratorCannotBuild_IsAnErrorAtItsDeclaration()
    {
        const string source = """
            using Tralys.Ui.Mvvm;

            namespace Sample;

            public sealed partial class Dashboard : ObservableObject
            {
                [ObservableProperty]
                public static partial int Level { get; set; }

                [ObservableProperty]
                public partial Spot Aim { get; set; }

                [ObservableProperty]
                public partial (int Row, Spot Spot) Target { get; set; }

                [ObservableProperty]
                public partial (int Row, int Column) Cell { get; set; }

                [ObservableProperty, NotifyPropertyChangedFor("Missing")]
                public partial int Score { get; set; }
            }

            public sealed partial class Plain
            {
                [ObservableProperty]
                public partial int Total { get; set; }
            }

            public struct Spot
            {
                public int X;
            }
            """;

        var errors = Harness
            .Diagnostics(new ObservablePropertyGenerator(), source)
            .Where(d => d.Id.StartsWith("TRL32", StringComparison.Ordinal))
            .Select(d => d.Located());

        await Assert
            .That(errors)
            .IsEquivalentTo([
                ("TRL3203", Harness.LineOf(source, "partial int Level")),
                ("TRL3204", Harness.LineOf(source, "partial Spot Aim")),
                ("TRL3204", Harness.LineOf(source, "Spot Spot) Target")),
                ("TRL3205", Harness.LineOf(source, "partial int Score")),
                ("TRL3202", Harness.LineOf(source, "partial int Total")),
            ]);
    }
}

sealed partial class Reading : ObservableObject
{
    public readonly List<string?> Log = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Ratio))]
    public partial int Score { get; set; }

    public float Ratio => Score / 100f;

    partial void OnScoreChanged(int oldValue, int newValue) =>
        Log.Add($"OnScoreChanged {oldValue} {newValue}");

    [ObservableProperty]
    public partial int Level { get; set; }

    partial void OnLevelChanging(int oldValue, ref int newValue)
    {
        Log.Add($"OnLevelChanging {oldValue} {newValue}");
        newValue = Math.Clamp(newValue, 0, 10);
    }
}

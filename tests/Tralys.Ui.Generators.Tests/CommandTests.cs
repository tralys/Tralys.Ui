using Microsoft.CodeAnalysis;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Mvvm.Generators;

namespace Tralys.Ui.Generators.Tests;

public sealed class CommandTests
{
    [Test]
    public async Task AnAsyncCommand_RefusesToRunAgainWhileInFlight()
    {
        var pending = new TaskCompletionSource();
        var runs = 0;
        var command = new Command(() =>
        {
            runs++;
            return new ValueTask(pending.Task);
        });

        command.Run();
        command.Run();
        pending.SetResult();

        await Assert.That(runs).IsEqualTo(1);
    }

    [Test]
    public async Task EveryCommandShape_Compiles()
    {
        const string source = """
            using System.Threading.Tasks;
            using Tralys.Ui.Mvvm;

            namespace Sample;

            public sealed partial class Inventory
            {
                [DelegateCommand]
                void Sort() { }

                [DelegateCommand]
                void Open(Slot slot) { }

                [DelegateCommand]
                ValueTask Save() => default;

                [DelegateCommand]
                ValueTask Drop(Slot? slot) => default;
            }

            sealed class Slot;
            """;

        var errors = Harness
            .Diagnostics(new DelegateCommandGenerator(), source)
            .Where(d => d.Severity == DiagnosticSeverity.Error);

        await Assert.That(errors).IsEmpty();
    }

    [Test]
    public async Task AStaticCommandMethod_AndASecondOverload_AreErrors()
    {
        const string source = """
            using Tralys.Ui.Mvvm;

            namespace Sample;

            public sealed partial class Inventory
            {
                [DelegateCommand]
                static void Sort() { }

                [DelegateCommand]
                void Open() { }

                [DelegateCommand]
                void Open(int slot) { }
            }
            """;

        var errors = Harness
            .Diagnostics(new DelegateCommandGenerator(), source)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.Located());

        await Assert
            .That(errors)
            .IsEquivalentTo([
                ("TRL3104", Harness.LineOf(source, "static void Sort")),
                ("TRL3105", Harness.LineOf(source, "void Open(int slot)")),
            ]);
    }
}

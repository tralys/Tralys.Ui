using Microsoft.CodeAnalysis;

namespace Tralys.Ui.CodeGen;

static class Descriptors
{
    public static DiagnosticDescriptor Error(string id, string title, string message) =>
        new(id, title, message, "Tralys.Ui", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static DiagnosticDescriptor CompilationEndError(
        string id,
        string title,
        string message
    ) =>
        new(
            id,
            title,
            message,
            "Tralys.Ui",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd
        );
}

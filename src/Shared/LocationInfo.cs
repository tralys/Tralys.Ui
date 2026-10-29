using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Tralys.Ui.CodeGen;

// A Location is not equatable, so carrying one through a pipeline step defeats its cache.
readonly record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo? From(Location? location) =>
        location?.SourceTree is null
            ? null
            : new LocationInfo(
                location.SourceTree.FilePath,
                location.SourceSpan,
                location.GetLineSpan().Span
            );
}

readonly record struct Problem(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    string Subject,
    string Detail = "",
    string Extra = ""
)
{
    public void Report(SourceProductionContext context) =>
        context.ReportDiagnostic(
            Diagnostic.Create(Descriptor, Location?.ToLocation(), Subject, Detail, Extra)
        );
}

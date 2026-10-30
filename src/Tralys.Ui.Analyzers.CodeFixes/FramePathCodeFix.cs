using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;

namespace Tralys.Ui.Analyzers.CodeFixes;

/// <summary>Marks a method or local function that takes a <c>Ui</c> <c>[FramePath]</c> (TRL3005).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FramePathCodeFix)), Shared]
public sealed class FramePathCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [UiFramePathAnalyzer.Id];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context
            .Document.GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);
        var declaration = root
            ?.FindToken(context.Span.Start)
            .Parent?.AncestorsAndSelf()
            .FirstOrDefault(n => n is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax);
        if (declaration is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Mark it [FramePath]",
                ct => Mark(context.Document, declaration, ct),
                nameof(FramePathCodeFix)
            ),
            context.Diagnostics
        );
    }

    static async Task<Document> Mark(
        Document document,
        SyntaxNode declaration,
        CancellationToken cancellationToken
    )
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var newline = root.DescendantTrivia()
            .FirstOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
        if (newline == default)
            newline = SyntaxFactory.LineFeed;
        var leading = declaration.GetLeadingTrivia();
        var indentation =
            leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
                ? SyntaxFactory.TriviaList(leading[leading.Count - 1])
                : SyntaxFactory.TriviaList();

        var attribute = SyntaxFactory
            .AttributeList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(
                        SyntaxFactory
                            .ParseName("Tralys.Ui.FramePath")
                            .WithAdditionalAnnotations(Simplifier.Annotation)
                    )
                )
            )
            .WithTrailingTrivia(newline);

        SyntaxNode marked = declaration switch
        {
            BaseMethodDeclarationSyntax { AttributeLists.Count: 0 } method => method
                .WithLeadingTrivia(indentation)
                .WithAttributeLists(
                    SyntaxFactory.SingletonList(attribute.WithLeadingTrivia(leading))
                ),
            BaseMethodDeclarationSyntax method => method.AddAttributeLists(
                attribute.WithLeadingTrivia(indentation)
            ),
            LocalFunctionStatementSyntax { AttributeLists.Count: 0 } local => local
                .WithLeadingTrivia(indentation)
                .WithAttributeLists(
                    SyntaxFactory.SingletonList(attribute.WithLeadingTrivia(leading))
                ),
            LocalFunctionStatementSyntax local => local.AddAttributeLists(
                attribute.WithLeadingTrivia(indentation)
            ),
            _ => declaration,
        };

        return await Simplifier
            .ReduceAsync(
                document.WithSyntaxRoot(root.ReplaceNode(declaration, marked)),
                Simplifier.Annotation,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }
}

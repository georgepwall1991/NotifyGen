using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NotifyGen.Generator;

/// <summary>
/// Code fix provider that adds the 'partial' modifier to classes marked with [Notify].
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NotifyCodeFixProvider))]
[Shared]
public sealed class NotifyCodeFixProvider : CodeFixProvider
{
    private const string Title = "Make class partial";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(DiagnosticDescriptors.ClassMustBePartial.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
            return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the class declaration at the diagnostic location
        var classDeclaration = root.FindToken(diagnosticSpan.Start)
            .Parent?
            .AncestorsAndSelf()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDeclaration == null)
            return;

        // Register a code action that adds the partial modifier
        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: ct => AddPartialModifierAsync(context.Document, classDeclaration, ct),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Document> AddPartialModifierAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return document;

        // Create the partial modifier with appropriate trivia
        var partialKeyword = SyntaxFactory.Token(SyntaxKind.PartialKeyword);

        // Find the position to insert the partial keyword
        // It should go before the 'class' keyword but after access modifiers
        var modifiers = classDeclaration.Modifiers;
        SyntaxTokenList newModifiers;

        if (modifiers.Count == 0)
        {
            // No modifiers, just add partial with trailing space
            newModifiers = SyntaxFactory.TokenList(
                partialKeyword.WithTrailingTrivia(SyntaxFactory.Space));
        }
        else
        {
            // Insert partial after the last modifier, preserving trivia
            var lastModifier = modifiers.Last();

            // The new partial keyword takes the trailing trivia from the last modifier
            var newPartial = partialKeyword
                .WithTrailingTrivia(lastModifier.TrailingTrivia);

            // Update the last modifier to have just a single space as trailing trivia
            var updatedLastModifier = lastModifier.WithTrailingTrivia(SyntaxFactory.Space);

            // Build new modifiers list
            var modifiersList = modifiers.Take(modifiers.Count - 1).ToList();
            modifiersList.Add(updatedLastModifier);
            modifiersList.Add(newPartial);
            newModifiers = SyntaxFactory.TokenList(modifiersList);
        }

        // Create the new class declaration with the partial modifier
        var newClassDeclaration = classDeclaration.WithModifiers(newModifiers);

        // Replace the old class declaration with the new one
        var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);
        return document.WithSyntaxRoot(newRoot);
    }
}

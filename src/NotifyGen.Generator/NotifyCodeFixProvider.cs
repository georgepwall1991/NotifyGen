using System;
using System.Collections.Generic;
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
using Microsoft.CodeAnalysis.Rename;

namespace NotifyGen.Generator;

/// <summary>
/// Code fixes for common [Notify] mistakes: missing partial, underscore field
/// names, nearby [NotifyAlso] typos, and CommunityToolkit property conversion.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NotifyCodeFixProvider))]
[Shared]
public sealed class NotifyCodeFixProvider : CodeFixProvider
{
    internal const string MakePartialTitle = "Make type partial";
    internal const string PrefixFieldsTitle =
        "Prefix private fields with underscore so they generate properties";
    internal const string ReplaceNotifyAlsoTitlePrefix = "Replace NotifyAlso name with '";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ClassMustBePartial.Id,
            DiagnosticDescriptors.NoEligibleFields.Id,
            DiagnosticDescriptors.UnknownNotifyAlsoProperty.Id,
            DiagnosticDescriptors.ContainingTypeMustBePartial.Id,
            DiagnosticDescriptors.ConvertCommunityToolkitOnNotifyType.Id,
            DiagnosticDescriptors.ConvertCommunityToolkitType.Id
        );

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context
            .Document.GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (root == null)
            return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        if (diagnostic.Id == DiagnosticDescriptors.UnknownNotifyAlsoProperty.Id)
        {
            await RegisterNotifyAlsoFixAsync(context, root, diagnostic).ConfigureAwait(false);
            return;
        }

        if (
            diagnostic.Id == DiagnosticDescriptors.ConvertCommunityToolkitOnNotifyType.Id
            || diagnostic.Id == DiagnosticDescriptors.ConvertCommunityToolkitType.Id
        )
        {
            var convertType = root.FindToken(diagnosticSpan.Start)
                .Parent?.AncestorsAndSelf()
                .OfType<TypeDeclarationSyntax>()
                .FirstOrDefault();
            if (convertType is null)
                return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CommunityToolkitMigrationFixer.Title,
                    createChangedDocument: ct =>
                        CommunityToolkitMigrationFixer.ConvertAsync(
                            context.Document,
                            convertType,
                            ct
                        ),
                    equivalenceKey: CommunityToolkitMigrationFixer.Title
                ),
                diagnostic
            );
            return;
        }

        var typeDeclaration = root.FindToken(diagnosticSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();

        if (typeDeclaration == null)
            return;

        if (
            diagnostic.Id == DiagnosticDescriptors.ClassMustBePartial.Id
            || diagnostic.Id == DiagnosticDescriptors.ContainingTypeMustBePartial.Id
        )
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: MakePartialTitle,
                    createChangedDocument: ct =>
                        AddPartialModifierAsync(context.Document, typeDeclaration, ct),
                    equivalenceKey: MakePartialTitle
                ),
                diagnostic
            );
            return;
        }

        if (diagnostic.Id != DiagnosticDescriptors.NoEligibleFields.Id)
            return;

        var semanticModel = await context
            .Document.GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (
            semanticModel?.GetDeclaredSymbol(typeDeclaration, context.CancellationToken)
                is INamedTypeSymbol typeSymbol
            && NotifyMemberSelection.TypeUsesOptIn(typeSymbol, context.CancellationToken)
        )
        {
            return;
        }

        var renameable = await GetRenameableFieldsAsync(
                context.Document,
                typeDeclaration,
                context.CancellationToken
            )
            .ConfigureAwait(false);
        if (renameable.Count == 0)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: PrefixFieldsTitle,
                createChangedDocument: ct =>
                    PrefixPrivateFieldsAsync(context.Document, typeDeclaration, ct),
                equivalenceKey: PrefixFieldsTitle
            ),
            diagnostic
        );
    }

    private static async Task RegisterNotifyAlsoFixAsync(
        CodeFixContext context,
        SyntaxNode root,
        Diagnostic diagnostic
    )
    {
        var attribute = root.FindToken(diagnostic.Location.SourceSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<AttributeSyntax>()
            .FirstOrDefault();
        if (attribute?.ArgumentList is null || attribute.ArgumentList.Arguments.Count == 0)
            return;

        var argument =
            root.FindToken(diagnostic.Location.SourceSpan.Start)
                .Parent?.AncestorsAndSelf()
                .OfType<AttributeArgumentSyntax>()
                .FirstOrDefault()
            ?? attribute.ArgumentList.Arguments[0];
        var referencedName = GetReferencedName(argument.Expression);
        if (string.IsNullOrEmpty(referencedName))
            return;

        var typeDeclaration = attribute
            .Ancestors()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();
        if (typeDeclaration is null)
            return;

        var semanticModel = await context
            .Document.GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (
            semanticModel?.GetDeclaredSymbol(typeDeclaration, context.CancellationToken)
            is not INamedTypeSymbol typeSymbol
        )
            return;

        var replacement = ClosestIdentifier.Find(
            referencedName!,
            GetKnownPropertyNames(typeSymbol)
        );
        if (replacement is null)
            return;

        var title = ReplaceNotifyAlsoTitlePrefix + replacement + "'";
        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                createChangedDocument: ct =>
                    ReplaceNotifyAlsoNameAsync(
                        context.Document,
                        argument.Expression,
                        replacement,
                        ct
                    ),
                equivalenceKey: title
            ),
            diagnostic
        );
    }

    private static async Task<Document> ReplaceNotifyAlsoNameAsync(
        Document document,
        ExpressionSyntax expression,
        string replacement,
        CancellationToken cancellationToken
    )
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return document;

        var replacementNode = CreateReplacementExpression(expression, replacement);
        if (replacementNode is null)
            return document;

        return document.WithSyntaxRoot(root.ReplaceNode(expression, replacementNode));
    }

    private static ExpressionSyntax? CreateReplacementExpression(
        ExpressionSyntax expression,
        string replacement
    )
    {
        if (
            expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression)
        )
        {
            return SyntaxFactory
                .LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(replacement)
                )
                .WithTriviaFrom(literal);
        }

        if (
            expression is InvocationExpressionSyntax invocation
            && invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" }
            && invocation.ArgumentList.Arguments.Count == 1
        )
        {
            var nameofArgument = invocation.ArgumentList.Arguments[0].Expression;
            var replacedArgument = nameofArgument switch
            {
                IdentifierNameSyntax identifier => invocation.ReplaceNode(
                    identifier,
                    SyntaxFactory.IdentifierName(replacement).WithTriviaFrom(identifier)
                ),
                MemberAccessExpressionSyntax memberAccess => invocation.ReplaceNode(
                    memberAccess.Name,
                    SyntaxFactory.IdentifierName(replacement).WithTriviaFrom(memberAccess.Name)
                ),
                _ => null,
            };
            return replacedArgument;
        }

        return null;
    }

    private static string? GetReferencedName(ExpressionSyntax expression)
    {
        if (
            expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression)
        )
        {
            return literal.Token.ValueText;
        }

        if (
            expression is InvocationExpressionSyntax invocation
            && invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" }
            && invocation.ArgumentList.Arguments.Count == 1
        )
        {
            return invocation.ArgumentList.Arguments[0].Expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
                _ => null,
            };
        }

        return null;
    }

    private static IEnumerable<string> GetKnownPropertyNames(INamedTypeSymbol typeSymbol)
    {
        foreach (var property in typeSymbol.GetMembers().OfType<IPropertySymbol>())
            yield return property.Name;

        foreach (var field in typeSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (FieldEligibilityClassifier.Classify(field) != FieldEligibility.Eligible)
                continue;

            yield return NotifyMemberSelection.GetGeneratedPropertyName(field);
        }
    }

    private static async Task<IReadOnlyList<IFieldSymbol>> GetRenameableFieldsAsync(
        Document document,
        TypeDeclarationSyntax typeDeclaration,
        CancellationToken cancellationToken
    )
    {
        var semanticModel = await document
            .GetSemanticModelAsync(cancellationToken)
            .ConfigureAwait(false);
        if (
            semanticModel?.GetDeclaredSymbol(typeDeclaration, cancellationToken)
            is not INamedTypeSymbol typeSymbol
        )
            return Array.Empty<IFieldSymbol>();

        var existingNames = new HashSet<string>(
            typeSymbol.GetMembers().Select(member => member.Name),
            StringComparer.Ordinal
        );

        var renameable = new List<IFieldSymbol>();
        foreach (var field in typeSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.DeclaredAccessibility != Accessibility.Private)
                continue;
            if (field.IsStatic || field.IsConst || field.IsReadOnly)
                continue;
            if (
                field.Name.StartsWith("_", StringComparison.Ordinal)
                || field.Name.StartsWith("<", StringComparison.Ordinal)
            )
                continue;
            if (field.Name.Length == 0)
                continue;

            var newName = "_" + field.Name;
            if (existingNames.Contains(newName))
                continue;

            renameable.Add(field);
            existingNames.Add(newName);
        }

        return renameable;
    }

    private static async Task<Document> PrefixPrivateFieldsAsync(
        Document document,
        TypeDeclarationSyntax typeDeclaration,
        CancellationToken cancellationToken
    )
    {
        var fields = await GetRenameableFieldsAsync(document, typeDeclaration, cancellationToken)
            .ConfigureAwait(false);
        if (fields.Count == 0)
            return document;

        var typeMetadataName = fields[0].ContainingType.ToDisplayString();
        var originalNames = fields.Select(field => field.Name).ToArray();
        var solution = document.Project.Solution;
        var projectId = document.Project.Id;

        foreach (var originalName in originalNames)
        {
            var project = solution.GetProject(projectId);
            if (project is null)
                break;

            var compilation = await project
                .GetCompilationAsync(cancellationToken)
                .ConfigureAwait(false);
            var field = compilation
                ?.GetTypeByMetadataName(typeMetadataName)
                ?.GetMembers(originalName)
                .OfType<IFieldSymbol>()
                .FirstOrDefault();
            if (field is null)
                continue;

            solution = await Renamer
                .RenameSymbolAsync(
                    solution,
                    field,
                    new SymbolRenameOptions(),
                    "_" + originalName,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        return solution.GetDocument(document.Id) ?? document;
    }

    private static async Task<Document> AddPartialModifierAsync(
        Document document,
        TypeDeclarationSyntax typeDeclaration,
        CancellationToken cancellationToken
    )
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return document;

        var partialKeyword = SyntaxFactory.Token(SyntaxKind.PartialKeyword);
        var modifiers = typeDeclaration.Modifiers;
        SyntaxTokenList newModifiers;

        if (modifiers.Count == 0)
        {
            newModifiers = SyntaxFactory.TokenList(
                partialKeyword.WithTrailingTrivia(SyntaxFactory.Space)
            );
        }
        else
        {
            var lastModifier = modifiers.Last();
            var newPartial = partialKeyword.WithTrailingTrivia(lastModifier.TrailingTrivia);
            var updatedLastModifier = lastModifier.WithTrailingTrivia(SyntaxFactory.Space);
            var modifiersList = modifiers.Take(modifiers.Count - 1).ToList();
            modifiersList.Add(updatedLastModifier);
            modifiersList.Add(newPartial);
            newModifiers = SyntaxFactory.TokenList(modifiersList);
        }

        var newTypeDeclaration = typeDeclaration.WithModifiers(newModifiers);
        var newRoot = root.ReplaceNode(typeDeclaration, newTypeDeclaration);
        return document.WithSyntaxRoot(newRoot);
    }
}

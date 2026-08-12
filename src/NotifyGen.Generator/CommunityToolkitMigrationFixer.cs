using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NotifyGen.Generator;

internal static class CommunityToolkitMigrationFixer
{
    internal const string Title = "Convert CommunityToolkit properties to NotifyGen";

    public static async Task<Document> ConvertAsync(
        Document document,
        TypeDeclarationSyntax typeDeclaration,
        CancellationToken cancellationToken
    )
    {
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (model is null)
            return document;

        if (
            model.GetDeclaredSymbol(typeDeclaration, cancellationToken)
            is not INamedTypeSymbol typeSymbol
        )
            return document;

        var computedSources = CollectGetOnlyChangedForTargets(typeSymbol);
        var hasNotify = typeSymbol
            .GetAttributes()
            .Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == "NotifyGen.NotifyAttribute"
            );
        var solution = document.Project.Solution;
        var originId = document.Id;
        var nodesByDocument = new Dictionary<DocumentId, List<TypeDeclarationSyntax>>();

        foreach (var reference in typeSymbol.DeclaringSyntaxReferences)
        {
            var partDocument = await FindDocumentAsync(
                    solution,
                    document,
                    reference.SyntaxTree,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (partDocument is null)
                continue;

            var root = await partDocument
                .GetSyntaxRootAsync(cancellationToken)
                .ConfigureAwait(false);
            if (root is null)
                continue;

            var node = root.FindNode(reference.Span).FirstAncestorOrSelf<TypeDeclarationSyntax>();
            if (node is null)
                continue;

            if (!nodesByDocument.TryGetValue(partDocument.Id, out var nodes))
            {
                nodes = new List<TypeDeclarationSyntax>();
                nodesByDocument[partDocument.Id] = nodes;
            }

            nodes.Add(node);
        }

        if (nodesByDocument.Count == 0)
            nodesByDocument[document.Id] = new List<TypeDeclarationSyntax> { typeDeclaration };

        foreach (var pair in nodesByDocument)
        {
            var partDocument = solution.GetDocument(pair.Key);
            if (partDocument is null)
                continue;

            var root = await partDocument
                .GetSyntaxRootAsync(cancellationToken)
                .ConfigureAwait(false);
            var partModel = await partDocument
                .GetSemanticModelAsync(cancellationToken)
                .ConfigureAwait(false);
            if (root is null || partModel is null)
                continue;

            var addNotify = !hasNotify;
            var newRoot = root.ReplaceNodes(
                pair.Value,
                (original, _) =>
                {
                    var updated = ConvertOneDeclaration(
                        original,
                        partModel,
                        computedSources,
                        addNotify,
                        cancellationToken
                    );
                    addNotify = false;
                    hasNotify = true;
                    return updated;
                }
            );
            if (newRoot is CompilationUnitSyntax unit)
                newRoot = EnsureUsing(unit, "NotifyGen");

            solution = solution.WithDocumentSyntaxRoot(pair.Key, newRoot);
        }

        return solution.GetDocument(originId) ?? document;
    }

    private static async Task<Document?> FindDocumentAsync(
        Solution solution,
        Document origin,
        SyntaxTree tree,
        CancellationToken cancellationToken
    )
    {
        var document = solution.GetDocument(tree);
        if (document is not null)
            return document;

        foreach (var candidate in origin.Project.Documents)
        {
            var candidateTree = await candidate
                .GetSyntaxTreeAsync(cancellationToken)
                .ConfigureAwait(false);
            if (candidateTree == tree)
                return solution.GetDocument(candidate.Id) ?? candidate;
        }

        return origin.TryGetSyntaxTree(out var originTree) && originTree == tree ? origin : null;
    }

    private static TypeDeclarationSyntax ConvertOneDeclaration(
        TypeDeclarationSyntax typeDeclaration,
        SemanticModel model,
        IReadOnlyDictionary<string, IReadOnlyList<string>> computedSources,
        bool addNotify,
        CancellationToken cancellationToken
    )
    {
        var updatedType = typeDeclaration.ReplaceNodes(
            typeDeclaration.Members,
            (original, _) => RewriteMember(original, model, computedSources, cancellationToken)
        );

        if (!updatedType.Modifiers.Any(SyntaxKind.PartialKeyword))
            updatedType = AddPartialModifier(updatedType);

        if (addNotify && !HasSimpleAttribute(updatedType, "Notify"))
            updatedType = AddSimpleAttribute(updatedType, "Notify");

        return updatedType.ReplaceNodes(
            updatedType.Members.OfType<PropertyDeclarationSyntax>(),
            (original, current) =>
            {
                if (!computedSources.TryGetValue(original.Identifier.ValueText, out var sources))
                    return current;
                if (HasSimpleAttribute(current, "NotifyComputed"))
                    return MergeNotifyComputed(current, sources);
                return AddNotifyComputed(current, sources);
            }
        );
    }

    private static Dictionary<string, IReadOnlyList<string>> CollectGetOnlyChangedForTargets(
        INamedTypeSymbol typeSymbol
    )
    {
        var sourcesByTarget = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var member in typeSymbol.GetMembers())
        {
            foreach (var attribute in member.GetAttributes())
            {
                if (
                    attribute.AttributeClass?.ToDisplayString()
                    != NotifyMemberSelection.NotifyPropertyChangedForAttributeName
                )
                {
                    continue;
                }

                foreach (var name in GetConstructorStringArguments(attribute))
                {
                    var property = typeSymbol
                        .GetMembers(name)
                        .OfType<IPropertySymbol>()
                        .FirstOrDefault();
                    if (property is not { IsIndexer: false, SetMethod: null })
                        continue;

                    var source = member switch
                    {
                        IFieldSymbol field => NotifyMemberSelection.GetGeneratedPropertyName(field),
                        IPropertySymbol sourceProperty => sourceProperty.Name,
                        _ => null,
                    };
                    if (source is null)
                        continue;

                    if (!sourcesByTarget.TryGetValue(name, out var sources))
                    {
                        sources = new List<string>();
                        sourcesByTarget[name] = sources;
                    }

                    if (!sources.Contains(source, StringComparer.Ordinal))
                        sources.Add(source);
                }
            }
        }

        return sourcesByTarget.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.Ordinal
        );
    }

    private static MemberDeclarationSyntax RewriteMember(
        MemberDeclarationSyntax member,
        SemanticModel model,
        IReadOnlyDictionary<string, IReadOnlyList<string>> computedSources,
        CancellationToken cancellationToken
    )
    {
        if (member is not (FieldDeclarationSyntax or PropertyDeclarationSyntax))
            return member;

        if (MemberHasUnsupportedCommunityToolkitCompanion(member, model, cancellationToken))
            return member;

        var remaining = new List<AttributeListSyntax>();
        var alsoNotify = new List<string>();
        var canExecute = new List<AttributeSyntax>();
        var sawOptIn = HasSimpleAttribute(member, "NotifyProperty");
        var touched = false;

        foreach (var list in member.AttributeLists)
        {
            var kept = new List<AttributeSyntax>();
            foreach (var attribute in list.Attributes)
            {
                var typeName = GetAttributeTypeName(attribute, model, cancellationToken);
                if (typeName == NotifyMemberSelection.ObservablePropertyAttributeName)
                {
                    sawOptIn = true;
                    touched = true;
                    continue;
                }

                if (typeName == NotifyMemberSelection.NotifyPropertyChangedForAttributeName)
                {
                    touched = true;
                    foreach (var target in GetStringArguments(attribute))
                    {
                        if (!computedSources.ContainsKey(target))
                            alsoNotify.Add(target);
                    }

                    continue;
                }

                if (
                    typeName
                    == NotifyMemberSelection.CommunityToolkitNotifyCanExecuteChangedForAttributeName
                )
                {
                    touched = true;
                    canExecute.Add(
                        attribute.WithName(
                            SyntaxFactory.ParseName("NotifyGen.NotifyCanExecuteChangedFor")
                        )
                    );
                    continue;
                }

                kept.Add(attribute);
            }

            if (kept.Count > 0)
            {
                remaining.Add(list.WithAttributes(SyntaxFactory.SeparatedList(kept)));
            }
            else
            {
                touched = true;
            }
        }

        if (!touched)
            return member;

        if (sawOptIn && !HasSimpleAttribute(remaining, "NotifyProperty"))
        {
            remaining.Insert(0, SimpleAttributeList("NotifyGen.NotifyProperty"));
        }

        foreach (var name in alsoNotify.Distinct(StringComparer.Ordinal))
        {
            remaining.Add(
                SyntaxFactory.AttributeList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory
                            .Attribute(SyntaxFactory.IdentifierName("NotifyAlso"))
                            .WithArgumentList(
                                SyntaxFactory.AttributeArgumentList(
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.AttributeArgument(
                                            SyntaxFactory.LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                SyntaxFactory.Literal(name)
                                            )
                                        )
                                    )
                                )
                            )
                    )
                )
            );
        }

        foreach (var attribute in canExecute)
            remaining.Add(
                SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
            );

        return member switch
        {
            FieldDeclarationSyntax field => field.WithAttributeLists(SyntaxFactory.List(remaining)),
            PropertyDeclarationSyntax property => property.WithAttributeLists(
                SyntaxFactory.List(remaining)
            ),
            _ => member,
        };
    }

    private static string? GetAttributeTypeName(
        AttributeSyntax attribute,
        SemanticModel model,
        CancellationToken cancellationToken
    )
    {
        var info = model.GetSymbolInfo(attribute, cancellationToken);
        var method =
            info.Symbol as IMethodSymbol
            ?? info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        return method?.ContainingType.ToDisplayString();
    }

    private static IEnumerable<string> GetConstructorStringArguments(AttributeData attribute)
    {
        foreach (var argument in attribute.ConstructorArguments)
        {
            if (argument.Kind == TypedConstantKind.Array)
            {
                foreach (var value in argument.Values)
                {
                    if (value.Value is string name && !string.IsNullOrEmpty(name))
                        yield return name;
                }
            }
            else if (argument.Value is string name && !string.IsNullOrEmpty(name))
            {
                yield return name;
            }
        }
    }

    private static IEnumerable<string> GetStringArguments(AttributeSyntax attribute)
    {
        foreach (
            var argument in attribute.ArgumentList?.Arguments
                ?? Enumerable.Empty<AttributeArgumentSyntax>()
        )
        {
            if (GetFirstStringArgumentFromExpression(argument.Expression) is { } name)
                yield return name;
        }
    }

    private static bool MemberHasUnsupportedCommunityToolkitCompanion(
        MemberDeclarationSyntax member,
        SemanticModel model,
        CancellationToken cancellationToken
    )
    {
        foreach (var list in member.AttributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                var typeName = GetAttributeTypeName(attribute, model, cancellationToken);
                if (
                    typeName == NotifyMemberSelection.NotifyPropertyChangedRecipientsAttributeName
                    || typeName == NotifyMemberSelection.NotifyDataErrorInfoAttributeName
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? GetFirstStringArgument(AttributeSyntax attribute)
    {
        var argument = attribute.ArgumentList?.Arguments.FirstOrDefault();
        if (argument is null)
            return null;

        return argument.Expression switch
        {
            LiteralExpressionSyntax literal
                when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
            InvocationExpressionSyntax invocation
                when invocation.Expression
                    is IdentifierNameSyntax { Identifier.ValueText: "nameof" }
                    && invocation.ArgumentList.Arguments.Count == 1 => invocation
                .ArgumentList
                .Arguments[0]
                .Expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
                _ => null,
            },
            _ => null,
        };
    }

    private static bool HasSimpleAttribute(MemberDeclarationSyntax member, string name) =>
        HasSimpleAttribute(member.AttributeLists, name);

    private static bool HasSimpleAttribute(IEnumerable<AttributeListSyntax> lists, string name)
    {
        return lists
            .SelectMany(list => list.Attributes)
            .Any(attribute => AttributeNameMatches(attribute.Name.ToString(), name));
    }

    private static bool AttributeNameMatches(string text, string name)
    {
        var simple = text.Contains('.') ? text.Split('.').Last() : text;
        if (simple.EndsWith("Attribute", StringComparison.Ordinal))
            simple = simple.Substring(0, simple.Length - "Attribute".Length);
        return simple.Equals(name, StringComparison.Ordinal);
    }

    private static T AddSimpleAttribute<T>(T member, string name)
        where T : MemberDeclarationSyntax
    {
        return (T)member.AddAttributeLists(SimpleAttributeList(name));
    }

    private static PropertyDeclarationSyntax MergeNotifyComputed(
        PropertyDeclarationSyntax property,
        IReadOnlyList<string> sources
    )
    {
        var existing = new List<string>();
        AttributeSyntax? computed = null;
        AttributeListSyntax? computedList = null;
        foreach (var list in property.AttributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                if (!AttributeNameMatches(attribute.Name.ToString(), "NotifyComputed"))
                    continue;
                computed = attribute;
                computedList = list;
                foreach (
                    var argument in attribute.ArgumentList?.Arguments
                        ?? Enumerable.Empty<AttributeArgumentSyntax>()
                )
                {
                    if (GetFirstStringArgumentFromExpression(argument.Expression) is { } name)
                        existing.Add(name);
                }
            }
        }

        var merged = existing.Concat(sources).Distinct(StringComparer.Ordinal).ToArray();
        if (computed is null || computedList is null)
            return AddNotifyComputed(property, merged);

        if (merged.SequenceEqual(existing, StringComparer.Ordinal))
            return property;

        var replacement = CreateNotifyComputedAttribute(merged);
        var newList =
            computedList.Attributes.Count == 1
                ? computedList.WithAttributes(SyntaxFactory.SingletonSeparatedList(replacement))
                : computedList.WithAttributes(
                    SyntaxFactory.SeparatedList(
                        computedList.Attributes.Select(attribute =>
                            attribute == computed ? replacement : attribute
                        )
                    )
                );
        return property.ReplaceNode(computedList, newList);
    }

    private static string? GetFirstStringArgumentFromExpression(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal
                when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
            InvocationExpressionSyntax invocation
                when invocation.Expression
                    is IdentifierNameSyntax { Identifier.ValueText: "nameof" }
                    && invocation.ArgumentList.Arguments.Count == 1 => invocation
                .ArgumentList
                .Arguments[0]
                .Expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
                _ => null,
            },
            _ => null,
        };
    }

    private static AttributeSyntax CreateNotifyComputedAttribute(IReadOnlyList<string> sources)
    {
        var attribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("NotifyComputed"));
        if (sources.Count == 0)
            return attribute;

        var arguments = sources.Select(source =>
            SyntaxFactory.AttributeArgument(
                SyntaxFactory
                    .InvocationExpression(SyntaxFactory.IdentifierName("nameof"))
                    .WithArgumentList(
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName(source))
                            )
                        )
                    )
            )
        );
        return attribute.WithArgumentList(
            SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(arguments))
        );
    }

    private static PropertyDeclarationSyntax AddNotifyComputed(
        PropertyDeclarationSyntax property,
        IReadOnlyList<string> sources
    )
    {
        return property.AddAttributeLists(
            SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(CreateNotifyComputedAttribute(sources))
            )
        );
    }

    private static AttributeListSyntax SimpleAttributeList(string name) =>
        SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.ParseName(name))
            )
        );

    private static TypeDeclarationSyntax AddPartialModifier(TypeDeclarationSyntax typeDeclaration)
    {
        var partialKeyword = SyntaxFactory.Token(SyntaxKind.PartialKeyword);
        if (typeDeclaration.Modifiers.Count == 0)
        {
            return typeDeclaration.WithModifiers(
                SyntaxFactory.TokenList(partialKeyword.WithTrailingTrivia(SyntaxFactory.Space))
            );
        }

        var lastModifier = typeDeclaration.Modifiers.Last();
        var modifiers = typeDeclaration
            .Modifiers.Take(typeDeclaration.Modifiers.Count - 1)
            .ToList();
        modifiers.Add(lastModifier.WithTrailingTrivia(SyntaxFactory.Space));
        modifiers.Add(partialKeyword.WithTrailingTrivia(lastModifier.TrailingTrivia));
        return typeDeclaration.WithModifiers(SyntaxFactory.TokenList(modifiers));
    }

    private static CompilationUnitSyntax EnsureUsing(
        CompilationUnitSyntax root,
        string namespaceName
    )
    {
        if (root.Usings.Any(directive => directive.Name?.ToString() == namespaceName))
            return root;

        var usingDirective = SyntaxFactory
            .UsingDirective(SyntaxFactory.ParseName(namespaceName))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
        return root.WithUsings(root.Usings.Add(usingDirective));
    }
}

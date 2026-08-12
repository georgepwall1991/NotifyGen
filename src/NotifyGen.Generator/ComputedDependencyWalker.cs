using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NotifyGen.Generator;

internal enum ComputedWalkStatus
{
    Inferred,
    Explicit,
    Empty,
    Unsupported,
    OnGeneratedMember,
    WritableTarget,
}

internal readonly struct ComputedWalkResult
{
    public ComputedWalkResult(
        ComputedWalkStatus status,
        ImmutableArray<string> dependencies,
        AttributeData? attribute
    )
    {
        Status = status;
        Dependencies = dependencies;
        Attribute = attribute;
    }

    public ComputedWalkStatus Status { get; }
    public ImmutableArray<string> Dependencies { get; }
    public AttributeData? Attribute { get; }
}

internal static class ComputedDependencyWalker
{
    internal const string AttributeMetadataName = "NotifyGen.NotifyComputedAttribute";

    internal static bool HasAttribute(ISymbol symbol) => GetAttribute(symbol) is not null;

    internal static AttributeData? GetAttribute(ISymbol symbol) =>
        symbol
            .GetAttributes()
            .FirstOrDefault(attribute =>
                attribute.AttributeClass?.ToDisplayString() == AttributeMetadataName
            );

    internal static ComputedWalkResult Analyze(
        IPropertySymbol property,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        IReadOnlyDictionary<string, string> fieldToProperty,
        Func<IPropertySymbol, bool> isGeneratedMember,
        CancellationToken cancellationToken
    )
    {
        _ = semanticModel;
        var attribute = GetAttribute(property);
        if (attribute is null)
        {
            return new ComputedWalkResult(
                ComputedWalkStatus.Empty,
                ImmutableArray<string>.Empty,
                null
            );
        }

        if (isGeneratedMember(property))
        {
            return new ComputedWalkResult(
                ComputedWalkStatus.OnGeneratedMember,
                ImmutableArray<string>.Empty,
                attribute
            );
        }

        if (property.IsStatic || property.IsIndexer || property.SetMethod is not null)
        {
            return new ComputedWalkResult(
                ComputedWalkStatus.WritableTarget,
                ImmutableArray<string>.Empty,
                attribute
            );
        }

        var explicitMode = attribute.ConstructorArguments.Length == 1;
        var explicitDependencies = explicitMode
            ? ReadExplicitDependencies(attribute)
            : ImmutableArray<string>.Empty;
        if (explicitMode)
        {
            if (explicitDependencies.Length == 0)
            {
                return new ComputedWalkResult(
                    ComputedWalkStatus.Empty,
                    ImmutableArray<string>.Empty,
                    attribute
                );
            }

            return new ComputedWalkResult(
                ComputedWalkStatus.Explicit,
                explicitDependencies,
                attribute
            );
        }

        if (
            !TryGetGetterSyntax(property, cancellationToken, out var getterSyntax)
            || !TryCollectDependencies(
                getterSyntax!,
                containingType,
                fieldToProperty,
                out var inferred
            )
        )
        {
            return new ComputedWalkResult(
                ComputedWalkStatus.Unsupported,
                ImmutableArray<string>.Empty,
                attribute
            );
        }

        if (inferred.Length == 0)
        {
            return new ComputedWalkResult(
                ComputedWalkStatus.Empty,
                ImmutableArray<string>.Empty,
                attribute
            );
        }

        return new ComputedWalkResult(ComputedWalkStatus.Inferred, inferred, attribute);
    }

    private static ImmutableArray<string> ReadExplicitDependencies(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length != 1)
            return ImmutableArray<string>.Empty;

        var argument = attribute.ConstructorArguments[0];
        if (
            argument.IsNull
            || argument.Kind != TypedConstantKind.Array
            || argument.Values.IsDefault
        )
            return ImmutableArray<string>.Empty;

        return argument
            .Values.Select(static value => value.IsNull ? null : value.Value as string)
            .Where(static name => !string.IsNullOrEmpty(name))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static bool TryGetGetterSyntax(
        IPropertySymbol property,
        CancellationToken cancellationToken,
        out SyntaxNode? getterSyntax
    )
    {
        getterSyntax = null;
        foreach (var reference in EnumeratePropertySyntax(property, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetGetterTarget(reference, out var target))
            {
                getterSyntax = target;
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<SyntaxNode> EnumeratePropertySyntax(
        IPropertySymbol property,
        CancellationToken cancellationToken
    )
    {
        foreach (var reference in property.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is { } syntax)
                yield return syntax;
        }

        if (property.GetMethod is not null)
        {
            foreach (var reference in property.GetMethod.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax(cancellationToken) is { } syntax)
                    yield return syntax;
            }
        }

        foreach (var typeReference in property.ContainingType.DeclaringSyntaxReferences)
        {
            if (typeReference.GetSyntax(cancellationToken) is not TypeDeclarationSyntax type)
                continue;

            foreach (var declaration in type.Members.OfType<PropertyDeclarationSyntax>())
            {
                if (declaration.Identifier.ValueText == property.Name)
                    yield return declaration;
            }
        }
    }

    private static bool TryGetGetterTarget(SyntaxNode syntax, out SyntaxNode? target)
    {
        target = syntax switch
        {
            PropertyDeclarationSyntax declaration => declaration.ExpressionBody?.Expression
                ?? GetAccessorBody(declaration.AccessorList),
            ArrowExpressionClauseSyntax arrow => arrow.Expression,
            AccessorDeclarationSyntax accessor
                when accessor.IsKind(SyntaxKind.GetAccessorDeclaration) => accessor
                .ExpressionBody
                ?.Expression
                ?? (SyntaxNode?)accessor.Body,
            _ => null,
        };
        return target is not null;
    }

    private static SyntaxNode? GetAccessorBody(AccessorListSyntax? accessorList)
    {
        var getter = accessorList?.Accessors.FirstOrDefault(accessor =>
            accessor.IsKind(SyntaxKind.GetAccessorDeclaration)
        );
        return getter?.ExpressionBody?.Expression ?? (SyntaxNode?)getter?.Body;
    }

    private static bool TryCollectDependencies(
        SyntaxNode getterSyntax,
        INamedTypeSymbol containingType,
        IReadOnlyDictionary<string, string> fieldToProperty,
        out ImmutableArray<string> dependencies
    )
    {
        var knownNames = new HashSet<string>(fieldToProperty.Values, StringComparer.Ordinal);
        foreach (var property in containingType.GetMembers().OfType<IPropertySymbol>())
            knownNames.Add(property.Name);

        var collected = new HashSet<string>(StringComparer.Ordinal);
        if (!WalkSyntax(getterSyntax, fieldToProperty, knownNames, collected))
        {
            dependencies = ImmutableArray<string>.Empty;
            return false;
        }

        dependencies = collected.ToImmutableArray();
        return true;
    }

    private static bool WalkSyntax(
        SyntaxNode? node,
        IReadOnlyDictionary<string, string> fieldToProperty,
        HashSet<string> knownNames,
        HashSet<string> dependencies
    )
    {
        if (node is null)
            return true;

        switch (node)
        {
            case LiteralExpressionSyntax:
            case PredefinedTypeSyntax:
            case OmittedArraySizeExpressionSyntax:
            case DefaultExpressionSyntax:
                return true;

            case IdentifierNameSyntax identifier:
                return TryAddName(
                    identifier.Identifier.ValueText,
                    fieldToProperty,
                    knownNames,
                    dependencies
                );

            case MemberAccessExpressionSyntax memberAccess
                when memberAccess.Expression is ThisExpressionSyntax
                    && memberAccess.Name is IdentifierNameSyntax name:
                return TryAddName(
                    name.Identifier.ValueText,
                    fieldToProperty,
                    knownNames,
                    dependencies
                );

            case MemberAccessExpressionSyntax:
                return false;

            case InterpolatedStringExpressionSyntax interpolated:
                return interpolated.Contents.All(content =>
                    content is InterpolatedStringTextSyntax
                    || content is InterpolationSyntax interpolation
                        && WalkSyntax(
                            interpolation.Expression,
                            fieldToProperty,
                            knownNames,
                            dependencies
                        )
                        && WalkSyntax(
                            interpolation.AlignmentClause?.Value,
                            fieldToProperty,
                            knownNames,
                            dependencies
                        )
                );

            case BinaryExpressionSyntax binary:
                return WalkSyntax(binary.Left, fieldToProperty, knownNames, dependencies)
                    && WalkSyntax(binary.Right, fieldToProperty, knownNames, dependencies);

            case PrefixUnaryExpressionSyntax unary:
                return WalkSyntax(unary.Operand, fieldToProperty, knownNames, dependencies);

            case PostfixUnaryExpressionSyntax unary:
                return WalkSyntax(unary.Operand, fieldToProperty, knownNames, dependencies);

            case ConditionalExpressionSyntax conditional:
                return WalkSyntax(conditional.Condition, fieldToProperty, knownNames, dependencies)
                    && WalkSyntax(conditional.WhenTrue, fieldToProperty, knownNames, dependencies)
                    && WalkSyntax(conditional.WhenFalse, fieldToProperty, knownNames, dependencies);

            case ParenthesizedExpressionSyntax parenthesized:
                return WalkSyntax(
                    parenthesized.Expression,
                    fieldToProperty,
                    knownNames,
                    dependencies
                );

            case CastExpressionSyntax cast:
                return WalkSyntax(cast.Expression, fieldToProperty, knownNames, dependencies);

            case ElementAccessExpressionSyntax elementAccess:
                return WalkSyntax(
                        elementAccess.Expression,
                        fieldToProperty,
                        knownNames,
                        dependencies
                    )
                    && elementAccess.ArgumentList.Arguments.All(argument =>
                        WalkSyntax(argument.Expression, fieldToProperty, knownNames, dependencies)
                    );

            case BlockSyntax block:
                return block.Statements.All(statement =>
                    WalkSyntax(statement, fieldToProperty, knownNames, dependencies)
                );

            case ReturnStatementSyntax returnStatement:
                return WalkSyntax(
                    returnStatement.Expression,
                    fieldToProperty,
                    knownNames,
                    dependencies
                );

            case ExpressionStatementSyntax expressionStatement:
                return WalkSyntax(
                    expressionStatement.Expression,
                    fieldToProperty,
                    knownNames,
                    dependencies
                );

            case InvocationExpressionSyntax:
            case ObjectCreationExpressionSyntax:
            case ImplicitObjectCreationExpressionSyntax:
            case AnonymousObjectCreationExpressionSyntax:
            case QueryExpressionSyntax:
            case AwaitExpressionSyntax:
            case AssignmentExpressionSyntax:
                return false;

            default:
                return false;
        }
    }

    private static bool TryAddName(
        string name,
        IReadOnlyDictionary<string, string> fieldToProperty,
        HashSet<string> knownNames,
        HashSet<string> dependencies
    )
    {
        if (fieldToProperty.TryGetValue(name, out var propertyName))
        {
            dependencies.Add(propertyName);
            return true;
        }

        if (knownNames.Contains(name))
        {
            dependencies.Add(name);
            return true;
        }

        return false;
    }
}

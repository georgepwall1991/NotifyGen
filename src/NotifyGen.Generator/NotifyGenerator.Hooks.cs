using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace NotifyGen.Generator;

public sealed partial class NotifyGenerator
{
    private static IMethodSymbol? FindNonPartialTypedChangedHook(
        INamedTypeSymbol containingType,
        string propertyName,
        ITypeSymbol propertyType
    )
    {
        var hookName = $"On{propertyName}Changed";
        for (var type = containingType; type is not null; type = type.BaseType)
        {
            foreach (var method in type.GetMembers(hookName).OfType<IMethodSymbol>())
            {
                if (
                    method.Parameters.Length == 2
                    && method.Parameters[0].RefKind == RefKind.None
                    && method.Parameters[1].RefKind == RefKind.None
                    && AreHookTypesEquivalent(method.Parameters[0].Type, propertyType)
                    && AreHookTypesEquivalent(method.Parameters[1].Type, propertyType)
                    && !IsPartialMethod(method)
                    && (
                        SymbolEqualityComparer.Default.Equals(method.ContainingType, containingType)
                        || IsInheritedHookAccessible(method, containingType)
                    )
                )
                {
                    return method;
                }
            }
        }

        return null;
    }

    private static string? GetExistingTypedHookParameterTypeName(
        IMethodSymbol? method,
        ITypeSymbol propertyType,
        int parameterIndex
    )
    {
        if (
            method is null
            || SymbolEqualityComparer.IncludeNullability.Equals(
                method.Parameters[parameterIndex].Type,
                propertyType
            )
        )
        {
            return null;
        }

        var parameterType = method.Parameters[parameterIndex].Type;
        if (parameterType.TypeKind == TypeKind.Dynamic)
        {
            return parameterType.NullableAnnotation == NullableAnnotation.Annotated
                ? "global::System.Object?"
                : "global::System.Object";
        }

        return parameterType.ToDisplayString(TypeDisplayFormat);
    }

    private static bool IsInheritedHookAccessible(
        IMethodSymbol method,
        INamedTypeSymbol containingType
    )
    {
        return method.DeclaredAccessibility switch
        {
            Accessibility.Public
                or Accessibility.Protected
                or Accessibility.ProtectedOrInternal => true,
            Accessibility.Internal
                or Accessibility.ProtectedAndInternal => SymbolEqualityComparer.Default.Equals(
                method.ContainingAssembly,
                containingType.ContainingAssembly
            ),
            _ => false,
        };
    }

    private static bool AreHookTypesEquivalent(ITypeSymbol left, ITypeSymbol right)
    {
        if (SymbolEqualityComparer.Default.Equals(left, right))
            return true;

        if (
            SymbolEqualityComparer.Default.Equals(
                left.WithNullableAnnotation(NullableAnnotation.None),
                right.WithNullableAnnotation(NullableAnnotation.None)
            )
        )
        {
            return true;
        }

        // `dynamic` and `object` have the same metadata signature, even though
        // Roslyn preserves a dynamic annotation on one of the symbols.
        if (
            (left.TypeKind == TypeKind.Dynamic || left.SpecialType == SpecialType.System_Object)
            && (right.TypeKind == TypeKind.Dynamic || right.SpecialType == SpecialType.System_Object)
        )
        {
            return true;
        }

        if (left is IArrayTypeSymbol leftArray && right is IArrayTypeSymbol rightArray)
        {
            return leftArray.Rank == rightArray.Rank
                && AreHookTypesEquivalent(leftArray.ElementType, rightArray.ElementType);
        }

        if (left is INamedTypeSymbol leftNamed && right is INamedTypeSymbol rightNamed)
        {
            return SymbolEqualityComparer.Default.Equals(
                    leftNamed.OriginalDefinition,
                    rightNamed.OriginalDefinition
                )
                && leftNamed.TypeArguments.Length == rightNamed.TypeArguments.Length
                && Enumerable
                    .Range(0, leftNamed.TypeArguments.Length)
                    .All(index =>
                        AreHookTypesEquivalent(
                            leftNamed.TypeArguments[index],
                            rightNamed.TypeArguments[index]
                        )
                    );
        }

        return false;
    }

    private static bool IsPartialMethod(IMethodSymbol method) =>
        method.IsPartialDefinition
        || method.PartialDefinitionPart is not null
        || method.PartialImplementationPart is not null
        || method.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is MethodDeclarationSyntax declaration
            && declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
        );

}

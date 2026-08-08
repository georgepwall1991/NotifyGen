using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NotifyGen.Generator;

internal static class PartialPropertyEligibility
{
    public static bool IsSupported(
        IPropertySymbol property,
        System.Threading.CancellationToken cancellationToken
    )
    {
        if (
            property.IsStatic
            || property.IsIndexer
            || property.Parameters.Length != 0
            || property.ReturnsByRef
            || property.ReturnsByRefReadonly
            || RequiresUnsafeType(property.Type)
        )
        {
            return false;
        }

        if (HasImplementationPart(property, cancellationToken))
            return false;

        var foundDefinition = false;
        foreach (var reference in property.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is not PropertyDeclarationSyntax declaration)
                continue;

            if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                continue;

            if (
                declaration.ExplicitInterfaceSpecifier != null
                || declaration.Modifiers.Any(static modifier => !IsSupportedModifier(modifier.Kind()))
            )
            {
                return false;
            }

            if (declaration.AccessorList is not { } accessorList)
                continue;

            var accessors = accessorList.Accessors;
            if (accessors.Count != 2)
                return false;

            // An implementation part already exists; do not emit a duplicate implementation.
            if (
                accessors.Any(accessor =>
                    accessor.Body != null || accessor.ExpressionBody != null
                )
            )
            {
                return false;
            }

            if (
                accessors.Any(accessor =>
                    !accessor.IsKind(SyntaxKind.GetAccessorDeclaration)
                    && !accessor.IsKind(SyntaxKind.SetAccessorDeclaration)
                )
            )
            {
                return false;
            }

            if (
                !accessors.Any(static accessor =>
                    accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
                || !accessors.Any(static accessor =>
                    accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
            )
            {
                return false;
            }

            foundDefinition = true;
        }

        return foundDefinition;
    }

    private static bool HasImplementationPart(
        IPropertySymbol property,
        System.Threading.CancellationToken cancellationToken
    )
    {
        foreach (var reference in property.ContainingType.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is not TypeDeclarationSyntax declaration)
                continue;

            foreach (
                var propertyDeclaration in declaration.Members.OfType<PropertyDeclarationSyntax>()
            )
            {
                if (
                    propertyDeclaration.Identifier.ValueText != property.Name
                    || !propertyDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)
                )
                {
                    continue;
                }

                if (
                    propertyDeclaration.AccessorList is { } accessorList
                    && accessorList.Accessors.Any(accessor =>
                        accessor.Body != null || accessor.ExpressionBody != null
                    )
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool RequiresUnsafeType(ITypeSymbol type) =>
        type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer
        || type is IArrayTypeSymbol arrayType && RequiresUnsafeType(arrayType.ElementType);

    private static bool IsSupportedModifier(SyntaxKind kind) =>
        kind
            is SyntaxKind.PublicKeyword
                or SyntaxKind.PrivateKeyword
                or SyntaxKind.ProtectedKeyword
                or SyntaxKind.InternalKeyword
                or SyntaxKind.PartialKeyword;
}

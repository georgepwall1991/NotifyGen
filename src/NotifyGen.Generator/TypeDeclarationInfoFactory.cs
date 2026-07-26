using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NotifyGen.Generator;

internal static class TypeDeclarationInfoFactory
{
    public static bool TryCreateChain(
        SemanticModel semanticModel,
        ClassDeclarationSyntax targetDeclaration,
        CancellationToken cancellationToken,
        out ImmutableArray<TypeDeclarationInfo> declarations
    )
    {
        var builder = ImmutableArray.CreateBuilder<TypeDeclarationInfo>();
        var syntaxChain = targetDeclaration
            .AncestorsAndSelf()
            .OfType<TypeDeclarationSyntax>()
            .Reverse();

        foreach (var declaration in syntaxChain)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (
                semanticModel.GetDeclaredSymbol(declaration, cancellationToken)
                is not INamedTypeSymbol symbol
            )
            {
                declarations = ImmutableArray<TypeDeclarationInfo>.Empty;
                return false;
            }

            if (declaration.Modifiers.Any(SyntaxKind.FileKeyword))
            {
                declarations = ImmutableArray<TypeDeclarationInfo>.Empty;
                return false;
            }

            var typeParameters = declaration.TypeParameterList is { } typeParameterList
                ? typeParameterList.Parameters.Select(GetTypeParameterSource).ToImmutableArray()
                : ImmutableArray<string>.Empty;
            var requiredModifiers = declaration
                .Modifiers.Where(IsRequiredModifier)
                .Select(static token => token.ValueText)
                .ToImmutableArray();
            var constraintClauses = declaration
                .ConstraintClauses.Select(static clause =>
                    clause.NormalizeWhitespace().ToFullString()
                )
                .ToImmutableArray();

            builder.Add(
                new TypeDeclarationInfo(
                    GetKind(declaration),
                    declaration.Identifier.Text,
                    symbol.MetadataName,
                    GetAccessibility(symbol.DeclaredAccessibility),
                    requiredModifiers,
                    typeParameters,
                    constraintClauses,
                    GetMetadataIdentity(symbol),
                    declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
                )
            );
        }

        declarations = builder.ToImmutable();
        return declarations.Length > 0;
    }

    private static TypeDeclarationKind GetKind(TypeDeclarationSyntax declaration) =>
        declaration switch
        {
            ClassDeclarationSyntax => TypeDeclarationKind.Class,
            StructDeclarationSyntax => TypeDeclarationKind.Struct,
            InterfaceDeclarationSyntax => TypeDeclarationKind.Interface,
            RecordDeclarationSyntax record
                when record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) =>
                TypeDeclarationKind.RecordStruct,
            RecordDeclarationSyntax => TypeDeclarationKind.RecordClass,
            _ => throw new InvalidOperationException(
                $"Unsupported containing type syntax: {declaration.Kind()}"
            ),
        };

    private static string GetTypeParameterSource(TypeParameterSyntax parameter)
    {
        var variance = parameter.VarianceKeyword.IsKind(SyntaxKind.None)
            ? string.Empty
            : parameter.VarianceKeyword.Text + " ";
        return variance + parameter.Identifier.Text;
    }

    private static bool IsRequiredModifier(SyntaxToken token) =>
        token.IsKind(SyntaxKind.StaticKeyword)
        || token.IsKind(SyntaxKind.AbstractKeyword)
        || token.IsKind(SyntaxKind.SealedKeyword)
        || token.IsKind(SyntaxKind.ReadOnlyKeyword)
        || token.IsKind(SyntaxKind.RefKeyword)
        || token.IsKind(SyntaxKind.UnsafeKeyword);

    private static string GetAccessibility(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.Private => "private",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "internal",
        };

    private static string GetMetadataIdentity(INamedTypeSymbol symbol)
    {
        var names = new Stack<string>();
        for (var current = symbol; current != null; current = current.ContainingType)
            names.Push(current.MetadataName);

        var typeIdentity = string.Join("+", names);
        return symbol.ContainingNamespace.IsGlobalNamespace
            ? typeIdentity
            : $"{symbol.ContainingNamespace.ToDisplayString()}.{typeIdentity}";
    }
}

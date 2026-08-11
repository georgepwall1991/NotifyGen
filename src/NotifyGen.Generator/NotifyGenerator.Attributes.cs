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
    /// <summary>
    /// Collects untargeted property-forwardable attributes plus explicit property/get/set targets.
    /// </summary>
    private static void CollectForwardedAttributes(
        IFieldSymbol field,
        Compilation compilation,
        CancellationToken cancellationToken,
        out ImmutableArray<string> propertyAttributes,
        out ImmutableArray<string> getterAttributes,
        out ImmutableArray<string> setterAttributes
    )
    {
        var property = ImmutableArray.CreateBuilder<string>();
        var getters = ImmutableArray.CreateBuilder<string>();
        var setters = ImmutableArray.CreateBuilder<string>();

        foreach (var attribute in field.GetAttributes())
        {
            if (
                attribute.AttributeClass is not { } attributeClass
                || IsNotifyGenAttribute(attributeClass)
                || IsFileLocalType(attributeClass, cancellationToken)
                || attribute.ConstructorArguments.Any(argument =>
                    ContainsFileLocalType(argument, cancellationToken)
                )
                || attribute.NamedArguments.Any(named =>
                    ContainsFileLocalType(named.Value, cancellationToken)
                )
                || !CanApplyToProperty(attributeClass, cancellationToken)
            )
            {
                continue;
            }

            property.Add(FormatAttribute(attribute));
        }

        foreach (var syntaxReference in field.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (syntaxReference.GetSyntax(cancellationToken) is not VariableDeclaratorSyntax)
                continue;

            var tree = syntaxReference.SyntaxTree;
            var semanticModel = compilation.GetSemanticModel(tree);
            var fieldDeclaration = syntaxReference
                .GetSyntax(cancellationToken)
                .Ancestors()
                .OfType<FieldDeclarationSyntax>()
                .FirstOrDefault();
            if (fieldDeclaration is null)
                continue;

            foreach (var attributeList in fieldDeclaration.AttributeLists)
            {
                if (attributeList.Target?.Identifier is not { } targetToken)
                    continue;

                ImmutableArray<string>.Builder? destination = targetToken.Kind() switch
                {
                    SyntaxKind.PropertyKeyword => property,
                    SyntaxKind.GetKeyword => getters,
                    SyntaxKind.SetKeyword => setters,
                    _ => null,
                };
                if (destination is null)
                    continue;

                foreach (var attributeSyntax in attributeList.Attributes)
                {
                    if (
                        !TryFormatAttributeFromSyntax(
                            attributeSyntax,
                            semanticModel,
                            cancellationToken,
                            out var formatted
                        )
                    )
                    {
                        continue;
                    }

                    destination.Add(formatted);
                }
            }
        }

        propertyAttributes = property.ToImmutable();
        getterAttributes = getters.ToImmutable();
        setterAttributes = setters.ToImmutable();
    }

    private static bool TryFormatAttributeFromSyntax(
        AttributeSyntax attributeSyntax,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string formatted
    )
    {
        formatted = string.Empty;
        if (
            !TryGetAttributeTypeSymbol(
                semanticModel.GetSymbolInfo(attributeSyntax, cancellationToken),
                out var attributeClass
            )
            || IsNotifyGenAttribute(attributeClass)
            || IsFileLocalType(attributeClass, cancellationToken)
        )
        {
            return false;
        }

        var constructorArguments = ImmutableArray.CreateBuilder<string>();
        var namedArguments = ImmutableArray.CreateBuilder<string>();
        foreach (
            var argument in attributeSyntax.ArgumentList?.Arguments
                ?? Enumerable.Empty<AttributeArgumentSyntax>()
        )
        {
            if (!TryFormatAttributeArgumentExpression(
                    argument.Expression,
                    semanticModel,
                    cancellationToken,
                    out var value
                )
                || ContainsFileLocalTypeFromExpression(
                    argument.Expression,
                    semanticModel,
                    cancellationToken
                )
            )
            {
                return false;
            }

            if (argument.NameEquals is { } nameEquals)
            {
                namedArguments.Add(
                    $"{EscapeIdentifier(nameEquals.Name.Identifier.ValueText)} = {value}"
                );
            }
            else
            {
                constructorArguments.Add(value);
            }
        }

        var attributeType = attributeClass.ToDisplayString(FullyQualifiedTypeDisplayFormat);
        var allArgs = constructorArguments.Concat(namedArguments);
        formatted = $"[{attributeType}({string.Join(", ", allArgs)})]";
        return true;
    }

    private static bool TryGetAttributeTypeSymbol(
        SymbolInfo symbolInfo,
        out INamedTypeSymbol attributeClass
    )
    {
        ISymbol? attributeSymbol = symbolInfo.Symbol;
        if (attributeSymbol is null && symbolInfo.CandidateSymbols.Length == 1)
            attributeSymbol = symbolInfo.CandidateSymbols[0];

        attributeClass =
            (attributeSymbol as INamedTypeSymbol)
            ?? attributeSymbol?.ContainingType!;
        return attributeClass is not null;
    }

    private static bool ContainsFileLocalTypeFromExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken
    )
    {
        var typeInfo = semanticModel.GetTypeInfo(expression, cancellationToken).Type;
        if (typeInfo != null && IsFileLocalType(typeInfo, cancellationToken))
            return true;

        if (
            expression is TypeOfExpressionSyntax typeOf
            && semanticModel.GetTypeInfo(typeOf.Type, cancellationToken).Type is { } typeofType
        )
        {
            return IsFileLocalType(typeofType, cancellationToken);
        }

        return false;
    }

    private static bool TryFormatAttributeArgumentExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string formatted
    )
    {
        formatted = string.Empty;
        if (expression is TypeOfExpressionSyntax typeOfExpression)
        {
            var type = semanticModel.GetTypeInfo(typeOfExpression.Type, cancellationToken).Type;
            if (type is null)
                return false;
            formatted = $"typeof({FormatType(type)})";
            return true;
        }

        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        if (constant.HasValue)
        {
            formatted = FormatConstantObject(
                constant.Value,
                semanticModel.GetTypeInfo(expression, cancellationToken).Type
            );
            return true;
        }

        if (expression is ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax)
        {
            // Fall back to operation-based constants for simple arrays when available.
            if (
                semanticModel.GetOperation(expression, cancellationToken)
                    is IArrayCreationOperation { Initializer: { } initializer }
            )
            {
                var elements = new List<string>();
                foreach (var element in initializer.ElementValues)
                {
                    if (element.ConstantValue.HasValue)
                    {
                        elements.Add(
                            FormatConstantObject(
                                element.ConstantValue.Value,
                                element.Type
                            )
                        );
                    }
                    else
                    {
                        return false;
                    }
                }

                var elementType =
                    semanticModel.GetTypeInfo(expression, cancellationToken).Type
                        is IArrayTypeSymbol arrayType
                        ? FormatType(arrayType.ElementType)
                        : "object";
                formatted = $"new {elementType}[] {{ {string.Join(", ", elements)} }}";
                return true;
            }
        }

        return false;
    }

    private static string FormatConstantObject(object? value, ITypeSymbol? type)
    {
        if (value is null)
            return "null";

        if (value is string s)
            return SymbolDisplay.FormatLiteral(s, quote: true);

        if (value is char c)
            return SymbolDisplay.FormatLiteral(c, quote: true);

        if (value is bool b)
            return b ? "true" : "false";

        if (value is double d)
            return d.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "D";

        if (value is float f)
            return f.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "F";

        if (value is decimal m)
            return m.ToString(System.Globalization.CultureInfo.InvariantCulture) + "M";

        if (type?.SpecialType == SpecialType.System_Byte)
            return $"(byte){System.Convert.ToByte(value, System.Globalization.CultureInfo.InvariantCulture)}";

        if (type?.SpecialType == SpecialType.System_SByte)
            return $"(sbyte){System.Convert.ToSByte(value, System.Globalization.CultureInfo.InvariantCulture)}";

        if (type?.SpecialType == SpecialType.System_Int16)
            return $"(short){System.Convert.ToInt16(value, System.Globalization.CultureInfo.InvariantCulture)}";

        if (type?.SpecialType == SpecialType.System_UInt16)
            return $"(ushort){System.Convert.ToUInt16(value, System.Globalization.CultureInfo.InvariantCulture)}";

        if (type?.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumType)
        {
            var name = enumType
                .GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(field =>
                    field.HasConstantValue && Equals(field.ConstantValue, value)
                )
                ?.Name;
            if (name != null)
                return $"{FormatType(enumType)}.{EscapeIdentifier(name)}";
        }

        if (value is IFormattable formattable)
            return formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture);

        return value.ToString() ?? "null";
    }

    private static bool ContainsFileLocalType(
        TypedConstant constant,
        CancellationToken cancellationToken
    )
    {
        if (constant.IsNull)
            return false;

        if (constant.Type != null && IsFileLocalType(constant.Type, cancellationToken))
            return true;

        if (constant.Kind == TypedConstantKind.Type && constant.Value is ITypeSymbol type)
            return IsFileLocalType(type, cancellationToken);

        return constant.Kind == TypedConstantKind.Array
            && constant.Values.Any(value => ContainsFileLocalType(value, cancellationToken));
    }

    private static bool IsNotifyGenAttribute(INamedTypeSymbol attributeClass)
    {
        var namespaceName = attributeClass.ContainingNamespace.ToDisplayString();
        return namespaceName == "NotifyGen"
            || namespaceName.StartsWith("NotifyGen.", StringComparison.Ordinal);
    }

    private static bool IsFileLocalType(
        ITypeSymbol type,
        CancellationToken cancellationToken
    )
    {
        return type switch
        {
            IArrayTypeSymbol array
                => IsFileLocalType(array.ElementType, cancellationToken),
            IPointerTypeSymbol pointer
                => IsFileLocalType(pointer.PointedAtType, cancellationToken),
            INamedTypeSymbol named
                => IsFileLocalNamedType(named, cancellationToken)
                    || named.TypeArguments.Any(argument =>
                        IsFileLocalType(argument, cancellationToken)
                    ),
            _ => false,
        };
    }

    private static bool IsFileLocalNamedType(
        INamedTypeSymbol type,
        CancellationToken cancellationToken
    )
    {
        for (var current = type; current != null; current = current.ContainingType)
        {
            foreach (var reference in current.DeclaringSyntaxReferences)
            {
                var syntax = reference.GetSyntax(cancellationToken);
                if (
                    syntax is BaseTypeDeclarationSyntax declaration
                    && declaration.Modifiers.Any(SyntaxKind.FileKeyword)
                    || syntax is DelegateDeclarationSyntax delegateDeclaration
                        && delegateDeclaration.Modifiers.Any(SyntaxKind.FileKeyword)
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsDirectAttributeUsage(
        AttributeData usage,
        INamedTypeSymbol attributeClass,
        CancellationToken cancellationToken
    )
    {
        if (
            usage.ApplicationSyntaxReference?.GetSyntax(cancellationToken)
            is not AttributeSyntax attributeSyntax
        )
        {
            return false;
        }

        var declaringType = attributeSyntax
            .Ancestors()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();
        return declaringType != null
            && attributeClass.DeclaringSyntaxReferences.Any(reference =>
                reference.GetSyntax(cancellationToken).Span == declaringType.Span
            );
    }

    private static bool CanApplyToProperty(
        INamedTypeSymbol attributeClass,
        CancellationToken cancellationToken
    )
    {
        AttributeData? usage = null;
        for (var current = attributeClass; current != null; current = current.BaseType)
        {
            var hasSourceDeclaration = current.DeclaringSyntaxReferences.Length > 0;
            usage = current
                .GetAttributes()
                .Where(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == AttributeUsageAttributeName
                )
                .FirstOrDefault(attribute =>
                    !hasSourceDeclaration
                    || IsDirectAttributeUsage(attribute, current, cancellationToken)
                );
            if (usage != null)
                break;
        }

        if (usage == null || usage.ConstructorArguments.Length == 0)
            return true;

        var targets = usage.ConstructorArguments[0].Value;
        if (targets == null)
            return false;

        var targetValue = Convert.ToInt64(
            targets,
            System.Globalization.CultureInfo.InvariantCulture
        );
        return ((AttributeTargets)targetValue & AttributeTargets.Property) != 0;
    }

    private static string FormatAttribute(AttributeData attribute)
    {
        var attributeType = attribute.AttributeClass!.ToDisplayString(
            FullyQualifiedTypeDisplayFormat
        );
        var arguments = attribute
            .ConstructorArguments
            .Select(FormatTypedConstant)
            .Concat(
                attribute.NamedArguments.Select(named =>
                    $"{EscapeIdentifier(named.Key)} = {FormatTypedConstant(named.Value)}"
                )
            );
        return $"[{attributeType}({string.Join(", ", arguments)})]";
    }

    private static string FormatTypedConstant(TypedConstant constant)
    {
        if (constant.IsNull)
            return "null";

        if (constant.Kind == TypedConstantKind.Array)
        {
            var arrayType = (IArrayTypeSymbol)constant.Type!;
            return $"new {FormatType(arrayType.ElementType)}[] {{ {string.Join(", ", constant.Values.Select(FormatTypedConstant))} }}";
        }

        if (constant.Kind == TypedConstantKind.Type && constant.Value is ITypeSymbol type)
            return $"typeof({FormatType(type)})";

        if (constant.Kind == TypedConstantKind.Enum)
        {
            return $"({FormatType(constant.Type!)}){Convert.ToString(constant.Value, System.Globalization.CultureInfo.InvariantCulture)}";
        }

        var formatted = FormatPrimitive(constant.Value!);
        formatted = constant.Type?.SpecialType switch
        {
            SpecialType.System_Byte => $"(byte){formatted}",
            SpecialType.System_SByte => $"(sbyte){formatted}",
            SpecialType.System_Int16 => $"(short){formatted}",
            SpecialType.System_UInt16 => $"(ushort){formatted}",
            _ => formatted,
        };

        return formatted;
    }

}

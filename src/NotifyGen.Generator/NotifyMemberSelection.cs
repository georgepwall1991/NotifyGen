using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NotifyGen.Generator;

/// <summary>
/// Chooses which fields and incomplete partials a [Notify] type generates.
/// Opt-in starts only when a member is marked [NotifyProperty] or
/// CommunityToolkit [ObservableProperty] — never [NotifyAlso].
/// </summary>
internal static class NotifyMemberSelection
{
    internal const string NotifyPropertyAttributeName = "NotifyGen.NotifyPropertyAttribute";
    internal const string NotifyNameAttributeName = "NotifyGen.NotifyNameAttribute";
    internal const string ObservablePropertyAttributeName =
        "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute";
    internal const string NotifyPropertyChangedForAttributeName =
        "CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedForAttribute";
    internal const string CommunityToolkitNotifyCanExecuteChangedForAttributeName =
        "CommunityToolkit.Mvvm.ComponentModel.NotifyCanExecuteChangedForAttribute";

    public static bool TypeUsesOptIn(
        INamedTypeSymbol type,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var member in type.GetMembers())
        {
            if (!HasOptInMarker(member))
                continue;

            if (member is IFieldSymbol)
                return true;

            if (
                member is IPropertySymbol property
                && PartialPropertyEligibility.IsSupported(property, cancellationToken)
            )
            {
                return true;
            }
        }

        return false;
    }

    public static bool TypeHasCommunityToolkitPropertyAttributes(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (HasCommunityToolkitPropertyAttribute(member))
                return true;
        }

        return false;
    }

    public static bool DeclarationHasCommunityToolkitPropertyAttributes(
        TypeDeclarationSyntax declaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken
    )
    {
        foreach (var member in declaration.Members)
        {
            foreach (var list in member.AttributeLists)
            {
                foreach (var attribute in list.Attributes)
                {
                    var info = semanticModel.GetSymbolInfo(attribute, cancellationToken);
                    var method =
                        info.Symbol as IMethodSymbol
                        ?? info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
                    var name = method?.ContainingType.ToDisplayString();
                    if (
                        name == ObservablePropertyAttributeName
                        || name == NotifyPropertyChangedForAttributeName
                    )
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static bool HasOptInMarker(ISymbol member)
    {
        foreach (var attribute in member.GetAttributes())
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            if (name == NotifyPropertyAttributeName || name == ObservablePropertyAttributeName)
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasCommunityToolkitPropertyAttribute(ISymbol member)
    {
        foreach (var attribute in member.GetAttributes())
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            if (
                name == ObservablePropertyAttributeName
                || name == NotifyPropertyChangedForAttributeName
            )
            {
                return true;
            }
        }

        return false;
    }

    public static bool ShouldGenerateField(IFieldSymbol field, bool typeUsesOptIn)
    {
        if (FieldEligibilityClassifier.Classify(field) == FieldEligibility.Ignored)
            return false;

        if (!typeUsesOptIn)
            return FieldEligibilityClassifier.Classify(field) == FieldEligibility.Eligible;

        return HasOptInMarker(field) && IsOptInFieldShape(field);
    }

    public static bool ShouldGeneratePartial(
        IPropertySymbol property,
        bool typeUsesOptIn,
        CancellationToken cancellationToken
    )
    {
        if (!PartialPropertyEligibility.IsSupported(property, cancellationToken))
            return false;

        return !typeUsesOptIn || HasOptInMarker(property);
    }

    public static bool IsOptInFieldShape(IFieldSymbol field)
    {
        if (field.DeclaredAccessibility != Accessibility.Private)
            return false;
        if (field.IsStatic || field.IsConst || field.IsReadOnly)
            return false;
        if (field.Name.Length == 0 || field.Name.StartsWith("<", StringComparison.Ordinal))
            return false;

        return true;
    }

    public static string GetGeneratedPropertyName(IFieldSymbol field)
    {
        var notifyName = field
            .GetAttributes()
            .FirstOrDefault(attribute =>
                attribute.AttributeClass?.ToDisplayString() == NotifyNameAttributeName
            );
        if (notifyName?.ConstructorArguments.FirstOrDefault().Value is string customName)
            return customName;

        if (field.Name.StartsWith("_", StringComparison.Ordinal) && field.Name.Length >= 2)
            return char.ToUpperInvariant(field.Name[1]) + field.Name.Substring(2);

        return char.ToUpperInvariant(field.Name[0]) + field.Name.Substring(1);
    }
}

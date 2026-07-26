using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace NotifyGen.Generator;

internal enum FieldEligibility
{
    Eligible,
    Ignored,
    NotPrivate,
    InvalidFieldName,
    StaticOrConst,
    Readonly,
}

internal static class FieldEligibilityClassifier
{
    private const string NotifyIgnoreAttributeName = "NotifyGen.NotifyIgnoreAttribute";

    public static FieldEligibility Classify(IFieldSymbol field)
    {
        if (
            field
                .GetAttributes()
                .Any(static attribute =>
                    attribute.AttributeClass?.ToDisplayString() == NotifyIgnoreAttributeName
                )
        )
            return FieldEligibility.Ignored;

        if (field.DeclaredAccessibility != Accessibility.Private)
            return FieldEligibility.NotPrivate;

        if (!field.Name.StartsWith("_", StringComparison.Ordinal) || field.Name.Length < 2)
            return FieldEligibility.InvalidFieldName;

        if (field.IsStatic || field.IsConst)
            return FieldEligibility.StaticOrConst;

        if (field.IsReadOnly)
            return FieldEligibility.Readonly;

        return FieldEligibility.Eligible;
    }
}

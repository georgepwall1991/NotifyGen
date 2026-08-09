using System.Linq;
using Microsoft.CodeAnalysis;

namespace NotifyGen.Generator;

internal enum PropertyChangedInvokerKind
{
    None,
    Generated,
    String,
    EventArgs,
}

internal static class PropertyChangedInvoker
{
    public static PropertyChangedInvokerKind Find(INamedTypeSymbol type)
    {
        var stringMethod = FindMethod(type, isEventArgs: false);
        if (stringMethod is not null)
            return PropertyChangedInvokerKind.String;

        return FindMethod(type, isEventArgs: true) is not null
            ? PropertyChangedInvokerKind.EventArgs
            : PropertyChangedInvokerKind.None;
    }

    private static IMethodSymbol? FindMethod(INamedTypeSymbol type, bool isEventArgs)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var namedMembers = current.GetMembers("OnPropertyChanged");
            if (namedMembers.Length == 0)
                continue;

            var methods = namedMembers.OfType<IMethodSymbol>().ToArray();
            if (methods.Length == 0)
                return null;

            // A derived declaration with the same name hides the base method group
            // for the generated call. Do not select a base helper that C# lookup
            // would make inaccessible or ambiguous.
            foreach (var method in methods)
            {
                if (
                    method.IsStatic
                    || !method.ReturnsVoid
                    || method.TypeParameters.Length != 0
                    || method.Parameters.Length != 1
                    || method.Parameters[0].RefKind != RefKind.None
                    || !IsAccessible(method, type)
                )
                    continue;

                var parameterType = method.Parameters[0].Type;
                if (!isEventArgs && parameterType.SpecialType == SpecialType.System_String)
                    return method;
                if (
                    isEventArgs
                    && parameterType.ToDisplayString()
                        == "System.ComponentModel.PropertyChangedEventArgs"
                )
                    return method;
            }

            return null;
        }

        return null;
    }

    private static bool IsAccessible(IMethodSymbol method, INamedTypeSymbol fromType)
    {
        if (SymbolEqualityComparer.Default.Equals(method.ContainingType, fromType))
            return method.DeclaredAccessibility != Accessibility.NotApplicable;

        return method.DeclaredAccessibility == Accessibility.Public
            || method.DeclaredAccessibility == Accessibility.Protected
            || method.DeclaredAccessibility == Accessibility.ProtectedOrInternal
            || (
                method.DeclaredAccessibility == Accessibility.Internal
                || method.DeclaredAccessibility == Accessibility.ProtectedAndInternal
            )
                && SymbolEqualityComparer.Default.Equals(
                    method.ContainingAssembly,
                    fromType.ContainingAssembly
                );
    }
}

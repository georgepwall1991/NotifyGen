using System.Linq;
using Microsoft.CodeAnalysis;

namespace NotifyGen.Generator;

internal enum PropertyChangingInvokerKind
{
    None,
    Generated,
    String,
    EventArgs,
}

internal static class PropertyChangingInvoker
{
    public static PropertyChangingInvokerKind Find(INamedTypeSymbol type)
    {
        var stringMethod = FindMethod(type, isEventArgs: false);
        if (stringMethod is not null)
            return PropertyChangingInvokerKind.String;

        return FindMethod(type, isEventArgs: true) is not null
            ? PropertyChangingInvokerKind.EventArgs
            : PropertyChangingInvokerKind.None;
    }

    private static IMethodSymbol? FindMethod(INamedTypeSymbol type, bool isEventArgs)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var namedMembers = current.GetMembers("OnPropertyChanging");
            if (namedMembers.Length == 0)
                continue;

            var methods = namedMembers.OfType<IMethodSymbol>().ToArray();
            if (methods.Length == 0)
                return null;

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
                        == "System.ComponentModel.PropertyChangingEventArgs"
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

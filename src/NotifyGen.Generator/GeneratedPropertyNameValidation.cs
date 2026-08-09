using Microsoft.CodeAnalysis.CSharp;

namespace NotifyGen.Generator;

internal static class GeneratedPropertyNameValidation
{
    public static bool IsValid(string name) =>
        SyntaxFacts.IsValidIdentifier(name)
        && SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None
        && SyntaxFacts.GetContextualKeywordKind(name) == SyntaxKind.None;
}

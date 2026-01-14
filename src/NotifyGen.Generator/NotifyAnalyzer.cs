using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NotifyGen.Generator;

/// <summary>
/// Analyzer that detects common mistakes when using the [Notify] attribute.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NotifyAnalyzer : DiagnosticAnalyzer
{
    private const string NotifyAttributeName = "NotifyGen.NotifyAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ClassMustBePartial,
            DiagnosticDescriptors.NoEligibleFields);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        // Get the class symbol
        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, context.CancellationToken)
            is not INamedTypeSymbol classSymbol)
            return;

        // Check if class has [Notify] attribute
        var hasNotifyAttribute = classSymbol.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == NotifyAttributeName);

        if (!hasNotifyAttribute)
            return;

        // Check if class is partial
        var isPartial = classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword);

        if (!isPartial)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.ClassMustBePartial,
                classDeclaration.Identifier.GetLocation(),
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Check if there are any eligible fields (for partial classes)
        var hasEligibleFields = classSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Any(f => f.DeclaredAccessibility == Accessibility.Private
                && f.Name.StartsWith("_")
                && f.Name.Length >= 2
                && !f.GetAttributes().Any(a =>
                    a.AttributeClass?.ToDisplayString() == "NotifyGen.NotifyIgnoreAttribute"));

        if (!hasEligibleFields)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.NoEligibleFields,
                classDeclaration.Identifier.GetLocation(),
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }
}

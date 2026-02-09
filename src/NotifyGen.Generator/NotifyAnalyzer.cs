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
    private const string NotifyAlsoAttributeName = "NotifyGen.NotifyAlsoAttribute";
    private const string NotifyIgnoreAttributeName = "NotifyGen.NotifyIgnoreAttribute";
    private const string NotifyNameAttributeName = "NotifyGen.NotifyNameAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ClassMustBePartial,
            DiagnosticDescriptors.NoEligibleFields,
            DiagnosticDescriptors.UnknownNotifyAlsoProperty,
            DiagnosticDescriptors.StaticOrConstField,
            DiagnosticDescriptors.ReadonlyField);

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

        // Analyze fields for eligibility and report specific issues
        AnalyzeFieldEligibility(context, classSymbol, classDeclaration);

        // Check NotifyAlso references (NOTIFY003)
        AnalyzeNotifyAlsoReferences(context, classSymbol);
    }

    private static void AnalyzeFieldEligibility(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol classSymbol,
        ClassDeclarationSyntax classDeclaration)
    {
        var allFields = classSymbol.GetMembers().OfType<IFieldSymbol>().ToImmutableArray();
        var hasEligibleFields = false;

        foreach (var field in allFields)
        {
            // Skip fields with [NotifyIgnore]
            if (field.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == NotifyIgnoreAttributeName))
                continue;

            // Check if field is eligible
            var isPrivate = field.DeclaredAccessibility == Accessibility.Private;
            var hasUnderscore = field.Name.StartsWith("_") && field.Name.Length >= 2;
            var isInstance = !field.IsStatic && !field.IsConst;
            var isMutable = !field.IsReadOnly;

            // Report specific issues for fields with underscore prefix
            if (hasUnderscore && isPrivate)
            {
                // Report static/const fields
                if (!isInstance)
                {
                    var fieldSyntax = field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken);
                    var location = fieldSyntax?.GetLocation() ?? classDeclaration.Identifier.GetLocation();

                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.StaticOrConstField,
                        location,
                        field.Name);
                    context.ReportDiagnostic(diagnostic);
                    continue;
                }

                // Report readonly fields
                if (!isMutable)
                {
                    var fieldSyntax = field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken);
                    var location = fieldSyntax?.GetLocation() ?? classDeclaration.Identifier.GetLocation();

                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.ReadonlyField,
                        location,
                        field.Name);
                    context.ReportDiagnostic(diagnostic);
                    continue;
                }

                // This field is eligible
                hasEligibleFields = true;
            }
        }

        // If no eligible fields, report NOTIFY002
        if (!hasEligibleFields)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.NoEligibleFields,
                classDeclaration.Identifier.GetLocation(),
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeNotifyAlsoReferences(SyntaxNodeAnalysisContext context, INamedTypeSymbol classSymbol)
    {
        // Collect all property names that exist or will be generated
        var existingProperties = classSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Select(p => p.Name)
            .ToImmutableHashSet();

        // Collect property names that will be generated from fields (respecting [NotifyName])
        var generatedProperties = classSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.DeclaredAccessibility == Accessibility.Private
                && f.Name.StartsWith("_")
                && f.Name.Length >= 2
                && !f.GetAttributes().Any(a =>
                    a.AttributeClass?.ToDisplayString() == NotifyIgnoreAttributeName))
            .Select(f =>
            {
                var notifyNameAttr = f.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == NotifyNameAttributeName);
                return notifyNameAttr?.ConstructorArguments.FirstOrDefault().Value as string
                    ?? char.ToUpperInvariant(f.Name[1]) + f.Name.Substring(2);
            })
            .ToImmutableHashSet();

        var allKnownProperties = existingProperties.Union(generatedProperties);

        // Check each field with [NotifyAlso]
        foreach (var field in classSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            var notifyAlsoAttributes = field.GetAttributes()
                .Where(a => a.AttributeClass?.ToDisplayString() == NotifyAlsoAttributeName);

            foreach (var attr in notifyAlsoAttributes)
            {
                var propertyName = attr.ConstructorArguments.FirstOrDefault().Value as string;
                if (string.IsNullOrEmpty(propertyName))
                    continue;

                if (!allKnownProperties.Contains(propertyName!))
                {
                    // Find the attribute syntax location for better error placement
                    var location = GetAttributeLocation(field, attr, context.CancellationToken);

                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.UnknownNotifyAlsoProperty,
                        location,
                        field.Name,
                        propertyName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static Location GetAttributeLocation(IFieldSymbol field, AttributeData attribute, System.Threading.CancellationToken ct)
    {
        // Try to get the syntax location of the attribute
        if (attribute.ApplicationSyntaxReference?.GetSyntax(ct) is { } syntax)
        {
            return syntax.GetLocation();
        }

        // Fall back to the field's location
        if (field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(ct) is { } fieldSyntax)
        {
            return fieldSyntax.GetLocation();
        }

        return Location.None;
    }
}

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
    private const string NotifyNameAttributeName = "NotifyGen.NotifyNameAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ClassMustBePartial,
            DiagnosticDescriptors.NoEligibleFields,
            DiagnosticDescriptors.UnknownNotifyAlsoProperty,
            DiagnosticDescriptors.StaticOrConstField,
            DiagnosticDescriptors.ReadonlyField,
            DiagnosticDescriptors.ContainingTypeMustBePartial
        );

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
        if (
            context.SemanticModel.GetDeclaredSymbol(classDeclaration, context.CancellationToken)
            is not INamedTypeSymbol classSymbol
        )
            return;

        // Check if class has [Notify] attribute
        var hasNotifyAttribute = classSymbol
            .GetAttributes()
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
                classSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
            return;
        }

        var nonPartialContainingDeclaration = classDeclaration
            .Ancestors()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(static declaration =>
                !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            );
        if (nonPartialContainingDeclaration != null)
        {
            var containingTypeSymbol = context.SemanticModel.GetDeclaredSymbol(
                nonPartialContainingDeclaration,
                context.CancellationToken
            );
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.ContainingTypeMustBePartial,
                    nonPartialContainingDeclaration.Identifier.GetLocation(),
                    containingTypeSymbol?.Name
                        ?? nonPartialContainingDeclaration.Identifier.ValueText,
                    classSymbol.Name
                )
            );
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
        ClassDeclarationSyntax classDeclaration
    )
    {
        var hasEligibleFields = false;

        foreach (var field in classSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            var eligibility = FieldEligibilityClassifier.Classify(field);
            switch (eligibility)
            {
                case FieldEligibility.Eligible:
                    hasEligibleFields = true;
                    break;
                case FieldEligibility.StaticOrConst:
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.StaticOrConstField,
                            GetFieldLocation(field, classDeclaration, context.CancellationToken),
                            field.Name
                        )
                    );
                    break;
                case FieldEligibility.Readonly:
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.ReadonlyField,
                            GetFieldLocation(field, classDeclaration, context.CancellationToken),
                            field.Name
                        )
                    );
                    break;
            }
        }

        if (!hasEligibleFields)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.NoEligibleFields,
                    classDeclaration.Identifier.GetLocation(),
                    classSymbol.Name
                )
            );
        }
    }

    private static Location GetFieldLocation(
        IFieldSymbol field,
        ClassDeclarationSyntax classDeclaration,
        System.Threading.CancellationToken cancellationToken
    )
    {
        return field
                .DeclaringSyntaxReferences.FirstOrDefault()
                ?.GetSyntax(cancellationToken)
                .GetLocation()
            ?? classDeclaration.Identifier.GetLocation();
    }

    private static void AnalyzeNotifyAlsoReferences(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol classSymbol
    )
    {
        // Collect all property names that exist or will be generated
        var existingProperties = classSymbol
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Select(p => p.Name)
            .ToImmutableHashSet();

        // Collect property names that will be generated from fields (respecting [NotifyName])
        var generatedProperties = classSymbol
            .GetMembers()
            .OfType<IFieldSymbol>()
            .Where(static field =>
                FieldEligibilityClassifier.Classify(field) == FieldEligibility.Eligible
            )
            .Select(f =>
            {
                var notifyNameAttr = f.GetAttributes()
                    .FirstOrDefault(a =>
                        a.AttributeClass?.ToDisplayString() == NotifyNameAttributeName
                    );
                return notifyNameAttr?.ConstructorArguments.FirstOrDefault().Value as string
                    ?? char.ToUpperInvariant(f.Name[1]) + f.Name.Substring(2);
            })
            .ToImmutableHashSet();

        var allKnownProperties = existingProperties.Union(generatedProperties);

        // Check each field with [NotifyAlso]
        foreach (var field in classSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            var notifyAlsoAttributes = field
                .GetAttributes()
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
                        propertyName
                    );
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static Location GetAttributeLocation(
        IFieldSymbol field,
        AttributeData attribute,
        System.Threading.CancellationToken ct
    )
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

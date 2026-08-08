using System.Collections.Generic;
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
            DiagnosticDescriptors.ContainingTypeMustBePartial,
            DiagnosticDescriptors.FileLocalTypeNotSupported,
            DiagnosticDescriptors.GeneratedPropertyNameCollision
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

        if (
            context.SemanticModel.GetDeclaredSymbol(classDeclaration, context.CancellationToken)
            is not INamedTypeSymbol classSymbol
        )
            return;

        var notifyAttribute = classSymbol
            .GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == NotifyAttributeName);

        if (notifyAttribute == null)
            return;

        if (
            notifyAttribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken)
                is { } attributeSyntax
            && (
                attributeSyntax.SyntaxTree != classDeclaration.SyntaxTree
                || !attributeSyntax
                    .AncestorsAndSelf()
                    .OfType<ClassDeclarationSyntax>()
                    .Any(declaration => declaration.Span == classDeclaration.Span)
            )
        )
        {
            return;
        }

        if (classDeclaration.Modifiers.Any(SyntaxKind.FileKeyword))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.FileLocalTypeNotSupported,
                    classDeclaration.Identifier.GetLocation(),
                    classSymbol.Name
                )
            );
            return;
        }

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

        var hasInvalidContainingType = false;
        foreach (
            var containingDeclaration in classDeclaration
                .Ancestors()
                .OfType<TypeDeclarationSyntax>()
        )
        {
            var containingTypeSymbol = context.SemanticModel.GetDeclaredSymbol(
                containingDeclaration,
                context.CancellationToken
            );
            if (containingDeclaration.Modifiers.Any(SyntaxKind.FileKeyword))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.FileLocalTypeNotSupported,
                        containingDeclaration.Identifier.GetLocation(),
                        containingTypeSymbol?.Name ?? containingDeclaration.Identifier.ValueText
                    )
                );
                hasInvalidContainingType = true;
                continue;
            }

            if (containingDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                continue;

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.ContainingTypeMustBePartial,
                    containingDeclaration.Identifier.GetLocation(),
                    containingTypeSymbol?.Name ?? containingDeclaration.Identifier.ValueText,
                    classSymbol.Name
                )
            );
            hasInvalidContainingType = true;
        }

        if (hasInvalidContainingType)
            return;

        AnalyzeFieldEligibility(context, classSymbol, classDeclaration);
        AnalyzeGeneratedPropertyNameCollisions(context, classSymbol, classDeclaration);
        AnalyzeNotifyAlsoReferences(context, classSymbol);
    }

    private static void AnalyzeFieldEligibility(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol classSymbol,
        ClassDeclarationSyntax classDeclaration
    )
    {
        var hasEligibleMembers = false;

        foreach (var field in classSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            var eligibility = FieldEligibilityClassifier.Classify(field);
            switch (eligibility)
            {
                case FieldEligibility.Eligible:
                    hasEligibleMembers = true;
                    break;
                case FieldEligibility.StaticOrConst:
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.StaticOrConstField,
                            GetSymbolLocation(field, classDeclaration, context.CancellationToken),
                            field.Name
                        )
                    );
                    break;
                case FieldEligibility.Readonly:
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.ReadonlyField,
                            GetSymbolLocation(field, classDeclaration, context.CancellationToken),
                            field.Name
                        )
                    );
                    break;
            }
        }

        if (
            classSymbol
                .GetMembers()
                .OfType<IPropertySymbol>()
                .Any(property => IsIncompletePartialProperty(property, context.CancellationToken))
        )
        {
            hasEligibleMembers = true;
        }

        if (!hasEligibleMembers)
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

    private static void AnalyzeGeneratedPropertyNameCollisions(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol classSymbol,
        ClassDeclarationSyntax classDeclaration
    )
    {
        var generatedNames = new Dictionary<string, ISymbol>();
        foreach (var member in classSymbol.GetMembers())
        {
            var propertyName = member switch
            {
                IFieldSymbol field when
                    FieldEligibilityClassifier.Classify(field) == FieldEligibility.Eligible
                    => GetPropertyName(field),
                IPropertySymbol property when
                    IsIncompletePartialProperty(property, context.CancellationToken)
                    => property.Name,
                _ => null,
            };

            if (propertyName == null)
                continue;

            if (generatedNames.ContainsKey(propertyName))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.GeneratedPropertyNameCollision,
                        GetSymbolLocation(member, classDeclaration, context.CancellationToken),
                        propertyName
                    )
                );
            }
            else
            {
                generatedNames.Add(propertyName, member);
            }
        }

        foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (
                IsIncompletePartialProperty(property, context.CancellationToken)
                || !generatedNames.ContainsKey(property.Name)
            )
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.GeneratedPropertyNameCollision,
                    GetSymbolLocation(property, classDeclaration, context.CancellationToken),
                    property.Name
                )
            );
        }
    }

    private static void AnalyzeNotifyAlsoReferences(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol classSymbol
    )
    {
        var existingProperties = classSymbol
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Select(p => p.Name)
            .ToImmutableHashSet();

        var generatedMembers = new Dictionary<string, ISymbol>();
        foreach (var field in classSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (FieldEligibilityClassifier.Classify(field) != FieldEligibility.Eligible)
                continue;

            generatedMembers[GetPropertyName(field)] = field;
        }

        foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (IsIncompletePartialProperty(property, context.CancellationToken))
                generatedMembers[property.Name] = property;
        }

        var allKnownProperties = existingProperties.Union(generatedMembers.Keys);

        foreach (var generatedMember in generatedMembers)
        {
            foreach (var attribute in GetNotifyAlsoAttributes(generatedMember.Value))
            {
                var propertyName = attribute.ConstructorArguments.FirstOrDefault().Value as string;
                if (string.IsNullOrEmpty(propertyName))
                    continue;

                if (!allKnownProperties.Contains(propertyName!))
                {
                    var location = GetAttributeLocation(
                        generatedMember.Value,
                        attribute,
                        context.CancellationToken
                    );
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.UnknownNotifyAlsoProperty,
                            location,
                            generatedMember.Value.Name,
                            propertyName
                        )
                    );
                }
            }
        }
    }

    private static IEnumerable<AttributeData> GetNotifyAlsoAttributes(ISymbol member) =>
        member
            .GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == NotifyAlsoAttributeName);

    private static string GetPropertyName(IFieldSymbol field)
    {
        var notifyNameAttr = field
            .GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == NotifyNameAttributeName);

        if (notifyNameAttr?.ConstructorArguments.FirstOrDefault().Value is string customName)
            return customName;

        return char.ToUpperInvariant(field.Name[1]) + field.Name.Substring(2);
    }

    private static bool IsIncompletePartialProperty(
        IPropertySymbol property,
        System.Threading.CancellationToken ct
    ) => PartialPropertyEligibility.IsSupported(property, ct);

    private static Location GetSymbolLocation(
        ISymbol symbol,
        ClassDeclarationSyntax classDeclaration,
        System.Threading.CancellationToken cancellationToken
    )
    {
        return symbol
                .DeclaringSyntaxReferences.FirstOrDefault()
                ?.GetSyntax(cancellationToken)
                .GetLocation()
            ?? classDeclaration.Identifier.GetLocation();
    }

    private static Location GetAttributeLocation(
        ISymbol member,
        AttributeData attribute,
        System.Threading.CancellationToken ct
    )
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax(ct) is { } syntax)
            return syntax.GetLocation();

        if (member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(ct) is { } memberSyntax)
            return memberSyntax.GetLocation();

        return Location.None;
    }
}

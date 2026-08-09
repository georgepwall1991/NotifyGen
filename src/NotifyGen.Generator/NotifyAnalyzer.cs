using System;
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
            DiagnosticDescriptors.NotifyAlsoDependencyCycle,
            DiagnosticDescriptors.GeneratedPropertyNameCollision,
            DiagnosticDescriptors.NotifyAlsoSubPropertyRequiresInpc,
            DiagnosticDescriptors.NotifyAlsoTargetRequiresGeneratedSource,
            DiagnosticDescriptors.NotifyAlsoTargetSubPropertyUnsupported,
            DiagnosticDescriptors.ExistingInpcRequiresInvoker,
            DiagnosticDescriptors.NotifyAlsoTargetCollectionUnsupported,
            DiagnosticDescriptors.NotifyAlsoCollectionRequiresReference,
            DiagnosticDescriptors.InvalidGeneratedPropertyName,
            DiagnosticDescriptors.ExistingInpcChangingRequiresInvoker
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
        AnalyzeExistingInpcHost(context, classSymbol, classDeclaration);
        AnalyzeExistingInpcChangingHost(context, classSymbol, classDeclaration, notifyAttribute);
    }

    private static void AnalyzeExistingInpcHost(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol classSymbol,
        ClassDeclarationSyntax classDeclaration
    )
    {
        var inpcType = context.SemanticModel.Compilation.GetTypeByMetadataName(
            "System.ComponentModel.INotifyPropertyChanged"
        );
        if (
            inpcType is null
            || !classSymbol.AllInterfaces.Contains(inpcType, SymbolEqualityComparer.Default)
            || PropertyChangedInvoker.Find(classSymbol) != PropertyChangedInvokerKind.None
        )
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.ExistingInpcRequiresInvoker,
                classDeclaration.Identifier.GetLocation(),
                classSymbol.Name
            )
        );
    }

    private static void AnalyzeExistingInpcChangingHost(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol classSymbol,
        ClassDeclarationSyntax classDeclaration,
        AttributeData notifyAttribute
    )
    {
        var implementChanging = notifyAttribute.NamedArguments.Any(named =>
            named.Key == "ImplementChanging" && named.Value.Value is true
        );
        if (!implementChanging)
            return;

        var inpcChangingType = context.SemanticModel.Compilation.GetTypeByMetadataName(
            "System.ComponentModel.INotifyPropertyChanging"
        );
        if (
            inpcChangingType is null
            || !classSymbol.AllInterfaces.Contains(
                inpcChangingType,
                SymbolEqualityComparer.Default
            )
            || PropertyChangingInvoker.Find(classSymbol)
                != PropertyChangingInvokerKind.None
        )
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.ExistingInpcChangingRequiresInvoker,
                classDeclaration.Identifier.GetLocation(),
                classSymbol.Name
            )
        );
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
                    var propertyName = GetPropertyName(field);
                    if (!GeneratedPropertyNameValidation.IsValid(propertyName))
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                DiagnosticDescriptors.InvalidGeneratedPropertyName,
                                GetSymbolLocation(field, classDeclaration, context.CancellationToken),
                                field.Name,
                                propertyName
                            )
                        );
                    }
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

            if (
                propertyName == null
                || !GeneratedPropertyNameValidation.IsValid(propertyName)
            )
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

            var propertyName = GetPropertyName(field);
            if (GeneratedPropertyNameValidation.IsValid(propertyName))
                generatedMembers[propertyName] = field;
        }

        foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (IsIncompletePartialProperty(property, context.CancellationToken))
                generatedMembers[property.Name] = property;
        }

        var allKnownProperties = existingProperties.Union(generatedMembers.Keys);
        var inpcType = context.SemanticModel.Compilation.GetTypeByMetadataName(
            "System.ComponentModel.INotifyPropertyChanged"
        );
        var edges = ImmutableArray.CreateBuilder<DependencyEdge>();
        var analyzedMembers = generatedMembers.ToList();
        foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (
                !generatedMembers.ContainsKey(property.Name)
                && GetNotifyAlsoAttributes(property).Any(RequestsNotifyFrom)
            )
            {
                analyzedMembers.Add(new KeyValuePair<string, ISymbol>(property.Name, property));
            }
        }

        foreach (var generatedMember in analyzedMembers)
        {
            foreach (var attribute in GetNotifyAlsoAttributes(generatedMember.Value))
            {
                var referencedName = attribute.ConstructorArguments.FirstOrDefault().Value as string;
                if (string.IsNullOrEmpty(referencedName))
                    continue;

                var notifyFrom = RequestsNotifyFrom(attribute);
                var sourceName = notifyFrom ? referencedName! : generatedMember.Key;
                var targetName = notifyFrom ? generatedMember.Key : referencedName!;
                var location = GetAttributeLocation(
                    generatedMember.Value,
                    attribute,
                    context.CancellationToken
                );

                if (RequestsCollectionNotification(attribute))
                {
                    if (notifyFrom)
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                DiagnosticDescriptors.NotifyAlsoTargetCollectionUnsupported,
                                location,
                                targetName
                            )
                        );
                    }
                    else if (!MemberHasReferenceType(generatedMember.Value))
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                DiagnosticDescriptors.NotifyAlsoCollectionRequiresReference,
                                location,
                                generatedMember.Value.Name
                            )
                        );
                    }
                }

                if (notifyFrom && RequestsSubPropertyNotification(attribute))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.NotifyAlsoTargetSubPropertyUnsupported,
                            location,
                            targetName
                        )
                    );
                }

                if (
                    !notifyFrom
                    && RequestsSubPropertyNotification(attribute)
                    && !MemberImplementsInpc(generatedMember.Value, inpcType)
                )
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.NotifyAlsoSubPropertyRequiresInpc,
                            location,
                            generatedMember.Value.Name
                        )
                    );
                }

                if (notifyFrom && !generatedMembers.ContainsKey(sourceName))
                {
                    if (!allKnownProperties.Contains(sourceName))
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                DiagnosticDescriptors.UnknownNotifyAlsoProperty,
                                location,
                                generatedMember.Value.Name,
                                sourceName
                            )
                        );
                    }
                    else
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                DiagnosticDescriptors.NotifyAlsoTargetRequiresGeneratedSource,
                                location,
                                targetName,
                                sourceName
                            )
                        );
                    }
                    continue;
                }

                var endpointName = notifyFrom ? sourceName : targetName;
                if (!allKnownProperties.Contains(endpointName))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.UnknownNotifyAlsoProperty,
                            location,
                            generatedMember.Value.Name,
                            endpointName
                        )
                    );
                    continue;
                }

                if (generatedMembers.ContainsKey(sourceName))
                {
                    edges.Add(
                        new DependencyEdge(
                            sourceName,
                            targetName,
                            generatedMember.Value,
                            attribute
                        )
                    );
                }
            }
        }

        ReportDependencyCycle(context, edges.ToImmutable());
    }

    private static bool RequestsCollectionNotification(AttributeData attribute) =>
        attribute.NamedArguments.Any(named =>
            named.Key == "NotifyOnCollectionChanged" && named.Value.Value is true
        );

    private static bool MemberHasReferenceType(ISymbol member)
    {
        var type = member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null,
        };
        return type is INamedTypeSymbol namedType
            ? namedType.IsReferenceType
            : type is ITypeParameterSymbol typeParameter
                && typeParameter.HasReferenceTypeConstraint;
    }

    private static bool RequestsSubPropertyNotification(AttributeData attribute) =>
        attribute.NamedArguments.Any(named =>
            named.Key == "NotifyOnSubPropertyChanged"
            && named.Value.Value is true
        );

    private static bool RequestsNotifyFrom(AttributeData attribute) =>
        attribute.NamedArguments.Any(named =>
            named.Key == "NotifyFrom" && named.Value.Value is true
        );

    private static bool MemberImplementsInpc(ISymbol member, INamedTypeSymbol? inpcType)
    {
        if (inpcType == null)
            return false;

        var type = member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null,
        };
        if (type == null)
            return false;

        if (type is INamedTypeSymbol namedType)
        {
            return namedType.IsReferenceType
                && (
                    SymbolEqualityComparer.Default.Equals(namedType, inpcType)
                    || namedType.AllInterfaces.Contains(inpcType, SymbolEqualityComparer.Default)
                );
        }

        return type is ITypeParameterSymbol typeParameter
            && typeParameter.HasReferenceTypeConstraint
            && typeParameter.ConstraintTypes.Any(constraint =>
                SymbolEqualityComparer.Default.Equals(constraint, inpcType)
                || constraint is INamedTypeSymbol namedConstraint
                    && namedConstraint.AllInterfaces.Contains(inpcType, SymbolEqualityComparer.Default)
            );
    }

    private static IEnumerable<AttributeData> GetNotifyAlsoAttributes(ISymbol member) =>
        member
            .GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == NotifyAlsoAttributeName);

    private static string GetGeneratedPropertyName(ISymbol member) =>
        member switch
        {
            IPropertySymbol property => property.Name,
            IFieldSymbol field => GetPropertyName(field),
            _ => member.Name,
        };

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

    private static void ReportDependencyCycle(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<DependencyEdge> edges
    )
    {
        var edgesBySource = edges
            .GroupBy(edge => edge.Source)
            .ToDictionary(group => group.Key, group => group.ToImmutableArray());
        var colors = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new List<string>();

        foreach (var source in edgesBySource.Keys.OrderBy(static name => name))
        {
            if (colors.ContainsKey(source))
                continue;

            if (Visit(source))
                return;
        }

        bool Visit(string source)
        {
            colors[source] = 1;
            stack.Add(source);

            if (edgesBySource.TryGetValue(source, out var sourceEdges))
            {
                foreach (var edge in sourceEdges)
                {
                    if (colors.TryGetValue(edge.Target, out var targetColor))
                    {
                        if (targetColor == 1)
                        {
                            var cycleStart = stack.IndexOf(edge.Target);
                            var cycle = stack
                                .Skip(cycleStart)
                                .Concat(new[] { edge.Target });
                            var location = GetAttributeLocation(
                                edge.Member,
                                edge.Attribute,
                                context.CancellationToken
                            );
                            context.ReportDiagnostic(
                                Diagnostic.Create(
                                    DiagnosticDescriptors.NotifyAlsoDependencyCycle,
                                    location,
                                    string.Join(" -> ", cycle)
                                )
                            );
                            return true;
                        }

                        if (targetColor == 2)
                            continue;
                    }

                    if (Visit(edge.Target))
                        return true;
                }
            }

            stack.RemoveAt(stack.Count - 1);
            colors[source] = 2;
            return false;
        }
    }

    private readonly struct DependencyEdge
    {
        public string Source { get; }
        public string Target { get; }
        public ISymbol Member { get; }
        public AttributeData Attribute { get; }

        public DependencyEdge(
            string source,
            string target,
            ISymbol member,
            AttributeData attribute
        )
        {
            Source = source;
            Target = target;
            Member = member;
            Attribute = attribute;
        }
    }

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

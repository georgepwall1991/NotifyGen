using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace NotifyGen.Generator;

/// <summary>
/// Incremental source generator that generates INotifyPropertyChanged implementation
/// for classes marked with [Notify].
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class NotifyGenerator : IIncrementalGenerator
{
    private const string NotifyAttributeName = "NotifyGen.NotifyAttribute";
    private const string NotifyAlsoAttributeName = "NotifyGen.NotifyAlsoAttribute";
    private const string NotifyNameAttributeName = "NotifyGen.NotifyNameAttribute";
    private const string NotifySetterAttributeName = "NotifyGen.NotifySetterAttribute";
    private const string NotifyCanExecuteChangedForAttributeName =
        "NotifyGen.NotifyCanExecuteChangedForAttribute";
    private const string NotifySuppressableAttributeName = "NotifyGen.NotifySuppressableAttribute";
    private const string AttributeUsageAttributeName = "System.AttributeUsageAttribute";

    /// <summary>
    /// Cached SymbolDisplayFormat for type names to avoid repeated allocations.
    /// </summary>
    private static readonly SymbolDisplayFormat TypeDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    private static readonly SymbolDisplayFormat FullyQualifiedTypeDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all class declarations with [Notify] attribute
        var classDeclarations = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateClass(node),
                transform: static (ctx, ct) => GetClassInfo(ctx, ct)
            )
            .Where(static info => info.HasValue)
            .Select(static (info, _) => info!.Value);

        // Generate source for each class
        context.RegisterSourceOutput(classDeclarations, GenerateSource);
    }

    /// <summary>
    /// Quick syntax check to filter candidate classes.
    /// </summary>
    private static bool IsCandidateClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDecl
            && classDecl.AttributeLists.Count > 0
            && classDecl.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    /// <summary>
    /// Extracts class information from the semantic model.
    /// </summary>
    private static NotificationTypeInfo? GetClassInfo(
        GeneratorSyntaxContext context,
        CancellationToken ct
    )
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        if (
            semanticModel.GetDeclaredSymbol(classDeclaration, ct)
            is not INamedTypeSymbol classSymbol
        )
            return null;

        var notifyAttribute = classSymbol
            .GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == NotifyAttributeName);
        if (notifyAttribute == null)
            return null;

        if (
            notifyAttribute.ApplicationSyntaxReference?.GetSyntax(ct)
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
            return null;
        }

        if (
            !TypeDeclarationInfoFactory.TryCreateChain(
                semanticModel,
                classDeclaration,
                ct,
                out var typeDeclarations
            )
        )
            return null;

        var implementChanging =
            notifyAttribute
                .NamedArguments.FirstOrDefault(a => a.Key == "ImplementChanging")
                .Value.Value
            is true;

        var suppressableAttribute = classSymbol
            .GetAttributes()
            .FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == NotifySuppressableAttributeName
            );
        var isSuppressable = suppressableAttribute != null;
        var alwaysNotifyProperties = ImmutableArray<string>.Empty;
        if (suppressableAttribute != null)
        {
            var alwaysNotifyArg = suppressableAttribute.NamedArguments.FirstOrDefault(a =>
                a.Key == "AlwaysNotify"
            );
            if (alwaysNotifyArg.Value.Kind == TypedConstantKind.Array)
            {
                alwaysNotifyProperties = alwaysNotifyArg
                    .Value.Values.Where(v => v.Value is string)
                    .Select(v => (string)v.Value!)
                    .ToImmutableArray();
            }
        }

        var inpcInterface = semanticModel.Compilation.GetTypeByMetadataName(
            "System.ComponentModel.INotifyPropertyChanged"
        );
        var alreadyImplementsInpc =
            inpcInterface != null
            && classSymbol.AllInterfaces.Contains(inpcInterface, SymbolEqualityComparer.Default);

        var propertyChangedInvoker = alreadyImplementsInpc
            ? PropertyChangedInvoker.Find(classSymbol)
            : PropertyChangedInvokerKind.Generated;

        var inpcChangingInterface = semanticModel.Compilation.GetTypeByMetadataName(
            "System.ComponentModel.INotifyPropertyChanging"
        );
        var alreadyImplementsInpcChanging =
            inpcChangingInterface != null
            && classSymbol.AllInterfaces.Contains(
                inpcChangingInterface,
                SymbolEqualityComparer.Default
            );
        var propertyChangingInvoker = alreadyImplementsInpcChanging
            ? PropertyChangingInvoker.Find(classSymbol)
            : PropertyChangingInvokerKind.Generated;

        var containingNamespace = classSymbol.ContainingNamespace;
        var namespaceName = containingNamespace.IsGlobalNamespace
            ? string.Empty
            : containingNamespace.ToDisplayString();

        return new NotificationTypeInfo(
            namespaceName,
            typeDeclarations,
            alreadyImplementsInpc,
            propertyChangedInvoker,
            alreadyImplementsInpcChanging,
            propertyChangingInvoker,
            implementChanging,
            isSuppressable,
            alwaysNotifyProperties,
            ExtractFields(classSymbol, semanticModel.Compilation, ct),
            classSymbol.GetMembers().Select(static member => member.Name).ToImmutableArray()
        );
    }

    /// <summary>
    /// Extracts field and incomplete partial-property information from the class.
    /// </summary>
    private static ImmutableArray<FieldInfo> ExtractFields(
        INamedTypeSymbol classSymbol,
        Compilation compilation,
        CancellationToken ct
    )
    {
        var members = ImmutableArray.CreateBuilder<FieldInfo>();
        foreach (var member in classSymbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (
                member is IFieldSymbol field
                && FieldEligibilityClassifier.Classify(field) == FieldEligibility.Eligible
            )
            {
                if (
                    GeneratedPropertyNameValidation.IsValid(GetPropertyName(field))
                    && !IsFileLocalType(field.Type, ct)
                )
                    members.Add(CreateFieldInfo(field, compilation, ct));
            }
            else if (
                member is IPropertySymbol property
                && IsIncompletePartialProperty(property, ct)
                && !IsFileLocalType(property.Type, ct)
            )
            {
                members.Add(CreatePartialPropertyInfo(property));
            }
        }

        var directMembers = members.ToImmutable();
        if (
            directMembers
                .GroupBy(static member => member.PropertyName)
                .Any(static group => group.Skip(1).Any())
            || classSymbol
                .GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property => !IsIncompletePartialProperty(property, ct))
                .Select(static property => property.Name)
                .Intersect(
                    directMembers.Select(static member => member.PropertyName),
                    StringComparer.Ordinal
                )
                .Any()
        )
        {
            return ImmutableArray<FieldInfo>.Empty;
        }

        var directTargets = directMembers.ToDictionary(
            static member => member.PropertyName,
            static member => member.AlsoNotify.ToBuilder(),
            StringComparer.Ordinal
        );
        var targetPropertyNames = new HashSet<string>(
            classSymbol
                .GetMembers()
                .OfType<IPropertySymbol>()
                .Select(static property => property.Name),
            StringComparer.Ordinal
        );
        foreach (var field in classSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (
                FieldEligibilityClassifier.Classify(field) == FieldEligibility.Eligible
                && GeneratedPropertyNameValidation.IsValid(GetPropertyName(field))
            )
                targetPropertyNames.Add(GetPropertyName(field));
        }

        foreach (var member in classSymbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            var targetName = member switch
            {
                IFieldSymbol field
                    when FieldEligibilityClassifier.Classify(field) == FieldEligibility.Eligible
                        && GeneratedPropertyNameValidation.IsValid(GetPropertyName(field))
                    => GetPropertyName(field),
                IPropertySymbol property => property.Name,
                _ => null,
            };
            if (targetName == null || !targetPropertyNames.Contains(targetName))
                continue;

            foreach (var attribute in GetNotifyAlsoAttributes(member))
            {
                if (!RequestsNotifyFrom(attribute))
                    continue;

                var sourceName = attribute.ConstructorArguments.FirstOrDefault().Value as string;
                if (
                    !string.IsNullOrEmpty(sourceName)
                    && directTargets.TryGetValue(sourceName!, out var targets)
                )
                {
                    targets.Add(targetName);
                }
            }
        }

        var mergedMembers = directMembers.ToDictionary(
            static member => member.PropertyName,
            member => member.WithAlsoNotify(directTargets[member.PropertyName].ToImmutable()),
            StringComparer.Ordinal
        );
        var result = ImmutableArray.CreateBuilder<FieldInfo>(directMembers.Length);
        foreach (var member in directMembers)
        {
            ct.ThrowIfCancellationRequested();
            result.Add(
                member.WithAlsoNotify(
                    ExpandAlsoNotify(
                        member.PropertyName,
                        mergedMembers[member.PropertyName].AlsoNotify,
                        mergedMembers
                    )
                )
            );
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<string> ExpandAlsoNotify(
        string sourcePropertyName,
        ImmutableArray<string> directTargets,
        IReadOnlyDictionary<string, FieldInfo> membersByPropertyName
    )
    {
        var expanded = ImmutableArray.CreateBuilder<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { sourcePropertyName };

        // Preserve the existing direct-edge order before walking newly discovered edges.
        foreach (var target in directTargets)
        {
            if (visited.Add(target))
                expanded.Add(target);
        }

        foreach (var target in directTargets)
        {
            AppendTransitiveAlsoNotify(target, expanded, visited, membersByPropertyName);
        }

        return expanded.ToImmutable();
    }

    private static void AppendTransitiveAlsoNotify(
        string source,
        ImmutableArray<string>.Builder expanded,
        HashSet<string> visited,
        IReadOnlyDictionary<string, FieldInfo> membersByPropertyName
    )
    {
        if (!membersByPropertyName.TryGetValue(source, out var sourceMember))
            return;

        foreach (var target in sourceMember.AlsoNotify)
        {
            if (visited.Add(target))
            {
                expanded.Add(target);
                AppendTransitiveAlsoNotify(target, expanded, visited, membersByPropertyName);
            }
        }
    }

    /// <summary>
    /// Creates metadata from an eligible field symbol.
    /// </summary>
    private static FieldInfo CreateFieldInfo(
        IFieldSymbol field,
        Compilation compilation,
        CancellationToken cancellationToken
    )
    {
        var propertyName = GetPropertyName(field);
        var typeName = field.Type.ToDisplayString(TypeDisplayFormat);
        var isNullable = IsNullableType(field.Type);
        var alsoNotify = GetSourceNotifyAlsoValues(field);
        var commandsToNotify = GetAttributeValues(field, NotifyCanExecuteChangedForAttributeName);
        var setterAccess = GetSetterAccessLevel(field);
        var isPrimitiveType = IsPrimitiveValueType(field.Type);
        var requiresUnsafe = RequiresUnsafeContext(field.Type);
        var typedHook = FindNonPartialTypedChangedHook(
            field.ContainingType,
            propertyName,
            field.Type
        );
        CollectForwardedAttributes(
            field,
            compilation,
            cancellationToken,
            out var propertyAttributes,
            out var getterAttributes,
            out var setterAttributes
        );

        return new FieldInfo(
            field.Name,
            propertyName,
            typeName,
            isNullable,
            alsoNotify,
            commandsToNotify,
            setterAccess,
            isPrimitiveType,
            requiresUnsafe,
            propertyAttributes: propertyAttributes,
            getterAttributes: getterAttributes,
            setterAttributes: setterAttributes,
            subPropertyNotify: GetSubPropertyNotifyTargets(field),
            collectionNotify: GetCollectionNotifyTargets(field),
            hasNonPartialTypedChangedHook: typedHook is not null,
            existingTypedChangedHookParameterTypeName: GetExistingTypedHookParameterTypeName(
                typedHook,
                field.Type,
                0
            ),
            existingTypedChangedHookNewParameterTypeName: GetExistingTypedHookParameterTypeName(
                typedHook,
                field.Type,
                1
            )
        );
    }

    /// <summary>
    /// Creates metadata from an incomplete C# 14 partial property definition.
    /// </summary>
    private static FieldInfo CreatePartialPropertyInfo(IPropertySymbol property)
    {
        var typeName = property.Type.ToDisplayString(TypeDisplayFormat);
        var typedHook = FindNonPartialTypedChangedHook(
            property.ContainingType,
            property.Name,
            property.Type
        );
        return new FieldInfo(
            "field",
            property.Name,
            typeName,
            IsNullableType(property.Type),
            GetSourceNotifyAlsoValues(property),
            GetAttributeValues(property, NotifyCanExecuteChangedForAttributeName),
            GetAccessorAccessLevel(property.SetMethod, property.DeclaredAccessibility),
            isPrimitiveType: IsPrimitiveValueType(property.Type),
            requiresUnsafe: RequiresUnsafeContext(property.Type),
            isPartialProperty: true,
            propertyAccessibility: GetAccessibilityText(property.DeclaredAccessibility),
            needsNullableBackingField: IsNonNullableReferenceType(property.Type),
            getterAccess: GetAccessorAccessLevel(property.GetMethod, property.DeclaredAccessibility),
            subPropertyNotify: GetSubPropertyNotifyTargets(property),
            collectionNotify: GetCollectionNotifyTargets(property),
            hasNonPartialTypedChangedHook: typedHook is not null,
            existingTypedChangedHookParameterTypeName: GetExistingTypedHookParameterTypeName(
                typedHook,
                property.Type,
                0
            ),
            existingTypedChangedHookNewParameterTypeName: GetExistingTypedHookParameterTypeName(
                typedHook,
                property.Type,
                1
            )
        );
    }

    private static IMethodSymbol? FindNonPartialTypedChangedHook(
        INamedTypeSymbol containingType,
        string propertyName,
        ITypeSymbol propertyType
    )
    {
        var hookName = $"On{propertyName}Changed";
        for (var type = containingType; type is not null; type = type.BaseType)
        {
            foreach (var method in type.GetMembers(hookName).OfType<IMethodSymbol>())
            {
                if (
                    method.Parameters.Length == 2
                    && method.Parameters[0].RefKind == RefKind.None
                    && method.Parameters[1].RefKind == RefKind.None
                    && AreHookTypesEquivalent(method.Parameters[0].Type, propertyType)
                    && AreHookTypesEquivalent(method.Parameters[1].Type, propertyType)
                    && !IsPartialMethod(method)
                    && (
                        SymbolEqualityComparer.Default.Equals(method.ContainingType, containingType)
                        || IsInheritedHookAccessible(method, containingType)
                    )
                )
                {
                    return method;
                }
            }
        }

        return null;
    }

    private static string? GetExistingTypedHookParameterTypeName(
        IMethodSymbol? method,
        ITypeSymbol propertyType,
        int parameterIndex
    )
    {
        if (
            method is null
            || SymbolEqualityComparer.IncludeNullability.Equals(
                method.Parameters[parameterIndex].Type,
                propertyType
            )
        )
        {
            return null;
        }

        var parameterType = method.Parameters[parameterIndex].Type;
        if (parameterType.TypeKind == TypeKind.Dynamic)
        {
            return parameterType.NullableAnnotation == NullableAnnotation.Annotated
                ? "global::System.Object?"
                : "global::System.Object";
        }

        return parameterType.ToDisplayString(TypeDisplayFormat);
    }

    private static bool IsInheritedHookAccessible(
        IMethodSymbol method,
        INamedTypeSymbol containingType
    )
    {
        return method.DeclaredAccessibility switch
        {
            Accessibility.Public
                or Accessibility.Protected
                or Accessibility.ProtectedOrInternal => true,
            Accessibility.Internal
                or Accessibility.ProtectedAndInternal => SymbolEqualityComparer.Default.Equals(
                method.ContainingAssembly,
                containingType.ContainingAssembly
            ),
            _ => false,
        };
    }

    private static bool AreHookTypesEquivalent(ITypeSymbol left, ITypeSymbol right)
    {
        if (SymbolEqualityComparer.Default.Equals(left, right))
            return true;

        if (
            SymbolEqualityComparer.Default.Equals(
                left.WithNullableAnnotation(NullableAnnotation.None),
                right.WithNullableAnnotation(NullableAnnotation.None)
            )
        )
        {
            return true;
        }

        // `dynamic` and `object` have the same metadata signature, even though
        // Roslyn preserves a dynamic annotation on one of the symbols.
        if (
            (left.TypeKind == TypeKind.Dynamic || left.SpecialType == SpecialType.System_Object)
            && (right.TypeKind == TypeKind.Dynamic || right.SpecialType == SpecialType.System_Object)
        )
        {
            return true;
        }

        if (left is IArrayTypeSymbol leftArray && right is IArrayTypeSymbol rightArray)
        {
            return leftArray.Rank == rightArray.Rank
                && AreHookTypesEquivalent(leftArray.ElementType, rightArray.ElementType);
        }

        if (left is INamedTypeSymbol leftNamed && right is INamedTypeSymbol rightNamed)
        {
            return SymbolEqualityComparer.Default.Equals(
                    leftNamed.OriginalDefinition,
                    rightNamed.OriginalDefinition
                )
                && leftNamed.TypeArguments.Length == rightNamed.TypeArguments.Length
                && Enumerable
                    .Range(0, leftNamed.TypeArguments.Length)
                    .All(index =>
                        AreHookTypesEquivalent(
                            leftNamed.TypeArguments[index],
                            rightNamed.TypeArguments[index]
                        )
                    );
        }

        return false;
    }

    private static bool IsPartialMethod(IMethodSymbol method) =>
        method.IsPartialDefinition
        || method.PartialDefinitionPart is not null
        || method.PartialImplementationPart is not null
        || method.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is MethodDeclarationSyntax declaration
            && declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
        );

    private static bool IsIncompletePartialProperty(
        IPropertySymbol property,
        CancellationToken ct
    ) => PartialPropertyEligibility.IsSupported(property, ct);

    /// <summary>
    /// Gets the property name from [NotifyName] or derives it from the field name.
    /// </summary>
    private static string GetPropertyName(IFieldSymbol field)
    {
        // Get property name from [NotifyName] or derive from field name (_name -> Name)
        var notifyNameAttr = field
            .GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == NotifyNameAttributeName);

        if (notifyNameAttr?.ConstructorArguments.FirstOrDefault().Value is string customName)
            return customName;

        return char.ToUpperInvariant(field.Name[1]) + field.Name.Substring(2);
    }

    private static bool IsNonNullableReferenceType(ITypeSymbol type) =>
        type.IsReferenceType && type.NullableAnnotation != NullableAnnotation.Annotated;

    /// <summary>
    /// Checks if a type is nullable.
    /// </summary>
    private static bool IsNullableType(ITypeSymbol type)
    {
        return type.NullableAnnotation == NullableAnnotation.Annotated
            || (
                type is INamedTypeSymbol namedType
                && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            );
    }

    /// <summary>
    /// Collects untargeted property-forwardable attributes plus explicit property/get/set targets.
    /// </summary>
    private static void CollectForwardedAttributes(
        IFieldSymbol field,
        Compilation compilation,
        CancellationToken cancellationToken,
        out ImmutableArray<string> propertyAttributes,
        out ImmutableArray<string> getterAttributes,
        out ImmutableArray<string> setterAttributes
    )
    {
        var property = ImmutableArray.CreateBuilder<string>();
        var getters = ImmutableArray.CreateBuilder<string>();
        var setters = ImmutableArray.CreateBuilder<string>();

        foreach (var attribute in field.GetAttributes())
        {
            if (
                attribute.AttributeClass is not { } attributeClass
                || IsNotifyGenAttribute(attributeClass)
                || IsFileLocalType(attributeClass, cancellationToken)
                || attribute.ConstructorArguments.Any(argument =>
                    ContainsFileLocalType(argument, cancellationToken)
                )
                || attribute.NamedArguments.Any(named =>
                    ContainsFileLocalType(named.Value, cancellationToken)
                )
                || !CanApplyToProperty(attributeClass, cancellationToken)
            )
            {
                continue;
            }

            property.Add(FormatAttribute(attribute));
        }

        foreach (var syntaxReference in field.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (syntaxReference.GetSyntax(cancellationToken) is not VariableDeclaratorSyntax)
                continue;

            var tree = syntaxReference.SyntaxTree;
            var semanticModel = compilation.GetSemanticModel(tree);
            var fieldDeclaration = syntaxReference
                .GetSyntax(cancellationToken)
                .Ancestors()
                .OfType<FieldDeclarationSyntax>()
                .FirstOrDefault();
            if (fieldDeclaration is null)
                continue;

            foreach (var attributeList in fieldDeclaration.AttributeLists)
            {
                if (attributeList.Target?.Identifier is not { } targetToken)
                    continue;

                ImmutableArray<string>.Builder? destination = targetToken.Kind() switch
                {
                    SyntaxKind.PropertyKeyword => property,
                    SyntaxKind.GetKeyword => getters,
                    SyntaxKind.SetKeyword => setters,
                    _ => null,
                };
                if (destination is null)
                    continue;

                foreach (var attributeSyntax in attributeList.Attributes)
                {
                    if (
                        !TryFormatAttributeFromSyntax(
                            attributeSyntax,
                            semanticModel,
                            cancellationToken,
                            out var formatted
                        )
                    )
                    {
                        continue;
                    }

                    destination.Add(formatted);
                }
            }
        }

        propertyAttributes = property.ToImmutable();
        getterAttributes = getters.ToImmutable();
        setterAttributes = setters.ToImmutable();
    }

    private static bool TryFormatAttributeFromSyntax(
        AttributeSyntax attributeSyntax,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string formatted
    )
    {
        formatted = string.Empty;
        if (
            !TryGetAttributeTypeSymbol(
                semanticModel.GetSymbolInfo(attributeSyntax, cancellationToken),
                out var attributeClass
            )
            || IsNotifyGenAttribute(attributeClass)
            || IsFileLocalType(attributeClass, cancellationToken)
        )
        {
            return false;
        }

        var constructorArguments = ImmutableArray.CreateBuilder<string>();
        var namedArguments = ImmutableArray.CreateBuilder<string>();
        foreach (
            var argument in attributeSyntax.ArgumentList?.Arguments
                ?? Enumerable.Empty<AttributeArgumentSyntax>()
        )
        {
            if (!TryFormatAttributeArgumentExpression(
                    argument.Expression,
                    semanticModel,
                    cancellationToken,
                    out var value
                )
                || ContainsFileLocalTypeFromExpression(
                    argument.Expression,
                    semanticModel,
                    cancellationToken
                )
            )
            {
                return false;
            }

            if (argument.NameEquals is { } nameEquals)
            {
                namedArguments.Add(
                    $"{EscapeIdentifier(nameEquals.Name.Identifier.ValueText)} = {value}"
                );
            }
            else
            {
                constructorArguments.Add(value);
            }
        }

        var attributeType = attributeClass.ToDisplayString(FullyQualifiedTypeDisplayFormat);
        var allArgs = constructorArguments.Concat(namedArguments);
        formatted = $"[{attributeType}({string.Join(", ", allArgs)})]";
        return true;
    }

    private static bool TryGetAttributeTypeSymbol(
        SymbolInfo symbolInfo,
        out INamedTypeSymbol attributeClass
    )
    {
        ISymbol? attributeSymbol = symbolInfo.Symbol;
        if (attributeSymbol is null && symbolInfo.CandidateSymbols.Length == 1)
            attributeSymbol = symbolInfo.CandidateSymbols[0];

        attributeClass =
            (attributeSymbol as INamedTypeSymbol)
            ?? attributeSymbol?.ContainingType!;
        return attributeClass is not null;
    }

    private static bool ContainsFileLocalTypeFromExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken
    )
    {
        var typeInfo = semanticModel.GetTypeInfo(expression, cancellationToken).Type;
        if (typeInfo != null && IsFileLocalType(typeInfo, cancellationToken))
            return true;

        if (
            expression is TypeOfExpressionSyntax typeOf
            && semanticModel.GetTypeInfo(typeOf.Type, cancellationToken).Type is { } typeofType
        )
        {
            return IsFileLocalType(typeofType, cancellationToken);
        }

        return false;
    }

    private static bool TryFormatAttributeArgumentExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string formatted
    )
    {
        formatted = string.Empty;
        if (expression is TypeOfExpressionSyntax typeOfExpression)
        {
            var type = semanticModel.GetTypeInfo(typeOfExpression.Type, cancellationToken).Type;
            if (type is null)
                return false;
            formatted = $"typeof({FormatType(type)})";
            return true;
        }

        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        if (constant.HasValue)
        {
            formatted = FormatConstantObject(
                constant.Value,
                semanticModel.GetTypeInfo(expression, cancellationToken).Type
            );
            return true;
        }

        if (expression is ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax)
        {
            // Fall back to operation-based constants for simple arrays when available.
            if (
                semanticModel.GetOperation(expression, cancellationToken)
                    is IArrayCreationOperation { Initializer: { } initializer }
            )
            {
                var elements = new List<string>();
                foreach (var element in initializer.ElementValues)
                {
                    if (element.ConstantValue.HasValue)
                    {
                        elements.Add(
                            FormatConstantObject(
                                element.ConstantValue.Value,
                                element.Type
                            )
                        );
                    }
                    else
                    {
                        return false;
                    }
                }

                var elementType =
                    semanticModel.GetTypeInfo(expression, cancellationToken).Type
                        is IArrayTypeSymbol arrayType
                        ? FormatType(arrayType.ElementType)
                        : "object";
                formatted = $"new {elementType}[] {{ {string.Join(", ", elements)} }}";
                return true;
            }
        }

        return false;
    }

    private static string FormatConstantObject(object? value, ITypeSymbol? type)
    {
        if (value is null)
            return "null";

        if (value is string s)
            return SymbolDisplay.FormatLiteral(s, quote: true);

        if (value is char c)
            return SymbolDisplay.FormatLiteral(c, quote: true);

        if (value is bool b)
            return b ? "true" : "false";

        if (value is double d)
            return d.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "D";

        if (value is float f)
            return f.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "F";

        if (value is decimal m)
            return m.ToString(System.Globalization.CultureInfo.InvariantCulture) + "M";

        if (type?.SpecialType == SpecialType.System_Byte)
            return $"(byte){System.Convert.ToByte(value, System.Globalization.CultureInfo.InvariantCulture)}";

        if (type?.SpecialType == SpecialType.System_SByte)
            return $"(sbyte){System.Convert.ToSByte(value, System.Globalization.CultureInfo.InvariantCulture)}";

        if (type?.SpecialType == SpecialType.System_Int16)
            return $"(short){System.Convert.ToInt16(value, System.Globalization.CultureInfo.InvariantCulture)}";

        if (type?.SpecialType == SpecialType.System_UInt16)
            return $"(ushort){System.Convert.ToUInt16(value, System.Globalization.CultureInfo.InvariantCulture)}";

        if (type?.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumType)
        {
            var name = enumType
                .GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(field =>
                    field.HasConstantValue && Equals(field.ConstantValue, value)
                )
                ?.Name;
            if (name != null)
                return $"{FormatType(enumType)}.{EscapeIdentifier(name)}";
        }

        if (value is IFormattable formattable)
            return formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture);

        return value.ToString() ?? "null";
    }

    private static bool ContainsFileLocalType(
        TypedConstant constant,
        CancellationToken cancellationToken
    )
    {
        if (constant.IsNull)
            return false;

        if (constant.Type != null && IsFileLocalType(constant.Type, cancellationToken))
            return true;

        if (constant.Kind == TypedConstantKind.Type && constant.Value is ITypeSymbol type)
            return IsFileLocalType(type, cancellationToken);

        return constant.Kind == TypedConstantKind.Array
            && constant.Values.Any(value => ContainsFileLocalType(value, cancellationToken));
    }

    private static bool IsNotifyGenAttribute(INamedTypeSymbol attributeClass)
    {
        var namespaceName = attributeClass.ContainingNamespace.ToDisplayString();
        return namespaceName == "NotifyGen"
            || namespaceName.StartsWith("NotifyGen.", StringComparison.Ordinal);
    }

    private static bool IsFileLocalType(
        ITypeSymbol type,
        CancellationToken cancellationToken
    )
    {
        return type switch
        {
            IArrayTypeSymbol array
                => IsFileLocalType(array.ElementType, cancellationToken),
            IPointerTypeSymbol pointer
                => IsFileLocalType(pointer.PointedAtType, cancellationToken),
            INamedTypeSymbol named
                => IsFileLocalNamedType(named, cancellationToken)
                    || named.TypeArguments.Any(argument =>
                        IsFileLocalType(argument, cancellationToken)
                    ),
            _ => false,
        };
    }

    private static bool IsFileLocalNamedType(
        INamedTypeSymbol type,
        CancellationToken cancellationToken
    )
    {
        for (var current = type; current != null; current = current.ContainingType)
        {
            foreach (var reference in current.DeclaringSyntaxReferences)
            {
                var syntax = reference.GetSyntax(cancellationToken);
                if (
                    syntax is BaseTypeDeclarationSyntax declaration
                    && declaration.Modifiers.Any(SyntaxKind.FileKeyword)
                    || syntax is DelegateDeclarationSyntax delegateDeclaration
                        && delegateDeclaration.Modifiers.Any(SyntaxKind.FileKeyword)
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsDirectAttributeUsage(
        AttributeData usage,
        INamedTypeSymbol attributeClass,
        CancellationToken cancellationToken
    )
    {
        if (
            usage.ApplicationSyntaxReference?.GetSyntax(cancellationToken)
            is not AttributeSyntax attributeSyntax
        )
        {
            return false;
        }

        var declaringType = attributeSyntax
            .Ancestors()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();
        return declaringType != null
            && attributeClass.DeclaringSyntaxReferences.Any(reference =>
                reference.GetSyntax(cancellationToken).Span == declaringType.Span
            );
    }

    private static bool CanApplyToProperty(
        INamedTypeSymbol attributeClass,
        CancellationToken cancellationToken
    )
    {
        AttributeData? usage = null;
        for (var current = attributeClass; current != null; current = current.BaseType)
        {
            var hasSourceDeclaration = current.DeclaringSyntaxReferences.Length > 0;
            usage = current
                .GetAttributes()
                .Where(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == AttributeUsageAttributeName
                )
                .FirstOrDefault(attribute =>
                    !hasSourceDeclaration
                    || IsDirectAttributeUsage(attribute, current, cancellationToken)
                );
            if (usage != null)
                break;
        }

        if (usage == null || usage.ConstructorArguments.Length == 0)
            return true;

        var targets = usage.ConstructorArguments[0].Value;
        if (targets == null)
            return false;

        var targetValue = Convert.ToInt64(
            targets,
            System.Globalization.CultureInfo.InvariantCulture
        );
        return ((AttributeTargets)targetValue & AttributeTargets.Property) != 0;
    }

    private static string FormatAttribute(AttributeData attribute)
    {
        var attributeType = attribute.AttributeClass!.ToDisplayString(
            FullyQualifiedTypeDisplayFormat
        );
        var arguments = attribute
            .ConstructorArguments
            .Select(FormatTypedConstant)
            .Concat(
                attribute.NamedArguments.Select(named =>
                    $"{EscapeIdentifier(named.Key)} = {FormatTypedConstant(named.Value)}"
                )
            );
        return $"[{attributeType}({string.Join(", ", arguments)})]";
    }

    private static string FormatTypedConstant(TypedConstant constant)
    {
        if (constant.IsNull)
            return "null";

        if (constant.Kind == TypedConstantKind.Array)
        {
            var arrayType = (IArrayTypeSymbol)constant.Type!;
            return $"new {FormatType(arrayType.ElementType)}[] {{ {string.Join(", ", constant.Values.Select(FormatTypedConstant))} }}";
        }

        if (constant.Kind == TypedConstantKind.Type && constant.Value is ITypeSymbol type)
            return $"typeof({FormatType(type)})";

        if (constant.Kind == TypedConstantKind.Enum)
        {
            return $"({FormatType(constant.Type!)}){Convert.ToString(constant.Value, System.Globalization.CultureInfo.InvariantCulture)}";
        }

        var formatted = FormatPrimitive(constant.Value!);
        formatted = constant.Type?.SpecialType switch
        {
            SpecialType.System_Byte => $"(byte){formatted}",
            SpecialType.System_SByte => $"(sbyte){formatted}",
            SpecialType.System_Int16 => $"(short){formatted}",
            SpecialType.System_UInt16 => $"(ushort){formatted}",
            _ => formatted,
        };

        return formatted;
    }

    private static string EscapeIdentifier(string identifier) =>
        SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
            || SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None
            ? "@" + identifier
            : identifier;

    private static string FormatType(ITypeSymbol type) =>
        type.ToDisplayString(FullyQualifiedTypeDisplayFormat);

    private static string FormatPrimitive(object value) =>
        value switch
        {
            string text => QuoteString(text),
            char character => QuoteChar(character),
            bool boolean => boolean ? "true" : "false",
            float single => single.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "F",
            double doubleValue => doubleValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "D",
            long longValue => longValue.ToString(System.Globalization.CultureInfo.InvariantCulture) + "L",
            ulong ulongValue => ulongValue.ToString(System.Globalization.CultureInfo.InvariantCulture) + "UL",
            uint uintValue => uintValue.ToString(System.Globalization.CultureInfo.InvariantCulture) + "U",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!
        };

    private static string QuoteString(string value) => $"\"{Escape(value)}\"";

    private static string QuoteChar(char value) => $"'{Escape(value.ToString())}'";

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        foreach (var character in value)
        {
            if (character == '\\')
                builder.Append('\\').Append('\\');
            else if (character == '"')
                builder.Append('\\').Append('"');
            else if (character == '\'')
                builder.Append('\\').Append('\'');
            else if (character == '\0')
                builder.Append('\\').Append('0');
            else if (character == '\a')
                builder.Append('\\').Append('a');
            else if (character == '\b')
                builder.Append('\\').Append('b');
            else if (character == '\f')
                builder.Append('\\').Append('f');
            else if (character == '\n')
                builder.Append('\\').Append('n');
            else if (character == '\r')
                builder.Append('\\').Append('r');
            else if (character == '\t')
                builder.Append('\\').Append('t');
            else if (character == '\v')
                builder.Append('\\').Append('v');
            else if (char.IsControl(character))
                builder.Append("\\u").Append(((int)character).ToString("X4"));
            else
                builder.Append(character);
        }

        return builder.ToString();
    }

    private static ImmutableArray<string> GetSubPropertyNotifyTargets(ISymbol member)
    {
        var memberType = member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null,
        };
        if (
            memberType is null
            || memberType is not ITypeParameterSymbol
                && !memberType.IsReferenceType
            || memberType is ITypeParameterSymbol parameter
                && !parameter.HasReferenceTypeConstraint
        )
        {
            return ImmutableArray<string>.Empty;
        }

        return member
            .GetAttributes()
            .Where(attribute =>
                attribute.AttributeClass?.ToDisplayString() == NotifyAlsoAttributeName
            )
            .Where(attribute => !RequestsNotifyFrom(attribute))
            .Where(attribute =>
                attribute.NamedArguments.Any(named =>
                    named.Key == "NotifyOnSubPropertyChanged"
                    && named.Value.Value is true
                )
            )
            .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
            .Where(static value => !string.IsNullOrEmpty(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static ImmutableArray<string> GetCollectionNotifyTargets(ISymbol member)
    {
        return GetNotifyAlsoAttributes(member)
            .Where(attribute => !RequestsNotifyFrom(attribute))
            .Where(attribute => attribute.NamedArguments.Any(named =>
                named.Key == "NotifyOnCollectionChanged" && named.Value.Value is true
            ))
            .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
            .Where(static value => !string.IsNullOrEmpty(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static ImmutableArray<string> GetSourceNotifyAlsoValues(ISymbol member)
    {
        return GetNotifyAlsoAttributes(member)
            .Where(attribute => !RequestsNotifyFrom(attribute))
            .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
            .Where(static value => !string.IsNullOrEmpty(value))
            .Cast<string>()
            .ToImmutableArray();
    }

    private static IEnumerable<AttributeData> GetNotifyAlsoAttributes(ISymbol member) =>
        member
            .GetAttributes()
            .Where(attribute =>
                attribute.AttributeClass?.ToDisplayString() == NotifyAlsoAttributeName
            );

    private static bool RequestsNotifyFrom(AttributeData attribute) =>
        attribute.NamedArguments.Any(named =>
            named.Key == "NotifyFrom" && named.Value.Value is true
        );

    /// <summary>
    /// Extracts string values from multiple instances of an attribute.
    /// </summary>
    private static ImmutableArray<string> GetAttributeValues(
        ISymbol member,
        string attributeName
    )
    {
        return member
            .GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == attributeName)
            .Select(a => a.ConstructorArguments.FirstOrDefault().Value as string)
            .Where(s => !string.IsNullOrEmpty(s))
            .Cast<string>()
            .ToImmutableArray();
    }

    /// <summary>
    /// Gets the setter access level from [NotifySetter] attribute.
    /// </summary>
    private static string? GetSetterAccessLevel(IFieldSymbol field)
    {
        var setterAttr = field
            .GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == NotifySetterAttributeName);

        if (setterAttr == null || setterAttr.ConstructorArguments.Length == 0)
            return null;

        var accessLevel = (int)setterAttr.ConstructorArguments[0].Value!;
        return accessLevel switch
        {
            0 => null, // Public - same as property, no modifier needed
            1 => "protected",
            2 => "internal",
            3 => "private",
            4 => "protected internal",
            5 => "private protected",
            _ => null,
        };
    }

    private static string? GetAccessorAccessLevel(
        IMethodSymbol? accessor,
        Accessibility propertyAccessibility
    )
    {
        if (accessor == null || accessor.DeclaredAccessibility == propertyAccessibility)
            return null;

        return GetAccessibilityText(accessor.DeclaredAccessibility);
    }

    private static string GetAccessibilityText(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "public",
        };

    /// <summary>
    /// Determines whether emitting the type requires an unsafe declaration context.
    /// </summary>
    private static bool RequiresUnsafeContext(ITypeSymbol type)
    {
        return type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer
            || (type is IArrayTypeSymbol arrayType && RequiresUnsafeContext(arrayType.ElementType));
    }

    /// <summary>
    /// Determines if the type is a primitive value type that supports direct == comparison.
    /// </summary>
    private static bool IsPrimitiveValueType(ITypeSymbol type)
    {
        if (type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
            return true;

        // Handle Nullable<T> - get the underlying type
        if (
            type is INamedTypeSymbol namedType
            && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
        )
        {
            type = namedType.TypeArguments[0];
        }

        return type.IsValueType
            && type.SpecialType switch
            {
                SpecialType.System_Boolean => true,
                SpecialType.System_Char => true,
                SpecialType.System_SByte => true,
                SpecialType.System_Byte => true,
                SpecialType.System_Int16 => true,
                SpecialType.System_UInt16 => true,
                SpecialType.System_Int32 => true,
                SpecialType.System_UInt32 => true,
                SpecialType.System_Int64 => true,
                SpecialType.System_UInt64 => true,
                SpecialType.System_Single => true,
                SpecialType.System_Double => true,
                SpecialType.System_Decimal => true,
                _ => false,
            };
    }

    /// <summary>
    /// Generates the source code for a class.
    /// </summary>
    private static void GenerateSource(
        SourceProductionContext context,
        NotificationTypeInfo classInfo
    )
    {
        if (!classInfo.CanGenerate)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        if (classInfo.IsSuppressable)
            sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();

        var hasNamespace = !string.IsNullOrEmpty(classInfo.Namespace);
        if (hasNamespace)
        {
            sb.AppendLine($"namespace {classInfo.Namespace}");
            sb.AppendLine("{");
        }

        var indent = hasNamespace ? "    " : "";
        for (var index = 0; index < classInfo.TypeDeclarations.Length; index++)
        {
            var declaration = classInfo.TypeDeclarations[index];
            var isTarget = index == classInfo.TypeDeclarations.Length - 1;
            var requiredModifiers =
                declaration.RequiredModifiers.Length == 0
                    ? string.Empty
                    : string.Join(" ", declaration.RequiredModifiers) + " ";
            if (
                isTarget
                && classInfo.Fields.Any(static field => field.RequiresUnsafe)
                && !declaration.RequiredModifiers.Contains("unsafe")
            )
            {
                requiredModifiers += "unsafe ";
            }

            var interfaces = string.Empty;
            if (isTarget)
            {
                var interfaceList = new List<string>();
                if (!classInfo.AlreadyImplementsInpc)
                    interfaceList.Add("INotifyPropertyChanged");
                if (classInfo.ImplementChanging && !classInfo.AlreadyImplementsInpcChanging)
                    interfaceList.Add("INotifyPropertyChanging");
                interfaces =
                    interfaceList.Count == 0
                        ? string.Empty
                        : " : " + string.Join(", ", interfaceList);
            }

            sb.AppendLine(
                $"{indent}{declaration.Accessibility} {requiredModifiers}partial {declaration.Keyword} {declaration.Name}{declaration.TypeParameterList}{interfaces}"
            );
            sb.AppendLine($"{indent}{{");
            indent += "    ";
        }

        // PropertyChanged event (only if not already implemented)
        if (!classInfo.AlreadyImplementsInpc)
        {
            sb.AppendLine(
                $"{indent}    public event PropertyChangedEventHandler? PropertyChanged;"
            );
            sb.AppendLine();
        }

        // PropertyChanging event (only if ImplementChanging is true and not already implemented)
        if (classInfo.ImplementChanging && !classInfo.AlreadyImplementsInpcChanging)
        {
            sb.AppendLine(
                $"{indent}    public event PropertyChangingEventHandler? PropertyChanging;"
            );
            sb.AppendLine();
        }

        // Suppression fields (only if IsSuppressable)
        if (classInfo.IsSuppressable)
        {
            // Generate static HashSet for AlwaysNotify properties
            if (classInfo.AlwaysNotifyProperties.Length > 0)
            {
                sb.AppendLine(
                    $"{indent}    private static readonly HashSet<string> _neverSuppressedProperties = new()"
                );
                sb.AppendLine($"{indent}    {{");
                foreach (var prop in classInfo.AlwaysNotifyProperties)
                {
                    sb.AppendLine($"{indent}        \"{prop}\",");
                }
                sb.AppendLine($"{indent}    }};");
                sb.AppendLine();
            }

            sb.AppendLine($"{indent}    private int _notificationSuppressionCount;");
            sb.AppendLine($"{indent}    private HashSet<string>? _pendingNotifications;");
            sb.AppendLine();
        }

        if (classInfo.IsSuppressable && classInfo.AlreadyImplementsInpc)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}    private void __notifyGenRaisePropertyChanged(string propertyName)");
            sb.AppendLine($"{indent}    {{");
            if (classInfo.AlwaysNotifyProperties.Length > 0)
            {
                sb.AppendLine(
                    $"{indent}        if (_notificationSuppressionCount > 0 && !_neverSuppressedProperties.Contains(propertyName))"
                );
            }
            else
            {
                sb.AppendLine($"{indent}        if (_notificationSuppressionCount > 0)");
            }
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            _pendingNotifications ??= new HashSet<string>();");
            sb.AppendLine($"{indent}            _pendingNotifications.Add(propertyName);");
            sb.AppendLine($"{indent}            return;");
            sb.AppendLine($"{indent}        }}");
            AppendPropertyChangedVariableCall(
                sb,
                indent + "        ",
                classInfo.PropertyChangedInvoker
            );
            sb.AppendLine($"{indent}    }}");
        }

        // Generate child-property subscription state and handlers.
        foreach (var field in classInfo.Fields)
        {
            if (HasSubPropertySubscription(field))
            {
                GenerateSubPropertyMembers(
                    sb,
                    field,
                    indent,
                    classInfo.PropertyChangedInvoker,
                    classInfo.IsSuppressable && classInfo.AlreadyImplementsInpc
                );
                sb.AppendLine();
            }

            if (HasCollectionSubscription(field))
            {
                GenerateCollectionMembers(
                    sb,
                    field,
                    indent,
                    classInfo.PropertyChangedInvoker,
                    classInfo.IsSuppressable && classInfo.AlreadyImplementsInpc
                );
                sb.AppendLine();
            }
        }

        // Generate properties
        foreach (var field in classInfo.Fields)
        {
            GenerateProperty(
                sb,
                field,
                indent,
                classInfo.ImplementChanging,
                classInfo.MemberNames,
                classInfo.PropertyChangedInvoker,
                classInfo.PropertyChangingInvoker,
                classInfo.IsSuppressable && classInfo.AlreadyImplementsInpc
            );
            sb.AppendLine();
        }

        // OnPropertyChanged method (only if not already implemented)
        if (!classInfo.AlreadyImplementsInpc)
        {
            sb.AppendLine(
                $"{indent}    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)"
            );
            sb.AppendLine($"{indent}    {{");
            if (classInfo.IsSuppressable)
            {
                if (classInfo.AlwaysNotifyProperties.Length > 0)
                {
                    // Check if property should never be suppressed
                    sb.AppendLine(
                        $"{indent}        if (_notificationSuppressionCount > 0 && !_neverSuppressedProperties.Contains(propertyName ?? \"\"))"
                    );
                }
                else
                {
                    sb.AppendLine($"{indent}        if (_notificationSuppressionCount > 0)");
                }
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine(
                    $"{indent}            _pendingNotifications ??= new HashSet<string>();"
                );
                sb.AppendLine($"{indent}            _pendingNotifications.Add(propertyName!);");
                sb.AppendLine($"{indent}            return;");
                sb.AppendLine($"{indent}        }}");
            }
            sb.AppendLine(
                $"{indent}        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));"
            );
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
        }

        // OnPropertyChanging method (only if ImplementChanging is true and not already implemented)
        if (classInfo.ImplementChanging && !classInfo.AlreadyImplementsInpcChanging)
        {
            sb.AppendLine(
                $"{indent}    protected virtual void OnPropertyChanging([CallerMemberName] string? propertyName = null)"
            );
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine(
                $"{indent}        PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));"
            );
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
        }

        // Generate partial hooks
        foreach (var field in classInfo.Fields)
        {
            sb.AppendLine(
                $"{indent}    partial void On{field.PropertyName}Changing({field.TypeName} oldValue, {field.TypeName} newValue);"
            );
            sb.AppendLine($"{indent}    partial void On{field.PropertyName}Changed();");
            if (!field.HasNonPartialTypedChangedHook)
            {
                sb.AppendLine(
                    $"{indent}    partial void On{field.PropertyName}Changed({field.TypeName} oldValue, {field.TypeName} newValue);"
                );
            }
        }

        // Suppression methods (only if IsSuppressable)
        if (classInfo.IsSuppressable)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine(
                $"{indent}    /// Suppresses PropertyChanged notifications until the returned IDisposable is disposed."
            );
            sb.AppendLine($"{indent}    /// Supports nested suppression scopes.");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public IDisposable SuppressNotifications()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        _notificationSuppressionCount++;");
            sb.AppendLine($"{indent}        return new NotificationSuppressor(this);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    private void ResumeNotifications()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine(
                $"{indent}        if (--_notificationSuppressionCount == 0 && _pendingNotifications != null)"
            );
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine(
                $"{indent}            foreach (var propertyName in _pendingNotifications)"
            );
            sb.AppendLine($"{indent}            {{");
            if (classInfo.AlreadyImplementsInpc)
            {
                AppendPropertyChangedVariableCall(
                    sb,
                    indent + "                ",
                    classInfo.PropertyChangedInvoker
                );
            }
            else
            {
                sb.AppendLine(
                    $"{indent}                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));"
                );
            }
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}            _pendingNotifications.Clear();");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    private sealed class NotificationSuppressor : IDisposable");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine(
                $"{indent}        private readonly {classInfo.TargetType.Name}{classInfo.TargetType.TypeParameterList} _owner;"
            );
            sb.AppendLine(
                $"{indent}        public NotificationSuppressor({classInfo.TargetType.Name}{classInfo.TargetType.TypeParameterList} owner) => _owner = owner;"
            );
            sb.AppendLine(
                $"{indent}        public void Dispose() => _owner.ResumeNotifications();"
            );
            sb.AppendLine($"{indent}    }}");
        }

        for (var index = classInfo.TypeDeclarations.Length - 1; index >= 0; index--)
        {
            indent = indent.Substring(0, indent.Length - 4);
            sb.AppendLine($"{indent}}}");
        }

        if (hasNamespace)
        {
            sb.AppendLine("}");
        }

        var sourceText = SourceText.From(sb.ToString(), Encoding.UTF8);
        context.AddSource(
            SourceHintName.Create(classInfo.TargetType.MetadataIdentity, classInfo.TargetType.Name),
            sourceText
        );
    }

    private static bool HasSubPropertySubscription(FieldInfo field) =>
        field.SubPropertyNotify.Length > 0 && !field.RequiresUnsafe;

    private static bool HasCollectionSubscription(FieldInfo field) =>
        field.CollectionNotify.Length > 0 && !field.RequiresUnsafe;

    private static string GetSubPropertyMemberPrefix(FieldInfo field)
    {
        var builder = new StringBuilder("__notifyGenSubProperty_");
        foreach (var character in field.PropertyName)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in field.PropertyName + "|" + field.FieldName)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            builder.Append('_').Append(hash.ToString("X8"));
        }

        return builder.ToString();
    }

    private static string GetCollectionMemberPrefix(FieldInfo field)
    {
        var builder = new StringBuilder("__notifyGenCollection_");
        foreach (var character in field.PropertyName)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in field.PropertyName + "|" + field.FieldName)
            {
                hash ^= character;
                hash *= 16777619;
            }

            builder.Append('_').Append(hash.ToString("X8"));
        }

        return builder.ToString();
    }

    private static void GenerateSubPropertyMembers(
        StringBuilder sb,
        FieldInfo field,
        string indent,
        PropertyChangedInvokerKind invoker,
        bool useSuppressionWrapper
    )
    {
        var prefix = GetSubPropertyMemberPrefix(field);
        sb.AppendLine(
            $"{indent}    private global::System.ComponentModel.INotifyPropertyChanged? {prefix}Source;"
        );
        sb.AppendLine($"{indent}    private bool {prefix}Initialized;");
        sb.AppendLine($"{indent}    private void {prefix}Changed(");
        sb.AppendLine(
            $"{indent}        object? sender, global::System.ComponentModel.PropertyChangedEventArgs e"
        );
        sb.AppendLine($"{indent}    )");
        sb.AppendLine($"{indent}    {{");
        foreach (var propertyName in field.SubPropertyNotify)
        {
            AppendPropertyChangedCall(
                sb,
                indent + "        ",
                propertyName,
                invoker,
                useSuppressionWrapper: useSuppressionWrapper
            );
        }
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    private void {prefix}Ensure(object? currentValue)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        if ({prefix}Initialized)");
        sb.AppendLine($"{indent}            return;");
        sb.AppendLine();
        sb.AppendLine($"{indent}        {prefix}Initialized = true;");
        sb.AppendLine(
            $"{indent}        if (currentValue is global::System.ComponentModel.INotifyPropertyChanged currentSource)"
        );
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            {prefix}Source = currentSource;");
        sb.AppendLine(
            $"{indent}            currentSource.PropertyChanged += {prefix}Changed;"
        );
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    private void {prefix}Update(object? newValue)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        {prefix}Ensure(newValue);");
        sb.AppendLine($"{indent}        if ({prefix}Source is not null)");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine(
            $"{indent}            {prefix}Source.PropertyChanged -= {prefix}Changed;"
        );
        sb.AppendLine($"{indent}            {prefix}Source = null;");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine();
        sb.AppendLine(
            $"{indent}        if (newValue is global::System.ComponentModel.INotifyPropertyChanged newSource)"
        );
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            {prefix}Source = newSource;");
        sb.AppendLine(
            $"{indent}            newSource.PropertyChanged += {prefix}Changed;"
        );
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
    }

    private static void GenerateCollectionMembers(
        StringBuilder sb,
        FieldInfo field,
        string indent,
        PropertyChangedInvokerKind invoker,
        bool useSuppressionWrapper
    )
    {
        var prefix = GetCollectionMemberPrefix(field);
        sb.AppendLine(
            $"{indent}    private global::System.Collections.Specialized.INotifyCollectionChanged? {prefix}Source;"
        );
        sb.AppendLine($"{indent}    private bool {prefix}Initialized;");
        sb.AppendLine($"{indent}    private void {prefix}Changed(");
        sb.AppendLine(
            $"{indent}        object? sender, global::System.Collections.Specialized.NotifyCollectionChangedEventArgs e"
        );
        sb.AppendLine($"{indent}    )");
        sb.AppendLine($"{indent}    {{");
        foreach (var propertyName in field.CollectionNotify)
        {
            AppendPropertyChangedCall(
                sb,
                indent + "        ",
                propertyName,
                invoker,
                useSuppressionWrapper: useSuppressionWrapper
            );
        }
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    private void {prefix}Ensure(object? currentValue)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        if ({prefix}Initialized)");
        sb.AppendLine($"{indent}            return;");
        sb.AppendLine();
        sb.AppendLine($"{indent}        {prefix}Initialized = true;");
        sb.AppendLine(
            $"{indent}        if (currentValue is global::System.Collections.Specialized.INotifyCollectionChanged currentSource)"
        );
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            {prefix}Source = currentSource;");
        sb.AppendLine($"{indent}            currentSource.CollectionChanged += {prefix}Changed;");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    private void {prefix}Update(object? newValue)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        {prefix}Ensure(newValue);");
        sb.AppendLine($"{indent}        if ({prefix}Source is not null)");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            {prefix}Source.CollectionChanged -= {prefix}Changed;");
        sb.AppendLine($"{indent}            {prefix}Source = null;");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine();
        sb.AppendLine(
            $"{indent}        if (newValue is global::System.Collections.Specialized.INotifyCollectionChanged newSource)"
        );
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            {prefix}Source = newSource;");
        sb.AppendLine($"{indent}            newSource.CollectionChanged += {prefix}Changed;");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
    }

    private static void AppendPropertyChangedCall(
        StringBuilder sb,
        string indent,
        string propertyName,
        PropertyChangedInvokerKind invoker,
        bool useSuppressionWrapper = false
    )
    {
        var argument = QuoteString(propertyName);
        if (useSuppressionWrapper)
        {
            sb.AppendLine($"{indent}__notifyGenRaisePropertyChanged({argument});");
            return;
        }
        if (invoker == PropertyChangedInvokerKind.EventArgs)
        {
            sb.AppendLine(
                $"{indent}OnPropertyChanged(new global::System.ComponentModel.PropertyChangedEventArgs({argument}));"
            );
        }
        else
        {
            sb.AppendLine($"{indent}OnPropertyChanged({argument});");
        }
    }

    private static void AppendPropertyChangedVariableCall(
        StringBuilder sb,
        string indent,
        PropertyChangedInvokerKind invoker
    )
    {
        if (invoker == PropertyChangedInvokerKind.EventArgs)
        {
            sb.AppendLine(
                $"{indent}OnPropertyChanged(new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));"
            );
        }
        else
        {
            sb.AppendLine($"{indent}OnPropertyChanged(propertyName);");
        }
    }

    private static void AppendPropertyChangingCall(
        StringBuilder sb,
        string indent,
        string propertyName,
        PropertyChangingInvokerKind invoker
    )
    {
        var argument = QuoteString(propertyName);
        if (invoker == PropertyChangingInvokerKind.EventArgs)
        {
            sb.AppendLine(
                $"{indent}OnPropertyChanging(new global::System.ComponentModel.PropertyChangingEventArgs({argument}));"
            );
        }
        else
        {
            sb.AppendLine($"{indent}OnPropertyChanging({argument});");
        }
    }

    private static string EnsureNonNull(string expression) =>
        expression.EndsWith("!", StringComparison.Ordinal) ? expression : $"{expression}!";

    private static string GetOldValueLocalName(ImmutableArray<string> memberNames)
    {
        const string baseName = "__notifyGenOldValue";
        var candidate = baseName;
        var suffix = 0;
        while (memberNames.Contains(candidate, StringComparer.Ordinal))
        {
            suffix++;
            candidate = $"{baseName}{suffix}";
        }

        return candidate;
    }

    /// <summary>
    /// Generates a single property.
    /// </summary>
    private static void GenerateProperty(
        StringBuilder sb,
        FieldInfo field,
        string indent,
        bool implementChanging,
        ImmutableArray<string> memberNames,
        PropertyChangedInvokerKind invoker,
        PropertyChangingInvokerKind changingInvoker,
        bool useSuppressionWrapper
    )
    {
        if (!field.PropertyAttributes.IsDefaultOrEmpty)
        {
            foreach (var attribute in field.PropertyAttributes)
            {
                sb.AppendLine($"{indent}    {attribute}");
            }
        }

        if (field.IsPartialProperty && field.NeedsNullableBackingField)
        {
            sb.AppendLine(
                $"{indent}    [field: global::System.Diagnostics.CodeAnalysis.MaybeNull, global::System.Diagnostics.CodeAnalysis.AllowNull]"
            );
        }

        var partialModifier = field.IsPartialProperty ? " partial" : string.Empty;
        var backingValue = field.NeedsNullableBackingField
            ? $"{field.FieldName}!"
            : field.FieldName;
        var hasSubPropertySubscription = HasSubPropertySubscription(field);
        var subPropertyPrefix = hasSubPropertySubscription
            ? GetSubPropertyMemberPrefix(field)
            : string.Empty;
        var hasCollectionSubscription = HasCollectionSubscription(field);
        var collectionPrefix = hasCollectionSubscription
            ? GetCollectionMemberPrefix(field)
            : string.Empty;
        sb.AppendLine(
            $"{indent}    {field.PropertyAccessibility}{partialModifier} {field.TypeName} {field.PropertyName}"
        );
        sb.AppendLine($"{indent}    {{");
        var getterModifier = field.GetterAccess != null ? $"{field.GetterAccess} " : "";
        if (
            hasSubPropertySubscription
            || hasCollectionSubscription
            || !field.GetterAttributes.IsDefaultOrEmpty
        )
        {
            if (!field.GetterAttributes.IsDefaultOrEmpty)
            {
                foreach (var attribute in field.GetterAttributes)
                    sb.AppendLine($"{indent}        {attribute}");
            }

            sb.AppendLine($"{indent}        {getterModifier}get");
            sb.AppendLine($"{indent}        {{");
            if (hasSubPropertySubscription)
                sb.AppendLine($"{indent}            {subPropertyPrefix}Ensure({backingValue});");
            if (hasCollectionSubscription)
                sb.AppendLine($"{indent}            {collectionPrefix}Ensure({backingValue});");
            sb.AppendLine($"{indent}            return {backingValue};");
            sb.AppendLine($"{indent}        }}");
        }
        else
        {
            sb.AppendLine($"{indent}        {getterModifier}get => {backingValue};");
        }
        var setterModifier = field.SetterAccess != null ? $"{field.SetterAccess} " : "";
        if (!field.SetterAttributes.IsDefaultOrEmpty)
        {
            foreach (var attribute in field.SetterAttributes)
                sb.AppendLine($"{indent}        {attribute}");
        }
        sb.AppendLine($"{indent}        {setterModifier}set");
        sb.AppendLine($"{indent}        {{");
        if (hasSubPropertySubscription)
        {
            sb.AppendLine($"{indent}            {subPropertyPrefix}Ensure({backingValue});");
        }
        if (hasCollectionSubscription)
        {
            sb.AppendLine($"{indent}            {collectionPrefix}Ensure({backingValue});");
        }
        // Pointer-like types use native-int equality to avoid function-pointer comparison warnings.
        if (field.IsPrimitiveType && field.RequiresUnsafe)
        {
            sb.AppendLine(
                $"{indent}            if ((nint){field.FieldName} == (nint)value) return;"
            );
        }
        else if (field.IsPrimitiveType)
        {
            sb.AppendLine($"{indent}            if ({backingValue} == value) return;");
        }
        else
        {
            sb.AppendLine(
                $"{indent}            if (EqualityComparer<{field.TypeName}>.Default.Equals({backingValue}, value)) return;"
            );
        }
        var oldValueLocalName = GetOldValueLocalName(memberNames);
        var isDynamicType = field.TypeName == "dynamic" || field.TypeName == "dynamic?";
        var oldValueArgument = isDynamicType
            ? $"((global::System.Object?){oldValueLocalName})!"
            : oldValueLocalName;
        var newValueArgument = isDynamicType
            ? "((global::System.Object?)value)!"
            : "value";
        var changingOldValueArgument = isDynamicType ? oldValueArgument : backingValue;
        var changingNewValueArgument = isDynamicType ? newValueArgument : "value";
        var existingOldHookType = field.ExistingTypedChangedHookParameterTypeName;
        var existingNewHookType = field.ExistingTypedChangedHookNewParameterTypeName;
        var typedOldValueArgument = existingOldHookType is not null
            ? $"({existingOldHookType})({EnsureNonNull(oldValueArgument)})"
            : field.IsNullable && !isDynamicType
                ? $"{oldValueArgument}!"
                : oldValueArgument;
        var typedNewValueArgument = existingNewHookType is not null
            ? $"({existingNewHookType})({EnsureNonNull(newValueArgument)})"
            : field.IsNullable && !isDynamicType
                ? $"{newValueArgument}!"
                : newValueArgument;
        sb.AppendLine($"{indent}            var {oldValueLocalName} = {backingValue};");
        // Fire PropertyChanging event if enabled
        if (implementChanging)
        {
            if (changingInvoker == PropertyChangingInvokerKind.Generated)
                sb.AppendLine($"{indent}            OnPropertyChanging();");
            else
                AppendPropertyChangingCall(
                    sb,
                    indent + "            ",
                    field.PropertyName,
                    changingInvoker
                );
        }
        sb.AppendLine(
            $"{indent}            On{field.PropertyName}Changing({changingOldValueArgument}, {changingNewValueArgument});"
        );
        sb.AppendLine($"{indent}            {field.FieldName} = value;");
        if (hasSubPropertySubscription)
        {
            sb.AppendLine($"{indent}            {subPropertyPrefix}Update(value);");
        }
        if (hasCollectionSubscription)
        {
            sb.AppendLine($"{indent}            {collectionPrefix}Update(value);");
        }
        if (invoker == PropertyChangedInvokerKind.Generated)
            sb.AppendLine($"{indent}            OnPropertyChanged();");
        else
            AppendPropertyChangedCall(
                sb,
                indent + "            ",
                field.PropertyName,
                invoker,
                useSuppressionWrapper: useSuppressionWrapper
            );

        // NotifyAlso properties
        foreach (var alsoNotify in field.AlsoNotify)
        {
            AppendPropertyChangedCall(
                sb,
                indent + "            ",
                alsoNotify,
                invoker,
                useSuppressionWrapper: useSuppressionWrapper
            );
        }

        // NotifyCanExecuteChanged for commands (requires IRelayCommand or compatible type)
        foreach (var command in field.CommandsToNotify)
        {
            sb.AppendLine($"{indent}            {command}?.NotifyCanExecuteChanged();");
        }

        sb.AppendLine($"{indent}            On{field.PropertyName}Changed();");
        sb.AppendLine(
            $"{indent}            On{field.PropertyName}Changed({typedOldValueArgument}, {typedNewValueArgument});"
        );
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
    }

}
